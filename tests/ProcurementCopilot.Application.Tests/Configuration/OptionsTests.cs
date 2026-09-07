using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProcurementCopilot.Application.Configuration;
using Shouldly;

namespace ProcurementCopilot.Application.Tests.Configuration;

public class OptionsTests
{
    [Fact]
    public void FoundryOptions_MissingKeys_NamesKeysNeverValues()
    {
        var options = new FoundryOptions { Endpoint = "https://x.openai.azure.com", ApiKey = "secret-value-1234" };

        options.IsConfigured.ShouldBeFalse();
        options.MissingKeys().ShouldBe(["Foundry:DeploymentName"]);
        string.Join(",", options.MissingKeys()).ShouldNotContain("secret-value");
        new FoundryOptions().MissingKeys().Count.ShouldBe(3);
    }

    [Fact]
    public void FoundryOptions_JudgeDeployment_FallsBackToMainDeployment()
    {
        new FoundryOptions { DeploymentName = "gpt" }.EffectiveJudgeDeployment.ShouldBe("gpt");
        new FoundryOptions { DeploymentName = "gpt", JudgeDeploymentName = "judge" }.EffectiveJudgeDeployment.ShouldBe("judge");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("https://x.openai.azure.com", true)]
    [InlineData("ftp://x", false)]
    [InlineData("not a url", false)]
    public void FoundryOptions_HasValidEndpoint_AcceptsEmptyOrHttp(string endpoint, bool valid)
    {
        new FoundryOptions { Endpoint = endpoint }.HasValidEndpoint.ShouldBe(valid);
    }

    [Fact]
    public void SessionsOptions_ResolveDirectory_DefaultsToLocalAppData()
    {
        string resolved = new SessionsOptions().ResolveDirectory();

        resolved.ShouldEndWith(Path.Combine("ProcurementCopilot", "sessions"));
        new SessionsOptions { Directory = "custom-sessions" }.ResolveDirectory().ShouldBe(Path.GetFullPath("custom-sessions"));
    }

    [Fact]
    public void WorkspaceOptions_ResolveRoot_HandlesRelativeAndAbsolute()
    {
        string baseDir = Path.GetTempPath();

        new WorkspaceOptions { Root = "workspace" }.ResolveRoot(baseDir).ShouldBe(Path.GetFullPath(Path.Combine(baseDir, "workspace")));
        new WorkspaceOptions { Root = baseDir }.ResolveRoot("ignored").ShouldBe(Path.GetFullPath(baseDir));
        new WorkspaceOptions().EffectiveReadOnlyPaths.ShouldBe(["rfps"]);
        new WorkspaceOptions().EffectiveWritablePaths.ShouldBe(["output"]);
        new WorkspaceOptions { AllowedExtensions = [".md"] }.EffectiveAllowedExtensions.ShouldBe([".md"]);
    }

    [Fact]
    public void TimeoutOptions_ExposeTimeSpans()
    {
        new ShellOptions { TimeoutSeconds = 3 }.Timeout.ShouldBe(TimeSpan.FromSeconds(3));
        new BackgroundAgentsOptions { TimeoutSeconds = 7 }.Timeout.ShouldBe(TimeSpan.FromSeconds(7));
        new TelemetryOptions().UseOtlp.ShouldBeFalse();
        new TelemetryOptions { OtlpEndpoint = "http://localhost:4317" }.UseOtlp.ShouldBeTrue();
        new ApprovalPolicyOptions().EffectiveRequireApprovalFor.ShouldBe(ApprovalPolicyOptions.Defaults);
    }

    [Fact]
    public void AddProcurementOptions_BindsSectionsAndValidatesOnStart()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:MaxFunctionInvocationIterations"] = "9",
            ["Security:ApprovalPolicy:RequireApprovalFor:0"] = "shell",
            ["Security:Shell:AllowedCommands:0"] = "pwd",
        }).Build();
        var services = new ServiceCollection().AddProcurementOptions(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<AgentOptions>>().Value.MaxFunctionInvocationIterations.ShouldBe(9);
        provider.GetRequiredService<IOptions<SecurityOptions>>().Value.ApprovalPolicy.RequireApprovalFor.ShouldBe(["shell"]);
        provider.GetRequiredService<IOptions<SecurityOptions>>().Value.Shell.EffectiveAllowedCommands.ShouldBe(["pwd"]);
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void AddProcurementOptions_InvalidValues_FailStartupValidation()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:MaxLoopIterations"] = "0",
            ["Foundry:Endpoint"] = "not-a-url",
        }).Build();
        using ServiceProvider provider = new ServiceCollection().AddProcurementOptions(configuration).BuildServiceProvider();

        AggregateException ex = Should.Throw<AggregateException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        ex.InnerExceptions.ShouldAllBe(e => e is OptionsValidationException);
        ex.Message.ShouldContain("Foundry:Endpoint");
        ex.Message.ShouldContain("MaxLoopIterations");
    }
}
