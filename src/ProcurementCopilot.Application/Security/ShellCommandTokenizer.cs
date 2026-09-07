namespace ProcurementCopilot.Application.Security;

/// <summary>A parsed pipeline segment: a command name and its arguments.</summary>
/// <param name="Command">The command name (first token).</param>
/// <param name="Arguments">The remaining tokens with quotes removed.</param>
public sealed record ShellSegment(string Command, IReadOnlyList<string> Arguments);

/// <summary>
/// Minimal, shell-agnostic tokenizer. Splits a command line on single <c>|</c> into segments and each segment on
/// whitespace, honouring single and double quotes. Any metacharacter that would change control flow is rejected
/// by <see cref="ShellCommandPolicy"/> before tokenisation, so this tokenizer never has to interpret them.
/// </summary>
public static class ShellCommandTokenizer
{
    /// <summary>Splits the command line into pipeline segments.</summary>
    public static IReadOnlyList<ShellSegment> Tokenize(string commandLine)
    {
        var segments = new List<ShellSegment>();
        foreach (string raw in SplitPipeline(commandLine))
        {
            List<string> tokens = SplitTokens(raw);
            if (tokens.Count == 0)
            {
                segments.Add(new ShellSegment(string.Empty, []));
                continue;
            }

            segments.Add(new ShellSegment(tokens[0], tokens.Skip(1).ToList()));
        }

        return segments;
    }

    private static IEnumerable<string> SplitPipeline(string commandLine)
    {
        var current = new System.Text.StringBuilder();
        char? quote = null;
        foreach (char c in commandLine)
        {
            if (quote is null && c is '\'' or '"')
            {
                quote = c;
            }
            else if (quote == c)
            {
                quote = null;
            }

            if (c == '|' && quote is null)
            {
                yield return current.ToString();
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        yield return current.ToString();
    }

    private static List<string> SplitTokens(string segment)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        char? quote = null;
        bool inToken = false;
        foreach (char c in segment)
        {
            if (quote is not null)
            {
                if (c == quote)
                {
                    quote = null;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                inToken = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (inToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    inToken = false;
                }

                continue;
            }

            current.Append(c);
            inToken = true;
        }

        if (inToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
