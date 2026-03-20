# RfkitEmulator (Phase 3)

Standalone **RFKIT REST** test double for **`RFKitAmpTuner`** development.

**QA:** For **Phase 8** (emulator + PgTgBridge) procedures and sign-off, see **[`../RFKitAmpTuner/Docs/QA_TEST_PLAN.md`](../RFKitAmpTuner/Docs/QA_TEST_PLAN.md)** and **[`../RFKitAmpTuner/Docs/TESTING_GUIDE.md`](../RFKitAmpTuner/Docs/TESTING_GUIDE.md)**. Implements vendor **OpenAPI 0.9.0** routes with **stateful** JSON derived from `Responses.json` plus **ASP.NET Core HTTP logging** (`AddHttpLogging` / `UseHttpLogging`) and structured **`ILogger`** events for state changes.

- **Not** a substitute for real RF hardware (no RF, no safety interlocks).

## Logging (standard infrastructure)

| Mechanism | Purpose |
|-----------|---------|
| **`Microsoft.AspNetCore.HttpLogging`** | Logs method, path, query, **request/response bodies** (size limits), status, duration. Category: `Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware`. Configure levels in `appsettings.json`. |
| **`ILogger<EmulatorStateStore>`** | Structured **business** logs when operate mode, active antenna, or operational interface changes, and when `POST /error/reset` runs. Category: `RfkitEmulator.EmulatorStateStore`. |

Request **body buffering** is enabled at the start of the pipeline so **HttpLogging** and PUT handlers can both read the body.

Limits (bytes) are in `appsettings.json` under **`RfkitEmulator:HttpLoggingRequestBodyLimit`** and **`HttpLoggingResponseBodyLimit`** (default 4096).

## Stateful behavior (Phase 3)

| Area | Behavior |
|------|----------|
| **GET `/power`** | **STANDBY:** forward/reflected/SWR/current zeroed (baseline temperature/voltage kept). **OPERATE:** baseline from `Responses.json`. If tuner `mode` is **AUTO_TUNING** (edit `Responses.json` to test), reflected/SWR show a non-idle hint. |
| **PUT `/operate-mode`** | Updates mode; **STANDBY** resets tuner to `AUTO` + `BYPASS` (then **OPERATE** restores tuner from `Responses.json`). |
| **PUT `/antennas/active`** | Updates active antenna; **GET `/antennas`** marks matching entry **ACTIVE**, internal slot **3** stays **DISABLED** when not active (matches seed file). |
| **PUT `/operational-interface`** | Echoes valid body; persisted for GET. |
| **POST `/error/reset`** | Clears `data.status` string. |

## Run

From repository root:

```powershell
dotnet run --project RfkitEmulator\RfkitEmulator.csproj
```

Default listen: **`http://0.0.0.0:8080`**. Override:

```powershell
$env:ASPNETCORE_URLS = 'http://0.0.0.0:9090'
dotnet run --project RfkitEmulator\RfkitEmulator.csproj
```

Quick check:

```powershell
curl.exe -s http://localhost:8080/info
```

**Windows PowerShell:** avoid `curl -d '{"operate_mode":"STANDBY"}'` (quoting breaks JSON). Use a file:

```powershell
Set-Content -Path $env:TEMP\op.json -Value '{"operate_mode":"STANDBY"}' -Encoding utf8
curl.exe -X PUT http://127.0.0.1:8080/operate-mode -H "Content-Type: application/json" --data-binary "@$env:TEMP\op.json"
```

## Routes

Same surface as Phase 2; see **`../RFKitAmpTuner/Docs/api/swagger.json`**. **GET-only** on `/tuner` per OpenAPI 0.9.0.

## Build

```powershell
dotnet build RfkitEmulator\RfkitEmulator.csproj -c Release
```

From repository root you can also run: `dotnet build RFKitAmpTuner.sln -c Release`.

Output: `RfkitEmulator/bin/Debug/net10.0/` or `Release`.

### MSB3026 — cannot copy `RfkitEmulator.exe` (file in use)

The **Release** (or **Debug**) **`RfkitEmulator.exe`** is locked while the app is running. **Stop** the running emulator (**Ctrl+C** in its terminal, or Task Manager, or `Stop-Process -Name RfkitEmulator -Force`), then build again.
