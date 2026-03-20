using Microsoft.AspNetCore.HttpLogging;
using RfkitEmulator;

var builder = WebApplication.CreateBuilder(args);

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(urls))
    builder.WebHost.UseUrls("http://0.0.0.0:8080");

var bodyLimitReq = builder.Configuration.GetValue("RfkitEmulator:HttpLoggingRequestBodyLimit", 4096);
var bodyLimitRes = builder.Configuration.GetValue("RfkitEmulator:HttpLoggingResponseBodyLimit", 4096);

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestQuery
        | HttpLoggingFields.RequestBody
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.ResponseBody
        | HttpLoggingFields.Duration;
    options.RequestBodyLogLimit = bodyLimitReq;
    options.ResponseBodyLogLimit = bodyLimitRes;
    options.MediaTypeOptions.AddText("application/json");
});

builder.Services.AddSingleton<EmulatorStateStore>();

var app = builder.Build();

// Allow HttpLogging and minimal API handlers to both read the request body (single stream).
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next().ConfigureAwait(false);
});

app.UseHttpLogging();

app.MapGet("/info", (EmulatorStateStore state) => state.GetInfo());
app.MapGet("/data", (EmulatorStateStore state) => state.GetData());
app.MapGet("/power", (EmulatorStateStore state) => state.GetPower());
app.MapGet("/tuner", (EmulatorStateStore state) => state.GetTuner());
app.MapGet("/antennas", (EmulatorStateStore state) => state.GetAntennas());
app.MapGet("/antennas/active", (EmulatorStateStore state) => state.GetActiveAntenna());
app.MapPut("/antennas/active", (EmulatorStateStore state, HttpRequest req) => state.SetActiveAntennaAsync(req));
app.MapGet("/operational-interface", (EmulatorStateStore state) => state.GetOperationalInterface());
app.MapPut("/operational-interface", (EmulatorStateStore state, HttpRequest req) => state.SetOperationalInterfaceAsync(req));
app.MapPost("/error/reset", (EmulatorStateStore state) => state.ResetError());
app.MapGet("/operate-mode", (EmulatorStateStore state) => state.GetOperateMode());
app.MapPut("/operate-mode", (EmulatorStateStore state, HttpRequest req) => state.SetOperateModeAsync(req));

var urlDisplay = urls ?? "http://0.0.0.0:8080";
app.Logger.LogInformation("RfkitEmulator Phase 3 (stateful + HttpLogging) listening on {Urls}", urlDisplay);
app.Logger.LogInformation("HttpLogging: request/response body limits {Req} / {Res} bytes", bodyLimitReq, bodyLimitRes);

app.Run();
