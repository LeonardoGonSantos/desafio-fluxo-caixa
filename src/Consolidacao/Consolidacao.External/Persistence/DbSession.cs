using System.Data.Common;
using Npgsql;

namespace Consolidacao.External.Persistence;

/// <summary>
/// Conexão + transação por escopo de requisição. Repositórios, outbox e queries
/// compartilham a mesma sessão, garantindo atomicidade da operação.
/// </summary>
public sealed class DbSession(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    public NpgsqlTransaction? Transaction { get; private set; }

    public async ValueTask<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = await dataSource.OpenConnectionAsync(cancellationToken);
        }

        return _connection;
    }

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException("Já existe uma transação em andamento nesta sessão.");
        }

        var connection = await GetConnectionAsync(cancellationToken);
        Transaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (Transaction is null)
        {
            return;
        }

        await Transaction.CommitAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (Transaction is null)
        {
            return;
        }

        try
        {
            await Transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async ValueTask DisposeTransactionAsync()
    {
        if (Transaction is not null)
        {
            await ((DbTransaction)Transaction).DisposeAsync();
            Transaction = null;
        }
    }
}
