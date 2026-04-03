#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PgTg.AMP;
using PgTg.Common;
using PgTg.Plugins;
using PgTg.Plugins.Core;
using RFKitAmpTuner.MyModel.Internal;

namespace RFKitAmpTuner.MyModel
{
    /// <summary>
    /// PgTgBridge plugin for RFKIT RF2K-S integrated amplifier and antenna tuner.
    /// Based on the KD4Z SampleAmpTuner architecture (TCP/serial CAT-style framing); HTTP/REST transport per Docs/RFKIT_Option1_Integration_And_Test_Plan.md.
    /// Prompt engineering for this plugin by Mark Bailey (human) KD4D
    /// Code modifications, testing and corrections by Brian McGinness (human) N3OC
    /// 1.0.0 - initial AI version by Cursor, based on KD4Z SampleAmpTuner, with additions for RFKIT specifics
    /// 1.0.1 - removed serial connection configuration in Lifecycle, divided tuner capacitor and inductor values by 2 so they would display on SmartSDR Pg and Tg panels
    /// 1.0.2 - updated for PgTg version 1007, added device panel configuration, fixed tuner status, fixed Tg panel forward power, implemented antenna 3 & 4 in device control panel, fixed fault indication in device control panel
    /// 1.0.3 - implemented antenna polling so antenna indicators on device control panel work now
    /// 1.0.4 - updated for PgTg 1010, fixed device control panel mouse click commands, changes to fault code implementation to work with mouse hover-over
    /// 1.0.5 - updated for PgTg 1011, fixed power LED behavior to then on/off based on device connection state
    /// </summary>
    [PluginInfo("rfpower.rfkit-amplifier-tuner", "RFKIT RF2K-S Amplifier+Tuner",
        Version = "1.0.5",
        Manufacturer = "RF-POWER (rf-power.eu)",
        Capability = PluginCapability.AmplifierAndTuner,
        Description = "RFKIT RF2K-S amplifier and tuner for PgTgBridge. PTT may be hardware-only; see RFKitAmpTuner/README.md and Docs/.",
        // UiSections declares which control groups PluginManagerForm will display
        // for this plugin when it is selected. Combine flags to enable multiple sections.
        //
        // Available sections:
        //   PluginUiSection.Tcp          - TCP radio button, IP address, port number
        //   PluginUiSection.Serial       - Serial radio button, COM port dropdown
        //   PluginUiSection.Reconnect    - Reconnect delay entry (shown with Tcp or Serial)
        //   PluginUiSection.Wol          - Wake-on-LAN checkbox, MAC address, Test button
        //   PluginUiSection.TcpMultiplex - TCP Multiplex Server enable + listen port
        //   PluginUiSection.GpioAction   - GPIO output action mapping grid
        //   PluginUiSection.Protocol     - CAT / CI-V frequency mode protocol selector
        //
        // Example — TCP + Serial + reconnect (most amplifier/tuner plugins):
        //   UiSections = PluginUiSection.Tcp | PluginUiSection.Serial | PluginUiSection.Reconnect
        //
        // Example — TCP only, with Wake-on-LAN:
        //   UiSections = PluginUiSection.Tcp | PluginUiSection.Reconnect | PluginUiSection.Wol
        // removed PluginUiSection.Serial by N3OC since the RFKIT doesn't support a serial connection
        UiSections = PluginUiSection.Tcp | PluginUiSection.Reconnect)]
    public class RFKitAmpTunerPlugin : IAmplifierTunerPlugin
    {
        public const string PluginId = "rfpower.rfkit-amplifier-tuner";
        private const string ModuleName = "RFKitAmpTunerPlugin";

        private readonly CancellationToken _cancellationToken;

        // Internal components
        private IRFKitAmpTunerConnection? _connection;
        private CommandQueue? _commandQueue;
        private ResponseParser? _parser;
        private StatusTracker? _statusTracker;
        private RFKitAmpTunerConfiguration? _config;

        private bool _radioConnected;
        private bool _stopped;
        private bool _disposed;
        private bool _disableControlsOnDisconnect = true;

        #region IDevicePlugin

