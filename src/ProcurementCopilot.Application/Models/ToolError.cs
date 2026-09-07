using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Application.Models;

/// <summary>Structured error returned to the model instead of throwing.</summary>
/// <param name="Error">Always <see langword="true"/>.</param>
/// <param name="Code">Stable error code.</param>
/// <param name="Message">What went wrong and how to fix the call.</param>
public sealed record ToolError(bool Error, string Code, string Message)
{
    /// <summary>Serialises a domain error as a JSON tool error.</summary>
    public static string FromError(Error error) =>
        ToolJson.Serialize(new ToolError(true, error.Code, error.Message));
}
