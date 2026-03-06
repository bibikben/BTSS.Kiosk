# Korzh EasyQuery + EasyReport integration scaffold (BTSS)

This patch adds:

## BTSS.IAR.Api
- Enables Razor Pages + static files
- Adds two placeholder pages:
  - `/korzh/query` (EasyQuery UI host)
  - `/korzh/reports` (EasyReport UI host)

These pages are intentionally placeholders so the solution compiles without Korzh dependencies.

### To complete Korzh integration
1. Add your Korzh EasyQuery packages to `BTSS.IAR.Api`.
2. Copy the EasyQuery UI (from Korzh sample) into `Pages/Korzh/Query.cshtml` and add the required JS/CSS (wwwroot).
3. Add your EasyReport Starter Kit (or packages) and replace `Pages/Korzh/Reports.cshtml`.
4. Point both at the same SQL DB used by `AppDbContext`.

## BTSS.IAR.Kiosk
- Adds two MAUI pages with WebView:
  - `KorzhQueryPage` -> `{IarApiBaseUrl}/korzh/query`
  - `KorzhReportsPage` -> `{IarApiBaseUrl}/korzh/reports`
- Adds buttons on the Admin page: **Query** and **Reports**

### Notes
- The WebView uses `AppSettings.IarApiBaseUrl` (default `http://localhost:5080`).
- If you host Korzh pages on a different base URL, set it in Admin -> Email tab -> IAR API base url.
