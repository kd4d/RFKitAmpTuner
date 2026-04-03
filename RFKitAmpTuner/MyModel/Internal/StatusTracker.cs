#nullable enable

using PgTg.AMP;
using PgTg.Common;
using PgTg.Plugins.Core;
using System;
using System.Collections.Generic;
using System.Data.Common;
using MeterUnits = PgTg.RADIO.MeterUnits;

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// Tracks current status of the RFKIT RF2K-S amplifier+tuner plugin.
    /// Maintains both amplifier and tuner state. Thread-safe via lock.
    /// </summary>
    internal class StatusTracker
    {
        private const string ModuleName = "StatusTracker";
        private readonly object _lock = new();

        // Amplifier state
        public AmpOperateState AmpState { get; private set; } = AmpOperateState.Unknown;
        public bool IsPtt { get; private set; }

        /// <summary>
        /// Radio's PTT state from interlock status (TRANSMITTING).
        /// May lead the device's PTT detection, especially with hardware keying.
        /// </summary>
        public bool RadioPtt { get; private set; }

        public double ForwardPower { get; private set; }
        public double SWR { get; private set; } = 1.0;
        public double ReturnLoss { get; private set; } = 99;
        public int Temperature { get; private set; }
        public double Voltage { get; private set; }
        public double Current { get; private set; }
        public int BandNumber { get; private set; }
        public string BandName { get; private set; } = string.Empty;
        public int FaultCode { get; private set; }
        public string SerialNumber { get; private set; } = string.Empty;

        public static string GetFaultDescription(int faultCode) => faultCode switch
        {
            // these must match lookup switch in RfkitCatFromJson.FltLineFromData
            0 => string.Empty,
            1 => "Severe LPF Error",
            2 => "High Current",
            3 => "Wrong Frequency",
            4 => "Overheating",
            5 => "High Antenna Reflection",
            6 => "High Input Power",
            7 => "High Output Power",
            8 => "No Internal High Voltage",
            9 => "High SWR",
            _ => $"Fault (code {faultCode})"
        };
        public double FirmwareVersion { get; private set; }
        public bool IsVitaDataPopulated { get; private set; }

        // Added: track current plugin connection state so GetDeviceData can report ON/OFF
        public PluginConnectionState ConnectionState { get; set; } = PluginConnectionState.Disconnected;

        // Tuner state
        public TunerOperateState TunerState { get; private set; } = TunerOperateState.Unknown;
        public TunerTuningState TuningState { get; private set; } = TunerTuningState.Unknown;
        public int InductorValue { get; private set; }
        public int CapacitorValue { get; private set; }
        public int Antenna { get; private set; }
        public int FanSpeed { get; private set; }      // Fan speed 0–6 (from $FAN; poll)
        public double TunerSWR { get; private set; } = 1.0;
        public double VFWD { get; private set; }

        /// <summary>
        /// Convert raw VFWD ADC value to Watts using power-law calibration.
        /// P = 0.000721 * VFWD^1.803
        /// </summary>
        /// // RFKIT returns watts, not ADC voltage, so commented out by N3OC and replaced with direct ForwardPower reading from device
        //private double TunerForwardPowerWatts => VFWD > 0 ? Math.Pow(VFWD, 1.803) * 0.000721 : 0.0;
        private double TunerForwardPowerWatts => VFWD > 0 ? VFWD : 0.0;

        /// <summary>
        /// Apply a status update from the parser.
        /// </summary>
        public void ApplyUpdate(ResponseParser.StatusUpdate update)
        {
            lock (_lock)
            {
                // Amplifier updates
                if (update.AmpState.HasValue) AmpState = update.AmpState.Value;
                if (update.IsPtt.HasValue) IsPtt = update.IsPtt.Value;
                if (update.ForwardPower.HasValue) ForwardPower = update.ForwardPower.Value;
                if (update.SWR.HasValue) SWR = update.SWR.Value;
                if (update.ReturnLoss.HasValue) ReturnLoss = update.ReturnLoss.Value;
                if (update.Temperature.HasValue) Temperature = update.Temperature.Value;
                if (update.Voltage.HasValue) Voltage = update.Voltage.Value;
                if (update.Current.HasValue) Current = update.Current.Value;
                if (update.BandNumber.HasValue) BandNumber = update.BandNumber.Value;
                if (update.BandName != null) BandName = update.BandName;
                if (update.FaultCode.HasValue) FaultCode = update.FaultCode.Value;
                if (update.SerialNumber != null) SerialNumber = update.SerialNumber;
                if (update.FirmwareVersion.HasValue) FirmwareVersion = update.FirmwareVersion.Value;
                if (update.IsVitaDataPopulated) IsVitaDataPopulated = true;

                // Tuner updates
                if (update.TunerState.HasValue) TunerState = update.TunerState.Value;
                if (update.TuningState.HasValue) TuningState = update.TuningState.Value;
                if (update.InductorValue.HasValue) InductorValue = update.InductorValue.Value;
                if (update.CapacitorValue.HasValue) CapacitorValue = update.CapacitorValue.Value;
                if (update.Antenna.HasValue) Antenna = update.Antenna.Value;
                if (update.FanSpeed.HasValue) FanSpeed = update.FanSpeed.Value;
                if (update.TunerSWR.HasValue) TunerSWR = update.TunerSWR.Value;
                if (update.VFWD.HasValue) VFWD = update.VFWD.Value;
            }
        }

        /// <summary>
        /// Get current amplifier status for events.
        /// </summary>
        public AmplifierStatusData GetAmplifierStatus()
        {
            lock (_lock)
            {
                return new AmplifierStatusData
                {
                    OperateState = AmpState,
                    IsPttActive = IsPtt,
                    BandNumber = BandNumber,
                    BandName = BandName,
                    FaultCode = FaultCode,
                    FirmwareVersion = FirmwareVersion.ToString("F2"),
                    SerialNumber = SerialNumber,
                    ForwardPower = ForwardPower,
                    SWR = SWR,
                    ReturnLoss = ReturnLoss,
                    Temperature = Temperature
                };
            }
        }

        /// <summary>
        /// Get current tuner status for events.
        /// </summary>
        public TunerStatusData GetTunerStatus()
        {
            lock (_lock)
            {
                return new TunerStatusData
                {
                    OperateState = TunerState,
                    TuningState = TuningState,
                    InductorValue = InductorValue,
                    Capacitor1Value = CapacitorValue,
                    Capacitor2Value = 0,
                    LastSwr = TunerSWR,
                    FirmwareVersion = FirmwareVersion.ToString("F2"),
                    SerialNumber = SerialNumber,
                    ForwardPower = TunerForwardPowerWatts
                };
            }
        }

        /// <summary>
        /// Get meter readings for VITA-49 sender.
        /// Returns zero values for power/SWR when not transmitting to prevent frozen meter display.
        /// Uses RadioPtt OR IsPtt to determine transmit state.
        /// </summary>
        public Dictionary<MeterType, MeterReading> GetMeterReadings()
        {
            lock (_lock)
            {
                // Use RadioPtt (from radio interlock) OR IsPtt (from device) to determine if transmitting.
                // RadioPtt may be true before device IsPtt is detected (especially with hardware keying).
                bool isTransmitting = RadioPtt || IsPtt;

                // Use current values if transmitting, otherwise force zeros to prevent meter freeze
                double currentFwdPower = isTransmitting ? ForwardPower : 0;
                double currentSwr = isTransmitting ? SWR : 1.0;
                double currentReturnLoss = isTransmitting ? ReturnLoss : 99;
                double currentTunerFwdPower = isTransmitting ? TunerForwardPowerWatts : 0;
                double currentTunerSwr = isTransmitting ? TunerSWR : 1.0;

                var readings = new Dictionary<MeterType, MeterReading>
                {
                    [MeterType.ForwardPower] = new MeterReading(MeterType.ForwardPower, currentFwdPower, MeterUnits.Watts),
                    [MeterType.SWR] = new MeterReading(MeterType.SWR, currentSwr, MeterUnits.SWR),
                    [MeterType.ReturnLoss] = new MeterReading(MeterType.ReturnLoss, currentReturnLoss, MeterUnits.Db),
                    [MeterType.Temperature] = new MeterReading(MeterType.Temperature, Temperature, MeterUnits.DegreesC),
                    [MeterType.TunerForwardPower] = new MeterReading(MeterType.TunerForwardPower, currentTunerFwdPower, MeterUnits.Watts),
                    [MeterType.TunerSWR] = new MeterReading(MeterType.TunerSWR, currentTunerSwr, MeterUnits.SWR),
                    [MeterType.TunerReturnLoss] = new MeterReading(MeterType.TunerReturnLoss, currentReturnLoss, MeterUnits.Db)
                };

                return readings;
            }
        }

        /// <summary>
        /// Get device data for the /device WebSocket endpoint and Device Control panel.
        /// </summary>
        public Dictionary<string, object> GetDeviceData()
        {
            lock (_lock)
            {
                return new Dictionary<string, object>
                {
                    ["ON"] = ConnectionState == PluginConnectionState.Connected ? 0 : 1,
                    ["OS"] = AmpState == AmpOperateState.Operate || AmpState == AmpOperateState.Transmit ? 1 : 0,
                    ["AN"] = Antenna,
                    ["AI"] = TunerState == TunerOperateState.Inline ? 1 : 0,
                    ["FL"] = FaultCode > 0 ? 1 : 0,
                    ["FaultDesc"] = GetFaultDescription(FaultCode),
                    ["BN"] = BandNumber,

                    // "FN" — Fan speed: integer 0–6 fed to the fan control row
                    //         Up/down buttons send $FC0;–$FC6; (FanControlDefinition.SetCommandPrefix = "$FC")
                    ["FN"] = FanSpeed
                };
            }
        }

        /// <summary>
        /// Zero meter values (for shutdown).
        /// </summary>
        public void ZeroMeterValues()
        {
            lock (_lock)
            {
                ForwardPower = 0;
                SWR = 1.0;
                ReturnLoss = 99;
                Temperature = 0;
                TunerSWR = 1.0;
                VFWD = 0;
            }
        }

        /// <summary>
        /// Set the radio's PTT state from interlock status.
        /// Called when the radio transitions to/from TRANSMITTING state.
        /// </summary>
        /// <returns>True if the RadioPtt value changed.</returns>
        public bool SetRadioPtt(bool isPtt)
        {
            lock (_lock)
            {
                if (RadioPtt != isPtt)
                {
                    Logger.LogVerbose(ModuleName, $"RadioPtt changed: {RadioPtt} -> {isPtt}");
                    RadioPtt = isPtt;
                    return true;
                }
            }
            return false;
        }
    }
}
