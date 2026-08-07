import { API_BASE_URL } from '../config';
import { ApiError, request, tokenStore } from './client';
import { AuthTokens, DriverRoute, RiderEventType, TripType } from './types';

/** Requests an SMS OTP for the driver's phone. */
export async function requestOtp(schoolCode: string, phone: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/otp/request`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ schoolCode, phone }),
  });
  if (!response.ok) {
    throw new ApiError(response.status, 'Could not request a code. Check the school code.');
  }
}

/** Completes OTP login and stores the session. */
export async function verifyOtp(
  schoolCode: string,
  phone: string,
  code: string,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/otp/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ schoolCode, phone, code }),
  });
  if (!response.ok) {
    throw new ApiError(response.status, 'Invalid or expired code.');
  }

  const tokens = (await response.json()) as AuthTokens;
  await tokenStore.set(tokens);
}

/** Revokes the session server-side and clears local tokens. */
export async function logout(): Promise<void> {
  const refreshToken = await tokenStore.getRefresh();
  if (refreshToken) {
    try {
      await fetch(`${API_BASE_URL}/api/v1/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
    } catch {
      // Offline logout still clears the local session.
    }
  }
  await tokenStore.clear();
}

export const driverApi = {
  getRoute: () => request<DriverRoute>('/api/v1/driver/route'),

  startTrip: (type: TripType, inspectionOk: boolean, inspectionNotes: string | null) =>
    request<string>('/api/v1/driver/trips', {
      method: 'POST',
      body: JSON.stringify({ type, inspectionOk, inspectionNotes }),
    }),

  recordLocation: (latitude: number, longitude: number) =>
    request<void>('/api/v1/driver/location', {
      method: 'POST',
      body: JSON.stringify({ latitude, longitude }),
    }),

  markRider: (studentId: string, eventType: RiderEventType, remarks: string | null) =>
    request<void>(`/api/v1/driver/riders/${studentId}/events`, {
      method: 'POST',
      body: JSON.stringify({ eventType, remarks }),
    }),

  endTrip: () => request<void>('/api/v1/driver/trips/end', { method: 'POST' }),
};
