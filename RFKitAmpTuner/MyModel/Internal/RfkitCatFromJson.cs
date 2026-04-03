#nullable enable

using PgTg.Common;
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// Pure JSON → CAT <c>$KEY value;</c> builders for <see cref="ResponseParser"/> (§ 4.7 and related). Phase 6: unit-testable without HTTP.
    /// </summary>
    internal static class RfkitCatFromJson
    {
        // added missing module name by N3OC so logger can be used
        private const string ModuleName = "RfkitCatFromJson";
        public static string PowerLine(JsonElement powerRoot)
        {
            var fwd = ReadIntUnit(powerRoot, "forward", "value");
            var swr = ReadDoubleUnit(powerRoot, "swr", "value");
            var swr10 = (int)Math.Round(swr * 10, MidpointRounding.AwayFromZero);
            return $"$PWR {fwd} {swr10};";
        }

        public static string TmpFromPower(JsonElement powerRoot)
        {
            var temp = ReadDoubleUnit(powerRoot, "temperature", "value");
            var ti = (int)Math.Round(temp, MidpointRounding.AwayFromZero);
            return $"$TMP {ti};";
        }

        public static string? OprLineFromOperateMode(JsonElement root)
        {
            if (!root.TryGetProperty("operate_mode", out var om))
                return null;
            var mode = om.GetString();
            return mode == "OPERATE" ? "$OPR 1;" : "$OPR 0;";
        }

        /// <summary><c>$IDN</c> and optional <c>$VER</c> from <c>GET /info</c>.</summary>
        public static string IdentifyLines(JsonElement infoRoot)
        {
            var name = infoRoot.TryGetProperty("device", out var d)
                ? d.GetString() ?? Constants.IdentifyResponse
                : Constants.IdentifyResponse;
            var ver = "";
            if (infoRoot.TryGetProperty("software_version", out var sv) &&
                sv.TryGetProperty("controller", out var c))
            {
                ver = c.GetInt32().ToString(CultureInfo.InvariantCulture);
            }

            var sb = new StringBuilder();
            sb.Append("$IDN ").Append(name).Append(';');
            if (ver.Length > 0)
                sb.Append("$VER ").Append(ver).Append(';');
            return sb.ToString();
        }

        /// <summary>Poll <c>$VER;</c> — controller version from info.</summary>
        public static string? VerPollLine(JsonElement infoRoot)
        {
            if (!infoRoot.TryGetProperty("software_version", out var sv) ||
                !sv.TryGetProperty("controller", out var c))
                return null;
            var ver = c.GetInt32().ToString(CultureInfo.InvariantCulture);
            return $"$VER {ver};";
        }

        /// <summary>Poll <c>$SER;</c> — prefer <c>custom_device_name</c>, else <c>device</c>.</summary>
        public static string? SerPollLine(JsonElement infoRoot)
        {
            if (infoRoot.TryGetProperty("custom_device_name", out var cn) &&
                cn.ValueKind == JsonValueKind.String)
            {
                var s = cn.GetString();
                if (!string.IsNullOrEmpty(s))
                    return $"$SER {s};";
            }

            if (infoRoot.TryGetProperty("device", out var d) && d.ValueKind == JsonValueKind.String)
            {
                var s = d.GetString();
                if (!string.IsNullOrEmpty(s))
                    return $"$SER {s};";
            }

            return null;
        }

        public static string BndLineFromData(JsonElement dataRoot)
        {
            //Logger.LogLudicrous(ModuleName, $"Calculating band index from data: {dataRoot}");
            if (!dataRoot.TryGetProperty("frequency", out var f) || f.ValueKind != JsonValueKind.Object)
                return "$BND 0;";
            //Logger.LogLudicrous(ModuleName, $"frequency property: {f}"); //{"value": 7039.0, "unit": "kHz"}
            var kHz = ReadIntUnitObject(f, "value");
            var band = FrequencyKhzToBandIndex(kHz);
            //Logger.LogLudicrous(ModuleName, $"Calculated band index {band} from frequency {kHz} kHz");
            return $"$BND {band};";
        }

        public static string VltLineFromPower(JsonElement powerRoot)
        {
            var v = ReadDoubleUnit(powerRoot, "voltage", "value");
            var i = ReadDoubleUnit(powerRoot, "current", "value");
            var vv = (int)Math.Round(v * 10, MidpointRounding.AwayFromZero);
            var ii = (int)Math.Round(i * 10, MidpointRounding.AwayFromZero);
            return $"$VLT {vv} {ii};";
        }

        public static string BypLineFromTuner(JsonElement tunerRoot)
        {
            var mode = tunerRoot.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
            // changed to "OFF" instead of "BYPASS" for RFKIT by N3OC, RFKIT API 0.9.0 documentation is incorrect on this parameter
            // Cursor built this correctly but had to be fixed by N3OC because of incorrect API docs
            var b = string.Equals(mode, "OFF", StringComparison.OrdinalIgnoreCase) ? "B" : "N";
            return $"$BYP {b};";
        }

        public static string TplLineFromTuner(JsonElement tunerRoot)
        {
            var mode = tunerRoot.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
            // the following needs verification, but seems to be correct based on RFKIT API 0.9.0 documentation and testing with Cursor
            var tuning = string.Equals(mode, "AUTO_TUNING", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
            return $"$TPL {tuning};";
        }

        public static string SwrLineFromPower(JsonElement powerRoot)
        {
            var swr = ReadDoubleUnit(powerRoot, "swr", "value");
            return $"$SWR {swr.ToString("0.00", CultureInfo.InvariantCulture)};";
        }

        public static string FpwLineFromPower(JsonElement powerRoot)
        {
            var fwd = ReadIntUnit(powerRoot, "forward", "value");
            return $"$FPW {fwd};";
        }

        public static string IndLineFromTuner(JsonElement tunerRoot)
        {
            if (tunerRoot.TryGetProperty("L", out var l) && l.ValueKind == JsonValueKind.Object)
            {
                var n = ReadIntUnitObject(l, "value");
                return $"$IND {n:X};";
            }

            return "$IND 0;";
        }

        public static string CapLineFromTuner(JsonElement tunerRoot)
        {
            if (tunerRoot.TryGetProperty("C", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                var n = ReadIntUnitObject(c, "value");
                return $"$CAP {n:X};";
            }

            return "$CAP 0;";
        }

        public static string FltLineFromData(JsonElement dataRoot)
        {
            // log status property from json for debugging, added by N3OC
            //Logger.LogLudicrous(ModuleName, $"Calculating fault code from data: {dataRoot}");
            if (dataRoot.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String)
            {
                // log status property from json for debugging, added by N3OC
                //Logger.LogLudicrous(ModuleName, $"status property: {s}");
                var st = s.GetString() ?? "";
                // log status string for debugging, added by N3OC
                //Logger.LogLudicrous(ModuleName, $"status string: '{st}'");

                // the following mapping is based on RFKIT API 0.9.0 documentation and testing by N3OC, and may need revision if RFKIT status strings change as defined in the "API and SDK Documentation" link found on this website: https://rf-power.eu/documentation/
                
                // map KPA1500 fault codes to RFKIT status strings:
                // 00 - status json is null or empty (RFKIT returns empty for OK)
                // 20 - High Current
                // 40 - Overheating
                // 60 - High Input Power
                // 90 - High SWR
                // 90 - High Antenna Reflection
                // C0 - High Output Power
                // 85 - No internal high voltage
                // the following are not implemented by Elecraft but are in RFKIT and need to map to something
                // D0 - Severe Error LPF
                // D1 - Wrong Frequency
                // Map RFKIT status strings to KPA1500 fault codes and log unmapped statuses for debugging.
                if (st.Length > 0)
                {
                    var sw = st.Trim().ToUpperInvariant();
                    // Use string codes because some fault codes are hex/text like "C0" or "D0".
                    // mapped to new integer codes for StatusTracker GetFaultDescription to map back to descriptions, added by N3OC
                    var mappedStr = sw switch
                    {
                        // High current
                        "HIGH CURRENT" => "2",
                        // Overheating
                        "OVERHEATING" => "4",
                        // Excess input power
                        "HIGH INPUT POWER" => "6",
                        // High SWR
                        "HIGH SWR" => "9",
                        // High Antenna Reflection
                        "HIGH ANTENNA REFLECTION" => "5",
                        // High output power
                        "HIGH OUTPUT POWER" => "7",
                        // internal HV failure
                        "NO INTERNAL HIGH VOLTAGE" => "8",
                        // Additional RFKIT-specific statuses mapped to placeholder codes
                        "SEVERE LPF ERROR" => "1",
                        "WRONG FREQUENCY" => "3",
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(mappedStr))
                        {
                        // log mapped status for debugging, added by N3OC
                        Logger.LogLudicrous(ModuleName, $"Mapped RFKIT status '{st}' to fault code {mappedStr}");
                        // changed from FLT to FL for PgTg 1009 by N3OC
                        return $"$FLT {mappedStr};";
                    }

                    Logger.LogLudicrous(ModuleName, $"Unmapped RFKIT status string: '{st}'");
                }
            }

            return "$FLT 0;";
        }

        public static string AntLineFromAntennas(JsonElement antennaRoot)
        {
            //Logger.LogLudicrous(ModuleName, $"Calculating antenna code from antennas: {antennaRoot}");
            if (antennaRoot.TryGetProperty("number", out var a) && a.ValueKind == JsonValueKind.Number)
            {
                //Logger.LogLudicrous(ModuleName, $"antenna number property: {a}");
                return $"$ANT {a:X};";
            }

            return "$ANT 0;";
        }

        public static int FrequencyKhzToBandIndex(int kHz)
        {
            if (kHz <= 2000) return 0;
            if (kHz <= 3800) return 1;
            if (kHz <= 5450) return 2;
            if (kHz <= 7300) return 3;
            if (kHz <= 10140) return 4;
            if (kHz <= 14350) return 5;
            if (kHz <= 18168) return 6;
            if (kHz <= 21450) return 7;
            if (kHz <= 24990) return 8;
            if (kHz <= 29700) return 9;
            return 10;
        }

        private static int ReadIntUnit(JsonElement root, string objName, string valueName)
        {
            if (!root.TryGetProperty(objName, out var o) || o.ValueKind != JsonValueKind.Object)
                return 0;
            return ReadIntUnitObject(o, valueName);
        }

        // following is raising an exception on frequency
        // made more robust by GitHub Copilot by handling both number and string cases, and by catching exceptions from invalid formats
                private static int ReadIntUnitObject(JsonElement o, string valueName)
        {
            if (!o.TryGetProperty(valueName, out var v))
                return 0;

            // If it's a JSON number, prefer TryGetInt32 but fall back to double
            // to handle values like 7039.0 which would otherwise throw on GetInt32.
            if (v.ValueKind == JsonValueKind.Number)
            {
                if (v.TryGetInt32(out var i))
                    return i;

                try
                {
                    var d = v.GetDouble();
                    return (int)Math.Round(d, MidpointRounding.AwayFromZero);
                }
                catch
                {
                    return 0;
                }
            }

            // If it's a string, try parsing as int or double.
            if (v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (string.IsNullOrEmpty(s))
                    return 0;

                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var si))
                    return si;

                if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var sd))
                    return (int)Math.Round(sd, MidpointRounding.AwayFromZero);
            }

            return 0;
        }

        private static double ReadDoubleUnit(JsonElement root, string objName, string valueName)
        {
            if (!root.TryGetProperty(objName, out var o) || o.ValueKind != JsonValueKind.Object)
                return 0;
            return o.TryGetProperty(valueName, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble()
                : 0;
        }
    }
}
