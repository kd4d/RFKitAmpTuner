#nullable enable

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
            if (!dataRoot.TryGetProperty("frequency", out var f) || f.ValueKind != JsonValueKind.Object)
                return "$BND 0;";
            var kHz = ReadIntUnitObject(f, "value");
            var band = FrequencyKhzToBandIndex(kHz);
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
            var b = string.Equals(mode, "BYPASS", StringComparison.OrdinalIgnoreCase) ? "B" : "N";
            return $"$BYP {b};";
        }

        public static string TplLineFromTuner(JsonElement tunerRoot)
        {
            var mode = tunerRoot.TryGetProperty("mode", out var m) ? m.GetString() ?? "" : "";
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
            if (dataRoot.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String)
            {
                var st = s.GetString() ?? "";
                if (st.Length > 0 && int.TryParse(st, out var code))
                    return $"$FLT {code};";
            }

            return "$FLT 0;";
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

        private static int ReadIntUnitObject(JsonElement o, string valueName)
        {
            return o.TryGetProperty(valueName, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : 0;
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
