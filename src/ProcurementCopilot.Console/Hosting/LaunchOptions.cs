namespace ProcurementCopilot.ConsoleApp.Hosting;

/// <summary>Command-line flags understood by the console.</summary>
public sealed class LaunchOptions
{
    /// <summary>Validate DI and configuration, then exit without calling a model.</summary>
    public bool SelfCheck { get; init; }

    /// <summary>Replay the canned demo with the scripted chat client; no credentials needed.</summary>
    public bool Fake { get; init; }

    /// <summary>Session id to resume on start.</summary>
    public Guid? ResumeSessionId { get; init; }

    /// <summary>Arguments forwarded to the host (everything not consumed here).</summary>
    public string[] HostArgs { get; init; } = [];

    /// <summary>Parses the process arguments.</summary>
    public static LaunchOptions Parse(string[] args)
    {
        bool selfCheck = false;
        bool fake = false;
        Guid? resume = null;
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
                case "--session" when i + 1 < args.Length && Guid.TryParse(args[i + 1], out Guid id):
                    resume = id;
                    i++;
                    break;
                default:
                    rest.Add(args[i]);
                    break;
            }
        }

        return new LaunchOptions { SelfCheck = selfCheck, Fake = fake, ResumeSessionId = resume, HostArgs = rest.ToArray() };
    }
}
