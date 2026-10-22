using System.Text.Json;
using System.Text.Json.Nodes;
using Lancamentos.Application.IntegrationEvents;
using Lancamentos.External.Persistence.Repositories;
using Shouldly;

namespace Lancamentos.External.Tests;

/// <summary>
/// Lado PUBLICADOR do contrato lancamento.registrado.v1. O mesmo arquivo de exemplo é verificado pelo consumidor
/// (Consolidacao.External.Tests): se alguém renomear um campo de um lado, o outro teste quebra.
/// </summary>
public class EventContractTests
{
    private static JsonNode Exemplo() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", "lancamento.registrado.v1.json")))!;

    [Fact]
    public void EventoPublicado_TemExatamenteOFormatoDoContratoV1()
    {
        var evento = new LancamentoRegistradoIntegrationEvent(
            Guid.Parse("8f14e45f-ceea-467a-9575-9a4f1c1d2b01"),
            new DateTimeOffset(2025, 6, 10, 15, 0, 0, TimeSpan.Zero),
            Guid.Parse("0b7e7bc1-5c1a-4a3c-9d3e-3a6e4a5f9c02"),
            "Credito",
            150.75m,
            new DateOnly(2025, 6, 10));

        var json = JsonSerializer.SerializeToNode(evento, OutboxRepository.JsonOptions)!.AsObject();
        json.Remove("eventType"); // metadado de roteamento, vai no header/routing key — não faz parte do corpo do contrato

        JsonNode.DeepEquals(json, Exemplo()).ShouldBeTrue($"Corpo publicado divergiu do contrato:\n{json}");
    }

    [Fact]
    public void TipoDoEvento_EOIdentificadorVersionadoDoContrato() =>
        LancamentoRegistradoIntegrationEvent.Type.ShouldBe("lancamento.registrado.v1");
}
