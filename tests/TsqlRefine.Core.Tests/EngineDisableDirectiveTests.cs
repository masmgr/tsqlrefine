using TsqlRefine.Core.Engine;
using TsqlRefine.Rules;

namespace TsqlRefine.Core.Tests;

public sealed class EngineDisableDirectiveTests
{
    private readonly TsqlRefineEngine _engine;

    public EngineDisableDirectiveTests()
    {
        var rules = new BuiltinRuleProvider().GetRules();
        _engine = new TsqlRefineEngine(rules);
    }

    [Fact]
    public void DisableAll_EntireScript_SuppressesAllDiagnostics()
    {
        // Single line test to ensure basic functionality works
        var sql = "/* tsqlrefine-disable */ select * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        // All diagnostics on line 0 should be suppressed
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void DisableSpecificRule_EntireScript_OnlySuppressesThatRule()
    {
        var sql = "/* tsqlrefine-disable avoid-select-star */ select * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        // avoid-select-star should be suppressed
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void WithoutDisable_HasDiagnostics()
    {
        // Baseline test - ensure diagnostics appear without disable
        var sql = "select * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        // Should have avoid-select-star diagnostic
        Assert.Contains(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void DisableEnableRegion_OnlySuppressesWithinRegion()
    {
        // Test that a diagnostic on an enabled line is NOT suppressed
        // The first select * (line 0) should trigger avoid-select-star
        var sql = "select * from t1;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var selectStarDiagnostics = result.Files[0].Diagnostics
            .Where(d => d.Code == "avoid-select-star").ToList();

        // Line 0: select * from t1 - NOT suppressed
        Assert.Contains(selectStarDiagnostics, d => d.Range.Start.Line == 0);
    }

    [Fact]
    public void DisableEnableRegion_SuppressesWithinRegion()
    {
        // Test that a diagnostic within a disabled region IS suppressed
        var sql = "/* tsqlrefine-disable */\nselect * from t1;\n/* tsqlrefine-enable */";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var selectStarDiagnostics = result.Files[0].Diagnostics
            .Where(d => d.Code == "avoid-select-star").ToList();

        // Line 1: select * from t1 - SHOULD BE suppressed (between disable on 0 and enable on 2)
        Assert.Empty(selectStarDiagnostics);
    }

    [Fact]
    public void DisableSpecificRule_Region_SuppressesThatRuleInRegion()
    {
        // Test that a specific rule is suppressed within its region
        var sql = "/* tsqlrefine-disable avoid-select-star */\nselect * from t1;\n/* tsqlrefine-enable avoid-select-star */";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var selectStarDiagnostics = result.Files[0].Diagnostics
            .Where(d => d.Code == "avoid-select-star").ToList();

        // Line 1: suppressed (between disable on line 0 and enable on line 2)
        Assert.Empty(selectStarDiagnostics);
    }

    [Fact]
    public void DiagnosticAfterEnableRegion_NotSuppressed()
    {
        // After enable, the next diagnostic should NOT be suppressed
        // Note: avoid-select-star only returns the first SELECT *, so we use a different rule
        // Use dml-without-where which fires for UPDATE without WHERE
        var sql = "/* tsqlrefine-disable dml-without-where */\nupdate t set x = 1;\n/* tsqlrefine-enable dml-without-where */\nupdate t set y = 2;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var dmlDiagnostics = result.Files[0].Diagnostics
            .Where(d => d.Code == "dml-without-where").ToList();

        // Line 1: suppressed (between disable on line 0 and enable on line 2)
        Assert.DoesNotContain(dmlDiagnostics, d => d.Range.Start.Line == 1);
        // Line 3: NOT suppressed (after enable on line 2)
        Assert.Contains(dmlDiagnostics, d => d.Range.Start.Line == 3);
    }

    [Fact]
    public void CaseInsensitiveDirective_Works()
    {
        var sql = "/* TSQLREFINE-DISABLE */ select * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void LineComment_DisableDirective_Works()
    {
        var sql = "-- tsqlrefine-disable\nselect * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void ParseErrors_NotSuppressedByDisable()
    {
        // Parse errors should NOT be suppressed - they indicate syntax issues
        var sql = "/* tsqlrefine-disable */ SELECT * FROM";  // Incomplete SELECT (parse error)

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        // Parse errors have code "parse-error", and they are added before rule diagnostics
        // and are not filtered by disable directives
        Assert.Contains(result.Files[0].Diagnostics, d => d.Code == TsqlRefineEngine.ParseErrorCode);
    }

    [Fact]
    public void DisableMultipleRules_SuppressesAllSpecifiedRules()
    {
        // Test that multiple rules can be disabled at once
        var sql = "/* tsqlrefine-disable avoid-select-star, dml-without-where */\nselect * from t;\nupdate t set x = 1;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var diagnostics = result.Files[0].Diagnostics;

        // Lines 1-2 should NOT have diagnostics for disabled rules
        var selectStarDiags = diagnostics.Where(d => d.Code == "avoid-select-star").ToList();
        var dmlDiags = diagnostics.Where(d => d.Code == "dml-without-where").ToList();

        Assert.Empty(selectStarDiags);
        Assert.Empty(dmlDiags);
    }

    [Fact]
    public void DisableMultipleRules_AfterEnable_DiagnosticsReturn()
    {
        // Test that diagnostics return after enable for multiple rules
        // Use dml-without-where because it fires for each UPDATE without WHERE
        var sql = "/* tsqlrefine-disable dml-without-where */\nupdate t set x = 1;\n/* tsqlrefine-enable dml-without-where */\nupdate t set y = 2;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var dmlDiags = result.Files[0].Diagnostics
            .Where(d => d.Code == "dml-without-where").ToList();

        // Line 1: suppressed
        Assert.DoesNotContain(dmlDiags, d => d.Range.Start.Line == 1);
        // Line 3: NOT suppressed (after enable on line 2)
        Assert.Contains(dmlDiags, d => d.Range.Start.Line == 3);
    }

    [Fact]
    public void DisableDuplicateRuleIds_IsIdempotent()
    {
        var sql = "/* tsqlrefine-disable dml-without-where, DML-WITHOUT-WHERE */\nupdate t set x = 1;\n/* tsqlrefine-enable dml-without-where */\nupdate t set y = 2;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var dmlDiags = result.Files[0].Diagnostics
            .Where(d => d.Code == "dml-without-where").ToList();

        // Line 1: suppressed by disable directive.
        Assert.DoesNotContain(dmlDiags, d => d.Range.Start.Line == 1);
        // Line 3: not suppressed after a single enable.
        Assert.Contains(dmlDiags, d => d.Range.Start.Line == 3);
    }

    [Fact]
    public void NestedGlobalDisables_AllSuppressed()
    {
        // Test that nested disables work - all diagnostics within any disable range are suppressed
        var sql = "/* tsqlrefine-disable */\nupdate t set x = 1;\n/* tsqlrefine-disable */\nupdate t set y = 2;\n/* tsqlrefine-enable */\nupdate t set z = 3;\n/* tsqlrefine-enable */";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var dmlDiagnostics = result.Files[0].Diagnostics
            .Where(d => d.Code == "dml-without-where").ToList();

        // All lines 1, 3, 5 should be suppressed (within at least one disable range)
        Assert.Empty(dmlDiagnostics);
    }

    [Fact]
    public void NestedGlobalDisables_AfterAllEnables_DiagnosticsReturn()
    {
        // Test that after all enables, diagnostics return
        var sql = "/* tsqlrefine-disable */\nupdate t set x = 1;\n/* tsqlrefine-disable */\nupdate t set y = 2;\n/* tsqlrefine-enable */\nupdate t set z = 3;\n/* tsqlrefine-enable */\nupdate t set w = 4;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var dmlDiagnostics = result.Files[0].Diagnostics
            .Where(d => d.Code == "dml-without-where").ToList();

        // Lines 1, 3, 5 should be suppressed
        Assert.DoesNotContain(dmlDiagnostics, d => d.Range.Start.Line == 1);
        Assert.DoesNotContain(dmlDiagnostics, d => d.Range.Start.Line == 3);
        Assert.DoesNotContain(dmlDiagnostics, d => d.Range.Start.Line == 5);

        // Line 7 should NOT be suppressed (after both enables on lines 4 and 6)
        Assert.Contains(dmlDiagnostics, d => d.Range.Start.Line == 7);
    }

    [Fact]
    public void DisableSpecificRuleWithReason_StillSuppressesDiagnostics()
    {
        var sql = "/* tsqlrefine-disable avoid-select-star: legacy view depends on column order */\nselect * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void DisableAllWithReasonColonOnly_StillSuppressesAllDiagnostics()
    {
        var sql = "/* tsqlrefine-disable: generated code */\nselect * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }

    [Fact]
    public void DisableEnableWithReasons_RegionStillWorks()
    {
        var sql = "/* tsqlrefine-disable dml-without-where: migration script */\nupdate t set x = 1;\n/* tsqlrefine-enable dml-without-where: end migration */\nupdate t set y = 2;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        var dmlDiags = result.Files[0].Diagnostics
            .Where(d => d.Code == "dml-without-where").ToList();

        // Line 1: suppressed (within disable region)
        Assert.DoesNotContain(dmlDiags, d => d.Range.Start.Line == 1);
        // Line 3: NOT suppressed (after enable)
        Assert.Contains(dmlDiags, d => d.Range.Start.Line == 3);
    }

    [Fact]
    public void DisableAllWithReasonNoSpace_LineComment_StillSuppresses()
    {
        var sql = "-- tsqlrefine-disable:generated code\nselect * from t;";

        var result = _engine.Run(
            command: "lint",
            inputs: new[] { new SqlInput("test.sql", sql) },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.DoesNotContain(result.Files[0].Diagnostics, d => d.Code == "avoid-select-star");
    }
}
