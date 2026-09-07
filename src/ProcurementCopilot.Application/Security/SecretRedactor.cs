using System.Text.RegularExpressions;

namespace ProcurementCopilot.Application.Security;

/// <summary>
/// Removes secrets and personal data from free text before it reaches logs, exceptions or telemetry.
/// Redacts registered secret values, <c>api-key=…</c> / <c>Bearer …</c> patterns, common key shapes and email addresses.
/// </summary>
public sealed partial class SecretRedactor
{
    /// <summary>Replacement for secrets.</summary>
    public const string SecretMask = "[REDACTED]";

    /// <summary>Replacement for email addresses.</summary>
    public const string EmailMask = "[REDACTED_EMAIL]";

    private readonly List<string> _secrets = [];

    /// <summary>Initializes a redactor with optional known secret values.</summary>
    public SecretRedactor(params IEnumerable<string?> knownSecrets)
    {
        foreach (string? secret in knownSecrets)
        {
            Register(secret);
        }
    }

    /// <summary>Adds a literal secret value that must never appear in output.</summary>
    public void Register(string? secret)
    {
        if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= 6)
        {
            _secrets.Add(secret);
        }
    }

    /// <summary>Redacts secrets and email addresses in the text.</summary>
    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        string result = text;
        foreach (string secret in _secrets)
        {
            result = result.Replace(secret, SecretMask, StringComparison.Ordinal);
        }

        result = KeyValuePattern().Replace(result, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{SecretMask}");
        result = BearerPattern().Replace(result, $"Bearer {SecretMask}");
        result = KeyShapePattern().Replace(result, SecretMask);
        result = EmailPattern().Replace(result, EmailMask);
        return result;
    }

    /// <summary>Returns <see langword="true"/> when the text contains something the redactor would change.</summary>
    public bool ContainsSensitiveData(string? text) => !string.IsNullOrEmpty(text) && !string.Equals(text, Redact(text), StringComparison.Ordinal);

    [GeneratedRegex(@"(?<key>\b(?:api[-_]?key|apikey|x-api-key|password|pwd|secret|token|authorization)\b)(?<sep>\s*[:=]\s*)(?<value>(?!Bearer[ ])[^\s,;""']+)", RegexOptions.IgnoreCase)]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"\b(?:sk-[A-Za-z0-9_-]{16,}|[A-Fa-f0-9]{32,})\b")]
    private static partial Regex KeyShapePattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();
}
