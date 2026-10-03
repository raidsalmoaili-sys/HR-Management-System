# <Feature name>

**Status:** planning | in-progress | done
**Owner:** <your name>
**Created:** YYYY-MM-DD

## Goal

One or two sentences. What is this feature for? Who uses it?

## Current behavior

What does the app do today, in the area this feature touches? (For a brand-new feature, write "Does not exist yet.")

## Desired behavior

What should the app do after this feature is built? Describe it from the user's point of view, step by step.

## Out of scope

Things this feature does NOT do. Write these down so future-you doesn't get confused.

## Design decisions

Short bullet list of choices you made and *why*. Examples:
- Store leave balance on the `Employee` entity (not a separate table) — only one balance per employee, no history needed yet.
- Use a server-rendered Razor form (not AJAX) — keeps it consistent with the existing auth pages.

## Data model changes

List entities, properties, migrations. Example:
- New entity `LeaveRequest` with `Id`, `EmployeeId`, `StartDate`, `EndDate`, `Status`.
- Migration: `AddLeaveRequests`.

## Files to add or change

- `Controllers/<Name>Controller.cs` — new
- `Models/<Name>.cs` — new
- `Views/<Name>/Index.cshtml` — new
- `Models/ApplicationDbContext.cs` — add `DbSet<...>`

## Implementation steps

1. ...
2. ...
3. ...

## Open questions

- ?

---

## Changes

Add a new dated section here each time the feature is updated or extended. Do not rewrite history above — append below.

### YYYY-MM-DD — <short title of change>

What changed and why.
