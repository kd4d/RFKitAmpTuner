#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;
using PgTg.Common;

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// Optional startup-only file log of RFKIT REST HTTP exchanges (and CAT framing lines) for field debugging.
    /// Window starts when <see cref="Begin"/> is called from <see cref="RfkitHttpConnection.StartAsync"/>.
    /// </summary>
    internal sealed class RfkitStartupTrafficCapture : IDisposable
    {
        private const string ModuleName = "RfkitStartupTrafficCapture";

        private readonly object _lock = new();
        private readonly int _windowSeconds;
        private readonly int _maxBodyChars;
        private readonly Uri _baseUri;
        private DateTimeOffset _endUtc;
        private StreamWriter? _writer;
        private string? _filePath;
        private bool _begun;
        private bool _disposed;

        public RfkitStartupTrafficCapture(int startupCaptureSeconds, int maxBodyChars, Uri baseUri)
        {
            _windowSeconds = NormalizeStartupCaptureSeconds(startupCaptureSeconds);
            _maxBodyChars = Math.Max(256, maxBodyChars);
            _baseUri = baseUri;
            _endUtc = DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Returns a positive duration in seconds, <b>0</b> if disabled, or clamps to <see cref="Constants.RfkitStartupCaptureMaxSeconds"/>.
        /// </summary>
        public static int NormalizeStartupCaptureSeconds(int seconds)
        {
            if (seconds <= 0)
                return 0;
            if (seconds > Constants.RfkitStartupCaptureMaxSeconds)
                return Constants.RfkitStartupCaptureMaxSeconds;
            return seconds;
        }

        public static string TruncateForLog(string? text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (text.Length <= maxChars)
                return text;
            return text.Substring(0, maxChars) + string.Format(CultureInfo.InvariantCulture, "... [truncated, {0} chars total]", text.Length);
        }

        /// <summary>Starts the capture window and creates the log file. Safe to call once.</summary>
        public void Begin()
        {
            if (_windowSeconds == 0)
                return;

            lock (_lock)
            {
                if (_begun || _disposed)
                    return;
                _begun = true;
                _endUtc = DateTimeOffset.UtcNow.AddSeconds(_windowSeconds);

                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "PgTg",
                    "RfKitAmpTuner");
                Directory.CreateDirectory(dir);
                _filePath = Path.Combine(dir, $"rfkit-http-capture-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

                _writer = new StreamWriter(_filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };
                _writer.WriteLine("# RFKitAmpTuner HTTP startup capture");
                _writer.WriteLine("# Base URL: " + _baseUri);
                _writer.WriteLine("# Window: " + _windowSeconds + " s from plugin connection StartAsync");
                _writer.WriteLine("# Max body chars per field: " + _maxBodyChars);
                _writer.WriteLine("# ---");
                Logger.LogInfo(ModuleName, $"RFKIT startup HTTP capture: {_windowSeconds} s -> {_filePath}");
            }
        }

        private bool IsActiveLocked()
        {
            if (_disposed || _windowSeconds == 0 || !_begun)
                return false;
            return DateTimeOffset.UtcNow < _endUtc;
        }

        public void LogHttp(string method, string relativePath, int? statusCode, string? requestBody, string? responseBody, string? errorNote = null)
        {
            lock (_lock)
            {
                if (!IsActiveLocked() || _writer == null)
                    return;

                var ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                _writer.WriteLine();
                _writer.WriteLine("--- " + ts + " " + method + " " + relativePath + " ---");
                if (statusCode.HasValue)
                    _writer.WriteLine("Status: " + statusCode.Value);
                if (!string.IsNullOrEmpty(errorNote))
                    _writer.WriteLine("Note: " + errorNote);
                if (!string.IsNullOrEmpty(requestBody))
                {
                    _writer.WriteLine("Request:");
                    _writer.WriteLine(TruncateForLog(requestBody, _maxBodyChars));
                }

                if (responseBody != null)
                {
                    _writer.WriteLine("Response:");
                    _writer.WriteLine(TruncateForLog(responseBody, _maxBodyChars));
                }
            }
        }

        public void LogCatOut(string catBatch)
        {
            lock (_lock)
            {
                if (!IsActiveLocked() || _writer == null)
                    return;

                var ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                _writer.WriteLine();
                _writer.WriteLine("--- " + ts + " CAT >> plugin Send ---");
                _writer.WriteLine(TruncateForLog(catBatch, _maxBodyChars));
            }
        }

        public void LogCatIn(string syntheticResponse)
        {
            lock (_lock)
            {
                if (!IsActiveLocked() || _writer == null)
                    return;

                var ts = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                _writer.WriteLine();
                _writer.WriteLine("--- " + ts + " CAT << synthetic to parser ---");
                _writer.WriteLine(TruncateForLog(syntheticResponse, _maxBodyChars));
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                _disposed = true;
                try
                {
                    _writer?.WriteLine();
                    _writer?.WriteLine("# --- capture window ended or plugin stopped ---");
                }
                catch
                {
                    // ignored
                }

                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
