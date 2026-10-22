CREATE TABLE IF NOT EXISTS saldo_diario (
    data            date           PRIMARY KEY,
    total_creditos  numeric(18,2)  NOT NULL DEFAULT 0 CHECK (total_creditos >= 0),
    total_debitos   numeric(18,2)  NOT NULL DEFAULT 0 CHECK (total_debitos  >= 0),
    atualizado_em   timestamptz    NOT NULL
);
