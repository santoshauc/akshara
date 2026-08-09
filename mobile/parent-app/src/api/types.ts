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
  /** Accrued once the line is past due (per the head's fine rule). */
  lateFine: number;
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

/** One library loan of the child. */
export interface BookLoan {
  id: string;
  bookTitle: string;
  author: string;
  issuedOn: string;
  dueOn: string;
  returnedOn: string | null;
  overdue: boolean;
}

/** The child's hostel stay. */
export interface ChildHostel {
  hostelName: string;
  roomNumber: string;
  wardenName: string | null;
  wardenPhone: string | null;
  allocatedOn: string;
}

/** A leave request for a child. Status: 1 pending, 2 approved, 3 rejected. */
export interface LeaveRequest {
  id: string;
  fromDate: string;
  toDate: string;
  reason: string;
  status: number;
  decisionNote: string | null;
  requestedAt: string;
}

/** One message in the parent↔school conversation. */
export interface StudentMessage {
  id: string;
  sentByStaff: boolean;
  senderName: string;
  body: string;
  sentAt: string;
  read: boolean;
}

/** One child's line in the family fee view. */
export interface FamilyChildFee {
  studentId: string;
  studentName: string;
  className: string | null;
  totalDue: number;
  totalConcession: number;
  totalPaid: number;
  balance: number;
}

/** The whole family's fee position. */
export interface FamilyFeeSummary {
  children: FamilyChildFee[];
  familyBalance: number;
}

/** Online payment order with its browser checkout URL. */
export interface FeeOrder {
  orderId: string;
  gatewayOrderId: string;
  amount: number;
  checkoutUrl: string;
}

export interface FeeSummary {
  studentId: string;
  dueLines: FeeDueLine[];
  payments: FeePayment[];
  totalLateFine: number;
  totalConcession: number;
  totalDue: number;
  totalPaid: number;
  balance: number;
}

/** One subject: the child's % beside the section average. */
export interface SubjectComparison {
  subject: string;
  childPercent: number;
  classAverage: number;
}

/** How the child compares with their section — anonymous aggregates only. */
export interface StudentInsights {
  examName: string | null;
  subjects: SubjectComparison[];
  rank: number | null;
  sectionSize: number | null;
  childAttendancePercent: number | null;
  classAttendancePercent: number | null;
}
