using System.Text;
using Spectre.Console;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>
/// While the TUI owns the terminal, everything the slash commands write through Spectre.Console is captured here
/// (plain text, fixed width) and drained into the transcript pane, so the existing commands need no changes.
/// </summary>
public sealed class SpectreCapture : IAnsiConsoleOutput, IDisposable
{
    private readonly StringWriter _buffer = new();
    private readonly TextWriter _writer;
    private readonly IAnsiConsole _previous;

    /// <summary>Installs the capture as the global Spectre console.</summary>
    public SpectreCapture(int width)
    {
        Width = Math.Max(40, width);
        _writer = TextWriter.Synchronized(_buffer);
        _previous = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = this,
        });
    }

    /// <inheritdoc />
    public TextWriter Writer => _writer;

    /// <inheritdoc />
    public bool IsTerminal => false;

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height => 50;

    /// <inheritdoc />
    public void SetEncoding(Encoding encoding)
    {
        // The transcript is a .NET string; encoding is irrelevant.
    }

    /// <summary>Returns and clears the captured text.</summary>
    public string Drain()
    {
        lock (_writer)
        {
            _writer.Flush();
            StringBuilder sb = _buffer.GetStringBuilder();
            if (sb.Length == 0)
            {
                return string.Empty;
            }

            string text = sb.ToString();
            sb.Clear();
            return text;
        }
    }

    /// <summary>Restores the previous global console.</summary>
    public void Dispose()
    {
        AnsiConsole.Console = _previous;
        _buffer.Dispose();
    }
}
