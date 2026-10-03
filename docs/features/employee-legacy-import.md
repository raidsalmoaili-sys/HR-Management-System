# Employee legacy import — REMOVED

**Status:** Removed (portfolio edition, 2026-10-03)

This one-off importer loaded a client's real employee spreadsheets into the database.
It was removed together with the `import-legacy-employees`, `rollback-legacy-import`,
`inspect-hr-data` and `wipe-companies-branches` CLI commands, and the source spreadsheets,
because this copy of the project is a public demo that only contains fictional seed data.

Demo data now comes from `HrSeeder` (`dotnet run -- seed`) — see [hr-core-modules](hr-core-modules.md).
The reusable Excel import from the Employees page is unaffected — see [employee-sheet-import](employee-sheet-import.md).
