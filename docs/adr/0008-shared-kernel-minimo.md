# ADR 0008 — Shared Kernel mínimo e contratos por consumidor

**Status:** aceito

## Decisão
`FluxoCaixa.BuildingBlocks` contém **apenas primitivas genéricas** de DDD/CQRS: `Entity`, `AggregateRoot`, `ValueObject`,
`Money`, `Result`/`Error`, interfaces de handler. Segue a ideia de *Shared Kernel* do Evans (cap. 14): pequeno, estável e
acordado entre os dois lados.

**Não** compartilhamos o contrato de integração: o `LancamentoRegistradoIntegrationEvent` é declarado pelo publicador
(Lançamentos, versão no `EventType` = `lancamento.registrado.v1`) e **copiado** pelo consumidor (Consolidação).
Os serviços só concordam no formato JSON.

## Por quê
Um assembly de contratos compartilhado obriga a fazer deploy coordenado de ambos a cada mudança. Com a cópia por consumidor,
cada lado evolui no seu ritmo (campos adicionais são ignorados) e mudanças incompatíveis exigem um novo `EventType` (v2).
Testes de arquitetura garantem que um serviço não referencia o outro.

## Custo assumido
Pequena duplicação de código de infraestrutura (`DbSession`, migrator, decorators) entre os dois serviços, preferida a um
projeto "common" que acoplaria os ciclos de vida. Se aparecer um terceiro serviço, extrair um pacote de infraestrutura passa a compensar.
