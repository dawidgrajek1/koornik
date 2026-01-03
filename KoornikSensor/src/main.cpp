#include <Arduino.h>
#include <Wire.h>
#include <WiFi.h>
#include <Preferences.h>
#include <PubSubClient.h>
#include <Adafruit_VL53L0X.h>
#include <ESPAsyncWebServer.h>
#include <AsyncTCP.h>

// ==================== PIN DEFINITIONS ====================
#define LED_PIN 2           // Onboard LED
#define RESET_PIN 23        // Short to GND to enter config mode

#define I2C0_SDA 21         // Front sensor I2C
#define I2C0_SCL 22
#define I2C1_SDA 18         // Back sensor I2C
#define I2C1_SCL 19

// ==================== CONFIGURATION ====================
#define AP_SSID "ToF-Setup"
#define MQTT_PORT 1883
#define MQTT_TOPIC_PREFIX "koornik"

#define DEFAULT_THRESHOLD 1000       // Default detection threshold in mm
#define DISTANCE_STREAM_INTERVAL 500  // Live distance streaming interval in ms
#define STATUS_PUBLISH_INTERVAL 10000 // Status publish interval in ms
#define CALIBRATION_MARGIN 100      // Margin to subtract from calibrated distance
#define CALIBRATION_SAMPLES 10      // Number of samples for calibration
#define DETECTION_TIMEOUT 2000      // Timeout for direction detection in ms
#define DETECTION_COOLDOWN 1000     // Cooldown after detection before allowing new detection
#define MIN_TRIGGER_INTERVAL 50     // Minimum time between first and second sensor trigger (ms)
#define WIFI_CONNECT_TIMEOUT 30000  // WiFi connection timeout in ms
#define MQTT_RETRY_INTERVAL 5000    // MQTT reconnection interval in ms

// ==================== LED BLINK PATTERNS ====================
#define LED_BLINK_AP 1000           // Slow blink for AP mode
#define LED_BLINK_CONNECTING 200    // Fast blink while connecting

// ==================== GLOBAL OBJECTS ====================
TwoWire I2C_0 = TwoWire(0);
TwoWire I2C_1 = TwoWire(1);

Adafruit_VL53L0X sensorFront;
Adafruit_VL53L0X sensorBack;

WiFiClient espClient;
PubSubClient mqtt(espClient);
AsyncWebServer server(80);
Preferences preferences;

// ==================== STATE VARIABLES ====================
enum DeviceMode { MODE_AP, MODE_NORMAL };
enum DetectionState { IDLE, FRONT_FIRST, BACK_FIRST, COOLDOWN };

DeviceMode deviceMode = MODE_NORMAL;
DetectionState detectionState = IDLE;

String savedSSID = "";
String savedPassword = "";
String savedMqttBroker = "";

// MAC address (without colons) for topic identification
String deviceMac = "";

// Dynamic MQTT topics (built after WiFi connects)
String topicTransition = "";
String topicStatus = "";
String topicCommand = "";
String topicDistance = "";

int thresholdFront = DEFAULT_THRESHOLD;
int thresholdBack = DEFAULT_THRESHOLD;

unsigned long detectionStartTime = 0;
unsigned long lastMqttAttempt = 0;
unsigned long lastLedToggle = 0;
unsigned long lastDistancePublish = 0;
unsigned long lastStatusPublish = 0;
bool ledState = false;
bool sensorsInitialized = false;
bool distanceStreamEnabled = false;

