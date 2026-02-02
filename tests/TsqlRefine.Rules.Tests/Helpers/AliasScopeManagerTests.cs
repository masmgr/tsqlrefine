using TsqlRefine.Rules.Helpers;
using Xunit;

namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class AliasScopeManagerTests
{
    [Fact]
    public void IsAliasDefinedInAnyScope_EmptyStack_ReturnsFalse()
    {
        // Arrange
        var manager = new AliasScopeManager();

        // Act & Assert
        Assert.False(manager.IsAliasDefinedInAnyScope("t1"));
    }

    [Fact]
    public void IsAliasDefinedInAnyScope_AliasInCurrentScope_ReturnsTrue()
    {
        // Arrange
        var manager = new AliasScopeManager();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "t1", "t2" };

        // Act
        using (manager.PushScope(aliases))
        {
            // Assert
            Assert.True(manager.IsAliasDefinedInAnyScope("t1"));
            Assert.True(manager.IsAliasDefinedInAnyScope("t2"));
            Assert.False(manager.IsAliasDefinedInAnyScope("t3"));
        }
    }

    [Fact]
    public void IsAliasDefinedInAnyScope_AliasInOuterScope_ReturnsTrue()
    {
        // Arrange
        var manager = new AliasScopeManager();
        var outerAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "outer" };
        var innerAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "inner" };

        // Act & Assert - simulates correlated subquery
        using (manager.PushScope(outerAliases))
        {
            using (manager.PushScope(innerAliases))
            {
                // Inner scope can see both inner and outer aliases
                Assert.True(manager.IsAliasDefinedInAnyScope("inner"));
                Assert.True(manager.IsAliasDefinedInAnyScope("outer"));
                Assert.False(manager.IsAliasDefinedInAnyScope("unknown"));
            }
        }
    }

    [Fact]
    public void IsAliasDefinedInAnyScope_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var manager = new AliasScopeManager();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TableAlias" };

        // Act
        using (manager.PushScope(aliases))
        {
            // Assert - SQL aliases are case-insensitive
            Assert.True(manager.IsAliasDefinedInAnyScope("TableAlias"));
            Assert.True(manager.IsAliasDefinedInAnyScope("tablealias"));
            Assert.True(manager.IsAliasDefinedInAnyScope("TABLEALIAS"));
        }
    }

    [Fact]
    public void PushScope_Dispose_PopsScopeCorrectly()
    {
        // Arrange
        var manager = new AliasScopeManager();
        var scope1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" };
        var scope2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "b" };

        // Act & Assert
        using (manager.PushScope(scope1))
        {
            Assert.True(manager.IsAliasDefinedInAnyScope("a"));

            using (manager.PushScope(scope2))
            {
                Assert.True(manager.IsAliasDefinedInAnyScope("a"));
                Assert.True(manager.IsAliasDefinedInAnyScope("b"));
            }

            // After inner scope disposed, "b" should no longer be visible
            Assert.True(manager.IsAliasDefinedInAnyScope("a"));
            Assert.False(manager.IsAliasDefinedInAnyScope("b"));
        }

        // After outer scope disposed, "a" should no longer be visible
        Assert.False(manager.IsAliasDefinedInAnyScope("a"));
    }

    [Fact]
    public void IsAliasDefinedInAnyScope_DeeplyNestedScopes_FindsAliasInAnyLevel()
    {
        // Arrange
        var manager = new AliasScopeManager();
        var scope1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "level1" };
        var scope2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "level2" };
        var scope3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "level3" };
        var scope4 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "level4" };

        // Act & Assert - simulate deeply nested subqueries
        using (manager.PushScope(scope1))
        using (manager.PushScope(scope2))
        using (manager.PushScope(scope3))
        using (manager.PushScope(scope4))
        {
            // All levels should be accessible
            Assert.True(manager.IsAliasDefinedInAnyScope("level1"));
            Assert.True(manager.IsAliasDefinedInAnyScope("level2"));
            Assert.True(manager.IsAliasDefinedInAnyScope("level3"));
            Assert.True(manager.IsAliasDefinedInAnyScope("level4"));
        }
    }

    [Fact]
    public void IsAliasDefinedInAnyScope_SameAliasInMultipleScopes_ReturnsTrue()
    {
        // Arrange
        var manager = new AliasScopeManager();
        var outerScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "t", "outer_only" };
        var innerScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "t", "inner_only" };

        // Act & Assert - alias shadowing scenario
        using (manager.PushScope(outerScope))
        {
            using (manager.PushScope(innerScope))
            {
                // "t" exists in both scopes (shadowing)
                Assert.True(manager.IsAliasDefinedInAnyScope("t"));
                Assert.True(manager.IsAliasDefinedInAnyScope("outer_only"));
                Assert.True(manager.IsAliasDefinedInAnyScope("inner_only"));
            }
        }
    }
}
