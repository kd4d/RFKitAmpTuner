# Option #1: RFKIT HTTP Plugin — Detailed Integration and Test Plan

**Repository:** This document ships in the **standalone RFKitAmpTuner** repository (plugin + tests + **RfkitEmulator**). It is **not** the KD4Z **PgTgSamplePlugins** monorepo — obtain KD4Z samples only from **[github.com/KD4Z/PgTgSamplePlugins](https://github.com/KD4Z/PgTgSamplePlugins)**. **Baseline `SampleAmpTuner` reference commit** and attribution: **[`ATTRIBUTION.md`](../../ATTRIBUTION.md)** at the repository root.

**Implementation project:** `RFKitAmpTuner/` in this repository. Do **not** fold RFKIT-specific transport into KD4Z **`SampleAmpTuner`**; maintain RFKIT code here and compare to upstream when rebaselining.

## 1. Scope and Objectives

**Goal:** Implement the PgTgBridge amplifier+tuner plugin for the RFKIT (RF2K-S) amplifier by replacing the sample’s stream-based transport (serial/TCP CAT) with **HTTP REST** calls to the [RFKIT API](https://rf-power.eu/wp-content/uploads/2024/12/RFKIT_api_doc_0_9_0.html) (default base URL `http://localhost:8080`).

**In scope:**

- Locked **implementation decisions** in **§ 2** (host not modifiable; compile-time init default off; hardware PTT).
- New connection implementation that satisfies `IRFKitAmpTunerConnection` and uses HTTP instead of a byte stream.
- Mapping of all existing “CAT” command strings and poll queries to RFKIT REST endpoints (or documented gaps).
- Configuration for base URL (host + port or full URL).
- Preserve existing plugin behavior: `CommandQueue`, `ResponseParser`, `StatusTracker`, and plugin entry points remain unchanged; only the transport is swapped.

**Out of scope (for this plan):**

- Supporting other amplifiers; this plan is specific to RFKIT 0.9.0.
- Changing PgTgBridge host application or plugin interfaces.

### 1.1 Upstream (KD4Z) maintenance

**PgTgSamplePlugins** on **[github.com/KD4Z/PgTgSamplePlugins](https://github.com/KD4Z/PgTgSamplePlugins)** will change over time. This repository does **not** auto-track it. When rebaselining:

1. Clone or fetch **KD4Z** `PgTgSamplePlugins` and compare **`SampleAmpTuner/`** (and host-facing patterns) to the **pinned baseline commit** in **[`ATTRIBUTION.md`](../../ATTRIBUTION.md)**.
2. Port deliberate changes into **`RFKitAmpTuner/`** as needed; re-run **build**, **`RFKitAmpTuner.Tests`**, and **QA_TEST_PLAN** Phases **8–9**.
3. Optionally update **`ATTRIBUTION.md`** with a **new** baseline SHA if you formally adopt a newer reference.

---

## 2. Implementation decisions and constraints (locked)

The following choices apply to the RFKIT HTTP plugin work and related documentation. They supersede earlier open questions where noted.

### 2.1 Host application source is not available

- **PgTgBridge** is distributed as an installed product (MSI); **host source code is not in** the PgTgSamplePlugins repository and is not assumed to be modifiable.
- Therefore **plugin behavior cannot rely on** new host UI fields, new `IPluginConfiguration` properties, or host command-line flags unless KD4Z adds them independently.
- **Configuration strategy:** Use **compile-time** flags in the plugin (see below) and **runtime** settings already supported by the host (TCP/Serial IP, port, reconnect delay, etc., repurposed for HTTP base URL where needed). Document all plugin-specific toggles in this repo’s README and user docs.

### 2.2 Device initialization handshake: compile-time flag, default **disabled**

- **`DeviceInitializationEnabled`** in `MyModel/Internal/Constants.cs` is the **single compile-time switch** for the wakeup / identify sequence (`$WKP;` / `$IDN;` and related `CommandQueue` logic).
- **Default: `false`** for the sample projects (aligned with the upstream PgTgSamplePlugins README and `Constants.cs` documentation). Polling starts immediately after connect—appropriate for RFKIT HTTP and many third-party devices that do not implement the fictitious serial handshake.
- Skilled users set **`true`** and **rebuild** the plugin DLL if a device requires the handshake.
- **RFKIT plugin:** Keep the same pattern unless RFKIT’s deployment explicitly requires init; if init is enabled, `RfkitHttpConnection` must still synthesize a response so `OnInitializationResponse` can complete (see **§ 4.1 Device Initialization** in the command-to-API mapping).

### 2.3 PTT: hardware-controlled; no “unimplemented” API to the host

- **Decision:** Actual **PTT keying is performed outside this plugin** (hardware interlock or other path). The plugin does **not** rely on RFKIT REST (or serial CAT) to assert or release PTT on the amplifier.
- **Host contract:** `IAmplifierPlugin.SendPriorityCommand(AmpCommand)` is **`void`**. There is **no documented** return value, status code, or host flag meaning “PTT unimplemented” or “hardware PTT only.” Public plugin documentation (e.g. `PgTgBridge-Plugin-Programmers-Reference.md`) treats software PTT as **safety-critical** and expected when an amplifier plugin is active.
- **Implementation guidance (plugin-internal, not a host API):**
  - **`SendPriorityCommand`:** Prefer a **no-op** (do not send `$TX15;` / `$RX;` over HTTP or serial) **or** a documented **synthetic acknowledgment** path (e.g. immediately raise parsed `$TX;` / `$RX;` via `DataReceived`) so the bridge’s **PttReady** / interlock timing does not stall—**only if** testing shows the host requires it. Risks of synthetic acks must be documented (possible mismatch with real RF state).
  - **Preferred state source:** Reflect transmit state from **RFKIT polling** (e.g. power / data / tuner JSON) when the API exposes “transmitting” or equivalent; merge with **`SetRadioPtt`** / meter logic already in `StatusTracker` where applicable.
  - **`SetRadioConnected(false)`:** Keep **safe behavior** (e.g. still clear local PTT/watchdog state per sample pattern); avoid sending amp keying commands that hardware already owns unless required for fault clearing—document the exact choice in the User Guide.
- **Documentation:** User Guide and Troubleshooting must state clearly that **PTT is hardware (or out-of-band)** and what the plugin does when the host sends TX/RX priority commands (no-op vs synthetic ack vs polled state). Emulator behavior should mirror the same contract for repeatable tests.

### 2.4 Summary table

| Topic | Decision |
|--------|-----------|
| Host code / new host settings | Not assumed; compile-time + existing host config only |
| Device init handshake | `DeviceInitializationEnabled`, default **`false`**, rebuild to enable |
| PTT | Hardware outside plugin; no official “unimplemented” to host; document internal handling |

---

## 3. Architecture Overview

### 3.1 Current plugin flow (RFKitAmpTuner)

1. **Plugin** (`RFKitAmpTunerPlugin`) chooses `SerialConnection` or `TcpConnection` from config, builds `CommandQueue`, `ResponseParser`, `StatusTracker`.
2. **CommandQueue** sends command strings (e.g. `$PWR;`, `$OPR1;`, `$TX15;`) via `IRFKitAmpTunerConnection.Send(string)` and consumes responses via `DataReceived`.
3. **Connection** delivers raw text to the plugin; `ResponseParser` parses `$KEY value;` lines and updates `StatusTracker`; plugin raises status/meter events.

### 3.2 Option #1 Flow

1. Plugin gains a **third connection type**: HTTP (e.g. `PluginConnectionType.HTTP` or a dedicated “RFKIT” type). When selected, plugin instantiates **RfkitHttpConnection** instead of `TcpConnection` or `SerialConnection`.
2. **RfkitHttpConnection** implements `IRFKitAmpTunerConnection`:
   - **StartAsync:** No persistent socket. “Connection” is established by verifying the API is reachable (e.g. `GET /info` or `GET /operate-mode`). On success, set `ConnectionState = Connected` and raise `ConnectionStateChanged`.
   - **Send(string):** Parse the incoming string into one or more semicolon-terminated commands (e.g. `$PWR;`, `$RX;$FLC;`). For each command, execute the corresponding REST call(s), then build the fake CAT response string(s) that `ResponseParser` expects. After processing all commands (or in a single batch), raise `DataReceived` once with the concatenated response (e.g. `$PWR 500 12; $OPR 1;`).
   - **IsConnected:** True when the last reachability check succeeded; optionally refresh periodically or on next Send failure.
   - **Stop/Dispose:** No stream to close; clear state and set `ConnectionState = Disconnected`.

3. **CommandQueue** and **ResponseParser** are unchanged: they still send the same strings and parse the same `$KEY value;` format. Only the source of the response string changes (REST + translation instead of serial/TCP).

### 3.3 Key Design Points

| Concern | Approach |
|--------|----------|
| Request/response model | Each `Send(data)` may contain multiple commands (e.g. `$RX;$FLC;`). Split by `;`, map each to REST, collect responses, concatenate, then invoke `DataReceived` once. |
| Thread safety | `Send()` may be called from timer thread; HTTP calls should be synchronous from that thread (or block until result). No background “receive” loop. |
| Connection state | “Connected” = API reachable. Optionally: background ping or treat first failed request as disconnect and set state to Reconnecting/Disconnected. |
| Initialization | **Default (§ 2.2):** `DeviceInitializationEnabled = false`—no `$WKP;`/`$IDN;` wait; HTTP connect + reachability only. If compile-time enabled, synthesize responses so `CommandQueue` init completes (see § 4.1). |
| Errors | On HTTP error (timeout, 5xx, 4xx): either return a safe default response (e.g. `$FLT 1;`) or invoke `DataReceived` with an error response so parser/tracker can record fault. |

---

## 4. Command-to-API Mapping

The following mapping is implemented in **`RfkitCommandMapper`** + **`RfkitCatFromJson`** (used by **`RfkitHttpConnection`**). Exact JSON property names follow the RFKIT OpenAPI spec checked in under **`Docs/api/`**; adjust if live hardware differs.

### 4.1 Device Initialization

| CAT command(s) | REST / behavior | Fake response for parser |
|----------------|------------------|---------------------------|
| `$WKP;` | Optional no-op or GET to check reachability | — |
| `$IDN;` | `GET /info` | `$IDN RFKIT;` (or use device name from `/info` if available). `$VER x.y;` from info if present. |

Init completes when the plugin receives any semicolon-terminated response; one response from `GET /info` is enough.

### 4.2 Operate / Standby (amplifier)

| CAT command | REST | Fake response |
|-------------|------|----------------|
| `$OPR1;` | `PUT /operate-mode` body `{"operate_mode": "OPERATE"}` | Optional: `$OPR 1;` |
| `$OPR0;` | `PUT /operate-mode` body `{"operate_mode": "STANDBY"}` | Optional: `$OPR 0;` |
| `$OPR;` (poll) | `GET /operate-mode` | `$OPR 1;` or `$OPR 0;` from JSON |

### 4.3 PTT (amplifier)

| CAT command | REST / behavior | Fake response |
|-------------|------------------|----------------|
| `$TX15;` | **Plan decision (§ 2.3):** PTT is **hardware-controlled**; do not key the amp via REST. Prefer **no-op** or **synthetic `$TX;`** only if host interlock testing requires it (document in User Guide). Do not assume an RFKIT PTT endpoint unless verified. | `$TX;` only if synthetic-ack path is chosen |
| `$RX;` | Same: **no-op** or synthetic `$RX;` if required for host state; not a substitute for hardware PTT. | `$RX;` if synthetic-ack path is chosen |

**Note:** The public RFKIT doc does not show an explicit PTT endpoint. This plan **does not** require REST PTT; align mapper and emulator with § **2.3** (hardware PTT, documented plugin-side handling).

### 4.4 Fault clear

| CAT command | REST | Fake response |
|-------------|------|----------------|
| `$FLC;` | `POST /error/reset` | `$FLT 0;` |

### 4.5 Frequency

| CAT command | REST / behavior | Fake response |
|-------------|------------------|----------------|
| `$FRQnnnnn;` | If RFKIT has a frequency or band endpoint: use it. Else: no-op and optionally respond `$FRQ nnnnn;` for band tracking. | Optional echo or skip. |

### 4.6 Tuner: bypass / inline / tune / antenna

| CAT command | REST / behavior | Fake response |
|-------------|------------------|----------------|
| `$BYPB;` | Map to RFKIT if available (e.g. operational-interface or tuner bypass). Else: no-op. | `$BYP B;` |
| `$BYPN;` | Same as above for “inline”. | `$BYP N;` |
| `$TUN;` | If API has “start tune”: use it. Else: no-op. | `$TPL 1;` then later `$TPL 0;` (may require polling /tuner). |
| `$TUS;` | If API has “stop tune”: use it. | `$TPL 0;` |
| `$ANT 1;` / `$ANT 2;` / `$ANT 3;` | `PUT /antennas/active` with body selecting antenna 1/2/3 (exact schema from OpenAPI). | Optional: `$ANT n;` |

### 4.7 Polling (single-command responses)

For each poll command, one GET (or combined GET) and build one or more `$KEY value;` lines:

| Poll command | REST | Fake response building |
|--------------|------|-------------------------|
| `$PWR;` | `GET /power` | From power JSON: forward power (W), SWR → `$PWR &lt;fwd&gt; &lt;swr*10&gt;;` (e.g. `$PWR 500 12;`). |
| `$TMP;` | `GET /data` or `/power` (if temp present) | `$TMP &lt;temp&gt;;` |
| `$OPR;` | `GET /operate-mode` | `$OPR 1;` or `$OPR 0;` |
| `$BND;` | From `/data` or band from API if present | `$BND n;` or default 0. |
| `$VLT;` | From `/data` or `/power` (voltage/current) | `$VLT vvv iii;` (e.g. *10 for parser). |
| `$BYP;` | From `GET /tuner` (or operational-interface) | `$BYP B;` or `$BYP N;` |
| `$TPL;` | From `GET /tuner` (tuning in progress?) | `$TPL 1;` or `$TPL 0;` |
| `$SWR;` | From `GET /tuner` or `/power` | `$SWR n.nn;` |
| `$FPW;` | From `/tuner` (forward power or ADC) | `$FPW &lt;value&gt;;` |
| `$IND;` / `$CAP;` | From `GET /tuner` (relay/inductor/capacitor state) | `$IND &lt;hex&gt;;` `$CAP &lt;hex&gt;;` |
| `$FLT;` | From `/data` or error state if available | `$FLT n;` |
| `$VER;` / `$SER;` | From `GET /info` | `$VER x.y;` `$SER &lt;id&gt;;` |

**Batching:** To reduce round-trips, when the plugin sends a single poll command (e.g. `$PWR;`), the connection can call only the needed endpoint. When the plugin sends multiple commands in one `Send()`, process each in order and concatenate responses. Optionally, a “batch” GET (e.g. call `/data`, `/power`, `/tuner` once and distribute to multiple fake responses) can be introduced later for efficiency.

---

## 5. Configuration and Plugin Wiring

### 5.1 Configuration

- **Existing:** `RFKitAmpTunerConfiguration` has `ConnectionType`, `IpAddress`, `Port` (TCP), `SerialPort`, `BaudRate`.
- **Add (recommended):**
  - **BaseUrl** (string): e.g. `http://192.168.1.100:8080` or `http://localhost:8080`. Used only when connection type is HTTP/RFKIT.
  - **Connection type:** Extend enum or add a third option (e.g. `PluginConnectionType.HTTP` or a plugin-specific “RFKIT”) so the UI can show “HTTP” instead of TCP/Serial when appropriate.

If you prefer not to extend the shared enum, use TCP with a special port or a convention (e.g. port 8080 + “use HTTP” checkbox) and treat that as HTTP; base URL = `http://{IpAddress}:{Port}`.

### 5.2 Plugin Initialization

In `RFKitAmpTunerPlugin.InitializeAsync`:

- When `ConnectionType == PluginConnectionType.HTTP` (or your chosen value), instantiate **RfkitHttpConnection** and configure it with `BaseUrl` (or `IpAddress` + `Port` for URL).
- Do **not** create `TcpConnection` or `SerialConnection` in that case.
- Rest of wiring (CommandQueue, Parser, StatusTracker, events) stays the same.

### 5.3 UI (PgTgBridge)

- If the host supports it, add a UI section for “HTTP” / “RFKIT”: base URL or host + port, and optionally a “Test connection” button that does `GET /info`.
- If not, use TCP UI with instructions: set IP to RFKIT host, port to 8080, and use a build-time or config flag to force HTTP behavior for this plugin.

### 5.4 Roadmap to final state (REST: emulator matches hardware)

**Final state (done when this work is complete):**

- **Physical RFKIT:** `RFKitAmpTuner` uses **`RfkitHttpConnection`** (not TCP/serial CAT to the amp) to call the **real RFKIT REST API** as deployed on the RF2K-S (same routes and JSON semantics as vendor documentation / live device).
- **Emulator:** A **standalone process** in this repo exposes the **same REST surface** as the hardware (§ **9.2**): same paths (`/info`, `/power`, `/data`, `/tuner`, `/operate-mode`, `/antennas/...`, `/operational-interface`, `/error/reset`, etc.), **stateful** responses, and **request/response logging** so behavior is debuggable and repeatable. The plugin cannot tell emulator vs. device except by URL and timing—**contract parity**, not RF power.
- **Docs:** Installation, User Guide, Emulator Guide, Troubleshooting, and READMEs describe **emulator-first testing**, then **cutover to the amplifier IP/host**, with **§ 2.3** (hardware PTT) called out in both environments.

**Why TCP/serial in the repo today is not the final state:** That path reuses the KD4Z **fictitious CAT** framing for scaffolding. The physical amplifier’s **documented** control path for this plan is **HTTP REST** (§ **3.2**, § **4**). Final validation is always against **real hardware**; the emulator exists to get there safely without RF.

---

**Ten phases** (each phase may span multiple PRs; order matters):

| Phase | Outcome | Primary references |
|------|---------|-------------------|
| **1** | **Canonical API contract** in-repo: OpenAPI and/or **golden JSON** samples per endpoint (from vendor file, `openapi.json` on device, or captured from hardware). | § **6** step 1; § **4** tables |
| **2** | **Emulator MVP:** project in solution; serves all required routes with **static** JSON (200 OK); proves routing and build/deploy story. | **`RfkitEmulator/`** in **`RFKitAmpTuner.sln`**; § **9**; § **10** build emulator |
| **3** | **Emulator “looks like hardware”:** **stateful** model (operate/standby, tuner, antennas, faults); GETs reflect PUTs/POSTs; **structured request + response logging**. | § **9.2**–**9.3** |
| **4** | **Plugin configuration** for HTTP: **`UseRfkitRestApi`**, **`HttpBaseUrl`**, **`GetEffectiveRfkitHttpBaseUri()`**; TCP UI = REST base URL when REST enabled (§ **5.1**). | **`RFKitAmpTunerConfiguration`**, **`RFKitAmpTunerPlugin.InitializeAsync`** |
| **5** | **`RfkitHttpConnection`:** reachability in `StartAsync`, `Send` → REST + synthetic CAT via **`RfkitCommandMapper`**. | § **6** step 3 |
| **6** | **`RfkitCommandMapper` + `RfkitCatFromJson`:** full § **4** mapping; **`RFKitAmpTuner.Tests`** (xUnit) with golden JSON + **`FakeRfkitRestClient`**. | § **6** step 4; § **7.1** |
| **7** | **Plugin wiring:** choose HTTP connection in `InitializeAsync`; **§ 2.2** init synthesis if enabled; **§ 2.3** PTT policy implemented and logged. | § **6** steps 5–8 |
| **8** | **Integration tests** and manual checklist **against emulator only** (PgTgBridge + plugin + emulator). | § **7.2**, § **7.5** |
| **9** | **Physical RFKIT:** same checklist against **real** base URL; fix mapping/edge cases; confirm meters and safety behavior. | § **7.3** |
| **10** | **Shipping docs:** `Installation.md` (or equivalent), User Guide, Setup, Emulator Guide, Troubleshooting; **README.md** (root + `RFKitAmpTuner/`) updated to final behavior. | § **10**–**11**; § **6** steps 10–11 |

**Granular ordered tasks** remain in § **6** (implementation checklist) and § **9** (emulator detail); the table above is the **phase gate** view.

---

**What is next (immediate action):**

- **Phase 1 (complete):** Authoritative OpenAPI is checked in under **`RFKitAmpTuner/Docs/api/`** (`swagger.json` from [RF-POWER `swagger.zip`](https://rf-power.eu/wp-content/uploads/2024/12/swagger.zip); see `Docs/api/README.md` for verification).
- **Phase 2 (complete):** **`RfkitEmulator/`** — ASP.NET Core host on **`http://0.0.0.0:8080`**, routes from OpenAPI; see `RfkitEmulator/README.md`.
- **Phase 3 (complete):** **Stateful** responses (operate mode, power, antennas, tuner coupling, error reset) + **`AddHttpLogging` / `UseHttpLogging`** and **`ILogger<EmulatorStateStore>`** (no custom log framework).
- **Phase 4 (complete):** **`RFKitAmpTunerConfiguration`**: **`UseRfkitRestApi`** (default true), **`HttpBaseUrl`**, **`GetEffectiveRfkitHttpBaseUri()`**, **`IsRfkitHttpTransportSelected()`**; plugin selects **`RfkitHttpConnection`** when TCP + REST.
- **Phase 6 (complete):** **`IRfkitRestClient`**, **`RfkitRestPaths`**, **`RfkitCatFromJson`**, **`RfkitCommandMapper`**; HTTP connection delegates per-command mapping to the mapper; unit tests in **`RFKitAmpTuner.Tests/`** (`dotnet test RFKitAmpTuner.Tests/RFKitAmpTuner.Tests.csproj`).
- **Phases 8–10 (documentation / QA handoff):** Executable checklists and operator docs live under **`RFKitAmpTuner/Docs/`**:
  - **Phase 8:** **[TESTING_GUIDE.md](TESTING_GUIDE.md)** Phases **A0–D** + **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** § 1 (emulator + PgTg).
  - **Phase 9:** **TESTING_GUIDE.md** **Phase E** + **QA_TEST_PLAN.md** § 2 (physical RFKIT + radio).
  - **Phase 10:** **QA_TEST_PLAN.md** § 3 + **[USER_GUIDE.md](USER_GUIDE.md)**, **[QUICK_START.md](QUICK_START.md)**, **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)**, **[INSTALLATION_GUIDE.md](INSTALLATION_GUIDE.md)** (shipping / support set). **Emulator “user guide”:** **[RfkitEmulator/README.md](../../RfkitEmulator/README.md)**.

---

## 6. Implementation Steps (Ordered)

1. **Obtain RFKIT API schema**  
   Get OpenAPI (JSON/YAML) for RFKIT 0.9.0 from vendor or by inspecting `http://<rfkit>/openapi.json` (or similar). Confirm exact property names for Power, Data, Tuner, Info, OperateMode, Antennas, Error.

2. **Add configuration**  
   Add `BaseUrl` (and optionally `PluginConnectionType.HTTP`) to `RFKitAmpTunerConfiguration`. If the host does not support a new enum value, document “use TCP with port 8080 and HTTP mode” and add a plugin-specific flag.

3. **Create RfkitHttpConnection**  
   - Implement `IRFKitAmpTunerConnection`.  
   - Constructor/config: accept base URL and cancellation token.  
   - **StartAsync:** Call `GET /info` (or `/operate-mode`). On success (2xx), set Connected and raise `ConnectionStateChanged`. On failure, set Reconnecting/Disconnected and optionally retry after delay (replicate existing reconnect behavior if desired).  
   - **Send(string):** Split by `;`, trim, ignore empty. For each token that looks like a command (e.g. starts with `$`), call **RfkitCommandMapper** to get REST method + URL + body and optional response string. Use `HttpClient` (or generated client) to perform the request. Build the full response string from mapper outputs, then invoke `DataReceived` on the same thread (or marshal to the same sync context if required).  
   - **IsConnected:** Return result of last reachability check; optionally set to false on first Send failure and trigger reconnect.  
   - **Stop** / **Dispose:** Set state to Disconnected, clear handlers.

4. **Implement RfkitCommandMapper**  
   - Input: one CAT command string (e.g. `$PWR;`, `$OPR1;`).  
   - Output: (HTTP method, path, body?), and optionally the fake CAT response string if the mapper will do the HTTP call and parsing internally. Alternatively, mapper only returns (method, path, body); connection executes HTTP and then calls a **ResponseBuilder** to convert JSON → `$KEY value;` using the mapping tables above.  
   - Implement all rows from the command-to-API mapping. For endpoints that return JSON, parse and map to the exact `$KEY value;` format that `ResponseParser` expects (see Constants and ResponseParser).

5. **Wire plugin to RfkitHttpConnection**  
   In `RFKitAmpTunerPlugin.InitializeAsync`, when config indicates HTTP/RFKIT, create and configure `RfkitHttpConnection`, assign to `_connection`, and use it for CommandQueue and events. Ensure no TCP/Serial code path runs.

6. **Handle init sequence**  
   With **`DeviceInitializationEnabled = false`** (plan default, § 2.2), no synthetic init response is required for startup. If a build sets **`true`**, ensure `$WKP;$IDN;` (or retry path) yields at least one response containing `;` (e.g. `$IDN RFKIT;`) so `CommandQueue.OnInitializationResponse` completes.

7. **Reconnect and errors**  
   On HTTP timeout or non-2xx, set connection state and optionally retry (e.g. same as TcpConnection reconnect loop). Optionally push a synthetic `$FLT 1;` (or similar) so the host can show a fault.

8. **Constants / device identity**  
   If desired, introduce `Constants.RfkitDeviceId` or use `$IDN RFKIT;` so the plugin identity remains consistent. Keep existing Constants for response keys so ResponseParser is unchanged.

9. **Build and deliver RFKIT Emulator**  
   Implement the emulator per Section 9; add to solution or repo; document build/run in Installation Instructions.

10. **Generate Installation Instructions**  
    Produce the detailed installation document per Section 10 (INSTALL.md or equivalent).

11. **Generate User Documentation**  
    Produce the user-facing documents per Section 11 (User Guide, Setup, Emulator Guide, etc.).

---

## 7. Test Plan

### 7.1 Unit / Component Tests (no hardware)

- **RfkitCommandMapper**
  - For each CAT command in the mapping table, assert correct HTTP method, path, and (where applicable) request body.
  - For poll commands, given a stub JSON response (e.g. Power, Tuner, OperateMode), assert the produced `$KEY value;` string matches what `ResponseParser` expects (run through `ResponseParser.Parse` and check StatusUpdate fields).
- **ResponseBuilder (if used)**
  - Given sample JSON from each endpoint (Power, Data, Tuner, Info, OperateMode), assert output strings are well-formed and parseable by `ResponseParser`.

### 7.2 Integration Tests (RFKIT device or mock server)

- **Mock HTTP server:** Implement a minimal REST server (e.g. in C# or Python) that exposes the same paths as RFKIT and returns static or simple dynamic JSON. Run the plugin against this server.
  - Verify startup: with **`DeviceInitializationEnabled = false`** (§ 2.2), plugin connects after `GET /info` (or chosen reachability check) **without** requiring `$WKP;$IDN;` responses. With init **enabled** in a test build, verify `$WKP;$IDN;` → 200 + fake `$IDN ...;` completes `OnInitializationResponse`.
  - Verify operate/standby: send `$OPR1;` / `$OPR0;`, assert PUT to `/operate-mode` with correct body.
  - Verify poll: send `$PWR;`, assert GET `/power` and response string contains `$PWR ...`.
  - Verify **PTT path (§ 2.3):** With hardware PTT, confirm `$TX15;`/`$RX;` either no-op at REST layer or match chosen synthetic-ack behavior; emulator and mapper stay consistent with User Guide.
  - Verify fault clear: `$FLC;` → POST `/error/reset`.
- **Live RFKIT (if available):**
  - Configure plugin with base URL of the device. Start PgTgBridge, load plugin, start connection.
  - Check: connection state becomes Connected; operate/standby toggles the unit; power/tuner polls return plausible values; meters update in the host UI.

### 7.3 Manual Test Checklist (with PgTgBridge)

1. Install PgTgBridge and deploy the built plugin DLL to the plugin directory.
2. In Plugin Manager, add/select the RFKIT plugin; set connection type to HTTP and base URL to `http://<RFKIT_IP>:8080`.
3. Start the plugin; confirm “Connected” and that no errors appear in logs.
4. Operate/Standby: switch to Operate and Standby from the host UI; confirm the amplifier follows.
5. **PTT (§ 2.3):** With hardware PTT, confirm meters / status match **expected** behavior (polled RFKIT state ± synthetic ack). Document any limitation if host **PttReady** / latency metrics are N/A or misleading.
6. Tuner: if the device has a tuner, trigger tune from the host and confirm tuning state and SWR/relay values update.
7. Disconnect: power off RFKIT or block network; confirm plugin goes to Disconnected/Reconnecting and, after reconnect delay, retries.
8. Stop plugin and restart; confirm clean reconnect and no duplicate events or leaks.

### 7.4 Regression

- Optionally run SampleAmpTuner sample tests unchanged in this repo to ensure the reference sample still builds.
- Optionally keep a “dual” build or config so the sample still supports TCP/Serial for other devices.

### 7.5 Testing with the RFKIT Emulator

- Run the **RFKIT Emulator** (Section 9) on the host (e.g. `http://localhost:8080`). Configure the plugin with that base URL.
- Confirm all integration and manual tests pass against the emulator. Use emulator logs to verify every REST call and response the plugin produces.

---

## 8. Dependencies and Deliverables

- **Dependencies:** .NET 10 (per sample); `System.Net.Http.HttpClient` (or generated OpenAPI client for C#). No new NuGet is strictly required if using hand-written HTTP and JSON parsing (e.g. `System.Text.Json`).
- **Governance:** Implementation must comply with **§ 2** (host not modifiable; compile-time init default off; hardware PTT; no host “unimplemented” API).
- **Deliverables:**  
  - `RfkitHttpConnection.cs` (implements `IRFKitAmpTunerConnection`).  
  - `RfkitCommandMapper.cs` (and optionally `RfkitResponseBuilder.cs`) for command → REST and JSON → CAT response.  
  - Configuration and plugin wiring changes in `RFKitAmpTunerPlugin.cs` and `RFKitAmpTunerConfiguration.cs`.  
  - **RFKIT Emulator** (standalone service; see Section 9).  
  - Unit and integration tests as above.  
  - **Installation Instructions** (Section 10).  
  - **User Documentation** (Section 11).  
  - Short “RFKIT setup” note in README or Documentation: base URL, port 8080, and any PTT/band limitations.

---

## 9. RFKIT Amplifier Emulator

### 9.1 Purpose

- Provide a **software stand-in** for the RFKIT (RF2K-S) amplifier that implements the same REST API (RFKIT 0.9.0).
- **Log** every incoming request (method, path, query, body) and every response (status, body) for debugging and test verification.
- Return **appropriate, consistent responses** so the PgTgBridge RFKIT plugin can run end-to-end without hardware (development, CI, and user demos).

### 9.2 Behavior

- **API surface:** Implement the same endpoints as the [RFKIT API doc](https://rf-power.eu/wp-content/uploads/2024/12/RFKIT_api_doc_0_9_0.html):
  - **Antennas:** GET/PUT `/antennas/active`, GET `/antennas`.
  - **Data:** GET `/data`.
  - **Error:** POST `/error/reset`.
  - **Info:** GET `/info`.
  - **OperateMode:** GET/PUT `/operate-mode`.
  - **OperationalInterface:** GET/PUT `/operational-interface`.
  - **Power:** GET `/power`.
  - **Tuner:** GET `/tuner`.
- **Stateful behavior:** The emulator maintains internal state (e.g. operate/standby, active antenna, tuner bypass/inline, tuning in progress, fault cleared). PUT/POST requests update this state; GET requests return JSON derived from current state so the plugin sees realistic values (e.g. forward power and SWR that vary with operate mode or a simple simulation).
- **Logging:**
  - **Request log:** For each request, log timestamp, method, path, and (if present) body. Optionally log client IP or User-Agent.
  - **Response log:** Log status code and response body (or a short summary for large bodies). Logs should be easy to correlate with requests (e.g. same line or sequential lines per request/response).
  - **Output:** Console and/or file. Support a configurable log path and optional verbose vs. concise mode. Optionally support a “session” log file that is overwritten per run for quick inspection.
- **Responses:** JSON payloads must match the structure the plugin’s RfkitResponseBuilder expects (property names and types consistent with RFKIT 0.9.0). Use the OpenAPI spec or the RFKIT doc to define DTOs. For values (power, SWR, temperature, voltage, current, tuner state, etc.), use plausible constants or simple rules (e.g. operate=1 → non-zero power; standby=0 → zero power) so meters and status in PgTgBridge update correctly.

### 9.3 Implementation Options

- **C# (ASP.NET Core / minimal API):** Single solution with the plugin; same language; easy to share DTOs or OpenAPI-generated models. Run as a console app or Windows service.
- **Python (FastAPI / Flask):** Good if the team prefers Python; implement same routes and JSON; run with `python -m uvicorn` or similar. Logging via Python logging to file and console.
- **Node.js (Express):** Same idea; implement routes and JSON; log with middleware.

Recommendation: **C#** so the emulator can live in the same repo/solution as the plugin and reuse any shared API models.

### 9.4 Build and Run

- **Build:** Emulator is a separate project (e.g. `RfkitEmulator` or `Tools/RfkitEmulator`). Build with the same toolchain as the plugin (e.g. `dotnet build`). No dependency on PgTg DLLs.
- **Run:** Executable or `dotnet run`; default listen URL `http://localhost:8080` (or configurable via command-line or `appsettings.json`). Document in Installation Instructions and User Documentation.
- **Deployment:** Users can run the emulator on the same machine as PgTgBridge (localhost) or on another machine (set plugin base URL to `http://<emulator-host>:8080`).

### 9.5 Deliverables

- Emulator project (source and build output).
- Default configuration (port 8080, log path).
- Brief “Emulator User Guide” (Section 11) describing how to run it, where logs go, and how to use it with the plugin for testing.

---

## 10. Installation Instructions (To Be Generated)

The following **detailed installation instructions** shall be generated and maintained as a single document (e.g. `Docs/Installation.md` or `INSTALL.md` in the repo root). Target audience: developers and advanced users who build the plugin and/or emulator from source.

### 10.1 Contents to Include

1. **Prerequisites**
   - Windows 10/11 (64-bit).
   - .NET 10 SDK (or version required by the sample; link to official download).
   - Visual Studio 2022 or 2026 (or VS Build Tools) with workload for .NET desktop development, if building from IDE. Alternatively, command-line build with `dotnet` only.
   - (Optional) Python 3.x if any tooling or alternate emulator is Python-based.

2. **Installing PgTgBridge (main program)**
   - Download the MSI installer from the official release location, e.g.:  
     `https://releases.kd4z.com/PgTgBridge-1.26.999.1.msi`  
     (Replace with current version/link as appropriate.)
   - Run the MSI; accept license; choose install path (default e.g. `C:\Program Files\PgTgBridge`).
   - Note the installation path; the **plugin directory** is typically `<InstallPath>\plugins` or as documented in PgTgBridge.
   - Confirm PgTgBridge runs and that the Plugin Manager shows no plugins until you add the RFKIT plugin DLL.

3. **Obtaining the plugin and emulator source**
   - Clone or download the **RFKitAmpTuner** repository (this tree: plugin + **RfkitEmulator** + tests). For KD4Z **samples** only, clone **[KD4Z PgTgSamplePlugins](https://github.com/KD4Z/PgTgSamplePlugins)** separately — see **[`ATTRIBUTION.md`](../../ATTRIBUTION.md)**.
   - Open **`RFKitAmpTuner.sln`** in Visual Studio, or from the repo root: `dotnet build RFKitAmpTuner.sln`.

4. **Building the RFKIT plugin**
   - Ensure the solution references the PgTgBridge assemblies (e.g. `PgTg.dll`, `PgTg.Common.dll`, `PgTg.Helpers.dll`) from the PgTgBridge install directory (see sample’s `.csproj` HintPath).
   - Build the RFKIT plugin project:  
     `dotnet build RFKitAmpTuner/RFKitAmpTuner.csproj`  
     or Build Solution in VS.
   - Output: e.g. `RFKitAmpTuner/bin/Debug/net10.0/RFKitAmpTuner.dll` (or Release). Note the full path.

5. **Building the RFKIT Emulator**
   - Build the emulator project:  
     `dotnet build RfkitEmulator/RfkitEmulator.csproj`  
     (or the actual project path under `Tools/` or repo root.)
   - Output: executable or DLL under `RfkitEmulator/bin/Debug/net10.0/` (or Release). Note the path for running.

6. **Deploying the plugin to PgTgBridge**
   - Copy the built plugin DLL (e.g. `RFKitAmpTuner.dll`) to the PgTgBridge plugin directory (e.g. `C:\Program Files\PgTgBridge\plugins`).
   - Do not copy the sample TCP/Serial-only DLL if you have a separate RFKIT plugin build; copy the DLL that includes `RfkitHttpConnection` and RFKIT configuration.
   - Restart PgTgBridge if it is running; open Plugin Manager and confirm the RFKIT (or Sample Amplifier+Tuner with HTTP) plugin appears and can be selected.

7. **Running the RFKIT Emulator (for testing without hardware)**
   - From the emulator output directory:  
     `dotnet run --project RfkitEmulator`  
     or run `RfkitEmulator.exe`.
   - Default: listens on `http://localhost:8080`. Optionally specify port or config file (document in Emulator User Guide).
   - Confirm the emulator logs “Listening on …” and that a browser or `curl` to `http://localhost:8080/info` returns JSON.

8. **Configuring the plugin in PgTgBridge**
   - In Plugin Manager, add or select the RFKIT plugin. Set connection type to HTTP (or the option that uses `RfkitHttpConnection`).
   - Set base URL to `http://localhost:8080` when using the emulator on the same machine, or `http://<IP>:8080` for a real RFKIT or emulator on another machine.
   - Save configuration and start the plugin. Verify “Connected” and that meters/status update when using the emulator.

9. **Verification**
   - Short checklist: plugin loads, connection state Connected, operate/standby toggles, poll data (power, SWR, etc.) visible, no errors in PgTgBridge or emulator logs.

10. **Troubleshooting (installation-specific)**
    - Plugin not discovered: check DLL is in the correct plugin folder and that PgTgBridge version matches the plugin’s referenced PgTg version.
    - Build errors (missing PgTg references): confirm PgTgBridge is installed and HintPath in `.csproj` points to the correct install path.
    - Emulator port in use: change emulator port and plugin base URL accordingly.
    - Link to full Troubleshooting in User Documentation (Section 11).

### 10.2 Format and Location

- **Format:** Markdown (e.g. `Docs/Installation.md` or `INSTALL.md`). Optionally generate a PDF from the same source for distribution.
- **Location:** Repository root or `Docs/` folder; link from main README.

---

## 11. User Documentation (To Be Generated)

The following **user-facing documents** shall be generated and maintained. They can be written in Markdown and optionally exported to PDF.

### 11.1 Documents to Create

1. **RFKIT Plugin User Guide**
   - Overview: what the plugin does (control RFKIT amplifier and tuner via PgTgBridge over HTTP).
   - Prerequisites: PgTgBridge installed, RFKIT (or emulator) reachable on the network.
   - Connecting: how to configure base URL, connection type, and verify connection.
   - Operate and Standby: how to switch the amplifier on/off from the host.
   - **PTT and metering (required by § 2.3):** State explicitly that **amplifier PTT is hardware (or out-of-band)**; the host may still call `SendPriorityCommand`—document whether the plugin **no-ops**, uses **synthetic ack**, and/or derives TX state from **RFKIT polling**. Explain impact on **PttReady** / latency logging if applicable. Forward power, SWR, and other meters from REST polling.
   - Tuner: bypass/inline, start/stop tune, antenna selection (if supported).
   - Safety and warnings: radio disconnect forces RX; do not rely on emulator for real RF.
   - Where to find logs (PgTgBridge logs, plugin logs if any).

2. **Setup Guide (Quick Start)**
   - Short path: install PgTgBridge (MSI) → install/build plugin → deploy DLL → configure plugin (HTTP, base URL) → start plugin. Optionally: run emulator for testing.
   - Reference to full Installation Instructions (Section 10) for build-from-source steps.
   - Note **`DeviceInitializationEnabled`** default **`false`** (compile-time); rebuild if enabling init handshake (§ 2.2).

3. **RFKIT Emulator User Guide**
   - Purpose: testing without hardware; development; demos.
   - How to run: command line, default port (8080), optional config (port, log path).
   - Logging: where logs are written (console, file path); log format (request/response); how to use logs to verify plugin behavior.
   - State simulation: what the emulator simulates (operate/standby, power, SWR, tuner); that it is not a substitute for real hardware for RF safety.
   - Using with PgTgBridge: set plugin base URL to emulator address; start emulator before starting the plugin.
   - Emulator should **not** simulate software PTT via REST unless testing that path; align with § **2.3** for hardware PTT contract.

4. **Troubleshooting**
   - Connection fails: check base URL, firewall, RFKIT/emulator running.
   - Plugin not loading: DLL location, PgTgBridge version, .NET version.
   - No meter data: check operate mode, PTT (if applicable), and emulator/device logs.
   - Errors in logs: common error messages and resolutions; link to support or issue tracker.

### 11.2 Format and Location

- **Format:** Markdown under `Docs/` (e.g. `Docs/UserGuide.md`, `Docs/SetupGuide.md`, `Docs/EmulatorUserGuide.md`, `Docs/Troubleshooting.md`). Optionally generate PDFs (e.g. via Pandoc, MkDocs, or CI) for release packages.
- **Location:** Repository `Docs/` folder; link from README. If the plugin is distributed separately, include the same docs in the distribution or a dedicated docs URL.

### 11.3 Generation and Maintenance

- Documents are maintained as Markdown in the repo. Any “generation” step (e.g. PDF export) can be scripted (e.g. `scripts/generate-docs.ps1` or `docs/build-pdf.sh`) and documented in the README or Installation Instructions.
- Keep User Guide, Setup Guide, Emulator Guide, and Troubleshooting in sync with plugin and emulator behavior (e.g. when new options or limitations are added).

---

## 12. Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| RFKIT API has no explicit PTT | **Accepted (§ 2.3):** PTT is hardware; plugin does not require REST PTT. Document no-op vs synthetic ack; derive TX/meters from polling where possible. |
| Host cannot report “PTT unimplemented” | **Accepted:** `SendPriorityCommand` is `void`; document behavior in User Guide / Troubleshooting (§ 2.3). |
| Init handshake mismatch with RFKIT | **Default off (§ 2.2):** `DeviceInitializationEnabled = false`; enable compile-time only if needed. |
| JSON property names differ from doc | Obtain OpenAPI spec or capture live responses; adjust mapping and ResponseBuilder. |
| Rate limiting / latency | Respect existing plugin polling intervals; avoid extra batch calls that could overwhelm the device until proven safe. |
| Connection state “stale” (e.g. device power cycle) | On first Send failure, set Disconnected and run reconnect logic; optional periodic GET /info to refresh state. |
| Plugin host only offers TCP/Serial UI | Use TCP configuration with convention (port 8080 + HTTP) and document; or contribute a small UI extension for HTTP base URL. |
| Emulator logs grow large | Rotate or limit log file size; document in Emulator User Guide. |

---

## 13. Summary

- **Roadmap (§ 5.4):** **Ten phases** from canonical API spec through **stateful REST emulator**, **HTTP plugin**, **mapper unit tests (Phase 6)**, **emulator validation (Phase 8)**, **physical RFKIT validation (Phase 9)**, and **shipping documentation (Phase 10)**. **QA / production procedures** are centralized in **[QA_TEST_PLAN.md](QA_TEST_PLAN.md)** with step detail in **[TESTING_GUIDE.md](TESTING_GUIDE.md)**.
- **Decisions (§ 2):** PgTgBridge host source is **not** assumed available—no reliance on new host config. **`DeviceInitializationEnabled`** defaults **`false`** (compile-time, rebuild to enable). **PTT is hardware** outside the plugin; there is **no** host API for “unimplemented”—document no-op / synthetic ack / polled state in user-facing docs and tests.
- **What:** New `RfkitHttpConnection` + command/response mapping layer so the RFKitAmpTuner plugin talks to RFKIT over REST instead of serial/TCP, without changing CommandQueue, ResponseParser, or StatusTracker. **RFKIT Emulator** provides a test double with full request/response logging and appropriate responses. **Installation Instructions** and **User Documentation** provide detailed setup and usage.
- **How:** Implement `IRFKitAmpTunerConnection` with HTTP; on each `Send()`, translate CAT commands to REST calls and fake CAT responses; wire plugin to use this connection when configured for HTTP/RFKIT. Implement emulator as a separate service implementing the RFKIT API and logging. Generate and maintain Installation Instructions (Markdown) and User Guide, Setup Guide, Emulator User Guide, and Troubleshooting (Markdown, optional PDF).
- **Test:** Unit tests for mapper/response builder; integration tests against emulator and (if available) real RFKIT; manual checklist with PgTgBridge. Use emulator logs to verify all REST traffic. Validate **§ 2.3** PTT and **§ 2.2** init defaults explicitly.

This plan is the blueprint for Option #1 for the RFKIT amplifier in the **standalone RFKitAmpTuner** repository (plugin + emulator + docs), under the locked decisions in **§ 2**. KD4Z **PgTgSamplePlugins** remains the upstream reference for the **SampleAmpTuner** pattern only.
