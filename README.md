# RFKitAmpTuner

**PgTgBridge** third-party plugin for the **RF-POWER RFKIT (RF2K-S)** amplifier and tuner, plus **RfkitEmulator** (REST API test double).

| | |
|---|---|
| **Plugin ID** | `rfpower.rfkit-amplifier-tuner` |
| **Maintainer org (GitHub)** | KD4D (private) |
| **Upstream pattern** | KD4Z **SampleAmpTuner** — see **[ATTRIBUTION.md](ATTRIBUTION.md)** |

## Repository layout

| Path | Purpose |
|------|---------|
| **`RFKitAmpTuner/`** | Plugin source, `Docs/`, deploy script |
| **`RFKitAmpTuner.Tests/`** | xUnit tests (mapper / JSON→CAT) |
| **`RfkitEmulator/`** | Stateful RFKIT OpenAPI **0.9.0** emulator (port **8080** default) |
| **`RFKitAmpTuner.sln`** | Build all three projects |

## Prerequisites

- **Windows 10/11**, **[PgTgBridge](https://www.kd4z.com/downloads)** installed (for `PgTg*.dll` references at build time and runtime).
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** for build, emulator, and tests.

## Build

From **this repository root**:

```powershell
dotnet build RFKitAmpTuner.sln -c Release
dotnet test RFKitAmpTuner.Tests\RFKitAmpTuner.Tests.csproj -c Release
```

## Documentation (start here)

| Doc | Purpose |
|-----|---------|
| **[RFKitAmpTuner/README.md](RFKitAmpTuner/README.md)** | Plugin overview, configuration |
| **[RFKitAmpTuner/Docs/QUICK_START.md](RFKitAmpTuner/Docs/QUICK_START.md)** | Fast path + QA document map |
| **[RFKitAmpTuner/Docs/INSTALLATION_GUIDE.md](RFKitAmpTuner/Docs/INSTALLATION_GUIDE.md)** | Install PgTg, deploy DLL, Plugin Manager |
| **[RFKitAmpTuner/Docs/TESTING_GUIDE.md](RFKitAmpTuner/Docs/TESTING_GUIDE.md)** | Phase A0–E procedures |
| **[RFKitAmpTuner/Docs/QA_TEST_PLAN.md](RFKitAmpTuner/Docs/QA_TEST_PLAN.md)** | Phase 8–10 sign-off |
| **[ATTRIBUTION.md](ATTRIBUTION.md)** | KD4Z baseline commit + trademarks |

## Emulator

```powershell
dotnet run --project RfkitEmulator\RfkitEmulator.csproj
```

Details: **[RfkitEmulator/README.md](RfkitEmulator/README.md)**. For **startup HTTP capture** from the plugin (default **60** s, **0** = off; e.g. **600**), see **[RFKitAmpTuner/Docs/INSTALLATION_GUIDE.md](RFKitAmpTuner/Docs/INSTALLATION_GUIDE.md)** § 5.4.1.

## Git (new clone)

This tree is intended as a **fresh** root for `git init` and your **KD4D** remote. **Do not** set `origin` to KD4Z’s `PgTgSamplePlugins` unless you intend read-only `fetch`; pushes go to **your** private repo.

```powershell
cd <path-to>\RFKitAmpTuner
git init
git add .
git commit -m "Initial import: RFKitAmpTuner + Tests + RfkitEmulator"
git remote add origin https://github.com/KD4D/RFKitAmpTuner.git
git push -u origin main
```

Replace the remote URL with your actual **KD4D** repository.
