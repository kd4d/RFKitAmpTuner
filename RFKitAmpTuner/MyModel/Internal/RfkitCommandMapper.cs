#nullable enable

using System;
using System.Globalization;

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// Maps one CAT token to RFKIT REST calls + synthetic CAT responses (integration plan § 4). Phase 6.
    /// </summary>
    internal static class RfkitCommandMapper
    {
        private const string ModuleName = "RfkitCommandMapper";

        /// <summary>
        /// Process one command token (with or without trailing ';').
        /// </summary>
        /// <returns>Synthetic response line(s) for <see cref="ResponseParser"/>, or <c>null</c> if none.</returns>
        public static string? ProcessOneCommand(
            string token,
            IRfkitRestClient client,
            Action<string, string>? logVerbose = null)
        {
            var t = token.TrimEnd(';').Trim();

            if (t.StartsWith(Constants.SetFreqKhzCmdPrefix, StringComparison.Ordinal))
                return BuildFrqEchoLine(t);

            if (t.Equals("$WKP", StringComparison.Ordinal) || t.Equals(Constants.WakeUpCmd.TrimEnd(';'), StringComparison.Ordinal))
                return null;

            if (t.Equals(Constants.IdentifyCmd.TrimEnd(';'), StringComparison.Ordinal) || t.Equals("$IDN", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Info);
                if (doc == null)
                    return $"$IDN {Constants.IdentifyResponse};";
                return RfkitCatFromJson.IdentifyLines(doc.RootElement);
            }

            if (t.Equals("$PWR", StringComparison.Ordinal) || t.StartsWith("$PWR", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Power);
                return doc == null ? null : RfkitCatFromJson.PowerLine(doc.RootElement);
            }

            if (t.Equals("$TMP", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Power);
                return doc == null ? null : RfkitCatFromJson.TmpFromPower(doc.RootElement);
            }

            if (t.Equals("$OPR", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.OperateMode);
                return doc == null ? null : RfkitCatFromJson.OprLineFromOperateMode(doc.RootElement);
            }

            if (t.Equals(Constants.OperateCmd.TrimEnd(';'), StringComparison.Ordinal) || t.Equals("$OPR1", StringComparison.Ordinal))
            {
                var ok = client.PutJson(RfkitRestPaths.OperateMode, """{"operate_mode":"OPERATE"}""");
                if (!ok)
                    logVerbose?.Invoke(ModuleName, "PUT operate-mode (OPERATE) non-success");
                return "$OPR 1;";
            }

            if (t.Equals(Constants.StandbyCmd.TrimEnd(';'), StringComparison.Ordinal) || t.Equals("$OPR0", StringComparison.Ordinal))
            {
                var ok = client.PutJson(RfkitRestPaths.OperateMode, """{"operate_mode":"STANDBY"}""");
                if (!ok)
                    logVerbose?.Invoke(ModuleName, "PUT operate-mode (STANDBY) non-success");
                return "$OPR 0;";
            }

            if (t.Equals("$BND", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Data);
                return doc == null ? "$BND 0;" : RfkitCatFromJson.BndLineFromData(doc.RootElement);
            }

            if (t.Equals("$VLT", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Power);
                return doc == null ? null : RfkitCatFromJson.VltLineFromPower(doc.RootElement);
            }

            if (t.Equals("$BYP", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Tuner);
                return doc == null ? "$BYP N;" : RfkitCatFromJson.BypLineFromTuner(doc.RootElement);
            }

            if (t.Equals("$TPL", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Tuner);
                return doc == null ? "$TPL 0;" : RfkitCatFromJson.TplLineFromTuner(doc.RootElement);
            }

            if (t.Equals("$SWR", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Power);
                return doc == null ? null : RfkitCatFromJson.SwrLineFromPower(doc.RootElement);
            }

            if (t.Equals("$FPW", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Power);
                return doc == null ? null : RfkitCatFromJson.FpwLineFromPower(doc.RootElement);
            }

            if (t.Equals("$IND", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Tuner);
                return doc == null ? "$IND 0;" : RfkitCatFromJson.IndLineFromTuner(doc.RootElement);
            }

            if (t.Equals("$CAP", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Tuner);
                return doc == null ? "$CAP 0;" : RfkitCatFromJson.CapLineFromTuner(doc.RootElement);
            }

            if (t.Equals("$FLT", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Data);
                return doc == null ? "$FLT 0;" : RfkitCatFromJson.FltLineFromData(doc.RootElement);
            }

            if (t.Equals(Constants.ClearFaultCmd.TrimEnd(';'), StringComparison.Ordinal) || t.Equals("$FLC", StringComparison.Ordinal))
            {
                var ok = client.PostWithoutBody(RfkitRestPaths.ErrorReset);
                if (!ok)
                    logVerbose?.Invoke(ModuleName, "POST error/reset non-success");
                return "$FLT 0;";
            }

            if (t.Equals(Constants.PttOnCmd.TrimEnd(';'), StringComparison.Ordinal) || t.Equals("$TX15", StringComparison.Ordinal))
                return "$TX;";

            if (t.Equals(Constants.PttOffCmd.TrimEnd(';'), StringComparison.Ordinal) || t.Equals("$RX", StringComparison.Ordinal))
                return "$RX;";

            if (t.Equals(Constants.ShutdownCmd.TrimEnd(';'), StringComparison.Ordinal))
                return null;

            if (t.Equals("$VER", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Info);
                return doc == null ? null : RfkitCatFromJson.VerPollLine(doc.RootElement);
            }

            if (t.Equals("$SER", StringComparison.Ordinal))
            {
                using var doc = client.Get(RfkitRestPaths.Info);
                return doc == null ? null : RfkitCatFromJson.SerPollLine(doc.RootElement);
            }

            if (t.Equals(Constants.BypassCmd.TrimEnd(';'), StringComparison.Ordinal))
                return "$BYP B;";

            if (t.Equals(Constants.InlineCmd.TrimEnd(';'), StringComparison.Ordinal))
                return "$BYP N;";

            if (t.Equals(Constants.TuneStartCmd.TrimEnd(';'), StringComparison.Ordinal))
                return "$TPL 1;";

            if (t.Equals(Constants.TuneStopCmd.TrimEnd(';'), StringComparison.Ordinal))
                return "$TPL 0;";

            if (t.StartsWith("$ANT ", StringComparison.Ordinal))
            {
                TryPutAntenna(client, t, logVerbose);
                return null;
            }

            logVerbose?.Invoke(ModuleName, $"No HTTP mapping for token: {t}");
            return null;
        }

        private static void TryPutAntenna(IRfkitRestClient client, string t, Action<string, string>? logVerbose)
        {
            var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return;
            if (!int.TryParse(parts[1], out var n) || n is < 1 or > 3)
                return;

            var body = n switch
            {
                1 => """{"type":"INTERNAL","number":1}""",
                2 => """{"type":"INTERNAL","number":2}""",
                _ => """{"type":"INTERNAL","number":3}"""
            };

            var ok = client.PutJson(RfkitRestPaths.AntennasActive, body);
            if (!ok)
                logVerbose?.Invoke(ModuleName, "PUT antennas/active non-success");
        }

        /// <summary>Echo <c>$FRQ nnnnn;</c> (§ 4.5).</summary>
        public static string? BuildFrqEchoLine(string t)
        {
            var prefix = Constants.SetFreqKhzCmdPrefix;
            if (!t.StartsWith(prefix, StringComparison.Ordinal))
                return null;

            var suffix = t.AsSpan(prefix.Length);
            if (suffix.Length < 5)
                return null;

            if (!int.TryParse(suffix.Slice(0, 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var khz))
                return null;

            if (khz is < 0 or > 999_999)
                return null;

            return $"$FRQ {khz:D5};";
        }
    }
}
