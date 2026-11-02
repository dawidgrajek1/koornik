# Koornik SignalR Real-Time Communication

## Overview

The Koornik API exposes a SignalR hub for real-time IoT device events. Clients can connect to receive live updates about device transitions, status changes, distance readings, and zone occupancy without polling.

## Connection

**Hub URL:** `/hubs/koornik`

**Full URL Example:** `https://your-api-host/hubs/koornik`

**Supported Transports:** WebSockets (preferred), Server-Sent Events, Long Polling

## Client Methods (Events Received by Frontend)

These are the methods that clients should implement to receive events from the server.

### `OnTransition`

Called when a person moves between zones (detected by a device).

**Payload:**
```json
{
  "transitionId": "guid",
  "deviceId": "guid",
  "deviceName": "string",
  "macAddress": "string (e.g., AA:BB:CC:DD:EE:FF)",
  "direction": "IN | OUT",
  "fromZoneId": "guid",
  "toZoneId": "guid",
  "timestamp": "ISO 8601 datetime (UTC)"
}
```

**Direction Values:**
- `IN` - Movement from back zone to front zone (entering)
- `OUT` - Movement from front zone to back zone (exiting)

### `OnDeviceStatus`

Called when a device reports its status.

**Payload:**
```json
{
  "macAddress": "string",
  "state": "string (e.g., IDLE, DETECTING)",
  "thresholdFront": "number | null",
  "thresholdBack": "number | null",
  "timestamp": "ISO 8601 datetime (UTC)"
}
```

### `OnDistanceReading`

Called when distance sensor readings are received. This is a high-frequency event (only sent to clients subscribed to the specific device).

**Payload:**
```json
{
  "macAddress": "string",
  "frontDistance": "number (millimeters)",
  "backDistance": "number (millimeters)",
  "timestamp": "ISO 8601 datetime (UTC)"
}
```

### `OnZoneOccupancyChanged`

Called after a transition changes a zone's occupancy. One event is sent for each zone involved (the zone left and the zone entered). Border devices have "Outside" on one side, which isn't a tracked zone, so they produce a single event.

**Payload:**
```json
{
  "zoneId": "guid",
  "zoneName": "string",
  "currentOccupancy": "number (people currently in the zone)",
  "capacity": "number (maximum allowed occupancy)",
  "timestamp": "ISO 8601 datetime (UTC)"
}
```

## Server Methods (Invoked by Frontend)

These are methods that clients can call on the hub to manage subscriptions.

### `SubscribeToDevice`

Subscribe to events from a specific device by MAC address.

**Parameters:**
- `macAddress` (string): The device MAC address (e.g., `"AA:BB:CC:DD:EE:FF"`)

**Returns:** void

### `UnsubscribeFromDevice`

Unsubscribe from a specific device's events.

**Parameters:**
- `macAddress` (string): The device MAC address

**Returns:** void

### `SubscribeToZone`

Subscribe to transition events affecting a specific zone.

**Parameters:**
- `zoneId` (Guid): The zone identifier

**Returns:** void

### `UnsubscribeFromZone`

Unsubscribe from a specific zone's events.

**Parameters:**
- `zoneId` (Guid): The zone identifier

**Returns:** void

## Event Routing

| Event Type | Broadcast To |
|------------|--------------|
| `OnTransition` | All connected clients + device group + both zone groups |
| `OnDeviceStatus` | All connected clients |
| `OnDistanceReading` | Device group only (subscribers) |
| `OnZoneOccupancyChanged` | All connected clients + zone group |

## JavaScript/TypeScript Client Example

