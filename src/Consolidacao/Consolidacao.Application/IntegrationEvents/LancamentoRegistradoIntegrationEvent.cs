namespace Consolidacao.Application.IntegrationEvents;

/// <summary>
/// Contrato v1 publicado pelo serviço de Lançamentos. É uma cópia deliberada (consumer-driven):
/// os serviços não compartilham assemblies de contrato, só o formato JSON.
/// </summary>
public sealed record LancamentoRegistradoIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid LancamentoId,
    string Tipo,
    decimal Valor,
    DateOnly DataCompetencia)
{
    public const string Type = "lancamento.registrado.v1";
}
