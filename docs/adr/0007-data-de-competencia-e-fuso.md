# ADR 0007 — Data de competência, fuso e saldo diário

**Status:** aceito

O enunciado diz "saldo diário consolidado" sem definir alguns pontos. Decisões:

| Ponto | Decisão |
|---|---|
| Qual data define o dia? | **Data de competência** (`DateOnly`) informada no lançamento — regime de competência. `criado_em` (UTC) é só auditoria. |
| Fuso | O "hoje" do negócio é `America/Sao_Paulo`. Persistência de instantes em UTC (`timestamptz`). |
| Data futura | Rejeitada no lançamento (e na consulta), para não misturar saldo realizado com projetado. |
| Data retroativa | Permitida; o saldo acumulado é calculado na leitura e se ajusta sozinho. |
| "Saldo diário" | A API devolve **os dois**: `saldoDoDia` (créditos − débitos do dia) e `saldoAcumulado` (soma até a data, inclusive). |
| Dia sem movimento | `200 OK` com totais zerados e acumulado herdado. O relatório existe para qualquer data passada. |
| Valores | `decimal` / `numeric(18,2)`; nunca `double`. Positivo, até 2 casas. O tipo (Crédito/Débito) dá o sinal. |
| Saldo negativo | Permitido (fluxo de caixa pode ficar negativo). |
