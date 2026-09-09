# 📊 المراقبة والتتبع (Observability & Monitoring)

تعتمد خدمة `SuperMarket.Identity` على أركان المراقبة الحديثة الثلاثة (**Logs, Traces, Metrics**) لضمان رؤية تشغيلية كاملة لحركة الكاشيرية والفروع وأجهزة الـ POS:

---

## 📝 1. السجلات الهيكلية (Structured Logging via Serilog)

يتم تسجيل كافة الأحداث بصيغة **JSON مهيكلة** متوافقة مع أنظمة التحليل المركزية:

### بيانات الإثراء التلقائية لنقاط البيع (POS Enriched Properties):
* **`CorrelationId`:** معرّف فريد يتتبع الطلب عبر كافة الخدمات من الـ Gateway وحتى الـ Database.
* **`BranchId`:** كود أو معرف الفرع الذي تمت منه العملية.
* **`RegisterNumber`:** رقم جهاز الكاشير (مثل `POS-01-REG-02`).
* **`CashierId` (sub):** معرّف الكاشير المنفذ للعملية.
* **`SupervisorId`:** معرّف المشرف في حال كانت العملية تتطلب موافقة استثنائية (Override).

```json
{
  "Timestamp": "2026-09-09T10:15:30.123Z",
  "Level": "Information",
  "MessageTemplate": "Cashier {CashierCode} logged in successfully on register {RegisterNumber} at branch {BranchCode}",
  "Properties": {
    "CashierCode": "EMP-1042",
    "RegisterNumber": "POS-01-REG-01",
    "BranchCode": "BR-01",
    "CorrelationId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "SourceContext": "SuperMarket.Identity.Application.Features.Staff.Commands.AuthenticateCashierPinCommandHandler"
  }
}
```

---

## 🔍 2. التتبع الموزع (Distributed Tracing via OpenTelemetry & Jaeger)

يتم تتبع كل استدعاء يمر عبر الخدمة وتسجيل الـ Spans في **Jaeger**:
* **HTTP Span:** مدة استقبال وفك تشفير الـ JWT.
* **Database Span:** مدة تنفيذ استعلامات PostgreSQL عبر EF Core (جلب بيانات الموظف أو الفرع).
* **Messaging Span:** مدة كتابة ونشر الرسائل في Outbox و RabbitMQ.

```
[Jaeger Trace: Cashier Fast Login]
├── [YARP Gateway] ────────────────────────────────── (8ms)
│   └── [Identity.API: POST /api/staff/login-pin] ── (16ms)
│       ├── [POS Register Hardware Check] ────────── (1.2ms)
│       ├── [Argon2id PIN Hash Verify] ───────────── (6.5ms)
│       ├── [Postgres: SELECT Staff & Roles] ─────── (2.8ms)
│       └── [Keycloak Token Issuance] ────────────── (4.1ms)
```

---

## 📈 3. مقاييس الأداء (Prometheus Metrics)

توفر الخدمة نقطة `/metrics` لتجميع المقاييس ومراقبتها في **Prometheus & Grafana**:

| اسم المقياس (Metric Name) | النوع (Type) | الوصف الهندسي في بيئة السوبرماركت |
|---|---|---|
| `pos_cashier_logins_total` | Counter | إجمالي عدد عمليات تسجيل دخول الكاشيرية بنجاح. |
| `pos_supervisor_overrides_total` | Counter | عدد الموافقات الاستثنائية التي منحها المشرفون (Void/Override). |
| `pos_unauthorized_terminal_attempts_total` | Counter | عدد محاولات الاتصال من أجهزة POS غير مصرحة أو معطلة. |
| `pos_token_validation_duration_seconds` | Histogram | زمن استغراق التحقق من الـ Tokens (p50, p95, p99). |
| `pos_db_query_duration_seconds` | Histogram | زمن استجابة استعلامات قاعدة بيانات `identity_db`. |
| `pos_outbox_pending_messages_count` | Gauge | عدد رسائل التكامل المعلقة في جدول الـ Outbox. |

---

## 💓 4. فحوصات الصحة (Health Checks)

تطبيق المعيار السحابي عبر نقطتين منفصلتين باستخدام حزم `AspNetCore.HealthChecks`:

1. **`/health/live` (Liveness Probe):**
   * يفحص فقط أن تطبيق .NET حي في الذاكرة.
   * لا يفحص أي تبعيات خارجية (لمنع الـ Cascading Restarts).
2. **`/health/ready` (Readiness Probe):**
   * يفحص جاهزية الاتصال بـ:
     * قاعدة بيانات **PostgreSQL** (`identity_db`).
     * وسيط الرسائل **RabbitMQ**.
     * خادم التخزين المؤقت **Redis**.
   * في حال فشل أي منها، يتم إخراج الحاوية من الـ Load Balancer مؤقتاً.
