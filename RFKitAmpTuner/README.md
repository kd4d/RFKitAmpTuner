# RFKIT RF2K-S Amplifier+Tuner Plugin (PgTgBridge)

Third-party PgTgBridge plugin for the **RFKIT** integrated amplifier and antenna tuner (**RF2K-S**), following the **KD4Z SampleAmpTuner** pattern (`CommandQueue`, `ResponseParser`, `StatusTracker`). Upstream reference: **[`ATTRIBUTION.md`](../ATTRIBUTION.md)** (pinned **KD4Z** commit).

| | |
|---|---|
| **Plugin ID** | `rfpower.rfkit-amplifier-tuner` |
| **Manufacturer** | RF-POWER ([rf-power.eu](https://rf-power.eu)) |
| **Capability** | `AmplifierAndTuner` |

## Status

- **Transport today:** TCP or serial with the same fictitious `$KEY value;` framing as the KD4Z sample (for bring-up and non-HTTP testing).
- **Phase 4 (HTTP):** TCP + **`UseRfkitRestApi`** (default **true**) uses **`RfkitHttpConnection`** and RFKIT REST at **`http://{IpAddress}:{Port}/`**. See **`Docs/INSTALLATION_GUIDE.md`** and **`Docs/TESTING_GUIDE.md`** (living documents; update as we proceed).
- **Phase 6:** **`RfkitCommandMapper`** + **`RfkitCatFromJson`** (JSON → synthetic CAT); covered by **`RFKitAmpTuner.Tests`** with golden JSON. From repo root: `dotnet test RFKitAmpTuner.Tests/RFKitAmpTuner.Tests.csproj`.
- **Phases 8–10 (testing / production handoff):** Use **`Docs/QA_TEST_PLAN.md`** for emulator QA (Phase 8), hardware + radio (Phase 9), and release sign-off (Phase 10). Operators: **`Docs/USER_GUIDE.md`** and **`Docs/QUICK_START.md`**.
- Further detail: [RFKIT REST API v0.9.0](https://rf-power.eu/wp-content/uploads/2024/12/RFKIT_api_doc_0_9_0.html); **`Docs/RFKIT_Option1_Integration_And_Test_Plan.md`** (roadmap, mapping, PTT/init decisions).

## Build

1. Install **PgTgBridge** so `PgTg.dll`, `PgTg.Common.dll`, and `PgTg.Helpers.dll` resolve (see `.csproj` `HintPath`s).
2. From the repository root:

```powershell
dotnet build RFKitAmpTuner\RFKitAmpTuner.csproj -c Release
dotnet test RFKitAmpTuner.Tests\RFKitAmpTuner.Tests.csproj -c Release
```

Or build the whole solution from the repository root: `dotnet build RFKitAmpTuner.sln -c Release`.

3. Deploy the DLL and enable the plugin — see **`Docs/INSTALLATION_GUIDE.md`** (includes elevated copy / `scripts/Deploy-ToPgTgBridge.ps1`).

## Configuration defaults

- **Phase 4 (HTTP):** With **TCP** selected in Plugin Manager (default), **`UseRfkitRestApi`** is **`true`**: the plugin uses **RFKIT REST** at **`http://{IpAddress}:{Port}/`**. Set **`UseRfkitRestApi = false`** only for raw CAT-over-TCP testing. Optional **`HttpBaseUrl`** (non-empty) overrides the derived URL (e.g. `http://192.168.1.5:8080`).
- **Startup HTTP capture (debug):** **`RfkitStartupCaptureSeconds`** = seconds to capture after connection start (**default `60`**; **`0`** = off; e.g. **`600`** = 10 min; max **7200**). Logs REST + CAT to **`%ProgramData%\PgTg\RfKitAmpTuner\rfkit-http-capture-*.log`**. Bodies truncated per **`RfkitHttpTrafficMaxBodyChars`** (default **8192**). See **`Docs/INSTALLATION_GUIDE.md`** § **5.4.1** and **`Docs/TESTING_GUIDE.md`** § **B2.1**.
- **REST transport:** **`ReconnectDelayMs`** (Plugin Manager) controls delay between reconnect attempts after **`GET /info`** failure; **`RfkitHttpHeartbeatIntervalMs`** / **`RfkitHttpRequestTimeoutSeconds`** are in **`Constants.cs`**. **`SetFrequencyKhz`** maps to a synthetic **`$FRQ nnnnn;`** echo (no device PUT until the API requires it).
- Default **TCP** fields: `127.0.0.1:8080` (matches vendor OpenAPI default and **RfkitEmulator**).
- **`DeviceInitializationEnabled`** defaults to **`false`** in `MyModel/Internal/Constants.cs` (compile-time; rebuild to change). See the integration plan § 2.

## PTT

Amplifier **PTT may be hardware-only**; the host still calls `SendPriorityCommand`. This build **synthesizes `$TX;` / `$RX;`** responses (no REST keying). Full explanation: **`Docs/USER_GUIDE.md`** § 3 and integration plan § **2.3**.

## KD4Z samples (not in this repository)

**`SampleAmpTuner`** and other KD4Z **`Sample*`** projects are **not** included here. Clone **[KD4Z PgTgSamplePlugins](https://github.com/KD4Z/PgTgSamplePlugins)** at the **[baseline commit](https://github.com/KD4Z/PgTgSamplePlugins/tree/6941ac4cc047bf8c385b37544fe58272fdb002ad)** (see **[`ATTRIBUTION.md`](../ATTRIBUTION.md)**) when you need to compare or track upstream changes. All RFKIT-specific implementation lives under **`RFKitAmpTuner/`** in **this** repo.

## Documentation

| Document | Purpose |
|----------|---------|
| **`Docs/QUICK_START.md`** | **Fast path** — emulator or hardware |
| **`Docs/INSTALLATION_GUIDE.md`** | **PgTgBridge install → build → deploy → Plugin Manager** (living doc) |
| **`Docs/USER_GUIDE.md`** | **Operators:** connection, **PTT**, meters, safety |
| **`Docs/TROUBLESHOOTING.md`** | **Symptom → fix** (install, REST, meters, emulator, hardware) |
| **`Docs/TESTING_GUIDE.md`** | **Step-by-step** Phase A0–E (unit tests, emulator, **hardware + radio**) |
| **`Docs/QA_TEST_PLAN.md`** | **Phase 8–10** scope, exit criteria, **release sign-off** |
| `Docs/RFKIT_Option1_Integration_And_Test_Plan.md` | HTTP mapping, emulator, roadmap, locked decisions |
| `Docs/api/README.md` | RFKIT **OpenAPI 3.0** spec: `swagger.json` (from vendor `swagger.zip`), path/schema verification |
| [`../RfkitEmulator/README.md`](../RfkitEmulator/README.md) | **REST emulator** (stateful JSON, HttpLogging + ILogger, port 8080) |

Repository root **[`README.md`](../README.md)** describes the full tree (plugin + tests + emulator).
