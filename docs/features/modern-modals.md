# Modern modals (all dialogs)

## Goal
Every popup dialog in the system (`<dialog class="modal">`) uses the same modern look as the
«إنشاء حساب دخول» dialog: navy/gold header, soft rounded inputs, clean footer.

## How
- New `wwwroot/css/modal-modern.css`, linked in `Views/Shared/_HtmlHead.cshtml` right after `app.min.css`.
  Plain CSS (no Tailwind rebuild needed). It restyles `dialog.modal`, `.modal-header/.modal-title/.modal-close`,
  `.modal-body` inputs (`.auth-input`, `.select-box`, text/date/number inputs, selects, textareas), and `.modal-footer` buttons.
  Empty validation summaries (`.validation-summary-valid`) are hidden inside dialogs.
- Titles now carry an icon tile + a small gold "section" line:
  `<span class="modal-title"><span class="mm-ico"><i class="fa-solid …"></i></span><span class="mm-txt"><small>Section</small>Title</span></span>`
  Applied in: Companies, Branches, Departments, JobTitles, Sponsors, HrRoles, RoleAssignments (create + edit),
  Employees Index (add), Detail (transfer, edit basics), `_StatusModal`.
- `_LoginAccount.cshtml` keeps its own `la-` styles (hero with the employee avatar).

## Adding a new dialog
Use `<dialog class="modal">` (or `modal wide`) with the header/body/footer classes and the `mm-ico`/`mm-txt` title markup — the look is automatic.

## Changes
- 2026-09-30 — created.
