namespace ProcurementCopilot.ConsoleApp.Runtime;

/// <summary>What this console instance is allowed to do with the session.</summary>
public enum SessionRole
{
    /// <summary>Holds the session lock; sends prompts, approves tools, saves the session.</summary>
    Driver,

    /// <summary>Read-only: follows the driver's status file and re-reads the session file; can <c>/takeover</c>.</summary>
    Observer,
}
