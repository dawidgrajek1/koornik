/**
 * API service for communicating with the Koornik backend.
 */

import { API_BASE_URL } from '../config/env';
import type { Device } from '../types/device';
import type { Zone, ZoneState } from '../types/zone';
import type { Transition } from '../types/transition';

/**
 * Generic fetch wrapper with error handling.
 */
async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${endpoint}`;
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const errorBody = await response.text();
    throw new Error(`API error ${response.status}: ${errorBody}`);
  }

  // Handle 204 No Content
  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}

// ============================================================================
// Device API
// ============================================================================

/** Fetch all devices */
export async function getDevices(): Promise<Device[]> {
  return fetchApi<Device[]>('/api/v1/devices');
}

/** Fetch a single device by ID */
export async function getDevice(id: string): Promise<Device> {
  return fetchApi<Device>(`/api/v1/devices/${id}`);
}

/** Fetch a device by MAC address */
export async function getDeviceByMac(macAddress: string): Promise<Device> {
  return fetchApi<Device>(`/api/v1/devices/by-mac/${encodeURIComponent(macAddress)}`);
}

/** Fetch a device by zone ID */
export async function getDeviceByZone(zoneId: string): Promise<Device> {
  return fetchApi<Device>(`/api/v1/devices/by-zone/${zoneId}`);
}

// ============================================================================
// Zone API
// ============================================================================

/** Fetch all zones */
export async function getZones(): Promise<Zone[]> {
  return fetchApi<Zone[]>('/api/v1/zones');
}

/** Fetch a single zone by ID */
export async function getZone(id: string): Promise<Zone> {
  return fetchApi<Zone>(`/api/v1/zones/${id}`);
}

/** Fetch current zone state (occupancy) */
export async function getZoneState(id: string): Promise<ZoneState> {
  return fetchApi<ZoneState>(`/api/v1/zones/${id}/state`);
}

/** Fetch zone state at a specific timestamp */
export async function getZoneStateAt(id: string, timestamp: string): Promise<ZoneState> {
  return fetchApi<ZoneState>(`/api/v1/zones/${id}/state/at?timestamp=${encodeURIComponent(timestamp)}`);
}

/** Fetch all zone states (convenience function) */
export async function getAllZoneStates(zones: Zone[]): Promise<Map<string, ZoneState>> {
  const states = await Promise.all(
    zones.map(zone => getZoneState(zone.id).catch(() => null))
  );
  
  const stateMap = new Map<string, ZoneState>();
  states.forEach((state, index) => {
    if (state) {
      stateMap.set(zones[index].id, state);
    }
  });
  
  return stateMap;
}

// ============================================================================
// Transition API
// ============================================================================

/** Fetch all transitions */
export async function getTransitions(): Promise<Transition[]> {
  return fetchApi<Transition[]>('/api/v1/transitions');
}

/** Fetch a single transition by ID */
export async function getTransition(id: string): Promise<Transition> {
  return fetchApi<Transition>(`/api/v1/transitions/${id}`);
}

/** Fetch transitions by zone with optional time range */
export async function getTransitionsByZone(
  zoneId: string,
  start?: string,
  end?: string
): Promise<Transition[]> {
  const params = new URLSearchParams();
  if (start) params.append('start', start);
  if (end) params.append('end', end);
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchApi<Transition[]>(`/api/v1/transitions/by-zone/${zoneId}${query}`);
}

/** Fetch transitions by device with optional time range */
export async function getTransitionsByDevice(
  deviceId: string,
  start?: string,
  end?: string
): Promise<Transition[]> {
  const params = new URLSearchParams();
  if (start) params.append('start', start);
  if (end) params.append('end', end);
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchApi<Transition[]>(`/api/v1/transitions/by-device/${deviceId}${query}`);
}

/** Fetch transitions by time range */
export async function getTransitionsByTimeRange(
  start?: string,
  end?: string
): Promise<Transition[]> {
  const params = new URLSearchParams();
  if (start) params.append('start', start);
  if (end) params.append('end', end);
  const query = params.toString() ? `?${params.toString()}` : '';
  return fetchApi<Transition[]>(`/api/v1/transitions/by-time-range${query}`);
}

// ============================================================================
// Health API
// ============================================================================

/** Check API health */
export async function checkHealth(): Promise<boolean> {
  try {
    await fetch(`${API_BASE_URL}/health/ready`);
    return true;
  } catch {
    return false;
  }
}
