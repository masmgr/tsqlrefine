namespace TsqlRefine.Rules.Tests.Helpers;

public sealed class DuplicateNameAnalysisHelpersTests
{
    [Fact]
    public void FindDuplicateNames_WithCaseInsensitiveDuplicates_ReturnsDuplicates()
    {
        // Arrange
        var items = new[]
        {
            new TestItem("Id"),
            new TestItem("name"),
            new TestItem("ID"),
            new TestItem("Name")
        };

        // Act
        var duplicates = DuplicateNameAnalysisHelpers.FindDuplicateNames(items, item => item.Name).ToArray();

        // Assert
        Assert.Equal(2, duplicates.Length);
        Assert.Equal("ID", duplicates[0].Name);
        Assert.Equal("Name", duplicates[1].Name);
    }

    [Fact]
    public void FindDuplicateNames_WithNullNames_IgnoresNulls()
    {
        // Arrange
        var items = new[]
        {
            new TestItem(null),
            new TestItem("a"),
            new TestItem(null),
            new TestItem("A")
        };

        // Act
        var duplicates = DuplicateNameAnalysisHelpers.FindDuplicateNames(items, item => item.Name).ToArray();

        // Assert
        Assert.Single(duplicates);
        Assert.Equal("A", duplicates[0].Name);
    }

    [Fact]
    public void FindDuplicateNames_WithCustomComparer_UsesProvidedComparison()
    {
        // Arrange
        var items = new[]
        {
            new TestItem("a"),
            new TestItem("A")
        };

        // Act
        var duplicates = DuplicateNameAnalysisHelpers.FindDuplicateNames(
            items,
            item => item.Name,
            StringComparer.Ordinal).ToArray();

        // Assert
        Assert.Empty(duplicates);
    }

    [Fact]
    public void FindDuplicateNames_WithNullItems_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DuplicateNameAnalysisHelpers.FindDuplicateNames<TestItem>(null!, item => item.Name).ToArray());
    }

    [Fact]
    public void FindDuplicateNames_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var items = new[] { new TestItem("a") };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DuplicateNameAnalysisHelpers.FindDuplicateNames(items, null!).ToArray());
    }

    private sealed record TestItem(string? Name);
}
