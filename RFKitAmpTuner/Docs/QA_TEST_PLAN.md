# RFKIT Plugin — QA / Production Test Plan (Phases 8–10)

**Living document.** Use this for **test team handoff** and **release sign-off**. Detailed step-by-step procedures remain in **[TESTING_GUIDE.md](TESTING_GUIDE.md)**; this file defines **scope**, **order**, **exit criteria**, and **evidence**.

| Revision | Date | Notes |
|----------|------|--------|
| 0.1 | 2026-03-20 | Phases 8–10: emulator E2E, hardware + radio, release readiness. |
| 0.2 | 2026-03-20 | **PgTgBridge** + **RFKIT firmware** fields and how to obtain them; required every run. |
| 0.3 | 2026-03-22 | Optional evidence: **TESTING_GUIDE** § **B2.1** startup HTTP capture file (see **INSTALLATION_GUIDE** § **5.4.1**). |

---

## Document map

**Test team entry:** **[QUICK_START.md](QUICK_START.md)** — full document index + ordered **Phases 8–10** execution (this file is the sign-off record).

| Phase | Primary procedure | Pass / fail recorded here |
|-------|-------------------|---------------------------|
| **8** | **[TESTING_GUIDE.md](TESTING_GUIDE.md)** Phases **A0–C** + **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** | § 1 below |
| **9** | **TESTING_GUIDE.md** Phase **E** (hardware + radio) | § 2 below |
| **10** | Release checklist + doc completeness | § 3 below |

---

## Record build under test (**required every QA run**)

Copy this block into your test ticket or lab sheet **before** Phase 8 or Phase 9 execution (fill **all** rows; use **N/A** only where noted).

| Field | Value (fill in) |
|-------|------------------|
| **Test run ID / date** | e.g. `QA-2026-03-20-01` |
| **Git commit** (plugin source) | `git rev-parse --short HEAD` from **this repository root** (or **N/A** if DLL only) |
| **RFKitAmpTuner.dll** — File version | Right-click DLL → **Properties** → **Details** → *File version* |
| **PgTgBridge** — **product version** | See **§ How to obtain versions** below (e.g. `1.26.xxx.x`) |
| **PgTgBridge** — **MSI / installer name** (if known) | e.g. `PgTgBridge-1.26.999.1.msi` — **N/A** if unknown |
| **Windows** — edition + build | e.g. `Windows 11 23H2`, `Win+R` → `winver` |
| **RFKIT** — **firmware / controller / GUI version** | **Phase 8 (emulator only):** `N/A — RfkitEmulator (OpenAPI 0.9.0–aligned)` **Phase 9:** see **§ How to obtain versions** |
| **RFKIT** — **hardware identity** | **Phase 8:** `N/A` **Phase 9:** model/serial or asset tag |
| **Radio / host path** | e.g. `Flex + SmartSDR + PgTg` / **N/A** if Phase 8 without radio |

### How to obtain **PgTgBridge** version

Use **at least one** of these (note all that apply in the ticket):

1. **Settings / About** in **PgTgController** (if the UI exposes a version string).
2. **Windows Settings → Apps → Installed apps** → **PgTgBridge** → version column.
3. **Control Panel → Programs and Features** → **PgTgBridge** → *Version*.
4. **MSI file name** or release notes from the KD4Z download you installed.
5. **Optional (advanced):** Inspect `C:\Program Files\PgTgBridge\bin\PgTg.dll` → Properties → *File version* (may differ from marketing version — still record it).

### How to obtain **RFKIT firmware / software version** (Phase 9 — hardware)

Use **at least one** of these:

1. **HTTP:** From a PC on the same LAN:  
   `curl.exe -s http://<RFKIT_IP>:8080/info`  
   Record **`software_version`** (e.g. `controller`, `GUI`) and **`device`** string from JSON.
2. **RFKIT front panel / web UI** (if the product exposes firmware there).
3. **Asset label / commissioning sheet** for the test unit.

> **Rule:** Do **not** leave **PgTgBridge version** or **RFKIT firmware** blank on Phase **9** runs. On Phase **8** emulator-only runs, set RFKIT firmware row to **`N/A — RfkitEmulator`** as above.

---

## 1. Phase 8 — Integration vs **RfkitEmulator** (no RFKIT hardware)

