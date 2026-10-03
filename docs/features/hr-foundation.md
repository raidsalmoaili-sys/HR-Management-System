# Foundation phase — port HR prototype into ASP.NET Core

## Context

Repo: `/Users/mohammedyusef/development/dotne/Project_HR_1`. Existing ASP.NET Core 10 MVC app with EF Core + PostgreSQL + stock Identity. Prototype at `docs/design/source/` — 28 services, bilingual AR/EN with RTL, 4-step entry flow, scope-aware data, navy/gold visual system.

Foundation = the shared scaffolding so each of the 28 prototype services can later land as its own `docs/features/<service>.md` plan.

### Confirmed decisions (this iteration)

- Employee and User are separate tables. The link FK lives on `Employee.UserId` (and `Owner.UserId`). `ApplicationUser` has no business FKs.
- Owner: one Owner can own many Organizations (1:N).
- User types via `IdentityRole`s `SuperAdmin`/`Admin` (IT) + presence of row in `Owner`/`Employee` table (Owner/Employee).
- **Two parallel permission systems**:
  - **HrRole** — bundles of `(serviceId, permission)` assigned to Employees at a scope (Org/Company/Branch). Managed by Owner (or delegated).
  - **AdminRole** — bundles of admin-portal capabilities assigned to Admin users (global, no scope). Managed by SuperAdmin.
- **System capabilities** (manage HR roles, assign roles, approve employees) are modeled as synthetic HrRole permissions under sentinel `ServiceId = "_system"` so Owner can delegate them to Employees.
- **Public registration is Employee-only** — creates an `EmployeeApplication` row (pending), never a final `Employee` directly. Owner reviews and approves, assigning Org/Company/Branch + initial HrRole.
- **Admin/SuperAdmin/Owner accounts** are created from the IT Portal or seeder, never via public registration.
- Login & Register pages restyled to the prototype's navy/gold + Tajawal/Inter aesthetic; new `_AuthLayout`.
- Employee PK: integer + `EmployeeCode` string ("E1001"). Job title is `JobTitleAr/En` (renamed from RoleAr/En to avoid confusion with permission Role).
- Skip-empty-step: at each entry step, filter to visible choices. If exactly one remains, set scope automatically and hide that step in the stepper.
- Tailwind: compiled via npm.
- Seeder: manual only (`dotnet run -- seed`). Includes SuperAdmin + demo Owner + demo orgs/companies/branches/employees + sample HrRoles + sample AdminRoles.
- Owner Portal foundation UI: approve pending employees + HrRole CRUD + assign HrRoles to employees.
- Admin Portal foundation UI: create Owners + create Admins + AdminRole CRUD + assign AdminRoles + tickets list.

---

## 1. Save the HR-system skill

- **New** `.claude/skills/hr-system/SKILL.md` — the SKILL.md the user pasted, prefixed with YAML frontmatter (`name: hr-system`, descriptive `description`). Body verbatim; one TODO line at top noting the mojibake on Arabic characters.

## 2. ApplicationUser + IdentityRoles

- **New** `Models/ApplicationUser.cs` — `ApplicationUser : IdentityUser`. Empty class.
- **Modify** `Models/AppDbContext.cs` — base class `IdentityDbContext<ApplicationUser, IdentityRole, string>`. Add all HR DbSets.
- **Modify** `Program.cs` — `AddIdentity<ApplicationUser, IdentityRole>(...)`. Existing password policy stays.
- **Modify** `Controllers/AccountController.cs` — swap to `<ApplicationUser>` typed managers.
- Seeder creates IdentityRoles `SuperAdmin` and `Admin`.

## 3. Owner entity

- **New** `Models/Hr/Owner.cs`:
  ```
  Id (int PK)
  OwnerCode (string, unique — "OWN-001")
  UserId (string, unique, FK → AspNetUsers.Id)
  User (ApplicationUser nav)
  NameAr, NameEn, Phone, Email
  CreatedAt, CreatedByUserId (FK → AspNetUsers)
  Organizations (nav: Organization[])
  ```

## 4. AdminRole permission system

For IT-portal capabilities only. Separate from `HrRole`.

- **New** `Models/Hr/AdminPermissions/AdminRole.cs`:
  ```
  Id (int PK)
  Code (string, unique — "tickets-only")
  NameAr, NameEn, Description?
  IsSystem (bool)            // can't delete if true
  Permissions (nav: AdminRolePermission[])
  ```
- **New** `Models/Hr/AdminPermissions/AdminRolePermission.cs`:
  ```
  Id, AdminRoleId (FK), Permission (string)
  ```
  Where `Permission` is one of a fixed string set, defined as constants in **`Services/Permissions/AdminPermissions.cs`**:
  - `manage-owners`
  - `manage-admins`
  - `manage-admin-roles`
  - `view-tickets`
  - `manage-tickets`
  - `view-login-log`
  - `view-activity-log`
  Unique composite `(AdminRoleId, Permission)`.

- **New** `Models/Hr/AdminPermissions/AdminRoleAssignment.cs`:
  ```
  Id, UserId (FK → AspNetUsers), AdminRoleId (FK)
  AssignedByUserId (FK → AspNetUsers), AssignedAt
  ```
  An admin user can have multiple AdminRoles. SuperAdmin bypasses this entirely (god-mode).

## 5. Scope entities

All under `Models/Hr/`. Fields the foundation actually uses; salary/contract/residency etc. deferred to per-service plans.

