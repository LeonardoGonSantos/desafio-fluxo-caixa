# ADR 0002 — RabbitMQ com Transactional Outbox

**Status:** aceito

## Contexto
Precisamos publicar `LancamentoRegistrado` sem nunca perder o evento e sem que a indisponibilidade do broker
impeça o registro do lançamento (*dual-write problem*: gravar no banco **e** publicar no broker não é atômico).

## Decisão
1. O handler grava o lançamento **e** uma linha em `outbox_messages` na **mesma transação**.
2. Um `BackgroundService` (`OutboxProcessor`) lê pendentes com `FOR UPDATE SKIP LOCKED`, publica com
   *publisher confirms* e `mandatory`, e só então marca `processed_at`.
3. Broker fora do ar ⇒ a mensagem fica no banco e é reenviada com backoff exponencial; o `POST` continua devolvendo `201`.
4. `mandatory=true`: se nenhuma fila estiver vinculada (a Consolidação ainda não subiu), o broker devolve a mensagem,
   o publisher a trata como falha e ela **permanece no outbox** — não é descartada silenciosamente pelo exchange.
5. Publicação com Polly (retry exponencial + jitter e circuit breaker).

Broker: **RabbitMQ** (quorum queues, DLQ, delivery-limit nativo).

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
- (−) Latência extra de até `Outbox:PollingInterval` (1 s padrão) e carga de polling no banco (índice parcial em pendentes).
- (−) Ordem estrita não é garantida entre instâncias concorrentes — irrelevante aqui, pois somar é comutativo.
