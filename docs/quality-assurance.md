# Quality assurance workflows

The repository layers contract and corpus checks on top of the unit test suite.

## Corpus checks

SQL fixtures live under `tests/corpus/sql` and are registered in
`tests/corpus/manifest.json`. The manifest is mandatory and records provenance,
license, immutable source revision, modification status, checksum, and minimum SQL
Server compatibility level. See `tests/corpus/README.md` before importing a
third-party file.

The `TsqlRefine.Qa.Tests` project verifies:

- lint completes without parser or rule crashes and matches the diagnostic snapshot;
- every built-in rule has documentation, SQL examples, and a preset assignment;
- formatting is idempotent and preserves significant tokens and comments;
- fixes remain parseable and converge within five passes;
- corpus files behave across compatibility levels 110 through 160;
- lint/fix JSON models validate against the committed schemas;
- CLI exit codes remain stable.
- every public concrete rule is registered exactly once, rule IDs are unique
  kebab-case values, and fixable rules provide a rule-specific fix implementation;
- every rule, run directly against the samples and corpus (with schema and relation
  fixtures for dependent rules), emits diagnostics whose code, category, and
  fixability agree with the rule metadata, whose ranges stay inside the analyzed
  document, and whose fixes have non-overlapping in-document edits; schema-dependent
  rules must emit at least one validated diagnostic (`RuleDiagnosticIntegrityTests`);
- every rule tolerates degenerate inputs — empty, comment-only, unparseable SQL,
  and a missing AST fragment — without throwing (`RuleRobustnessTests`).

Property tests share a grammar-based SQL generator across Core, Rules, and
Formatting. It produces parseable SELECT, UPDATE, and DELETE scripts so that
crash-resistance, formatter idempotency, and autofix syntax/range/convergence
properties exercise rule logic rather than mostly parser-error paths.

## Diagnostic baselines and SARIF output

`CliBaselineAndSarifTests` and `OutputContractTests` cover `baseline create`/`baseline trim`
round-tripping, `--baseline`/`--show-suppressed` suppression accounting, and `--output sarif`
against a SARIF 2.1.0 shape. Baseline and SARIF payloads are validated against
[`schemas/baseline.schema.json`](../schemas/baseline.schema.json) and the diagnostic-range
conventions documented in [CLI Specification](cli.md#4-json-output-specification-diagnostics).
See [CI Integration](ci-integration.md#sarif-output-and-code-scanning) for wiring these into a
pipeline.

## Static analysis and code metrics

`CodeMetricsConfig.txt` gates new code at cyclomatic complexity 15,
maintainability index 10, and class coupling 40. Existing hotspots carry a
declaration-level `SuppressMessage` with a baseline-debt justification; do not
add project-wide suppressions for new violations.

SonarAnalyzer.CSharp and Meziantou.Analyzer are installed in observation mode.
Reviewed rules can be promoted from `suggestion` in `.editorconfig` in small
batches. `BannedSymbols.txt` rejects ambient local time and culture-sensitive
parameterless string casing APIs.

To intentionally accept a diagnostic change:

```powershell
$env:UPDATE_CORPUS_SNAPSHOTS = '1'
dotnet test tests/TsqlRefine.Qa.Tests -c Release
Remove-Item Env:UPDATE_CORPUS_SNAPSHOTS
git diff -- tests/corpus/snapshots/diagnostics.json
```

Review additions and removals as behavior changes; a snapshot difference is not by
itself proof of a false positive.

## Coverage

CI merges Cobertura results and compares line and branch coverage with
`scripts/coverage-baseline.json`. Update the baseline only after reviewing the
coverage report and tests added for the changed behavior.

## Scheduled checks

- `performance.yml` runs the all-rule corpus benchmark on pushes to `main` and
  alerts when the cached result regresses by more than 50 percent.
- `mutation.yml` runs Stryker.NET against Rules, Core, and Formatting weekly and
  runs changed-code mutation analysis for pull requests. Core is gated at 51
  (56.71% measured baseline) and Formatting at 77 (82.31% measured baseline).
  Rules remains report-only until the corrected full-run configuration produces
  its first valid score; its previous repository-relative mutate glob excluded
  all runnable mutants.
- `dogfood.yml` runs the strict preset over repository SQL monthly and uploads the
  diagnostics for human triage. Private SQL estates can reuse this workflow in an
  access-controlled repository; do not copy customer or sensitive SQL into the
  public corpus.
