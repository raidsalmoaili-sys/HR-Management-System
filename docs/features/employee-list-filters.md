# employee-list-filters

**Status:** done
**Owner:** raidsalmoaili-sys
**Created:** 2026-09-30

## Goal

Search and filter the Employees list (hundreds of rows) instead of scrolling.

## Current behavior

`Employees/Index` has a filter bar above the table:
- **Search** box — name (AR/EN), employee code, e-mail, mobile, national ID, iqama, job title (sheet). Submits 0.6 s after typing stops, or on Enter.
- **Company**, **Branch** (only branches of the chosen company), **Nationality** (Saudi / non-Saudi) drop-downs — apply on change.
- **Status chips** — الكل / على رأس العمل / مجاز / مستقيل / هارب, each with a live count (see [employee-status](employee-status.md)).
- "مسح الفلاتر" resets everything; the card header shows "X من Y سجل" while filtering, and the empty row says nothing matched.

## Desired behavior

As above. Filters live in the URL (`?q=&status=&companyId=&branchId=&nationality=`), so refresh/back/bookmark keep them,
and changing an employee's status from the list returns to the same filtered view.

## Out of scope

- Paging (the list is still one page — fine for ~500 employees).
- Column sorting, department/job-title filters (the print roster has those).

## Design decisions

- Server-side GET filtering in `BuildListVmAsync`, after `IScopeContext.Filter` — can only narrow the user's scope, and works the same for every row count.
- Status counts ignore the status filter itself (so the chips show how many are in each status for the other filters).
- UI styles are inside the partial (`_EmployeeFilters.cshtml`) so no Tailwind rebuild is needed.

## Data model changes

None.

## Files to add or change

- `Models/ViewModels/Management/ManagementViewModels.cs` — `EmployeeListFilter`; `EmployeesListVM` gets `Filter`, `TotalCount`, `StatusCounts`, `FilterCompanies`, `FilterBranches`.
- `Controllers/EmployeesController.cs` — `Index([FromQuery] EmployeeListFilter)`, filtering in `BuildListVmAsync`.
- `Views/Employees/_EmployeeFilters.cshtml` — new.
- `Views/Employees/Index.cshtml` — renders the bar, filtered count, empty message.

## Open questions

- Add paging if the list grows past a few thousand employees.

---

## Changes

### 2026-09-30 — Initial version

Requested by the user after the status feature. Verified locally: search, company → branch cascade, status chips with counts, nationality, empty result, and status change from a filtered list keeps the filters.

## Changes 2026-09-30 — search relevance

- Searching "خالد" listed people whose *father* is خالد above people whose first name is خالد (plain alphabetical order).
- Results are now ranked: exact employee code → name starts with the term (first name) → term starts a later word (father/grandfather/family) → other matches (ID, phone, email). Alphabetical within each rank. Done in memory after the query, in `EmployeesController.BuildListVmAsync`.

## Changes 2026-09-30 — search by name position + highlight

- Ranking replaced: exact code → 1st name starts with the term → 2nd (father) → 3rd → 4th… → term inside a word → other fields. Partial words work ("خال" → خالد).
- Matches are highlighted in the name / code / email with `<mark class="hl-q">` (bold, dark text on soft gold) via the `Hl()` helper in `Index.cshtml`.
