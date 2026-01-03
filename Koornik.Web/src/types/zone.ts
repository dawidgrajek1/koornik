/**
 * Zone types matching the backend API schema.
 */

import type { Polygon } from 'geojson';

/** Response from GET /api/v1/zones and GET /api/v1/zones/{id} */
export interface Zone {
  id: string;
  name: string | null;
  capacity: number;
  /** GeoJSON polygon for floor plan rendering */
  geoJson?: Polygon | null;
}

/** Response from GET /api/v1/zones/{id}/state */
export interface ZoneState {
  zoneId: string;
  zoneName: string | null;
  timestamp: string;
  occupancyCount: number;
  capacity: number;
  /** Calculated percentage (0-100) */
  occupancyPercentage: number;
}

/** Response from GET /api/v1/zones/{id}/state/history */
export interface ZoneStateHistory {
  zoneId: string;
  zoneName: string | null;
  startDate: string;
  endDate: string;
  states: ZoneState[] | null;
}

/** Request body for POST /api/v1/zones */
export interface CreateZoneRequest {
  name?: string | null;
  capacity: number;
}

/** Request body for PUT /api/v1/zones/{id} */
export interface UpdateZoneRequest {
  id: string;
  name?: string | null;
  capacity: number;
  geoJson?: Polygon | null;
}
