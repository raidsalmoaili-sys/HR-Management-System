# employee-branch-transfer

**Status:** done — pending manual test in a running app + `docker compose up -d db`
**Owner:** raidsalmoaili-sys
**Created:** 2026-08-24

## Goal

Let an authorized HR user move an existing employee to a different branch — same company or a completely different company — without deleting the employee or re-entering their data.

## Current behavior

`Employee.CompanyId` / `Employee.BranchId` are set once, at creation time, in `EmployeesController.Create` (`Views/Employees/_AddEmployeeTab_General.cshtml` posts `BranchId`; `CompanyId` is derived server-side from the branch). There is no action, view, or button anywhere that changes them afterward. There is also currently no "Edit employee" flow of any kind — `Detail.cshtml` is read-only plus a Delete button.

## Desired behavior

1. **Entry point** — a "نقل الموظف" / "Transfer Employee" button on `Views/Employees/Detail.cshtml`, next to the existing Delete button. Visible only when the user has the transfer permission (see Design decisions).
2. **Modal** (`<dialog>`, same pattern as the existing "Add Employee" modal on `Index.cshtml` — `data-modal-open` / `data-modal-close`), containing:
   - A **Company** `<select>`, populated from `_scope.Filter`/`IPermissionService.VisibleCompaniesAsync` (whichever the rest of the page already uses for scope-aware company lists) so the user only ever sees companies within their current HR scope.
   - A **Branch** `<select>`, filtered client-side by the chosen company using the same `data-company` attribute + `filterBy()` JS pattern already used for Company→Branch→JobTitle/Department/Sponsor in `_AddEmployeeTab_General.cshtml`. No AJAX reload.
   - A visible warning banner, shown/hidden client-side, when the selected company differs from the employee's current company.
   - A required "reason" free-text note (`Reason`, mapped to `ActivityLog.Details`).
   - A confirm button that submits the form.
3. **On confirm** (`POST /Employees/Transfer`):
   - Re-check `IPermissionService.CanAsync` for the transfer permission before doing anything.
   - Validate the destination branch exists and (if `_scope.Current` is narrower than Global) is inside the caller's current scope — same pattern `Create` already uses for `_scope.Current.CompanyId/BranchId`.
   - Update `Employee.CompanyId` and `Employee.BranchId`.
   - **No manual counter maintenance needed** — see Design decisions below; `Branch.EmployeesCount` / `Company.EmployeesCount` are computed live from `COUNT()` queries wherever they're read (`PermissionService.VisibleCompaniesAsync`/`VisibleBranchesAsync`, `CompaniesController`), not persisted/incremented columns. Moving the employee row is sufficient.
   - Write one `ActivityLog` entry via `IActivityLogger.LogAsync("Employee", employee.Id, "Transfer", ...)` with old company/branch, new company/branch, and the reason in `Details`.
   - Redirect back to `Detail`.

## Out of scope

- No dedicated transfer-history table — `ActivityLog` is the only record of past transfers, per `CLAUDE.md`'s guidance not to build new persistence beyond what's asked.
- Does not touch `EmployeesController.Create` or the initial-creation flow.
- Does not touch the permission system or `IScopeContext` themselves — only *uses* them.
- No bulk/multi-employee transfer — one employee at a time, from `Detail.cshtml`.

## Design decisions

- **Permission gate — reuses an existing, currently-unwired catalog entry.** `ServiceCatalog.cs` already defines a `"transfers"` service (`النقل` / `Transfers`, lifecycle category) that no controller action checks yet. **Confirmed:** gate the new action on `_perms.CanAsync(User, "transfers", HrBusinessPermissions.Edit, _scope.Current)` instead of inventing a new service id.
- **`Company.EmployeesCount` / `Branch.EmployeesCount` are NOT persisted counters.** Verified in `PermissionService.cs:146` ("Overwrite stale counters with real DB counts") — they're recomputed from `GroupBy`/`Count()` every time a company/branch list is loaded. This means the original ask ("update the counter on the old and new branch") requires no code — it already happens automatically. Flagging this because it changes the plan from what was originally described.
- **Reason note is optional.** Confirmed — matches the original ask.
- **`SponsorId` is independent of Company/Branch.** Confirmed by the user: the Sponsor column only tracks which company pays the employee's salary — it is not the same concept as `Employee.CompanyId`. It is not validated or touched by the transfer action.
- **`JobTitleId` / `DepartmentId` on cross-company transfer — RESOLVED, confirmed by real data.** Reviewed the sample employee sheets:
  - `عدد الموظفين الفعليين لتاريخ 12-08-2026.xlsx` (156 rows) has exactly the three columns that matter here: `جهة العمل` (Company), `الإدارة` (Branch — physical location, e.g. "ملاهي ستي مول الشفا"), `جهة استلام راتبه` (Sponsor — which of ~3 holding companies pays the salary). Confirms Sponsor is a payroll-only concept, independent of Company/Branch.
  - `جميع بيانات الموظفين 26-07-2026.xlsx` (485 rows, merged payroll export) has the same three columns under different headers (`جهة العمل` / `الفرع / الموقع` / `الشركة التابعة`), plus a populated `المسمى الوظيفي` (Job Title) column — but the same title text (e.g. "عامل", "سائق عموم مركبات") recurs across multiple different companies, so job titles are not meaningfully company-specific the way the app's `JobTitle` entity models them. Neither file has anything resembling the app's `Department` entity.
  - **Decision:** `Transfer` moves only `Employee.CompanyId`/`BranchId`. `JobTitleId`, `DepartmentId`, and `SponsorId` are left completely untouched — there is no Edit-employee screen to fix a forced mismatch, and the real data shows these three aren't tightly coupled to Company/Branch anyway.

