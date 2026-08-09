# SchoolErp — Roadmap v3 (insights & growth)

Roadmap v2 closed the operational gaps (see PROJECT_STATUS.md). This roadmap
adds the features that help a school GROW and UNDERSTAND itself: family-level
fee views, an admissions pipeline, and analytics for every audience —
management, teachers and parents. Work top-to-bottom; every item ships with
integration tests, portal/app UI where relevant, browser verification, doc
updates and one commit.

## Phase D — insights & growth

| # | Item | Acceptance criteria | Status |
|---|---|---|---|
| D1 | Sibling fee views | Parents with multiple enrolled children see ONE family view: each child's balance (dues + fines − concessions − paid) plus the combined family total, in the parent app (`GET parent/family/fees`) without switching children. Staff get the same family ledger from any sibling's profile (guardian-linked children listed with balances and profile links). Existing per-child Pay-now flows unchanged. Family balance never counts a child twice when both parents are linked. | DONE |
| D2 | Admissions enquiries (CRM) | AdmissionEnquiry entity (RLS): child name, DOB, class applied for, parent name/phone/email, source (WalkIn/Phone/Website/Referral), status pipeline New → Contacted → Visit → Admitted / Lost, follow-up date, free-text notes. Portal "Admissions" page: create, filter by status, follow-ups due highlighted, one-click Convert that pre-fills the admission form and stamps the enquiry Admitted with the created StudentId. Dashboard gains open-enquiries + follow-ups-due-today tiles. New permissions admissions.view/manage (claims backfilled). | DONE |
| D3 | Management insights | New permission insights.view (SchoolAdmin only via backfill). GET insights/management returns: 30-day daily attendance % trend, 6-month fee-collection series + outstanding total, per-class attendance % this month, per-exam average % (published, current year), enquiry funnel counts, substitution count this month. Portal "Insights" page renders them as CHARTS (MudChart line/bar/donut) with plain-language captions ("Collections up X% vs last month"). Every number tenant-scoped; empty modules degrade to friendly empty states. | DONE |
| D4 | Teacher performance insights | Per teacher: periods/week taught, average % achieved in the papers of subjects they teach (published exams, current year), delta vs the school-wide average for those same exams, days absent (covers arranged), and marks-entry backlog (assigned papers without marks). Rendered on the Insights page as a comparative bar chart + table. Framed as "teaching outcomes" (correlation, not verdict) in the caption. | |
| D5 | Student peer comparison (parents + staff) | GET parent/children/{id}/insights: per-subject child % vs class average for the latest published exam, monthly attendance % vs class average, existing rank/size. Parent app gains an "Insights" card: side-by-side bars per subject (no chart library — flex-width bars), attendance comparison, localized en/te. Staff see the same comparison on the student profile beside the report card. Peers = same section, published exams only; never exposes another child's identity. | |

## Design guardrails for this phase

- **Comparisons stay anonymous.** Peer data is always an aggregate (class
  average, rank of N) — never another named student's marks.
- **Teacher metrics are descriptive, not disciplinary.** Show outcomes with
  context (class sizes, subjects), caption them as correlations.
- **Charts degrade.** A school with no exams/fees yet must see friendly
  empty states, not broken axes.
- **No new chart dependencies.** Portal uses MudChart; the parent app draws
  proportional flex-width bars.

## Standing rules (unchanged from v2)

Module pattern, RLS on every new tenant table, permission constants +
startup claims backfill, outbox for side effects, zero-warning builds,
tests before UI, browser-verify everything, update PROJECT_STATUS.md and
this file's Status column, one commit per item.