        public PluginInfo Info { get; } = new PluginInfo
        {
            Id = PluginId,
            Name = "RFKIT RF2K-S Amplifier+Tuner",
            Version = "1.0.5",
            Manufacturer = "RF-POWER (rf-power.eu)",
            Capability = PluginCapability.AmplifierAndTuner,
            Description = "RFKIT RF2K-S amplifier and tuner for PgTgBridge. See RFKitAmpTuner/README.md.",
            ConfigurationType = typeof(RFKitAmpTunerConfiguration),
            // removed PluginUiSection.Serial since this is TCP only
            UiSections = PluginUiSection.Tcp | PluginUiSection.Reconnect
        };

        public PluginConnectionState ConnectionState => _connection?.ConnectionState ?? PluginConnectionState.Disconnected;

        public double MeterDisplayMaxPower => Constants.MeterDisplayMaxPower;

        /// <summary>
        /// Controls whether the Device Control panel automatically disables LEDs and buttons
        /// when the connection state is not Connected. Set via plugin settings JSON:
        ///   "DisableControlsOnDisconnect": false
        /// Defaults to true (controls are disabled when disconnected, except the Power LED).
        /// Set to false if your plugin manages UI state independently via GetDeviceData() values.
        /// </summary>
        public bool DisableControlsOnDisconnect => _disableControlsOnDisconnect;

        public event EventHandler<PluginConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<MeterDataEventArgs>? MeterDataAvailable;

        /// <summary>
        /// Raise when data has changed.
        /// Bridge subscribes to push /device WebSocket updates on change instead of polling.
        /// </summary>
        public event EventHandler? DeviceDataChanged;

        #endregion

        #region IAmplifierPlugin

        public event EventHandler<AmplifierStatusEventArgs>? StatusChanged;

        #endregion

        #region ITunerPlugin

        public event EventHandler<TunerStatusEventArgs>? TunerStatusChanged;

        #endregion

        public RFKitAmpTunerPlugin(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        #region Lifecycle

        public Task InitializeAsync(IPluginConfiguration configuration, CancellationToken cancellationToken)
        {
            _config = configuration as RFKitAmpTunerConfiguration ?? new RFKitAmpTunerConfiguration
            {
                IpAddress = configuration.IpAddress,
                Port = configuration.Port,
                Enabled = configuration.Enabled,
                ReconnectDelayMs = configuration.ReconnectDelayMs,
                ConnectionType = configuration.ConnectionType,
                SerialPort = configuration.SerialPort,
                BaudRate = configuration.BaudRate
            };

            // Create connection: serial, RFKIT HTTP (TCP UI + REST), or raw TCP stream
            if (_config.ConnectionType == PluginConnectionType.Serial)
            {
                var serialConnection = new SerialConnection(_cancellationToken);
                serialConnection.Configure(_config.SerialPort, _config.BaudRate);
                _connection = serialConnection;
                Logger.LogInfo(ModuleName, $"Using serial connection: {_config.SerialPort} at {_config.BaudRate} baud");
            }
            else if (_config.IsRfkitHttpTransportSelected())
            {
                try
                {
                    var baseUri = _config.GetEffectiveRfkitHttpBaseUri();
                    RfkitStartupTrafficCapture? startupCapture = null;
                    var captureSec = RfkitStartupTrafficCapture.NormalizeStartupCaptureSeconds(_config.RfkitStartupCaptureSeconds);
                    if (captureSec > 0)
                        startupCapture = new RfkitStartupTrafficCapture(captureSec, _config.RfkitHttpTrafficMaxBodyChars, baseUri);
                    _connection = new RfkitHttpConnection(baseUri, _cancellationToken, _config.ReconnectDelayMs, startupCapture);
                    Logger.LogInfo(ModuleName, $"Using RFKIT REST (HTTP) at {baseUri} (Phase 4; UseRfkitRestApi=true)");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ModuleName, $"Invalid RFKIT HTTP base URL: {ex.Message}");
                    throw;
                }
            }
            else
            {
                var tcpConnection = new TcpConnection(_cancellationToken);
                tcpConnection.Configure(_config.IpAddress, _config.Port);
                _connection = tcpConnection;
                Logger.LogInfo(ModuleName, $"Using raw TCP stream (CAT framing): {_config.IpAddress}:{_config.Port} (UseRfkitRestApi=false)");
            }

            // Create other components
            _commandQueue = new CommandQueue(_connection, _cancellationToken);
            _parser = new ResponseParser();
            _statusTracker = new StatusTracker();

            // Configure command queue
            _commandQueue.Configure(
                _config.PollingIntervalRxMs,
                _config.PollingIntervalTxMs,
                _config.PttWatchdogIntervalMs);
            _commandQueue.SkipDeviceWakeup = _config.SkipDeviceWakeup;

            // Read the connection-state UI behaviour from settings.
            // Set "DisableControlsOnDisconnect": false in the plugin's settings JSON to opt out.
            _disableControlsOnDisconnect = _config.DisableControlsOnDisconnect;

            // Wire up events
            _connection.DataReceived += OnDataReceived;
            _connection.ConnectionStateChanged += OnConnectionStateChanged;

            return Task.CompletedTask;
        }

