/**
 * Environment configuration with typed access to Vite env variables.
 * All env variables must be prefixed with VITE_ to be exposed to the client.
 */

/** Backend API base URL (without trailing slash) */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

/** SignalR Hub URL */
export const HUB_URL = import.meta.env.VITE_HUB_URL as string;

// Validate required env variables in development
if (import.meta.env.DEV) {
  if (!API_BASE_URL) {
    console.warn('VITE_API_BASE_URL is not set. API calls will fail.');
  }
  if (!HUB_URL) {
    console.warn('VITE_HUB_URL is not set. SignalR connection will fail.');
  }
}
