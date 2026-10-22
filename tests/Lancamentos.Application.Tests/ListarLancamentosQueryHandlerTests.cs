using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Common;
using Lancamentos.Application.Lancamentos;
using Lancamentos.Application.Lancamentos.ListarLancamentos;
using NSubstitute;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class ListarLancamentosQueryHandlerTests
{
    private static readonly DateOnly Dia = new(2025, 6, 10);
    private readonly ILancamentoQueries _queries = Substitute.For<ILancamentoQueries>();
    private readonly ListarLancamentosQueryHandler _handler;

    public ListarLancamentosQueryHandlerTests() => _handler = new ListarLancamentosQueryHandler(_queries);

    private static List<LancamentoResponse> Itens(int quantidade) =>
        Enumerable.Range(0, quantidade)
            .Select(i => new LancamentoResponse(
                Guid.NewGuid(), "Credito", 10m, $"Item {i}", Dia, "Ativo", null, new DateTimeOffset(2025, 6, 10, 12, 0, 0, TimeSpan.Zero).AddMinutes(-i)))
            .ToList();

    private void QuerieRetorna(List<LancamentoResponse> itens) =>
        _queries.ListAsync(default, default, default, default, default, default).ReturnsForAnyArgs(itens);

    [Fact]
    public async Task PedeUmaLinhaAMaisQueOTamanhoDaPagina()
    {
        QuerieRetorna(Itens(0));

        await _handler.Handle(new ListarLancamentosQuery(Dia, Dia, null, 20), CancellationToken.None);

        await _queries.Received(1).ListAsync(Dia, Dia, null, 21, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComMaisItensQueAPagina_DevolveSoOTamanhoDaPaginaEUmCursorParaAProxima()
    {
        var itens = Itens(4); // pageSize 3 => a 4ª linha só sinaliza que há mais
        QuerieRetorna(itens);

        var page = (await _handler.Handle(new ListarLancamentosQuery(Dia, Dia, null, 3), CancellationToken.None)).Value;

        page.Items.Select(i => i.Id).ShouldBe(itens.Take(3).Select(i => i.Id));
        LancamentoCursor.TryDecode(page.NextCursor, out var cursor).ShouldBeTrue();
        cursor.Id.ShouldBe(itens[2].Id); // o cursor aponta para o ÚLTIMO item entregue, não para a linha extra
        cursor.CriadoEm.ShouldBe(itens[2].CriadoEm);
    }

    [Fact]
    public async Task UltimaPagina_NaoTemCursor()
    {
        QuerieRetorna(Itens(3));

        var page = (await _handler.Handle(new ListarLancamentosQuery(Dia, Dia, null, 3), CancellationToken.None)).Value;

        page.Items.Count.ShouldBe(3);
        page.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task ComCursor_RepassaAPosicaoDecodificadaParaAConsulta()
    {
        QuerieRetorna(Itens(0));
        var posicao = new LancamentoCursor(Dia, new DateTimeOffset(2025, 6, 10, 9, 0, 0, TimeSpan.Zero), Guid.NewGuid());

        await _handler.Handle(new ListarLancamentosQuery(Dia, Dia, null, 20, posicao.Encode()), CancellationToken.None);

        await _queries.Received(1).ListAsync(Dia, Dia, null, 21, posicao, Arg.Any<CancellationToken>());
    }
}
