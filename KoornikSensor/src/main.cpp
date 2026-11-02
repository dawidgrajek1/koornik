#include <Arduino.h>
#include <Wire.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <Preferences.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
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
#ifndef AP_PASSWORD
#error "AP_PASSWORD is not defined - set KOORNIK_AP_PASSWORD before building"
#endif
static_assert(sizeof(AP_PASSWORD) - 1 >= 8 && sizeof(AP_PASSWORD) - 1 <= 63,
              "KOORNIK_AP_PASSWORD must be 8-63 characters (WPA2 requirement)");

#define MQTT_PORT_TLS 8883
#define MQTT_PORT_PLAIN 1883
#define MQTT_TOPIC_PREFIX "koornik"

#define DEFAULT_THRESHOLD 1000       // Default detection threshold in mm
#define DISTANCE_STREAM_INTERVAL 500  // Live distance streaming interval in ms
#define STATUS_PUBLISH_INTERVAL 10000 // Status publish interval in ms
#define CALIBRATION_MARGIN 100      // Margin to subtract from calibrated distance
#define CALIBRATION_SAMPLES 10      // Number of samples for calibration
#define CALIBRATION_MIN_VALID 7     // Minimum in-range samples per sensor for calibration to succeed
static_assert(CALIBRATION_MIN_VALID <= CALIBRATION_SAMPLES, "CALIBRATION_MIN_VALID must not exceed CALIBRATION_SAMPLES");
#define MIN_THRESHOLD 50            // Lowest allowed detection threshold in mm
#define MAX_THRESHOLD 2000          // Highest allowed detection threshold in mm (VL53L0X max range)
#define DIST_OUT_OF_RANGE 9999      // Reported distance when a reading is invalid/out of range
#define RESTART_DELAY 1000          // Delay before restarting after saving config, so the HTTP response is sent
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

WiFiClient plainClient;
WiFiClientSecure secureClient;
PubSubClient mqtt;
AsyncWebServer server(80);
Preferences preferences;

// ==================== STATE VARIABLES ====================
enum DeviceMode { MODE_AP, MODE_NORMAL };
enum DetectionState { IDLE, FRONT_FIRST, BACK_FIRST, COOLDOWN };
enum ReadingStatus {
    READING_VALID,          // Range status 0: good measurement
    READING_POOR,           // Range status 1-3 (sigma/signal/min-range fail): usable for detection only
    READING_OUT_OF_RANGE,   // Range status 4: nothing within range
    READING_ERROR           // Sensor communication failed
};

DeviceMode deviceMode = MODE_NORMAL;
DetectionState detectionState = IDLE;

String savedSSID = "";
String savedPassword = "";
String savedMqttBroker = "";
uint16_t savedMqttPort = MQTT_PORT_TLS;
String savedMqttUser = "";
String savedMqttPassword = "";
String savedMqttCaCert = "";        // PEM CA certificate used to verify the broker
bool savedMqttInsecure = false;     // true = plain TCP without TLS (development only)

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

