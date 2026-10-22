using Lancamentos.Application.IntegrationEvents;
using Lancamentos.Domain.Events;
using Lancamentos.Domain.Lancamentos;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class IntegrationEventMapperTests
{
    [Fact]
    public void Map_LancamentoRegistrado_PreservaEventIdEValores()
    {
        var domainEvent = new LancamentoRegistradoDomainEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), TipoLancamento.Debito, 42.5m, new DateOnly(2025, 6, 10));

        var mapped = IntegrationEventMapper.Map(domainEvent);

        mapped.EventId.ShouldBe(domainEvent.EventId);
        mapped.LancamentoId.ShouldBe(domainEvent.LancamentoId);
        mapped.Tipo.ShouldBe("Debito");
        mapped.Valor.ShouldBe(42.5m);
        mapped.DataCompetencia.ShouldBe(domainEvent.DataCompetencia);
        mapped.EventType.ShouldBe("lancamento.registrado.v1");
    }
}
