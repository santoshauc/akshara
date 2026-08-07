# SchoolErp — Parent & Driver App Manual

Both apps sign in with the school code and a phone number: the school
office registers the number, the app sends a one-time SMS code, and the
session then stays signed in.

---

## Parent app

Sign in with the phone number registered as a **guardian**. All linked
children appear as tabs at the top; everything below is for the selected
child.

| Card | What it shows |
|---|---|
| **Attendance this month** | Percentage plus a day-by-day list (absences, lates, leaves). |
| **Transport** | Bus route, the child's stop and pickup time, and a tap-to-call button for the driver. |
| **Live bus** | During a trip: "Pickup/Drop trip in progress", when the bus was last seen, and an *Open bus location in Maps* button. Updates itself every few seconds; outside trips it shows "The bus is not on a trip right now." |
| **Class timetable** | The published schedule as day tabs (defaults to today) with a period-by-period timeline — subject, time, teacher. |
| **Homework** | Assignments for the child's class with due dates. |
| **Latest result** | The most recent published exam: per-subject marks, total, grade, and section rank. |
| **Fees** | Outstanding balance, upcoming dues, and past receipts. |
| **Notices** | School announcements, pinned ones first. |

Parents also receive SMS automatically: absence alerts, published results,
fee receipts, and bus boarding/drop notifications.

Pull down to refresh. *Sign out* is at the top right.

---

## Driver app

Sign in with the phone number the school assigned to your route. The app
has one screen — your route.

### Before the trip

The **pre-trip inspection checklist** (fuel, tyres, brakes, emergency kit)
must be fully ticked before *Start trip* unlocks — the server refuses a
trip without a completed inspection. Choose **Morning pickup** or
**Evening drop**, then start.

### During the trip

- The green banner confirms the trip is running and **GPS is on** — your
  location is sent automatically every few seconds so parents can see the
  bus. Keep the app open.
- Riders are grouped by stop, in stop order. As students board (or get
  off), tap **Board** / **Drop** — the guardian is notified by SMS
  immediately. Tap **Absent** for a no-show.
- Each student can only be marked once per trip; duplicate taps are
  ignored (no duplicate SMS).

### Ending

Tap **End trip**. The checklist resets for the next trip. Only one trip
can be active on a route at a time.

### Troubleshooting

- **"Trip cannot start"** — tick every inspection item first.
- **No riders listed** — the school hasn't allocated students to your
  route's stops yet; contact the office.
- **Location not updating** — allow location permission for the app and
  keep it in the foreground during trips.
