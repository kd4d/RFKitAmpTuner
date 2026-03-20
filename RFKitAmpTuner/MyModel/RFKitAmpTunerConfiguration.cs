#nullable enable

using System;
using PgTg.Common;
using PgTg.Plugins.Core;
using RFKitAmpTuner.MyModel.Internal;

namespace RFKitAmpTuner.MyModel
{
    /// <summary>
    /// Configuration for the RFKIT RF2K-S amplifier+tuner plugin.
    /// Implements PgTgBridge <c>IAmplifierTunerConfiguration</c>.
    /// </summary>
    /// <remarks>
    /// <b>Phase 4 (HTTP):</b> When <see cref="ConnectionType"/> is <see cref="PluginConnectionType.TCP"/> and
    /// <see cref="UseRfkitRestApi"/> is <c>true</c> (default), the plugin uses RFKIT REST over HTTP
    /// (<c>RfkitHttpConnection</c> in Internal) instead of a raw TCP byte stream. The host UI still shows TCP
    /// (IP + port); <see cref="HttpBaseUrl"/> overrides the derived URL when non-empty.
    /// </remarks>
    public class RFKitAmpTunerConfiguration : IAmplifierTunerConfiguration
    {
        // IPluginConfiguration
        public string PluginId { get; set; } = RFKitAmpTunerPlugin.PluginId;
        public bool Enabled { get; set; } = false;
        public PluginConnectionType ConnectionType { get; set; } = PluginConnectionType.TCP;
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public string SerialPort { get; set; } = "COM1";
        public int BaudRate { get; set; } = 38400;
        public int ReconnectDelayMs { get; set; } = 5000;
        public bool TcpSupported { get; set; } = true;
        public bool SerialSupported { get; set; } = true;
        public bool WolSupported { get; set; } = false;
        public bool SkipDeviceWakeup { get; set; } = false;

        /// <summary>
        /// When <c>true</c> and <see cref="ConnectionType"/> is <see cref="PluginConnectionType.TCP"/>, use RFKIT HTTP REST
        /// (<c>RfkitHttpConnection</c>) instead of raw CAT over TCP. Default <c>true</c> for RF2K-S.
        /// Set <c>false</c> only for legacy/raw stream testing against a TCP terminal.
        /// </summary>
        public bool UseRfkitRestApi { get; set; } = true;

        /// <summary>
        /// Optional absolute base URL (e.g. <c>http://192.168.1.10:8080</c>). Whitespace or empty means:
        /// derive <c>http://{IpAddress}:{Port}/</c> (trailing slash normalized in <see cref="GetEffectiveRfkitHttpBaseUri"/>).
        /// </summary>
        public string HttpBaseUrl { get; set; } = "";

        // IAmplifierConfiguration
        public int PollingIntervalRxMs { get; set; } = Constants.PollingRxMs;
        public int PollingIntervalTxMs { get; set; } = Constants.PollingTxMs;
        public int PttWatchdogIntervalMs { get; set; } = Constants.PttWatchdogMs;

        // ITunerConfiguration
        public int TuneTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Resolves the RFKIT REST base URI for <see cref="RfkitHttpConnection"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">URL is missing scheme/host or cannot be parsed.</exception>
        public Uri GetEffectiveRfkitHttpBaseUri()
        {
            string raw = HttpBaseUrl?.Trim() ?? "";
            if (raw.Length == 0)
            {
                raw = $"http://{IpAddress.Trim()}:{Port}/";
            }

            if (!raw.Contains("://", StringComparison.Ordinal))
                raw = "http://" + raw.TrimStart('/');

            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"Invalid RFKIT HTTP base URL: '{raw}'");

            var builder = new UriBuilder(uri)
            {
                Path = uri.AbsolutePath.TrimEnd('/') + "/"
            };
            return builder.Uri;
        }

        /// <summary>
        /// <c>true</c> when this configuration selects HTTP REST transport (vs serial or raw TCP).
        /// </summary>
        public bool IsRfkitHttpTransportSelected() =>
            ConnectionType == PluginConnectionType.TCP && UseRfkitRestApi;
    }
}
