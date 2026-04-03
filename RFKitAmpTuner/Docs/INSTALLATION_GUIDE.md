# RFKIT Plugin — Installation Guide

**Living document.** Revise this file as the workflow, PgTgBridge UI, or plugin packaging changes.

| Revision | Date | Notes |
|----------|------|--------|
| 0.1 | 2026-03-20 | Initial guide: PgTgBridge from KD4Z, build, elevated deploy, Plugin Manager, TCP **8080** for REST. |
| 0.2 | 2026-03-20 | Plugin Manager may reject **127.0.0.1**; use LAN IP for same-PC emulator. |
| 0.3 | 2026-03-20 | Phase 8 prep: **`dotnet test`** for **`RFKitAmpTuner.Tests`** after build (PgTg **HintPath** required). |
| 0.4 | 2026-03-20 | Phase 9–10: links to **USER_GUIDE**, **QA_TEST_PLAN**, **TROUBLESHOOTING**, **QUICK_START**; hardware note § 5.5. |
| 0.5 | 2026-03-20 | Standalone **RFKitAmpTuner** repo: **`RFKitAmpTuner.sln`**, paths; **ATTRIBUTION** / KD4Z baseline. |
| 0.6 | 2026-03-22 | **Startup HTTP capture:** `RfkitStartupCaptureSeconds` / `RfkitHttpTrafficMaxBodyChars` in settings JSON; log file under **`%ProgramData%\PgTg\RfKitAmpTuner\`**. |
| 0.7 | 2026-03-22 | Capture duration: any **seconds** &gt; 0 (e.g. **600** = 10 min), max **7200**; clamped if higher. |
| 0.8 | 2026-03-22 | Default **`RfkitStartupCaptureSeconds`** = **60**; **`0`** disables. |

---

## 1. Prerequisites

| Requirement | Notes |
|-------------|--------|
| **Windows 10/11 (64-bit)** | Same as [PgTgBridge](https://www.kd4z.com/downloads). |
| **.NET 10 Runtime** | PgTgBridge installer prompts if missing. **Building** the plugin also needs [.NET 10 SDK](https://dotnet.microsoft.com/download). |
| **PgTgBridge** | Installed product (MSI), not only DLLs. |
| **This repository** | **RFKitAmpTuner** root: `RFKitAmpTuner/` (plugin), `RFKitAmpTuner.Tests/`, `RfkitEmulator/`, `RFKitAmpTuner.sln`. |

Optional: **Git** to clone/pull updates.

---

## 2. Install PgTgBridge

1. Open **[Downloads — KD4Z.COM](https://www.kd4z.com/downloads)**.
2. Download the current **PgTgBridge** MSI (e.g. v1.26.999.1 Beta).
3. Run the installer **as Administrator** if prompted.
4. Confirm assemblies exist (used by the plugin project at **build** time):
   - `C:\Program Files\PgTgBridge\bin\PgTg.dll`
   - `C:\Program Files\PgTgBridge\bin\PgTg.Common.dll`
   - `C:\Program Files\PgTgBridge\bin\PgTg.Helpers.dll`
5. Complete any **first-run / OBE** steps in **PgTgController** per the vendor **Installation Guide** on the same downloads page.

---

## 3. Build `RFKitAmpTuner`

From the **repository root** (folder containing **`RFKitAmpTuner.sln`**):

```powershell
dotnet build RFKitAmpTuner.sln -c Release
```

Or build the plugin project only:

```powershell
dotnet build RFKitAmpTuner\RFKitAmpTuner.csproj -c Release
```

Output DLL (typical):

`RFKitAmpTuner\bin\Release\net10.0\RFKitAmpTuner.dll`

Fix any **compile errors** before deploying. If references to `PgTg` fail, PgTgBridge is missing or not under `C:\Program Files\PgTgBridge\bin\` — reinstall or adjust `HintPath` in `RFKitAmpTuner.csproj`.

### 3.1 Unit tests (optional, same prerequisites as build)

The **mapper / JSON→CAT** logic is covered by **`RFKitAmpTuner.Tests`** (xUnit). From the repository root, after a successful **`dotnet build`** of the plugin:

```powershell
dotnet test RFKitAmpTuner.Tests\RFKitAmpTuner.Tests.csproj -c Release
```

Expect **all tests passed**. This uses the same **`PgTg*.dll`** references as the plugin project (`InternalsVisibleTo` exposes internal APIs to the test assembly). A **clean clone** on a new machine still requires **PgTgBridge** installed at the paths in **`RFKitAmpTuner.csproj`** (or updated **HintPath**s) before **`dotnet test`** or **`dotnet build`** succeeds.

---

## 4. Deploy the plugin DLL

PgTgBridge loads third-party plugins from:

**`C:\Program Files\PgTgBridge\plugins\`**

Windows protects `Program Files`; copying requires elevation.

### Option A — Deploy script (recommended)

1. Open **PowerShell as Administrator**.
2. Run:

```powershell
cd "<path-to-repo>\RFKitAmpTuner\scripts"
powershell -ExecutionPolicy Bypass -File .\Deploy-ToPgTgBridge.ps1
```

The script copies `..\bin\Release\net10.0\RFKitAmpTuner.dll` into the `plugins` folder.

### Option B — Manual copy

Copy **`RFKitAmpTuner.dll`** from `bin\Release\net10.0\` to **`C:\Program Files\PgTgBridge\plugins\`**.

> **Note:** Only **`RFKitAmpTuner.dll`** is required in `plugins\` for normal installs (same pattern as other shipped sample plugins). The host supplies `PgTg` interfaces at runtime.

---

## 5. Enable the plugin and set TCP for RFKIT REST

The host **Plugin Manager** UI still labels the transport **TCP** (IP + port). For **RFKIT HTTP (Phase 4)**, that pair defines the REST base URL:

**`http://{IpAddress}:{Port}/`**

