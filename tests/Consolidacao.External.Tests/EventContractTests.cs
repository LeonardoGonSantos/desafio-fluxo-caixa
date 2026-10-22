using System.Reflection;
using System.Text.Json;
using Consolidacao.Application.IntegrationEvents;
using Shouldly;

namespace Consolidacao.External.Tests;

/// <summary>
/// Lado CONSUMIDOR do contrato lancamento.registrado.v1, verificado contra o mesmo arquivo de exemplo
/// usado pelo publicador (Lancamentos.External.Tests).
/// </summary>
public class EventContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string Exemplo() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", "lancamento.registrado.v1.json"));

    [Fact]
    public void ConsumidorEntendeOContratoV1()
    {
        var evento = JsonSerializer.Deserialize<LancamentoRegistradoIntegrationEvent>(Exemplo(), Web)!;

        evento.EventId.ShouldBe(Guid.Parse("8f14e45f-ceea-467a-9575-9a4f1c1d2b01"));
        evento.LancamentoId.ShouldBe(Guid.Parse("0b7e7bc1-5c1a-4a3c-9d3e-3a6e4a5f9c02"));
        evento.OccurredAt.ShouldBe(new DateTimeOffset(2025, 6, 10, 15, 0, 0, TimeSpan.Zero));
        evento.Tipo.ShouldBe("Credito");
        evento.Valor.ShouldBe(150.75m);
        evento.DataCompetencia.ShouldBe(new DateOnly(2025, 6, 10));
    }

    [Fact]
    public void TodoCampoQueOConsumidorEsperaExisteNoContrato()
    {
        var campos = JsonDocument.Parse(Exemplo()).RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        var esperados = typeof(LancamentoRegistradoIntegrationEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        foreach (var esperado in esperados)
        {
            campos.ShouldContain(esperado, $"O consumidor espera o campo '{esperado}', ausente do contrato v1.");
        }
    }

    [Fact]
    public void TipoDoEvento_EOMesmoIdentificadorVersionadoDoPublicador() =>
        LancamentoRegistradoIntegrationEvent.Type.ShouldBe("lancamento.registrado.v1");
}