## Data model changes

None. No new entity, no migration — `Employee.CompanyId`/`BranchId` already exist and are mutable columns.

## Files to add or change

- `Controllers/EmployeesController.cs` — add `Transfer` (GET, returns/validates lookups if needed) and `Transfer` (POST) actions.
- `Views/Employees/Detail.cshtml` — add the "Transfer" button + `<dialog>` modal (or a small `Views/Employees/_TransferEmployeeModal.cshtml` partial, mirroring how `_AddEmployeeTab_*.cshtml` partials are split out).
- `Models/ViewModels/Management/ManagementViewModels.cs` — add a `TransferEmployeeVM` (BranchId, CompanyId for lookups, Reason).
- `Models/Hr/HrPermissions/HrRolePermission.cs` — no change (reusing existing `HrBusinessPermissions.Edit`).
- `Models/Hr/ServiceCatalog.cs` — no change (reusing `"transfers"`, confirmed).

## Implementation steps

1. Add `TransferEmployeeVM` to `ManagementViewModels.cs`.
2. Add `Transfer` POST action to `EmployeesController` (permission check via `"transfers"`/`Edit` → scope check → destination branch validation → update `Employee.CompanyId`/`BranchId` only → `ActivityLog` → redirect).
3. Populate the Company/Branch lookups needed for the modal (extend `Detail`'s GET action or a small dedicated lookup action).
4. Add the button + modal markup to `Detail.cshtml`, reusing the existing cascading-dropdown JS pattern.
5. Manual test: same-company branch transfer, cross-company transfer, transfer attempted outside current scope (should be blocked), transfer without the `transfers`/`edit` permission (should be forbidden), verify `Branch`/`Company` employee counts update correctly on the Companies/Branches pages afterward, verify `ActivityLog` entry.
6. Update this file's Status to `done` and record any decisions that changed during implementation.

## Open questions

None outstanding — all resolved, see Design decisions above.
3. Should the "reason" note be required or optional? The original ask says "حقل اختياري (ملاحظة قصيرة)" (optional) — confirming: **optional**, unless you say otherwise.

---

## Changes

Add a new dated section here each time the feature is updated or extended. Do not rewrite history above — append below.

### 2026-08-24 — Implemented

- Added `TransferEmployeeVM` (`BranchId`, `Reason`, `Companies`/`Branches` lookup lists) to `Models/ViewModels/Management/ManagementViewModels.cs`; `EmployeeDetailVM` now carries `CanTransfer` + `Transfer`.
- `EmployeesController`: `Detail` GET now checks the `transfers`/`Edit` permission and populates scope-filtered Company/Branch lookups when the user can transfer. Added `POST Transfer(int id, TransferEmployeeVM m)` — re-checks the permission, validates the destination branch exists and is inside the caller's current scope, rejects a no-op transfer to the same branch, updates only `Employee.CompanyId`/`BranchId`, writes an `ActivityLog` entry ("from: old company/branch — to: new company/branch — reason: ..."), redirects back to `Detail`.
- `Views/Employees/Detail.cshtml`: added a "Transfer Employee" button (visible only when `CanTransfer`) and a `<dialog>` modal with the same cascading Company→Branch `<select>` pattern used on the Add Employee form (picking a branch also snaps the company dropdown to match), plus a client-side warning banner that appears when the chosen company differs from the employee's current one, and an optional reason textarea.
- No migration — only existing mutable columns touched.
- Build verified via `dotnet build` (no CS errors; only a pre-existing EPPlus version-resolution warning and a file-lock error copying the `.exe`, caused by the developer's own `dotnet run` instance already holding it open).
- Not yet manually tested end-to-end in the browser (needs `docker compose up -d db` + a running app) — see Implementation step 5 in this file for the test checklist.
