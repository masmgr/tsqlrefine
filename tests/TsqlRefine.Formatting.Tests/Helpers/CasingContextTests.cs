using TsqlRefine.Formatting.Helpers.Casing;
using Xunit;

namespace TsqlRefine.Formatting.Tests.Helpers;

public class CasingContextTests
{
    #region Initial State

    [Fact]
    public void NewCasingContext_HasDefaultValues()
    {
        var context = new CasingContext();

        Assert.False(context.InTableContext);
        Assert.False(context.AfterAsKeyword);
        Assert.Null(context.LastSchemaName);
        Assert.False(context.InExecuteContext);
        Assert.False(context.ExecuteProcedureProcessed);
        Assert.False(context.InTableColumnList);
    }

    #endregion

    #region Property Setters

    [Fact]
    public void InTableContext_CanBeSet()
    {
        var context = new CasingContext { InTableContext = true };
        Assert.True(context.InTableContext);
    }

    [Fact]
    public void AfterAsKeyword_CanBeSet()
    {
        var context = new CasingContext { AfterAsKeyword = true };
        Assert.True(context.AfterAsKeyword);
    }

    [Fact]
    public void LastSchemaName_CanBeSet()
    {
        var context = new CasingContext { LastSchemaName = "dbo" };
        Assert.Equal("dbo", context.LastSchemaName);
    }

    [Fact]
    public void InExecuteContext_CanBeSet()
    {
        var context = new CasingContext { InExecuteContext = true };
        Assert.True(context.InExecuteContext);
    }

    [Fact]
    public void ExecuteProcedureProcessed_CanBeSet()
    {
        var context = new CasingContext { ExecuteProcedureProcessed = true };
        Assert.True(context.ExecuteProcedureProcessed);
    }

    [Fact]
    public void InTableColumnList_CanBeSet()
    {
        var context = new CasingContext { InTableColumnList = true };
        Assert.True(context.InTableColumnList);
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ClearsAllState()
    {
        var context = new CasingContext
        {
            InTableContext = true,
            AfterAsKeyword = true,
            LastSchemaName = "sys",
            InExecuteContext = true,
            ExecuteProcedureProcessed = true,
            InTableColumnList = true
        };

        context.Reset();

        Assert.False(context.InTableContext);
        Assert.False(context.AfterAsKeyword);
        Assert.Null(context.LastSchemaName);
        Assert.False(context.InExecuteContext);
        Assert.False(context.ExecuteProcedureProcessed);
        Assert.False(context.InTableColumnList);
    }

    [Fact]
    public void Reset_OnAlreadyDefaultContext_RemainsDefault()
    {
        var context = new CasingContext();
        context.Reset();

        Assert.False(context.InTableContext);
        Assert.False(context.AfterAsKeyword);
        Assert.Null(context.LastSchemaName);
        Assert.False(context.InExecuteContext);
        Assert.False(context.ExecuteProcedureProcessed);
        Assert.False(context.InTableColumnList);
    }

    #endregion
}
