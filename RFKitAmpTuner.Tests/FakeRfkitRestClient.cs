using System.Collections.Generic;
using System.Text.Json;
using RFKitAmpTuner.MyModel.Internal;

namespace RFKitAmpTuner.Tests;

/// <summary>In-memory <see cref="IRfkitRestClient"/> for Phase 6 unit tests.</summary>
internal sealed class FakeRfkitRestClient : IRfkitRestClient
{
    public Dictionary<string, string> JsonByPath { get; } = new();

    public List<(string Method, string Path, string? Body)> Calls { get; } = new();

    public JsonDocument? Get(string relativePath)
    {
        Calls.Add(("GET", relativePath, null));
        if (!JsonByPath.TryGetValue(relativePath, out var json))
            return null;
        return JsonDocument.Parse(json);
    }

    public bool PutJson(string relativePath, string jsonBody)
    {
        Calls.Add(("PUT", relativePath, jsonBody));
        return true;
    }

    public bool PostWithoutBody(string relativePath)
    {
        Calls.Add(("POST", relativePath, null));
        return true;
    }
}
