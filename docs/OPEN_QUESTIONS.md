# Open questions

Questions raised while implementing `docs/requirements.md`, with the conservative interpretation that was chosen.

1. **Shell allowlist on Windows.** The allowlist mixes POSIX (`ls`, `cat`, `head`, `wc`, `grep`) and Windows (`dir`, `type`, `findstr`) commands.
   *Chosen:* the policy is shell-agnostic and evaluated on the parsed command line; at runtime Git Bash is preferred on Windows when present
   (so the POSIX commands work), otherwise the default PowerShell/cmd is used. The tool description tells the model which shell family is active.
2. **Where do bid scores "persist to session state"?** *Chosen:* in `AgentSession.StateBag` under a dedicated provider key, so the loop
   evaluator, the compaction strategy and the console all read the same state, and it survives `/session resume`.
3. **Should plan mode hide side-effecting tools or block them?** *Chosen:* block (spec §4.5) via a guarding `DelegatingAIFunction`,
   and additionally tell the model in the instructions that those tools only work in execute mode.
4. **Web-search capability detection.** There is no runtime capability flag on `IChatClient`. *Chosen:* the client is treated as supporting
   hosted web search when it exposes an OpenAI `ResponsesClient` via `GetService`, or implements the local marker `ISupportsHostedWebSearch`
   (used by the fake). Otherwise a warning is logged and `DisableWebSearch = true`.
5. **Approval wrapping order vs. the mode guard.** The framework detects approval-required tools by the outer `ApprovalRequiredAIFunction`
   type. *Chosen:* mode guard inside, approval wrapper outside, so approval detection is never lost; in plan mode the model is told not to
   call side-effecting tools at all, and if it does the call is blocked after approval without writing anything.
