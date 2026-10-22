-- Um banco por serviço (database-per-service). As tabelas são criadas pelos scripts
-- de cada serviço (Database/Scripts), aplicados via DbUp no startup.
CREATE DATABASE lancamentos;
CREATE DATABASE consolidacao;