- **`Organization.cs`** — `Id`, `Slug` (unique), `OwnerId` (FK → Owner), `NameAr`, `NameEn`, `Initials`, `SectorAr`, `SectorEn`, `EmployeesCount`, `CompaniesCount`, `BranchesCount`, `LastLoginLabel?`, `Status`, `IsPrimary`, `Companies` (nav).
- **`Company.cs`** — `Id`, `Slug` (unique), `OrganizationId` (FK), `NameAr`, `NameEn`, `ShortNameAr`, `ShortNameEn`, `Initials`, `SectorAr`, `SectorEn`, `EmployeesCount`, `BranchesCount`, `EstablishedYear` (string), `Branches` (nav).
- **`Branch.cs`** — `Id`, `Slug` (unique), `CompanyId` (FK), `NameAr`, `NameEn`, `CityAr`, `CityEn`, `EmployeesCount`, `ManagerAr`, `AddressAr`, `IsPrimary`, `Employees` (nav).
- **`Employee.cs`** — approved employees only:
  ```
  Id (int PK), EmployeeCode (string, unique)
  UserId (string?, unique, FK → AspNetUsers)
  NameAr, NameEn, Initials
  JobTitleAr, JobTitleEn       // job, NOT role
  DeptAr
  CompanyId (FK, required), BranchId (FK, required)
  Status, AvatarColor, Email
  ApprovedByUserId (FK → AspNetUsers), ApprovedAt
  ApplicationId? (FK → EmployeeApplication — audit trail)
  ```
- **`EmployeeApplication.cs`** — pending employee registrations:
  ```
  Id (int PK)
  UserId (string, unique, FK → AspNetUsers)     // user account already created
  NameAr, NameEn, Email, Phone
  RequestedOrganizationId (FK → Organization)   // applicant picks during register
  RequestedCompanyId? (FK)                       // optional hints
  RequestedBranchId? (FK)
  Status (enum: Pending | Approved | Rejected)
  AppliedAt
  ReviewedByUserId? (FK → AspNetUsers), ReviewedAt?
  RejectionReason?
  ```
  On approval, an `Employee` row is created and the application is marked `Approved` (kept for audit) with the new Employee.Id stored back in `Employee.ApplicationId`.

### DbContext config

- Unique indexes on every `Slug`, `EmployeeCode`, `OwnerCode`, `Owner.UserId`, `Employee.UserId`, `EmployeeApplication.UserId`.
- Scope FKs `OnDelete(Restrict)`. `*.UserId` FKs `OnDelete(SetNull)` where the column is nullable, else `Restrict`.

## 6. HrRole permission system

All under `Models/Hr/HrPermissions/`.

- **`HrRole.cs`**:
  ```
  Id (int PK), Code (string, unique)
  NameAr, NameEn, Description?
  IsSystem (bool)
  OwnerId? (FK → Owner — null for system roles; otherwise the owner who created it)
  Permissions (nav)
  ```
- **`HrRolePermission.cs`**:
  ```
  Id, HrRoleId (FK), ServiceId (string), Permission (string)
  ```
  Where `ServiceId` is either a kebab-case business service ID (`"payroll"`, `"leaves"`, ...) OR the sentinel `"_system"` for system capabilities. `Permission` is one of `"view"|"create"|"edit"|"delete"|"approve"` for business services, OR one of:
  - `manage-hr-roles`
  - `assign-hr-roles`
  - `approve-employees`
  - `view-employees`
  for `ServiceId = "_system"`.
  Unique composite `(HrRoleId, ServiceId, Permission)`.

- **`EmployeeRoleAssignment.cs`**:
  ```
  Id, EmployeeId (FK), HrRoleId (FK)
  ScopeLevel (enum: Organization | Company | Branch)
  OrganizationId? (FK), CompanyId? (FK), BranchId? (FK)
  AssignedByUserId (FK), AssignedAt
  ```
  Constraint enforced in code: exactly one of `OrgId/CompanyId/BranchId` non-null, matching `ScopeLevel`.

## 7. Service catalog

