using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RfkitEmulator;

/// <summary>
/// Thread-safe in-memory RFKIT device state (Phase 3). Mutated by PUT/POST; reflected in GET responses.
/// </summary>
public sealed class EmulatorStateStore
{
    private readonly object _sync = new();
    private readonly ILogger<EmulatorStateStore> _logger;

    private readonly JsonSerializerOptions _jsonWrite = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly JsonElement _info;
    private readonly PowerDto _powerBaseline;
    private readonly TunerDto _tunerBaseline;

    private DataDto _data;
    private TunerDto _tuner;
    private List<AntennaWithStateDto> _antennas;
    private AntennaRefDto _activeAntenna;
    private OperationalInterfaceDto _operationalInterface;
    private string _operateMode;

    public EmulatorStateStore(ILogger<EmulatorStateStore> logger, IHostEnvironment env)
    {
        _logger = logger;

        var responsesPath = Path.Combine(AppContext.BaseDirectory, "Responses.json");
        if (!File.Exists(responsesPath))
            throw new FileNotFoundException("Responses.json not found next to executable.", responsesPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(responsesPath));
        var root = doc.RootElement;

        _info = root.GetProperty("info").Clone();
        _powerBaseline = root.GetProperty("power").Deserialize<PowerDto>(_jsonWrite)
                         ?? throw new InvalidOperationException("Invalid power in Responses.json");
        _data = root.GetProperty("data").Deserialize<DataDto>(_jsonWrite)
                ?? throw new InvalidOperationException("Invalid data in Responses.json");
        _tuner = root.GetProperty("tuner").Deserialize<TunerDto>(_jsonWrite)
                 ?? throw new InvalidOperationException("Invalid tuner in Responses.json");
        _tunerBaseline = _tuner;
        _antennas = root.GetProperty("antennas").Deserialize<List<AntennaWithStateDto>>(_jsonWrite)
                    ?? throw new InvalidOperationException("Invalid antennas in Responses.json");
        _activeAntenna = root.GetProperty("activeAntenna").Deserialize<AntennaRefDto>(_jsonWrite)
                         ?? throw new InvalidOperationException("Invalid activeAntenna in Responses.json");
        _operationalInterface = root.GetProperty("operationalInterface").Deserialize<OperationalInterfaceDto>(_jsonWrite)
                                ?? throw new InvalidOperationException("Invalid operationalInterface in Responses.json");
        _operateMode = root.GetProperty("operateMode").GetProperty("operate_mode").GetString() ?? "OPERATE";

        _logger.LogInformation(
            "Emulator state initialized from Responses.json (Environment: {Environment})",
            env.EnvironmentName);
    }

    public IResult GetInfo()
    {
        lock (_sync)
        {
            return Json(_info);
        }
    }

    public IResult GetData()
    {
        lock (_sync)
        {
            return Json(_data);
        }
    }

    public IResult GetPower()
    {
        lock (_sync)
        {
            return Json(BuildPowerResponse());
        }
    }

    public IResult GetTuner()
    {
        lock (_sync)
        {
            return Json(_tuner);
        }
    }

    public IResult GetAntennas()
    {
        lock (_sync)
        {
            SyncAntennaStatesWithActive();
            return Json(_antennas);
        }
    }

    public IResult GetActiveAntenna()
    {
        lock (_sync)
        {
            return Json(_activeAntenna);
        }
    }

