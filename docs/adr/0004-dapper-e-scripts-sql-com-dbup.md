# ADR 0004 — Dapper + scripts SQL versionados (DbUp)

**Status:** aceito

## Decisão
- Acesso a dados **somente com Dapper** (SQL parametrizado), dentro dos projetos `*.External` (repositórios e queries).
- O esquema vive em `Database/Scripts/NNNN_descricao.sql`, **embutido no assembly** e aplicado no startup por **DbUp**
  (tabela `schemaversions`; cada script roda uma única vez, em ordem). As migrations rodam com retry enquanto o Postgres sobe.
- Um banco por serviço; o container PostgreSQL cria os dois bancos (`docker/postgres/init`), as tabelas são de responsabilidade de cada serviço.
- Repositórios usam uma `DbSession` por escopo (conexão + transação compartilhadas com o outbox ⇒ atomicidade).
- Concorrência tratada no banco: `SELECT … FOR UPDATE` (estorno e saldo), `INSERT … ON CONFLICT DO NOTHING` (idempotência).

## Alternativas
- **EF Core:** ótimo, mas o desafio pede Dapper, e o SQL explícito deixa as garantias de concorrência visíveis.
- **Scripts rodados manualmente:** não reproduzível; DbUp torna `docker compose up` suficiente.

## Consequências
(+) Controle total do SQL e do plano de execução, sem mágica; migrations idempotentes e auditáveis. 
(−) Mapeamento manual (`record` privados de linha → agregado via `Rehydrate`). Mudança de esquema só para frente (sem *down*).
