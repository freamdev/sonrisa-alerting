# Alerts frontend

Angular 22 standalone UI for login, independent personal choices and administration.

Use Node 24.15+ within major 24. From this directory:

```powershell
npm.cmd ci
npm.cmd start
npm.cmd test
npm.cmd run build
npm.cmd run format:check
```

The dev proxy expects the backend at http://localhost:5165. The browser opens at http://localhost:4200. Root Compose also serves the built frontend at port 4200, with same-origin API proxying; avoid running both on that port simultaneously.

See [Phase 08 setup, security contract and manual acceptance](../docs/phase08-angular.md). Phase 08 awaits human validation. No Phase 09 work is included.
