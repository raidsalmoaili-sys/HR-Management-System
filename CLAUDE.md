# Project_HR_1

ASP.NET Core 10 MVC app. EF Core + PostgreSQL. ASP.NET Core Identity for auth. Views styled with Tailwind CSS.

## Feature plan workflow (IMPORTANT)

Every feature in this project has a living plan document in [docs/features/](docs/features/). Treat these files as the source of truth for *what* a feature does and *why*.

### When the user asks for a NEW feature

1. Pick a short kebab-case name for the feature (e.g. `employee-leave-requests`).
2. Check [docs/features/](docs/features/) first — if a file with that name already exists, this is NOT a new feature, follow the "existing feature" flow below.
3. Create `docs/features/<feature-name>.md` by copying [docs/features/TEMPLATE.md](docs/features/TEMPLATE.md).
4. Fill in the plan WITH the user before writing any code. Confirm the plan, then implement.
5. As you implement, keep the plan file in sync (update the status, mark steps done, note any decisions that changed).

### When the user asks to UPDATE or EXTEND an existing feature

1. Read the existing `docs/features/<feature-name>.md` file FIRST — do not start from scratch.
2. Add the new work as a new dated section at the bottom of the same file (under `## Changes`). Do not delete old context — it is history.
3. Update the "Status" and "Current behavior" sections at the top so they reflect the new state after the change.

### When unsure which feature a request belongs to

List the files in `docs/features/` and ask the user which one this work belongs to, or whether it should be a new feature file.

### Rules for plan files

- One feature = one file. Keep names stable; do not rename.
- Plans are documentation, not throwaway notes. Write them so the user can re-read them weeks later and remember the decisions.
- Link related features with `[other-feature](other-feature.md)` when they touch each other.

## Project structure

- [Controllers/](Controllers/) — MVC controllers
- [Models/](Models/) — EF entities, view models, `ApplicationUser`, `AppDbContext`
- [Models/Hr/](Models/Hr/) — HR domain entities (Owner, Organization, Company, Branch, Employee, EmployeeApplication, ServiceCatalog) and seeder
- [Models/Hr/HrPermissions/](Models/Hr/HrPermissions/) — `HrRole`, `HrRolePermission`, `EmployeeRoleAssignment`
- [Models/Hr/AdminPermissions/](Models/Hr/AdminPermissions/) — `AdminRole`, `AdminRolePermission`, `AdminRoleAssignment`
- [Models/Hr/Audit/](Models/Hr/Audit/) — `LoginLog`, `ActivityLog`
- [Models/Hr/Support/](Models/Hr/Support/) — `SupportTicket`
- [Services/Permissions/](Services/Permissions/) — `IPermissionService` (HR) + `IAdminPermissionService` (IT)
- [Services/Scope/](Services/Scope/) — `IScopeContext`, `HrScope`, cookie middleware
- [Services/I18n/](Services/I18n/) — `IHrLocalizer`, `HrDict`
- [Services/Audit/](Services/Audit/) — `IActivityLogger`
- [Views/](Views/) — Razor views, Tailwind classes
- [Migrations/](Migrations/) — EF Core migrations (PostgreSQL)
- [Program.cs](Program.cs) — app startup, DI, Identity, DbContext, middleware order, `seed` arg
- [docs/features/](docs/features/) — feature plan documents (see workflow above)
- [.claude/skills/hr-system/](.claude/skills/hr-system/) — high-level skill for porting the prototype
- [docs/design/source/](docs/design/source/) — the HTML/JS prototype being ported

## HR system architecture

- **Identity**: `ApplicationUser : IdentityUser` (no business FKs). Employee↔User link is on `Employee.UserId`; Owner↔User link on `Owner.UserId`. All HR relationships connect to `Employee`, never the user row.
- **User types**: IdentityRoles `SuperAdmin` and `Admin` for IT staff. Owner identified by row in `Owners` table with matching `UserId`; Employee by row in `Employees`; pending applicant by row in `EmployeeApplications`. Use `IPermissionService.ResolveAsync(user)` to get the `UserKind`.
- **Two parallel permission systems**:
  - `IPermissionService` — HR side. Consults `EmployeeRoleAssignment` + `HrRolePermission`. Permissions stored as `(serviceId, permission)` pairs where `serviceId` is a kebab-case business service id OR the sentinel `"_system"` for system capabilities (`manage-hr-roles`, `assign-hr-roles`, `approve-employees`, `view-employees`).
  - `IAdminPermissionService` — IT-portal side. Consults `AdminRoleAssignment` + `AdminRolePermission`. Permission strings are constants in `Services/Permissions/AdminPermissions.cs`.