        public async Task StartAsync()
        {
            Logger.LogVerbose(ModuleName, "StartAsync");
            if (_connection == null || _commandQueue == null || _config == null)
                throw new InvalidOperationException("Plugin not initialized");

            if (_cancellationToken.IsCancellationRequested)
            {
                Logger.LogInfo(ModuleName, "Plugin startup cancelled before start");
                return;
            }

            // Start connection first (must be connected before sending init commands)
            await _connection.StartAsync();

            if (_cancellationToken.IsCancellationRequested)
            {
                _connection.Stop();
                Logger.LogInfo(ModuleName, "Plugin startup cancelled after connection start");
                return;
            }

            // Start command queue with device initialization (waits for device to respond)
            await _commandQueue.StartAsync();

            if (!_cancellationToken.IsCancellationRequested)
            {
                Logger.LogInfo(ModuleName, "Plugin started");
            }
            else
            {
                Logger.LogInfo(ModuleName, "Plugin startup cancelled after commandQueue start");
            }
        }

        public async Task StopAsync()
        {
            if (_stopped) return;

            Logger.LogInfo(ModuleName, "Stopping plugin");

            // Zero meter values and send final update
            _statusTracker?.ZeroMeterValues();
            RaiseMeterDataEvent();

            // Stop command queue
            _commandQueue?.Stop();

            // Unwire connection events before stopping
            if (_connection != null)
            {
                _connection.DataReceived -= OnDataReceived;
                _connection.ConnectionStateChanged -= OnConnectionStateChanged;
                _connection.Stop();
            }

            _stopped = true;
            Logger.LogInfo(ModuleName, "Plugin stopped");

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Ensure stopped first to unwire events
            if (!_stopped)
            {
                try
                {
                    StopAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    Logger.LogError(ModuleName, $"Error in StopAsync during Dispose: {ex.Message}");
                }
            }

            _commandQueue?.Dispose();

            if (_connection != null)
            {
                _connection.DataReceived -= OnDataReceived;
                _connection.ConnectionStateChanged -= OnConnectionStateChanged;
                _connection.Stop();
                _connection.Dispose();
            }
        }

        #endregion

        #region IDevicePlugin Wakeup/Shutdown

        public async Task WakeupDeviceAsync()
        {
            if (_connection?.IsConnected == true)
            {
                Logger.LogInfo(ModuleName, "WakeupDeviceAsync: starting device initialization");
                if (_commandQueue != null)
                    await _commandQueue.InitializeDeviceAsync();
            }
        }

        public Task ShutdownDeviceAsync()
        {
            if (_connection?.IsConnected == true)
            {
                _connection.Send(Constants.ShutdownCmd);
                Logger.LogInfo(ModuleName, "ShutdownDeviceAsync: sent ShutdownCmd");
            }
            return Task.CompletedTask;
        }

        #endregion

        #region IAmplifierPlugin Methods

        public AmplifierStatusData GetStatus()
        {
            return _statusTracker?.GetAmplifierStatus() ?? new AmplifierStatusData();
        }

        public void SendPriorityCommand(AmpCommand command)
        {
            if (_commandQueue == null || _statusTracker == null) return;

            _commandQueue.SendPriorityCommand(command, _statusTracker.AmpState);
        }

        public void SetFrequencyKhz(int frequencyKhz)
        {
            _commandQueue?.SetFrequencyKhz(frequencyKhz);
        }

        public void SetRadioConnected(bool connected)
        {
            _radioConnected = connected;

            if (!connected && _commandQueue != null)
            {
                // Safety: force release PTT if radio disconnects
                _commandQueue.ForceReleasesPtt();
                Logger.LogVerbose(ModuleName, "Radio disconnected, forcing device to RX (Safety Measure)");
            }
        }

