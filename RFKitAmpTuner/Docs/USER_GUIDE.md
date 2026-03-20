# RFKIT RF2K-S Plugin — User Guide (PgTgBridge)

**Audience:** Operators and advanced users running **RFKitAmpTuner** with **PgTgBridge**.  
**API details:** [RFKIT REST API v0.9.0](https://rf-power.eu/wp-content/uploads/2024/12/RFKIT_api_doc_0_9_0.html) · OpenAPI in-repo: **`Docs/api/`**.

| Revision | Date | Notes |
|----------|------|--------|
| 0.1 | 2026-03-20 | Phase 10: connection, REST, PTT, meters, safety. |

---

## 1. What this plugin does

- Controls **RF-POWER RFKIT (RF2K-S)** **amplifier + tuner** from **PgTgBridge** using **HTTP REST** (default), not serial CAT to the amp.
- **Plugin ID:** `rfpower.rfkit-amplifier-tuner`.
- The host still shows **TCP** + **IP + port**; for RFKIT that pair means **`http://IP:port/`** when REST mode is on (default).

---

## 2. Connection settings (Plugin Manager)

| Setting | Typical value |
|---------|----------------|
| **Enabled** | Yes |
| **Transport** | **TCP** (required for REST URL mapping) |
| **IP** | **RFKIT** LAN address, **or** PC LAN address if using **RfkitEmulator** on same machine |
| **Port** | **8080** (vendor default; change only if your device is configured otherwise) |

**Important:**

- **Do not use port 1500** for RFKIT REST — that matches **sample Elecraft** profiles, not RFKIT HTTP.
- If PgTg rejects **127.0.0.1**, use your PC’s **non-loopback IPv4** with the emulator (emulator listens on **0.0.0.0:8080**).

**Override URL:** If your host exposes **`HttpBaseUrl`**, a non-empty value replaces `http://{IpAddress}:{Port}/`. See **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** § 6.

---

## 3. PTT (transmit) — read this

**Per integration plan § 2.3:**

- **Amplifier PTT may be hardware-only** (foot switch, interlock, etc.). The **RFKIT public API** does not provide a documented software PTT endpoint used by this plugin.
- When PgTg sends **TX/RX priority** commands, the plugin **does not** key the amp over REST. It **synthesizes** the CAT-style responses **`$TX;`** and **`$RX;`** so internal **CommandQueue** / **ResponseParser** flow matches the KD4Z sample pattern **without** sending keying commands to the device over HTTP.
- **Transmit state in the UI** can still reflect **radio interlock** (**RadioPtt**) and polled device status where available.

**You must not assume** that clicking TX in software has the same effect as hardware keying. Follow **RF-POWER** safety guidance and your station’s interlocks.

---

## 4. Meters and frequency display

- **`-- MHz` or empty frequency:** Usually means the **host has no radio VFO** for that line (e.g. radio not connected to PgTg). This is **not** fixed by the RFKIT plugin alone — connect the radio per **PgTgBridge** / Flex documentation.
- **Forward power / SWR in receive:** The plugin follows KD4Z-style behavior: **0 W** forward and **1.0** SWR when **not transmitting** may be **normal**. **Temperature** (`$TMP`) should still update from REST when polling works.
- **While transmitting** (legal, safe setup): forward power and SWR should reflect **GET /power** (and related) from the RFKIT.

---

## 5. Operate and standby

- Use PgTg / SmartSDR controls for **Operate** and **Standby** as provided for your setup.
- The plugin maps these to **`PUT /operate-mode`** with body **`OPERATE`** or **`STANDBY`** (RFKIT OpenAPI 0.9.0).
- Confirm **physical** amplifier state when possible.

---

## 6. Tuner, antennas, faults

- **Tuner / bypass / tune** commands map per **[RFKIT_Option1_Integration_And_Test_Plan.md](RFKIT_Option1_Integration_And_Test_Plan.md)** § 4.6–4.7.
- **Fault clear** maps to **`POST /error/reset`**; the parser sees **`$FLT 0;`** on success path.

---

## 7. Reconnect behavior

- If the RFKIT becomes unreachable (network, power, wrong IP), the plugin enters a **reconnect** loop (interval from **ReconnectDelayMs** in Plugin Manager).
- Some PgTg builds **do not** show “Reconnecting” in the UI — check **PgTgController** **Info** logs for RFKIT REST messages.

---

## 8. Device initialization (advanced)

- **`DeviceInitializationEnabled`** is a **compile-time** flag in plugin source (**`Constants.cs`**), default **`false`**. If **`true`**, a rebuild is required. When **false**, startup does not wait on a serial-style **`$WKP;$IDN;`** handshake.

---

## 9. Testing without hardware

- Run **RfkitEmulator** on **port 8080** and point the plugin at **`http://<host>:8080/`**. See **[RfkitEmulator/README.md](../../RfkitEmulator/README.md)**.

---

## 10. Where to get help

| Topic | Document |
|-------|----------|
| Install / deploy | **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** |
| Problems | **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** |
| QA procedures | **[TESTING_GUIDE.md](TESTING_GUIDE.md)**, **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** |
| Fast path | **[QUICK_START.md](QUICK_START.md)** |

**Vendor:** [rf-power.eu](https://rf-power.eu) — hardware, firmware, and RF safety.
