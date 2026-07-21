using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Provides complete snapshot column models for built-in schema analysis.
/// This contract lives outside PluginSdk so additional metadata does not break external plugins.
/// </summary>
public interface IColumnSchemaProvider
{
    /// <summary>Gets the snapshot column models for a resolved table or view.</summary>
    IReadOnlyList<ColumnSchema> GetColumnSchemas(ResolvedTable table);
}
