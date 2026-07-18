using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Rules.Tests.Correctness;

internal static class ExecCatalogRuleTestHelper
{
    internal static RuleContext CreateContext(string callSql, string definitionSql)
    {
        var catalog = ObjectCatalogCollector.Collect([(definitionSql, "definition.sql")], 150);
        return RuleTestContext.CreateContext(callSql) with
        {
            ObjectCatalog = new ObjectCatalogProvider(catalog)
        };
    }
}
