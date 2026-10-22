using System.Reflection;
using FluxoCaixa.BuildingBlocks.Cqrs;
using NetArchTest.Rules;
using Shouldly;

namespace FluxoCaixa.ArchitectureTests;

/// <summary>Regras de dependência entre camadas e serviços, verificadas a cada build.</summary>
public class ArchitectureTests
{
    private static readonly Assembly LancamentosDomain = typeof(Lancamentos.Domain.Lancamentos.Lancamento).Assembly;
    private static readonly Assembly LancamentosApplication = typeof(Lancamentos.Application.DependencyInjection).Assembly;
    private static readonly Assembly LancamentosExternal = typeof(Lancamentos.External.DependencyInjection).Assembly;
    private static readonly Assembly LancamentosApi = typeof(Lancamentos.Api.Controllers.LancamentosController).Assembly;

    private static readonly Assembly ConsolidacaoDomain = typeof(Consolidacao.Domain.SaldosDiarios.SaldoDiario).Assembly;
    private static readonly Assembly ConsolidacaoApplication = typeof(Consolidacao.Application.DependencyInjection).Assembly;
    private static readonly Assembly ConsolidacaoExternal = typeof(Consolidacao.External.DependencyInjection).Assembly;
    private static readonly Assembly ConsolidacaoApi = typeof(Consolidacao.Api.Controllers.SaldosDiariosController).Assembly;

    public static TheoryData<string, Assembly> Domains => new()
    {
        { "Lancamentos", LancamentosDomain },
        { "Consolidacao", ConsolidacaoDomain },
    };

