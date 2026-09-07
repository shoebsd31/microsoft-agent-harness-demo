# Phase 0 — Installed API notes (captured by reflection, 2026-09-06)

Versions resolved from NuGet at build time:

| Package | Version | Notes |
|---|---|---|
| Microsoft.Agents.AI / .Abstractions / .Harness / .OpenAI | 1.20.0 | stable; all on the same version |
| Microsoft.Agents.AI.Tools.Shell | 1.20.0-preview.260831.1 | prerelease only; major.minor matches Harness |
| Microsoft.Extensions.AI / .Abstractions / .OpenAI | 10.9.0 | minimum pinned by the 1.20.0 packages |
| OpenAI | resolved transitively (>= 2.10.0) | used for `OpenAIClient` + `ResponsesClient` |

## `HarnessAgentOptions` (Microsoft.Agents.AI.Harness 1.20.0)

Verified members used by `HarnessAgentFactory`:

`Id`, `Name`, `Description`, `MaxContextWindowTokens`, `MaxOutputTokens`, `CompactionStrategy`, `DisableCompaction`,
`ChatOptions`, `HarnessInstructions`, `ChatHistoryProvider`, `AIContextProviders`, `LoopEvaluators`, `LoopAgentOptions`,
`MaximumIterationsPerRequest`, `DisableToolAutoApproval`, `ToolApprovalAgentOptions`,
`DisableApprovalNotRequiredFunctionBypassing`, `DisableApprovalResponseBinding`, `DisableFileMemory`, `FileMemoryStore`,
`FileAccessStore`, `FileAccessProviderOptions`, `DisableWebSearch`, `DisableTodoProvider`, `DisableAgentModeProvider`,
`AgentModeProviderOptions`, `DisableAgentSkillsProvider`, `AgentSkillsSource`, `DisableOpenTelemetry`,
`OpenTelemetrySourceName`, `BackgroundAgents`, `BackgroundAgentsProviderOptions`.

* The per-request function-iteration limit is `MaximumIterationsPerRequest` (there is no `MaxFunctionInvocationIterations`).
* `HarnessAgent.DefaultInstructions` is a public static string; `HarnessInstructions` is prepended to `ChatOptions.Instructions`.
* `ToolApprovalAgentOptions.AutoApprovalRules : IEnumerable<Func<ToolAutoApprovalRuleContext, ValueTask<bool>>>`.
* `AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule` and `FileAccessProvider.ReadOnlyToolsAutoApprovalRule` are static rule delegates.
* `AgentFileSkillsSource(string skillPath, AgentFileSkillScriptRunner? scriptRunner = null, AgentFileSkillsSourceOptions? options = null, ILoggerFactory? lf = null)`.
* `FileAccessProviderOptions { Instructions, DisableWriteTools, DisableReadOnlyToolApproval, DisableWriteToolApproval }`.
* `BackgroundAgentsProviderOptions { Instructions, AgentListBuilder, WaitTimeout }`; provider tools are `background_agents_*`.
* `LoopEvaluator.EvaluateAsync(LoopContext, ct) -> LoopEvaluation` (`Stop()`, `Continue(feedback)`, `ContinueWithMessages`).
  `LoopContext { Agent, Session, InitialMessages, Iteration, LastResponse, Feedback }`. `LoopAgentOptions.MaxIterations` is `int?`.
* `AIJudgeLoopEvaluator(IChatClient judgeClient, AIJudgeLoopEvaluatorOptions { Instructions, Criteria, FeedbackMessageTemplate })`.
* `CompactionStrategy` (namespace `Microsoft.Agents.AI.Compaction`): ctor `(CompactionTrigger trigger, CompactionTrigger? target)`,
  override `CompactCoreAsync(CompactionMessageIndex, ILogger?, ct) -> ValueTask<bool>`. Index exposes `Groups`, `IncludedTokenCount`,
  `InsertGroup(index, CompactionGroupKind, messages)`. Groups have `Kind`, `Messages`, `TokenCount`, `IsExcluded`, `ExcludeReason`.
  Summary groups are tagged with `CompactionMessageGroup.SummaryPropertyKey`.
* `ChatHistoryProvider` overridables: `ProvideChatHistoryAsync`, `StoreChatHistoryAsync`, `InvokingCoreAsync`, `InvokedCoreAsync`;
  `InMemoryChatHistoryProvider(InMemoryChatHistoryProviderOptions)` with `GetMessages/SetMessages(session)`.
* `AIAgent.CurrentRunContext` (static) exposes `Agent`, `Session`, `RequestMessages`, `RunOptions` for the current run.
* `AgentModeProvider.GetModeAsync/SetModeAsync(session, mode)`; `TodoProvider.GetAllTodosAsync/GetRemainingTodosAsync(session)`.
  Both are resolved with `agent.GetService<T>()`.
* `AgentFileStore` abstract: `WriteAsync`, `ReadAsync`, `DeleteAsync`, `ListChildrenAsync`, `FileExistsAsync`, `SearchAsync`, `CreateDirectoryAsync`.
  `FileSystemAgentFileStore(string rootDirectory)`.
* Approval content types live in Microsoft.Extensions.AI: `ToolApprovalRequestContent { ToolCall, CreateResponse(bool, reason) }`,
  `ToolApprovalResponseContent`, plus `CreateAlwaysApproveToolResponse(...)` extension in Microsoft.Agents.AI.
* `HostedWebSearchTool` is in Microsoft.Extensions.AI; the harness adds it unless `DisableWebSearch`.
* Shell: `LocalShellExecutor(LocalShellExecutorOptions { Mode, Shell, ShellArgv, WorkingDirectory, ConfineWorkingDirectory,
  Environment, CleanEnvironment, Policy, Timeout, MaxOutputBytes, AcknowledgeUnsafe })`, `RunAsync(command) -> ShellResult
  { Stdout, Stderr, ExitCode, Duration, Truncated, TimedOut, FormatForModel() }`, `AsAIFunction(name = "run_shell", requireApproval = true)`.
  `ShellPolicy(denyList, allowList, custom)` is a regex pre-filter. Verified on Windows that `ShellArgv = [bash.exe]` (Git Bash) works.
* OpenAI: `new OpenAIClient(ApiKeyCredential, OpenAIClientOptions { Endpoint })`, `GetResponsesClient()`, and the adapters
  `ResponsesClient.AsIChatClient(model)` (Microsoft.Extensions.AI.OpenAI) or
  `AsIChatClientWithStoredOutputDisabled(model)` (Microsoft.Agents.AI.OpenAI).
