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
