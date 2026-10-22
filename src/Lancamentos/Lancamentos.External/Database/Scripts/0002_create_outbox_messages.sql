CREATE TABLE IF NOT EXISTS outbox_messages (
    id            uuid          PRIMARY KEY,
    type          varchar(200)  NOT NULL,
    routing_key   varchar(200)  NOT NULL,
    payload       jsonb         NOT NULL,
    occurred_at   timestamptz   NOT NULL,
    processed_at  timestamptz   NULL,
    attempts      int           NOT NULL DEFAULT 0,
    last_error    text          NULL
);

CREATE INDEX IF NOT EXISTS ix_outbox_pendentes
    ON outbox_messages (occurred_at) WHERE processed_at IS NULL;
