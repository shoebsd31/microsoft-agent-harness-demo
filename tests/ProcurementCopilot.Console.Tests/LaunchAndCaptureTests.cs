using ProcurementCopilot.ConsoleApp.Hosting;
using ProcurementCopilot.ConsoleApp.Tui;
using Shouldly;
using Spectre.Console;

namespace ProcurementCopilot.Console.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void Parse_RecognisesAllFlagsAndForwardsTheRest()
    {
        var session = Guid.NewGuid();
        var attach = Guid.NewGuid();

        LaunchOptions options = LaunchOptions.Parse(["--fake", "--session", session.ToString(), "--attach", attach.ToString("N"), "--classic", "--Data:Provider=Json"]);

        options.Fake.ShouldBeTrue();
        options.ResumeSessionId.ShouldBe(session);
        options.AttachSessionId.ShouldBe(attach);
        options.Ui.ShouldBe(UiMode.Classic);
        options.UseTui.ShouldBeFalse();
        options.HostArgs.ShouldBe(["--Data:Provider=Json"]);
    }

    [Fact]
    public void Parse_TuiFlagForcesTheTui()
    {
        LaunchOptions.Parse(["--tui"]).UseTui.ShouldBeTrue();
    }

    [Fact]
    public void Parse_InvalidSessionIdIsForwardedUntouched()
    {
        LaunchOptions options = LaunchOptions.Parse(["--session", "not-a-guid"]);

        options.ResumeSessionId.ShouldBeNull();
        options.HostArgs.ShouldBe(["--session", "not-a-guid"]);
    }
}

public class SpectreCaptureTests
{
    [Fact]
    public void Drain_ReturnsPlainTextWrittenThroughSpectreAndRestoresTheConsole()
    {
        IAnsiConsole before = AnsiConsole.Console;
        string text;
        using (var capture = new SpectreCapture(100))
        {
            AnsiConsole.MarkupLine("[red]hello[/] [bold]world[/]");
            AnsiConsole.Write(new Table().AddColumn("A").AddRow("1"));
            text = capture.Drain();
            capture.Drain().ShouldBe(string.Empty);
        }

        text.ShouldContain("hello world");
        text.ShouldContain("1");
        text.ShouldNotContain("[");
        AnsiConsole.Console.ShouldBeSameAs(before);
    }
}