when **`UseRfkitRestApi`** is **true** (plugin default).

### 5.1 Restart PgTgBridge

After copying a **new** DLL:

- Restart the **PgTgBridge** tray/controller application and/or **Windows service** (per KD4Z documentation) so the new plugin is discovered.

### 5.2 Plugin Manager

1. Open **PgTgController → Settings → Plugin Manager** (wording may vary slightly by version).
2. Find **RFKIT** / plugin ID **`rfpower.rfkit-amplifier-tuner`**.
3. **Enable** the plugin.
4. Set **connection type** to **TCP** (not serial), unless you intentionally use serial.
5. Set **IP** and **port**:
   - **RfkitEmulator on the same PC:** Port **`8080`**. If Plugin Manager shows *“configured with localhost IP (127.0.0.1)… enter a valid IP”*, the **host** is blocking loopback — use this PC’s **LAN IPv4** (e.g. `192.168.1.42` from `ipconfig`) instead of **`127.0.0.1`**, with **RfkitEmulator** still listening on **`http://0.0.0.0:8080`** (default).
   - **RFKIT hardware on the LAN:** that device’s real IP and **`8080`** (unless configured otherwise).
   - **Not** **`1500`** for RFKIT REST — that port is typical for **Elecraft KPA** in samples, not RFKIT HTTP.

### 5.3 Multiple “Amplifier + Tuner” plugins

If **Elecraft KPA1500** (or another amp+tuner plugin) is **also** enabled, the list may show **several rows**. Always edit the row that matches **RFKIT** / **`rfpower.rfkit-amplifier-tuner`**, not another manufacturer.

### 5.4 Optional: `SettingsConfig.json`

Advanced users may edit **`C:\ProgramData\PgTg\0\SettingsConfig.json`** (with PgTgBridge **stopped**, and a **backup** first). Under **`PluginConfigurations`**, locate the object with **`"PluginId": "rfpower.rfkit-amplifier-tuner"`** and set **`Port`** to **`8080`** (and **`IpAddress`** as needed). Restart PgTgBridge after saving.

#### 5.4.1 Startup HTTP capture (field / QA debugging)

After deploying a build that includes **`RfkitStartupTrafficCapture`**, you can log **RFKIT REST** traffic (and **CAT** framing in/out) for a **configurable number of seconds** after the plugin connection starts (countdown begins at **`StartAsync`**). Output is a **UTF-8 text file**, not the main PgTg log.

| JSON property | Values | Purpose |
|-----------------|--------|---------|
| **`RfkitStartupCaptureSeconds`** | **Default `60`** (one minute) if omitted in a fresh plugin config; **`0`** = off; **1 … 7200** = capture for that many **seconds** | Examples: **`60`** (default), **`600`** = 10 minutes, **`3600`** = 1 hour. Values **&gt; 7200** are **clamped to 7200** (2 hours max). |
| **`RfkitHttpTrafficMaxBodyChars`** | Integer (default **8192**) | Max characters per request/response **body** field in the file (8 KB default). Longer payloads are truncated with a marker. |

Add these properties **next to** the other RFKIT fields (same object as **`IpAddress`**, **`Port`**, etc.):

```json
"RfkitStartupCaptureSeconds": 60,
"RfkitHttpTrafficMaxBodyChars": 8192
```

