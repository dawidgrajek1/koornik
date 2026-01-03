/**
 * SignalR event types matching the backend hub contract.
 * See: .github/README-SignalR.md
 */

/** Direction of transition through a device */
export type TransitionDirection = 'IN' | 'OUT';

/** Received via OnTransition event */
export interface TransitionEvent {
  transitionId: string;
  deviceId: string;
  deviceName: string;
  macAddress: string;
  direction: TransitionDirection;
  fromZoneId: string | null;
  toZoneId: string | null;
  timestamp: string;
}

/** Received via OnDeviceStatus event */
export interface DeviceStatusEvent {
  macAddress: string;
  state: string;
  thresholdFront: number | null;
  thresholdBack: number | null;
  distanceStreamEnabled: boolean | null;
  timestamp: string;
}

/** Received via OnDistanceReading event (subscription required) */
export interface DistanceReadingEvent {
  macAddress: string;
  frontDistance: number;
  backDistance: number;
  timestamp: string;
}
