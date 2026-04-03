#nullable enable

// using System is needed for Uri, InvalidOperationException, and StringComparison
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

        /// <summary>
        /// Unique identifier for this plugin.
        /// </summary>
        public string PluginId { get; set; } = RFKitAmpTunerPlugin.PluginId;

        /// <summary>
        /// Whether this plugin instance is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Connection type (TCP or Serial).
        /// </summary>
        public PluginConnectionType ConnectionType { get; set; } = PluginConnectionType.TCP;

        /// <summary>
        /// IP address of the tuner when using TCP connection.
        /// </summary>
        public string IpAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// TCP port number for tuner communication.
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Serial port name when using serial connection (e.g., "COM1", "/dev/ttyUSB0").
        /// </summary>
        public string SerialPort { get; set; } = "COM1";

        /// <summary>
        /// Serial baud rate. Default 38400 matches sample device specification.
        /// </summary>
        public int BaudRate { get; set; } = 38400;

        /// <summary>
        /// Delay in milliseconds before attempting to reconnect after connection loss.
        /// </summary>
        public int ReconnectDelayMs { get; set; } = 5000;

        /// <summary>
        /// Indicates TCP connection is supported by this plugin.
        /// </summary>
        public bool TcpSupported { get; set; } = true;

        /// <summary>
        /// Indicates Serial connection is supported by this plugin.
        /// </summary>
        public bool SerialSupported { get; set; } = false;

        /// <summary>
        /// Indicates Wake-on-LAN is not supported by this plugin.
        /// </summary>
        public bool WolSupported { get; set; } = false;

        /// <summary>
        /// When true, skip the device initialization/wake-up sequence (AmpWakeupMode=0).
        /// Not applicable to tuner-only plugins but required by IPluginConfiguration.
        /// </summary>
        public bool SkipDeviceWakeup { get; set; } = false;
        public bool DisableControlsOnDisconnect { get; set; } = true;

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

        /// <summary>
        /// Logs RFKIT REST HTTP traffic (and CAT framing) to a UTF-8 file under
        /// <c>%ProgramData%\PgTg\RfKitAmpTuner\</c> for this many <b>seconds</b> after the connection <c>StartAsync</c>.
        /// Default <b>60</b> when the property is omitted in new configuration. <b>0</b> = off. Examples: <b>600</b> = 10 minutes.
        /// Values above <see cref="Constants.RfkitStartupCaptureMaxSeconds"/> are clamped.
        /// Request/response bodies are truncated per <see cref="RfkitHttpTrafficMaxBodyChars"/>.
        /// Set in <c>SettingsConfig.json</c> on the RFKIT plugin object (see <b>INSTALLATION_GUIDE</b>).
        /// </summary>
        public int RfkitStartupCaptureSeconds { get; set; } = 60;

        /// <summary>
        /// Max characters logged per request/response body field during startup capture (default 8192 = 8 KB).
        /// </summary>
        public int RfkitHttpTrafficMaxBodyChars { get; set; } = Constants.DefaultRfkitHttpTrafficMaxBodyChars;

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