        public void SetOperateMode(bool operate)
        {
            if (_commandQueue == null) return;

            _commandQueue.SetOperateMode(operate);
            Logger.LogVerbose(ModuleName, $"Setting amplifier to {(operate ? "OPERATE" : "STANDBY")} mode");
        }

        public void SetRadioPtt(bool isPtt)
        {
            if (_statusTracker != null && _statusTracker.SetRadioPtt(isPtt))
            {
                // RadioPtt changed - update command queue PTT state
                _commandQueue?.OnPttStateChanged(isPtt);
            }
        }

        public void SetTransmitMode(string mode)
        {
            // Notify the plugin of the radio's current transmit mode (e.g. "USB", "CW", "AM").
        }

        #endregion

        #region ITunerPlugin Methods

        public TunerStatusData GetTunerStatus()
        {
            return _statusTracker?.GetTunerStatus() ?? new TunerStatusData();
        }

        public void SetInline(bool inline)
        {
            _commandQueue?.SetTunerInline(inline);
        }

        public void StartTune()
        {
            _commandQueue?.SetTuneStart(true);
        }

        public void StopTune()
        {
            _commandQueue?.SetTuneStart(false);
        }

        #endregion

        #region Event Handlers

        private void OnDataReceived(string data)
        {
            if (_parser == null || _statusTracker == null || _commandQueue == null) return;

            // Parse the response
            var update = _parser.Parse(data, _statusTracker);

            // Handle TX/RX acknowledgments
            if (update.IsPtt.HasValue)
            {
                _commandQueue.OnTxRxResponseReceived(update.IsPtt.Value);
            }

            // Handle firmware version
            if (update.FirmwareVersion.HasValue)
            {
                _commandQueue.SetFirmwareVersion(update.FirmwareVersion.Value);
            }

            // Determine what changed before applying the update
            bool hadAmpChange = update.AmpStateChanged || update.PttStateChanged || update.PttReady;
            bool hadTunerChange = update.TunerStateChanged || update.TuningStateChanged || update.TunerRelaysChanged;
            bool hadDeviceDataChange = update.AmpStateChanged || update.TunerStateChanged
                 || update.FaultCode.HasValue || update.BandNumber.HasValue || update.Antenna.HasValue
                 || update.FanSpeed.HasValue;

            // Apply to status tracker
            _statusTracker.ApplyUpdate(update);

            // Update command queue PTT state
            if (update.IsPtt.HasValue)
            {
                _commandQueue.OnPttStateChanged(update.IsPtt.Value);
            }

            // Update command queue tuning state
            if (update.TuningState.HasValue)
            {
                _commandQueue.OnTuningStateChanged(update.TuningState.Value == TunerTuningState.TuningInProgress);
            }

            // Raise amplifier events
            if (hadAmpChange)
            {
                var ampStatus = _statusTracker.GetAmplifierStatus();
                ampStatus.WhatChanged = DetermineAmpChange(update);
                StatusChanged?.Invoke(this, new AmplifierStatusEventArgs(ampStatus, PluginId));
            }

            // Raise tuner events
            if (hadTunerChange)
            {
                var tunerStatus = _statusTracker.GetTunerStatus();
                tunerStatus.WhatChanged = DetermineTunerChange(update);
                TunerStatusChanged?.Invoke(this, new TunerStatusEventArgs(tunerStatus, PluginId));
            }

            // Raise device data changed event for Device Control panel updates
            if (hadDeviceDataChange)
                DeviceDataChanged?.Invoke(this, EventArgs.Empty);

            // Raise meter data event on every status update from the device
            RaiseMeterDataEvent();
        }

        private void OnConnectionStateChanged(PluginConnectionState state)
        {
            var previous = ConnectionState;
            ConnectionStateChanged?.Invoke(this, new PluginConnectionStateChangedEventArgs(previous, state));

            if (state == PluginConnectionState.Connected)
            {
                Logger.LogInfo(ModuleName, "Connected to device");
            }
            else if (state == PluginConnectionState.Reconnecting)
            {
                Logger.LogInfo(ModuleName, "Reconnecting to device (RFKIT REST or serial/TCP transport)");
            }
            else if (state == PluginConnectionState.Connecting)
            {
                Logger.LogVerbose(ModuleName, "Connecting to device...");
            }
            else if (state == PluginConnectionState.Disconnected)
            {
                Logger.LogInfo(ModuleName, "Disconnected from device");
            }
        }

