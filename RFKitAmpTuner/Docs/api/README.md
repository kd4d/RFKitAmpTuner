# RFKIT REST API specification (Phase 1)

## Source

| Item | URL |
|------|-----|
| Documentation page | [RF-POWER Documentation](https://rf-power.eu/documentation/) |
| Vendor bundle (zip) | [swagger.zip](https://rf-power.eu/wp-content/uploads/2024/12/swagger.zip) |

This folder contains the **extracted OpenAPI document** from that zip plus a copy of the zip for reproducibility.

## Verification (canonical contract for plugin + emulator)

- **Format:** OpenAPI **3.0.0** (`"openapi": "3.0.0"`).
- **API title / version:** **RFKIT** **0.9.0** (`info.title`, `info.version`).
- **Default server:** `http://localhost:8080` (`servers[0].url`).
- **Paths** (all present under `paths`):

  | Path | Methods |
  |------|---------|
  | `/info` | GET |
  | `/data` | GET |
  | `/power` | GET |
  | `/tuner` | GET, PUT |
  | `/antennas` | GET |
  | `/antennas/active` | GET, PUT |
  | `/operational-interface` | GET, PUT |
  | `/error/reset` | POST |
  | `/operate-mode` | GET, PUT |

- **Schemas:** `components.schemas` defines **Info**, **Data**, **Power**, **Tuner**, **OperateMode**, **Antenna**, shared types (e.g. `FloatWithUnit`), **Error**, and **examples** under `components.examples`.

This matches the integration plan’s expected RFKIT 0.9.0 surface (emulator and `RfkitCommandMapper` should implement these routes and JSON shapes).

## Usage

- **Reference** when implementing `RfkitHttpConnection`, mapper, response builder, and **RfkitEmulator**.
- Optional: generate C# clients with [OpenAPI Generator](https://openapi-generator.tech/) from `swagger.json`, or hand-map with `System.Text.Json` and types aligned to these schemas.

## Rights

Specification is **copyright RF-POWER / vendor**; kept here for plugin development against the published API. Do not remove this README when updating `swagger.json` from a newer vendor release—refresh the **Source** table and re-run verification.
