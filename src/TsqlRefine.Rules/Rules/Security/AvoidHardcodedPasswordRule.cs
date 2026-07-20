using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Security;

/// <summary>
/// Detects password literals in login DDL and ad hoc data-source connection strings.
/// </summary>
public sealed partial class AvoidHardcodedPasswordRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-hardcoded-password",
        Description: "Detects hardcoded passwords in login DDL and ad hoc data-source connection strings.",
        Category: "Security",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidHardcodedPasswordVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    [GeneratedRegex(@"(?:^|;)\s*(?:password|pwd)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPasswordRegex();

    private sealed class AvoidHardcodedPasswordVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(PasswordCreateLoginSource node)
        {
            ReportPassword(node.Password, "CREATE LOGIN");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterLoginOptionsStatement node)
        {
            foreach (var passwordOption in node.Options.OfType<PasswordAlterPrincipalOption>())
            {
                ReportPassword(passwordOption.Password, "ALTER LOGIN");
                ReportPassword(passwordOption.OldPassword, "ALTER LOGIN OLD_PASSWORD");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(OpenRowsetTableReference node)
        {
            ReportPassword(node.Password, "OPENROWSET");
            ReportConnectionString(node.ProviderString, "OPENROWSET");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AdHocDataSource node)
        {
            ReportConnectionString(node.InitString, "OPENDATASOURCE");
            base.ExplicitVisit(node);
        }

        private void ReportPassword(Literal? password, string construct)
        {
            if (password is not StringLiteral)
            {
                return;
            }

            AddDiagnostic(
                fragment: password,
                message: $"Avoid a hardcoded password in {construct}. Use a secure credential or deployment-time secret instead.",
                code: "avoid-hardcoded-password",
                category: "Security",
                fixable: false
            );
        }

        private void ReportConnectionString(StringLiteral? connectionString, string construct)
        {
            if (connectionString is null || !ConnectionStringPasswordRegex().IsMatch(connectionString.Value))
            {
                return;
            }

            ReportPassword(connectionString, $"{construct} connection string");
        }
    }
}
