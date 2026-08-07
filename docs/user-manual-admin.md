# SchoolErp — School Admin Portal Manual

The portal is a web app (installable as a PWA). Sign in with your school
code, email/phone, and password. If your account has two-factor
authentication turned on, you'll also enter a 6-digit code from your
authenticator app.

Menu items appear according to your role's permissions — if you don't see a
section below, your role doesn't include it.

## First-time setup (a new school, in order)

1. **Academics** — create the academic year (mark it *current*), classes
   with sections, and subjects.
2. **Teachers** — add teaching staff (employee code, phone, qualification).
3. **Students** — admit students: personal details, guardians (a guardian's
   phone number is their parent-app login; siblings share one guardian
   record automatically), and the class/section placement.
4. **Fees** — define fee heads (Tuition, Transport…), then a per-class fee
   plan for the year.
5. **Transport** (if applicable) — vehicles, routes with ordered stops,
   driver assignment (the driver's phone is their driver-app login), and
   per-student stop allocations.
6. **Timetable** — see below; publish when ready.

## Daily work

### Attendance
Pick class, section, and date; mark each student Present / Absent / Late /
Half day / Leave and save. Guardians of absent students automatically get
an SMS.

### Exams and results
Create an exam with papers per class/subject (max/pass marks), enter marks,
then **Publish**. Publishing computes totals, grades, and section ranks,
makes results visible in the parent app, and sends result SMS to guardians.
Unpublished marks are never visible to parents.

### Fees
Record counter payments against a student — receipts are numbered
sequentially per school and an SMS confirmation goes out. Online payments
made from the parent app reconcile automatically through the payment
gateway webhook.

### Notices and homework
Notices can target the whole school or one class, carry an expiry date, and
can be pinned. Homework is per class/section with a due date. Both appear
in the parent app immediately.

## Timetable (draft → publish)

*Timetable* in the menu. Pick a class (and optionally a section — empty
means "all sections"). Add slots: day, period, times, subject, and teacher.

- **Teacher picker**: choose a registered teacher, or type a free-text name
  for a guest. The system **rejects a save that double-books a teacher** at
  overlapping times — in this timetable or any other class's.
- Saving stores a **draft**: parents see nothing until you press **Publish
  to parents**. Editing a published timetable returns it to draft.
- The week grid below the editor previews exactly what parents will see.

Each teacher's combined weekly schedule (all classes) is on the *Teachers*
page via the calendar icon.

## Oversight

### Audit log
Every change made by staff — who, what, when, from which IP — searchable
and filterable by date. Read-only and append-only.

### My devices  (account menu)
Everywhere your account is signed in. *Sign out* any session you don't
recognise; that device's login dies within minutes.

### Security  (account menu)
Turn two-factor authentication on: add the setup key to an authenticator
app, confirm a code, and **store the 8 recovery codes** — they are shown
only once and each works once. Turning MFA off requires a current code.

## Troubleshooting

- **A new menu item is missing after an upgrade** — sign out and back in;
  permissions are embedded in your login session.
- **Locked out** — five wrong passwords (or MFA codes) lock the account
  for 15 minutes.
- **Stale screen after an upgrade** — the portal updates itself on the
  next load; if something looks off once, hard-refresh (Ctrl+Shift+R).