// ==================== HTML PAGE ====================
const char index_html[] PROGMEM = R"rawliteral(
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
    <title>ToF Sensor Setup</title>
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }
        .container {
            background: white;
            border-radius: 20px;
            padding: 30px;
            width: 100%;
            max-width: 400px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
        }
        h1 {
            text-align: center;
            color: #333;
            margin-bottom: 10px;
            font-size: 24px;
        }
        .subtitle {
            text-align: center;
            color: #666;
            margin-bottom: 25px;
            font-size: 14px;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            margin-bottom: 8px;
            color: #333;
            font-weight: 600;
            font-size: 14px;
        }
        select, input[type="text"], input[type="password"] {
            width: 100%;
            padding: 15px;
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            font-size: 16px;
            transition: border-color 0.3s;
            -webkit-appearance: none;
            appearance: none;
        }
        select:focus, input:focus {
            outline: none;
            border-color: #667eea;
        }
        select {
            background: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%23333' d='M6 8L1 3h10z'/%3E%3C/svg%3E") no-repeat right 15px center;
            background-color: white;
            cursor: pointer;
        }

        button {
            width: 100%;
            padding: 16px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
            min-height: 54px;
        }
        button:hover {
            transform: translateY(-2px);
            box-shadow: 0 5px 20px rgba(102, 126, 234, 0.4);
        }
        button:active {
            transform: translateY(0);
        }

        .status {
            text-align: center;
            margin-top: 15px;
            padding: 10px;
            border-radius: 8px;
            font-size: 14px;
        }
        .status.error {
            background: #ffe0e0;
            color: #c00;
        }
        .status.success {
            background: #e0ffe0;
            color: #060;
        }
        .loading {
            display: inline-block;
            width: 16px;
            height: 16px;
            border: 2px solid #ccc;
            border-top-color: #667eea;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin-right: 8px;
            vertical-align: middle;
        }
        @keyframes spin {
            to { transform: rotate(360deg); }
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>ToF Sensor Setup</h1>
        <p class="subtitle">Configure WiFi and MQTT connection</p>
        
        <form id="configForm" action="/save" method="POST">
            <div class="form-group">
                <label for="ssid">WiFi Network Name (SSID)</label>
                <input type="text" id="ssid" name="ssid" placeholder="Enter network name" required>
            </div>
            
            <div class="form-group">
                <label for="password">WiFi Password</label>
                <input type="password" id="password" name="password" placeholder="Enter password">
            </div>
            
            <div class="form-group">
                <label for="mqtt">MQTT Broker IP</label>
                <input type="text" id="mqtt" name="mqtt" placeholder="e.g., 192.168.1.100" required>
            </div>
            
            <button type="submit">Save & Connect</button>
            
            <div id="status" class="status hidden"></div>
        </form>
    </div>
    
    <script>
        document.getElementById('configForm').addEventListener('submit', function(e) {
            e.preventDefault();
            
            const ssid = document.getElementById('ssid').value;
            const password = document.getElementById('password').value;
            const mqttBroker = document.getElementById('mqtt').value;
            
            if (!ssid) {
                showStatus('Please enter a network name', 'error');
                return;
            }
            
            if (!mqttBroker) {
                showStatus('Please enter MQTT broker IP', 'error');
                return;
            }
            
            const formData = new FormData();
            formData.append('ssid', ssid);
            formData.append('password', password);
            formData.append('mqtt', mqttBroker);
            
            fetch('/save', {
                method: 'POST',
                body: formData
            })
            .then(response => response.text())
            .then(result => {
                showStatus('Settings saved! Rebooting...', 'success');
                setTimeout(() => {
                    showStatus('Device is rebooting. Please connect to your WiFi network.', 'success');
                }, 2000);
            })
            .catch(err => {
                showStatus('Save failed: ' + err, 'error');
            });
        });
        
        function showStatus(message, type) {
            const status = document.getElementById('status');
            status.textContent = message;
            status.className = 'status ' + type;
            status.classList.remove('hidden');
        }
    </script>
</body>
</html>
)rawliteral";

// ==================== FUNCTION DECLARATIONS ====================
void setupPins();
void loadPreferences();
void savePreferences();
void clearPreferences();
bool connectToWiFi();
void startAPMode();
void setupWebServer();
void setupMQTT();
void mqttCallback(char* topic, byte* payload, unsigned int length);
void reconnectMQTT();
void initSensors();
void calibrateSensors();
void detectDirection();
void updateLED();
void publishStatus();

// ==================== SETUP ====================
void setup() {
    Serial.begin(115200);
    delay(1000);
    Serial.println("\n\n=== ToF Direction Sensor ===");
    
    setupPins();
    
    // Check if reset pin is shorted to GND
    if (digitalRead(RESET_PIN) == LOW) {
        Serial.println("Reset pin LOW - clearing preferences and entering AP mode");
        delay(100);  // Debounce
        if (digitalRead(RESET_PIN) == LOW) {
            clearPreferences();
            startAPMode();
            return;
        }
    }
    
    // Load saved preferences
    loadPreferences();
    
    // Try to connect to WiFi
    if (savedSSID.length() > 0) {
        if (connectToWiFi()) {
            deviceMode = MODE_NORMAL;
            initSensors();
            setupMQTT();
        } else {
            startAPMode();
        }
    } else {
        Serial.println("No saved WiFi credentials - starting AP mode");
        startAPMode();
    }
}