volatile bool restartRequested = false;
unsigned long restartRequestedAt = 0;

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
        select, input[type="text"], input[type="password"], input[type="number"], textarea {
            width: 100%;
            padding: 15px;
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            font-size: 16px;
            transition: border-color 0.3s;
            -webkit-appearance: none;
            appearance: none;
        }
        textarea {
            font-family: monospace;
            font-size: 12px;
            min-height: 120px;
            resize: vertical;
        }
        .checkbox {
            display: flex;
            align-items: center;
            gap: 8px;
            font-weight: normal;
        }
        .hint {
            margin-top: 6px;
            color: #888;
            font-size: 12px;
        }
        select:focus, input:focus, textarea:focus {
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
                <label for="mqtt">MQTT Broker Host</label>
                <input type="text" id="mqtt" name="mqtt" placeholder="e.g., broker.local" required>
                <p class="hint">Must match the name in the broker's TLS certificate.</p>
            </div>
            
            <div class="form-group">
                <label for="mqttPort">MQTT Port</label>
                <input type="number" id="mqttPort" name="mqttPort" min="1" max="65535" value="8883">
            </div>
            
            <div class="form-group">
                <label for="mqttUser">MQTT Username</label>
                <input type="text" id="mqttUser" name="mqttUser" placeholder="Enter username" required>
            </div>
            
            <div class="form-group">
                <label for="mqttPassword">MQTT Password</label>
                <input type="password" id="mqttPassword" name="mqttPassword" placeholder="Enter password" required>
            </div>
            
            <div class="form-group">
                <label for="mqttCa">Broker CA Certificate (PEM)</label>
                <textarea id="mqttCa" name="mqttCa" placeholder="-----BEGIN CERTIFICATE-----"></textarea>
            </div>
            
            <div class="form-group">
                <label class="checkbox">
                    <input type="checkbox" id="mqttInsecure" name="mqttInsecure">
                    Disable TLS (development only)
                </label>
            </div>
            
            <button type="submit">Save & Connect</button>
            
            <div id="status" class="status hidden"></div>
        </form>
    </div>
    
    <script>
        document.getElementById('mqttInsecure').addEventListener('change', function() {
            const port = document.getElementById('mqttPort');
            if (this.checked && port.value === '8883') port.value = '1883';
            if (!this.checked && port.value === '1883') port.value = '8883';
        });

        document.getElementById('configForm').addEventListener('submit', function(e) {
            e.preventDefault();
            
            const ssid = document.getElementById('ssid').value;
            const password = document.getElementById('password').value;
            const mqttBroker = document.getElementById('mqtt').value;
            const mqttPort = document.getElementById('mqttPort').value;
            const mqttUser = document.getElementById('mqttUser').value;
            const mqttPassword = document.getElementById('mqttPassword').value;
            const mqttCa = document.getElementById('mqttCa').value.trim();
            const mqttInsecure = document.getElementById('mqttInsecure').checked;
            
            if (!ssid) {
                showStatus('Please enter a network name', 'error');
                return;
            }
            
            if (!mqttBroker) {
                showStatus('Please enter MQTT broker host', 'error');
                return;
            }
            
            if (!mqttInsecure && !mqttCa) {
                showStatus('Paste the broker CA certificate or disable TLS', 'error');
                return;
            }
            
            const formData = new URLSearchParams();
            formData.append('ssid', ssid);
            formData.append('password', password);
            formData.append('mqtt', mqttBroker);
            formData.append('mqttPort', mqttPort);
            formData.append('mqttUser', mqttUser);
            formData.append('mqttPassword', mqttPassword);
            formData.append('mqttCa', mqttCa);
            formData.append('mqttInsecure', mqttInsecure ? '1' : '0');
            
            fetch('/save', {
                method: 'POST',
                body: formData
            })
            .then(response => response.text().then(text => {
                if (!response.ok) throw new Error(text);
                return text;
            }))
            .then(result => {
                showStatus('Settings saved! Rebooting...', 'success');
                setTimeout(() => {
                    showStatus('Device is rebooting. Please connect to your WiFi network.', 'success');
                }, 2000);
            })
            .catch(err => {
                showStatus('Save failed: ' + err.message, 'error');
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
ReadingStatus readSensor(Adafruit_VL53L0X& sensor, int& distance);

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

void loop() {
    updateLED();
    
    if (restartRequested && millis() - restartRequestedAt >= RESTART_DELAY) {
        Serial.println("Restarting to apply new configuration...");
        ESP.restart();
    }
    
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

void setupPins() {
    pinMode(LED_PIN, OUTPUT);
    digitalWrite(LED_PIN, LOW);
    
    pinMode(RESET_PIN, INPUT_PULLUP);
}

void loadPreferences() {
    preferences.begin("tof-config", true);  // Read-only
    savedSSID = preferences.getString("ssid", "");
    savedPassword = preferences.getString("password", "");
    savedMqttBroker = preferences.getString("mqtt", "");
    savedMqttPort = preferences.getUShort("mqtt_port", MQTT_PORT_TLS);
    savedMqttUser = preferences.getString("mqtt_user", "");
    savedMqttPassword = preferences.getString("mqtt_pass", "");
    savedMqttCaCert = preferences.getString("mqtt_ca", "");
    savedMqttInsecure = preferences.getBool("mqtt_insecure", false);
    thresholdFront = preferences.getInt("thresh_front", DEFAULT_THRESHOLD);
    thresholdBack = preferences.getInt("thresh_back", DEFAULT_THRESHOLD);
    preferences.end();
    
    // Don't log the SSID or any credentials
    Serial.println("Loaded preferences:");
    Serial.printf("  WiFi: %s\n", savedSSID.length() > 0 ? "configured" : "not configured");
    Serial.printf("  MQTT: %s:%u (%s)\n", savedMqttBroker.c_str(), savedMqttPort,
                  savedMqttInsecure ? "plain TCP" : "TLS");
    Serial.printf("  Thresholds: Front=%d, Back=%d\n", thresholdFront, thresholdBack);
}

void savePreferences() {
    preferences.begin("tof-config", false);  // Read-write
    preferences.putString("ssid", savedSSID);
    preferences.putString("password", savedPassword);
    preferences.putString("mqtt", savedMqttBroker);
    preferences.putUShort("mqtt_port", savedMqttPort);
    preferences.putString("mqtt_user", savedMqttUser);
    preferences.putString("mqtt_pass", savedMqttPassword);
    preferences.putString("mqtt_ca", savedMqttCaCert);
    preferences.putBool("mqtt_insecure", savedMqttInsecure);
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
    Serial.println("Connecting to WiFi...");
    
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
    WiFi.softAP(AP_SSID, AP_PASSWORD);
    
    Serial.println("AP SSID: " + String(AP_SSID));
    Serial.println("AP IP: " + WiFi.softAPIP().toString());
    
    setupWebServer();
}

void setupWebServer() {
    // Serve main page
    server.on("/", HTTP_GET, [](AsyncWebServerRequest *request) {
        request->send(200, "text/html", index_html);
    });
    
    // Save configuration endpoint
    server.on("/save", HTTP_POST, [](AsyncWebServerRequest *request) {
        // Runs on the AsyncTCP task and must not block, so the restart happens in loop()
        if (restartRequested) {
            request->send(409, "text/plain", "Already saved, restarting");
            return;
        }
        if (!request->hasParam("ssid", true) || !request->hasParam("mqtt", true)) {
            request->send(400, "text/plain", "Missing parameters");
            return;
        }
        
        auto param = [request](const char* name) -> String {
            return request->hasParam(name, true) ? request->getParam(name, true)->value() : String();
        };
        
        String ssid = param("ssid");
        String password = param("password");
        String broker = param("mqtt");
        long port = param("mqttPort").toInt();
        String caCert = param("mqttCa");
        bool insecure = param("mqttInsecure") == "1";
        
        if (ssid.length() == 0 || ssid.length() > 32) {
            request->send(400, "text/plain", "SSID must be 1-32 characters");
            return;
        }
        if (password.length() > 0 && (password.length() < 8 || password.length() > 63)) {
            request->send(400, "text/plain", "WiFi password must be empty or 8-63 characters");
            return;
        }
        if (broker.length() == 0) {
            request->send(400, "text/plain", "MQTT broker is required");
            return;
        }
        if (port == 0) {
            port = insecure ? MQTT_PORT_PLAIN : MQTT_PORT_TLS;
        } else if (port < 1 || port > 65535) {
            request->send(400, "text/plain", "Invalid MQTT port");
            return;
        }
        if (!insecure && caCert.indexOf("-----BEGIN CERTIFICATE-----") < 0) {
            request->send(400, "text/plain", "A PEM CA certificate is required unless TLS is disabled");
            return;
        }
        
        savedSSID = ssid;
        savedPassword = password;
        savedMqttBroker = broker;
        savedMqttPort = (uint16_t)port;
        savedMqttUser = param("mqttUser");
        savedMqttPassword = param("mqttPassword");
        savedMqttCaCert = insecure ? String() : caCert;
        savedMqttInsecure = insecure;
        
        Serial.println("Received new configuration");
        savePreferences();
        
        request->send(200, "text/plain", "OK");
        
        restartRequestedAt = millis();
        restartRequested = true;
    });
    
    server.begin();
    Serial.println("Web server started");
}

void setupMQTT() {
    if (savedMqttInsecure) {
        Serial.println("WARNING: MQTT TLS disabled - traffic and credentials are sent in plain text");
        mqtt.setClient(plainClient);
    } else {
        // savedMqttCaCert is a global, so the pointer stays valid for the client's lifetime
        secureClient.setCACert(savedMqttCaCert.c_str());
        mqtt.setClient(secureClient);
    }
    mqtt.setServer(savedMqttBroker.c_str(), savedMqttPort);
    mqtt.setCallback(mqttCallback);
    mqtt.setBufferSize(512);  // Increase buffer for JSON payloads
    reconnectMQTT();
}

int clampThreshold(int value) {
    return constrain(value, MIN_THRESHOLD, MAX_THRESHOLD);
}

void publishCommandError(const char* error) {
    JsonDocument doc;
    doc["state"] = "command_failed";
    doc["error"] = error;
    String json;
    serializeJson(doc, json);
    mqtt.publish(topicStatus.c_str(), json.c_str());
}

void handleCommand(const byte* payload, unsigned int length) {
    // Expected format: {"action":"calibrate"} or {"action":"enableDistance","enabled":true}
    // or {"action":"updateSettings","payload":{"thresholdFront":400,"thresholdBack":400}}
    JsonDocument doc;
    DeserializationError err = deserializeJson(doc, payload, length);
    if (err) {
        Serial.printf("Invalid command JSON: %s\n", err.c_str());
        publishCommandError("invalid_json");
        return;
    }
    
    const char* action = doc["action"] | "";
    
    if (strcmp(action, "calibrate") == 0) {
        calibrateSensors();
    }
    else if (strcmp(action, "enableDistance") == 0) {
        if (!doc["enabled"].is<bool>()) {
            publishCommandError("invalid_enabled");
            return;
        }
        distanceStreamEnabled = doc["enabled"].as<bool>();
        Serial.printf("Distance streaming %s\n", distanceStreamEnabled ? "enabled" : "disabled");
    }
    else if (strcmp(action, "updateSettings") == 0) {
        JsonObjectConst settings = doc["payload"];
        if (settings.isNull()) {
            publishCommandError("missing_payload");
            return;
        }
        
        if (settings["thresholdFront"].is<int>()) {
            thresholdFront = clampThreshold(settings["thresholdFront"].as<int>());
        }
        if (settings["thresholdBack"].is<int>()) {
            thresholdBack = clampThreshold(settings["thresholdBack"].as<int>());
        }
        
        savePreferences();
        Serial.printf("Settings updated: Front=%d, Back=%d\n", thresholdFront, thresholdBack);
        
        // Publish updated status
        String statusJson = "{\"state\":\"settings_updated\",\"thresholds\":{\"front\":" + 
                            String(thresholdFront) + ",\"back\":" + String(thresholdBack) + "}}";
        mqtt.publish(topicStatus.c_str(), statusJson.c_str());
    }
    else {
        Serial.printf("Unknown command action: '%s'\n", action);
        publishCommandError("unknown_action");
    }
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
    Serial.printf("MQTT message [%s]: %.*s\n", topic, (int)length, (const char*)payload);
    
    if (topicCommand == topic) {
        handleCommand(payload, length);
    }
}

void reconnectMQTT() {
    if (mqtt.connected()) return;
    
    unsigned long now = millis();
    if (now - lastMqttAttempt < MQTT_RETRY_INTERVAL) return;
    lastMqttAttempt = now;
    
    Serial.println("Attempting MQTT connection...");
    
    String clientId = "ToF-" + deviceMac;
    bool connected = savedMqttUser.length() > 0
        ? mqtt.connect(clientId.c_str(), savedMqttUser.c_str(), savedMqttPassword.c_str())
        : mqtt.connect(clientId.c_str());
    if (connected) {
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

struct CalibrationSamples {
    long sum = 0;
    int valid = 0;
    int outOfRange = 0;
    
    void add(ReadingStatus status, int distance) {
        if (status == READING_VALID) {
            sum += distance;
            valid++;
        } else if (status == READING_OUT_OF_RANGE) {
            outOfRange++;
        }
    }
};

// Returns nullptr on success, otherwise an error code
const char* computeThreshold(const CalibrationSamples& samples, int& threshold) {
    if (samples.valid >= CALIBRATION_MIN_VALID) {
        int candidate = samples.sum / samples.valid - CALIBRATION_MARGIN;
        // Clamping up to MIN_THRESHOLD would put the threshold at or beyond the background,
        // leaving the sensor permanently triggered
        if (candidate < MIN_THRESHOLD) return "too_close";
        threshold = clampThreshold(candidate);
        return nullptr;
    }
    if (samples.outOfRange >= CALIBRATION_MIN_VALID) {
        // Open passage beyond sensor range: anything that comes into range triggers
        threshold = MAX_THRESHOLD;
        return nullptr;
    }
    return "invalid_readings";
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
    
    CalibrationSamples front, back;
    
    for (int i = 0; i < CALIBRATION_SAMPLES; i++) {
        int distFront, distBack;
        ReadingStatus statusFront = readSensor(sensorFront, distFront);
        ReadingStatus statusBack = readSensor(sensorBack, distBack);
        front.add(statusFront, distFront);
        back.add(statusBack, distBack);
        delay(50);
    }
    
    Serial.printf("Calibration samples: front %d valid/%d out of range, back %d valid/%d out of range (of %d)\n",
                  front.valid, front.outOfRange, back.valid, back.outOfRange, CALIBRATION_SAMPLES);
    
    int newFront, newBack;
    const char* errorFront = computeThreshold(front, newFront);
    const char* errorBack = computeThreshold(back, newBack);
    
    if (!errorFront && !errorBack) {
        thresholdFront = newFront;
        thresholdBack = newBack;
        savePreferences();
        
        Serial.printf("Calibration complete: Front=%dmm, Back=%dmm\n", thresholdFront, thresholdBack);
        
        String statusJson = "{\"state\":\"calibrated\",\"thresholds\":{\"front\":" + 
                            String(thresholdFront) + ",\"back\":" + String(thresholdBack) + "}}";
        mqtt.publish(topicStatus.c_str(), statusJson.c_str());
    } else {
        const char* sensor = errorFront ? "front" : "back";
        const char* error = errorFront ? errorFront : errorBack;
        Serial.printf("Calibration failed - %s sensor: %s\n", sensor, error);
        String errorJson = "{\"state\":\"calibration_failed\",\"error\":\"" + String(error) +
                           "\",\"sensor\":\"" + sensor + "\",\"valid\":{\"front\":" +
                           String(front.valid) + ",\"back\":" + String(back.valid) + "}}";
        mqtt.publish(topicStatus.c_str(), errorJson.c_str());
    }
    
    // Calibration blocks for ~1 s; don't resume a half-finished detection afterwards
    detectionState = IDLE;
}

// Takes one measurement. For READING_OUT_OF_RANGE and READING_ERROR, distance is set to DIST_OUT_OF_RANGE.
ReadingStatus readSensor(Adafruit_VL53L0X& sensor, int& distance) {
    VL53L0X_RangingMeasurementData_t measure = {};
    if (sensor.rangingTest(&measure, false) != VL53L0X_ERROR_NONE) {
        distance = DIST_OUT_OF_RANGE;
        return READING_ERROR;
    }
    if (measure.RangeStatus == 4 || measure.RangeMilliMeter >= 8190) {
        distance = DIST_OUT_OF_RANGE;
        return READING_OUT_OF_RANGE;
    }
    distance = measure.RangeMilliMeter;
    return measure.RangeStatus == 0 ? READING_VALID : READING_POOR;
}

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

void detectDirection() {
    int distFront, distBack;
    // Poor-quality readings still count for detection (e.g. dark clothing gives a weak signal)
    ReadingStatus statusFront = readSensor(sensorFront, distFront);
    ReadingStatus statusBack = readSensor(sensorBack, distBack);
    bool validFront = statusFront == READING_VALID || statusFront == READING_POOR;
    bool validBack = statusBack == READING_VALID || statusBack == READING_POOR;
    
    bool frontTriggered = validFront && (distFront < thresholdFront);
    bool backTriggered = validBack && (distBack < thresholdBack);
    
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