# ADR 0005 — Consistência eventual e consumidor idempotente

**Status:** aceito

## Contexto
O broker entrega *at least once* (reentregas após falha, restart, nack). Um evento duplicado não pode somar duas vezes no saldo.

## Decisão
- **Idempotência atômica:** o `AplicarLancamentoCommand` insere o `event_id` em `processed_events`
  (`ON CONFLICT DO NOTHING`) **na mesma transação** que atualiza `saldo_diario`. Se o evento já existe, nada é aplicado
  e a mensagem é confirmada. Rollback desfaz as duas coisas juntas.
- **Sem perda de atualização:** o agregado `SaldoDiario` é carregado com `FOR UPDATE`; consumidores concorrentes do mesmo dia são serializados.
  (Testado com 100 mensagens simultâneas.)
- **Ordem irrelevante:** somar créditos/débitos é comutativo. Lançamento retroativo (data de competência antiga) funciona sem tratamento especial;
  o **saldo acumulado é calculado na leitura** (`SUM` até a data), então dias posteriores se corrigem sozinhos.
- **Falhas:** JSON inválido, tipo desconhecido ou violação de domínio ⇒ `nack(requeue:false)` ⇒ **DLQ** direto.
  Falha transitória (ex.: banco indisponível) ⇒ a aplicação **republica a mensagem na própria fila com `x-retry-count + 1`**
  (após um atraso crescente) e confirma a original; ao atingir `MaxProcessingAttempts` ela vai para a **DLQ**.
  Ordem importa: publica (com *confirm*) e só então confirma a original ⇒ no pior caso há duplicata (absorvida pela idempotência), nunca perda.
  > **Por que não o `x-delivery-limit` da quorum queue?** Foi a primeira tentativa, e um teste com RabbitMQ 4 mostrou que
  > `nack(requeue:true)` **não** incrementa a contagem usada pelo limite (a mensagem foi entregue 12× sem ir para a DLQ).
  > Sem contagem na aplicação, uma mensagem "venenosa" ficaria em loop infinito.
- **Cache:** invalidado **após o commit**. Um retroativo altera o acumulado de todos os dias seguintes, então a invalidação é total (tag).

## Consequências
(+) Efeito *exactly-once* sobre o saldo; sem locks distribuídos. 
(−) O saldo fica defasado por alguns instantes (outbox + fila). A API documenta a consistência eventual.
(−) `processed_events` cresce; em produção, expurgar por janela de retenção (≥ janela máxima de redelivery).

## Evolução
Job de **reconciliação** recalculando `saldo_diario` a partir dos lançamentos para detectar/corrigir divergência.