        private void RaiseMeterDataEvent()
        {
            if (_statusTracker == null) return;

            var readings = _statusTracker.GetMeterReadings();
            bool isTransmitting = _statusTracker.IsPtt || _statusTracker.RadioPtt;
            var args = new MeterDataEventArgs(readings, isTransmitting, PluginId);
            MeterDataAvailable?.Invoke(this, args);
        }

        #endregion

        #region IDevicePlugin Device Control

        public Dictionary<string, object> GetDeviceData()
        {
            return _statusTracker?.GetDeviceData() ?? new Dictionary<string, object>();
        }

        public bool SendDeviceCommand(string command)
        {
            Logger.LogLudicrous(ModuleName, $"Received device command: {command}");
            if (_connection == null || !_connection.IsConnected) return false;
            Logger.LogLudicrous(ModuleName, $"SendDeviceCommand: {command}");
            _connection.Send(command);
            return true;
        }

        /// <summary>
        /// Returns the LED layout shown in the Device Control window for this combined
        /// amplifier+tuner plugin.
        ///
        /// HOW IT WORKS — three-part data flow:
        ///   1. The poller in CommandQueue sends the commands in Constants.RxPollCommands on a
        ///      timer (Constants.PollingRxMs).  Each command triggers a device response.
        ///   2. ResponseParser turns each response into a StatusUpdate, and StatusTracker stores
        ///      the resulting state.  StatusTracker.GetDeviceData() exposes those values as a
        ///      Dictionary keyed by the same short strings used in ResponseKey below.
        ///   3. When DeviceDataChanged fires, the Controller re-fetches GetDeviceData() and
        ///      compares each value to the LED's ActiveValue (string, case-insensitive).
        ///      Match → ActiveColor + ActiveText.  No match → InactiveColor + InactiveText.
        ///      When the user clicks a LED, SendDeviceCommand() is called with either
        ///      ActiveCommand (if currently active) or InactiveCommand (if currently inactive).
        ///
        /// POLLER COMMANDS that populate these LEDs (see Constants.RxPollCommands):
        ///   $OPR;  → response "$OPR 1;" or "$OPR 0;" → StatusTracker["ON"] and ["OS"]
        ///   $ANT;  → response "$ANT 1;"  or "$ANT 2;" → StatusTracker["AN"]
        ///   $BYP;  → response "$BYP N;"  or "$BYP B;" → StatusTracker["AI"]
        ///   $FLT;  → response "$FLT n;"               → StatusTracker["FL"]
        /// </summary>
        public DeviceControlDefinition? GetDeviceControlDefinition()
        {
            return new DeviceControlDefinition
            {
                Elements = new List<DeviceControlElement>
                {
                    // ---------------------------------------------------------------
                    // POWER LED  (Amplifier)
                    //   ResponseKey "ON" populated by $OPR; poll.
                    //   StatusTracker["ON"] = 1 when AmpState is not Unknown or Standby.
                    //   Active (green)   = amplifier is powered on
                    //   Inactive (gray)  = amplifier is off or not yet responding
                    //   Click while ON   → sends $ON0; (power off)
                    //   Click while OFF  → sends $ON1; (power on)
                    //
                    //   IsPowerIndicator = true tells the Device Control panel that this
                    //   element represents the device power state.  Two behaviours follow:
                    //     1. This LED remains ENABLED even when the device is off, so the
                    //        user can always click it to power the device back on.
                    //     2. All other LEDs (and the fan buttons, if present) are disabled
                    //        automatically while this element is inactive (device off).
                    //   Set IsPowerIndicator on at most ONE element per definition.
                    // ---------------------------------------------------------------
                    // commented out by N3OC since the RFKIT doesn't support a power off command
                    new DeviceControlElement
                    {
                        ActiveColor      = "green",
                        InactiveColor    = "gray",
                        ActiveText       = "Power",
                        InactiveText     = "Power",
                        ActiveCommand    = "$ON0;",    // Send to turn device off
                        InactiveCommand  = "$ON1;",    // Send to turn device on
                        ResponseKey      = "ON",       // Matches GetDeviceData()["ON"]
                        ActiveValue      = "1",        // 1 = powered on
                        IsClickable      = false,
                        IsPowerIndicator = false        // Keeps this LED clickable when device is off
                    },

                    // ---------------------------------------------------------------
                    // OPERATE / STANDBY LED  (Amplifier)
                    //   ResponseKey "OS" populated by $OPR; poll.
                    //   StatusTracker["OS"] = 1 when AmpState is Operate or Transmit.
                    //   Active (green)   = amplifier in Operate mode (ready to TX)
                    //   Inactive (yellow)= amplifier in Standby mode (yellow = caution, not fault)
                    //   Click while OPERATE → sends $OS0; (go to standby)
                    //   Click while STANDBY → sends $OS1; (go to operate)
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "green",
                        InactiveColor  = "yellow",
                        ActiveText     = "Operate",
                        InactiveText   = "Standby",
                        ActiveCommand  = "$OS0;",    // Go to standby
                        InactiveCommand = "$OS1;",   // Go to operate
                        ResponseKey    = "OS",       // Matches GetDeviceData()["OS"]
                        ActiveValue    = "1",
                        IsClickable    = true
                    },

                    // ---------------------------------------------------------------
                    // ANTENNA 1 LED  (Shared by Amp + Tuner)
                    //   ResponseKey "AN" populated by $ANT; poll.
                    //   Device response: "$ANT 1;" sets AN = 1.
                    //   Active (green)   = Antenna 1 is currently selected
                    //   Inactive (gray)  = another antenna is selected
                    //   Click (any state)→ sends $ANT 1; to select antenna 1
                    //   NOTE: Ant1 and Ant2 share ResponseKey "AN" but have different
                    //         ActiveValue strings — only one can be active at a time.
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "green",
                        InactiveColor  = "gray",
                        ActiveText     = "Ant 1",
                        InactiveText   = "Ant 1",
                        ActiveCommand  = "$ANT 1;",  // Already on Ant1, re-select (harmless)
                        InactiveCommand = "$ANT 1;", // Switch to Ant1
                        ResponseKey    = "AN",       // Matches GetDeviceData()["AN"]
                        ActiveValue    = "1",        // Active when AN == 1
                        IsClickable    = true
                    },

                    // ---------------------------------------------------------------
                    // ANTENNA 2 LED  (Shared by Amp + Tuner)
                    //   Shares ResponseKey "AN" with Ant1, but ActiveValue = "2"
                    //   Active (green)   = Antenna 2 is currently selected
                    //   Inactive (gray)  = another antenna is selected
                    //   Click (any state)→ sends $ANT 2; to select antenna 2
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "green",
                        InactiveColor  = "gray",
                        ActiveText     = "Ant 2",
                        InactiveText   = "Ant 2",
                        ActiveCommand  = "$ANT 2;",  // Already on Ant2, re-select (harmless)
                        InactiveCommand = "$ANT 2;", // Switch to Ant2
                        ResponseKey    = "AN",       // Same key as Ant1, different ActiveValue
                        ActiveValue    = "2",        // Active when AN == 2
                        IsClickable    = true
                    },

