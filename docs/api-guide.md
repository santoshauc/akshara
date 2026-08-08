# SchoolErp — API Guide

Base URL: `http://localhost:5199` (dev). All endpoints live under
`/api/v1/…`. Interactive documentation: `/swagger` (Development only; the
root `/` redirects there).

## Conventions

- **Auth**: `Authorization: Bearer <accessToken>`. Access tokens last
  15 minutes; refresh with the rotating refresh token. Errors are RFC 7807
  problem details. Validation failures → 400; missing permission → 403;
  cross-tenant/nonexistent resources → 404 (never 403 — no existence leaks);
  business conflicts (duplicates, double-booking, inspection gate) → 409.
- **Permissions** are claims inside the JWT (e.g. `students.manage`).
  After role-claim changes, users must sign out/in to receive them.
- **Rate limits**: a global limiter on all controllers and a stricter one
  on `/auth/*`.

## Authentication flows

### Staff (password, optional MFA)

```
POST /api/v1/auth/login          { schoolCode, login, password }
  → 200 { accessToken, expiresInSeconds, refreshToken }        (no MFA)
  → 200 { mfaRequired: true, mfaToken }                        (MFA on)
POST /api/v1/auth/mfa/verify     { mfaToken, code }   code = TOTP or recovery
  → 200 { accessToken, expiresInSeconds, refreshToken }
```

Empty `schoolCode` = platform (Super Admin) sign-in.

### Parents & drivers (SMS OTP)

```
POST /api/v1/auth/otp/request    { schoolCode, phone }   → 202 always
POST /api/v1/auth/otp/verify     { schoolCode, phone, code } → tokens
```

### Session management

```
POST   /api/v1/auth/refresh          { refreshToken } → new token pair
POST   /api/v1/auth/logout           { refreshToken } → 204
GET    /api/v1/auth/sessions          → active devices for the caller
DELETE /api/v1/auth/sessions/{id}     → sign out one device
GET    /api/v1/auth/mfa               → { enabled }
POST   /api/v1/auth/mfa/enroll        → { sharedKey, authenticatorUri }
POST   /api/v1/auth/mfa/enable        { code } → { recoveryCodes[8] }
POST   /api/v1/auth/mfa/disable       { code } → 204
```

## Module endpoints (staff portal)

Requirements shown as `permission`.

| Area | Endpoints (under /api/v1) | Permission |
|---|---|---|
| Tenants (platform) | `GET/POST/PUT tenants`, status changes | `tenants.view/manage` |
| Academics | `academics/years`, `academics/classes` (+sections), `exams/subjects` | `academics.*`, `exams.manage` |
| Students | `students` (admit/list/profile; guardians dedup by phone) | `students.view/manage` |
| Attendance | `attendance` mark grid + month queries; absence SMS via outbox | `attendance.view/mark` |
| Exams | exams, papers, marks entry, publish (+result SMS), results with rank | `exams.*` |
| Fees | `fees/heads`, `fees/structure`, `fees/payments` (receipts, sequential), `fees/orders` (gateway), `fees/students/{id}` | `fees.*` |
| Notices | CRUD, class-scoped visibility, pinning | `communication.*` |
| Homework | CRUD per class/section | `homework.view/manage` |
| Transport | vehicles, routes, stops, student assignments | `transport.view/manage` |
| Timetable | `GET/PUT timetable` (define = full replace, drafts), `POST timetable/publish` | `timetable.view/manage` |
| Teachers | `teachers` CRUD, `teachers/{id}/schedule`; define-time clash detection lives on timetable PUT | `staff.view/manage` |
| Library | `library/books` (catalog + add), `library/loans` (issue, `loans/{id}/return`, open/overdue lists) | `library.view/manage` |
| Hostel | `hostel` (+ `/{id}/rooms`), `hostel/allocations` (allocate, `/{id}/vacate`) | `hostel.view/manage` |
| Audit | `GET audit?search&from&to` — latest 200, school-scoped | `audit.view` |

## Parent app API (identity-scoped, no permission claims)

All under `/api/v1/parent`, resolved through guardian links; a child that
isn't yours is a 404.

```
GET parent/children
GET parent/children/{id}/attendance?year&month
GET parent/children/{id}/exams            → published only
GET parent/children/{id}/exams/{examId}/result
GET parent/children/{id}/fees
GET parent/children/{id}/notices
GET parent/children/{id}/homework
GET parent/children/{id}/timetable        → published entries only
GET parent/children/{id}/transport        → 204 when no allocation
GET parent/children/{id}/bus              → live trip + last GPS fix; 204 when idle
GET parent/children/{id}/library          → book loans (open + history)
GET parent/children/{id}/hostel           → stay + warden contact; 204 for day scholars
GET parent/children/{id}/exams/{examId}/report-card → PDF (published only)
```

## Driver app API (identity-scoped)

All under `/api/v1/driver`, resolved by the driver's user id/phone.

```
GET  driver/route                 → route, stops, rider manifest, active trip
POST driver/trips                 { type: 1|2, inspectionOk, inspectionNotes }
                                  → 409 unless the inspection checklist passed
POST driver/location              { latitude, longitude }   (active trip)
POST driver/riders/{studentId}/events  { eventType: 1|2|3 } (board/drop/absent)
                                  → idempotent; guardian SMS via outbox
POST driver/trips/end
```

## Webhooks

`POST /api/v1/payments/webhook` — anonymous; the body is the gateway event
and the signature header is verified (Razorpay: `X-Razorpay-Signature`,
HMAC-SHA256 of the raw body). Invalid signatures are rejected. Replays are
idempotent — a captured payment is recorded exactly once.
