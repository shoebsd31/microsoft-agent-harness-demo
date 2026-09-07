using System.Diagnostics;
using OpenTelemetry;
using ProcurementCopilot.Application.Security;

namespace ProcurementCopilot.Infrastructure.Telemetry;

/// <summary>Redacts API keys and email addresses from every string span attribute before export, regardless of the sensitive-data switch.</summary>
public sealed class RedactingProcessor : BaseProcessor<Activity>
{
    private readonly SecretRedactor _redactor;

    /// <summary>Initializes the processor.</summary>
    public RedactingProcessor(SecretRedactor redactor) => _redactor = redactor;

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        foreach (KeyValuePair<string, object?> tag in data.TagObjects.ToArray())
        {
            if (tag.Value is string text && _redactor.ContainsSensitiveData(text))
            {
                data.SetTag(tag.Key, _redactor.Redact(text));
            }
        }

        if (data.DisplayName is { } name && _redactor.ContainsSensitiveData(name))
        {
            data.DisplayName = _redactor.Redact(name);
        }
    }
}
