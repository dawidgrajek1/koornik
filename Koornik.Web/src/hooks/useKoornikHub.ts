/**
 * SignalR hook for real-time communication with the Koornik hub.
 */

import { useEffect, useState, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { HUB_URL } from '../config/env';
import type {
  TransitionEvent,
  DeviceStatusEvent,
  DistanceReadingEvent,
} from '../types/signalr';

/** Maximum number of transitions to keep in history */
const MAX_TRANSITIONS = 100;

/** Connection state */
export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface UseKoornikHubResult {
  /** Current connection state */
  connectionState: ConnectionState;
  /** Recent transition events (newest first) */
  transitions: TransitionEvent[];
  /** Device statuses by MAC address */
  deviceStatuses: Map<string, DeviceStatusEvent>;
  /** Latest distance reading (for subscribed devices) */
  distanceReading: DistanceReadingEvent | null;
  /** Subscribe to a specific device's events (for distance readings) */
  subscribeToDevice: (macAddress: string) => Promise<void>;
  /** Unsubscribe from a specific device */
  unsubscribeFromDevice: (macAddress: string) => Promise<void>;
  /** Subscribe to a zone's transition events */
  subscribeToZone: (zoneId: string) => Promise<void>;
  /** Unsubscribe from a zone */
  unsubscribeFromZone: (zoneId: string) => Promise<void>;
  /** Clear all transitions */
  clearTransitions: () => void;
}

export function useKoornikHub(options?: { onDeviceStatus?: (event: DeviceStatusEvent) => void }): UseKoornikHubResult {
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
  const [transitions, setTransitions] = useState<TransitionEvent[]>([]);
  const [deviceStatuses, setDeviceStatuses] = useState<Map<string, DeviceStatusEvent>>(new Map());
  const [distanceReading, setDistanceReading] = useState<DistanceReadingEvent | null>(null);
  
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Initialize connection
  useEffect(() => {
    if (!HUB_URL) {
      console.error('HUB_URL is not configured');
      return;
    }

    // Track if cleanup has been called (for React Strict Mode)
    let isCancelled = false;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    connectionRef.current = connection;

    // Register event handlers
    connection.on('OnTransition', (event: TransitionEvent) => {
      setTransitions(prev => [event, ...prev].slice(0, MAX_TRANSITIONS));
    });

    connection.on('OnDeviceStatus', (event: DeviceStatusEvent) => {
      console.log('Received DeviceStatus event:', event);

      setDeviceStatuses(prev => {
        const next = new Map(prev);
        next.set(event.macAddress, event);
        return next;
      });

      // Notify consumer about each DeviceStatus event (e.g. to prompt on unknown devices)
      try {
        
        options?.onDeviceStatus?.(event);
      } catch (err) {
        // Swallow consumer errors to avoid breaking the hub
        console.error('onDeviceStatus handler threw:', err);
      }
    });

    connection.on('OnDistanceReading', (event: DistanceReadingEvent) => {
      setDistanceReading(event);
    });

    // Connection state handlers
    connection.onclose(() => {
      if (!isCancelled) setConnectionState('disconnected');
    });
    connection.onreconnecting(() => {
      if (!isCancelled) setConnectionState('reconnecting');
    });
    connection.onreconnected(() => {
      if (!isCancelled) setConnectionState('connected');
    });

    // Start connection
    setConnectionState('connecting');
    connection
      .start()
      .then(() => {
        if (!isCancelled) {
          setConnectionState('connected');
          console.log('SignalR connected');
        }
      })
      .catch(err => {
        // Ignore errors caused by cleanup during connection attempt
        if (!isCancelled) {
          console.error('SignalR connection failed:', err);
          setConnectionState('disconnected');
        }
      });

    // Cleanup
    return () => {
      isCancelled = true;
      connection.stop();
    };
  }, []);

  const subscribeToDevice = useCallback(async (macAddress: string) => {
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('SubscribeToDevice', macAddress);
        console.log(`Subscribed to device: ${macAddress}`);
      } catch (err) {
        console.error('Failed to subscribe to device:', err);
      }
    }
  }, []);

  const unsubscribeFromDevice = useCallback(async (macAddress: string) => {
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('UnsubscribeFromDevice', macAddress);
        console.log(`Unsubscribed from device: ${macAddress}`);
        setDistanceReading(null);
      } catch (err) {
        console.error('Failed to unsubscribe from device:', err);
      }
    }
  }, []);

  const subscribeToZone = useCallback(async (zoneId: string) => {
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('SubscribeToZone', zoneId);
        console.log(`Subscribed to zone: ${zoneId}`);
      } catch (err) {
        console.error('Failed to subscribe to zone:', err);
      }
    }
  }, []);

  const unsubscribeFromZone = useCallback(async (zoneId: string) => {
    const connection = connectionRef.current;
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('UnsubscribeFromZone', zoneId);
        console.log(`Unsubscribed from zone: ${zoneId}`);
      } catch (err) {
        console.error('Failed to unsubscribe from zone:', err);
      }
    }
  }, []);

  const clearTransitions = useCallback(() => {
    setTransitions([]);
  }, []);

  return {
    connectionState,
    transitions,
    deviceStatuses,
    distanceReading,
    subscribeToDevice,
    unsubscribeFromDevice,
    subscribeToZone,
    unsubscribeFromZone,
    clearTransitions,
  };
}
