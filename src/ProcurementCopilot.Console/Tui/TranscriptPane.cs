#pragma warning disable CS0618 // Terminal.Gui 2.4.17 flags TextView and the static Application facade obsolete ahead of v3; both are the stable, documented API of this release.
using System.Text;
using Terminal.Gui.Views;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>The scrolling transcript. All members must be called on the Terminal.Gui main thread.</summary>
public sealed class TranscriptPane
{
    private const int MaxChars = 300_000;
    private const int KeepChars = 240_000;
    private readonly TextView _view;
    private readonly StringBuilder _text = new();

    /// <summary>Wraps the text view that shows the transcript.</summary>
    public TranscriptPane(TextView view) => _view = view;

    /// <summary>True when the last character is a line break (or the pane is empty).</summary>
    public bool AtLineStart => _text.Length == 0 || _text[^1] == '\n';

    /// <summary>Appends raw text and scrolls to the end.</summary>
    public void Append(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        _text.Append(text.Replace("\r\n", "\n", StringComparison.Ordinal));
        if (_text.Length > MaxChars)
        {
            _text.Remove(0, _text.Length - KeepChars);
        }

        _view.Text = _text.ToString();
        _view.MoveEnd();
    }

    /// <summary>Appends a line, starting a new one first when needed.</summary>
    public void AppendLine(string line = "")
    {
        Append((AtLineStart ? string.Empty : "\n") + line + "\n");
    }

    /// <summary>Clears the transcript.</summary>
    public void Clear()
    {
        _text.Clear();
        _view.Text = string.Empty;
    }
}
