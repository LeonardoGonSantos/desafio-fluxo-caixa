# Diagramas de arquitetura (C4)

## Nível 1 — Contexto

```mermaid
C4Context
    title Fluxo de Caixa — Contexto
    Person(lojista, "Lojista", "Registra créditos/débitos e consulta o saldo diário")
    System(fluxo, "Sistema de Fluxo de Caixa", "Gestão de lançamentos e saldo diário consolidado")
    Rel(lojista, fluxo, "Usa", "HTTPS / REST")
```

## Nível 2 — Containers

```mermaid
flowchart LR
    lojista(["Lojista"])

    subgraph Lancamentos["Serviço de Lançamentos"]
        lapi["Lancamentos.Api<br/>(ASP.NET Core · CQRS · DDD)"]
        outbox["OutboxProcessor<br/>(BackgroundService)"]
        ldb[("PostgreSQL<br/>lancamentos<br/>lancamentos · outbox_messages · idempotency_keys")]
    end

    subgraph Broker["RabbitMQ"]
        ex{{"exchange<br/>fluxocaixa.lancamentos (topic)"}}
        q["fila quorum<br/>consolidacao.lancamentos"]
        dlq["DLQ<br/>consolidacao.lancamentos.dlq"]
    end

    subgraph Consolidacao["Serviço de Consolidação"]
        capi["Consolidacao.Api<br/>(consultas + rate limiter + cache)"]
        consumer["LancamentoRegistradoConsumer<br/>(BackgroundService)"]
        cdb[("PostgreSQL<br/>consolidacao<br/>saldo_diario · processed_events")]
    end

    lojista -->|"POST /lancamentos<br/>Idempotency-Key"| lapi
    lapi -->|"1 transação:<br/>lançamento + outbox"| ldb
    outbox -->|"lê pendentes<br/>SKIP LOCKED"| ldb
    outbox -->|"publica<br/>(confirms + mandatory)"| ex
    ex --> q
    q -->|"at-least-once"| consumer
    q -.->|"poison / delivery-limit"| dlq
    consumer -->|"1 transação:<br/>processed_events + saldo (FOR UPDATE)"| cdb
    lojista -->|"GET /saldos-diarios/{data}"| capi
    capi -->|"leitura"| cdb
```

> **Isolamento:** não existe nenhuma seta entre `Lancamentos.Api` e `Consolidacao.*`. Se a Consolidação ou o broker caírem,
> o `POST` continua gravando; as mensagens esperam no `outbox_messages`.

## Camadas de cada serviço

```mermaid
flowchart TB
    api["Api<br/>Controllers · filtros · ProblemDetails · Swagger"]
    app["Application<br/>Commands/Queries · Handlers · Validators · Decorators"]
    dom["Domain<br/>Agregados · Value Objects · Eventos · Interfaces de repositório"]
    ext["External<br/>Dapper · DbUp (Scripts) · RabbitMQ · Outbox · Cache"]
    bb["BuildingBlocks (Shared Kernel mínimo)"]

    api --> app --> dom --> bb
    api --> ext
    ext -.->|"implementa interfaces de"| app
    ext -.->|"implementa interfaces de"| dom
```
