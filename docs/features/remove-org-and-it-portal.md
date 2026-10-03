# Remove Organization layer & IT Portal — collapse to single-root model

**Status:** in-progress
**Owner:** mhusef
**Created:** 2026-05-24

## Goal

Simplify the architecture by dropping the `Organization` hierarchy layer, deleting the IT Portal (`/Admin`), and removing the `Owner` table. After this change there is exactly one root user (Identity role `SuperAdmin`) who bypasses every permission check; everyone else is an `Employee` with explicit grants on services. The capabilities that used to live in the IT Portal (managing companies, branches, tickets, login/activity logs, plus a new "job titles" feature) become normal tiles in the service catalog. Public registration is removed — login is the only public-facing auth screen, and new employees are created from inside the system by someone with the `add-employee` service permission.

## Current behavior

- Hierarchy: `Owner → Organization → Company → Branch → Employee`.
- Two Identity roles: `SuperAdmin` and `Admin`. Plus an `Owner` table mapping an `ApplicationUser` to a business owner record.
- Separate IT Portal at `/Admin/*` with its own layout (`_AdminLayout`), parallel permission system (`IAdminPermissionService`, `AdminRole`, `AdminRolePermission`, `AdminRoleAssignment`).
- Separate Owner Portal at `/Owner/*` for HR role management, role assignments, and approving pending employees.
- Public `/Account/Register` creates an `EmployeeApplication` row in `Pending` status; the Owner Portal reviews and approves.
- Scope levels: `Global | Organization | Company | Branch`.

## Desired behavior

- Hierarchy: `Company → Branch → Employee`. Companies are top-level.
- One Identity role: `SuperAdmin`. No `Admin` role, no `Owner` table.
- SuperAdmin (Identity) bypasses every permission check and is the only user who can do anything without an explicit grant.
- Everyone else is an `Employee`. Permissions come exclusively from `EmployeeRoleAssignment` → `HrRole` → `HrRolePermission`.
- All former IT-portal capabilities live as tiles in a new **System** category in the service catalog:
  `manage-companies`, `manage-branches`, `manage-jobs`, `manage-tickets`, `view-login-log`, `view-activity-log`, `settings-hr-roles`, `settings-role-assignments`.
- `/Account/Register` is removed. Login is the only public auth screen. New employees are created from inside by anyone with the `add-employee` service permission — the action creates both `ApplicationUser` and `Employee` in one transaction.
- Scope levels: `Global | Company | Branch`.

## Out of scope

- Migrating real production data (DB is local-only).
- Adding new business services beyond `manage-jobs` / `JobTitle`.
- Password reset / email confirmation for new employees (admin hands credentials over out-of-band).
- Replacing the 28 existing business services or their layouts.

## Design decisions

- **SuperAdmin lives only in Identity, not in any business table.** Cleanest possible "one root user" rule. No row to keep in sync.
- **Job titles are scoped per-company** (`JobTitle.CompanyId` FK, implements `IScoped`) — mirrors how employees are scoped, and lets different companies define different position catalogs.
- **The existing `add-employee` service stays as the entry point for creating employees**, rather than inventing a new sysadmin-style screen — keeps the single permission-check pattern consistent (no parallel admin universe).
- **Migration is a clean rewrite of `AddHrCore`**, not an additive teardown — the DB is local-only and the seeder rebuilds everything from scratch. The schema we keep is the schema we generate.
- **`settings-pending-employees` is deleted, not migrated** — without public registration there is no waiting room, so the screen has no users.

## Data model changes

**Delete:** `Organization`, `Owner`, `EmployeeApplication`, `AdminRole`, `AdminRolePermission`, `AdminRoleAssignment`.

**Edit:** drop `OrganizationId` from `Company`, `EmployeeRoleAssignment`, `ActivityLog`, `SupportTicket`. Drop `OwnerId` from `HrRole`. Drop `RequestedOrganizationId` from `EmployeeApplication` (then the whole entity goes).

**New:** `JobTitle (Id, Code, NameAr, NameEn, CompanyId FK, IsActive)` implementing `IScoped`. Add nullable `JobTitleId` on `Employee`.

**Migration:** delete the existing `20260520030103_AddHrCore` files, regenerate cleanly against the trimmed model.

## Files to add or change

See the plan file at `~/.claude/plans/i-need-main-edit-gentle-acorn.md` for the exhaustive list. High-level:

- **Delete:** `Models/Hr/{Organization,Owner,EmployeeApplication}.cs`, `Models/Hr/AdminPermissions/`, `Controllers/{Admin,Owner,Pending}Controller.cs`, `Views/{Admin,Owner,Pending}/`, `Views/Shared/_{Admin,Owner}{Layout,Nav,Topbar}.cshtml`, `Views/Entry/Organization.cshtml`, `Views/Account/Register.cshtml`, `Services/Permissions/{IAdminPermissionService,AdminPermissionService,AdminPermissions}.cs`, `Models/ViewModels/{Admin,Owner,Auth/EmployeeRegisterViewModel}`.
- **Rewrite:** `Models/AppDbContext.cs`, `Services/Permissions/PermissionService.cs`, `Services/Scope/*`, `Models/Hr/HrSeeder.cs`, `Controllers/EntryController.cs`, `Models/Hr/ServiceCatalog.cs`, the `AddHrCore` migration.
- **Add:** `Models/Hr/JobTitle.cs`, `Controllers/{Companies,Branches,JobTitles,Tickets,LoginLog,ActivityLog,HrRoles,RoleAssignments,Employees}Controller.cs` plus matching views, `Models/ViewModels/Employees/AddEmployeeViewModel.cs`.

## Implementation steps

1. Update EF model: delete entities, drop FK columns, add `JobTitle`, update `AppDbContext`.
2. Regenerate `AddHrCore` migration cleanly. Verify schema.
3. Simplify scope system (drop `Organization` level).
4. Delete `IAdminPermissionService`; simplify `PermissionService` (drop `Admin`/`Owner`/`Pending` `UserKind` branches).
5. Update `ServiceCatalog` (new `system` category + new services).
6. Delete IT Portal: `AdminController`, `Views/Admin/`, `_Admin*` layouts.
7. Delete Owner Portal + `PendingController`; rebuild surviving features as new service-gated controllers under `_Layout`.
8. Update Entry flow (3 steps: Welcome → Company → Branch). Delete public registration entirely.
9. Rewrite `HrSeeder` (only SuperAdmin + companies/branches/employees, no Owner/Org).
10. Clean up stragglers (`HelpController` role checks, `_Topbar` nav, leftover `[Authorize(Roles="Admin")]`).
11. `dotnet build` + reset DB + `dotnet run -- seed` + smoke-test the verification checklist.

## Open questions

- (resolved 2026-05-23) "Jobs" = job positions/titles; new `JobTitle` entity scoped per-company.
- (resolved 2026-05-23) Migration = clean rewrite of `AddHrCore`.
- (resolved 2026-05-23) New services live in a new `system` category.
- (resolved 2026-05-24) Public registration removed; employees created via existing `add-employee` service.

---

## Changes

### 2026-05-24 — Plan approved; implementation started.

Initial scope captured above. Implementation work begins this commit.