// ==================== MAIN LOOP ====================
void loop() {
    updateLED();
    
    if (deviceMode == MODE_AP) {
        // AP mode - just handle web server (async)
        return;
    }
    
    // Normal mode - handle MQTT and sensors
    if (!mqtt.connected()) {
        reconnectMQTT();
    }
    mqtt.loop();
    
    // Publish status periodically
    publishStatus();
    
    // Read sensors and detect direction
    if (sensorsInitialized) {
        detectDirection();
    }
}

// ==================== PIN SETUP ====================
void setupPins() {
    pinMode(LED_PIN, OUTPUT);
    digitalWrite(LED_PIN, LOW);
    
    pinMode(RESET_PIN, INPUT_PULLUP);
}

// ==================== PREFERENCES ====================
void loadPreferences() {
    preferences.begin("tof-config", true);  // Read-only
    savedSSID = preferences.getString("ssid", "");
    savedPassword = preferences.getString("password", "");
    savedMqttBroker = preferences.getString("mqtt", "");
    thresholdFront = preferences.getInt("thresh_front", DEFAULT_THRESHOLD);
    thresholdBack = preferences.getInt("thresh_back", DEFAULT_THRESHOLD);
    preferences.end();
    
    Serial.println("Loaded preferences:");
    Serial.println("  SSID: " + savedSSID);
    Serial.println("  MQTT: " + savedMqttBroker);
    Serial.printf("  Thresholds: Front=%d, Back=%d\n", thresholdFront, thresholdBack);
}

void savePreferences() {
    preferences.begin("tof-config", false);  // Read-write
    preferences.putString("ssid", savedSSID);
    preferences.putString("password", savedPassword);
    preferences.putString("mqtt", savedMqttBroker);
    preferences.putInt("thresh_front", thresholdFront);
    preferences.putInt("thresh_back", thresholdBack);
    preferences.end();
    Serial.println("Preferences saved");
}

void clearPreferences() {
    preferences.begin("tof-config", false);
    preferences.clear();
    preferences.end();
    Serial.println("Preferences cleared");
}

// ==================== WIFI ====================
String getMacAddressClean() {
    String mac = WiFi.macAddress();
    mac.replace(":", "");
    mac.toUpperCase();
    return mac;
}

void buildMqttTopics() {
    deviceMac = getMacAddressClean();
    topicTransition = String(MQTT_TOPIC_PREFIX) + "/" + deviceMac + "/transition";
    topicStatus = String(MQTT_TOPIC_PREFIX) + "/" + deviceMac + "/status";
    topicCommand = String(MQTT_TOPIC_PREFIX) + "/" + deviceMac + "/command";
    topicDistance = String(MQTT_TOPIC_PREFIX) + "/" + deviceMac + "/distance";
    
    Serial.println("Device MAC: " + deviceMac);
    Serial.println("Topics configured:");
    Serial.println("  Transition: " + topicTransition);
    Serial.println("  Status: " + topicStatus);
    Serial.println("  Command: " + topicCommand);
    Serial.println("  Distance: " + topicDistance);
}

bool connectToWiFi() {
    Serial.println("Connecting to WiFi: " + savedSSID);
    
    WiFi.mode(WIFI_STA);
    WiFi.begin(savedSSID.c_str(), savedPassword.c_str());
    
    unsigned long startTime = millis();
    while (WiFi.status() != WL_CONNECTED) {
        if (millis() - startTime > WIFI_CONNECT_TIMEOUT) {
            Serial.println("\nWiFi connection timeout!");
            return false;
        }
        
        // Fast blink while connecting
        if (millis() - lastLedToggle > LED_BLINK_CONNECTING) {
            ledState = !ledState;
            digitalWrite(LED_PIN, ledState);
            lastLedToggle = millis();
        }
        
        delay(100);
        Serial.print(".");
    }
    
    Serial.println("\nWiFi connected!");
    Serial.println("IP address: " + WiFi.localIP().toString());
    digitalWrite(LED_PIN, HIGH);  // Solid ON when connected
    
    // Build MQTT topics now that we have MAC address
    buildMqttTopics();
    
    return true;
}

void startAPMode() {
    deviceMode = MODE_AP;
    Serial.println("Starting AP mode...");
    
    WiFi.mode(WIFI_AP);
    WiFi.softAP(AP_SSID);
    
    Serial.println("AP SSID: " + String(AP_SSID));
    Serial.println("AP IP: " + WiFi.softAPIP().toString());
    
    setupWebServer();
}