    public static TheoryData<string, Assembly> Applications => new()
    {
        { "Lancamentos", LancamentosApplication },
        { "Consolidacao", ConsolidacaoApplication },
    };

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_NaoDependeDe_Application_External_Api(string servico, Assembly domain)
    {
        string[] proibidos = [$"{servico}.Application", $"{servico}.External", $"{servico}.Api"];

        var result = Types.InAssembly(domain).ShouldNot().HaveDependencyOnAny(proibidos).GetResult();

        result.IsSuccessful.ShouldBeTrue(Falhas(result));
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_NaoDependeDe_Dapper_Npgsql_RabbitMQ_AspNet(string servico, Assembly domain)
    {
        _ = servico;
        string[] proibidos = ["Dapper", "Npgsql", "RabbitMQ", "Microsoft.AspNetCore", "FluentValidation", "Polly"];

        var result = Types.InAssembly(domain).ShouldNot().HaveDependencyOnAny(proibidos).GetResult();

        result.IsSuccessful.ShouldBeTrue(Falhas(result));
    }

    [Theory]
    [MemberData(nameof(Applications))]
    public void Application_NaoDependeDe_External_Api(string servico, Assembly application)
    {
        string[] proibidos = [$"{servico}.External", $"{servico}.Api", "Dapper", "Npgsql", "RabbitMQ", "Microsoft.AspNetCore"];

        var result = Types.InAssembly(application).ShouldNot().HaveDependencyOnAny(proibidos).GetResult();

        result.IsSuccessful.ShouldBeTrue(Falhas(result));
    }

    private static readonly Assembly KernelAssembly = typeof(FluxoCaixa.BuildingBlocks.Domain.Money).Assembly;
    private static readonly Assembly BuildingBlocksApplication = typeof(FluxoCaixa.BuildingBlocks.Application.CqrsRegistration).Assembly;
    private static readonly Assembly BuildingBlocksInfrastructure = typeof(FluxoCaixa.BuildingBlocks.Infrastructure.Persistence.DbSession).Assembly;
    private static readonly Assembly BuildingBlocksApi = typeof(FluxoCaixa.BuildingBlocks.Api.ApiDefaults).Assembly;

    [Fact]
    public void BuildingBlocks_NaoConhecemNenhumServico()
    {
        foreach (var assembly in new[] { KernelAssembly, BuildingBlocksApplication, BuildingBlocksInfrastructure, BuildingBlocksApi })
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny("Lancamentos", "Consolidacao").GetResult();
            result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name}: {Falhas(result)}");
        }
    }

    [Fact]
    public void BuildingBlocks_KernelEApplication_SemDependenciaDeInfraestrutura()
    {
        string[] proibidos = ["Dapper", "Npgsql", "RabbitMQ", "Microsoft.AspNetCore", "DbUp"];

        foreach (var assembly in new[] { KernelAssembly, BuildingBlocksApplication })
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(proibidos).GetResult();
            result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name}: {Falhas(result)}");
        }
    }

    [Fact]
    public void BuildingBlocks_Kernel_NaoDependeDeApplicationInfrastructureNemApi()
    {
        var result = Types.InAssembly(KernelAssembly)
            .ShouldNot().HaveDependencyOnAny("FluxoCaixa.BuildingBlocks.Application", "FluxoCaixa.BuildingBlocks.Infrastructure", "FluxoCaixa.BuildingBlocks.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Falhas(result));
    }

    [Fact]
    public void External_NaoDependeDe_Api()
    {
        Types.InAssembly(LancamentosExternal).ShouldNot().HaveDependencyOn("Lancamentos.Api").GetResult()
            .IsSuccessful.ShouldBeTrue();
        Types.InAssembly(ConsolidacaoExternal).ShouldNot().HaveDependencyOn("Consolidacao.Api").GetResult()
            .IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Servicos_SaoIndependentes_Lancamentos_NaoConheceConsolidacao_eViceVersa()
    {
        foreach (var assembly in new[] { LancamentosDomain, LancamentosApplication, LancamentosExternal, LancamentosApi })
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn("Consolidacao").GetResult();
            result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name}: {Falhas(result)}");
        }

        foreach (var assembly in new[] { ConsolidacaoDomain, ConsolidacaoApplication, ConsolidacaoExternal, ConsolidacaoApi })
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn("Lancamentos").GetResult();
            result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name}: {Falhas(result)}");
        }
    }

    [Fact]
    public void Handlers_E_Decorators_DevemSerSealed()
    {
        var naoSealed = new[] { LancamentosApplication, ConsolidacaoApplication }
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && ImplementaHandler(t))
            .Where(t => !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        naoSealed.ShouldBeEmpty();
    }

    [Fact]
    public void Controllers_SoExistemNaCamadaApi()
    {
        var foraDaApi = new[]
            {
                LancamentosDomain, LancamentosApplication, LancamentosExternal,
                ConsolidacaoDomain, ConsolidacaoApplication, ConsolidacaoExternal,
            }
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        foraDaApi.ShouldBeEmpty();
    }

    [Fact]
    public void Controllers_DependemDeHandlers_NaoDeRepositoriosNemInfraestrutura()
    {
        foreach (var api in new[] { LancamentosApi, ConsolidacaoApi })
        {
            var result = Types.InAssembly(api)
                .That().Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
                .ShouldNot().HaveDependencyOnAny("Dapper", "Npgsql", "RabbitMQ")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Falhas(result));
        }
    }

    [Fact]
    public void ControleDeSanidade_AsRegrasDetectamDependenciasReais()
    {
        // Garante que as regras acima não são vacuamente verdadeiras: estas dependências EXISTEM.
        Types.InAssembly(LancamentosApplication).That().HaveNameEndingWith("CommandHandler")
            .Should().HaveDependencyOn("Lancamentos.Domain").GetResult().IsSuccessful.ShouldBeTrue();
        Types.InAssembly(LancamentosExternal).That().HaveNameEndingWith("Repository")
            .Should().HaveDependencyOnAny("Dapper").GetResult().IsSuccessful.ShouldBeTrue();
        Types.InAssembly(LancamentosExternal).That().HaveName("RabbitMqPublisher")
            .Should().HaveDependencyOn("RabbitMQ").GetResult().IsSuccessful.ShouldBeTrue();
    }

    private static bool ImplementaHandler(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
             i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

    private static string Falhas(TestResult result) =>
        result.FailingTypeNames is null ? string.Empty : "Violações: " + string.Join(", ", result.FailingTypeNames);
}
