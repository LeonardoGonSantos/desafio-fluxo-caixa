CREATE TABLE IF NOT EXISTS idempotency_keys (
    key              varchar(100)  PRIMARY KEY,
    request_hash     varchar(64)   NOT NULL,
    status           smallint      NOT NULL CHECK (status IN (0, 1)), -- 0 = em andamento, 1 = concluída
    response_status  int           NULL,
    response_body    text          NULL,
    response_location text         NULL,
    created_at       timestamptz   NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_idempotency_keys_created_at ON idempotency_keys (created_at);