// ==================== WEB SERVER ====================
void setupWebServer() {
    // Serve main page
    server.on("/", HTTP_GET, [](AsyncWebServerRequest *request) {
        request->send_P(200, "text/html", index_html);
    });
    
    // Save configuration endpoint
    server.on("/save", HTTP_POST, [](AsyncWebServerRequest *request) {
        if (request->hasParam("ssid", true) && request->hasParam("mqtt", true)) {
            savedSSID = request->getParam("ssid", true)->value();
            savedPassword = request->getParam("password", true)->value();
            savedMqttBroker = request->getParam("mqtt", true)->value();
            
            Serial.println("Received config:");
            Serial.println("  SSID: " + savedSSID);
            Serial.println("  MQTT: " + savedMqttBroker);
            
            savePreferences();
            
            request->send(200, "text/plain", "OK");
            
            // Reboot after a short delay
            delay(1000);
            ESP.restart();
        } else {
            request->send(400, "text/plain", "Missing parameters");
        }
    });
    
    server.begin();
    Serial.println("Web server started");
}

// ==================== MQTT ====================
void setupMQTT() {
    mqtt.setServer(savedMqttBroker.c_str(), MQTT_PORT);
    mqtt.setCallback(mqttCallback);
    mqtt.setBufferSize(512);  // Increase buffer for JSON payloads
    reconnectMQTT();
}

void parseJsonCommand(const String& message) {
    // Simple JSON parsing for commands
    // Expected format: {"action":"calibrate"} or {"action":"enableDistance","enabled":true}
    // or {"action":"updateSettings","payload":{"thresholdFront":400,"thresholdBack":400}}
    
    if (message.indexOf("\"calibrate\"") >= 0) {
        calibrateSensors();
    }
    else if (message.indexOf("\"enableDistance\"") >= 0) {
        distanceStreamEnabled = message.indexOf("\"enabled\":true") >= 0 || 
                                message.indexOf("\"enabled\": true") >= 0;
        Serial.printf("Distance streaming %s\n", distanceStreamEnabled ? "enabled" : "disabled");
    }
    else if (message.indexOf("\"updateSettings\"") >= 0) {
        // Parse threshold settings from payload
        int frontIdx = message.indexOf("\"thresholdFront\":");
        int backIdx = message.indexOf("\"thresholdBack\":");
        
        if (frontIdx >= 0) {
            int start = frontIdx + 17;
            int end = message.indexOf(",", start);
            if (end < 0) end = message.indexOf("}", start);
            if (end > start) {
                thresholdFront = message.substring(start, end).toInt();
                if (thresholdFront < 50) thresholdFront = 50;
            }
        }
        
        if (backIdx >= 0) {
            int start = backIdx + 16;
            int end = message.indexOf(",", start);
            if (end < 0) end = message.indexOf("}", start);
            if (end > start) {
                thresholdBack = message.substring(start, end).toInt();
                if (thresholdBack < 50) thresholdBack = 50;
            }
        }
        
        savePreferences();
        Serial.printf("Settings updated: Front=%d, Back=%d\n", thresholdFront, thresholdBack);
        
        // Publish updated status
        String statusJson = "{\"state\":\"settings_updated\",\"thresholds\":{\"front\":" + 
                            String(thresholdFront) + ",\"back\":" + String(thresholdBack) + "}}";
        mqtt.publish(topicStatus.c_str(), statusJson.c_str());
    }
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
    String message = "";
    for (unsigned int i = 0; i < length; i++) {
        message += (char)payload[i];
    }
    
    Serial.printf("MQTT message [%s]: %s\n", topic, message.c_str());
    
    if (String(topic) == topicCommand) {
        parseJsonCommand(message);
    }
}

void reconnectMQTT() {
    if (mqtt.connected()) return;
    
    unsigned long now = millis();
    if (now - lastMqttAttempt < MQTT_RETRY_INTERVAL) return;
    lastMqttAttempt = now;
    
    Serial.println("Attempting MQTT connection...");
    
    String clientId = "ToF-" + deviceMac;
    if (mqtt.connect(clientId.c_str())) {
        Serial.println("MQTT connected");
        
        // Subscribe to command topic
        mqtt.subscribe(topicCommand.c_str());
        Serial.println("Subscribed to: " + topicCommand);
        
        // Publish online status as JSON (same format as periodic status)
        String statusJson = "{\"state\":\"online\",\"thresholds\":{\"front\":" + 
                            String(thresholdFront) + ",\"back\":" + String(thresholdBack) + 
                            "},\"sensors\":" + (sensorsInitialized ? "true" : "false") +
                            ",\"rssi\":" + String(WiFi.RSSI()) + "}";
        mqtt.publish(topicStatus.c_str(), statusJson.c_str());
        lastStatusPublish = millis();  // Reset timer to avoid immediate duplicate
    } else {
        Serial.printf("MQTT connection failed, rc=%d\n", mqtt.state());
    }
}

