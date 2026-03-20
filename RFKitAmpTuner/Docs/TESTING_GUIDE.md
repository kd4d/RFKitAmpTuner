# RFKIT Plugin — Step-by-Step Testing Guide

**Living document.** Extend this as automated tests, hardware checklists, and host logging evolve.

**Formal QA / sign-off:** Use **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** for **Phase 8–10** scope, exit criteria, and release checklist.

| Revision | Date | Notes |
|----------|------|--------|
| 0.1 | 2026-03-20 | Initial: RfkitEmulator, REST smoke, PgTgBridge + plugin, port **8080**, log checks. |
| 0.2 | 2026-03-20 | PgTgBridge may reject **127.0.0.1** — use LAN IP for same-PC tests. |
| 0.3 | 2026-03-20 | Meters: **`-- MHz`** vs radio; RX vs TX power display (by design). |
| 0.4 | 2026-03-20 | REST reconnect + **`$FRQ`** echo: stop emulator → **Reconnecting** then recovery when emulator returns. |
| 0.5 | 2026-03-20 | **Reconnect visibility:** PgTg UI may not label **Reconnecting**; use **Info** log lines (plugin + **RfkitHttpConnection**). Fixed **Previous** state on **ConnectionStateChanged**. |
| 0.6 | 2026-03-20 | Phase 8 prep: **Phase A0** — **`dotnet test`** for **`RFKitAmpTuner.Tests`** (no PgTg runtime; needs PgTg **DLLs** for compile, see **INSTALLATION_GUIDE** § 3.1). |
| 0.7 | 2026-03-20 | Phase 9: **Phase E** — real RFKIT + radio checklist; link **QA_TEST_PLAN**. |

---

## What you are verifying

1. **RFKIT REST** is reachable at the configured base URL (emulator or hardware).
2. **PgTgBridge** loads **RFKitAmpTuner** and uses **HTTP REST** when **TCP** is selected with **`UseRfkitRestApi`** true (default).
3. Plugin reaches **connected** state and exchanges traffic without persistent errors.

---

## Before you start

Complete **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** through deploy, restart, and Plugin Manager enable.

Confirm in Plugin Manager:

| Field | Expected for **local emulator** |
|-------|----------------------------------|
| Plugin | **RFKIT** / `rfpower.rfkit-amplifier-tuner` |
| Enabled | **Yes** |
| Connection | **TCP** |
| IP | **LAN IPv4 of this PC** (e.g. `192.168.x.x` from `ipconfig`) if Plugin Manager **rejects 127.0.0.1**; otherwise emulator host IP. |
| Port | **`8080`** — **not** `1500` (that is a different device profile). |

> **PgTgBridge** may refuse **`127.0.0.1`** as “invalid” for a device. For **RfkitEmulator** on the same machine, keep emulator on **`0.0.0.0:8080`** and enter your **non-loopback** IPv4 in Plugin Manager.

> If the UI shows the wrong port, click the **RFKIT** row (not Elecraft/other), set **8080**, then **Apply/Save** per host UI.

---

## Phase A0 — Automated unit tests (no emulator, no PgTgBridge runtime)

**Goal:** Confirm **JSON → synthetic CAT** and **command mapping** still pass after pulls or local edits.

