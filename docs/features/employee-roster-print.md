# employee-roster-print

**Status:** done
**Owner:** raidsalmoaili-sys
**Created:** 2026-07-27

## Goal

Let an HR user print (or just review on-screen) a customizable roster/statement of employees — picking which companies/branches to include and which employee fields (columns) show up in the printed sheet.

## Current behavior

> **Updated 2026-09-27 (redesign):** the setup page is now a 4-step page (scope → filters → columns → live preview) with a sticky summary/export panel, and the roster can be printed, exported to PDF **or Excel**, and filtered. See the last entry under Changes. The paragraph below is the original (pre-feature) state, kept as history.

Does not exist yet. `Views/Employees/Index.cshtml` only shows a fixed employee table (name, company, branch, job title, status) with no export/print/report options. The only related feature is [employee-bulk-import](employee-bulk-import.md), which goes the other direction (Excel → database), and already defines a reusable, sectioned list of all employee fields in `Models/Hr/EmployeeImportColumns.cs`.

## Desired behavior

1. **Entry point** — a "طباعة كشف الموظفين" / "Print Employee Roster" button on the Employees Index page (next to "Bulk Import" / "Add Employee"), visible to anyone who can view the Employees list (same `employees` / `View` permission as the Index page itself — no new permission).
2. **Setup page** (`GET /Employees/PrintSetup`):
   - **Scope section**
     - Multi-select checkboxes for **Companies**, restricted to whatever the current `IScopeContext` already exposes (if the user's active scope is a single company/branch, that's all they see here — the report never widens beyond the active HR scope, only narrows it further).
     - Multi-select checkboxes for **Branches**, belonging to the selectable companies. Checking/unchecking a company checkbox shows/hides its branches client-side (no page reload).
     - Leaving both empty = include everything the user's current scope allows.
     - A "quick pick" pair of cascading `<select>` dropdowns (Company → Branch) as a shortcut: picking a value there checks the matching checkbox(es) below, so the checkbox list stays the single source of truth submitted to the server.
   - **Columns section**
     - Checkbox grid built from `EmployeeImportColumns.All`, grouped into the same 7 sections already defined for bulk import (Basic Info, Job Classification, Identity Documents, Personal, Education, Financial, Contract).
     - Per-section "select all / none" plus one global "select all / none". All columns are checked by default.
   - **Submit** — `<form method="post" asp-action="PrintPreview">` posting the chosen `CompanyIds[]`, `BranchIds[]`, and `Columns[]` (column `Header` keys).
