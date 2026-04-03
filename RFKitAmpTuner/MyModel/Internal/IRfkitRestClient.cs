#nullable enable

using System.Text.Json;

namespace RFKitAmpTuner.MyModel.Internal
{
    /// <summary>
    /// Abstracts HTTP for <see cref="RfkitCommandMapper"/> (real <see cref="System.Net.Http.HttpClient"/> in production; fakes in unit tests). Phase 6.
    /// </summary>
    internal interface IRfkitRestClient
    {
        /// <summary>GET JSON; returns <c>null</c> on non-success or transport error.</summary>
        JsonDocument? Get(string relativePath);

        /// <summary>PUT with JSON body. Returns <c>true</c> on success (2xx).</summary>
        bool PutJson(string relativePath, string jsonBody);

        /// <summary>POST with empty body. Returns <c>true</c> on success (2xx).</summary>
        bool PostWithoutBody(string relativePath);
    }
}
