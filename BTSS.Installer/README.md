# BTSS.Installer

Windows installer/bootstrapper for the BTSS solution.

## What it does

- Lets the operator choose any combination of:
  - **Display/Admin** (`BTSS.IAR`)
  - **Kiosk** (`BTSS.IAR.Kiosk`)
  - **Service** (`BTSS.Service`)
- Collects the required OAuth/API settings once.
- Reads a more permanent machine identifier from `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`.
- Detects connected displays with `Screen.AllScreens`.
- Registers the device and detected displays back to the server using the existing `POST /api/me/devices/register` endpoint.
- Writes local configuration files for the Display/Admin app and the Windows service.
- Installs/updates the Windows service as **LocalSystem** using `sc.exe`.

## Packaging expectation

The installer expects published application payload folders to sit next to the installer executable, or under a `payload` subfolder:

- `BTSS.IAR`
- `BTSS.IAR.Kiosk`
- `BTSS.Service`

Example:

```text
BTSS.Installer.exe
payload/
  BTSS.IAR/
  BTSS.IAR.Kiosk/
  BTSS.Service/
```

## Notes

- The kiosk project currently loads `bootstrap-compat.json` from its app data directory. This installer writes that file to:
  - `%LOCALAPPDATA%\BTSS.IAR.Kiosk\bootstrap-compat.json`
  - `<InstallRoot>\Kiosk\config\bootstrap-compat.json`
- The server-side registration is intentionally routed through the existing device registration endpoint instead of introducing a new API table.
- The installer must be run elevated when installing the Windows service.
