namespace TsqlRefine.Rules.Helpers.Scope;

/// <summary>
/// Manages a stack of alias scopes for validating table references in nested queries.
/// Supports correlated subqueries by checking all scopes (current + outer).
/// </summary>
public sealed class AliasScopeManager
{
    private readonly Stack<HashSet<string>> _scopeStack = new();

    /// <summary>
    /// Checks if an alias is defined in any scope (current or outer).
    /// Optimized for typical query depths by checking current scope first.
    /// </summary>
    public bool IsAliasDefinedInAnyScope(string alias)
    {
        // Optimization: check current scope first (most common case)
        if (_scopeStack.TryPeek(out var currentScope) && currentScope.Contains(alias))
        {
            return true;
        }

        // Check outer scopes for correlated subqueries
        var isFirst = true;
        foreach (var scope in _scopeStack)
        {
            if (isFirst)
            {
                isFirst = false;
                continue; // Skip current scope (already checked)
            }

            if (scope.Contains(alias))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pushes a new scope onto the stack and returns a disposable guard.
    /// </summary>
    public ScopeGuard PushScope(HashSet<string> aliases)
    {
        _scopeStack.Push(aliases);
        return new ScopeGuard(_scopeStack);
    }

    /// <summary>
    /// RAII-style guard for automatic scope management.
    /// </summary>
    public readonly struct ScopeGuard(Stack<HashSet<string>> stack) : IDisposable
    {
        public void Dispose() => stack.Pop();
    }
}
