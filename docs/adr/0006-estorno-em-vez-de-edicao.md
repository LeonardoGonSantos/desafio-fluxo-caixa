# ADR 0006 — Estorno em vez de edição/exclusão

**Status:** aceito

## Decisão
Lançamentos são **imutáveis**. Corrigir um erro é criar um **estorno**: um novo lançamento de tipo inverso, mesmo valor,
vinculado ao original (`lancamento_original_id`), e marcar o original como `Estornado`.

## Por quê
- Trilha de auditoria íntegra — requisito natural em contexto financeiro/contábil.
- Para a Consolidação, o estorno é só **mais um `LancamentoRegistrado`**: não existe evento de "edição" ou "exclusão" nem lógica de reversão no consumidor.
- Evita reprocessar saldos já consolidados.

## Regras (no agregado `Lancamento`)
- Um lançamento só pode ser estornado uma vez; um estorno não pode ser estornado.
- Motivo é obrigatório. A data de competência do estorno é **hoje** (o mês de origem permanece intacto).
- Defesa em profundidade: `SELECT … FOR UPDATE` no handler **e** índice único parcial `ux_lancamentos_estorno_unico` no banco
  (testado com 6 estornos concorrentes: exatamente 1 sucesso, 5 conflitos).

## Evento de auditoria (`LancamentoEstornado`)
O estorno marca o original como `Estornado` e dispara um `LancamentoEstornadoDomainEvent` **somente para auditoria** (registrado em log
pelo handler). Ele **não é publicado no broker**: a Consolidação já recebe o estorno como um `LancamentoRegistrado` (lançamento inverso).
Publicá-lo agora seria uma armadilha — como a publicação usa `mandatory` e nenhuma fila está vinculada a essa routing key, a mensagem
ficaria para sempre pendente no outbox. Publicar exige antes um consumidor (ou um *alternate exchange*).

O `IntegrationEventMapper` trata cada evento de domínio de forma explícita e **lança exceção** para um evento sem decisão,
em vez de ignorá-lo em silêncio. O motivo do estorno acima do limite é rejeitado (`lancamento.motivo_longo`), nunca truncado.
