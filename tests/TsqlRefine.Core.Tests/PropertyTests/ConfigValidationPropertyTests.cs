using FsCheck;
using FsCheck.Xunit;
using TsqlRefine.Core.Config;

namespace TsqlRefine.Core.Tests.PropertyTests;

/// <summary>
/// Property-based tests for TsqlRefineConfig validation:
/// valid configs always pass, invalid configs always fail, and validation is idempotent.
/// </summary>
public sealed class ConfigValidationPropertyTests
{
    private static readonly int[] ValidCompatLevels = [100, 110, 120, 130, 140, 150, 160];

    [Property(MaxTest = 200)]
    public bool ValidConfig_PassesValidation(
        NonNegativeInt compatIdx, PositiveInt indentRaw, NonNegativeInt maxLine)
    {
        var compat = ValidCompatLevels[compatIdx.Get % ValidCompatLevels.Length];
        var indent = (indentRaw.Get % 16) + 1; // 1..16
        var maxLen = maxLine.Get % 201; // 0..200

        var config = new TsqlRefineConfig(
            CompatLevel: compat,
            Formatting: new FormattingConfig(IndentSize: indent, MaxLineLength: maxLen));

        return config.Validate() is null;
    }

    [Property(MaxTest = 200)]
    public bool InvalidCompatLevel_FailsValidation(int compat)
    {
        if (TsqlRefineConfig.ValidCompatLevels.Contains(compat))
        {
            return true; // skip valid compat levels
        }

        var config = new TsqlRefineConfig(CompatLevel: compat);
        var error = config.Validate();

        return error is not null && error.Contains("compatLevel", StringComparison.OrdinalIgnoreCase);
    }

    [Property(MaxTest = 200)]
    public bool InvalidIndentSize_FailsValidation(int indentSize)
    {
        if (indentSize >= 1 && indentSize <= 16)
        {
            return true; // skip valid indent sizes
        }

        var config = new TsqlRefineConfig(
            Formatting: new FormattingConfig(IndentSize: indentSize));
        var error = config.Validate();

        return error is not null && error.Contains("indentSize", StringComparison.OrdinalIgnoreCase);
    }

    [Property(MaxTest = 200)]
    public bool NegativeMaxLineLength_FailsValidation(NegativeInt maxLine)
    {
        var config = new TsqlRefineConfig(
            Formatting: new FormattingConfig(MaxLineLength: maxLine.Get));
        var error = config.Validate();

        return error is not null && error.Contains("maxLineLength", StringComparison.OrdinalIgnoreCase);
    }

    [Property(MaxTest = 200)]
    public bool Validation_IsIdempotent(int compat, int indent, int maxLine)
    {
        var config = new TsqlRefineConfig(
            CompatLevel: compat,
            Formatting: new FormattingConfig(IndentSize: indent, MaxLineLength: maxLine));

        var first = config.Validate();
        var second = config.Validate();
        return first == second;
    }

    [Fact]
    public void DefaultConfig_IsValid()
    {
        var error = TsqlRefineConfig.Default.Validate();
        Assert.Null(error);
    }

    [Property(MaxTest = 200)]
    public bool ValidConfig_ConstraintsSatisfied(
        NonNegativeInt compatIdx, PositiveInt indentRaw, NonNegativeInt maxLine)
    {
        var compat = ValidCompatLevels[compatIdx.Get % ValidCompatLevels.Length];
        var indent = (indentRaw.Get % 16) + 1;
        var maxLen = maxLine.Get % 201;

        var config = new TsqlRefineConfig(
            CompatLevel: compat,
            Formatting: new FormattingConfig(IndentSize: indent, MaxLineLength: maxLen));

        if (config.Validate() is not null)
        {
            return true; // skip invalid configs
        }

        return TsqlRefineConfig.ValidCompatLevels.Contains(config.CompatLevel)
               && config.Formatting!.IndentSize >= 1 && config.Formatting.IndentSize <= 16
               && config.Formatting.MaxLineLength >= 0;
    }
}
