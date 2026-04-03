namespace RFKitAmpTuner.Tests;

/// <summary>Golden RFKIT-shaped JSON fragments (aligned with <c>RfkitEmulator/Responses.json</c>).</summary>
internal static class GoldenJson
{
    public const string Power = """
{
  "temperature": { "value": 23.5, "unit": "°C" },
  "voltage": { "value": 53.3, "unit": "V" },
  "current": { "value": 0, "unit": "A" },
  "forward": { "value": 10, "max_value": 1500, "unit": "W" },
  "reflected": { "value": 0, "max_value": 0, "unit": "W" },
  "swr": { "value": 1.15, "max_value": 1.15, "unit": "" }
}
""";

    public const string Data7039 = """
{
  "band": { "value": 40, "unit": "m" },
  "frequency": { "value": 7039, "unit": "kHz" },
  "status": ""
}
""";

    /// <summary><see cref="RfkitCatFromJson.BypLineFromTuner"/> keys on <c>mode</c> == BYPASS.</summary>
    public const string TunerModeBypassWithLc = """
{
  "mode": "BYPASS",
  "L": { "value": 255, "unit": "" },
  "C": { "value": 16, "unit": "" }
}
""";

    public const string TunerAutoTuning = """
{ "mode": "AUTO_TUNING" }
""";

    public const string OperateModeOperate = """
{ "operate_mode": "OPERATE" }
""";

    public const string InfoSample = """
{
  "device": "B26 RF2K-S (emulator)",
  "software_version": { "GUI": 108, "controller": 131 },
  "custom_device_name": "RfkitEmulator MVP"
}
""";
}