                    // ---------------------------------------------------------------
                    // ANTENNA 3 LED  (Shared by Amp + Tuner)
                    //   Shares ResponseKey "AN" with Ant1, but ActiveValue = "2"
                    //   Active (green)   = Antenna 2 is currently selected
                    //   Inactive (gray)  = another antenna is selected
                    //   Click (any state)→ sends $ANT 2; to select antenna 2
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "green",
                        InactiveColor  = "gray",
                        ActiveText     = "Ant 3",
                        InactiveText   = "Ant 3",
                        ActiveCommand  = "$ANT 3;",  // Already on Ant2, re-select (harmless)
                        InactiveCommand = "$ANT 3;", // Switch to Ant2
                        ResponseKey    = "AN",       // Same key as Ant1, different ActiveValue
                        ActiveValue    = "3",        // Active when AN == 2
                        IsClickable    = true
                    },
                    // ---------------------------------------------------------------
                    // ANTENNA 4 LED  (Shared by Amp + Tuner)
                    //   Shares ResponseKey "AN" with Ant1, but ActiveValue = "2"
                    //   Active (green)   = Antenna 2 is currently selected
                    //   Inactive (gray)  = another antenna is selected
                    //   Click (any state)→ sends $ANT 2; to select antenna 2
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "green",
                        InactiveColor  = "gray",
                        ActiveText     = "Ant 4",
                        InactiveText   = "Ant 4",
                        ActiveCommand  = "$ANT 4;",  // Already on Ant2, re-select (harmless)
                        InactiveCommand = "$ANT 4;", // Switch to Ant2
                        ResponseKey    = "AN",       // Same key as Ant1, different ActiveValue
                        ActiveValue    = "4",        // Active when AN == 2
                        IsClickable    = true
                    },
                    // ---------------------------------------------------------------
                    // ATU INLINE / BYPASS LED  (Tuner)
                    //   ResponseKey "AI" populated by $BYP; poll.
                    //   Device response: "$BYP N;" = inline (not bypassed) → AI = 1
                    //                   "$BYP B;" = bypassed                → AI = 0
                    //   Active (green)   = ATU is inline (active matching)
                    //   Inactive (yellow)= ATU is bypassed (pass-through)
                    //   Click while INLINE  → sends $AI0; (bypass the ATU)
                    //   Click while BYPASS  → sends $AI1; (put ATU inline)
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "green",
                        InactiveColor  = "yellow",
                        ActiveText     = "Inline",
                        InactiveText   = "Bypass",
                        ActiveCommand  = "$AI0;",    // Currently inline → go to bypass
                        InactiveCommand = "$AI1;",   // Currently bypassed → go inline
                        ResponseKey    = "AI",       // Matches GetDeviceData()["AI"]
                        ActiveValue    = "1",        // 1 = inline
                        IsClickable    = true
                    },

                    // ---------------------------------------------------------------
                    // FAULT LED  (Amp + Tuner shared)
                    //   ResponseKey "FL" populated by $FLT; poll.
                    //   Active (red)     = a fault condition is present (FaultCode > 0)
                    //   Inactive (gray)  = no fault
                    //   Click while ACTIVE   → sends $FLC; (Clear Fault)
                    //   Click while INACTIVE → no-op (null = nothing sent)
                    // ---------------------------------------------------------------
                    new DeviceControlElement
                    {
                        ActiveColor    = "red",
                        InactiveColor  = "gray",
                        ActiveText     = "FAULT",
                        InactiveText   = "Fault",
                        ActiveCommand  = "$FLC;",    // Clear the fault
                        InactiveCommand = null,      // Nothing to do when no fault
                        ResponseKey    = "FL",       // Matches GetDeviceData()["FL"]
                        ActiveValue    = "1",        // Active when FaultCode > 0
                        IsClickable    = true
                    }
                },

                // ---------------------------------------------------------------
                // FAN SPEED ROW
                //   ResponseKey "FN" is populated by $FAN; poll → StatusTracker["FN"]
                //   MaxSpeed 6 matches the higher-power combined amp+tuner fan range (0–6).
                //   SetCommandPrefix "$FC" → button sends "$FC4;" to set speed 4.
                //
                //   Fan button enable/disable is automatic: because the Power element
                //   above has IsPowerIndicator = true, the panel disables the fan
                //   buttons whenever the device is off — no extra configuration needed.
                // ---------------------------------------------------------------
                // fan control not supported by RFKIT API so commented out by N3OC
                //FanControl = new FanControlDefinition
                //{
                //ResponseKey      = "FN",    // Matches GetDeviceData()["FN"]
                //MaxSpeed         = 6,        // 0 = off, 6 = full speed
                //SetCommandPrefix = "$FC",    // e.g. "$FC4;" to set speed 4
                //}
            };
        }

        #endregion

        #region Helpers

        private AmplifierStatusChange DetermineAmpChange(ResponseParser.StatusUpdate update)
        {
            if (update.PttReady) return AmplifierStatusChange.PttReady;
            if (update.PttStateChanged) return AmplifierStatusChange.PttStateChanged;
            if (update.AmpStateChanged) return AmplifierStatusChange.OperateStateChanged;
            return AmplifierStatusChange.General;
        }

        private TunerStatusChange DetermineTunerChange(ResponseParser.StatusUpdate update)
        {
            if (update.TuningStateChanged) return TunerStatusChange.TuningStateChanged;
            if (update.TunerStateChanged) return TunerStatusChange.OperateStateChanged;
            if (update.TunerRelaysChanged) return TunerStatusChange.RelayValuesChanged;
            return TunerStatusChange.General;
        }

        #endregion
    }
}
