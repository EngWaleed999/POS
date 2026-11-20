import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = (__ENV.BASE_URL || '').replace(/\/+$/, '');
const STAFF_PATH = __ENV.STAFF_PATH || '/api/v1/staff?page=1&pageSize=10';
const READER_TOKEN = __ENV.READER_TOKEN || '';
const FORBIDDEN_TOKEN = __ENV.FORBIDDEN_TOKEN || '';

if (!BASE_URL) throw new Error('BASE_URL is required, e.g. http://127.0.0.1:5080');
if (!READER_TOKEN || !FORBIDDEN_TOKEN) {
  throw new Error('READER_TOKEN and FORBIDDEN_TOKEN are required. Run provision-perf-users.ps1 first.');
}

export const options = {
  discardResponseBodies: true,
  vus: 1,
  iterations: 1,
  thresholds: { checks: ['rate>0.99'] },
};

export default function () {
  const url = BASE_URL + STAFF_PATH;

  const noToken = http.get(url, {
    tags: { endpoint: 'staff_list', auth_case: 'missing_token' },
    timeout: '10s',
  });
  check(noToken, { 'missing token is rejected with 401': (r) => r.status === 401 });

  const invalidToken = http.get(url, {
    headers: { Authorization: 'Bearer invalid.token.value' },
    tags: { endpoint: 'staff_list', auth_case: 'invalid_token' },
    timeout: '10s',
  });
  check(invalidToken, { 'invalid token is rejected with 401': (r) => r.status === 401 });

  const forbidden = http.get(url, {
    headers: { Authorization: 'Bearer ' + FORBIDDEN_TOKEN },
    tags: { endpoint: 'staff_list', auth_case: 'forbidden_user' },
    timeout: '10s',
  });
  check(forbidden, { 'authenticated user without users:read gets 403': (r) => r.status === 403 });

  const allowed = http.get(url, {
    headers: { Authorization: 'Bearer ' + READER_TOKEN },
    tags: { endpoint: 'staff_list', auth_case: 'authorized_user' },
    timeout: '10s',
  });
  check(allowed, { 'user with users:read gets 200': (r) => r.status === 200 });
}
