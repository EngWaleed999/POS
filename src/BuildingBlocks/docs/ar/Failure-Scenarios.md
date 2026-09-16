# سيناريوهات الفشل وإدارة الأخطاء (Failure Scenarios)

> **النطاق:** تصنيف تفصيلي لكافة مسارات الفشل، آليات الاكتشاف، ترجمة أكواد HTTP، سلوك السجلات، وسياسات التعافي عبر دورة حياة `SuperMarket.BuildingBlocks`.

---

## 1. التمييز بين أخطاء البزنس المتوقعة والانهيارات التقنية غير المتوقعة

تعتمد المعمارية فصلاً صارماً بين نوعين من الأخطاء:

```text
                               ┌────────────────────────────────┐
                               │       Incoming Operation       │
                               └───────────────┬────────────────┘
                                               │
                       ┌───────────────────────┴───────────────────────┐
                       ▼                                               ▼
       ┌───────────────────────────────┐               ┌───────────────────────────────┐
       │   Expected Business Failure   │               │   Unexpected System Failure   │
       │     (أخطاء بزنس متوقعة)       │               │    (انهيارات تقنية مفاجئة)    │
       ├───────────────────────────────┤               ├───────────────────────────────┤
       │ • مخالفة شروط التحقق          │               │ • انقطاع الاتصال بقاعدة الـ DB│
       │ • عنصر غير موجود (404)        │               │ • نفاد الذاكرة (Out of Memory)│
       │ • تعارض في الحالة (409)       │               │ • NullReferenceException      │
       │ • انعدام الصلاحية (401/403)   │               │ • انتهاء وقت الشبكة (Timeout) │
       ├───────────────────────────────┤               ├───────────────────────────────┤
       │ تُرجع عبر Result.Failure      │               │ تُرمى كـ Exception            │
       │ تُسجل كتحذير Warning          │               │ تُسجل كـ Error مع الـ Stack   │
       │ استجابة RFC 7807 (4xx)        │               │ استجابة RFC 7807 (500) آمنة   │
       └───────────────────────────────┘               └───────────────────────────────┘
```

---

## 2. مصفوفة سيناريوهات الفشل الشاملة (Failure Matrix)

| سيناريو الفشل | آلية الاكتشاف | المكون المعالج | استجابة الـ HTTP | مستوى وتفاصيل السجلات | سياسة التعافي والحل |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **فشل التحقق من صحة المدخلات** | مخالفة قواعد FluentValidation داخل الـ Pipeline | `ValidationPipelineBehavior` يجمع الأخطاء ويوقف المسار | `400 Bad Request` مع ProblemDetails بنوع `Validation` | يسجل `Warning` في `LoggingPipelineBehavior` مع كود `General.Validation` | العميل يصحح البيانات المرسلة؛ لا يتم تشغيل الـ Handler. |
| **العنصر غير موجود** | الـ Handler يستعلم عن السجل ولا يجده | الـ Handler يُرجع `Result.Failure(Error.NotFound(...))` | `404 Not Found` ProblemDetails | يسجل `Warning` مع ذكر كود ووصف الخطأ | العميل يتأكد من المعرف ويبلغ المستخدم. |
| **تعارض في حالة البزنس** | مخالفة شرط عمل (مثل: محاولة إغلاق وردية مغلقة بالفعل) | الـ Handler يُرجع `Result.Failure(Error.Conflict(...))` | `409 Conflict` ProblemDetails | يسجل `Warning` مع معرف الكيان وسبب التعارض | واجهة العميل تحدث الحالة اللحظية لتعكس الواقع. |
| **محاولة وصول غير مصادق عليها** | اكتشاف عدم وجود أو فساد توكن المستخدم | الـ Handler أو السياسة ترجع `Error.Unauthorized(...)` | `401 Unauthorized` ProblemDetails | يسجل `Warning` | العميل يعيد التوجيه لشاشة تسجيل الدخول وتجديد التوكن. |
| **حظر الصلاحيات (Forbidden)** | المستخدم مصادق عليه لكنه يفتقر للدور المطلوب | الـ Handler يرجع `Error.Forbidden(...)` | `403 Forbidden` ProblemDetails | يسجل `Warning` مع معرف المستخدم والعملية | الواجهة تعرض رسالة رفض الوصول وتمنع الزر. |
| **تجاوز عتبة زمن الاستجابة (SLA)** | ساعة توقيف `PerformancePipelineBehavior` تتجاوز العتبة | `PerformancePipelineBehavior` يسجل التحذير والمقاييس ويكمل | بحسب مخرجات العملية (200 أو 4xx) | يسجل `Warning` ("SLOW REQUEST ALERT: ... exceeded threshold") | إطلاق تنبيه في Prometheus/Grafana؛ فحص فهارس SQL وبطء الشبكة. |
| **إنشاء حالة Result غير صالحة** | فحص شروط الـ Constructor في `Result` | الـ Constructor يرمي `InvalidOperationException` فوراً | يلتقطه الـ Handler المركزي $\rightarrow$ `500 Server Error` | يسجل `Error` في `LoggingPipelineBehavior` و `GlobalExceptionHandler` | إصلاح خطأ برمجي: التأكد من عدم إرسال خطأ مع نجاح أو العكس. |
| **محاولة قراءة Value من نتيجة فاشلة** | استدعاء `.Value` عندما تكون `IsSuccess == false` | الخاصية ترمي `InvalidOperationException` | يلتقطه الـ Handler المركزي $\rightarrow$ `500 Server Error` | يسجل `Error` مع الـ StackTrace كاملاً | إصلاح خطأ برمجي: فحص `IsSuccess` أو استخدام دوال `Match` / `Bind`. |
| **انهيار الاتصال بقاعدة البيانات** | EF Core يرمي `NpgsqlException` أثناء `SaveChangesAsync` | الاستثناء يقفز عبر الـ Pipeline إلى `GlobalExceptionHandler` | `500 Server Error` (استجابة ProblemDetails معقمة بـ `TraceId`) | يسجل `Error` في `GlobalExceptionHandler` مع الـ StackTrace كاملاً | سياسات إعادة المحاولة اللحظية (Retry Execution Strategy) وتدخل DevOps. |
| **تعارض التعديل المتزامن (Concurrency)** | EF Core يرمي `DbUpdateConcurrencyException` | يلتقطه الـ Handler أو يقفز للـ Exception Handler | `409 Conflict` أو `500 Internal Server Error` | يسجل `Warning` إذا عولج، أو `Error` إذا قفز | جلب أحدث إصدار من السجل وإعادة تطبيق التعديلات. |

---

## 3. نماذج الاستجابات القياسية (RFC 7807 Problem Details)

### مثال 1: استجابة خطأ تحقق من المدخلات (400 Bad Request)
صادرة عن `ValidationPipelineBehavior` $\rightarrow$ `ResultProblemDetailsExtensions`:
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

### مثال 2: استجابة انهيار غير متوقع للسيرفر (500 Internal Server Error)
صادرة عن `GlobalExceptionHandler`:
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
*ملاحظة أمنية وهندسية:* يتم إخفاء تفاصيل كود C# وسلسلة الـ StackTrace وبيانات خادم قواعد البيانات تماماً عن العميل الخارجي لمنع ثغرات تسريب المعلومات، بينما يتم تزويده بمعرف تتبع موحد (`traceId`) يتيح لمهندسي الباك إند الرجوع المباشر للسجلات المركزية وفحص سبب الانهيار في ثوانٍ.
