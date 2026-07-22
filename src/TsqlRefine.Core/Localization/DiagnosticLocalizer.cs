using System.Collections.Frozen;
using System.Globalization;
using System.Resources;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Localization;

#pragma warning disable CA1031 // Translation providers are optional extensions and must not break analysis.
/// <summary>Resolves diagnostic resource keys while preserving the original message as a fallback.</summary>
public sealed class DiagnosticLocalizer
{
    private readonly IReadOnlyList<IDiagnosticLocalizationProvider> _providers;
    private readonly ResourceManager _builtInResources;

    public DiagnosticLocalizer(
        IEnumerable<IDiagnosticLocalizationProvider>? providers = null,
        ResourceManager? builtInResources = null)
    {
        _providers = providers?.ToArray() ?? Array.Empty<IDiagnosticLocalizationProvider>();
        _builtInResources = builtInResources ?? BuiltInResources.Manager;
    }

    public Diagnostic Localize(Diagnostic diagnostic, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(culture);

        var localization = diagnostic.Localization;
        if (localization is null)
        {
            return diagnostic;
        }

        var key = localization.Key;
        var arguments = localization.Arguments ?? FrozenDictionary<string, object?>.Empty;
        foreach (var candidateCulture in GetCultureChain(culture))
        {
            foreach (var provider in _providers)
            {
                try
                {
                    var providerTemplate = provider.GetString(key, candidateCulture);
                    if (providerTemplate is not null && TryFormat(providerTemplate, arguments, out var providerMessage))
                    {
                        return diagnostic with { Message = providerMessage };
                    }
                }
                catch (Exception)
                {
                    // A broken translation must not break analysis.
                }
            }

            var builtInTemplate = TryGetBuiltIn(key, candidateCulture);
            if (builtInTemplate is not null && TryFormat(builtInTemplate, arguments, out var builtInMessage))
            {
                return diagnostic with { Message = builtInMessage };
            }
        }

        return diagnostic;
    }

    private string? TryGetBuiltIn(string key, CultureInfo culture)
    {
        try
        {
            return _builtInResources.GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }

    private static bool TryFormat(
        string template,
        IReadOnlyDictionary<string, object?> arguments,
        out string message)
    {
        try
        {
            message = template;
            foreach (var (name, value) in arguments)
            {
                message = message.Replace("{" + name + "}", Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal);
            }

            // Resource templates must not contain unresolved named placeholders.
            var open = message.IndexOf('{');
            var close = message.IndexOf('}');
            if (open >= 0 && close > open)
            {
                message = string.Empty;
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            message = string.Empty;
            return false;
        }
    }

    private static IEnumerable<CultureInfo> GetCultureChain(CultureInfo culture)
    {
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            yield return current;
        }
        yield return CultureInfo.InvariantCulture;
    }
}

internal static class BuiltInResources
{
    public static readonly ResourceManager Manager = new(
        "TsqlRefine.Core.Localization.Resources.Diagnostics",
        typeof(BuiltInResources).Assembly);
}
#pragma warning restore CA1031
