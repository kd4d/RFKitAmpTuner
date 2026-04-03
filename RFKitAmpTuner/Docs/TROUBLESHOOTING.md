# RFKIT Plugin — Troubleshooting

**Living document.** Symptom → likely cause → action. For **QA steps** see **[TESTING_GUIDE.md](TESTING_GUIDE.md)**.

| Revision | Date | Notes |
|----------|------|--------|
| 0.1 | 2026-03-20 | Phase 10: install, connection, meters, PTT, emulator. |
| 0.2 | 2026-03-22 | Startup HTTP capture file: path, default **60** s, **`0`** = off. |

---

## Installation & build

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| **CS / MSB** errors: cannot find **PgTg** | PgTgBridge not installed or wrong **HintPath** | Install PgTgBridge; confirm `C:\Program Files\PgTgBridge\bin\PgTg.dll` exists, or edit **`RFKitAmpTuner.csproj`** **HintPath** |
| **Access denied** copying DLL | **Program Files** protection | Run **Administrator** PowerShell; use **`scripts\Deploy-ToPgTgBridge.ps1`** |
| Plugin **not listed** in Plugin Manager | Wrong folder / name | Only **`RFKitAmpTuner.dll`** in **`...\PgTgBridge\plugins\`**; restart PgTgBridge |
| **dotnet test** fails to compile tests | Same as build — needs **PgTg** refs | **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** § 3.1 |

---

## Connection (emulator or hardware)

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| **Connection refused** / immediate failure | Emulator off; wrong IP; wrong port; firewall | Start **RfkitEmulator**; **`curl http://IP:8080/info`** from PC; check Windows Firewall |
| **127.0.0.1 … valid IP** (PgTg message) | Host blocks loopback | Use **LAN IPv4** of this PC + **8080** for same-machine emulator |
| UI shows **127.0.0.1:1500** for RFKIT | Wrong plugin row or old port | Select **RFKIT** / `rfpower.rfkit-amplifier-tuner`; set **8080**; **Save** |
| **Connected** but no REST traffic (emulator) | **`UseRfkitRestApi`** false (non-default build) or not using TCP REST path | Use default build; confirm **TCP** + REST per **INSTALLATION_GUIDE** |
| **SSL / HTTPS** errors | RFKIT default is **HTTP** | Use **`http://`** unless device is explicitly HTTPS |
| **404** on paths | Wrong service on port | Confirm process on **8080** is **RfkitEmulator** or RFKIT, not another app |

---

## Reconnect loop

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| Constant retries in logs | Device offline; bad IP; cable/Wi‑Fi | Fix network; confirm **`GET /info`** with **curl** |
| UI never says **Reconnecting** | PgTg limitation | Read **Info** logs: `RFKIT REST unreachable`, `retry in … ms` (**TESTING_GUIDE**) |

---

## Meters & frequency

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| **`-- MHz`** | No radio / VFO to PgTg | Connect radio per PgTgBridge; not an RFKIT-only bug |
| **0 W / SWR 1.0 in RX** | **By design** when not TX (KD4Z-style) | Brief legal TX to see power/SWR change; **temperature** should still move |
| Meters **stuck** in TX | REST failure; operate mode; hardware | Check PgTg logs; **GET /power** via browser/curl to device |

---

## PTT & safety

| Symptom | Expectation | Action |
|---------|-------------|--------|
| Software TX doesn’t key amp | **Hardware PTT** may be required | Normal per **USER_GUIDE** § 3; use hardware interlock |
| Host still sends TX commands | Host contract is **void** | Plugin uses **synthetic `$TX;`/`$RX;`** — no REST PTT |

---

## Emulator-specific

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| **Log spam** | **HttpLogging** every request | Lower log level in **`RfkitEmulator/appsettings.json`** |
| **MSB3026** / file in use | Emulator exe locked | Stop running instance; rebuild (**RfkitEmulator README**) |

---

## Startup HTTP capture (plugin debug file)

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| **No** `rfkit-http-capture-*.log` | **`RfkitStartupCaptureSeconds`** is **`0`** in **`SettingsConfig.json`**; capture window ended; REST path not used | Default in plugin config is **60** s; set **`0`** to disable. Confirm **TCP** + **`UseRfkitRestApi`**; see **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** § **5.4.1** |
| **Do not know where file is** | Path not obvious vs **`PgTg.log`** | Files are under **`%ProgramData%\PgTg\RfKitAmpTuner\`** (e.g. **`C:\ProgramData\PgTg\RfKitAmpTuner\`**). PgTg **Info** log line from **`RfkitStartupTrafficCapture`** shows full path when capture starts |
| **Huge** log / disk use | Fast polling for many minutes | Shorten duration (default **60** s) or set **`0`** after capture; max duration **7200** s (clamped) |

---

## Hardware-specific (Phase 9)

| Symptom | Likely cause | Action |
|---------|----------------|--------|
| Worked on emulator, fails on RFKIT | IP, port, firmware, VLAN | Confirm **`http://RFKIT_IP:8080/info`** from PC; check RFKIT network settings |
| **JSON** fields differ from doc | Firmware vs **0.9.0** OpenAPI | Capture response; file issue with **firmware version** |

---

## Related documents

| Document | Purpose |
|----------|---------|
| [INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md) | Full install |
| [USER_GUIDE.md](USER_GUIDE.md) | PTT, meters, settings |
| [TESTING_GUIDE.md](TESTING_GUIDE.md) | Step-by-step verification |
| [QA_TEST_PLAN.md](QA_TEST_PLAN.md) | Phase 8–10 sign-off |
