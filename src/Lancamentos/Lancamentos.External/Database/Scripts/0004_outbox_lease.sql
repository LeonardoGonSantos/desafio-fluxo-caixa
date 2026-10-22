-- Lease: o OutboxProcessor reserva mensagens por um tempo e publica FORA de transação.
-- Se o processo morrer, o lease expira e outra instância reprocessa (entrega at-least-once).
ALTER TABLE outbox_messages ADD COLUMN IF NOT EXISTS locked_until timestamptz NULL;