- **New** `Models/Hr/HrService.cs` — `record HrService(string Id, string NameAr, string NameEn, string Icon, string Color, string Category, int? Badge = null)`.
- **New** `Models/Hr/HrServiceCategory.cs` — `record HrServiceCategory(string Id, string NameAr, string NameEn)`.
- **New** `Models/Hr/ServiceCatalog.cs`:
  - `Services` = 28 entries ported verbatim from `docs/design/source/assets/services.js`.
  - `Categories` = 7 entries.
  - **Plus** a synthetic 8th category `settings` with 3 entries — these don't appear in the prototype's launcher (they're rendered separately in the Owner Portal nav, see §17):
    - `settings-hr-roles` ("Manage HR Roles" / "إدارة الأدوار")
    - `settings-role-assignments` ("Assign Roles" / "إسناد الأدوار")
    - `settings-pending-employees` ("Pending Employees" / "الموظفون قيد المراجعة")
  - Helpers `ById(string)`, `ByCategory(string)`, `BusinessServices` (excludes settings category).
- **Permissions in §6 use `_system` as ServiceId** — the catalog's `settings` category is purely UI navigation. The wiring: each setting screen calls `Permissions.CanAsync(user, "_system", "manage-hr-roles", scope)` etc. Synthetic-service category lets the launcher / Owner nav present the same surface for delegated employees.

## 8. Permission services

Two services, both consulted by the layout/controllers.

- **New** `Services/Permissions/IPermissionService.cs` (HR side):
  ```
  Task<bool> CanAsync(ClaimsPrincipal user, string serviceId, string permission, HrScope? scope = null);
  Task<IReadOnlySet<string>> AllowedServicesAsync(ClaimsPrincipal user, HrScope scope);
  Task<IReadOnlyList<Organization>> VisibleOrganizationsAsync(ClaimsPrincipal user);
  Task<IReadOnlyList<Company>> VisibleCompaniesAsync(ClaimsPrincipal user, int organizationId);
  Task<IReadOnlyList<Branch>> VisibleBranchesAsync(ClaimsPrincipal user, int companyId);
  Task<UserKind> ResolveAsync(ClaimsPrincipal user); // enum: SuperAdmin, Admin, Owner, Employee, Pending, None
  ```
- **New** `Services/Permissions/PermissionService.cs` — rules:
  - `SuperAdmin` / `Admin` IdentityRole → all-allowed.
  - `Owner` (row in `Owners` with `UserId`) → allowed for any org/company/branch under their owned orgs. Has all `_system` permissions implicitly.
  - `Employee` (row in `Employees` with `UserId`) → resolved via `EmployeeRoleAssignment`:
    - Branch-scope assignment → permissions at that branch.
    - Company-scope → permissions at every branch under that company.
    - Org-scope → permissions at every company/branch under that org.
    - Visible orgs/companies/branches = closure of all assignments + own company/branch.
  - `EmployeeApplication` row only → `Pending` (no permissions; UI shows pending screen).
  - Nothing → `None` (no access).
  - Per-request memoization.
- **New** `Services/Permissions/IAdminPermissionService.cs`:
  ```
  Task<bool> CanAsync(ClaimsPrincipal user, string adminPermission);
  Task<IReadOnlySet<string>> AllowedAdminPermissionsAsync(ClaimsPrincipal user);
  ```
- **New** `Services/Permissions/AdminPermissionService.cs` — rules:
  - `SuperAdmin` IdentityRole → all admin permissions.
  - `Admin` IdentityRole → union of `AdminRolePermission`s across their `AdminRoleAssignment`s.
  - Anyone else → none.
- Register both scoped in `Program.cs`.

## 9. i18n — `IHrLocalizer`

- **New** `Services/I18n/HrDict.cs` — port ~290 keys from `docs/design/source/assets/i18n.js`. Plus new keys for new screens: `auth_login`, `auth_register`, `auth_email`, `auth_password`, `auth_confirm_password`, `auth_remember_me`, `auth_signin`, `auth_register_btn`, `auth_have_account`, `auth_no_account`, `auth_application_title`, `auth_select_org`, `auth_pending_title`, `auth_pending_msg`, plus Owner-portal/Admin-portal labels.
- **New** `Services/I18n/IHrLocalizer.cs` — `Lang`, `IsRtl`, `T(key)`, `L(ar, en)`.
- **New** `Services/I18n/HrLocalizer.cs` — reads `hr_lang` cookie (default `"ar"`), falls back to key on dict miss.
- **New** `Controllers/LanguageController.cs` — `[HttpPost] Set(string lang, string returnUrl)` → write `hr_lang` cookie → `LocalRedirect`.
- `Program.cs` — `AddHttpContextAccessor()` + `AddScoped<IHrLocalizer, HrLocalizer>()`.
- `Views/_ViewImports.cshtml` — `@inject IHrLocalizer L`.

## 10. Compiled Tailwind + tokens

- **New** `package.json` — devDeps `tailwindcss@^3.4`, `postcss`, `autoprefixer`, `tailwindcss-rtl`. Scripts `css:dev`, `css:build`.
- **New** `tailwind.config.js` — `content: ["./Views/**/*.cshtml", "./wwwroot/js/**/*.js"]`; theme.extend with color tokens (mapped to CSS vars for Tweaks runtime mutation), fontFamily (`tajawal`, `inter`), borderRadius (`card`, `ctrl`); plugin `tailwindcss-rtl`.
- **New** `postcss.config.js` — standard.
- **New** `wwwroot/css/app.css` — `@tailwind` directives + `:root` CSS vars (`--hr-primary`, `--hr-accent` + variants) + `[data-density]` blocks + component classes for `.entry-shell`, `.choice-card`, `.svc-tile`, `.scope-strip`, `.topbar`, `.svc-nav`, `.tweaks-panel`, `.stepper-step`, `.auth-card`, `.auth-input`, `.admin-nav`, `.owner-nav`.
- **Modify** `.gitignore` — add `node_modules/`, `wwwroot/css/app.min.css`, `wwwroot/css/app.min.css.map`.

## 11. Scope context + middleware

- **New** `Services/Scope/HrScope.cs` — `enum ScopeLevel { Global, Organization, Company, Branch }` + class `HrScope`.
- **New** `Services/Scope/IScoped.cs` — marker (foundation entities do not implement it; per-service entities will).
- **New** `Services/Scope/IScopeContext.cs` + `ScopeContext.cs` — `Current`, `Color`, `LabelAr`, `LabelEn`, `Filter<T>`.
- **New** `Services/Scope/ScopeCookieMiddleware.cs` — reads/writes `hr_scope` cookie (JSON, essential, 30d).
- Register in `Program.cs`.

## 12. Activity + Login logs

- **New** `Models/Hr/Audit/LoginLog.cs` — `Id (long), Timestamp, UserId? (FK), Email, Success, IpAddress, UserAgent, FailureReason?`.
- **New** `Models/Hr/Audit/ActivityLog.cs` — `Id (long), Timestamp, UserId (FK), EmployeeId? (FK), EntityType, EntityId, Action, Details? (json), OrgId?/CompanyId?/BranchId?, IpAddress?`.
- **New** `Services/Audit/IActivityLogger.cs` + `ActivityLogger.cs` — `LogAsync(...)`.
- Login & logout in `AccountController` write to `LoginLog` (always) and `ActivityLog` (on success only).
- Register both scoped in `Program.cs`.

## 13. Support tickets

- **New** `Models/Hr/Support/SupportTicket.cs` — `Id, TicketCode (unique), CreatedByUserId (FK), AssignedToUserId? (FK), OrganizationId?/CompanyId?/BranchId?, Subject, Description, Priority (enum), Status (enum), CreatedAt, UpdatedAt`.
- Replies/attachments → deferred to a `support-replies` feature plan; foundation has bare ticket only.

## 14. Layouts + partials

- **Modify** `Views/Shared/_Layout.cshtml` — full HR shell for the Owner/Employee app:
  - `<html lang="@L.Lang" dir="@(L.IsRtl ? "rtl" : "ltr")" data-density="..." style="...tweak vars from cookie...">`.
  - `<head>` loads Tajawal + Inter, Font Awesome 6.5, `~/css/app.min.css`. Remove the Tailwind CDN script tag.
  - Body skeleton: `_Topbar`, `_ScopeStrip`, `_SvcNav`, `<main class="page-area">@RenderBody()</main>`, `_TweaksFab`, `_TweaksPanel`.
- **New** `Views/Shared/_EntryLayout.cshtml` — head same; body: `_EntryTopbar` + `_Stepper` (receives visible steps) + `@RenderBody()`.
- **New** `Views/Shared/_AuthLayout.cshtml` — head same; body: language-switch chip in corner + centered `<main class="auth-shell">@RenderBody()</main>`. No topbar, no scope. Used by Login + Register + Pending status pages.
- **New** `Views/Shared/_AdminLayout.cshtml` — head same; body: `_AdminTopbar` (brand + lang + user) + `_AdminNav` (Home, Owners, Admins, Admin Roles, Tickets — filtered by AdminPermission) + `@RenderBody()`.
- **New** `Views/Shared/_OwnerLayout.cshtml` — full HR shell with an extra `_OwnerNav` section (Pending Employees, HR Roles, Role Assignments) shown only to users with the relevant `_system` permissions. Used by `Owner/*` controller views.
- **New** `Views/Shared/_PublicLayout.cshtml` — small navbar for Home/Privacy.
- **New partials** (`Views/Shared/`): `_Topbar`, `_ScopeStrip`, `_SvcNav`, `_EntryTopbar`, `_Stepper`, `_TweaksFab`, `_TweaksPanel`, `_AdminTopbar`, `_AdminNav`, `_OwnerNav`, `_AuthLangSwitch`.
- **Modify** `Views/Home/Index.cshtml`, `Views/Home/Privacy.cshtml` — use `_PublicLayout`.
- **Modify** `Views/_ViewImports.cshtml` — `@inject IScopeContext Scope`, `@inject IPermissionService Permissions`, `@inject IAdminPermissionService AdminPermissions`.

## 15. Login + Register UI (styled + Employee-only)

- **Modify** `Views/Account/Login.cshtml` — uses `_AuthLayout`. Card-centered design: brand logo at top, bilingual welcome heading from `L.T(...)`, email + password + "remember me", navy submit button, "No account? Register as employee" link at bottom. Language switcher in the corner via `_AuthLangSwitch`.
- **Modify** `Views/Account/Register.cshtml` — uses `_AuthLayout`. Form fields: `NameAr`, `NameEn`, `Email`, `Phone`, `RequestedOrganizationId` (select populated from DB), `Password`, `ConfirmPassword`. Subtitle: "Your application will be reviewed by the organization owner". Bilingual labels.
- **Modify** `Models/RegisterViewModel.cs` → **rename to `Models/EmployeeRegisterViewModel.cs`** with all new fields above. Validation: name fields required, email unique, password ≥6 chars (matches existing policy).
- **Modify** `Controllers/AccountController.cs`:
  - `GET Register` populates a `EmployeeRegisterViewModel` with the list of available Organizations (from DB) for the dropdown.
  - `POST Register(EmployeeRegisterViewModel m)`:
    1. Validate.
    2. Create `ApplicationUser` (email + password). Do NOT sign in yet.
    3. Create `EmployeeApplication` row with `Status=Pending`, `RequestedOrganizationId=m.RequestedOrganizationId`, `UserId=user.Id`.
    4. Sign user in.
    5. Redirect to `/Pending` (a status screen — see §16).
  - `POST Login` success: write `LoginLog` row, redirect to `/Entry/Resume`.
  - `POST Login` failure: write `LoginLog` row (success=false, reason), re-render.
  - `Logout`: write `ActivityLog` row Action=Logout, redirect to `/`.

## 16. Entry flow + skip + Pending status

- **New** `Controllers/EntryController.cs` (`[Authorize]`):
  - `GET Resume` — dispatch based on `Permissions.ResolveAsync(user)`:
    - `SuperAdmin`/`Admin` → `/Admin`.
    - `Owner` → `Welcome` (or skip per scope rules).
    - `Employee` → `Welcome` (filtered + skip).
    - `Pending` → `/Pending`.
    - `None` → `/NoAccess`.
  - `GET Welcome` / `GET Organization` — Welcome wraps Org step with intro copy. View receives `OrganizationStepVM { Orgs, VisibleSteps }`.
  - **Skip logic** at the start of each step's GET action:
    - If exactly 1 visible choice remains → set cookie, redirect to next step; mark this step `Visible=false` in the steps list.
    - If 0 → "Enter at this scope level" CTA.
  - `POST ChooseOrganization/ChooseCompany/ChooseBranch` — write cookie, redirect.
  - `POST EnterGlobal/EnterOrgScope/EnterCompanyScope` — explicit higher-level entries.
  - `POST SwitchScope` — used by scope-strip chips.
  - `POST Reset` — clear `hr_scope`, redirect `Welcome`.
- **New** `Controllers/PendingController.cs` (`[Authorize]`) — `GET Index` renders `Views/Pending/Index.cshtml` showing application status + applied org + applied date. Uses `_AuthLayout`.
- **New** view models (`Models/ViewModels/Entry/`): `OrganizationStepVM`, `CompanyStepVM`, `BranchStepVM`, `LauncherVM`, `EntryStep`.
- **New** views (`Views/Entry/`): `Welcome`, `Organization`, `Company`, `Branch`, `Launcher`, `NoAccess`. Markup ported from `docs/design/source/assets/router.js` entry screens. Forms only, no JS.
- **New** `Controllers/ServiceController.cs` — `[Authorize] GET Show(string id)`: 404 if not in `ServiceCatalog`; check `Permissions.CanAsync(user, id, "view", currentScope)` — 403 if no; else render `Views/Service/Placeholder.cshtml` ("Coming soon. Plan: docs/features/<id>.md").
- `Program.cs` — `MapControllerRoute("service", "Service/{id}", ...)`.

## 17. Owner Portal

`[Authorize]` + per-action permission checks (`_system/manage-hr-roles` etc., or implicit Owner-role bypass).

- **New** `Controllers/OwnerController.cs`. Uses `_OwnerLayout`.
  - **Pending employees**
    - `GET Pending` — list `EmployeeApplication.Status=Pending` for orgs visible to current Owner (or to Employee with `_system/approve-employees`).
    - `GET Pending/{id}` — review form: applicant data + dropdowns `OrganizationId` / `CompanyId` (filtered by org) / `BranchId` (filtered by company) + optional `InitialHrRoleId` picker + scope-level picker.
    - `POST Approve(int id, ApproveApplicationVM m)` — create `Employee` row, set `Status=Approved`, link `User`. If `InitialHrRoleId` set, create one `EmployeeRoleAssignment`. ActivityLog write. Redirect `Pending`.
    - `POST Reject(int id, string reason)` — set `Status=Rejected`, store reason. ActivityLog. Redirect.
  - **HR Roles**
    - `GET HrRoles` — list `HrRole` rows scoped to Owner's org(s) + system roles (`OwnerId IS NULL AND IsSystem=true`).
    - `GET HrRoles/Create` / `POST HrRoles/Create(HrRoleEditVM m)` — name (ar/en), description, permission grid (one checkbox per `(ServiceId, Permission)` combo across all 28 business services + 4 `_system` permissions). Save → create `HrRole` + `HrRolePermission` rows.
    - `GET HrRoles/Edit/{id}` / `POST HrRoles/Edit(int id, HrRoleEditVM m)` — same grid; replace permissions.
    - `POST HrRoles/Delete/{id}` — block if `IsSystem=true`; else delete (cascade removes assignments).
  - **Assignments**
    - `GET Assignments?employeeId={id}` — for one employee, list their current `EmployeeRoleAssignment`s.
    - `POST Assignments/Create(AssignRoleVM m)` — `(employeeId, hrRoleId, scopeLevel, orgId?, companyId?, branchId?)`. Validate "exactly one scope id matches level". ActivityLog.
    - `POST Assignments/Delete/{id}` — remove assignment.
  - **Employees list** (read-only for foundation)
    - `GET Employees` — list employees visible to current Owner.
    - `GET Employees/{id}` — detail + "Assign role" link.
- **New** view models (`Models/ViewModels/Owner/`): `PendingListVM`, `ApproveApplicationVM`, `HrRoleEditVM` (incl. permission grid struct), `AssignRoleVM`, `EmployeesListVM`, `EmployeeDetailVM`.
- **New** views (`Views/Owner/`): `Pending.cshtml`, `PendingReview.cshtml`, `HrRoles.cshtml`, `HrRoleEdit.cshtml`, `Assignments.cshtml` (partial-style embedded in employee detail), `Employees.cshtml`, `EmployeeDetail.cshtml`.
- `_OwnerNav` partial: shows links to Pending / HR Roles / Employees based on which `_system` permissions the current user has (or always for Owners).

## 18. Admin Portal

`[Authorize(Roles = "SuperAdmin,Admin")]` on the class; per-action `AdminPermissions.CanAsync(...)` checks.

- **New** `Controllers/AdminController.cs`. Uses `_AdminLayout`.
  - `GET Index` — stats (counts).
  - **Owners** (gate: `manage-owners`)
    - `GET Owners` — list.
    - `GET Owners/Create` / `POST Owners/Create(CreateOwnerVM m)` — create `ApplicationUser` (email + temp password, printed to console for foundation), create `Owner` row. ActivityLog.
    - `POST Owners/Delete/{id}` — soft-delete (set Status; user account remains).
  - **Admins** (gate: `manage-admins`)
    - `GET Admins` — list users in `Admin` IdentityRole.
    - `GET Admins/Create` / `POST Admins/Create(CreateAdminVM m)` — create `ApplicationUser`, add to `Admin` IdentityRole. Optionally assign an initial `AdminRole`. Temp password to console.
    - `GET Admins/{id}` — detail with assigned AdminRoles + add/remove form.
    - `POST Admins/AssignRole(int userId, int adminRoleId)` — create `AdminRoleAssignment`.
    - `POST Admins/RemoveRole(int assignmentId)` — delete.
  - **AdminRoles** (gate: `manage-admin-roles`)
    - `GET AdminRoles` — list.
    - `GET AdminRoles/Create` / `POST` — checkbox grid of admin permissions, save.
    - `GET AdminRoles/Edit/{id}` / `POST` — edit.
    - `POST AdminRoles/Delete/{id}` — block if `IsSystem`.
  - **Tickets** (gate: `view-tickets` / `manage-tickets`)
    - `GET Tickets?status=...` — list.
    - `GET Tickets/{id}` — detail.
    - `POST Tickets/{id}/Assign(string userId)` — assign IT user.
    - `POST Tickets/{id}/Status(string status)` — change status.
- **New** view models (`Models/ViewModels/Admin/`): `AdminDashboardVM`, `CreateOwnerVM`, `CreateAdminVM`, `AdminRoleEditVM`, `TicketListVM`, `TicketDetailVM`.
- **New** views (`Views/Admin/`): `Index`, `Owners`, `OwnerCreate`, `Admins`, `AdminCreate`, `AdminDetail`, `AdminRoles`, `AdminRoleEdit`, `Tickets`, `TicketDetail`.

## 19. Help Center

`[Authorize]`.

- **New** `Controllers/HelpController.cs` (uses `_Layout` — same shell as the rest of the user app):
  - `GET Index` — list current user's tickets.
  - `GET New` / `POST New(CreateTicketVM m)` — create with current scope context, redirect `Index`.
  - `GET Ticket/{id}` — view; only the creator or SuperAdmin/Admin can see.
- Topbar "Help" icon in `_Topbar` (and `_AdminTopbar`) → `/Help`.

## 20. Single migration

After §2–§7 + §11–§13 compile, generate ONE migration `AddHrCore`:
```
dotnet ef migrations add AddHrCore
dotnet ef database update
```
Tables created: `Owners`, `Organizations`, `Companies`, `Branches`, `Employees`, `EmployeeApplications`, `HrRoles`, `HrRolePermissions`, `EmployeeRoleAssignments`, `AdminRoles`, `AdminRolePermissions`, `AdminRoleAssignments`, `LoginLogs`, `ActivityLogs`, `SupportTickets`. Plus FKs to `AspNetUsers`.

## 21. Manual seeder

- **New** `Models/Hr/HrSeeder.cs` — `public static async Task SeedAsync(IServiceProvider sp)`:
  1. Ensure IdentityRoles `SuperAdmin`, `Admin`.
  2. Ensure SuperAdmin user `admin@local` (temp password printed; reused if exists).
  3. Wipe HR tables (orgs/companies/branches/employees/applications/hr_roles/hr_role_permissions/employee_role_assignments/admin_roles/admin_role_permissions/admin_role_assignments). **Does NOT touch Identity tables on re-run** beyond ensuring the SuperAdmin exists.
  4. Create demo Owner user (`hr@demo.local`) + `Owner` row.
  5. Port 2 orgs / 5 companies / 10 branches / ~24 employees from `docs/design/source/assets/data.js`. Each employee gets an `ApplicationUser` (so they can log in; password printed).
  6. Sample `HrRole`s: `"hr-full-access"` (all 28 business services × all perms + all `_system` perms), `"hr-manager"` (employees+attendance+leaves+payroll subset), `"hr-view-only"` (every service × `view`). Mark `IsSystem=true`.
  7. Assign `hr-full-access` at `Organization` scope to 1–2 demo employees.
  8. Sample `AdminRole`s: `"admin-full"` (all admin perms), `"tickets-only"` (`view-tickets`+`manage-tickets`), `"owner-creator"` (`manage-owners`). `IsSystem=true`.
- **Modify** `Program.cs` — top of `Main`: if `args.Contains("seed")`, build minimal host, resolve `IServiceProvider`, run `HrSeeder.SeedAsync(sp)`, exit 0. Otherwise normal startup. Never auto-seed.

## 22. Update `CLAUDE.md`

Append a new section **"HR system architecture"** with:
- `ApplicationUser` has no business FKs. Employee↔User link lives on `Employee.UserId`. Same for `Owner.UserId`.
- IdentityRoles: `SuperAdmin`, `Admin`. Owner/Employee detected via table row.
- Two permission systems:
  - `IPermissionService` for HR services (consults `EmployeeRoleAssignment` + `HrRolePermission`; supports synthetic `_system` permissions).
  - `IAdminPermissionService` for IT-portal capabilities (consults `AdminRoleAssignment` + `AdminRolePermission`).
- `IScopeContext` is read-only "what scope am I in"; `IScoped` marker for entities that want scope filtering.
- `IHrLocalizer` (`L.T(key)`, `L.L(ar, en)`); cookie `hr_lang`.
- `ServiceCatalog.Services` = 28 business + 3 settings synthetic. Single source of truth.
- Public registration is Employee-only and creates `EmployeeApplication`. Owners approve. SuperAdmin/Admin/Owner accounts come from the IT portal or the seeder.
- Layouts: `_Layout` (HR shell), `_EntryLayout` (entry steps), `_AuthLayout` (login/register/pending), `_AdminLayout` (IT portal), `_OwnerLayout` (HR shell + Owner nav), `_PublicLayout` (Home/Privacy).
- Tailwind: `npm run css:dev` while developing, `npm run css:build` before commit.
- Seed: `dotnet run -- seed` (manual only).
- Skill: `.claude/skills/hr-system/SKILL.md`.

Append to "Common commands": `npm install`, `npm run css:dev`, `npm run css:build`, `dotnet run -- seed`.

---

## Implementation order

Each step compiles + runs on its own. Don't batch.

1. §1 skill file.
2. §22 CLAUDE.md stub (architecture notes ahead of code).
3. §2 ApplicationUser + Identity type swap + DbContext base change. Compile.
4. §3 Owner + §4 AdminRole + §5 scope/employee/application + §6 HrRole + §12 audit + §13 ticket entities — one pass. Compile.
5. §7 ServiceCatalog (pure code, no DB).
6. §9 i18n.
7. §10 Tailwind (`npm install` + `npm run css:build` smoke test).
8. §11 ScopeContext + middleware. Register in DI.
9. §8 PermissionService + AdminPermissionService. Register in DI.
10. §21 seeder + §20 migration. `dotnet ef database update`. `dotnet run -- seed`. Verify DB.
11. §14 layouts + partials. Move Home/Privacy to `_PublicLayout`.
12. §15 Login + Register restyle + Employee-only register flow. Hook login logs.
13. §16 EntryController + PendingController + ServiceController + views.
14. §19 HelpController + views.
15. §17 OwnerController + views (Pending, HrRoles, Assignments, Employees).
16. §18 AdminController + views (Owners, Admins, AdminRoles, Tickets).
17. CLAUDE.md polish (fix any path drift during implementation).
18. Manual verification walk (below).

---

## Critical files

- `Program.cs` — Identity types, DI registrations, middleware order, `seed` arg, service route.
- `Models/AppDbContext.cs` — base class change, HR DbSets, model config.
- `Models/Hr/ServiceCatalog.cs` (new) — 28 business + 3 settings synthetic services.
- `Models/Hr/Owner.cs`, `Organization.cs`, `Company.cs`, `Branch.cs`, `Employee.cs`, `EmployeeApplication.cs` (new).
- `Models/Hr/HrPermissions/{HrRole, HrRolePermission, EmployeeRoleAssignment}.cs` (new).
- `Models/Hr/AdminPermissions/{AdminRole, AdminRolePermission, AdminRoleAssignment}.cs` (new).
- `Models/Hr/Audit/{LoginLog, ActivityLog}.cs` (new), `Models/Hr/Support/SupportTicket.cs` (new).
- `Services/Permissions/PermissionService.cs` + `AdminPermissionService.cs` (new) — every access decision.
- `Services/Scope/ScopeContext.cs` (new) — every scope read.
- `Services/I18n/HrDict.cs` (new) + `HrLocalizer.cs` (new).
- `Models/Hr/HrSeeder.cs` (new) — bootstrap data.
- `Views/Shared/{_Layout, _AuthLayout, _AdminLayout, _OwnerLayout, _EntryLayout, _PublicLayout}.cshtml`.
- `Controllers/{Account, Entry, Service, Pending, Owner, Admin, Help, Language}Controller.cs`.
- `tailwind.config.js` + `wwwroot/css/app.css` (new).

---

## Verification

Run in order. Each is observable.

1. `dotnet build` — succeeds.
2. Reset `hr_db` → `dotnet ef database update` — creates all 15 HR tables.
3. `dotnet run -- seed` → exits 0 + prints `SuperAdmin: admin@local / <pw>` and `Owner: hr@demo.local / <pw>`. In psql: `Organizations`=2, `Employees`≈24, `HrRoles`=3, `AdminRoles`=3, `AspNetUsers`≥26.
4. `npm install` + `npm run css:build` → produces `wwwroot/css/app.min.css`.
5. `dotnet run` → boots; `/` shows public landing on `_PublicLayout`.
6. **Auth UI** — visit `/Account/Login`. Sees the new styled card on `_AuthLayout` (navy/gold, Tajawal in AR by default, language switch in corner). Visit `/Account/Register`. Sees the employee-application form (NameAr/En, email, phone, org dropdown, password). Switch language → instant flip.
7. **SuperAdmin login** (`admin@local`) → lands on `/Admin` (`_AdminLayout`). Home shows counts. Owners list shows seeded Owner. AdminRoles list shows 3 seeded roles. Tickets list empty.
8. **Create new Admin** from `/Admin/Admins/Create` → assign `tickets-only` AdminRole during creation. Log out. Log in as new admin → `_AdminLayout` shows ONLY the Tickets nav item (Owners/Admins/AdminRoles hidden because no permission). Visiting `/Admin/Owners` returns 403.
9. **Create new Owner** as SuperAdmin from `/Admin/Owners/Create` → new user + Owner row created. Activity log row written.
10. **Public Employee registration** — log out. `/Account/Register` → fill form, select org "Al-Ofoq". Submit → user created, `EmployeeApplication` row inserted with `Status=Pending`. Lands on `/Pending` showing "Your application is under review".
11. **Owner approval** — log in as seeded Owner (`hr@demo.local`). Lands on `/Entry/Welcome` (Owner sees both seeded orgs). Walk entry. Then click "Pending Employees" in Owner nav (visible because Owner has `_system/approve-employees` implicitly). See the new application. Open it → select Org, Company, Branch + initial HrRole `hr-view-only`. Approve. `Employee` row created with `UserId` linked, application marked `Approved`.
12. **Newly approved employee login** — log in as the newly-registered employee. Lands on `/Entry/Welcome`. Skip-step logic kicks in: they only have 1 visible org → Org step skipped (stepper shows 3 steps: Company → Branch → Services). Reach Launcher. Only the tiles permitted by `hr-view-only` are interactive (others greyed or hidden — design choice flag if needed).
13. **HR Role CRUD** — back as Owner. `/Owner/HrRoles/Create` → name+permission grid → save. Edit one role → toggle permissions → save. Delete a non-system role.
14. **Role assignment** — `/Owner/Employees/{id}` for an employee → click "Assign role" → pick role + scope (Branch) + branch picker → save. Log in as that employee → confirm new permissions reflected at Launcher.
15. **Skip + 1-step entry** — assign a role to an employee at only one branch in one company in one org. Log in → 4-step entry collapses to 0 steps (all skipped) → goes straight to Launcher (or Dashboard).
16. **Help Center** — as any logged-in user, topbar Help → `/Help` → submit a ticket. As SuperAdmin → `/Admin/Tickets` → see the new ticket → reassign + change status.
17. **Login log** — `select email, success, "FailureReason" from "LoginLogs" order by id desc limit 10;` shows recent attempts (success + intentional fail).
18. **Language toggle** + **Scope jump** + **Tweaks panel** — same as prior plan: lang flip works, scope chip switch reloads with recolor, Tweaks accent change is instant + persists via cookie.
19. **Logout** + protected route — visit `/Entry/Launcher` while signed out → bounced to `/Account/Login?returnUrl=...`.

All 19 pass → foundation is solid. Each of the 28 services can now be tackled as `docs/features/<service-id>.md`.

---

## Risks / scope honesty

This foundation is large — realistic ~2–3 weeks of focused implementation. Two permission systems, two portals (Admin + Owner), an approval workflow, redesigned auth, plus the shared infrastructure. If you want a smaller first checkpoint, candidates to peel off into a Phase 1B:

- §17 Owner Portal HrRole CRUD + Assignments (push to a feature plan, rely on seeded roles only).
- §18 AdminRole CRUD + Admin Assignment (keep SuperAdmin as the only effective IT user for Phase 1A).
- §13 + §19 Help Center (defer the whole tickets feature).

Speak up if you want one of these peeled out. Otherwise the plan as written is the single foundation.

---

## Deferred (NOT part of this plan)

- All 28 business-service implementations — each becomes `docs/features/<service-id>.md`.
- `SupportTicketReply` entity + replies/attachments/notifications/SLA.
- Real email delivery for temp passwords (currently console).
- Activity log + Login log viewer UIs (only writes in foundation; no read screens).
- Identity-message localization (English Identity errors stay).
- Self-hosted fonts / Font Awesome.
- Per-user persistence of Tweaks (cookie only).
- DB-backed pinned quick-jump list (9 hardcoded).
- Forgot password / email confirmation flows.
- Postgres password in `appsettings.json` — committed plaintext, flagged but out of scope.

---

## Changes

### 2026-05-20 — Foundation implemented end-to-end

All 19 implementation todos completed. Plan followed as written with a small number of pragmatic adjustments noted below. Build green; all auth-protected routes redirect correctly; seeded DB has 1 Owner, 2 Orgs, 5 Companies, 9 Branches, 24 Employees, 3 HrRoles (202 permissions), 3 AdminRoles, 2 demo `EmployeeRoleAssignment`s.

**Adjustments from the plan:**
- **Postgres port**: changed from `5432` → `5433` (host) in `docker-compose.yml` and `appsettings.json` to avoid conflict with another local Postgres instance on the dev machine. Container still listens on `5432` internally.
- **Branch count**: prototype actually has 9 branches, not 10 as the plan estimated. Seeder ports all 9 verbatim.
- **Naming-collision aliases** required:
  - `using AP = Project_HR_1.Services.Permissions.AdminPermissions;` in `HrSeeder.cs` + `AdminController.cs` (namespace `Project_HR_1.Models.Hr.AdminPermissions` shadowed the static class of the same name).
  - `using OwnerEntity = Project_HR_1.Models.Hr.Owner;` in `Models/ViewModels/Admin/AdminViewModels.cs` (sibling `Project_HR_1.Models.ViewModels.Owner` namespace shadowed the entity).
- `AdminController.TicketStatus` action → renamed to `ChangeTicketStatus` because the original collided with the `TicketStatus` enum.
- **Tailwind class limitations**: replaced `focus:ring-primary/30` and `hover:bg-accent/20` with `focus:ring-accent` and `hover:bg-amber-100` — Tailwind v3 can't compute opacity on tokens defined via CSS vars.
- **HtmlHead partial**: factored shared `<head>` content (fonts, Font Awesome, `app.min.css`) into `Views/Shared/_HtmlHead.cshtml` to avoid duplicating across the six layouts.
- **Tweaks panel** (§14): cookie writes are client-side JS only. Reading the cookie server-side to pre-render the theme (avoiding FOUC) is a planned polish, not done in this foundation.

**Files added** (~70 total): all entities under `Models/Hr/*`; all services under `Services/{Permissions,Scope,I18n,Audit}/*`; `HrSeeder.cs`; one EF migration `20260520030103_AddHrCore`; six layouts + eleven partials under `Views/Shared/*`; ten view-model files; seven new controllers (`Entry`, `Pending`, `Service`, `Owner`, `Admin`, `Help`, `Language`); 19 Razor views across `Views/{Entry,Pending,Service,Owner,Admin,Help}/`; plus `package.json`, `tailwind.config.js`, `postcss.config.js`, `wwwroot/css/app.css`.

**Verification** (foundation smoke test):
- `dotnet build` → 0 errors, 0 warnings.
- `docker compose up -d db` (Postgres on 5433) → ready.
- `dotnet ef database update` → applied `AddHrCore`, created all 15 HR tables.
- `dotnet run -- seed` → exits 0, prints credentials, populates DB.
- `npm install` + `npm run css:build` → produces `wwwroot/css/app.min.css` (~22 KB).
- `dotnet run` → boots on port 5011.
- Public routes (`/`, `/Privacy`, `/Account/Login`, `/Account/Register`) → `200`, navy/gold styling, Tajawal font, RTL by default.
- Protected routes (`/Entry/Resume`, `/Admin`, `/Owner/Pending`, `/Help`, `/Service/payroll`) → `302` to `/Account/Login?ReturnUrl=...`.
- Compiled CSS at `/css/app.min.css` → `200`.

**Seeded credentials** (from `dotnet run -- seed`):
- SuperAdmin: `admin@local` / `Admin123`
- Demo Owner: `hr@demo.local` / `Owner123`
- 24 demo Employees: `<firstname.lastname>@demo.local` / `Demo123`. The first two employees got `hr-full-access` (Org scope) and `hr-manager` (Company scope) assignments.

**Next:** per-service feature plans, one per service id from the launcher (e.g. `docs/features/payroll.md`, `docs/features/leaves.md`). Use `TEMPLATE.md` as the starting point; the foundation plan above is the architectural reference they all sit on top of.

### 2026-09-28 — Fix: scope filter crashed at Branch/Company scope

`IScopeContext.Filter<T>()` used the explicit `IScoped.CompanyId/BranchId` members inside the LINQ query.
EF Core can only translate those when the entity has a real column with the same name, so it threw
`InvalidOperationException: The LINQ expression ... ((IScoped)b).BranchId ... could not be translated` for:
- `Branch` at Branch scope (its "BranchId" is really `Id`),
- `Company` at Company scope (its "CompanyId" is really `Id`),
- `Department` / `JobTitle` / `Sponsor` at Branch scope (they have no branch column).
Visible symptom: Employees list, Add Employee and Print Roster pages returned a 500 after choosing a branch
(or some companies) in the scope strip.
Fix in `Services/Scope/ScopeContext.cs`: the filter is now built per entity against its mapped column
(`BranchId`, or `Id` on `Branch`; `CompanyId`, or `Id` on `Company`). Company-level entities are filtered by
the branch's company when the scope is a single branch. Verified at Global, Company and Branch scope.
