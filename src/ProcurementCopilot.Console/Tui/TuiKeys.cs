using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Status-bar shortcuts and global key handling for the TUI.</summary>
public static class TuiKeys
{
    /// <summary>Builds the status bar shortcuts.</summary>
    public static IEnumerable<Shortcut> Shortcuts(TuiShell shell) =>
    [
        new Shortcut(Key.F1, "Help", () => shell.Submit("/help"), string.Empty),
        new Shortcut(Key.F2, "Mode", shell.ToggleMode, string.Empty),
        new Shortcut(Key.F3, "Todos", () => shell.Submit("/todos"), string.Empty),
        new Shortcut(Key.F4, "Traces", () => shell.Submit("/traces"), string.Empty),
        new Shortcut(Key.F5, "Take over / Detach", shell.TakeoverOrDetach, string.Empty),
        new Shortcut(Key.F6, "Sessions", () => shell.Submit("/session list"), string.Empty),
        new Shortcut(Key.F7, "Who drives", () => shell.Submit("/whois"), string.Empty),
        new Shortcut(Key.Esc, "Cancel run", shell.CancelRun, string.Empty),
        new Shortcut(Key.Q.WithCtrl, "Quit", shell.Quit, string.Empty),
    ];

    /// <summary>Handles keys that must work wherever the focus is.</summary>
    public static void Handle(TuiShell shell, Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.Esc:
                shell.CancelRun();
                key.Handled = true;
                break;
            case KeyCode.Q | KeyCode.CtrlMask:
                shell.Quit();
                key.Handled = true;
                break;
            case KeyCode.F1:
                shell.Submit("/help");
                key.Handled = true;
                break;
            case KeyCode.F2:
                shell.ToggleMode();
                key.Handled = true;
                break;
            case KeyCode.F3:
                shell.Submit("/todos");
                key.Handled = true;
                break;
            case KeyCode.F4:
                shell.Submit("/traces");
                key.Handled = true;
                break;
            case KeyCode.F5:
                shell.TakeoverOrDetach();
                key.Handled = true;
                break;
            case KeyCode.F6:
                shell.Submit("/session list");
                key.Handled = true;
                break;
            case KeyCode.F7:
                shell.Submit("/whois");
                key.Handled = true;
                break;
            case KeyCode.CursorUp:
                shell.History(-1);
                break;
            case KeyCode.CursorDown:
                shell.History(1);
                break;
            default:
                break;
        }
    }
}

/// <summary>Prompt history for Up/Down navigation.</summary>
public sealed class PromptHistory
{
    private readonly List<string> _entries = [];
    private int _index;

    /// <summary>Records a submitted line and resets the cursor to "after the last entry".</summary>
    public void Add(string text)
    {
        _entries.Add(text);
        _index = _entries.Count;
    }

    /// <summary>Moves the cursor; returns the entry to show (empty string past the end) or <see langword="null"/> when there is no history.</summary>
    public string? Move(int delta)
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        _index = Math.Clamp(_index + delta, 0, _entries.Count);
        return _index == _entries.Count ? string.Empty : _entries[_index];
    }
}
