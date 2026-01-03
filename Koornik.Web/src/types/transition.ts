/**
 * Transition types matching the backend API schema.
 */

/** Response from GET /api/v1/transitions and related endpoints */
export interface Transition {
  id: string;
  deviceId: string;
  createdAt: string;
  fromZoneId: string | null;
  toZoneId: string | null;
}

/** Request body for POST /api/v1/transitions */
export interface CreateTransitionRequest {
  deviceId: string;
  fromZoneId?: string | null;
  toZoneId?: string | null;
  createdAt?: string | null;
}