// ==================== SENSORS ====================
void initSensors() {
    Serial.println("Initializing I2C buses...");
    
    I2C_0.begin(I2C0_SDA, I2C0_SCL, 400000);
    I2C_1.begin(I2C1_SDA, I2C1_SCL, 400000);
    
    Serial.println("Initializing VL53L0X sensors...");
    
    if (!sensorFront.begin(0x29, false, &I2C_0)) {
        Serial.println("ERROR: Failed to initialize front sensor!");
        sensorsInitialized = false;
        return;
    }
    Serial.println("Front sensor initialized");
    
    if (!sensorBack.begin(0x29, false, &I2C_1)) {
        Serial.println("ERROR: Failed to initialize back sensor!");
        sensorsInitialized = false;
        return;
    }
    Serial.println("Back sensor initialized");
    
    sensorsInitialized = true;
    Serial.println("All sensors initialized successfully");
}

void calibrateSensors() {
    if (!sensorsInitialized) {
        Serial.println("Cannot calibrate - sensors not initialized");
        String errorJson = "{\"state\":\"calibration_failed\",\"error\":\"sensors_not_ready\"}";
        mqtt.publish(topicStatus.c_str(), errorJson.c_str());
        return;
    }
    
    Serial.println("Starting calibration...");
    mqtt.publish(topicStatus.c_str(), "{\"state\":\"calibrating\"}");
    
    long sumFront = 0;
    long sumBack = 0;
    int validFront = 0;
    int validBack = 0;
    
    for (int i = 0; i < CALIBRATION_SAMPLES; i++) {
        VL53L0X_RangingMeasurementData_t measureFront;
        VL53L0X_RangingMeasurementData_t measureBack;
        
        sensorFront.rangingTest(&measureFront, false);
        sensorBack.rangingTest(&measureBack, false);
        
        // Use 9999 as max distance if reading is out of range
        int distFront = (measureFront.RangeStatus != 4) ? measureFront.RangeMilliMeter : 9999;
        int distBack = (measureBack.RangeStatus != 4) ? measureBack.RangeMilliMeter : 9999;
        
        sumFront += distFront;
        validFront++;
        
        sumBack += distBack;
        validBack++;
        
        delay(50);
    }
    
    if (validFront > 0 && validBack > 0) {
        int avgFront = sumFront / validFront;
        int avgBack = sumBack / validBack;
        
        thresholdFront = avgFront - CALIBRATION_MARGIN;
        thresholdBack = avgBack - CALIBRATION_MARGIN;
        
        // Ensure thresholds are not negative
        if (thresholdFront < 50) thresholdFront = 50;
        if (thresholdBack < 50) thresholdBack = 50;
        
        savePreferences();
        
        Serial.printf("Calibration complete: Front=%dmm, Back=%dmm\n", thresholdFront, thresholdBack);
        
        String statusJson = "{\"state\":\"calibrated\",\"thresholds\":{\"front\":" + 
                            String(thresholdFront) + ",\"back\":" + String(thresholdBack) + "}}";
        mqtt.publish(topicStatus.c_str(), statusJson.c_str());
    } else {
        Serial.println("Calibration failed - not enough valid readings");
        String errorJson = "{\"state\":\"calibration_failed\",\"error\":\"invalid_readings\"}";
        mqtt.publish(topicStatus.c_str(), errorJson.c_str());
    }
}

// ==================== STATUS PUBLISHING ====================
void publishStatus() {
    if (!mqtt.connected()) return;
    
    unsigned long now = millis();
    if (now - lastStatusPublish < STATUS_PUBLISH_INTERVAL) return;
    lastStatusPublish = now;
    
    String statusJson = "{\"state\":\"online\",\"thresholds\":{\"front\":" + 
                        String(thresholdFront) + ",\"back\":" + String(thresholdBack) + 
                        "},\"sensors\":" + (sensorsInitialized ? "true" : "false") +
                        ",\"rssi\":" + String(WiFi.RSSI()) + "}";
    mqtt.publish(topicStatus.c_str(), statusJson.c_str());
}

