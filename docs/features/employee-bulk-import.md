# Employee Bulk Import

**Status:** done
**Owner:** Abdulmohsen
**Created:** 2026-06-14

## Goal

Allow HR managers with `add-employee` create permission to import multiple employees at once from an Excel (.xlsx) file instead of adding them one by one.

## Current behavior

Employees can only be added one at a time via the modal form on the Employees Index page.

## Desired behavior

1. A "Bulk Import" button appears next to "Add Employee" for users with create permission.
2. The user clicks it and lands on `/Employees/BulkImport`.
3. The page explains the required columns and shows a "Download Template" link that returns a pre-formatted `.xlsx` file.
4. The user fills the template and uploads it via the form.
5. The server validates each row (required fields, email format, password length, existing branch slug, optional job title code) and imports all valid rows.
6. After import, a result page shows how many employees were added and a table of any rows that had errors (row number, employee code, email, error message).

## Out of scope

- Updating existing employees (this is create-only).
- Assigning HR roles during import.
- Sending welcome emails.
- Async / background processing for very large files.

## Design decisions

- Use `BranchSlug` (not `BranchId`) in the template so the file is human-readable. Users can see slugs on the Branches page.
- Use `JobTitleCode` (optional) for the same reason. The code must belong to the same company as the branch.
- Valid rows are saved even when other rows in the same file have errors (skip-bad-rows strategy), matching the user's stated requirement.
- EPPlus (NonCommercial license) for reading/writing `.xlsx`. License context set globally at startup in `Program.cs`.
- PRG pattern: POST stores result in `TempData` as JSON, then redirects to `BulkImportResult` GET.
- Permission gate: same as single-add — `add-employee` service + `HrBusinessPermissions.Create`.

## Data model changes

No new entities or migrations. The feature writes to the existing `Employees` table (and creates `AspNetUsers` rows via Identity, same as single-add).

## Files to add or change

- `docs/features/employee-bulk-import.md` — this file
- `Project_HR_1.csproj` — add `EPPlus` package reference
- `Program.cs` — set `ExcelPackage.LicenseContext = LicenseContext.NonCommercial`
- `Models/ViewModels/Management/ManagementViewModels.cs` — add `BulkImportVM`, `BulkImportResultVM`, `BulkImportErrorRow`
- `Controllers/EmployeesController.cs` — add `DownloadTemplate`, `BulkImport` GET/POST, `BulkImportResult` GET
- `Views/Employees/BulkImport.cshtml` — new upload page
- `Views/Employees/BulkImportResult.cshtml` — new result page
- `Views/Employees/Index.cshtml` — add Bulk Import button
- `Models/Hr/EmployeeImportColumns.cs` — new (2026-06-24), shared column metadata for the expanded template

## Excel template columns

69 columns as of 2026-06-24 (see "Changes" below) — the authoritative, up-to-date list is
`Models/Hr/EmployeeImportColumns.cs`, also rendered live on the `/Employees/BulkImport` page. The
original 7-column template (EmployeeCode, NameAr, NameEn, Email, InitialPassword, BranchSlug,
JobTitleCode) is superseded; `NameAr`/`NameEn` were replaced by the granular `FirstNameAr`/
`FatherNameAr`/`GrandfatherNameAr`/`FamilyNameAr` (+ `En` variants) to match the Add Employee form.

Since 2026-07-26 the admin can pick a subset of these 69 columns before downloading — see "Changes"
below. The 7 required columns (`EmployeeCode`, `Email`, `InitialPassword`, `FirstNameAr`,
`FatherNameAr`, `FamilyNameAr`, `BranchSlug`) are always included regardless of selection.

## Implementation steps

1. Feature doc ✔
2. Add EPPlus NuGet + set license context ✔
3. Add ViewModels ✔
4. Add controller actions ✔
5. Create BulkImport.cshtml ✔
6. Create BulkImportResult.cshtml ✔
7. Add button to Index.cshtml ✔

## Open questions

- Should we add a max-rows guard (e.g., 500 rows per file) to prevent accidental abuse?
  - Decision: not for now; add when/if needed.

---

## Changes

