# ADR 0001 — Dois serviços independentes (Lançamentos e Consolidação)

**Status:** aceito

## Contexto
O requisito não funcional exige que o controle de lançamentos **continue operando mesmo se a consolidação diária falhar**.
Além disso, os dois lados têm perfis de carga diferentes: escrita transacional (lançamentos) e leitura em pico de 50 req/s (saldo).

## Decisão
Dois serviços executáveis, cada um com **seu próprio banco** (database-per-service) e sem chamadas síncronas entre eles.
A integração é apenas por eventos (ver ADR 0002).

## Alternativas consideradas
| Alternativa | Por que não |
|---|---|
| Monolito modular, um deploy | Uma falha/pico na consolidação derruba ou degrada os lançamentos; o isolamento exigido seria só "de código", não de processo. |
| Consolidação chamando a API de Lançamentos por HTTP | Acopla disponibilidade e latência: Lançamentos fora do ar = consolidação fora do ar. |
| Lançamentos chamando a Consolidação por HTTP | Exatamente o acoplamento que o requisito proíbe. |
| Banco compartilhado, consolidação lendo a tabela de lançamentos | Acopla esquema e carga de leitura ao banco transacional. |

## Consequências
- (+) Falha da Consolidação não afeta o `POST /lancamentos`; os serviços escalam e fazem deploy separadamente.
- (+) Cada banco é dimensionado e evoluído de forma independente.
- (−) Consistência **eventual**: o saldo reflete um lançamento segundos depois (ver ADR 0005). Documentado na API.
- (−) Mais peças operacionais (broker, 2 bancos). Mitigado por `docker compose`.

## Gatilhos para revisitar
Separar o consumer da API de leitura da Consolidação em processos distintos quando precisarmos escalar leitura e escrita
independentemente. Hoje, a 50 req/s, um processo basta.
