# SuperMarket POS — Performance Engineering & Bottleneck Analysis Report

> Status: awaiting measured evidence. Do not fill performance values until raw k6 and host/service measurements are available.

## 1. Executive Summary
- Benchmark date:
- Tested commit SHA:
- Build/runtime mode:
- Overall findings:
- Primary bottleneck:
- Before/After improvement:

## 2. Scope and Workloads
| Scenario | Endpoint | Purpose | Data set |
|---|---|---|---|
| Authorization smoke | GET /api/v1/staff | Functional security gate | Reader + Cashier |
| Staff read | GET /api/v1/staff | JWT, authorization, Redis, PostgreSQL | |
| Cashier login | POST /api/v1/auth/cashier-login | PIN verification, DB, Keycloak | |
| Staff registration | POST /api/v1/staff | Cross-system provisioning | |

## 3. Environment
| Component | Version/configuration |
|---|---|
| CPU / physical cores / logical processors | |
| RAM / WSL allocation / swap | |
| OS / Windows build | |
| .NET runtime / GC settings | |
| PostgreSQL | |
| Keycloak / JVM | |
| Redis | |
| k6 version / generator location | |
| Docker resource limits | |
| API hosting mode and URL | |

State clearly whether the generator and services shared one physical host.

## 4. Methodology
- Warm-up:
- Arrival-rate stages:
- Pre-allocated VUs / max VUs:
- Runs per scenario:
- Dataset size and account distribution:
- Cache state (warm/cold):
- Error handling and stopping criteria:
- Variables held constant between Before and After:

## 5. Results
| Scenario / Build | Actual RPS | p50 ms | p90 ms | p95 ms | p99 ms | Error % | Dropped iterations |
|---|---:|---:|---:|---:|---:|---:|---:|
| Staff read / Before | | | | | | | |
| Staff read / After | | | | | | | |
| Cashier login / Before | | | | | | | |
| Cashier login / After | | | | | | | |
| Staff registration / Before | | | | | | | |
| Staff registration / After | | | | | | | |

## 6. Resource Utilization
| Component | CPU peak/avg | Memory peak/avg | Connections/threads | Notes |
|---|---:|---:|---:|---|
| Identity API | | | | |
| PostgreSQL | | | | |
| Keycloak | | | | |
| Redis | | | | |
| k6 generator | | | | |
| WSL/Docker host | | | | |

## 7. Trace and Database Analysis
- Representative slow trace and Trace ID:
- ASP.NET Core/Kestrel evidence:
- Redis hit/miss and command latency:
- PostgreSQL active/idle connections:
- Slow SQL and EXPLAIN (ANALYZE, BUFFERS):
- Keycloak token endpoint latency/errors:

## 8. Bottleneck Findings
For each finding, record:
1. Observed symptom and metric.
2. Trace/query/resource evidence.
3. Root cause.
4. Proposed change.
5. Risk and trade-offs.
6. Validation plan.

## 9. Before vs After
- Commit SHAs:
- Runtime/configuration parity:
- Repeated-run median and spread:
- Absolute and relative change:
- Regressions:
- Measurement limitations:

## 10. Architecture Decisions
Explain whether Outbox, RabbitMQ, Hangfire, caching changes, indexes, connection-pool tuning, or other mechanisms are justified by measured evidence. Do not recommend components without a demonstrated problem.

## 11. Conclusion and Next Actions
- Confirmed bottlenecks:
- Changes validated:
- Remaining risks:
- Next experiments:

## 12. Evidence Appendix
List raw summary, console, metadata, timeseries, docker stats, Grafana screenshots, Jaeger traces, SQL plans, and commit SHAs. Remove all credentials and personal data before sharing.
