#pragma warning disable CS0618 // Terminal.Gui 2.4.17 flags TextView and the static Application facade obsolete ahead of v3; both are the stable, documented API of this release.
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TgApp = Terminal.Gui.App.Application;

namespace ProcurementCopilot.ConsoleApp.Tui;

/// <summary>Modal approval dialog: Once / Always (this session) / Deny. Returns the same y/a/n choice the classic prompt uses.</summary>
public static class ApprovalDialog
{
    /// <summary>Shows the dialog on the UI thread and completes with <c>y</c>, <c>a</c> or <c>n</c>.</summary>
    /// <param name="beforeShow">Runs on the UI thread first (used to flush pending transcript text).</param>
    /// <param name="tool">Tool name.</param>
    /// <param name="arguments">Pretty-printed arguments.</param>
    public static Task<string> AskAsync(Action beforeShow, string tool, string arguments)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        TgApp.Invoke(() =>
        {
            try
            {
                beforeShow();
                completion.TrySetResult(Show(tool, arguments));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
            {
                completion.TrySetResult("n");
            }
        });
        return completion.Task;
    }

    private static string Show(string tool, string arguments)
    {
        string choice = "n";
        var dialog = new Dialog { Title = " 🔐 Approval required ", Width = Dim.Percent(72), Height = Dim.Percent(70) };
        var header = new Label { X = 1, Y = 0, Text = $"The agent wants to call {tool}. Arguments:" };
        var body = new TextView { X = 1, Y = 2, Width = Dim.Fill(1), Height = Dim.Fill(3), ReadOnly = true, WordWrap = true, Text = arguments };
        var hint = new Label { X = 1, Y = Pos.AnchorEnd(3), Text = "Once = this call only · Always = no more prompts for this tool this session · Deny = the agent is told no" };
        dialog.Add(header, body, hint);

        Button Make(string text, string value, Pos x)
        {
            var button = new Button { Text = text, X = x, Y = Pos.AnchorEnd(1) };
            button.Accepting += (_, e) =>
            {
                choice = value;
                e.Handled = true;
                TgApp.RequestStop(dialog);
            };
            return button;
        }

        Button once = Make("_Once", "y", 1);
        once.IsDefault = true;
        Button always = Make("_Always this session", "a", Pos.Right(once) + 2);
        Button deny = Make("_Deny", "n", Pos.Right(always) + 2);
        dialog.Add(once, always, deny);
        once.SetFocus();
        TgApp.Run(dialog);
        dialog.Dispose();
        return choice;
    }
}
