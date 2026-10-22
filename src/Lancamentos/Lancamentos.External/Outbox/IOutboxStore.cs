using Lancamentos.External.Messaging;

namespace Lancamentos.External.Outbox;

/// <summary>Acesso à tabela outbox_messages para o processador (separa persistência de orquestração).</summary>
public interface IOutboxStore
{
    /// <summary>
    /// Reserva até <paramref name="batchSize"/> mensagens pendentes, em ordem de ocorrência, por <paramref name="lease"/>.
    /// A reserva é uma transação curta: nenhum lock fica preso durante a publicação.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, TimeSpan lease, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Registra a falha e libera a mensagem para nova tentativa.</summary>
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken);

    /// <summary>Libera, sem registrar tentativa, mensagens reservadas que não chegaram a ser publicadas.</summary>
    Task ReleaseAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
