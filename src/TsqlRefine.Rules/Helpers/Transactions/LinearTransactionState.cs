using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Transactions;

/// <summary>
/// Tracks the lexical transaction balance shared by rules that intentionally do not perform
/// control-flow analysis.
/// </summary>
internal sealed class LinearTransactionState
{
    private readonly List<BeginTransactionStatement> _openTransactions = [];

    internal int Depth => _openTransactions.Count;

    internal bool HasSavepoint { get; private set; }

    internal IReadOnlyList<BeginTransactionStatement> OpenTransactions => _openTransactions;

    internal void Begin(BeginTransactionStatement statement) => _openTransactions.Add(statement);

    internal void Commit()
    {
        if (_openTransactions.Count == 0)
        {
            return;
        }

        _openTransactions.RemoveAt(_openTransactions.Count - 1);
        if (_openTransactions.Count == 0)
        {
            HasSavepoint = false;
        }
    }

    internal void Save()
    {
        if (_openTransactions.Count > 0)
        {
            HasSavepoint = true;
        }
    }

    internal void Rollback(RollbackTransactionStatement statement)
    {
        if (TransactionStatementHelpers.IsFullRollback(statement, _openTransactions.FirstOrDefault()))
        {
            Reset();
        }
    }

    internal LinearTransactionSnapshot Capture() =>
        new(_openTransactions.ToArray(), HasSavepoint);

    internal void Restore(LinearTransactionSnapshot snapshot)
    {
        _openTransactions.Clear();
        _openTransactions.AddRange(snapshot.OpenTransactions);
        HasSavepoint = snapshot.HasSavepoint;
    }

    internal void Reset()
    {
        _openTransactions.Clear();
        HasSavepoint = false;
    }
}

internal sealed record LinearTransactionSnapshot(
    IReadOnlyList<BeginTransactionStatement> OpenTransactions,
    bool HasSavepoint);

/// <summary>Centralizes the meaning of named and unnamed transaction rollbacks.</summary>
internal static class TransactionStatementHelpers
{
    internal static bool IsFullRollback(
        RollbackTransactionStatement rollback,
        BeginTransactionStatement? outerTransaction)
    {
        if (rollback.Name is null)
        {
            return true;
        }

        return outerTransaction?.Name?.Value is { Length: > 0 } transactionName &&
            string.Equals(rollback.Name.Value, transactionName, StringComparison.OrdinalIgnoreCase);
    }
}
