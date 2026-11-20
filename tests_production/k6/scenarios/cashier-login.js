import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { SharedArray } from 'k6/data';

const BASE_URL = (__ENV.BASE_URL || '').replace(/\/+$/, '');
const CASHIERS = new SharedArray('cashier-test-accounts', () => {
  const records = JSON.parse(open('./data/cashiers.json'));
  if (!Array.isArray(records) || records.length === 0) {
    throw new Error('data/cashiers.json must be a non-empty JSON array. Run provision-perf-users.ps1 first.');
  }
  return records;
});

if (!BASE_URL) throw new Error('BASE_URL is required, e.g. http://127.0.0.1:5080');

export const options = {
  discardResponseBodies: true,
  scenarios: {
    cashier_login_load: {
      executor: 'ramping-arrival-rate',
      startRate: 1,
      timeUnit: '1s',
      preAllocatedVUs: 10,
      maxVUs: 20,
      stages: [
        { target: 1, duration: '30s' },
        { target: 2, duration: '1m' },
        { target: 3, duration: '1m' },
        { target: 5, duration: '1m' },
        { target: 0, duration: '30s' },
      ],
      gracefulStop: '30s',
    },
  },
  thresholds: {
    'http_req_failed{endpoint:cashier_login}': ['rate<0.01'],
    checks: ['rate>0.99'],
    dropped_iterations: ['count==0'],
  },
};

export default function () {
  const cashier = CASHIERS[(exec.vu.idInTest - 1) % CASHIERS.length];
  if (!cashier.username || !cashier.pin || !cashier.registerId ||
      String(cashier.pin).includes('REPLACE_WITH') ||
      String(cashier.registerId).includes('REPLACE_WITH')) {
    throw new Error('Complete data/cashiers.json or rerun provision-perf-users.ps1.');
  }

  const response = http.post(
    BASE_URL + '/api/v1/auth/cashier-login',
    JSON.stringify({
      username: cashier.username,
      pin: String(cashier.pin),
      registerId: cashier.registerId,
    }),
    {
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      tags: { endpoint: 'cashier_login' },
      timeout: '30s',
    }
  );

  check(response, {
    'cashier login returns 200': (r) => r.status === 200,
    'cashier login returns JSON': (r) => (r.headers['Content-Type'] || '').includes('application/json'),
  });
}
