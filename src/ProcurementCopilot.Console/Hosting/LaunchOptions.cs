namespace ProcurementCopilot.ConsoleApp.Hosting;

/// <summary>Which front-end to run.</summary>
public enum UiMode
{
    /// <summary>TUI when attached to a real terminal, classic line mode otherwise.</summary>
    Auto,

    /// <summary>Full-screen Terminal.Gui interface.</summary>
    Tui,

    /// <summary>Line-oriented Spectre.Console interface (works with redirected input).</summary>
    Classic,
}

/// <summary>Command-line flags understood by the console.</summary>
public sealed class LaunchOptions
{
    /// <summary>Validate DI and configuration, then exit without calling a model.</summary>
    public bool SelfCheck { get; init; }

    /// <summary>Replay the canned demo with the scripted chat client; no credentials needed.</summary>
    public bool Fake { get; init; }

    /// <summary>Session id to resume (and drive) on start.</summary>
    public Guid? ResumeSessionId { get; init; }

    /// <summary>Session id to attach to as a read-only observer.</summary>
    public Guid? AttachSessionId { get; init; }

    /// <summary>Requested front-end.</summary>
    public UiMode Ui { get; init; } = UiMode.Auto;

    /// <summary>Arguments forwarded to the host (everything not consumed here).</summary>
    public string[] HostArgs { get; init; } = [];

    /// <summary>Resolves <see cref="Ui"/>: the TUI needs a real terminal on both ends.</summary>
    public bool UseTui => Ui switch
    {
        UiMode.Tui => true,
        UiMode.Classic => false,
        _ => !Console.IsInputRedirected && !Console.IsOutputRedirected,
    };

    /// <summary>Parses the process arguments.</summary>
    public static LaunchOptions Parse(string[] args)
    {
        bool selfCheck = false;
        bool fake = false;
        Guid? resume = null;
        Guid? attach = null;
        UiMode ui = UiMode.Auto;
        var rest = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--self-check":
                    selfCheck = true;
                    break;
                case "--fake":
                    fake = true;
                    break;
                case "--classic":
                    ui = UiMode.Classic;
                    break;
                case "--tui":
                    ui = UiMode.Tui;
                    break;
                case "--session" when i + 1 < args.Length && Guid.TryParse(args[i + 1], out Guid id):
                    resume = id;
                    i++;
                    break;
                case "--attach" when i + 1 < args.Length && Guid.TryParse(args[i + 1], out Guid observed):
                    attach = observed;
                    i++;
                    break;
                default:
                    rest.Add(args[i]);
                    break;
            }
        }

        return new LaunchOptions { SelfCheck = selfCheck, Fake = fake, ResumeSessionId = resume, AttachSessionId = attach, Ui = ui, HostArgs = rest.ToArray() };
    }
}
