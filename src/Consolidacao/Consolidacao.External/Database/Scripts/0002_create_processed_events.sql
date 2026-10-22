-- Idempotência do consumidor: um evento só altera o saldo uma vez, mesmo com entrega duplicada.
CREATE TABLE IF NOT EXISTS processed_events (
    event_id      uuid         PRIMARY KEY,
    processed_at  timestamptz  NOT NULL
);
