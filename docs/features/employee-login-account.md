# Employee login account (for existing employees)

## Goal
A login could only be created when adding a new employee (email + initial password) or via Excel import.
Employees added without one had no way to get a login. The profile page now lets HR create one, and reset its password.

## Where
- `Views/Employees/_LoginAccount.cshtml` — card on `Employees/Detail` (under the status card) + two dialogs.
  - No login → pill «لا يوجد حساب دخول» + button «إنشاء حساب دخول» (email prefilled from the employee, password with show/hide).
  - Has login → pill «لديه حساب» + the login email + button «تغيير كلمة المرور».
  - On a validation error the dialog re-opens (`TempData["reopenLogin"]` / `["reopenPassword"]`).
- `EmployeesController.CreateLogin(id, email, password)` — creates the Identity user in one transaction with linking `Employee.UserId` and `Employee.Email`. Rejects: employee already has a login, email used by another user/employee, Identity password rules (8+, digit, uppercase — translated via `TranslateIdentityError`).
- `EmployeesController.ResetLoginPassword(id, password)` — reset token + `ResetPasswordAsync`. Only an Admin/SuperAdmin may reset an Admin/SuperAdmin user's password.
- Both require `add-employee` × Edit in the current scope and write to the activity log (`CreateLogin`, `ResetPassword`).
- `EmployeeDetailVM` gained `LoginEmail` and `CanAssignRoles`; the roles section title is now «الأدوار والصلاحيات» with an «إسناد دور» link to RoleAssignments when the employee has a login.

## Permissions note
A login alone grants nothing: the employee sees nothing until a role is assigned (إسناد الأدوار).
The seeded «صلاحية كاملة» role covers business services + `_system` permissions only — System-category
services (companies, branches, job titles, departments, sponsors, tickets, logs, HR roles, role assignments)
must be ticked in the role editor to match the Admin account.

## Changes
- 2026-09-30 — created.