// ==================== DIRECTION DETECTION ====================
void detectDirection() {
    VL53L0X_RangingMeasurementData_t measureFront;
    VL53L0X_RangingMeasurementData_t measureBack;
    
    sensorFront.rangingTest(&measureFront, false);
    sensorBack.rangingTest(&measureBack, false);
    
    // Use 9999 as max distance if reading is out of range
    int distFront = (measureFront.RangeStatus != 4) ? measureFront.RangeMilliMeter : 9999;
    int distBack = (measureBack.RangeStatus != 4) ? measureBack.RangeMilliMeter : 9999;
    
    bool frontTriggered = (measureFront.RangeStatus != 4) && (distFront < thresholdFront);
    bool backTriggered = (measureBack.RangeStatus != 4) && (distBack < thresholdBack);
    
    unsigned long now = millis();
    unsigned long elapsed = now - detectionStartTime;
    
    // Publish live distance data if enabled
    if (distanceStreamEnabled && (now - lastDistancePublish >= DISTANCE_STREAM_INTERVAL)) {
        String distanceJson = "{\"front\":" + String(distFront) + ",\"back\":" + String(distBack) + "}";
        mqtt.publish(topicDistance.c_str(), distanceJson.c_str());
        lastDistancePublish = now;
    }
    
    switch (detectionState) {
        case IDLE:
            if (frontTriggered && !backTriggered) {
                detectionState = FRONT_FIRST;
                detectionStartTime = now;
                Serial.println("Detection: Front sensor triggered first");
            } else if (backTriggered && !frontTriggered) {
                detectionState = BACK_FIRST;
                detectionStartTime = now;
                Serial.println("Detection: Back sensor triggered first");
            }
            break;
            
        case FRONT_FIRST:
            // Only accept second trigger after minimum interval (debounce)
            if (backTriggered && elapsed >= MIN_TRIGGER_INTERVAL) {
                // Front then Back = IN (front to back)
                Serial.printf("Direction: IN (front to back) - elapsed %lu ms\n", elapsed);
                mqtt.publish(topicTransition.c_str(), "{\"direction\":\"IN\"}");
                detectionState = COOLDOWN;
                detectionStartTime = now;
            } else if (elapsed > DETECTION_TIMEOUT) {
                Serial.println("Detection timeout - resetting");
                detectionState = IDLE;
            }
            break;
            
        case BACK_FIRST:
            // Only accept second trigger after minimum interval (debounce)
            if (frontTriggered && elapsed >= MIN_TRIGGER_INTERVAL) {
                // Back then Front = OUT (back to front)
                Serial.printf("Direction: OUT (back to front) - elapsed %lu ms\n", elapsed);
                mqtt.publish(topicTransition.c_str(), "{\"direction\":\"OUT\"}");
                detectionState = COOLDOWN;
                detectionStartTime = now;
            } else if (elapsed > DETECTION_TIMEOUT) {
                Serial.println("Detection timeout - resetting");
                detectionState = IDLE;
            }
            break;
            
        case COOLDOWN:
            // Wait for cooldown period and both sensors to clear before allowing new detection
            if (elapsed >= DETECTION_COOLDOWN && !frontTriggered && !backTriggered) {
                Serial.println("Cooldown complete - ready for new detection");
                detectionState = IDLE;
            }
            break;
    }
}

// ==================== LED STATUS ====================
void updateLED() {
    unsigned long now = millis();
    
    if (deviceMode == MODE_AP) {
        // Slow blink in AP mode
        if (now - lastLedToggle > LED_BLINK_AP) {
            ledState = !ledState;
            digitalWrite(LED_PIN, ledState);
            lastLedToggle = now;
        }
    } else if (deviceMode == MODE_NORMAL) {
        if (!sensorsInitialized) {
            // LED off if sensors failed
            digitalWrite(LED_PIN, LOW);
        } else if (WiFi.status() == WL_CONNECTED) {
            // Solid on when connected and working
            digitalWrite(LED_PIN, HIGH);
        } else {
            // Fast blink if WiFi disconnected
            if (now - lastLedToggle > LED_BLINK_CONNECTING) {
                ledState = !ledState;
                digitalWrite(LED_PIN, ledState);
                lastLedToggle = now;
            }
        }
    }
}