# Common Tasks

## Update ScriptDom Parser
When adding support for a new SQL Server version:
1. Update compat level mapping in `ScriptDomTokenizer.cs`
2. Add new parser version case in `GetParser()` method
3. Update documentation with supported versions

## Modify CLI Commands
Commands are dispatched in `CliApp.RunAsync()`. To add a command:
1. Add command definition in `CliParser.cs`
2. Add handler method in `CliApp.cs`
3. Update help text and documentation

## Change Output Format
Output formatting is in `CommandExecutor.cs`:
- Text output: `file:line:col: Severity: Message (rule-id)` format (ESLint/GCC style)
- JSON output: Uses System.Text.Json with `JsonDefaults.cs` options

## Debug Rule Execution
Add logging or breakpoints in:
- `TsqlRefineEngine.Run()`: Main execution loop
- `ScriptDomTokenizer.Analyze()`: Parsing stage
- Individual rule's `Analyze()` method

## Initialize New Project
Run the `init` command to create default configuration:
```powershell
dotnet run --project src/TsqlRefine.Cli -c Release -- init
```
This creates:
- `tsqlrefine.json`: Default configuration file
- `tsqlrefine.ignore`: Default ignore patterns file
