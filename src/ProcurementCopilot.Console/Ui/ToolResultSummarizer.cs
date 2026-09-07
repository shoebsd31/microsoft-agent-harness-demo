using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ProcurementCopilot.ConsoleApp.Ui;

/// <summary>Turns tool arguments and results into the one-line summaries shown in the console.</summary>
public static class ToolResultSummarizer
{
    /// <summary>Formats call arguments as a compact comma list of values.</summary>
    public static string Arguments(FunctionCallContent call)
    {
        if (call.Arguments is null || call.Arguments.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", call.Arguments.Values.Select(v => Render.Truncate(Scalar(v), 40)));
    }

    /// <summary>Summarises a tool result: score, verdict, saved path, error code or a text excerpt.</summary>
    public static string Result(object? result)
    {
        string text = result?.ToString() ?? string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.True)
                {
                    return "error " + root.GetProperty("code").GetString();
                }

                foreach (string key in new[] { "weightedTotal", "verdict", "savedTo", "converted", "title", "name" })
                {
                    if (root.TryGetProperty(key, out JsonElement value))
                    {
                        return value.ValueKind == JsonValueKind.Number ? value.GetDecimal().ToString("0.00", CultureInfo.InvariantCulture) : value.ToString();
                    }
                }
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                return $"{root.GetArrayLength()} items";
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall through to the text excerpt.
        }

        return Render.Truncate(text, 80);
    }

    private static readonly JsonSerializerOptions Compact = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Scalar(object? value) => value switch
    {
        null => "null",
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? string.Empty,
        JsonElement e => e.ToString(),
        string s => s,
        bool or int or long or decimal or double => value.ToString() ?? string.Empty,
        _ => Compose(value),
    };

    private static string Compose(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, value.GetType(), Compact);
        }
        catch (NotSupportedException)
        {
            return value.ToString() ?? string.Empty;
        }
    }
}