**Prerequisites:** Same as building the plugin — **.NET 10 SDK** and **PgTg** assemblies at **`RFKitAmpTuner.csproj`** **HintPath** (default `C:\Program Files\PgTgBridge\bin\`). See **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** § 3 and § 3.1.

From the **repository root**:

```powershell
dotnet test RFKitAmpTuner.Tests/RFKitAmpTuner.Tests.csproj -c Release
```

**Pass criteria:** All tests **Passed** (typically runs in under a minute).

---

## Phase A — REST emulator alone (no PgTgBridge)

**Goal:** Prove the HTTP API answers before involving the plugin.

### A1. Start RfkitEmulator

From repo root:

```powershell
dotnet run --project RfkitEmulator\RfkitEmulator.csproj
```

Default URL: **`http://0.0.0.0:8080`** (reachable as **`http://127.0.0.1:8080`**).

### A2. Smoke test `GET /info`

**PowerShell:**

```powershell
curl.exe -s http://127.0.0.1:8080/info
```

Expect **HTTP 200** and JSON body (device/info fields per OpenAPI).

### A3. Optional — another endpoint

```powershell
curl.exe -s http://127.0.0.1:8080/power
```

Expect **200** + JSON.

If these fail: fix port conflict, firewall, or emulator crash **before** testing the plugin.

---

## Phase B — Plugin + emulator (PgTgBridge)

**Goal:** Plugin **Start** triggers REST (e.g. **`GET /info`**) and reaches a stable **connected** state.

### B1. Start emulator first

Keep **RfkitEmulator** running (**Phase A**).

### B2. PgTgBridge configuration

1. **RFKIT** plugin **enabled**, TCP **`<your-LAN-IPv4>:8080`** (same IP you set in Plugin Manager; see **Before you start**).
2. If two amp+tuner plugins are enabled, consider **disabling** the non-RFKIT one during this test to avoid confusion.

### B3. Start the RFKIT plugin connection

Use the host control that **starts** or **connects** the amplifier/tuner plugin (exact control label varies by PgTgBridge version).

### B4. Observe logs

1. Open **PgTgBridge / PgTgController** log viewer (or log files — location per KD4Z docs).
2. Look for plugin module messages indicating:
   - **RFKIT REST (HTTP)** at `http://<your-configured-IP>:8080/` (your LAN IP if loopback was blocked).
   - Absence of repeated connection/HTTP errors after startup.

### B5. Observe emulator logging

With default **HttpLogging** in **RfkitEmulator**, the console (or configured sinks) should show incoming requests such as **`GET /info`** when the plugin connects/polls.

**Pass criteria (initial):**

- Emulator shows **GET** (and later **GET/PUT** as the host polls and you operate SSDR).
- Plugin does not sit in a tight **error/reconnect** loop.

---

## Phase C — Functional spot-checks (manual)

Perform as features are stable; extend this list over time.

| Step | Action | Expect |
|------|--------|--------|
| C1 | Operate/Standby (or equivalent) from SSDR / host | Corresponding **PUT** on emulator (e.g. `/operate-mode`) in logs |
| C2 | Frequency / band change if applicable | Plugin maps to REST; emulator shows relevant **GET/PUT** |
| C3 | Disconnect emulator (stop process) | Connection should go **Reconnecting** / **Disconnected**; after **ReconnectDelayMs** intervals, **`GET /info`** retries. Restart emulator — plugin should reach **Connected** again (watch PgTg + emulator logs). |

---

## Phase D — Phase 8 summary (emulator)

**Phase 8** = complete **Phase A0** (optional), **A**, **B**, **C** above, then record results in **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** § 1.

---

## Phase E — Real RFKIT + radio (Phase 9)

**Goal:** Validate the same plugin build against **physical RFKIT** REST with **PgTgBridge** and a **connected radio** (Flex / SmartSDR path per KD4Z) so frequency and interlock behavior match production.

**Prerequisites:**

- **Phase 8** passed on this build (or documented exception).
- **RFKIT** powered, on LAN; note **IPv4** and **port** (default **8080**).
- From the plugin PC: **`curl.exe -s http://<RFKIT_IP>:8080/info`** returns **200** + JSON (adjust URL if non-default port or HTTPS).
- **Stop RfkitEmulator** if it was sharing **8080** on the same IP.
- **Radio** linked to PgTgBridge per vendor docs.

**Safety:** RF exposure and amplifier interlocks are **operator responsibility**. This plugin does **not** implement RFKIT REST PTT; see **[USER_GUIDE.md](USER_GUIDE.md)** § 3.

### E1 — Repoint Plugin Manager

1. Open **RFKIT** row (`rfpower.rfkit-amplifier-tuner`).
2. **TCP** → **IP** = RFKIT address, **Port** = **8080** (or device port).
3. Save / Apply; restart plugin connection if required.

### E2 — Connect and observe

1. **Start** RFKIT plugin; expect **Connected**.
2. PgTg **Info** logs: no tight loop of **GET /info** failures.
3. **Temperature** meter should update over time ( **`$TMP`** from **GET /power** ).

### E3 — Operate / standby

1. Toggle **Operate** and **Standby** from the host UI.
2. **Expect:** **`PUT /operate-mode`** on the wire (packet capture / RFKIT tools if available) **and/or** visible **physical** state on amplifier.

### E4 — Radio frequency / band (if applicable)

1. With radio connected, change VFO / band.
2. **Expect:** MHz line **not** permanently **`--`** when host supplies frequency; plugin **`$FRQ`** behavior is **echo-only** per integration plan § 4.5 (no device PUT until API requires it).

### E5 — Power / SWR (RX vs TX)

1. **RX:** Forward power **0 W** and SWR **1.0** may be **normal** (sample-style **`StatusTracker`**).
2. **TX** (legal, safe power): forward power and SWR should reflect plausible **GET /power** values.

### E6 — PTT behavior (§ 2.3)

1. Note: plugin responds with **synthetic `$TX;` / `$RX;`** — **no** REST keying.
2. Confirm **hardware / station** keying matches your safety model; document any host **PttReady** / timing observations for the report.

### E7 — Fault clear (optional)

1. If host exposes fault clear and device has a fault path: trigger clear.
2. **Expect:** **`POST /error/reset`** if mapping applies; **`$FLT 0;`** path in parser.

### E8 — Network fault / recovery

1. Disconnect RFKIT Ethernet or block **8080** briefly.
2. **Expect:** reconnect attempts in **Info** log; restore network → **Connected** again.

### E9 — Sign-off

Complete **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** § 2 (Phase 9 exit table).

---

## Quick reference — URLs

| Scenario | Base URL |
|----------|-----------|
| Emulator, **curl** / browser on same PC | `http://127.0.0.1:8080/` |
| Emulator, **PgTgBridge** (if loopback blocked) | `http://<this-PC-LAN-IP>:8080/` |
| Same machine, custom port | `http://<host>:<port>/` |
| LAN device | `http://<device-ip>:8080/` |

---

## Troubleshooting (testing)

| Symptom | Likely cause |
|---------|----------------|
| **127.0.0.1:1500** in UI | Wrong plugin row or old value — set **RFKIT** to **8080**. |
| *“localhost IP (127.0.0.1)… valid IP”* | Host blocks loopback — use **LAN IPv4** + **8080** for local emulator. |
| **Meters: `-- MHz`**, little else | **`-- MHz`** is normally the **radio VFO** line. With **no Flex/radio connected** to PgTgBridge (emulator-only test), the host often has **no frequency** to show — not an RFKIT REST bug. Connect SmartSDR / radio per PgTg requirements to populate MHz. |
| **Meters: power/SWR look empty in RX** | **`StatusTracker.GetMeterReadings()`** intentionally reports **0 W** forward power and **1.0** SWR when **not transmitting** (same as KD4Z samples). **Temperature** should still update from **`$TMP`**. Brief **TX** (safe setup) should show forward power/SWR changing. |
| **No “Reconnecting” in PgTg UI** | Normal for some builds: the host may only show **Connected** / **Disconnected**. Look in **PgTgController logs** at **Info** for: **`Reconnecting to device (RFKIT REST…)`**, **`RFKIT REST unreachable (GET /info); retry in … ms`**, **`RFKIT REST heartbeat failed; entering reconnect`**. |
| **Emulator log spam when up** | **HttpLogging** logs every request; when the plugin reconnects, polling resumes → many **GET /power**, **/tuner**, etc. Reduce log level in **`RfkitEmulator/appsettings.json`** if needed. |
| Connection refused | Emulator not running; wrong port; firewall. |
| 404 on paths | Emulator version mismatch; wrong base path (trailing slash usually OK). |
| Plugin connects but no REST in emulator log | **`UseRfkitRestApi`** false (non-default build) or not RFKIT transport — see integration plan. |
| SSL errors | RFKIT default is **HTTP**; HTTPS needs matching URL and device support. |

---

## Related documents

| Document | Purpose |
|----------|---------|
| [`QA_TEST_PLAN.md`](QA_TEST_PLAN.md) | **Phase 8–10** QA scope, exit criteria, release sign-off. |
| [`INSTALLATION_GUIDE.md`](INSTALLATION_GUIDE.md) | Install PgTgBridge, build, deploy, Plugin Manager. |
| [`USER_GUIDE.md`](USER_GUIDE.md) | Operators: PTT, meters, connection. |
| [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) | Symptom → fix. |
| [`QUICK_START.md`](QUICK_START.md) | Shortest path to first connect. |
| [`RFKIT_Option1_Integration_And_Test_Plan.md`](RFKIT_Option1_Integration_And_Test_Plan.md) | Full integration phases and API mapping. |
| [`../../RfkitEmulator/README.md`](../../RfkitEmulator/README.md) | Emulator run options and PowerShell `curl` examples. |
