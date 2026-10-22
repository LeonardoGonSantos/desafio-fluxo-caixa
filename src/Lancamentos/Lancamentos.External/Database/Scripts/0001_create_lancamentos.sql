CREATE TABLE IF NOT EXISTS lancamentos (
    id                     uuid           PRIMARY KEY,
    tipo                   smallint       NOT NULL CHECK (tipo IN (1, 2)),
    valor                  numeric(18,2)  NOT NULL CHECK (valor > 0),
    descricao              varchar(200)   NOT NULL,
    data_competencia       date           NOT NULL,
    status                 smallint       NOT NULL CHECK (status IN (1, 2)),
    lancamento_original_id uuid           NULL REFERENCES lancamentos (id),
    criado_em              timestamptz    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_lancamentos_data_competencia ON lancamentos (data_competencia, criado_em);

-- Segunda linha de defesa contra estorno duplo em concorrência.
CREATE UNIQUE INDEX IF NOT EXISTS ux_lancamentos_estorno_unico
    ON lancamentos (lancamento_original_id) WHERE lancamento_original_id IS NOT NULL;
