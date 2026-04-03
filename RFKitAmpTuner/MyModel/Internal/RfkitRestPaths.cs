#nullable enable

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// Relative URL paths for RFKIT OpenAPI 0.9.0 (no leading slash; used with <see cref="System.Net.Http.HttpClient.BaseAddress"/>).
    /// Phase 6: single source for mapper + tests.
    /// </summary>
    internal static class RfkitRestPaths
    {
        public const string Info = "info";
        public const string Data = "data";
        public const string Power = "power";
        public const string Tuner = "tuner";
        public const string OperateMode = "operate-mode";
        public const string AntennasActive = "antennas/active";
        public const string ErrorReset = "error/reset";
    }
}
