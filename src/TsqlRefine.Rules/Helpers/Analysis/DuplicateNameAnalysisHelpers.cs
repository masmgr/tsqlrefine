namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Helper utilities for finding duplicate names in collections.
/// </summary>
public static class DuplicateNameAnalysisHelpers
{
    /// <summary>
    /// Finds duplicate names in a sequence based on a name selector.
    /// Returns each duplicate item with its duplicated name (second and later occurrences).
    /// </summary>
    /// <typeparam name="T">The element type in the sequence.</typeparam>
    /// <param name="items">The source items.</param>
    /// <param name="nameSelector">Selects a nullable name from each item.</param>
    /// <param name="comparer">
    /// The comparer used for name equality.
    /// Defaults to <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </param>
    /// <returns>A sequence of duplicate items and their names.</returns>
    /// <exception cref="ArgumentNullException">Thrown when items or nameSelector is null.</exception>
    public static IEnumerable<(T Item, string Name)> FindDuplicateNames<T>(
        IEnumerable<T> items,
        Func<T, string?> nameSelector,
        StringComparer? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nameSelector);

        var seen = new HashSet<string>(comparer ?? StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var name = nameSelector(item);
            if (name == null)
            {
                continue;
            }

            if (!seen.Add(name))
            {
                yield return (item, name);
            }
        }
    }
}
