/**
 * Device types matching the backend API schema.
 */

/** Response from GET /api/v1/devices and GET /api/v1/devices/{id} */
export interface Device {
  id: string;
  name: string | null;
  ipAddress: string | null;
  macAddress: string | null;
  frontZoneId: string | null;
  backZoneId: string | null;
}

/** Request body for POST /api/v1/devices */
export interface CreateDeviceRequest {
  name?: string | null;
  ipAddress?: string | null;
  macAddress?: string | null;
  frontZoneId?: string | null;
  backZoneId?: string | null;
}

/** Request body for PUT /api/v1/devices/{id} */
export interface UpdateDeviceRequest {
  id: string;
  name?: string | null;
  ipAddress?: string | null;
  macAddress?: string | null;
  frontZoneId?: string | null;
  backZoneId?: string | null;
}
