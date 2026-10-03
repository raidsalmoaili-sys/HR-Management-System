# employee-sheet-import

**Status:** in-progress (built 2026-09-27 + owner decisions same day — waiting for first real import)
**Owner:** Raid
**Created:** 2026-09-27

## Goal

Import employees straight from the company's own employee sheet (the 42-column "قاعدة بيانات الموظفين"
export, e.g. `Employees/تقرير موظفين31-08-2026/جميع الموظفين من التامينات/مطابقة_التأمينات_حسب_الفرع.xlsx`)
without first copying the data into the system template, and fix the known data problems on the way in.

## Current behavior

Only the system template import existed ([employee-bulk-import](employee-bulk-import.md)). It needs a
`BranchSlug`, an email and a password per row, so the real sheet (no emails, company/branch as names) could
not be imported. The one-off CLI importer ([employee-legacy-import](employee-legacy-import.md)) was rolled back.

## Desired behavior

1. On `/Employees/BulkImport` a new card "استيراد كشف الموظفين الجاهز (بدون قالب)": pick the .xlsx and choose
   what to do with employees that already exist (same رقم الموظف): **skip** (default) or **update from the sheet**.
2. "معاينة قبل الاستيراد" runs a full dry run — nothing is saved — and shows: new / updated / skipped / rejected
   counts, Saudi vs non-Saudi, the companies/branches/sponsors that will be auto-created, the automatic
   corrections, rejected rows with the reason, and warnings grouped by type. All notes can be downloaded as Excel.
3. "تأكيد الاستيراد" saves everything in ONE transaction (all or nothing) and shows the same page as a result.

## Out of scope

- Creating login accounts (imported employees get `UserId = null`; add a login later from the employee page if needed).
- Gender, religion and birth date — the `Employee` model has no fields for them yet (would need a migration).
- Deleting/terminating employees that are missing from the sheet.

## Design decisions

- **Columns are found by header name, not position** (row with both "رقم الموظف" and "اسم الموظف", first 15 rows
  of any sheet). Headers are normalized (spaces, hamza forms, ة/ه, ى/ي, tatweel) and have aliases, so small
  spelling differences between exports still work. Required columns: رقم الموظف، اسم الموظف، جهة العمل.
- **Company = جهة العمل, Branch = الاداره, Sponsor = الكفيل** — same mapping the owner confirmed for the legacy import.
  Missing ones are auto-created; sponsors are per-company (schema rule). Empty branch → "فرع غير محدد" (warning).
- **Name matching ignores spaces/hamza/ta-marbuta** so "شركة عبد المحسن…" and "شركة عبدالمحسن…" are one company.
  A trailing number on a *company* name is treated as a typo ("…التجارية2"); on a *branch* it's kept (real branches
  like "مركز فيو كلينيك لطب الاسنان2").
- **ID type comes from the number**: 1xxxxxxxxx → `NationalId`, anything else → `IqamaNumber`. A mismatch with the
  nationality (e.g. سعودي + 2xxxxxxxxx) is imported but flagged for review.
- **Duplicate National ID inside the file** → first row wins, the later row is rejected (same rule as the legacy import).
- **Hijri dates** (ID expiry) are converted with the Umm al-Qura calendar.
- **Auto-corrections**: nationality spellings (فلبينية→فلبيني، مـصري/مصرية/مصر→مصري …), GOSI occupations with the
  spaces stripped (مشغلالعاب→مشغل ألعاب …), bank names unified to the system list (سامبا→الأهلي، الهولندي→ساب …),
  IBAN upper-cased/spaces removed, mobile numbers to 05xxxxxxxx.
- **Kept but flagged**: IBAN that is still not `SA`+22 digits, ID not 10 digits. **Dropped + flagged**: unusable phone/email.
- **Update mode only overwrites with non-empty sheet values** — it never blanks out data typed in the system.
- **Allowances**: "بدل السكن" → housing, "بدل نقل" → transport, "البدلات" + "بدلات أخرى" → other allowance
  (see open question).
- Nationality list (`Lookups.Nationalities`) extended with 24 countries so fewer people end up as "أخرى".
- File kept in a temp folder between preview and commit (random token, deleted after commit, >1 day cleaned).

## Data model changes

None (no migration). `Lookups.Nationalities` gained entries (code-only list).

