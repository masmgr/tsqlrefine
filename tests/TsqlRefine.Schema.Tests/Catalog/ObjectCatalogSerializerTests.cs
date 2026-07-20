using System.Text.Json;
using System.Text.Json.Nodes;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Schema.Tests.Catalog;

public sealed class ObjectCatalogSerializerTests
{
    [Fact]
    public void Deserialize_UnsupportedVersion_ThrowsJsonException()
    {
        var json = CreateValidCatalogJson();
        json["version"] = ObjectCatalogSerializer.CurrentVersion + 1;

        var exception = Assert.Throws<JsonException>(() =>
            ObjectCatalogSerializer.Deserialize(json.ToJsonString()));

        Assert.Contains("Unsupported object catalog version", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("objects")]
    [InlineData("references")]
    public void Deserialize_MissingRequiredData_ThrowsJsonException(string propertyName)
    {
        var json = CreateValidCatalogJson();
        json.Remove(propertyName);

        var exception = Assert.Throws<JsonException>(() =>
            ObjectCatalogSerializer.Deserialize(json.ToJsonString()));

        Assert.Contains("missing required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject CreateValidCatalogJson() => new()
    {
        ["version"] = ObjectCatalogSerializer.CurrentVersion,
        ["generatedAt"] = "2026-01-01T00:00:00+00:00",
        ["compatLevel"] = 160,
        ["scope"] = new JsonObject
        {
            ["databases"] = new JsonArray(),
            ["isAuthoritative"] = true,
            ["includesExternalReferences"] = false,
            ["defaultSchema"] = "dbo"
        },
        ["objects"] = new JsonArray(),
        ["references"] = new JsonArray()
    };
}
