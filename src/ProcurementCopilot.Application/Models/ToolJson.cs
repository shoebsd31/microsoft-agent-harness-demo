using System.Text.Encodings.Web;
using System.Text.Json;

namespace ProcurementCopilot.Application.Models;

/// <summary>
/// Serialises tool payloads with the source-generated context and relaxed escaping, so the
/// <c>&lt;untrusted_data&gt;</c> envelope reaches the model verbatim instead of as unicode-escaped angle brackets.
/// </summary>
public static class ToolJson
{
    /// <summary>Shared options: camel case, string enums, nulls omitted, relaxed escaping.</summary>
    public static readonly JsonSerializerOptions Options = new(ToolJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Serialises a payload whose type is registered in <see cref="ToolJsonContext"/>.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
