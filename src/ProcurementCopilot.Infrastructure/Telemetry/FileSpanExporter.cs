using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OpenTelemetry;

namespace ProcurementCopilot.Infrastructure.Telemetry;

/// <summary>Writes finished spans as JSON lines to <c>logs/traces-yyyyMMdd.jsonl</c>. Used when no OTLP endpoint is configured.</summary>
public sealed class FileSpanExporter : BaseExporter<Activity>
{
    private readonly string _directory;
    private readonly object _lock = new();

    /// <summary>Initializes the exporter for a directory.</summary>
    public FileSpanExporter(string directory) => _directory = directory;

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            string path = Path.Combine(_directory, $"traces-{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.jsonl");
            lock (_lock)
            {
                using StreamWriter writer = File.AppendText(path);
                foreach (Activity activity in batch)
                {
                    writer.WriteLine(Serialize(activity));
                }
            }

            return ExportResult.Success;
        }
        catch (IOException)
        {
            return ExportResult.Failure;
        }
    }

    private static string Serialize(Activity activity)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("name", activity.DisplayName);
            json.WriteString("source", activity.Source.Name);
            json.WriteString("traceId", activity.TraceId.ToString());
            json.WriteString("spanId", activity.SpanId.ToString());
            json.WriteString("parentSpanId", activity.ParentSpanId.ToString());
            json.WriteString("start", activity.StartTimeUtc.ToString("O", CultureInfo.InvariantCulture));
            json.WriteNumber("durationMs", activity.Duration.TotalMilliseconds);
            json.WriteString("status", activity.Status.ToString());
            json.WriteStartObject("tags");
            foreach (KeyValuePair<string, object?> tag in activity.TagObjects)
            {
                json.WriteString(tag.Key, tag.Value?.ToString());
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
