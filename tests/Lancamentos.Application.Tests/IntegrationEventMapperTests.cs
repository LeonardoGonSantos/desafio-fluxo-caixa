using FluxoCaixa.BuildingBlocks.Domain;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.IntegrationEvents;
using Lancamentos.Domain.Events;
using Lancamentos.Domain.Lancamentos;
using NSubstitute;
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

    private sealed record EventoDesconhecido(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class AgregadoDeTeste : AggregateRoot<Guid>
    {
        public AgregadoDeTeste() : base(Guid.NewGuid())
        {
        }

        public void Dispara(IDomainEvent evento) => Raise(evento);
    }

    [Fact]
    public async Task EnqueueDomainEvents_EventoSemMapeamento_LancaExcecaoEmVezDeIgnorarEmSilencio()
    {
        var agregado = new AgregadoDeTeste();
        agregado.Dispara(new EventoDesconhecido(Guid.NewGuid(), DateTimeOffset.UtcNow));

        var act = () => Substitute.For<IOutbox>().EnqueueDomainEventsAsync(agregado, CancellationToken.None);

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain(nameof(EventoDesconhecido));
    }

    [Fact]
    public async Task EnqueueDomainEvents_Sucesso_LimpaOsEventosDoAgregado()
    {
        var agregado = new AgregadoDeTeste();
        agregado.Dispara(new LancamentoRegistradoDomainEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), TipoLancamento.Credito, 1m, new DateOnly(2025, 6, 10)));
        var outbox = Substitute.For<IOutbox>();

        await outbox.EnqueueDomainEventsAsync(agregado, CancellationToken.None);

        agregado.DomainEvents.ShouldBeEmpty();
        await outbox.Received(1).EnqueueAsync(Arg.Any<LancamentoRegistradoIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
