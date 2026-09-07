namespace ProcurementCopilot.Application.Security;

/// <summary>
/// Wraps third-party text (vendor notes, bid clauses, search results) in a clearly delimited envelope so the model
/// treats it as data. The closing tag is neutralised inside the content so it cannot be terminated early.
/// </summary>
public static class UntrustedDataEnvelope
{
    /// <summary>Opening tag prefix.</summary>
    public const string OpenPrefix = "<untrusted_data source=\"";

    /// <summary>Closing tag.</summary>
    public const string Close = "</untrusted_data>";

    /// <summary>Wraps the content, labelling its source such as <c>vendor:VND-0003</c>.</summary>
    public static string Wrap(string source, string? content)
    {
        string safeSource = source.Replace("\"", "'", StringComparison.Ordinal).Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal);
        string body = (content ?? string.Empty)
            .Replace(Close, "&lt;/untrusted_data&gt;", StringComparison.OrdinalIgnoreCase)
            .Replace(OpenPrefix, "&lt;untrusted_data source=\"", StringComparison.OrdinalIgnoreCase);
        return $"{OpenPrefix}{safeSource}\">{body}{Close}";
    }

    /// <summary>Wraps vendor-authored text.</summary>
    public static string ForVendor(string vendorId, string? content) => Wrap($"vendor:{vendorId}", content);

    /// <summary>Wraps bid-authored text.</summary>
    public static string ForBid(string bidId, string? content) => Wrap($"bid:{bidId}", content);

    /// <summary>Returns <see langword="true"/> when the text is already a complete envelope.</summary>
    public static bool IsWrapped(string? text) =>
        text is not null && text.StartsWith(OpenPrefix, StringComparison.Ordinal) && text.EndsWith(Close, StringComparison.Ordinal);
}
