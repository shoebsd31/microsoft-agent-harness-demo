#pragma warning disable CS0618 // Terminal.Gui 2.4.17 flags TextView and the static Application facade obsolete ahead of v3; both are the stable, documented API of this release.
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Builds the screen: transcript and prompt on the left, todo / tasks / context / traces panels on the right, status bar below.</summary>
public sealed class TuiLayout
{
    /// <summary>Creates all views.</summary>
    public TuiLayout(IEnumerable<Shortcut> shortcuts)
    {
        Root = new Window { Title = " Procurement Copilot ", BorderStyle = LineStyle.Rounded };

        TranscriptFrame = new FrameView { Title = " Transcript ", X = 0, Y = 0, Width = Dim.Percent(64), Height = Dim.Fill(4) };
        Transcript = new TextView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true };
        TranscriptFrame.Add(Transcript);

        InputFrame = new FrameView { Title = " Prompt ", X = 0, Y = Pos.Bottom(TranscriptFrame), Width = Dim.Percent(64), Height = 3 };
        Input = new TextField { X = 0, Y = 0, Width = Dim.Fill(), Height = 1 };
        InputFrame.Add(Input);

        TodosFrame = Panel(" Todos ", Pos.Right(TranscriptFrame), 0, Dim.Percent(30), wrap: true, out TextView todos);
        Todos = todos;
        TasksFrame = Panel(" Background tasks ", Pos.Right(TranscriptFrame), Pos.Bottom(TodosFrame), Dim.Percent(22), wrap: true, out TextView tasks);
        Tasks = tasks;
        ContextFrame = Panel(" Context ", Pos.Right(TranscriptFrame), Pos.Bottom(TasksFrame), Dim.Percent(26), wrap: false, out TextView context);
        Context = context;
        TracesFrame = Panel(" Traces ", Pos.Right(TranscriptFrame), Pos.Bottom(ContextFrame), Dim.Fill(1), wrap: false, out TextView traces);
        Traces = traces;

        Status = new StatusBar { X = 0, Y = Pos.AnchorEnd(1), Width = Dim.Fill(), Height = 1 };
        foreach (Shortcut shortcut in shortcuts)
        {
            Status.Add(shortcut);
        }

        Root.Add(TranscriptFrame, InputFrame, TodosFrame, TasksFrame, ContextFrame, TracesFrame, Status);
    }

    /// <summary>The top-level window.</summary>
    public Window Root { get; }

    /// <summary>Transcript frame.</summary>
    public FrameView TranscriptFrame { get; }

    /// <summary>Transcript text.</summary>
    public TextView Transcript { get; }

    /// <summary>Prompt frame (its title shows mode, role and activity).</summary>
    public FrameView InputFrame { get; }

    /// <summary>Prompt input.</summary>
    public TextField Input { get; }

    /// <summary>Todos frame.</summary>
    public FrameView TodosFrame { get; }

    /// <summary>Todos text.</summary>
    public TextView Todos { get; }

    /// <summary>Tasks frame.</summary>
    public FrameView TasksFrame { get; }

    /// <summary>Tasks text.</summary>
    public TextView Tasks { get; }

    /// <summary>Context frame.</summary>
    public FrameView ContextFrame { get; }

    /// <summary>Context text.</summary>
    public TextView Context { get; }

    /// <summary>Traces frame.</summary>
    public FrameView TracesFrame { get; }

    /// <summary>Traces text.</summary>
    public TextView Traces { get; }

    /// <summary>Status bar.</summary>
    public StatusBar Status { get; }

    /// <summary>Sets a panel's text only when it changed (avoids redraw churn).</summary>
    public static void SetText(TextView view, string text)
    {
        if (!string.Equals(view.Text, text, StringComparison.Ordinal))
        {
            view.Text = text;
        }
    }

    private static FrameView Panel(string title, Pos x, Pos y, Dim height, bool wrap, out TextView text)
    {
        var frame = new FrameView { Title = title, X = x, Y = y, Width = Dim.Fill(), Height = height };
        text = new TextView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = wrap };
        frame.Add(text);
        return frame;
    }
}
