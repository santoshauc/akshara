import { API_BASE_URL } from '../config';
import { ApiError, request, requestBytes, tokenStore } from './client';
import {
  AuthTokens,
  BookLoan,
  BusLocation,
  Child,
  ChildHostel,
  ChildTransport,
  Exam,
  FeeOrder,
  FeeSummary,
  Homework,
  MonthAttendance,
  Notice,
  StudentResult,
  TimetableEntry,
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

  getTimetable: (studentId: string) =>
    request<TimetableEntry[]>(`/api/v1/parent/children/${studentId}/timetable`),

  /** Returns null when the child has no transport allocation (204). */
  getTransport: (studentId: string) =>
    request<ChildTransport | undefined>(
      `/api/v1/parent/children/${studentId}/transport`,
    ).then((t) => t ?? null),

  /** Returns null when no trip is active on the child's route (204). */
  getBus: (studentId: string) =>
    request<BusLocation | undefined>(`/api/v1/parent/children/${studentId}/bus`).then(
      (b) => b ?? null,
    ),

  /** The published report card as PDF bytes. 404 until the exam is published. */
  getReportCard: (studentId: string, examId: string) =>
    requestBytes(`/api/v1/parent/children/${studentId}/exams/${examId}/report-card`),

  getLibrary: (studentId: string) =>
    request<BookLoan[]>(`/api/v1/parent/children/${studentId}/library`),

  /** Creates an online payment order; open checkoutUrl in a browser. */
  createFeeOrder: (studentId: string, amount: number) =>
    request<FeeOrder>(`/api/v1/parent/children/${studentId}/fees/orders`, {
      method: 'POST',
      body: JSON.stringify({ amount }),
    }),

  /** Returns null for day scholars (204). */
  getHostel: (studentId: string) =>
    request<ChildHostel | undefined>(`/api/v1/parent/children/${studentId}/hostel`).then(
      (h) => h ?? null,
    ),
};