- **Scope**: `IScopeContext` is read-only "what scope am I in" (Global/Org/Company/Branch). `ScopeCookieMiddleware` reads/writes the `hr_scope` cookie. Per-service entities opt into `IScoped` to be filtered by `Scope.Filter<T>(...)`.
- **i18n**: `IHrLocalizer` exposes `L.T(key)` (dict lookup) and `L.L(ar, en)` (inline pair). Dictionary in `Services/I18n/HrDict.cs`. Cookie `hr_lang` (default `"ar"`). `<html lang dir>` set per request in layouts.
- **Service catalog**: `Models/Hr/ServiceCatalog.cs` — 28 business services + 3 synthetic `settings` services (`settings-hr-roles`, `settings-role-assignments`, `settings-pending-employees`). Single source of truth for ids/icons/colors/categories.
- **Public registration is Employee-only**: `/Account/Register` creates an `EmployeeApplication` (Status=Pending). Owners review via Owner Portal and approve, picking Org/Company/Branch + initial HrRole. SuperAdmin is created by the seeder; Admin and Owner accounts are created from the Admin Portal.
- **Layouts**: pick the right one per view —
  - `_Layout` — full HR shell (topbar + scope-strip + svc-nav + page area). Used inside the app.
  - `_EntryLayout` — entry stepper. Used for Welcome/Org/Company/Branch steps.
  - `_AuthLayout` — centered card on bg, lang switcher in corner. Login/Register/Pending.
  - `_AdminLayout` — IT portal shell (admin topbar + admin nav). Used by Admin Portal.
  - `_OwnerLayout` — HR shell + Owner-only nav (Pending / HR Roles / Employees). Used by Owner Portal.
  - `_PublicLayout` — minimal navbar for public landing (Home/Privacy).
- **Tailwind**: compiled via npm. Source `wwwroot/css/app.css`, output `wwwroot/css/app.min.css` (gitignored). Tokens are CSS vars on `:root` so the Tweaks panel can mutate them at runtime.
- **Seed**: manual only — `dotnet run -- seed`. Never auto-runs from `Migrate()`. Destructive: wipes every HR table and recreates FICTIONAL demo data (group "مجموعة الأفق القابضة": 3 companies, 7 branches, ~72 employees, attendance, leaves, advances, contracts, payroll). See [hr-core-modules](docs/features/hr-core-modules.md). Credentials:
  - SuperAdmin: `admin@local` / `Admin123`
  - HR manager (hr-manager role, Global scope): `hr@demo.local` / `Demo1234`
  - Branch viewer (hr-view-only, one branch): `viewer@demo.local` / `Demo1234`
- **Portfolio edition**: this copy contains NO real client data. Never add real names, IDs, spreadsheets or company names — demo data lives only in `HrSeeder`.
- **Demo database**: this copy (Project_HR_Note) uses its OWN local database `hr_demo` (real connection string in git-ignored `appsettings.Local.json`; `appsettings.json` holds a placeholder; no user-secrets) so seeding never touches the production project.
- **Database**: Postgres in `docker-compose.yml`. Maps host port `5433` → container `5432` to avoid conflict with other local Postgres instances. Start with `docker compose up -d db`.
- **Tweaks cookie**: `hr_tweaks` JSON `{accent, primary, density}`. Client JS writes it; the layout reads it server-side and emits `style="--hr-accent: ..."` on `<html>` to avoid FOUC. (Currently only the client JS path; reading on the server is a future polish.)

## Common commands

- Start Postgres: `docker compose up -d db` (port 5433 on host)
- Build: `dotnet build`
- Run: `dotnet run`
- Add migration: `dotnet ef migrations add <Name>`
- Apply migrations: `dotnet ef database update`
- Manual seed: `dotnet run -- seed`
- Tailwind dev watch: `npm run css:dev`
- Tailwind production build: `npm run css:build`
- First-time npm install: `npm install`