## Files to add or change

- `Services/Import/EmployeeSheetImporter.cs` — new (all parsing/matching/correction rules)
- `Controllers/EmployeesController.cs` — `ImportSheetPreview`, `ImportSheetCommit`, `ImportSheetIssues`
- `Views/Employees/ImportSheetPreview.cshtml` — new (preview + result)
- `Views/Employees/BulkImport.cshtml` — new upload card
- `Models/Hr/Lookups.cs` — more nationalities

## Implementation steps

1. Importer service ✔
2. Controller actions ✔
3. Views ✔
4. Owner runs preview on the 31-08-2026 sheet and confirms ☐

## Open questions

- "البدلات" is a big number (e.g. basic 1300, البدلات 2270, housing 430, transport 330). Is it the *total*
  of all allowances, or an extra allowance on top? Currently stored as "other allowance" on top of housing/transport.
  If it's the total, other = البدلات − housing − transport.
- 27 employees share the mobile 0501830446 (looks like an office number) — keep or clear?

---

## Changes

### 2026-09-27 — First version

Built as described above. Expected result for the 31-08-2026 sheet (499 rows): 4 rows rejected (duplicate
National ID), 13 companies after merging the "…التجارية2" typo, ~87 without ID, 32 without branch, ~18 IBANs to fix.

### 2026-09-27 — Owner decisions: official companies, duplicates, dashboard alerts, manual fix

- **Official companies**: the group's company names live in `Services/Import/CompanyNames.cs`
  (in the demo: شركة الأفق للتجارة، شركة الأفق للخدمات الطبية، مؤسسة الواحة للمقاولات). Every sheet
  spelling/sub-entity of جهة العمل and الكفيل is folded into one of them by fragment rules.
- **Duplicates** (owner: "keep the complete one, delete the other"): same ID/Iqama, or same name when one of the
  rows has no ID, = same person. The row with the most filled cells wins; its empty cells are filled from the
  dropped row (e.g. one row has the branch, the other the ID). Same name + different IDs = different people.
  Against the database: a row that matches an existing employee by code, ID, or (no-ID) name is never added
  again — it's skipped or updated per the chosen mode. The 31-08-2026 sheet: 10 rows merged → 489 employees.
- **Nationality** is stored in one (masculine) form regardless of the person's gender (owner).
- **ID expiry is stored Gregorian only**; Hijri values in the sheet are converted (owner).
- **Dashboard (`Entry/Launcher`)** — new widget "بيانات موظفين تحتاج إكمال": every active employee with no
  ID/Iqama, no real branch ("فرع غير محدد"), or nationality ≠ ID type (سعودي + إقامة / غير سعودي + هوية).
  Filter chips per type; each row opens `Employees/Detail/{id}`. Computed live, so it disappears once fixed.
- **Employee page** — yellow "بيانات تحتاج إكمال" banner with the same checks, and a new
  "تعديل البيانات الأساسية" modal (`EmployeesController.UpdateBasics`): nationality, National ID / Iqama
  (+ Gregorian expiry), passport, phone, email, job title, basic salary, bank, IBAN. Validates: National ID =
  10 digits starting 1, Iqama = 10 digits starting 2, IBAN = SA + 22 digits, no ID/email used by another employee.
  Branch is set with the existing "نقل الموظف" button.

### 2026-09-27 — Attach to the companies/branches the owner already created

The owner had already created the companies in the system with their own names/divisions
(e.g. different spellings of the same company name).
"( إدارة الملاهي)" / "(إدارة الأقمشة)" / "( إدارة العطور)"). The import now targets those records instead of
creating new ones:
- A sheet company is matched to a system company of the same official group (`CompanyNames.Canonicalize`).
  If the group has several companies in the system, `CompanyNames.Division` picks one by keyword:
  ملاهي/رمروما/ترفيه → الملاهي، اقمشة → الأقمشة، وكالات → الملاهي (owner), عطور/تجميل/التجارية → العطور ("التجارية" is an assumption).
- Branches are matched ignoring spaces, brackets, dashes and a leading "فرع". If the branch isn't under the matched
  company but exists exactly once under another company of the same group, that branch is used and the employee
  follows it to its company.
- Sponsors take the name of the matching system company.
- A new company/branch is only created when nothing matches — the preview lists them before confirming.
