# RFKIT Plugin — Quick Start & test-team guide

**Who this is for**

| Audience | Use this doc for… |
|----------|-------------------|
| **Test / QA / production** | **§ Document library**, **§ Full test execution (Phases 8–10)** — the complete map of what to read and in what order. |
| **Anyone** | **§ A** / **§ B** — fastest smoke path to **Connected** (emulator or hardware). |

**This file is not the only procedure:** a **full** qualification run always includes **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** (sign-off tables + **PgTgBridge** / **RFKIT firmware** on every run) and **[TESTING_GUIDE.md](TESTING_GUIDE.md)** (detailed steps). **§ A** and **§ B** below are shortened paths; they do **not** replace those documents.

| Revision | Date | Notes |
|----------|------|--------|
| 0.1 | 2026-03-20 | Phase 10: quick path emulator + hardware pointer. |
| 0.2 | 2026-03-20 | Test team: full document map + Phase 8–10 execution order; clarify scope vs smoke. |

---

## Document library (what each file is for)

Read in the **order in § Full test execution** when you are running formal QA.

| Document | Use when you need… |
|----------|---------------------|
| **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** | **Master QA hub:** fill **§ Record build under test** (**PgTgBridge version** + **RFKIT firmware**) **every run**; Phase **8 / 9 / 10** exit checklists and release sign-off. |
| **[TESTING_GUIDE.md](TESTING_GUIDE.md)** | **Step-by-step** instructions: **A0** (unit tests), **A–C** (emulator), **D** (Phase 8 wrap), **E** (hardware + radio). |
| **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** | Install PgTgBridge, **build** plugin, **deploy** DLL, Plugin Manager, **§ 5.5** hardware IP, optional `SettingsConfig.json`. |
| **[USER_GUIDE.md](USER_GUIDE.md)** | Operator behavior: **TCP = REST URL**, **PTT** (synthetic `$TX;`/`$RX;`), meters, **§ 2.3** safety context. |
| **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** | Symptom → fix (connection, **8080 vs 1500**, loopback, meters, emulator). |
| **[RFKIT_Option1_Integration_And_Test_Plan.md](RFKIT_Option1_Integration_And_Test_Plan.md)** | Engineering: API mapping, PTT/init **decisions**, roadmap (reference for disputes / design questions). |
| **[RfkitEmulator/README.md](../../RfkitEmulator/README.md)** | Run emulator, logging, **curl** PUT examples. |
| **`Docs/api/README.md`** | OpenAPI **0.9.0** / `swagger.json` location. |

---

## Full test execution (Phases 8–10) — **complete** instructions

Follow this sequence for a **full** handoff / release qualification (not only a smoke test).

### Before anything

1. **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** — PgTg installed, plugin **Release** built and copied to `plugins\`, Plugin Manager **RFKIT** row **TCP** + correct **IP** + **8080**.
2. Open **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** → complete **§ Record build under test** (including **PgTgBridge version** and **RFKIT firmware** row — use **N/A** for emulator only per that section).

### Phase 8 — Emulator integration

1. **[TESTING_GUIDE.md](TESTING_GUIDE.md)** — **Phase A0** (optional): `dotnet test RFKitAmpTuner.Tests\RFKitAmpTuner.Tests.csproj -c Release`.
2. **TESTING_GUIDE** — **Phase A** (emulator + `curl`), **Phase B** (PgTg + plugin), **Phase C** (spot checks + reconnect).
3. **TESTING_GUIDE** — **Phase D** (summary pointer).
4. **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** — **§ 1** exit criteria table; initials / date.

### Phase 9 — Real RFKIT + radio

1. Prerequisites in **QA_TEST_PLAN** § 2 and **TESTING_GUIDE** **Phase E**.
2. **Re-fill** **§ Record build under test** if the **PgTg** or **RFKIT** build changed; **must** record **RFKIT firmware** from **`GET /info`** or device (not **N/A**).
3. **[TESTING_GUIDE.md](TESTING_GUIDE.md)** — **Phase E** (E1–E9).
4. **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** — **§ 2** exit criteria; initials / date.

### Phase 10 — Release readiness

1. **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** — **§ 3** documentation checklist + packaging + approver.

### If something fails

- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)**  
- **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** — **Defect reporting** (attach versions from **§ Record build under test**).

---

## You need (smoke / lab machine)

- **Windows 10/11**, **PgTgBridge** installed ([KD4Z downloads](https://www.kd4z.com/downloads)).
- **.NET 10 SDK** only if **building** the plugin or running **RfkitEmulator** / **dotnet test** from this repo.
- **RFKIT** on the network **or** **RfkitEmulator** for testing.

---

## A — Smoke: **RfkitEmulator** (no amplifier)

1. **Clone / open** this **RFKitAmpTuner** repository (KD4D private remote — not KD4Z **PgTgSamplePlugins**).

2. **Terminal 1** — start emulator:
   ```powershell
   dotnet run --project RfkitEmulator\RfkitEmulator.csproj
   ```
   Default: **`http://0.0.0.0:8080`**.

3. **Build & deploy** plugin (**Administrator** PowerShell):
   ```powershell
   cd <repo>\RFKitAmpTuner\scripts
   powershell -ExecutionPolicy Bypass -File .\Deploy-ToPgTgBridge.ps1
   ```
   (Or build Release + copy `RFKitAmpTuner.dll` to `C:\Program Files\PgTgBridge\plugins\` — see **INSTALLATION_GUIDE**.)

4. **Restart PgTgBridge** (tray app / service per KD4Z).

5. **Plugin Manager** → enable **RFKIT** (`rfpower.rfkit-amplifier-tuner`) → **TCP** →  
   - **IP:** this PC’s **LAN IPv4** if **127.0.0.1** is rejected (e.g. `192.168.x.x` from `ipconfig`).  
   - **Port:** **`8080`** (not **1500**).

6. **Start** the RFKIT plugin connection in PgTg.

7. Confirm **Connected** and emulator console shows **GET /info** (and other GETs).

**Then for formal Phase 8:** **[TESTING_GUIDE.md](TESTING_GUIDE.md)** Phases **A0–C** + **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** § **1**.

---

## B — Smoke: **real RFKIT**

1. Complete **section A** once if you need to verify PgTg + DLL; **stop emulator** when switching to hardware.

2. Plugin Manager → **RFKIT** row → **TCP** → **RFKIT’s IP** (same subnet as PC), port **8080** (unless device uses another port).

3. Optional: **`HttpBaseUrl`** in custom settings to override `http://{Ip}:{Port}/` (see **INSTALLATION_GUIDE** § 6).

4. **Start** plugin; confirm **Connected**.

5. **Operate/Standby** and meters per **[USER_GUIDE.md](USER_GUIDE.md)**.

**Then for formal Phase 9:** **[TESTING_GUIDE.md](TESTING_GUIDE.md)** **Phase E** + **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** § **2** (with **§ Record build under test** including **RFKIT firmware**).

---

## Quick problems

| Symptom | Action |
|---------|--------|
| Connection failed | **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** |
| **127.0.0.1 invalid** | Use **LAN IPv4** + port **8080** for same-PC emulator |
| **8080 vs 1500** | **8080** = RFKIT REST; **1500** = sample Elecraft-style TCP |

---

## One-line answer: “Is Quick Start enough?”

**No** for full QA: use **§ Full test execution** above. **Yes** for a **smoke** check only — then still open **QA_TEST_PLAN** and **TESTING_GUIDE** before signing off any build.
