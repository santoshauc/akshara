# Akshara design system

The portal is benchmarked against mature enterprise software rather than
admin-dashboard templates. This document records *which principle came from
where* so future screens are argued from a reference, not from taste.

Nothing is copied — no branding, palettes, logos or proprietary assets. What
is borrowed is structure: how these products organise dense information and
how they let people act on it.

## Where each reference applies

| Reference | What we take | Where it shows up |
| --- | --- | --- |
| **Dynamics 365** | Command bar over the record; primary action rightmost, secondary actions beside it, rarely-used ones in overflow. Record header carries identity + status + key fields before any tab. | `AkPageHeader` actions slot; entity detail pages |
| **ServiceNow** | List-first information architecture: persistent toolbar, explicit active-filter state, result counts, bulk operations on selection, status as a first-class column. | `AkListToolbar`, `AkFilterChips`, list pages |
| **Salesforce Lightning** | Record page = header + tabs for related information, so a record is one destination rather than a hub of separate screens. | Student / teacher detail pages |
| **Power BI** | Page-level filter bar that governs everything below it; KPIs state a number *and* its comparison; an explicit "last updated" so nobody trusts stale figures. | Dashboard, `AkMetric` |
| **SAP Fiori** | One layout grammar reused everywhere; responsive rules defined per component rather than per page; accessibility treated as a requirement, not a pass at the end. | `enterprise.css`, responsive rules, WCAG work |
| **Atlassian / Linear** | Interaction quality: tight but breathable spacing, quiet chrome that recedes, motion only where it explains a change, keyboard paths for frequent actions. | Token scales, focus handling, row actions |

## Rules that follow from the above

1. **Colour carries meaning, never decoration.** Chrome is neutral; colour is
   reserved for status and the primary action. This is why the app bar is a
   surface rather than a slab of brand colour.
2. **Status is icon + label + colour.** Colour alone fails WCAG 1.4.1 and
   fails anyone printing a report in greyscale.
3. **Density is a feature.** A clerk marking 400 students wants rows, not
   cards. Row height is 40px; padding is 8/12, not 24/32.
4. **Borders before shadows.** Hairlines separate; shadows are reserved for
   things that genuinely float (dialogs, menus).
5. **Every list states its own state.** Loading is a shape-matched skeleton,
   empty says why and what to do next, error is a sentence plus Retry — never
   a raw exception.
6. **Filters are visible.** If a list is filtered, the filter is on screen and
   removable. A count that silently excludes rows is a support ticket.
7. **Destructive actions state their consequence.** `AkConfirm.AskAsync` takes
   an action and a consequence; "Are you sure?" is not a question anyone can
   answer.
8. **No page-local styling.** If a screen needs something new it becomes a
   shared primitive in `enterprise.css`, or it is not needed.

## The quality gate

Before a screen is considered done:

- Would it look credible inside Dynamics 365, ServiceNow or Salesforce?
- Is the primary action obvious, and are destructive ones protected?
- Are loading, empty and error states all implemented?
- Does it hold up at 1440 / 1280 / 1024 / tablet / mobile?
- Keyboard reachable, visible focus, contrast measured at 4.5:1 or better?
- Does it reuse the existing pattern rather than inventing one?

Contrast is **measured**, not eyeballed. Three real defects were caught this
way during the foundation work, including a brand colour rendering at 1.44:1
on dark surfaces.

## Layers

- `wwwroot/css/design-tokens.css` — tokens only. No component styling.
- `wwwroot/css/enterprise.css` — shared component classes and MudBlazor
  retuning. All reusable styling lives here.
- `Theme/AksharaTheme.cs` — the MudBlazor mirror of the tokens. Mud renders
  colours into inline styles that CSS variables never reach, so both layers
  must agree; change one and you must change the other.
- `wwwroot/css/app.css` — boot and framework chrome only. It loads last, so
  anything added here silently outranks the design system.
