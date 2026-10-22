-- Índice para a paginação por cursor (keyset) da listagem: mesma ordem do ORDER BY.
CREATE INDEX IF NOT EXISTS ix_lancamentos_keyset ON lancamentos (data_competencia DESC, criado_em DESC, id DESC);
DROP INDEX IF EXISTS ix_lancamentos_data_competencia;
