#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PgTg.Common;
using PgTg.Plugins.Core;

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// RFKIT REST (OpenAPI 0.9.0) transport: maps CAT-style <see cref="CommandQueue"/> strings to HTTP and
    /// raises <see cref="DataReceived"/> with synthetic <c>$KEY value;</c> lines for <see cref="ResponseParser"/>.
    /// Phase 4: configuration + full transport; Phase 6: <see cref="RfkitCommandMapper"/> + <see cref="RfkitCatFromJson"/>, reconnect loop, timeouts.
    /// </summary>
    internal sealed class RfkitHttpConnection : IRFKitAmpTunerConnection
    {
        private const string ModuleName = "RfkitHttpConnection";

        private readonly CancellationToken _cancellationToken;
        private readonly object _lock = new();
        private readonly HttpClient _http;
        private readonly int _reconnectDelayMs;
        private readonly HttpRestClient _httpRestClient;

        private bool _disposed;
        private bool _isRunning;
        private CancellationTokenSource? _heartbeatCts;
        private PluginConnectionState _connectionState = PluginConnectionState.Disconnected;

        public event Action<string>? DataReceived;
        public event Action<PluginConnectionState, PluginConnectionState>? ConnectionStateChanged;

        public PluginConnectionState ConnectionState
        {
            get { lock (_lock) { return _connectionState; } }
        }

        public bool IsConnected
        {
            get { lock (_lock) { return _connectionState == PluginConnectionState.Connected; } }
        }

        public RfkitHttpConnection(Uri baseUri, CancellationToken cancellationToken, int reconnectDelayMs)
        {
            _cancellationToken = cancellationToken;
            _reconnectDelayMs = Math.Max(Constants.RfkitHttpReconnectDelayMinMs, reconnectDelayMs);
            _http = new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(Constants.RfkitHttpRequestTimeoutSeconds)
            };
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            _httpRestClient = new HttpRestClient(this);
        }

        /// <summary>
        /// Starts a background loop: probe <c>GET /info</c> until success, then stay <see cref="PluginConnectionState.Connected"/>
        /// with periodic heartbeat; on failure, <see cref="PluginConnectionState.Reconnecting"/> and retry after
        /// the plugin <c>ReconnectDelayMs</c> (same idea as <see cref="TcpConnection"/>'s reconnect loop).
        /// </summary>
        public Task StartAsync()
        {
            if (_isRunning)
                return Task.CompletedTask;

            _isRunning = true;
            _ = ConnectMaintenanceLoopAsync();
            return Task.CompletedTask;
        }

        private async Task ConnectMaintenanceLoopAsync()
        {
            try
            {
                while (_isRunning && !_cancellationToken.IsCancellationRequested)
                {
                    SetState(PluginConnectionState.Connecting);

                    var ok = await ProbeInfoAsync().ConfigureAwait(false);
                    if (!ok)
                    {
                        Logger.LogInfo(ModuleName, $"RFKIT REST unreachable (GET /info); retry in {_reconnectDelayMs} ms");
                        SetState(PluginConnectionState.Reconnecting);
                        try
                        {
                            await Task.Delay(_reconnectDelayMs, _cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        continue;
                    }

                    SetState(PluginConnectionState.Connected);
                    Logger.LogInfo(ModuleName, $"RFKIT REST reachable at {_http.BaseAddress}");

                    var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
                    lock (_lock)
                    {
                        _heartbeatCts = heartbeatCts;
                    }

                    try
                    {
                        while (_isRunning && !heartbeatCts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(Constants.RfkitHttpHeartbeatIntervalMs, heartbeatCts.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            if (!_isRunning)
                                break;

                            if (!await ProbeInfoAsync().ConfigureAwait(false))
                            {
                                Logger.LogInfo(ModuleName, "RFKIT REST heartbeat failed; entering reconnect");
                                SetState(PluginConnectionState.Reconnecting);
                                break;
                            }
                        }
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            if (ReferenceEquals(_heartbeatCts, heartbeatCts))
                                _heartbeatCts = null;
                        }

                        heartbeatCts.Dispose();
                    }

                    if (!_isRunning)
                        break;

                    SetState(PluginConnectionState.Reconnecting);
                    try
                    {
                        await Task.Delay(_reconnectDelayMs, _cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                SetState(PluginConnectionState.Disconnected);
            }
        }

        private async Task<bool> ProbeInfoAsync()
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "info");
                using var resp = await _http.SendAsync(req, _cancellationToken).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Logger.LogVerbose(ModuleName, $"RFKIT probe /info: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            SignalTransportFailure();
            SetState(PluginConnectionState.Disconnected);
        }

        public bool Send(string data)
        {
            if (string.IsNullOrEmpty(data) || !IsConnected)
                return false;

            try
            {
                var response = ProcessCommands(data);
                if (response.Length > 0)
                    DataReceived?.Invoke(response);
                return true;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ModuleName, $"Send HTTP error: {ex.Message}");
                SignalTransportFailure();
                return false;
            }
            catch (TaskCanceledException ex) when (!_cancellationToken.IsCancellationRequested)
            {
                Logger.LogError(ModuleName, $"Send timed out: {ex.Message}");
                SignalTransportFailure();
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ModuleName, $"Send mapping error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Breaks the connected heartbeat wait so the maintenance loop retries after transport errors.</summary>
        private void SignalTransportFailure()
        {
            CancellationTokenSource? cts;
            lock (_lock)
            {
                cts = _heartbeatCts;
            }

            try
            {
                cts?.Cancel();
            }
            catch
            {
                // ignored
            }

            // While reconnecting, <see cref="IsConnected"/> must be false so <see cref="Send"/> does not hit a dead host.
            if (cts != null)
                SetState(PluginConnectionState.Reconnecting);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isRunning = false;
            SignalTransportFailure();
            SetState(PluginConnectionState.Disconnected);
            _http.Dispose();
        }

        private void SetState(PluginConnectionState state)
        {
            PluginConnectionState previous;
            lock (_lock)
            {
                previous = _connectionState;
                _connectionState = state;
            }

            if (previous != state)
                ConnectionStateChanged?.Invoke(previous, state);
        }

        private string ProcessCommands(string data)
        {
            var sb = new StringBuilder();
            var segments = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                var token = segment.Trim();
                if (token.Length == 0) continue;
                if (!token.StartsWith("$", StringComparison.Ordinal))
                    continue;

                var line = ProcessOneCommand(token);
                if (line != null)
                    sb.Append(line);
            }

            return sb.ToString();
        }

        /// <summary>Delegates to <see cref="RfkitCommandMapper"/> (Phase 6).</summary>
        private string? ProcessOneCommand(string token)
        {
            return RfkitCommandMapper.ProcessOneCommand(token, _httpRestClient, Logger.LogVerbose);
        }

        private JsonDocument? GetJsonDocument(string relativePath)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, relativePath);
                using var resp = _http.SendAsync(req, _cancellationToken).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    return null;
                var stream = resp.Content.ReadAsStream(_cancellationToken);
                return JsonDocument.Parse(stream);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogVerbose(ModuleName, $"GET {relativePath} failed: {ex.Message}");
                SignalTransportFailure();
                return null;
            }
            catch (TaskCanceledException ex)
            {
                if (!_cancellationToken.IsCancellationRequested)
                {
                    Logger.LogVerbose(ModuleName, $"GET {relativePath} timed out: {ex.Message}");
                    SignalTransportFailure();
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogVerbose(ModuleName, $"GET {relativePath} failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>HTTP implementation of <see cref="IRfkitRestClient"/> for <see cref="RfkitCommandMapper"/>.</summary>
        private sealed class HttpRestClient : IRfkitRestClient
        {
            private readonly RfkitHttpConnection _outer;

            public HttpRestClient(RfkitHttpConnection outer)
            {
                _outer = outer;
            }

            public JsonDocument? Get(string relativePath) => _outer.GetJsonDocument(relativePath);

            public bool PutJson(string relativePath, string jsonBody)
            {
                try
                {
                    using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    using var req = new HttpRequestMessage(HttpMethod.Put, relativePath) { Content = content };
                    using var resp = _outer._http.SendAsync(req, _outer._cancellationToken).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                    {
                        Logger.LogVerbose(ModuleName, $"PUT {relativePath} returned {(int)resp.StatusCode}");
                        return false;
                    }

                    return true;
                }
                catch (HttpRequestException ex)
                {
                    Logger.LogVerbose(ModuleName, $"PUT {relativePath} failed: {ex.Message}");
                    _outer.SignalTransportFailure();
                    return false;
                }
                catch (TaskCanceledException) when (!_outer._cancellationToken.IsCancellationRequested)
                {
                    Logger.LogVerbose(ModuleName, $"PUT {relativePath} timed out");
                    _outer.SignalTransportFailure();
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogVerbose(ModuleName, $"PUT {relativePath} failed: {ex.Message}");
                    return false;
                }
            }

            public bool PostWithoutBody(string relativePath)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, relativePath);
                    using var resp = _outer._http.SendAsync(req, _outer._cancellationToken).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                    {
                        Logger.LogVerbose(ModuleName, $"POST {relativePath} returned {(int)resp.StatusCode}");
                        return false;
                    }

                    return true;
                }
                catch (HttpRequestException ex)
                {
                    Logger.LogVerbose(ModuleName, $"POST {relativePath} failed: {ex.Message}");
                    _outer.SignalTransportFailure();
                    return false;
                }
                catch (TaskCanceledException) when (!_outer._cancellationToken.IsCancellationRequested)
                {
                    Logger.LogVerbose(ModuleName, $"POST {relativePath} timed out");
                    _outer.SignalTransportFailure();
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogVerbose(ModuleName, $"POST {relativePath} failed: {ex.Message}");
                    return false;
                }
            }
        }
    }
}
