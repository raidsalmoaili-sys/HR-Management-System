# HR core modules — attendance, leaves, payroll, contracts, advances, dashboard

**Status:** Done (portfolio edition, 2026-10-03)

## Goal
Turn the six most-used services of the launcher from "coming soon" placeholders into working modules built on
the existing employee database, and replace the real client data with a fictional seed so the project can be
shown publicly. The other services stay visible in the launcher as placeholders ("قيد التطوير").

## Current behavior

| Service id | Controller | What it does |
|---|---|---|
| `dashboard`  | `HrDashboardController`  | KPIs (headcount, Saudization, attendance today, payroll cost), 7-day attendance, payroll trend, breakdowns by company/nationality/department, leave usage, expiry alerts (iqama / ID / passport / contracts ≤ 60 days) |
| `attendance` | `AttendanceController`   | Daily sheet per date/branch (status, check-in/out, notes; late = after 08:00 + 15 min grace; approved leave and weekends pre-filled) + monthly matrix with counts and attendance rate |
| `leaves`     | `LeavesController`       | Requests (9 Saudi leave types), approve / reject / cancel, annual balance deducted on approval and restored on cancel, overlap check, employee status → `leave` while on leave |
| `advances`   | `AdvancesController`     | Salary advances (max 3× basic, one open advance per employee), monthly installment, approve / reject; repaid automatically by payroll approval, closed when fully repaid |
| `contracts`  | `ContractsController`    | Fixed-term / open-ended contracts, auto number `CT-{year}-0001`, renew (old one → `renewed`, no probation), terminate, printable Arabic A4 contract, optional sync of salary/allowances/dates back to the employee file |
| `payroll`    | `PayrollController`      | Monthly run per company: draft → approved → paid. Bonuses / manual deductions on drafts, recalculate, printable payslips, bank CSV (UTF-8 BOM) |

`ServiceController.Show` redirects these six ids to their controllers; every other id still renders `Placeholder`.
All module controllers inherit `HrModuleControllerBase` (permission check per service id, scope filtering, flash messages).

### Payroll rules (`Services/HrModules/PayrollCalculator.cs`)
- Daily rate = (basic + housing + transport + other) ÷ 30.
- Absence deduction = daily rate × (absent days from attendance + approved unpaid-leave days in the month).
- GOSI (Saudis only) = 9.75% × min(basic + housing, 45,000).
- Advance installment = min(monthly installment, remaining) for approved advances whose start month ≤ run month.
- Joiners / leavers inside the month are pro-rated on a 30-day basis.
- Weekend = Friday + Saturday.

### Data model (`Models/Hr/Modules/HrModules.cs`)
`AttendanceRecord` (unique employee+date), `LeaveRequest`, `SalaryAdvance`, `EmploymentContract` (unique number),
`PayrollRun` (unique company+year+month) and `PayrollItem` (unique run+employee). All carry CompanyId/BranchId
and implement `IScoped`, so `Scope.Filter()` works without joins.

### UI
Views under `Views/{HrDashboard,Attendance,Leaves,Advances,Contracts,Payroll}`. Styling uses the existing component
classes plus `wwwroot/css/hr-modules.css` (plain CSS, prefixed `hm-`, so it works before a Tailwind rebuild).
Flash messages: `Views/Shared/_ModuleFlash.cshtml`. Contract and payslip print pages use `Layout = null`.

## Demo data (`Models/Hr/HrSeeder.cs`)
Fictional group "مجموعة الأفق القابضة": شركة الأفق للتجارة (3 branches), شركة الأفق للخدمات الطبية (2), مؤسسة الواحة للمقاولات (2);
departments, job titles, sponsors, ~72 employees (Saudis + 8 expat nationalities) with random ID/IBAN numbers,
contracts (some expiring soon), 2 months of attendance, leave requests in every state, advances, and three
closed payroll months + one draft. Deterministic (`Random(42)`), dates relative to today.

## Setup after pulling
```
dotnet ef migrations add AddHrModules
dotnet ef database update
dotnet run -- seed
npm run css:build
```

## Changes
- 2026-10-03 — Initial version. Removed the legacy-import CLI commands and the real-data spreadsheets; anonymized
  `CompanyNames.cs`; fixed the seed wipe order (FK violations on Departments/Sponsors/Documents); computed the
  launcher's "attendance today" and "active leaves" tiles.