```typescript
import * as signalR from "@microsoft/signalr";

// Types matching server DTOs
interface TransitionEvent {
  transitionId: string;
  deviceId: string;
  deviceName: string;
  macAddress: string;
  direction: "IN" | "OUT";
  fromZoneId: string;
  toZoneId: string;
  timestamp: string;
}

interface DeviceStatusEvent {
  macAddress: string;
  state: string;
  thresholdFront: number | null;
  thresholdBack: number | null;
  timestamp: string;
}

interface DistanceReadingEvent {
  macAddress: string;
  frontDistance: number;
  backDistance: number;
  timestamp: string;
}

interface ZoneOccupancyChangedEvent {
  zoneId: string;
  zoneName: string;
  currentOccupancy: number;
  capacity: number;
  timestamp: string;
}

// Create connection
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/koornik")
  .withAutomaticReconnect()
  .configureLogging(signalR.LogLevel.Information)
  .build();

// Register event handlers
connection.on("OnTransition", (event: TransitionEvent) => {
  console.log(`Transition: ${event.direction} on ${event.deviceName}`);
  // Update UI, trigger animations, etc.
});

connection.on("OnDeviceStatus", (event: DeviceStatusEvent) => {
  console.log(`Device ${event.macAddress} is ${event.state}`);
});

connection.on("OnDistanceReading", (event: DistanceReadingEvent) => {
  console.log(`Distance: front=${event.frontDistance}mm, back=${event.backDistance}mm`);
});

connection.on("OnZoneOccupancyChanged", (event: ZoneOccupancyChangedEvent) => {
  console.log(`${event.zoneName}: ${event.currentOccupancy}/${event.capacity}`);
});

// Start connection
async function startConnection() {
  try {
    await connection.start();
    console.log("SignalR Connected");
    
    // Subscribe to specific device for distance readings
    await connection.invoke("SubscribeToDevice", "AA:BB:CC:DD:EE:FF");
    
    // Subscribe to a zone
    await connection.invoke("SubscribeToZone", "zone-guid-here");
  } catch (err) {
    console.error("SignalR Connection Error:", err);
    setTimeout(startConnection, 5000);
  }
}

// Handle disconnection
connection.onclose(async () => {
  await startConnection();
});

startConnection();
```

## React Hook Example

```tsx
import { useEffect, useState, useCallback } from "react";
import * as signalR from "@microsoft/signalr";

interface TransitionEvent {
  transitionId: string;
  deviceId: string;
  deviceName: string;
  macAddress: string;
  direction: "IN" | "OUT";
  fromZoneId: string;
  toZoneId: string;
  timestamp: string;
}

export function useKoornikHub(hubUrl: string = "/hubs/koornik") {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [transitions, setTransitions] = useState<TransitionEvent[]>([]);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    setConnection(newConnection);

    return () => {
      newConnection.stop();
    };
  }, [hubUrl]);

  useEffect(() => {
    if (!connection) return;

    connection.on("OnTransition", (event: TransitionEvent) => {
      setTransitions(prev => [event, ...prev].slice(0, 100)); // Keep last 100
    });

    connection.onclose(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));

    connection.start()
      .then(() => setIsConnected(true))
      .catch(console.error);

  }, [connection]);

  const subscribeToDevice = useCallback(async (macAddress: string) => {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke("SubscribeToDevice", macAddress);
    }
  }, [connection]);

  const subscribeToZone = useCallback(async (zoneId: string) => {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke("SubscribeToZone", zoneId);
    }
  }, [connection]);

  return {
    isConnected,
    transitions,
    subscribeToDevice,
    subscribeToZone,
  };
}
```

## C# Client Example (for other services)

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("https://api-host/hubs/koornik")
    .WithAutomaticReconnect()
    .Build();

connection.On<TransitionEvent>("OnTransition", (transitionEvent) =>
{
    Console.WriteLine($"Transition: {transitionEvent.Direction} on {transitionEvent.DeviceName}");
});

await connection.StartAsync();

// Subscribe to a device
await connection.InvokeAsync("SubscribeToDevice", "AA:BB:CC:DD:EE:FF");
```

## Error Handling

The hub logs connection and disconnection events. Clients should implement reconnection logic using `withAutomaticReconnect()` for JavaScript clients or equivalent for other platforms.

## CORS Configuration

If connecting from a different origin, ensure CORS is configured in the API to allow SignalR connections:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalR", policy =>
    {
        policy.WithOrigins("https://your-frontend.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

## Related Files

- Hub: `Koornik.Infrastructure/Hubs/KoornikHub.cs`
- Client Interface: `Koornik.Application/Interfaces/IKoornikHubClient.cs`
- Notification Service: `Koornik.Infrastructure/Services/RealTimeNotificationService.cs`
- MQTT Integration: `Koornik.Infrastructure/Mqtt/MqttMessageHandler.cs`
