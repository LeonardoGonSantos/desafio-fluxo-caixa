# ADR 0003 — CQRS explícito, sem MediatR

**Status:** aceito

## Decisão
- Cada caso de uso é uma pasta com `Command|Query`, `Handler`, `Validator` e DTO.
- Handlers implementam `ICommandHandler<TCommand,TResponse>` / `IQueryHandler<TQuery,TResponse>` e são **injetados diretamente
  nos controllers** (sem mediator/dispatcher).
- Preocupações transversais são **decorators** registrados com Scrutor, na ordem
  *Logging → Validação → Transação → Handler*.
- O lado de **escrita** passa pelo agregado e pelo repositório; o lado de **leitura** usa SQL direto para DTO (`I*Queries`),
  sem reidratar agregados. É isso que torna o CQRS real, e não só uma convenção de nomes.
- Falhas de negócio retornam `Result<T>` (sem exceções para fluxo de controle).

## Alternativas consideradas
- **MediatR:** o pacote passou a ter licença comercial; além disso, o dispatch por reflexão esconde o caminho de chamada (F12 não leva ao handler).
- **Mapear tudo em services:** perde a separação entre intenção de escrita e de leitura.

## Consequências
(+) Zero dependência de mediator, navegação trivial, pipeline explícito e testável por decorator. 
(−) O controller injeta um handler por ação; aceitável com 4 casos de uso por serviço.