    public async Task<IResult> SetActiveAntennaAsync(HttpRequest request)
    {
        string raw;
        AntennaRefDto? dto;
        try
        {
            raw = await ReadBodyAsync(request).ConfigureAwait(false);
            dto = JsonSerializer.Deserialize<AntennaRefDto>(raw, _jsonWrite);
            if (dto?.Type is null)
                return Results.BadRequest();
            if (dto.Type == "INTERNAL" && dto.Number is null)
                return Results.BadRequest();
            if (dto.Type == "EXTERNAL" && dto.Number is null)
                return Results.BadRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        lock (_sync)
        {
            _activeAntenna = dto!;
            SyncAntennaStatesWithActive();
            _logger.LogInformation(
                "Active antenna set to {AntennaType} number {AntennaNumber}",
                dto!.Type,
                dto.Number);
            return Json(_activeAntenna);
        }
    }

    public IResult GetOperationalInterface()
    {
        lock (_sync)
        {
            return Json(_operationalInterface);
        }
    }

    public async Task<IResult> SetOperationalInterfaceAsync(HttpRequest request)
    {
        string raw;
        OperationalInterfaceDto? dto;
        try
        {
            raw = await ReadBodyAsync(request).ConfigureAwait(false);
            dto = JsonSerializer.Deserialize<OperationalInterfaceDto>(raw, _jsonWrite);
            if (dto?.OperationalInterface is null)
                return Results.BadRequest();
            if (dto.OperationalInterface is not ("UNIV" or "CAT" or "UDP" or "TCI"))
                return Results.BadRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        lock (_sync)
        {
            _operationalInterface = dto!;
            _logger.LogInformation("Operational interface set to {Interface}", dto!.OperationalInterface);
            return Json(_operationalInterface);
        }
    }

    public IResult ResetError()
    {
        lock (_sync)
        {
            _data = _data with { Status = "" };
            _logger.LogInformation("Error reset (POST /error/reset): status cleared");
            return Results.Ok();
        }
    }

    public IResult GetOperateMode()
    {
        lock (_sync)
        {
            return Json(new OperateModeDto(_operateMode));
        }
    }

    public async Task<IResult> SetOperateModeAsync(HttpRequest request)
    {
        string raw;
        OperateModeDto? dto;
        try
        {
            raw = await ReadBodyAsync(request).ConfigureAwait(false);
            dto = JsonSerializer.Deserialize<OperateModeDto>(raw, _jsonWrite);
            if (dto?.OperateMode is null)
                return Results.BadRequest();
            if (dto.OperateMode is not ("OPERATE" or "STANDBY"))
                return Results.BadRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        lock (_sync)
        {
            _operateMode = dto!.OperateMode;
            if (_operateMode == "STANDBY")
                _tuner = _tunerBaseline with { Mode = "AUTO", Setup = "BYPASS" };
            else
                _tuner = _tunerBaseline;

            _logger.LogInformation("Operate mode set to {OperateMode}", _operateMode);
            return Json(new OperateModeDto(_operateMode));
        }
    }

    private PowerDto BuildPowerResponse()
    {
        if (_operateMode == "STANDBY")
        {
            return _powerBaseline with
            {
                Current = new UnitNumberDto(0, "A"),
                Forward = new IntWithMaxAndUnitDto(0, _powerBaseline.Forward.MaxValue, "W"),
                Reflected = new IntWithMaxAndUnitDto(0, _powerBaseline.Reflected.MaxValue, "W"),
                Swr = new FloatWithMaxAndUnitDto(1.0, 1.0, "")
            };
        }

        if (string.Equals(_tuner.Mode, "AUTO_TUNING", StringComparison.Ordinal))
        {
            return _powerBaseline with
            {
                Reflected = new IntWithMaxAndUnitDto(5, 100, "W"),
                Swr = new FloatWithMaxAndUnitDto(1.4, 3.0, "")
            };
        }

        return _powerBaseline;
    }

    /// <summary>
    /// INTERNAL slot 3 stays DISABLED when not active (matches seed Responses.json).
    /// </summary>
    private void SyncAntennaStatesWithActive()
    {
        foreach (var a in _antennas)
        {
            bool isActive = false;
            if (_activeAntenna.Type == "INTERNAL")
                isActive = a.Type == "INTERNAL" && a.Number == _activeAntenna.Number;
            else if (_activeAntenna.Type == "EXTERNAL")
                isActive = a.Type == "EXTERNAL" && a.Number == _activeAntenna.Number;

            if (isActive)
                a.State = "ACTIVE";
            else if (a.Type == "INTERNAL" && a.Number == 3)
                a.State = "DISABLED";
            else
                a.State = "AVAILABLE";
        }
    }

    private IResult Json<T>(T value) =>
        Results.Text(JsonSerializer.Serialize(value, _jsonWrite), "application/json; charset=utf-8", Encoding.UTF8);

    private IResult Json(JsonElement element) =>
        Results.Text(element.GetRawText(), "application/json; charset=utf-8", Encoding.UTF8);

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var raw = await reader.ReadToEndAsync().ConfigureAwait(false);
        request.Body.Position = 0;
        return raw;
    }

    public sealed record DataDto(
        [property: JsonPropertyName("band")] IntWithUnitDto Band,
        [property: JsonPropertyName("frequency")] IntWithUnitDto Frequency,
        [property: JsonPropertyName("status")] string Status);

    public sealed record IntWithUnitDto([property: JsonPropertyName("value")] int Value, [property: JsonPropertyName("unit")] string Unit);

    public sealed record UnitNumberDto([property: JsonPropertyName("value")] double Value, [property: JsonPropertyName("unit")] string Unit);

    public sealed record FloatWithUnitDto([property: JsonPropertyName("value")] double Value, [property: JsonPropertyName("unit")] string Unit);

    public sealed record IntWithMaxAndUnitDto(
        [property: JsonPropertyName("value")] int Value,
        [property: JsonPropertyName("max_value")] int MaxValue,
        [property: JsonPropertyName("unit")] string Unit);

    public sealed record FloatWithMaxAndUnitDto(
        [property: JsonPropertyName("value")] double Value,
        [property: JsonPropertyName("max_value")] double MaxValue,
        [property: JsonPropertyName("unit")] string Unit);

    public sealed record PowerDto(
        [property: JsonPropertyName("temperature")] FloatWithUnitDto Temperature,
        [property: JsonPropertyName("voltage")] FloatWithUnitDto Voltage,
        [property: JsonPropertyName("current")] UnitNumberDto Current,
        [property: JsonPropertyName("forward")] IntWithMaxAndUnitDto Forward,
        [property: JsonPropertyName("reflected")] IntWithMaxAndUnitDto Reflected,
        [property: JsonPropertyName("swr")] FloatWithMaxAndUnitDto Swr);

    public sealed record TunerDto(
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("setup")] string? Setup,
        [property: JsonPropertyName("tuned_frequency")] IntWithUnitDto? TunedFrequency,
        [property: JsonPropertyName("segment_size")] IntWithUnitDto? SegmentSize,
        [property: JsonPropertyName("L")] IntWithUnitDto? L,
        [property: JsonPropertyName("C")] IntWithUnitDto? C);

    public sealed class AntennaWithStateDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("number")]
        public int? Number { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } = "";
    }

    public sealed record AntennaRefDto(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("number")] int? Number);

    public sealed record OperationalInterfaceDto(
        [property: JsonPropertyName("operational_interface")] string OperationalInterface,
        [property: JsonPropertyName("error")] string? Error = null);

    public sealed record OperateModeDto([property: JsonPropertyName("operate_mode")] string OperateMode);
}