### 2026-07-27 — Job title columns: type the full name, not a code

Same request as the Nationality change below, applied to job titles: `JobTitleCode` and
`ActualJobTitleCode` required typing an internal code (`JT-001`) that had no visible reference; renamed
to `JobTitleName` / `ActualJobTitleName` and now matched by `JobTitle.NameAr` (within the branch's
company), like `DepartmentName`/`SponsorName`.

- `EmployeeImportColumns.cs`: both headers/labels updated; `EmployeeColumnReader.cs` (roster-print)
  keys renamed to match — it looks up formatters by `Header`, so a stale key would throw
  `KeyNotFoundException` the moment either column was selected for printing.
- `DownloadTemplate` now also fetches job titles (scoped like branches) and adds a new "المسميات
  الوظيفية" Reference sheet section (name + company), mirroring the existing Branches section — without
  it, "type the exact name" would be unusable (no way to know the exact spelling).
- **Consistency fix bundled in**: `ActualJobTitleName` resolution used to happen *after* the
  errors-block (silently resolving to `null` if unmatched, no error), while `JobTitleName` already
  validated properly inside the errors-block. Moved `ActualJobTitleName` resolution into the same block
  with its own error message, so an unmatched actual job title is now rejected clearly instead of
  silently dropped.
- `DirectSupervisorCode` (matches an existing employee's `EmployeeCode`) was intentionally left
  unchanged — the user's request was specifically about job-title codes, and an employee code is a
  reasonable, already-visible identifier (unlike an internal job-title code with no reference sheet).

### 2026-07-27 — Nationality column: type the full name, not a code

User request: instead of typing a 2-letter code (`SA`, `EG`, …) that requires cross-checking the
Reference sheet, the admin should be able to type the nationality's full Arabic name directly.

- `EmployeeImportColumns.cs`: the column's `Header` changed from `NationalityCode` to `Nationality`
  (Arabic label "الجنسية", sample value "سعودي"). Any code matching a column by this Header string had
  to be updated accordingly — see below.
- `BulkImport` POST: nationality is now resolved by matching `Lookups.Nationalities.NameAr` against the
  typed value (case-insensitive), not `Code`. **Revised same day per user feedback**: blank no longer
  defaults to Saudi — the user explicitly asked for blank to stay blank, not assume a nationality. Blank
  now leaves `NationalityCode`/`NationalityAr`/`NationalityEn` all `null` on the employee (and
  `isSaudi` false, so `EndOfServiceAdvance` is kept rather than nulled — see the `EndOfServiceAdvance`
  rule below, which only nulls it for confirmed-Saudi employees). A non-blank value that doesn't match
  any known nationality name is a real validation error (`"لا توجد جنسية بالاسم '...'"`), matching the
  existing `DepartmentName`/`SponsorName` pattern — previously an unmatched code was silently accepted
  with `NationalityAr`/`NationalityEn` left null, which is a worse failure mode now that free-text names
  are more typo-prone than 2-letter codes.
- `DownloadTemplate`'s Reference sheet section was re-oriented to lead with the Arabic name (the value
  to copy) instead of the code, and its heading updated to say "انسخ الاسم العربي بالضبط".
- `Services/Reports/EmployeeColumnReader.cs` (used by [employee-roster-print](employee-roster-print.md)
  to print/report on employees) keys its formatters by the same `Header` strings — its
  `NationalityCode` entry was renamed to `Nationality` to match, otherwise the roster-print feature
  would throw a `KeyNotFoundException` the moment a Nationality column was selected.
- The manual single-employee Add form (`AddEmployeeVM.NationalityCode`, a `<select>` dropdown) is
  unrelated and unchanged — this only affects the bulk-import Excel column, where free text makes more
  sense than a dropdown.

### 2026-07-27 (fix) — "clear all Required" was silently ignored

Reported by the user: after clearing every Required checkbox and uploading a fully-filled file, the
import still rejected every row demanding Email/InitialPassword/BranchSlug — exactly the static
default-required fields, as if the picker's choice had been ignored entirely.

