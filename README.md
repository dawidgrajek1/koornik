# Koornik

Koornik counts people moving between rooms in real time. Small sensors in each doorway detect who walks in and out, and a live dashboard shows how full every zone is compared with its capacity.

## How it works

1. **Sensor**: two time-of-flight distance sensors sit one behind the other in a doorway. The order in which they're interrupted gives the direction (`IN` / `OUT`), which is published over MQTT.
2. **API**: an ASP.NET Core service subscribes to the sensors and turns each transition into a move between the device's two zones. It stores the history and updates each zone's occupancy.
3. **Dashboard**: a React app shows zones on a floor plan with live occupancy pushed over SignalR. It's also where you manage devices and zones and trigger sensor calibration.

## Repository layout

| Path | What it is |
| --- | --- |
| [`KoornikSensor/`](KoornikSensor/README.md) | ESP32 firmware (PlatformIO). Covers hardware, wiring and the MQTT protocol |
| `Koornik.API/` | ASP.NET Core Web API, SignalR hub at `/hubs/koornik`, Swagger in development |
| `Koornik.Application/` | Use cases and services (devices, zones, transitions) |
| `Koornik.Domain/` | Entities, repository and specification abstractions |
| `Koornik.Infrastructure/` | EF Core + PostgreSQL, MQTT client, SignalR notifications |
| `Koornik.Contracts/` | Request/response DTOs shared by the API and SDK |
| `Koornik.Sdk/` | Typed .NET client for the API (Refit) |
| `Koornik.Application.Tests/` | Unit tests (xUnit, Moq, FluentAssertions) |
| `Koornik.Web/` | Dashboard (React 19, Vite, Ant Design, Leaflet) |

**Tech stack:** .NET 10, EF Core 10, PostgreSQL 18, MQTTnet 5, Eclipse Mosquitto 2, React 19, TypeScript, ESP32 (Arduino core 3).

## Getting started

### Requirements

- Docker with Compose
- .NET 10 SDK, for running the API or tests outside Docker
- [Bun](https://bun.sh) or Node.js, for the dashboard
- PlatformIO, for the firmware

### 1. Backend (database, broker, API)

```bash
cp .env.example .env          # then change POSTGRES_PASSWORD and the connection string
touch .env.secrets            # local overrides that are never committed (compose requires the file)

# Mosquitto reads a password file; create one user for the API and one for the sensors
docker run --rm -v "$PWD/mosquitto/config:/mosquitto/config" eclipse-mosquitto:2.0 \
  mosquitto_passwd -c -b /mosquitto/config/pwfile <user> <password>

docker compose up -d --build
```

The API listens on `http://localhost:5000` and applies database migrations on startup. In the `Development` environment, Swagger is available at `/swagger`. The health check is at `/health`.

To run the API outside Docker, see [`Koornik.API/README.md`](Koornik.API/README.md).

### 2. Dashboard

```bash
cd Koornik.Web
echo "VITE_API_BASE_URL=http://localhost:5000" >  .env.local
echo "VITE_HUB_URL=http://localhost:5000/hubs/koornik" >> .env.local
bun install
bun run dev                   # http://localhost:5173
```

### 3. Sensors

Build and flash the firmware, then set up each device from its setup page. The full steps are in [`KoornikSensor/README.md`](KoornikSensor/README.md):

```bash
cd KoornikSensor
export KOORNIK_AP_PASSWORD='choose-a-strong-password'
pio run -t upload
```

Register the device in the dashboard using its MAC address, assign its front and back zones, and run **Calibrate** with the doorway empty.

### Tests

```bash
dotnet test
```

## API overview

All endpoints are versioned under `/api/v1`. The full schema is in Swagger.

| Resource | Endpoints |
| --- | --- |
| Devices | CRUD, `POST /devices/{id}/calibrate`, `POST /devices/{id}/distance-stream` |
| Zones | CRUD, `GET /zones/{id}/state`, `/state/at`, `/state/history` |
| Transitions | `GET /transitions/by-device/{id}`, `/by-zone/{id}`, `/by-time-range` |

Live updates (transitions, zone occupancy, device status, distance streams) are pushed through the SignalR hub at `/hubs/koornik`. Events, subscription methods and client examples are documented in [`Koornik.API/README-SignalR.md`](Koornik.API/README-SignalR.md).

## License

[MIT](LICENSE)
