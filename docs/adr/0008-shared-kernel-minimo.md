# ADR 0008 — Shared Kernel mínimo, infraestrutura compartilhada e contratos por consumidor

**Status:** aceito (revisado após o code review)

## Decisão
O que é compartilhado entre os serviços vive em `src/BuildingBlocks`, dividido por **motivo de mudança**:

| Projeto | Conteúdo | Pode depender de |
|---|---|---|
| `FluxoCaixa.BuildingBlocks` | *Shared Kernel* (Evans, cap. 14): `Entity`, `AggregateRoot`, `ValueObject`, `Money`, `Result`/`Error`, interfaces CQRS. | nada de infraestrutura |
| `FluxoCaixa.BuildingBlocks.Application` | Decorators (validação, transação, log), `IUnitOfWork`, `AddCqrs(assembly)`. | Kernel, FluentValidation, Scrutor |
| `FluxoCaixa.BuildingBlocks.Infrastructure` | `DbSession`/`UnitOfWork` (Dapper/Npgsql), `DatabaseMigrator` (DbUp), conexão RabbitMQ. | Application |
| `FluxoCaixa.BuildingBlocks.Api` | `ProblemDetails`, middleware de correlação, tratamento global de exceções, health checks, `AddApiDefaults`. | Infrastructure, ASP.NET Core |

**Nunca compartilhado:** o contrato de integração. O `LancamentoRegistradoIntegrationEvent` é declarado pelo publicador
(Lançamentos, versionado em `EventType` = `lancamento.registrado.v1`) e **copiado** pelo consumidor (Consolidação).
Os serviços só concordam no formato JSON.

## Por quê
- Antes do refinamento havia 13 arquivos de infraestrutura duplicados entre os serviços (decorators, sessão de banco,
  migrator, middlewares). Isso viola DRY e faz correções precisarem ser repetidas em dois lugares.
- Um assembly de **contratos** compartilhado, por outro lado, obrigaria deploy coordenado a cada mudança; a cópia por
  consumidor permite evolução independente (campos novos são ignorados; mudança incompatível exige `v2`).
- A divisão em quatro projetos evita que o domínio herde dependências de infraestrutura: testes de arquitetura garantem
  que o Kernel e o `BuildingBlocks.Application` não referenciam Dapper, Npgsql, RabbitMQ nem ASP.NET Core, e que nenhum
  BuildingBlock conhece `Lancamentos` ou `Consolidacao`.

## Custo assumido
Uma mudança em um BuildingBlock afeta os dois serviços. É aceitável enquanto estiverem no mesmo repositório e versão;
se forem separados em repositórios, esses projetos viram pacotes NuGet versionados.
