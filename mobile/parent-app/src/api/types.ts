/** Token pair returned by the auth endpoints. */
export interface AuthTokens {
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
}

/** A child of the signed-in parent. */
export interface Child {
  studentId: string;
  fullName: string;
  admissionNumber: string;
  className: string | null;
  sectionName: string | null;
  rollNumber: number | null;
  photoUrl: string | null;
}

export type AttendanceStatus = 1 | 2 | 3 | 4 | 5; // Present, Absent, Late, HalfDay, Leave

export const ATTENDANCE_LABELS: Record<AttendanceStatus, string> = {
  1: 'Present',
  2: 'Absent',
  3: 'Late',
  4: 'Half day',
  5: 'Leave',
};

export interface AttendanceDay {
  date: string; // yyyy-MM-dd
  status: AttendanceStatus;
  remarks: string | null;
}

export interface MonthAttendance {
  studentId: string;
  year: number;
  month: number;
  days: AttendanceDay[];
  presentCount: number;
  absentCount: number;
  lateCount: number;
  halfDayCount: number;
  leaveCount: number;
  markedDays: number;
  attendancePercent: number;
}

export interface Exam {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: number; // 2 = Published (parents only ever see these)
}

export interface ResultLine {
  subjectName: string;
  maxMarks: number;
  marksObtained: number | null;
  isAbsent: boolean;
  grade: string;
  passed: boolean;
}

export interface StudentResult {
  studentId: string;
  examId: string;
  examName: string;
  lines: ResultLine[];
  totalMax: number;
  totalObtained: number;
  percent: number;
  overallGrade: string;
  sectionRank: number | null;
  sectionSize: number;
}

export interface FeeDueLine {
  feeHeadName: string;
  amount: number;
  dueDate: string;
  overdue: boolean;
}

export interface FeePayment {
  id: string;
  receiptNumber: string;
  amount: number;
  paidOn: string;
  mode: number;
  reference: string | null;
}

export interface Notice {
  id: string;
  title: string;
  body: string;
  schoolClassId: string | null;
  expiresOn: string | null;
  isPinned: boolean;
  publishedAt: string;
}

export interface Homework {
  id: string;
  className: string;
  subjectName: string;
  title: string;
  instructions: string;
  assignedOn: string;
  dueDate: string;
}

export interface TimetableEntry {
  id: string;
  dayOfWeek: number; // 1 = Monday … 7 = Sunday
  period: number;
  startTime: string; // HH:mm:ss
  endTime: string;
  subjectName: string;
  teacherName: string | null;
}

/** Live bus state while a trip is running on the child's route. */
export interface BusLocation {
  tripType: 1 | 2; // 1 = Pickup, 2 = Drop
  startedAt: string;
  latitude: number | null;
  longitude: number | null;
  lastSeenAt: string | null;
}

export interface ChildTransport {
  routeName: string;
  stopName: string;
  pickupTime: string | null;
  driverName: string | null;
  driverPhone: string | null;
  vehicleRegistration: string | null;
}

export interface FeeSummary {
  studentId: string;
  dueLines: FeeDueLine[];
  payments: FeePayment[];
  totalDue: number;
  totalPaid: number;
  balance: number;
}
