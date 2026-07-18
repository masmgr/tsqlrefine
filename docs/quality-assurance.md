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
- `mutation.yml` runs Stryker.NET against `TsqlRefine.Rules` weekly. Its reports are
  artifacts for survivor review; no absolute mutation score is enforced.
- `dogfood.yml` runs the strict preset over repository SQL monthly and uploads the
  diagnostics for human triage. Private SQL estates can reuse this workflow in an
  access-controlled repository; do not copy customer or sensitive SQL into the
  public corpus.
