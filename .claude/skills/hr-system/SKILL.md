---
name: hr-system
description: Guidance for porting the HR prototype (docs/design/source/) into the ASP.NET Core MVC app. Use when working on Owner/Organization/Company/Branch/Employee entities, the IScopeContext, the HrRole + AdminRole permission systems, the 28-service catalog, the entry flow, bilingual AR/EN UI, the Tweaks panel, ActivityLog/LoginLog, or SupportTickets.
---

> TODO: Arabic characters in the body below were mangled during paste (mojibake — e.g. `Ø¹Ø±Ø¨Ù` instead of `عربي`, `â` instead of `→`). Re-paste with proper UTF-8 when convenient.

# HR System â Bilingual, Multi-Scope Prototype

A production-ready HR management system prototype with Arabic/English bilingual support (RTL/LTR), multi-level organizational scope (Owner â Organization â Company â Branch), and 28 HR services.

## When to use this skill

Invoke this skill when the user asks to build, extend, or customize an HR / HRM / HRIS / workforce / payroll system, especially when:
- They need Arabic + English UI with proper RTL/LTR direction swap
- They have a multi-company / multi-branch structure (holding group, conglomerate)
- They want a comprehensive set of HR services (attendance, leaves, payroll, contracts, etc.)
- They mention Saudi Arabia / Gulf region context (residency tracking, Tajawal font, SAR currency)

## What's included

### Architecture
- **Entry flow:** 4-step welcome â Organization â Company â Branch â Services launcher
- **Multi-level scope:** at any step, an owner can stop and view aggregated data across that level (Owner view = all orgs, Org scope = all companies in org, Company scope = all branches in company, Branch scope = single branch)
- **App shell:** topbar (brand + workspace breadcrumb + language switch + servicesHub + date + notifications + user) + scope strip (purple/navy/gold/teal level chips) + secondary nav (breadcrumb + 9 quick-jump service chips + More) + page content area
- **No sidebar** â services accessed via launcher + quick-jump chips
- **Tweaks panel** (bottom-left FAB): 5 accent colors, 5 primary colors, density (comfortable/compact)

### 28 Services in 7 categories
- **Core:** Dashboard, Org Structure, Alerts, Tasks
- **Employees:** Employee DB, Add Employee, Attendance, Experience
- **Compensation:** Payroll, Allowances, Deductions, Bonuses, Raises, Salary Advance, Expenses
- **Time & Leave:** Leaves, Leave Tickets, Delegations
- **Performance:** Evaluation, Training
- **Compliance:** Contracts, Documents, Violations, Warnings
- **Service Lifecycle:** Custody, Transfers, Resignations, End of Service

### i18n
- Single source of truth: `HR.dict` in `assets/i18n.js`
- Lookup helpers: `HR.t(key)` for dictionary keys, `HR.L(ar, en)` for inline pairs
- Switching language updates `<html lang dir>`, fonts (Tajawal â Inter), arrow direction, sidebar/margin sides, badge alignment

### Visual system
- Navy primary (#1a2744) + gold accent (#c8a84b) enterprise aesthetic
- Tajawal (AR) / Inter (EN) typography
- Font Awesome 6.5 icons
- Stat cards with colored left rail, badges, tables with sticky navy headers
- Status badge system (success/danger/warning/info/gold/muted with optional dot)

## File structure

```
HR Prototype.html         # Entry point â minimal HTML shell
assets/
âââ i18n.js               # HR.dict + HR.t() + HR.L() + HR.setLang()
âââ data.js               # Orgs, companies, branches, employees, all service datasets
âââ services.js           # Service catalog (id, ar, en, icon, color, category)
âââ router.js             # State, render dispatch, entry screens, app shell, scope helpers
âââ screens.js            # Custom screens: dashboard, employees, attendance, leaves, payroll, tasks, alerts, org-structure, add-employee
âââ tableConfig.js        # Generic table screens for the other 18 services
âââ styles.css            # All styles including RTL/LTR mirroring rules
âââ tweaks.js             # Tweaks panel: accent + primary swatches, density toggle
```

## How to extend

### Add a new service
1. Append to `HR.services` array in `assets/services.js`: `{ id, ar, en, icon, color, cat }`
2. Add sample data to `assets/data.js`
3. If it's a standard table screen â add an entry to `HR.tableConfig` in `assets/tableConfig.js`
4. If it needs a custom layout â add `HR.screens.<id> = function(svc) { ... }` in `assets/screens.js`
5. Add translation keys for any new strings to `HR.dict` in `assets/i18n.js`

### Add a new translation
```js
// In HR.dict:
myKey: ['ÙØµ Ø¹Ø±Ø¨Ù', 'English text'],

// In templates:
${HR.t('myKey')}            // dictionary lookup
${HR.L(ar, en)}             // inline pair
${HR.L(item.field_ar, item.field_en)}  // bilingual data
```

### Pin a service to the quick-jump chips
Add its id to the `pinned` array in `HR.renderApp` in `assets/router.js`.

## Customization patterns

- **Color palette:** edit `:root` vars in `assets/styles.css` (`--primary`, `--accent` + light/deep variants); or expose new options in the Tweaks panel
- **Brand name:** edit `brand` key in `HR.dict`
- **Real org data:** replace the `HR.orgs`, `HR.companies`, `HR.branches` arrays in `assets/data.js`
- **More employees:** extend `empPool` array; the system auto-generates contracts, salaries, etc.
- **Currency:** edit `_money` / `money` helpers in `screens.js` and `tableConfig.js`

## Caveats

- All data is mocked client-side â no API integration
- Add Employee form does not yet have full English translation for field labels (only headers + sections)
- Some inline notes/details in activity feeds and alert rows remain in Arabic (data, not UI)
- Approve/Reject buttons trigger toast feedback only â no real backend
- Scope filtering uses `HR.scoped()` which checks employee membership; if you add datasets that don't reference an employee, they need explicit scope handling

## Default theme

- Primary: `#1a2744` (deep navy)
- Accent: `#c8a84b` (classic gold)
- Bg: `#f0f2f7`
- Text: `#1a2744` / muted `#7f8c9a`
- Radius: 16px / 10px
- Default language: Arabic (RTL)
