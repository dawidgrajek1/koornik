# KoornikSensor

Firmware for the Koornik people-flow sensor. The device is mounted in a doorway or passage, works out which way each person walks through it, and reports every transition (`IN` / `OUT`) to the Koornik backend over MQTT.

Each device uses two time-of-flight distance sensors placed one behind the other along the direction of travel. A person crossing the passage interrupts one beam before the other, and the order gives the direction.

## Hardware

| Part | Qty | Notes |
| --- | --- | --- |
| ESP32 dev board (`esp32doit-devkit-v1` / ESP32-WROOM-32) | 1 | Any ESP32 with two free I²C-capable pin pairs works; adjust the pins in `src/main.cpp` |
| VL53L0X time-of-flight breakout | 2 | e.g. Adafruit 3317 or GY-530. Range up to ~2 m |
| Jumper wires, 3.3 V supply (USB is fine) | | |

Both VL53L0X sensors keep the factory I²C address `0x29`. They don't conflict because each one sits on its own I²C bus (the ESP32 has two hardware I²C controllers), so `XSHUT` and `GPIO1` don't need to be connected.

### Wiring

| ESP32 pin | Connects to | Purpose |
| --- | --- | --- |
| `3V3` | VIN of both sensors | Power |
| `GND` | GND of both sensors | Ground |
| `GPIO21` | Front sensor `SDA` | I²C bus 0 |
| `GPIO22` | Front sensor `SCL` | I²C bus 0 |
| `GPIO18` | Back sensor `SDA` | I²C bus 1 |
| `GPIO19` | Back sensor `SCL` | I²C bus 1 |
| `GPIO23` | Momentary button / jumper to `GND` | Factory reset (hold LOW during boot) |
| `GPIO2` | Onboard LED | Status indicator |

```
                 ESP32
            ┌─────────────┐
 Front  SDA─┤ GPIO21      │
 VL53L0X SCL┤ GPIO22      │
            │             │
 Back   SDA─┤ GPIO18      │
 VL53L0X SCL┤ GPIO19      │
            │             │
 Reset btn ─┤ GPIO23  GND ├─ Reset btn / sensor GND
            │         3V3 ├─ sensor VIN
            └─────────────┘
```

Most breakouts already have I²C pull-up resistors. If yours doesn't, add 4.7 kΩ pull-ups to 3.3 V on each SDA/SCL line.

### Mounting

Mount both sensors at about chest height on one side of the passage, aimed across it at the opposite wall or door frame. Place them a few centimetres apart along the walking direction so that a person always crosses one beam before the other.

- Walking past the **front** sensor first and then the **back** sensor produces `IN`.
- Walking past the **back** sensor first and then the **front** sensor produces `OUT`.

The backend maps the directions to zones using the device's `FrontZone` / `BackZone` configuration.

### Status LED

| LED | Meaning |
| --- | --- |
| Slow blink (1 s) | Setup mode (access point running) |
| Fast blink | Connecting to Wi-Fi / Wi-Fi lost |
| Solid on | Connected, sensors running |
| Off | Sensor initialisation failed (check wiring) |

## Building and flashing