3. **Preview page** (`POST /Employees/PrintPreview`, view returned directly — no redirect, same pattern as the existing `PreviewContract` action):
   - Re-validates the `employees`/`View` permission, applies `IScopeContext.Filter` first, then narrows further by the posted company/branch ids (if any).
   - Loads employees with the includes needed for the selected columns (Personal/Financial/Work/Company/Branch/JobTitle/Department/Sponsor as needed) and renders one **on-screen, editable-feeling review table**: selected columns only, one row per employee. This is the "راجع وعدّل قبل الطباعة" step — nothing is generated yet.
   - Shows a small summary line above the table: which companies/branches were selected (or "الكل" if none), how many employees matched, and the generation timestamp.
   - Bottom bar with two actions, both inside a `<form>` that carries the same `CompanyIds[]` / `BranchIds[]` / `Columns[]` forward as hidden inputs:
     - **"تعديل" / Edit** → `history.back()` (plain button, no submit), returning to the setup page with the browser's own form-state restore.
     - **"تأكيد" / Confirm** → `<button type="submit" formaction="PrintPdf">` — posts the same selection to `PrintPdf`, which re-runs the identical scope+column query (shared helper, so the PDF always matches what was just reviewed) and returns a generated PDF file. The browser opens/downloads the PDF; printing happens from there (the PDF viewer's own print command), not from this app.

## Out of scope

- No in-place editing of employee data from the review table — "تعديل" only means going back to change scope/columns, not editing field values.
- No Excel/CSV export of the roster (bulk import already covers the Excel-in direction).
- No saved report presets/favorites, no scheduled/recurring reports.
- Does not bypass the active HR scope — a user scoped to one branch cannot use this report to see other branches or companies.
- No pagination/streaming for very large employee sets — relies on the browser to paginate the print job. Fine for expected data sizes; flagged below as a future concern if it ever becomes a problem.

## Design decisions

- Reuse `EmployeeImportColumns.All` / `Sections` as the single catalog of "what employee fields exist" instead of inventing a second column list — keeps bulk-import and print-report in sync when fields are added later.
- Because `EmployeeImportColumn` was built for *importing* (parsing text into an `Employee`), a new small reader is needed for the reverse direction (formatting an `Employee`'s value for a given column `Header`) — see Files below.
- The scope picker can only narrow the already-active `IScopeContext`, never widen it — consistent with every other list/report in the app and avoids a new permission check.
- `PrintPreview` renders its result view directly from the `POST` handler (no `TempData` + redirect), mirroring the existing `PreviewContract` action — simpler, and there's no persisted state to protect against double-submission here.
- "Edit" goes back via browser history instead of round-tripping the whole selection through query string or `TempData` — cheaper, and modern browsers restore checkbox state on back-navigation.
- Final output is a real PDF (not just browser print) — generated with QuestPDF, the same library already used by `ContractPdfBuilder`. A new `IRosterPdfBuilder` handles the tabular/multi-column layout (landscape, shrink-to-fit columns) instead of extending the single-record `ContractPdfBuilder`.
- `PrintPdf` re-runs the exact same scope+column query as `PrintPreview` (via a shared private helper on the controller) rather than trusting posted row data — guarantees the PDF reflects live data and can't be tampered with via hidden inputs beyond the company/branch/column selection.

## Data model changes

None — this is a read-only report over existing entities (`Employee`, `EmployeePersonal`, `EmployeeFinancial`, `EmployeeWork`, `Company`, `Branch`, `JobTitle`, `Department`, `Sponsor`).

## Files to add or change

- `Services/Reports/EmployeeColumnReader.cs` — new. Maps each `EmployeeImportColumn.Header` to a `Func<Employee, string>` that formats that field's current value for display.
- `Services/Documents/RosterPdfBuilder.cs` — new. `IRosterPdfBuilder.Build(RosterPdfData)` renders the selected columns/rows as a landscape QuestPDF table.
- `Models/ViewModels/Management/ManagementViewModels.cs` — add `PrintRosterSetupVM`, `PrintRosterRequestVM` (POST-bound: `CompanyIds`, `BranchIds`, `Columns`), `PrintRosterResultVM`.
- `Controllers/EmployeesController.cs` — add `PrintSetup()` (GET), `PrintPreview(PrintRosterRequestVM)` (POST, renders the review table), `PrintPdf(PrintRosterRequestVM)` (POST, returns the generated PDF file), and a shared private helper both POST actions call to resolve the scoped employee query.
- `Program.cs` — register `IRosterPdfBuilder` in DI (same lifetime/pattern as `IContractPdfBuilder`).
- `Views/Employees/PrintSetup.cshtml` — new.
- `Views/Employees/PrintPreview.cshtml` — new (review table + Edit/Confirm form).
- `Views/Employees/Index.cshtml` — add the "Print Employee Roster" button.

## Implementation steps

1. Add `EmployeeColumnReader` covering all 69 columns from `EmployeeImportColumns.All`.
2. Add the three view models.
3. Add `PrintSetup` GET action (builds scoped company/branch lookups + column/section metadata for the checkbox grid).
4. Add the shared "resolve scoped employees" helper (scope filter → optional company/branch narrowing → query with includes).
5. Add `PrintPreview` POST action (helper → map rows via `EmployeeColumnReader` → return the review view).
6. Add `RosterPdfBuilder` + `PrintPdf` POST action (helper → same column mapping → `IRosterPdfBuilder.Build(...)` → `File(...)`).
7. Build `PrintSetup.cshtml` (scope checkboxes + cascading quick-pick + column checkbox grid + select-all/none JS).
8. Build `PrintPreview.cshtml` (summary line + review table + Edit/Confirm form with hidden inputs carrying the selection forward).
9. Add the entry button on `Index.cshtml`.

## Open questions

- Expected roster size in practice — if a company ever has thousands of employees, the review table and PDF may need real pagination or a size warning. Not addressed in this first version.
- Should sensitive columns (National ID, Iqama, IBAN, etc.) require a permission beyond plain `employees`/`View`? Currently no — same visibility as the existing Employee Detail page.
- When many columns are selected, a single landscape PDF page may get visually cramped (small font / tight columns). First version accepts this trade-off rather than splitting into multiple stacked pages per employee.

---

## Changes

### 2026-07-27 — Initial plan

Drafted with the user: scope picker narrows but never widens the active `IScopeContext`, preview renders directly from the POST action, "Edit" uses browser back-navigation. First draft used browser print directly off the HTML table; revised same day — the user wants an HTML review/edit step first, then a "تأكيد" (Confirm) step that generates an actual PDF file (via a new QuestPDF-based `RosterPdfBuilder`), which they then print from their PDF viewer.

### 2026-07-27 — Implemented

Built as planned:
- `Services/Reports/EmployeeColumnReader.cs` — Header → formatted-value map covering all 68 printable columns (`InitialPassword` excluded; `BirthDate` reads as empty since it isn't persisted anywhere yet — pre-existing gap, not introduced here).
- `Services/Documents/RosterPdfBuilder.cs` (`IRosterPdfBuilder`) — landscape A4 QuestPDF table, registered in `Program.cs` alongside `IContractPdfBuilder`.
- `PrintRosterSetupVM` / `PrintRosterRequestVM` / `PrintRosterResultVM` added to `ManagementViewModels.cs`.
- `EmployeesController`: `PrintSetup` (GET), `PrintPreview` (POST, renders the review view directly), `PrintPdf` (POST, returns the generated file) — all gated by the same `employees`/`View` permission as the rest of the list, sharing a private `ResolveRosterAsync` helper so the review table and the PDF can never drift apart. `IHrLocalizer` and `IRosterPdfBuilder` added to the controller's constructor.
- `Views/Employees/PrintSetup.cshtml` — company/branch checkboxes (with cascading quick-pick selects) + the same sectioned column-picker grid pattern used by Bulk Import.
- `Views/Employees/PrintPreview.cshtml` — review table + "تعديل" (browser back) / "تأكيد وإنشاء PDF" (posts to `PrintPdf`) actions.
- Entry point: "طباعة كشف الموظفين" button added to `Views/Employees/Index.cshtml`'s page-head actions, next to Bulk Import/Add Employee, so it's reachable as a navigation link within the Employees section (and both new pages carry `CurrentServiceId = "employees"` so the breadcrumb still shows "الموظفون").
- Verified: `dotnet build` clean, and `PrintSetup`/`PrintPreview`/`PrintPdf` all correctly redirect unauthenticated requests to login (routes wired, no startup/DI errors). Not yet manually verified end-to-end with a logged-in user/real data.


### 2026-09-27 — Fix: PDF crashed with DocumentLayoutException when many columns were selected

`POST /Employees/PrintPdf` threw QuestPDF's "conflicting size constraints" error. Cause: every selected column
was a `RelativeColumn` on a fixed A4-landscape page, so with many columns each one was only a few points wide
and any value longer than that (IBAN, e-mail, long names) could not be laid out.
Fix in `Services/Documents/RosterPdfBuilder.cs`:
- each column gets a constant width sized to its longest value (34–170pt), plus a "#" row-number column;
- the page grows to fit all columns (min A4 landscape, same proportions) — PDF viewers scale it to the paper when printing;
- tokens longer than 26 characters get zero-width break points so they wrap instead of overflowing;
- the page is right-to-left and rows are zebra-striped.


### 2026-09-27 — Redesign: filters, live preview, Excel export, column ordering, templates

Requested by the user: a clearer, better-looking print page where they can filter, adjust, print, and export to PDF or Excel.
Designed first as a standalone mockup, approved by the user, then ported onto the existing actions.

**Setup page (`Views/Employees/PrintSetup.cshtml`) — rebuilt**
- Four numbered step cards (number turns gold once the step has a value) + a sticky side panel.
- **1 · Scope** — searchable Companies and Branches panes with tri-state "select all", live employee counts per row,
  branches grouped under their company, and removable gold chips for what is chosen. Replaces the old duplicate
  quick-pick `<select>`s + checkbox lists. Empty = whole active scope (unchanged rule).
- **2 · Filters (new)** — free-text search (name / code / national ID / iqama / mobile), status (active / leave / inactive),
  nationality (Saudi / non-Saudi / a specific one from `Lookups.Nationalities`), department, job title, sponsor,
  hire-date range, contract type, and ID/Iqama expiry (expired / within 30·60·90 days). Active filters show as removable chips.
- **3 · Columns** — collapsible groups per import section (same 7 sections), label + field key, search, presets
  (أساسي / وظيفي / الوثائق / الرواتب / الكل), and a drag-to-reorder **print order** list. The order is now respected in every output.
- **4 · Preview** — a paper-style sheet showing the first rows of the *real* data, landscape/portrait toggle, and a warning
  when there are more columns than the page fits (~9 landscape / ~5 portrait).
- **Side panel** — employees / columns / estimated pages, orientation, row numbers, show-filters-on-sheet, and the actions:
  طباعة (opens the review page in a new tab and triggers the print dialog), PDF, Excel, and "مراجعة الكشف كاملاً".
- **Templates** — built-in presets (basic, expiring iqamas, payroll, job) + user-saved templates. Saved in the browser's
  `localStorage` (key `hr.printRoster.templates.v1`), i.e. per user per browser — no DB table yet.
- Styles live in a new plain-CSS file `wwwroot/css/print-roster.css` (prefix `prs-`), loaded only by these two views, so no
  Tailwind rebuild is needed. It uses the runtime tokens `--hr-primary` / `--hr-accent`, so the Tweaks panel still applies.

**Review page (`Views/Employees/PrintPreview.cshtml`) — restyled**
- Same paper sheet (navy header row with the gold rule, zebra rows, IDs/codes left-to-right), filter line, totals.
- Sticky action bar: تعديل (back), Excel, PDF, طباعة (`window.print()`; `@media print` prints only the sheet and repeats the header row).
- Carries the full request (scope + filters + columns + options) as hidden inputs, so PDF/Excel regenerate the identical selection.

**Controller (`EmployeesController`)**
- `PrintRosterRequestVM` gained the filter fields and `Orientation`, `RowNumbers`, `ShowFilters`, `AutoPrint`.
- New `ApplyRosterFilters` (used by `ResolveRosterAsync`) — filters run in SQL after the scope filter, so they can only narrow.
- `ResolveRosterAsync` now keeps the user's column order (unknown/duplicate keys dropped; empty still falls back to all).
- New `POST PrintSummary` → JSON `{count, headers, keys, rows (first 8)}` for the live preview; same query as the outputs.
- New `POST PrintExcel` → EPPlus workbook: RTL sheet, title + scope + filters rows, navy header with gold bottom border,
  zebra rows, auto-filter, frozen header, auto-fit widths, print setup (fit to width, repeat header row). Logged to ActivityLog as `PrintExcel`.
- New `BuildFilterSummaryAsync` — human-readable filter list shown on the preview, PDF and Excel.
- `PrintSetup` now also loads departments / job titles / sponsors (scope-filtered) and per-company/branch employee counts.

**PDF (`RosterPdfBuilder`)** — `RosterPdfData` gained `FilterSummary`, `Portrait`, `RowNumbers`; header row uses the brand
navy with a gold bottom border. Arabic rendering still depends on the fonts installed on the server (unchanged).

**Out of scope changes:** the earlier "No Excel/CSV export" and "No saved report presets" lines no longer apply — Excel export
and browser-saved templates now exist. Still no server-side/shared templates and no pagination for very large rosters.

**Verified:** `dotnet build` clean; ran the app against a local Postgres with 120 demo employees and checked, logged in as the
SuperAdmin: setup page renders (AR and EN), live counts react to scope/filters/templates, Excel and PDF download, review page
renders with the carried-forward selection, no horizontal scroll at 390px width.


### 2026-09-30 — Employee status filter + column

Status filter now uses the four statuses from [employee-status](employee-status.md) (على رأس العمل / مجاز /
مستقيل / هارب) instead of active / leave / inactive, and a report-only column "حالة الموظف" was added
(`EmployeeColumnReader.ExtraColumns`, included in the Basic preset). The import template is unchanged.