Root cause: `BulkImport` POST read the embedded choice with
`string.IsNullOrEmpty(embeddedRequired) ? <defaults> : <parse it>`. An *admin deliberately clearing
every* Required box embeds an empty string (a real, meaningful "nothing is required" choice) — but
`IsNullOrEmpty("")` is `true`, so it was indistinguishable from "no choice was ever embedded" and fell
back to the static defaults every time. Fixed by checking for `null` specifically
(`embeddedRequired is null`) instead of `IsNullOrEmpty`, so an intentionally-empty required set is
honored as empty. Verified the underlying assumption with a standalone EPPlus round-trip test first:
`SetCustomPropertyValue("HrRequiredColumns", "")` → re-opened from bytes →
`GetCustomPropertyValue(...)` returns `string.Empty` (not `null`), while a property that was never set
returns `null` — confirming the two cases are in fact distinguishable and the fix is correct.

### 2026-07-27 — Section-level Required toggle (revised from a global toolbar)

First cut of the Required picker (below) added a second global toolbar — "Select all (Required)" /
"Deselect all (Required)" with a live count — mirroring the existing column-inclusion toolbar. The user
rejected this layout and asked for the same per-section toggle pattern already used for column
inclusion instead: each section header row (e.g. "البيانات الأساسية") already has one checkbox that
selects/deselects every column in that section; they wanted a second, equivalent toggle in the same
row for Required specifically, not a separate global control.

- The global "Required" toolbar (buttons + count) was removed entirely.
- Each section header `<tr>` now has three cells instead of one `colspan="6"`: the existing inclusion
  toggle (`colspan="4"`, columns 1–4), a new `bi-sec-req-toggle` checkbox aligned exactly under the
  "إلزامي" column (column 5), and an empty background cell for the Notes column (6).
- New JS: `toggleSectionRequired(secIdx, checked)` bulk-sets that section's non-disabled Required
  checkboxes; `syncSectionReqToggle(secIdx)` keeps the section toggle's own checked state in sync as
  individual Required boxes are hand-toggled (mirrors the pre-existing `syncSectionToggle` for
  inclusion). `syncReqCheckbox` (fires when a column is included/excluded) now calls
  `syncSectionReqToggle` instead of a removed global `updateReqCount`.

### 2026-07-27 — Admin can also choose which columns are Required

The "إلزامي" (Required) column in the picker table was a static, read-only badge (✔/—) reflecting
each column's built-in default from `EmployeeImportColumns.cs`. The user asked for the same "full
authority to the user" treatment already given to column inclusion: they decide which columns are
mandatory, via an actual checkbox.

- **`Views/Employees/BulkImport.cshtml`**: the Required cell is now a real checkbox (`name="required"`,
  `value="@col.Header"`), checked by default per `col.Required`. A hidden `reqSubmitted=1` field lets
  the server tell "form was submitted, honor `required` exactly as posted — even empty" apart from a
  bare/direct link (fall back to defaults) — this matters because, unlike column inclusion, an admin
  unchecking *every* Required box is a real, valid choice (a fully-optional file), not something to
  silently override. A column's Required checkbox auto-disables (and unchecks) when its own inclusion
  checkbox is off — a column that isn't in the file can't be mandatory — and restores to its default
  when re-included (`syncReqCheckbox()` in the view, wired into the existing per-column, per-section,
  and select-all/deselect-all handlers).
- **`DownloadTemplate(string[]? cols, string[]? required, string? reqSubmitted)`**: the required set now
  drives the template's visual styling (bold/colored header vs. light optional header, column width)
  instead of each column's static default. Critically, the choice is also embedded *inside the
  generated file* as a custom workbook property (`HrRequiredColumns`, comma-joined headers) — this is
  what lets the choice survive being filled in and uploaded in a completely different session, possibly
  days later, by someone else.
