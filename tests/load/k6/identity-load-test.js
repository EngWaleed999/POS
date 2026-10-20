import http from 'k6/http';
import { check, sleep } from 'k6';

// Performance threshold configuration
export const options = {
  stages: [
    { duration: '10s', target: 5 },   // Ramp-up to 5 concurrent users
    { duration: '30s', target: 10 },  // Sustained load with 10 users
    { duration: '10s', target: 0 },   // Graceful ramp-down
  ],
  thresholds: {
    http_req_duration: ['p(95)<300'], // 95% of requests must complete below 300ms
    http_req_failed: ['rate<0.01'],   // Error rate must be less than 1%
  },
};

const KEYCLOAK_URL = __ENV.KEYCLOAK_URL || 'http://keycloak:8080';
const REALM = __ENV.REALM || 'supermarket';

export default function () {
  // 1. Verify Keycloak OpenID configuration discovery endpoint
  const openIdUrl = `${KEYCLOAK_URL}/realms/${REALM}/.well-known/openid-configuration`;
  const openIdRes = http.get(openIdUrl);

  check(openIdRes, {
    'Keycloak OpenID Config reachable (status 200)': (r) => r.status === 200,
    'Issuer matches realm': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.issuer && body.issuer.includes(REALM);
      } catch (e) {
        return false;
      }
    },
  });

  sleep(1);
}