The project uses [PlatformIO](https://platformio.org/), either the VS Code extension or the CLI (`pip install platformio`).

The setup access point is protected with WPA2. Its password is given at build time through an environment variable, so it never gets committed. It must be 8–63 characters long, and the build fails with an error if it's missing or too short.

```bash
cd KoornikSensor
export KOORNIK_AP_PASSWORD='choose-a-strong-password'

pio run                      # build
pio run -t upload            # flash over USB
pio device monitor           # serial log, 115200 baud
```

The first build downloads the toolchain and these libraries automatically: Adafruit_VL53L0X, ArduinoJson 7, PubSubClient, and the ESP32Async forks of AsyncTCP and ESPAsyncWebServer. The platform is pinned to `espressif32 @ 8.0.0` (Arduino core 3.x). The original `me-no-dev` async libraries crash on that core.

The firmware uses the `min_spiffs.csv` partition table (1.9 MB app partition), because the TLS stack doesn't fit in the default 1.25 MB one.

## First-time setup

1. Power the device. With no saved configuration it starts the **`ToF-Setup`** Wi-Fi access point (LED blinks slowly).
2. Connect to it using the password you set in `KOORNIK_AP_PASSWORD`, then open `http://192.168.4.1`.
3. Fill in the form:
   - **Wi-Fi SSID / password**: the network the sensor should join.
   - **MQTT broker host / port**: default port `8883` (TLS). With TLS enabled, the host must match the CN/SAN of the broker's certificate.
   - **MQTT username / password**: credentials from the broker's password file.
   - **Broker CA certificate**: the PEM certificate of the CA that signed the broker's certificate.
   - **Disable TLS**: only for local development against a plain-text broker (port `1883`). Credentials are then sent unencrypted.
4. Press **Save & Connect**. The device saves the settings to flash (NVS), restarts, and joins your network.

To reconfigure, hold `GPIO23` to `GND` while powering on or resetting. This erases the stored settings and starts the access point again. The device also falls back to the access point if it can't join the saved Wi-Fi network within 30 s.

Wi-Fi SSIDs and passwords are never written to the serial log.

## MQTT interface

All topics use the device's Wi-Fi MAC address in upper case without colons, e.g. `A1B2C3D4E5F6`:

| Topic | Direction | Payload |
| --- | --- | --- |
| `koornik/<MAC>/transition` | device → backend | `{"direction":"IN"}` or `{"direction":"OUT"}` |
| `koornik/<MAC>/status` | device → backend | Device state (see below) |
| `koornik/<MAC>/distance` | device → backend | `{"front":812,"back":9999}` every 500 ms while streaming is enabled |
| `koornik/<MAC>/command` | backend → device | JSON command (see below) |

The MQTT client ID is `ToF-<MAC>`. In `distance` messages, distances are in millimetres, and `9999` means "no valid reading / out of range".

### Status messages

| `state` | When | Extra fields |
| --- | --- | --- |
| `online` | On connect and every 10 s | `thresholds: {front, back}`, `sensors` (bool, sensors initialised), `rssi` (dBm) |
| `calibrating` | Calibration started | |
| `calibrated` | Calibration succeeded | `thresholds: {front, back}` |
| `calibration_failed` | Calibration couldn't produce a usable threshold (see [Calibrate](#commands)) | `error`: `sensors_not_ready`, `invalid_readings` or `too_close`; for the last two also `sensor` (`front` / `back`, the first sensor that failed) and `valid: {front, back}` (number of good samples) |
| `settings_updated` | `updateSettings` applied | `thresholds: {front, back}` |
| `command_failed` | Command couldn't be processed | `error`: `invalid_json`, `unknown_action`, `invalid_enabled`, `missing_payload` |

Example:

```json
{"state":"online","thresholds":{"front":900,"back":900},"sensors":true,"rssi":-61}
```

### Commands

Commands are JSON objects with an `action` field. Formatting and whitespace don't matter, because they're parsed with ArduinoJson.

**Calibrate.** Run this with the passage empty. The device takes 10 samples per sensor and works out a threshold for each one:

```json
{"action":"calibrate"}
```

| What the sensor sees | Result |
| --- | --- |
| At least 7 good readings (range status 0) | Threshold = average distance − 100 mm, capped at 2000 mm |
| At least 7 out-of-range readings (nothing within ~2 m, e.g. an open hallway) | Threshold = 2000 mm, so anything that comes into range triggers it |
| Background closer than 150 mm (threshold would be below 50 mm) | Fails with `too_close`. Move the sensor or aim it further away |
| Anything else: a mix of in-range and out-of-range readings, mostly weak-signal readings, or sensor errors | Fails with `invalid_readings`. Check the mounting and wiring, or set the thresholds manually with `updateSettings` |

Only good-quality readings go into the average. Readings flagged as weak signal or high noise (range status 1–3) are ignored during calibration, but they still count for detection, so people in dark clothing are still detected. If either sensor fails, neither threshold changes.

**Enable / disable live distance streaming** (useful when mounting and tuning):

```json
{"action":"enableDistance","enabled":true}
```

**Set detection thresholds** manually, in millimetres. Each field is optional, and values are clamped to 50–2000 mm. Unknown fields are ignored.

```json
{"action":"updateSettings","payload":{"thresholdFront":900,"thresholdBack":900}}
```

Thresholds and calibration results are stored in flash and survive restarts.

### Detection parameters

These are compile-time constants in `src/main.cpp`:

| Constant | Default | Meaning |
| --- | --- | --- |
| `DETECTION_TIMEOUT` | 2000 ms | Max time between the first and second beam break |
| `MIN_TRIGGER_INTERVAL` | 50 ms | Minimum time between the two beam breaks (debounce) |
| `DETECTION_COOLDOWN` | 1000 ms | Wait after a transition; both beams must also be clear |
| `CALIBRATION_MARGIN` | 100 mm | Subtracted from the calibrated distance |

## Broker configuration

The device expects an MQTT broker with authentication and TLS. A minimal Mosquitto configuration:

```conf
listener 8883
cafile   /mosquitto/config/certs/ca.crt
certfile /mosquitto/config/certs/server.crt
keyfile  /mosquitto/config/certs/server.key

allow_anonymous false
password_file /mosquitto/config/pwfile
```

Create a user for each device (or one shared device account) with:

```bash
mosquitto_passwd -c /mosquitto/config/pwfile <username>
```

Paste the contents of `ca.crt` into the setup page.
