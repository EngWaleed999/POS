import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';

const BASE_URL = (__ENV.BASE_URL || '').replace(/\/+$/, '');
const TOKEN = __ENV.CREATE_STAFF_TOKEN || '';
const ROLE_ID = __ENV.ROLE_ID || '';
const BRANCH_ID = __ENV.BRANCH_ID || '';
const INITIAL_PASSWORD = __ENV.TEST_INITIAL_PASSWORD || '';
const RUN_ID = __ENV.RUN_ID || '';

if (!BASE_URL || !TOKEN || !ROLE_ID || !INITIAL_PASSWORD || !/^\d{4,6}$/.test(RUN_ID)) {
  throw new Error('Required: BASE_URL, CREATE_STAFF_TOKEN, ROLE_ID, TEST_INITIAL_PASSWORD and numeric RUN_ID (4-6 digits)');
}

export const options = {
  scenarios: {
    staff_registration_probe: {
      executor: 'constant-arrival-rate',
      rate: 1,
      timeUnit: '10s',
      duration: '60s', // Approximately six new test users. This is intentionally conservative.
      preAllocatedVUs: 2,
      maxVUs: 5,
      gracefulStop: '30s',
    },
  },
  thresholds: {
    'http_req_failed{endpoint:staff_register}': ['rate<0.01'],
    checks: ['rate>0.99'],
    dropped_iterations: ['count==0'],
  },
};

export default function () {
  const sequence = String(exec.scenario.iterationInTest + 1).padStart(4, '0');
  const username = `perf_${RUN_ID}_${sequence}`;
  const email = `perf.${RUN_ID}.${sequence}@example.com`;
  const phoneNumber = `+9677${RUN_ID}${sequence}`;

  const payload = {
    username,
    fullName: `Performance Test ${sequence}`,
    phoneNumber,
    email,
    roleId: ROLE_ID,
    branchId: BRANCH_ID || null,
    initialPassword: INITIAL_PASSWORD,
  };

  const response = http.post(`${BASE_URL}/api/v1/staff`, JSON.stringify(payload), {
    headers: {
      Authorization: `Bearer ${TOKEN}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    tags: { endpoint: 'staff_register' },
    timeout: '45s',
  });

  check(response, {
    'staff registration returns 201': (r) => r.status === 201,
  });
}
