# Epic 13 Cutover Runbook

## Purpose
This runbook covers migration, compatibility, and cutover from the legacy mixed architecture to the simplified OAuth2-first API, display registration, and service-local reporting model.

## Pre-cutover checklist
1. Back up the SQL Server database that hosts `ApiClients` and `SourceSystems`.
2. Back up the service local SQLite database and any print output directories.
3. Export kiosk bootstrap settings for any field devices that still rely on local preferences only.
4. Verify every API client can request an OAuth2 client credentials token.
5. Review `GET /api/cutover/summary` and confirm each client has the expected device/display/global settings.

## Migration order
1. **API schema normalization**
   - Start the API once with the Epic 13 patch.
   - The API normalizes JSON columns for global settings, device settings, and display registrations at startup.
2. **Client secret compatibility**
   - Existing plaintext client secrets from earlier builds are migrated to the hashed secret fields during startup.
3. **Kiosk compatibility migration**
   - For each kiosk-backed client, call `POST /api/clients/{id}/migrate/legacy-kiosk` with the old bootstrap values.
   - This writes device settings plus display registration rows and can optionally promote the legacy startup URL into global settings.
4. **Service compatibility migration**
   - For each service-backed client, call `POST /api/clients/{id}/migrate/legacy-service` with the prior worker configuration.
   - This stores service runtime metadata under `globalSettings.service`.
5. **Service deployment**
   - Deploy the updated Windows service.
   - The service now accepts both the new `Service` section and common legacy sections such as `IarApi`, `Polling`, `Printing`, and `Storage`.
6. **Kiosk deployment**
   - Deploy the updated kiosk bootstrap UI.
   - The kiosk mirrors critical bootstrap values to `bootstrap-compat.json` so existing preference-based installs can survive the transition.
7. **Admin verification**
   - Use BTSS.IAR to verify client settings, device registrations, command dispatch, and reporting from the service-local database.
8. **Retired table drop**
   - Review `GET /api/cutover/summary`.
   - When safe, call `POST /api/cutover/apply` with `{ "dropRetiredTables": true, "force": true }`.

## Rollback
1. Stop the API, kiosk, and service components.
2. Restore the SQL Server backup taken before migration.
3. Restore the service SQLite database and print job directories if operational data was generated after cutover.
4. Reinstall the prior kiosk/service binaries.
5. Reapply the previous appsettings files.
6. Bring the legacy stack online, then validate token issuance or prior auth behavior as appropriate for the rollback target build.

## Notes
- The retired table drop is intentionally explicit and not automatic.
- Sample payload verification lives in `BTSS.IntegrationTests`.
- The API cutover summary endpoint is intended to be used before any destructive change.
