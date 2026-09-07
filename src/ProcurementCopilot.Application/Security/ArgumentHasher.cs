using System.Security.Cryptography;
using System.Text;

namespace ProcurementCopilot.Application.Security;

/// <summary>Produces a stable SHA-256 of tool arguments for the audit trail, so arguments are never stored verbatim.</summary>
public static class ArgumentHasher
{
    /// <summary>Hashes the arguments joined with a unit separator.</summary>
    public static string Hash(params IEnumerable<string?> arguments)
    {
        string canonical = string.Join('', arguments.Select(a => a ?? string.Empty));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
