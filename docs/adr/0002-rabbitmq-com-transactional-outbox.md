# ADR 0002 — RabbitMQ com Transactional Outbox

**Status:** aceito

## Contexto
Precisamos publicar `LancamentoRegistrado` sem nunca perder o evento e sem que a indisponibilidade do broker
impeça o registro do lançamento (*dual-write problem*: gravar no banco **e** publicar no broker não é atômico).

## Decisão
1. O handler grava o lançamento **e** uma linha em `outbox_messages` na **mesma transação**.
2. Um `BackgroundService` (`OutboxProcessor`) trabalha em três passos curtos, **sem manter transação aberta durante I/O de rede**:
   1. **Reserva (claim)** — `UPDATE … SET locked_until = now() + lease WHERE id IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING …`
      e commit imediato. Duas instâncias nunca reservam a mesma mensagem.
   2. **Publica** no RabbitMQ com *publisher confirms* e `mandatory`, uma a uma, em ordem de ocorrência.
   3. **Marca** `processed_at` (sucesso) ou registra `attempts`/`last_error` e libera (falha). Numa falha, as demais mensagens do lote
      são liberadas sem registrar tentativa e o loop faz *backoff* exponencial.
3. Broker fora do ar ⇒ a mensagem fica no banco e é reenviada depois; o `POST` continua devolvendo `201`.
4. `mandatory=true`: se nenhuma fila estiver vinculada (a Consolidação ainda não subiu), o broker devolve a mensagem,
   o publisher a trata como falha e ela **permanece no outbox** — não é descartada silenciosamente pelo exchange.
5. Resiliência da publicação (Polly, injetada via `AddResiliencePipeline`): **circuit breaker + timeout**. **Não há retry no Polly**:
   o retry é o do próprio outbox, que é durável (sobrevive a restart). Dois retries empilhados só multiplicariam a latência sob falha.
6. **Lease**: se o processo morrer entre publicar e marcar, o lease expira e a mensagem é reprocessada por outra instância.
   Isso pode gerar **publicação duplicada** (entrega *at-least-once*), absorvida pelo consumidor idempotente (ADR 0005).
7. `Outbox:Enabled=false` desliga o loop (réplicas só de API; testes que dirigem o processamento).

## Alternativas consideradas
| Alternativa | Por que não |
|---|---|
| Publicar direto no handler, depois do commit | Se o broker cair entre o commit e a publicação, o evento se perde. Se publicar antes do commit, pode publicar algo que sofre rollback. |
| Kafka | Excelente para alto volume/replay, mas é desproporcional para 50 msg/s e adiciona custo operacional. Fica como evolução (ver README). |
| CDC (Debezium) sobre o outbox | Tira o polling, mas exige Kafka Connect. Evolução futura. |
| Azure Service Bus / SQS | Acoplaria o desafio a um provedor; RabbitMQ roda local em Docker. |

## Consequências
- (+) Nenhum evento perdido; Lançamentos isolado de falhas do broker.
- (+) Entrega *at least once* ⇒ o consumidor precisa ser idempotente (ADR 0005).
- (+) Locks de linha seguram só o tempo da reserva, não o da publicação.
- (−) Latência extra de até `Outbox:PollingInterval` (1 s padrão) e carga de polling no banco (índice parcial em pendentes).
- (−) Ordem estrita não é garantida entre instâncias concorrentes — irrelevante aqui, pois somar é comutativo.
