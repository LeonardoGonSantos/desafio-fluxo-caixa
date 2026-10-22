// Teste de carga do requisito não funcional:
//   "o serviço de consolidação recebe 50 requisições por segundo em picos, com no máximo 5% de perda".
//
// Cenário `leitura`: 50 req/s constantes em GET /api/v1/saldos-diarios/{data}.
// Cenário `escrita`: lançamentos entrando em paralelo (20 req/s) no serviço de Lançamentos, para que a
//                    consolidação leia ENQUANTO o consumer também escreve (pior caso realista).
//
// Execução (com a stack no ar):  docker compose --profile load run --rm k6
import http from 'k6/http';
import { check } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const CONSOLIDACAO = __ENV.BASE_URL || 'http://localhost:5002';
const LANCAMENTOS = __ENV.LANCAMENTOS_URL || 'http://localhost:5001';
const DURATION = __ENV.DURATION || '2m';
const RATE = Number(__ENV.RATE || 50);

export const options = {
  scenarios: {
    leitura: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 50,
      maxVUs: 200,
      exec: 'leitura',
    },
    escrita: {
      executor: 'constant-arrival-rate',
      rate: 20,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 20,
      maxVUs: 100,
      exec: 'escrita',
    },
  },
  thresholds: {
    // Perda máxima tolerada: 5%.
    'http_req_failed{scenario:leitura}': ['rate<0.05'],
    'http_req_duration{scenario:leitura}': ['p(95)<200', 'p(99)<500'],
    'http_req_failed{scenario:escrita}': ['rate<0.05'],
  },
};

function diaAleatorio() {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - 1 - Math.floor(Math.random() * 30));
  return d.toISOString().slice(0, 10);
}

export function leitura() {
  const res = http.get(`${CONSOLIDACAO}/api/v1/saldos-diarios/${diaAleatorio()}`, {
    tags: { name: 'GET saldo-diario' },
  });
  check(res, { 'status 200': (r) => r.status === 200 });
}

export function escrita() {
  const body = JSON.stringify({
    tipo: Math.random() < 0.6 ? 'Credito' : 'Debito',
    // toFixed evita dízimas de ponto flutuante (o domínio rejeita mais de 2 casas decimais).
    valor: Number((Math.random() * 1000 + 1).toFixed(2)),
    descricao: 'Carga k6',
    dataCompetencia: diaAleatorio(),
  });
  const res = http.post(`${LANCAMENTOS}/api/v1/lancamentos`, body, {
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': uuidv4() },
    tags: { name: 'POST lancamento' },
  });
  check(res, { 'status 201': (r) => r.status === 201 });
}
