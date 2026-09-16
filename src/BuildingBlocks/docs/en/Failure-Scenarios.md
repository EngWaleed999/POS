# SuperMarket.BuildingBlocks — Failure Scenarios & Error Handling

> **Scope:** Detailed catalog of failure paths, error detection mechanisms, HTTP status translations, logging behaviors, and recovery policies across the `BuildingBlocks` lifecycle.

---

## 1. Expected Business Failures vs. Unexpected System Failures

The architecture strictly distinguishes between two classes of failures:

```text
                               ┌────────────────────────────────┐
                               │       Incoming Operation       │
                               └───────────────┬────────────────┘
                                               │
                       ┌───────────────────────┴───────────────────────┐
                       ▼                                               ▼
       ┌───────────────────────────────┐               ┌───────────────────────────────┐
       │   Expected Business Failure   │               │   Unexpected System Failure   │
       ├───────────────────────────────┤               ├───────────────────────────────┤
       │ • Validation rule violation   │               │ • Database connection dropped │
       │ • Resource not found (404)    │               │ • Out of memory / StackExplode│
       │ • Concurrency conflict (409)  │               │ • NullReferenceException      │
       │ • Unauthorized access (401)   │               │ • Network timeout             │
       ├───────────────────────────────┤               ├───────────────────────────────┤
       │ Returned via Result.Failure   │               │ Thrown as Exception           │
       │ Logged as Warning             │               │ Logged as Error with Stack    │
       │ RFC 7807 4xx ProblemDetails   │               │ RFC 7807 500 ProblemDetails   │
       └───────────────────────────────┘               └───────────────────────────────┘
```

---

## 2. Comprehensive Failure Matrix

| Scenario | Detection Mechanism | Handling Component | HTTP Response | Logging Level & Details | Recovery & Mitigation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Command / Query Validation Failure** | FluentValidation rules fail inside `ValidationPipelineBehavior` | `ValidationPipelineBehavior` aggregates all errors and short-circuits | `400 Bad Request` with `ErrorType.Validation` ProblemDetails | Logged as `Warning` in `LoggingPipelineBehavior` with `General.Validation` | Caller corrects request payload; handler is never invoked. |
| **Resource Not Found** | Handler queries DB and finds no matching record | Handler returns `Result.Failure(Error.NotFound("Entity.NotFound", "..."))` | `404 Not Found` ProblemDetails | Logged as `Warning` in `LoggingPipelineBehavior` with error code | Caller checks identifier and retries or reports to end-user. |
| **Business State Conflict** | Domain invariant violation (e.g. shift already closed) | Handler returns `Result.Failure(Error.Conflict("Shift.AlreadyClosed", "..."))` | `409 Conflict` ProblemDetails | Logged as `Warning` with entity ID and reason | Client UI refreshes current state to reflect up-to-date data. |
| **Unauthorized Action** | Handler / Policy detects missing or invalid credentials | Handler returns `Result.Failure(Error.Unauthorized("Auth.Invalid", "..."))` | `401 Unauthorized` ProblemDetails | Logged as `Warning` | Client redirects to login / refreshes OAuth token. |
| **Forbidden Permission** | Current user lacks required role/permission | Handler returns `Result.Failure(Error.Forbidden("Auth.Forbidden", "..."))` | `403 Forbidden` ProblemDetails | Logged as `Warning` with actor ID | Client displays permission denied UI. |
| **SLA Latency Violation** | Stopwatch in `PerformancePipelineBehavior` exceeds threshold | `PerformancePipelineBehavior` logs warning, records metric, continues | Depends on handler outcome (200 / 4xx) | Logged as `Warning` ("SLOW REQUEST ALERT: ... exceeded threshold") | Alerts Prometheus / Grafana; developers investigate slow SQL or locks. |
| **Invalid Result State Construction** | Constructor invariant check inside `Result` or `Result<TValue>` | `Result` throws `InvalidOperationException` at construction | Caught by pipeline $\rightarrow$ `500 Internal Server Error` | Logged as `Error` in `LoggingPipelineBehavior` & `GlobalExceptionHandler` | Developer bug fix: ensure success has `Error.None` and failure has non-empty error. |
| **Premature Value Access on Failed Result** | Calling `.Value` when `IsSuccess == false` on `Result<TValue>` | Property getter throws `InvalidOperationException` | Caught by pipeline $\rightarrow$ `500 Internal Server Error` | Logged as `Error` with stack trace | Developer bug fix: check `IsSuccess` or use `Match` / `Bind`. |
| **Database Connection Failure** | EF Core throws `NpgsqlException` / `SqlException` during `SaveChangesAsync` | Exception bubbles through pipeline to `GlobalExceptionHandler` | `500 Internal Server Error` (Masked ProblemDetails with `TraceId`) | Logged as `Error` in `GlobalExceptionHandler` with full stack trace | Transient retry policies (Polly / EF Core execution strategy) or DevOps infrastructure recovery. |
| **Database Concurrency Conflict** | EF Core throws `DbUpdateConcurrencyException` on update | Exception bubbles up or caught by handler | `409 Conflict` or `500 Internal Server Error` | Logged as `Warning` (if caught) or `Error` (if unhandled) | Client fetches latest row version and re-applies changes. |

---

## 3. RFC 7807 Error Response Examples

### Example 1: Validation Failure (400 Bad Request)
Produced by `ValidationPipelineBehavior` $\rightarrow$ `ResultProblemDetailsExtensions`:
```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "PageNumber: Must be greater than 0. | PageSize: Must not exceed 100.",
  "instance": "/api/v1/sales",
  "errorCode": "General.Validation"
}
```

### Example 2: Unhandled Server Crash (500 Internal Server Error)
Produced by `GlobalExceptionHandler`:
```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred while processing your request. Please contact system support with the provided trace ID.",
  "instance": "/api/v1/sales/checkout",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```
*Notice:* The internal stack trace and database credentials are completely hidden from the HTTP response, while the `traceId` enables backend engineers to immediately locate the corresponding stack trace in application logs.