**Goal:** PgTgBridge + **RFKitAmpTuner** + **RfkitEmulator** at **`http://<host>:8080/`**; REST traffic matches **[RFKIT_Option1_Integration_And_Test_Plan.md](RFKIT_Option1_Integration_And_Test_Plan.md)** § 4.

### 1.1 Prerequisites

- **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** completed through Plugin Manager (**TCP**, correct **IP**, port **8080**).
- **RfkitEmulator** running (`dotnet run --project RfkitEmulator/RfkitEmulator.csproj`).
- Optional: **`dotnet test RFKitAmpTuner.Tests/RFKitAmpTuner.Tests.csproj -c Release`** (**TESTING_GUIDE** Phase **A0**).

### 1.2 Execute

1. **Phase A0** — Unit tests (optional but recommended for CI / pre-smoke).
2. **Phase A** — `curl` **GET /info**, **GET /power** (emulator alone).
3. **Phase B** — Start RFKIT plugin in PgTg; confirm **Connected** and **GET /info** (and polls) in emulator **HttpLogging**. *Optional:* **TESTING_GUIDE** § **B2.1** — enable **`RfkitStartupCaptureSeconds`** (default **60** s in plugin config; **`0`** = off) and attach **`rfkit-http-capture-*.log`** from **`%ProgramData%\PgTg\RfKitAmpTuner\`** as evidence.
4. **Phase C** — Spot checks: operate/standby if UI allows, **stop emulator** → reconnect behavior per **TESTING_GUIDE**; restart emulator → recovery.

### 1.3 Phase 8 exit criteria

| # | Criterion | Pass |
|---|-----------|------|
| 8.1 | No sustained HTTP error/reconnect loop while emulator is up | ☐ |
| 8.2 | Emulator log shows expected **GET** paths (**/info**, **/power**, **/data**, **/tuner**, **/operate-mode** as applicable) | ☐ |
| 8.3 | **PUT /operate-mode** seen when operate/standby toggled (if host sends commands) | ☐ / N/A |
| 8.4 | **Stop emulator** → PgTg **Info** logs show reconnect attempts; **restart** → connection restores | ☐ |
| 8.5 | **POST /error/reset** if fault clear exercised | ☐ / N/A |

**Checklist:** **§ Record build under test** completed for this run (PgTgBridge version + RFKIT row = emulator **N/A** as specified).

**Tester initials / date:** _______________

---

## 2. Phase 9 — **Physical RFKIT** + **radio** (production path)

**Goal:** Same behavioral expectations as Phase 8, against the **real** amplifier REST interface; **radio connected** to PgTgBridge so **frequency / PTT-related** host behavior can be observed.

### 2.1 Safety and responsibility

- **RF exposure:** Follow **RF-POWER** and local regulations. This plugin **does not** replace hardware interlocks or safe operating practice.
- **PTT:** Amplifier keying may be **hardware-only**. The plugin **does not** send RFKIT REST for PTT; it returns **synthetic `$TX;` / `$RX;`** responses for host/command-queue flow. See **[USER_GUIDE.md](USER_GUIDE.md)** § PTT.
- **Operate/Standby:** Confirm **physical** amplifier state matches UI when possible (audible/LED per device).

### 2.2 Prerequisites

- Phase **8** **passed** on the **same plugin build** (or documented waiver).
- **RFKIT** on LAN: note **IP** and port (default **8080**).
- **Firewall** allows PC ↔ RFKIT.
- **Radio + SmartSDR** (or supported radio path) connected per **PgTgBridge** / KD4Z documentation.
- Plugin Manager: **RFKIT** row → **TCP**, **RFKIT IP**, port **8080** (not **1500**).

### 2.3 Execute

Follow **[TESTING_GUIDE.md](TESTING_GUIDE.md)** — **Phase E — Real RFKIT + radio**.

### 2.4 Phase 9 exit criteria

| # | Criterion | Pass |
|---|-----------|------|
| 9.1 | Plugin **Connected**; **GET /info** succeeds (verify via device logs / packet capture if needed — no emulator console on hardware) | ☐ |
| 9.2 | **Operate / Standby** from host → **PUT /operate-mode** (wireshark or RFKIT vendor log if available) **or** verified **physical** state | ☐ |
| 9.3 | **Polling**: plausible **temperature**; **forward power / SWR** consistent with **RX vs TX** (see **TESTING_GUIDE** — RX may show 0 W / 1.0 SWR by design) | ☐ |
| 9.4 | **Frequency / band**: with radio linked, **MHz** line not stuck at **`--`** when host supplies VFO; **`$FRQ`** echo behavior per integration plan § 4.5 | ☐ / N/A |
| 9.5 | **Fault clear** (if applicable): **`POST /error/reset`** or device fault clears | ☐ / N/A |
| 9.6 | **Network fault**: disconnect RFKIT or block port → reconnect behavior; restore → **Connected** | ☐ |
| 9.7 | **PTT:** Document observed behavior (synthetic ack + **RadioPtt** / interlock); no expectation of REST PTT | ☐ |

**Checklist:** **§ Record build under test** completed for this run (**PgTgBridge** version + **RFKIT firmware** from **`GET /info`** or device — not **N/A**).

**Hardware ID / notes:** _______________

**Tester initials / date:** _______________

---

## 3. Phase 10 — Release readiness (documentation + packaging)

**Goal:** Production / support can install and operate without engineering walkthrough.

### 3.1 Documentation completeness

| Doc | Location | Required for sign-off |
|-----|----------|------------------------|
| Installation | [INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md) | ☐ |
| Attribution / KD4Z baseline | [`../../ATTRIBUTION.md`](../../ATTRIBUTION.md) | ☐ |
| Quick start | [QUICK_START.md](QUICK_START.md) | ☐ |
| User / operator | [USER_GUIDE.md](USER_GUIDE.md) | ☐ |
| Troubleshooting | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) | ☐ |
| Step-by-step QA | [TESTING_GUIDE.md](TESTING_GUIDE.md) + **this file** | ☐ |
| Emulator (test double) | [RfkitEmulator/README.md](../../RfkitEmulator/README.md) | ☐ |
| API reference (engineering) | [RFKIT_Option1_Integration_And_Test_Plan.md](RFKIT_Option1_Integration_And_Test_Plan.md) | ☐ |

### 3.2 Packaging

| # | Item | Pass |
|---|------|------|
| 10.1 | **RFKitAmpTuner.dll** built **Release**, copied to `PgTgBridge\plugins\` per install guide | ☐ |
| 10.2 | **Version** recorded (plugin **File version** + **PgTgBridge** + **RFKIT firmware** per **§ Record build under test**) in release notes or ticket | ☐ |
| 10.3 | **Support matrix** stated: Windows **10/11**, **.NET 10** runtime (PgTg requirement), RFKIT REST **0.9.0**-aligned | ☐ |
| 10.4 | Known limitations listed (PTT hardware, **`$FRQ`** echo-only, init compile-time flag) | ☐ |

### 3.3 Phase 10 exit criteria

- Phase **8** and **9** completed **or** explicitly waived with sign-off (e.g. hardware not yet available — ship **emulator-only** validation only).
- All rows in **§ 3.1** checked.
- **§ 3.2** complete for the build being released.

**Release approver:** _______________ **Date:** _______________

---

## Defect reporting

For each issue attach:

1. **Git commit** / DLL **File version** / **PgTgBridge product version** / **RFKIT firmware** (same as **§ Record build under test**).  
2. **PgTgController** log excerpt (Info/Error).  
3. **Emulator** log (Phase 8) or **network capture / RFKIT UI** (Phase 9) if relevant.  
4. **PluginConfigurations** snippet for **rfpower.rfkit-amplifier-tuner** (redact secrets).

---

## Related documents

| Document | Purpose |
|----------|---------|
| [INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md) | Build, deploy, Plugin Manager |
| [TESTING_GUIDE.md](TESTING_GUIDE.md) | Detailed Phase A0–E steps |
| [USER_GUIDE.md](USER_GUIDE.md) | Operator usage, PTT, meters |
| [TROUBLESHOOTING.md](TROUBLESHOOTING.md) | Symptom → fix |
| [QUICK_START.md](QUICK_START.md) | Shortest path to first connect |
| [`../../ATTRIBUTION.md`](../../ATTRIBUTION.md) | KD4Z **SampleAmpTuner** baseline commit + trademarks |
