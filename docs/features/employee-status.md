# employee-status

**Status:** done
**Owner:** raidsalmoaili-sys
**Created:** 2026-09-30

## Goal

Let HR mark each employee as **على رأس العمل / مجاز / مستقيل / هارب** (Active / On leave / Resigned / Absconded),
see it everywhere the employee appears, and print/filter the roster by it.

## Current behavior

- Each employee has a status shown as a coloured pill (Arabic label) on the Employees list and on the profile.
- It can be changed from the profile ("تغيير الحالة") or straight from the list by clicking the pill.
- Resigned / Absconded store the chosen date as the employee's **last working day** (`Employee.LastWorkDate`);
  that date can be corrected later with "تعديل آخر يوم" on the profile. Going back to Active clears it.
- Every change is written to the Activity Log (old → new, date, optional note).
- Print roster: status filter (4 statuses) + a printable "حالة الموظف" column (in the Basic preset).

## Desired behavior

1. Employees list — status column shows the pill; users with `add-employee` / Edit click it to open the status dialog.
2. Employee profile — a "حالة الموظف" card: pill, last working day (if any), buttons "تغيير الحالة" and
   (for resigned/absconded) "تعديل آخر يوم".
3. Status dialog — 4 options, date field shown only for resigned/absconded (empty = today), optional note.
4. Print roster — filter by any of the 4 statuses; "حالة الموظف" column available in all outputs (preview, PDF, Excel).

## Out of scope

- No status history table (the Activity Log is the history).
- No automatic status changes (e.g. leave requests ending) — manual only for now.
- Leave (مجاز) does not store start/end dates yet.
- Headcount widgets still count only `active` (unchanged).

## Design decisions

- **No migration**: `Employee.Status` (string, max 32) already existed; values are `active`, `leave`, `resigned`, `absconded`.
  Catalog + labels/colours/icons live in `Models/Hr/EmployeeStatuses.cs` (single source of truth).
- Unknown/legacy status values still render (grey, raw text) instead of throwing.
- The date for resigned/absconded reuses the existing `LastWorkDate` column (already imported/printed as "تاريخ آخر يوم").
- Editing the last day = submitting the same status with a new date (one endpoint, `ChangeStatus`).
- Permission: same as editing basic data — `add-employee` + Edit, within the active scope.
- Pill colours are defined in the partial's own `<style>`, so they work before the Tailwind CSS is rebuilt.
- The status column for printing is report-only (`EmployeeColumnReader.ExtraColumns`), so the Excel **import**
  template is unchanged.

## Data model changes

None.

## Files to add or change

- `Models/Hr/EmployeeStatuses.cs` — new (codes, AR/EN labels, badge colour, icon, helpers).
- `Controllers/EmployeesController.cs` — `ChangeStatus` POST; `CanEditStatus` on the list VM; roster status filter/summary use the catalog; extra printable columns.
- `Models/ViewModels/Management/ManagementViewModels.cs` — `EmployeesListVM.CanEditStatus`.
- `Views/Employees/_StatusBadge.cshtml`, `Views/Employees/_StatusModal.cshtml` — new shared partials.
- `Views/Employees/Index.cshtml` — pill + dialog.
- `Views/Employees/Detail.cshtml` — status card + dialog.
- `Services/Reports/EmployeeColumnReader.cs` — `ExtraColumns` + `Status` reader.
- `Views/Employees/PrintSetup.cshtml` — 4 status filter buttons, status column in Basic group/preset.

## Implementation steps

1. ✅ Status catalog.
2. ✅ `ChangeStatus` endpoint (+ last-day edit, activity log).
3. ✅ List pill + quick change.
4. ✅ Profile card + last-day edit.
5. ✅ Print filter + printable column — see [employee-roster-print](employee-roster-print.md).

## Open questions

- Should leave (مجاز) record from/to dates? Not now.

---

## Changes

### 2026-09-30 — Initial version

Planned with the user (change from profile **and** list), and during implementation the user asked that the
last working day be editable manually — added "تعديل آخر يوم". Verified locally: change from list, change from
profile, last-day edit, print filter by status, `dotnet build` clean.
