import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { SharedArray } from 'k6/data';

const BASE_URL = (__ENV.BASE_URL || '').replace(/\/+$/, '');
const STAFF_PATH = __ENV.STAFF_PATH || '/api/v1/staff?page=1&pageSize=20';
const PREALLOCATED_VUS = Number(__ENV.PREALLOCATED_VUS || 15);
const MAX_VUS = Number(__ENV.MAX_VUS || 60);

const TOKEN_POOL = new SharedArray('staff-read-token-pool', () => {
  const records = JSON.parse(open('./data/staff-tokens.json'));
  if (!Array.isArray(records) || records.length === 0) {
    throw new Error('data/staff-tokens.json must be a non-empty JSON array. Run provision-perf-users.ps1 first.');
  }
  return records;
});

if (!BASE_URL) throw new Error('BASE_URL is required, e.g. http://127.0.0.1:5080');
if (!Number.isInteger(PREALLOCATED_VUS) || PREALLOCATED_VUS < 1 ||
    !Number.isInteger(MAX_VUS) || MAX_VUS < PREALLOCATED_VUS) {
  throw new Error('Set valid PREALLOCATED_VUS and MAX_VUS (MAX_VUS >= PREALLOCATED_VUS)');
}

export const options = {
  discardResponseBodies: true,
  scenarios: {
    staff_read_load: {
      executor: 'ramping-arrival-rate',
      startRate: 1,
      timeUnit: '1s',
      preAllocatedVUs: PREALLOCATED_VUS,
      maxVUs: MAX_VUS,
      stages: [
        { target: 5, duration: '1m' },
        { target: 10, duration: '1m' },
        { target: 20, duration: '1m' },
        { target: 30, duration: '1m' },
        { target: 0, duration: '30s' },
      ],
      gracefulStop: '30s',
    },
  },
  thresholds: {
    'http_req_failed{endpoint:staff_list}': ['rate<0.01'],
    checks: ['rate>0.99'],
    dropped_iterations: ['count==0'],
  },
};

export default function () {
  const record = TOKEN_POOL[(exec.vu.idInTest - 1) % TOKEN_POOL.length];
  const token = typeof record === 'string' ? record : record.token;
  if (!token || String(token).includes('REPLACE_WITH')) {
    throw new Error('Replace token placeholders in data/staff-tokens.json or rerun provision-perf-users.ps1.');
  }

  const response = http.get(BASE_URL + STAFF_PATH, {
    headers: { Authorization: 'Bearer ' + token, Accept: 'application/json' },
    tags: { endpoint: 'staff_list' },
    timeout: '15s',
  });
  check(response, { 'GET staff list returns 200': (r) => r.status === 200 });
}