For a longer field capture (e.g. 10 minutes), use **`600`**. For **no** file capture, set **`"RfkitStartupCaptureSeconds": 0`**.

**Log file location:** **`%ProgramData%\PgTg\RfKitAmpTuner\`** (i.e. **`C:\ProgramData\PgTg\RfKitAmpTuner\`** on a typical install). File name pattern: **`rfkit-http-capture-YYYYMMDD-HHMMSS.log`**.

**PgTg log line:** When **`RfkitStartupCaptureSeconds`** is not **`0`**, **`RfkitStartupTrafficCapture`** logs one **Info** line with the **full path** to the file when the window starts (after bridge restart).

**After testing:** Set **`RfkitStartupCaptureSeconds`** back to **`0`** and restart the bridge — capture generates a lot of I/O during fast polling.

**Note:** If PgTg’s host deserializer **drops unknown properties**, use Plugin Manager **Save** after editing JSON, or confirm the RFKIT row still shows your IP/port so the merged config includes these keys.

### 5.5 RFKIT hardware (not emulator)

For **physical RFKIT** on the LAN:

1. Use the amplifier’s **actual IPv4** and **8080** (unless the unit uses a different HTTP port).
2. Confirm from the PC: **`curl.exe -s http://<RFKIT_IP>:8080/info`** returns **200** + JSON before relying on PgTg.
3. Follow **[USER_GUIDE.md](USER_GUIDE.md)** (PTT, meters) and **[TESTING_GUIDE.md](TESTING_GUIDE.md)** **Phase E**; sign-off **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** Phase **9**.

---

## 6. Optional settings (future / custom)

| Setting | Where | Purpose |
|---------|--------|---------|
| **`UseRfkitRestApi`** | Host **CustomSettings** or full `RFKitAmpTunerConfiguration` if the host supports it | **`false`** = raw CAT-over-TCP; **`true`** (default) = REST. |
| **`HttpBaseUrl`** | Same | Non-empty overrides `http://{Ip}:{port}/`. |
| **`RfkitStartupCaptureSeconds`** | **`SettingsConfig.json`** on the RFKIT plugin object (see § 5.4.1) | **Seconds** of capture (**default 60**; **0** = off; max **7200**). |
| **`RfkitHttpTrafficMaxBodyChars`** | Same | Per-field body limit in that file (default **8192**). |

If the host only supplies generic `IPluginConfiguration`, the plugin still defaults **`UseRfkitRestApi = true`** when it merges into `RFKitAmpTunerConfiguration`.

---

## 7. Related documents

| Document | Purpose |
|----------|---------|
| [`QUICK_START.md`](QUICK_START.md) | Fastest path: emulator or hardware. |
| [`TESTING_GUIDE.md`](TESTING_GUIDE.md) | Step-by-step Phase A0–E (emulator + hardware). |
| [`QA_TEST_PLAN.md`](QA_TEST_PLAN.md) | Phase **8–10** QA / release sign-off. |
| [`USER_GUIDE.md`](USER_GUIDE.md) | Operators: connection, PTT, meters. |
| [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) | Install + runtime problems. |
| [`RFKIT_Option1_Integration_And_Test_Plan.md`](RFKIT_Option1_Integration_And_Test_Plan.md) | Engineering roadmap, API mapping, decisions. |
| [`../../README.md`](../../README.md) | Repository root: solution layout, attribution. |
| [`../../ATTRIBUTION.md`](../../ATTRIBUTION.md) | KD4Z baseline commit + trademarks. |
| [`../../RfkitEmulator/README.md`](../../RfkitEmulator/README.md) | Run the REST emulator on port 8080. |

---

## 8. Troubleshooting (installation)

| Symptom | Check |
|---------|--------|
| Build: cannot find **PgTg** | PgTgBridge installed; `HintPath` in `.csproj`. |
| Copy: **Access denied** | Run PowerShell **as Administrator** or use Option A script. |
| Plugin not listed | DLL name/placement under `plugins\`; restart PgTgBridge. |
| UI shows **127.0.0.1:1500** for RFKIT | Wrong row (e.g. Elecraft) or port not saved — set RFKIT row to **8080** for emulator/typical RFKIT HTTP. |
| *“localhost IP (127.0.0.1)… valid IP”* | **PgTgBridge** host validation — use your PC’s **LAN IPv4** with port **8080** for local **RfkitEmulator** (emulator bound to `0.0.0.0`). |
| Connection fails immediately | Firewall; wrong IP/port; REST service not running; see **Testing Guide**. |