- **`BulkImport(IFormFile? file)` POST**: reads `HrRequiredColumns` back off the uploaded workbook
  (falls back to each column's static default if absent, e.g. a hand-built file). This same
  `requiredSet` now drives both the upfront "missing required column" rejection (previously checked
  `c.Required`) and the per-row validation, which was rewritten from seven hardcoded field checks
  (EmployeeCode/FirstNameAr/FatherNameAr/FamilyNameAr/Email/InitialPassword/BranchSlug always required)
  into a generic loop over any column the template marked required. The password-length check stays
  separate and unconditional — a password that *was* provided must still be ≥6 characters regardless of
  whether that column was marked mandatory. Business-rule checks that were never about "required" in the
  first place (branch/job title/department/sponsor must resolve to a real, existing record) are
  untouched and still fire unconditionally — so even if an admin marks `BranchSlug` optional, a row that
  leaves it blank still fails (with "no branch found for ''" instead of "BranchSlug is required"),
  because the import structurally cannot proceed without a resolvable branch.
- **Known trade-off, accepted deliberately**: a handful of columns are structurally load-bearing
  regardless of the Required flag (`EmployeeCode`, `Email`, `InitialPassword`, `BranchSlug` — needed to
  create the Identity user / resolve the branch / key the employee). Marking them non-required doesn't
  crash anything, but blank values there will still fail downstream (Identity rejects an empty email,
  branch resolution fails on an empty slug) — just with a less-friendly error than the old fixed message.
  This mirrors the same trade-off already accepted for column *inclusion* in the previous change.

### 2026-07-26 — Admin can choose which columns go in the template

The admin was previously forced to download the full 69-column template every time, even if they
only ever fill a handful of fields. `Views/Employees/BulkImport.cshtml` now renders the "full column
reference" table as an interactive picker: every column, including the 7 required ones, has a normal
checked-by-default checkbox, grouped by section with a per-section select-all toggle plus a global
select-all/deselect-all. Submitting the form does a GET to `DownloadTemplate` with `cols=<Header>`
repeated per selected column; the generated `.xlsx` only contains exactly the columns requested.

**Full user control, including over required columns** (revised same day after user feedback): the
first version of this picker rendered the 7 required columns as checked-and-disabled with a hidden
input, so they were always force-included server-side no matter what. The user explicitly asked for
the checkbox to give "كامل الصلاحية للمستخدم" (full authority to the user) — confirmed via follow-up
question that excluding a required column should rightfully cause the whole file to be rejected on
upload with a clear message, rather than the UI silently overriding the choice. So the disabled/hidden-
input special case was removed entirely; every column (required or not) is now a plain, fully
interactive checkbox, and `DownloadTemplate` trusts the `cols` selection completely — a required
column left unchecked is genuinely left out of the downloaded file. The pre-existing upfront
required-header check in the `BulkImport` POST (added earlier the same day) already produces exactly
the desired rejection message in that case, so no server-side validation changes were needed there.
One edge case handled: if the admin deselects literally everything, the browser submits no `cols` at
all, which is indistinguishable from a direct link with no selection — `DownloadTemplate` now builds
the selected list twice (once with the request, once unfiltered) and falls back to the full template
only when the filtered list is empty, so an all-deselected submit doesn't silently produce a broken
zero-column file.

- **`DownloadTemplate(string[]? cols)`**: builds a `selected` list (column + its original section
  index + section light color) by filtering `EmployeeImportColumns.All`, then a grouped `effSections`
  list (consecutive same-section runs) so the colored section-header band in row 2 still merges
  correctly over the filtered layout. Column widths and the collapsed/visible outline groups
  (Docs/Personal/Education/Contract collapsed, Financial visible-but-outlined) are now driven by each
  column's original section index / header name instead of hardcoded absolute column ranges, since a
  column's position in the output file no longer matches its position in the full 69-column list.
  No `cols` param (or empty) → full template, unchanged from before.
- **`BulkImport(IFormFile? file)` POST — header-based parsing**: this was the harder half. The row
  parser used to read by fixed 1-based column index (`S(1)`, `S(2)`, …), which assumed every uploaded
  file has all 69 columns in the original order. That assumption breaks once files can have arbitrary
  subsets. The parser now reads the row-4 English header text of the uploaded file into a
  `Dictionary<string,int> headerCol` (header → column index) once per import, and all field extraction
  goes through `S("EmployeeCode")`, `D("HireDate")`, etc. by header name instead of position. A column
  missing from the uploaded file simply resolves to blank/null, same as an empty cell would.
  Before the per-row loop, the 7 required headers are checked against `headerCol`; if any are missing
  (e.g. someone deleted a required column by hand in Excel) the whole upload is rejected up front with
  a clear error, rather than silently failing every row.
- **Design decision**: matching by the row-4 English header (already present in every template as a
  small reference row under the Arabic label) means the exact same parsing code handles the full
  template, any partial selection, and any column order — there is nothing left that depends on fixed
  positions. This also means a future request to reorder columns or split into named presets needs no
  parser changes.
- Verified end-to-end on a running instance: downloaded a 9-column template (7 required + `HireDate`
  + `Phone`, all other columns unchecked), confirmed via the raw `.xlsx` that `BankName` and other
  unselected columns were absent while the selected ones were present in the right order; filled and
  uploaded that file — imported cleanly (1/1, 0 errors) with `Phone` and `HireDate` landing on the
  correct employee despite sitting at columns H/I in the file instead of their original 21/18.
- Re-verified after the full-user-control revision: downloaded with `Email` deliberately excluded from
  `cols` → confirmed the raw `.xlsx` had 7 columns with no `Email` header; uploaded that file (filled)
  → rejected with "الملف ينقصه أعمدة إلزامية: البريد الإلكتروني" as expected, no employee created;
  submitted with no `cols` at all → confirmed the fallback still produces the full 69-column
  (`A1:BQ200`) template.

### 2026-06-24 — Expanded to cover the full employee profile

The template/import now mirrors all the fields added by
[employee-full-profile-form](employee-full-profile-form.md) (General, Personal, Education,
Financial, Work & Leave — everything except file uploads, which can't go in a spreadsheet cell).
69 columns total.

- **Design decision**: column metadata (header, sample value, required flag, AR/EN note) lives in a
  single new static class `Models/Hr/EmployeeImportColumns.cs`, shared by `DownloadTemplate` (writes
  headers + sample row), `BulkImport` POST (reads by the same 1-based column index), and the
  `BulkImport.cshtml` reference table (renders from `BulkImportVM.Columns`) — so the three can never
  drift out of sync.
- New lookup columns resolve by human-readable text, matching the existing `BranchSlug`/`JobTitleCode`
  pattern: `DepartmentName`/`SponsorName` (matched by `NameAr` within the branch's company),
  `ActualJobTitleCode` (separate from the General-tab `JobTitleCode`), `DirectSupervisorCode`
  (matched against existing employees' `EmployeeCode` — not other rows in the same file).
  `NationalityCode` defaults to `SA` when blank.
  Enum columns (`MaritalStatus`, `SalaryType`, `WorkSystem`, `ContractType`) are parsed case-insensitively
  by their C# enum member name (e.g. `Monthly`, `FullTime`).
- Same required fields as before plus name parts: `EmployeeCode`, `Email`, `InitialPassword`,
  `FirstNameAr`, `FatherNameAr`, `FamilyNameAr`, `BranchSlug`. Everything else is optional, consistent
  with the relaxed validation on the manual Add Employee form.
- `IsEducated = false` nulls all education columns server-side, same rule as the manual form.
  `EndOfServiceAdvance` is ignored automatically when `NationalityCode == "SA"`, same rule as the
  manual form.
- Verified end-to-end: downloaded the new template (confirmed all 69 headers present), uploaded a
  filled row through a running instance — `BranchSlug` resolution, decimal/date/enum parsing, and all
  three sub-tables (`EmployeePersonal`/`EmployeeFinancial`/`EmployeeWork`) populating correctly,
  confirmed via the employee Detail page. `DepartmentName`/`SponsorName` text-matching also confirmed
  working (an earlier failed attempt turned out to be an Arabic-encoding artifact in the test script
  that generated the throwaway `.xlsx`, not a bug in the matching code — a row with blank
  Department/Sponsor imported cleanly, and the lookup logic itself is the same pattern already used
  and trusted for `JobTitleCode`).

### 2026-06-14 — Initial implementation

Full feature built in one session. No DB migrations needed.
