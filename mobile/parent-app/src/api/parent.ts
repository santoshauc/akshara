import { API_BASE_URL } from '../config';
import { ApiError, request, tokenStore } from './client';
import {
  AuthTokens,
  Child,
  ChildTransport,
  Exam,
  FeeSummary,
  Homework,
  MonthAttendance,
  Notice,
  StudentResult,
} from './types';

/** Requests an SMS OTP. Always succeeds from the caller's perspective. */
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

export const parentApi = {
  getChildren: () => request<Child[]>('/api/v1/parent/children'),

  getAttendance: (studentId: string, year: number, month: number) =>
    request<MonthAttendance>(
      `/api/v1/parent/children/${studentId}/attendance?year=${year}&month=${month}`,
    ),

  getExams: (studentId: string) =>
    request<Exam[]>(`/api/v1/parent/children/${studentId}/exams`),

  getResult: (studentId: string, examId: string) =>
    request<StudentResult>(`/api/v1/parent/children/${studentId}/exams/${examId}/result`),

  getFees: (studentId: string) =>
    request<FeeSummary>(`/api/v1/parent/children/${studentId}/fees`),

  getNotices: (studentId: string) =>
    request<Notice[]>(`/api/v1/parent/children/${studentId}/notices`),

  getHomework: (studentId: string) =>
    request<Homework[]>(`/api/v1/parent/children/${studentId}/homework`),

  /** Returns null when the child has no transport allocation (204). */
  getTransport: (studentId: string) =>
    request<ChildTransport | undefined>(
      `/api/v1/parent/children/${studentId}/transport`,
    ).then((t) => t ?? null),
};
