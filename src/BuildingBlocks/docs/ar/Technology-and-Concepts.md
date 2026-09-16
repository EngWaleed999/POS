# التقنيات والمفاهيم البرمجية في SuperMarket.BuildingBlocks

> **النطاق:** توثيق شامل لكافة المفاهيم الهندسية، أنماط التصميم المؤسسية، والتقنيات المطبقة برمجياً في `SuperMarket.BuildingBlocks`.

---

## 1. نمط النتائج الصريحة ونمذجة الأخطاء (Result Pattern)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Results/Result.cs`، `Results/ResultT.cs`، `Results/Error.cs`، `Results/ErrorType.cs`
* **ماهيته:** تغليف نتائج العمليات البرمجية داخل كائن ثابت غير قابل للتعديل (`Result` أو `Result<TValue>`) يحمل حالة النجاح وبيانات النتيجة، أو حالة الفشل مع كائن `Error` ونوع الخطأ `ErrorType`.
* **لماذا تم استخدامه؟** لتفادي استخدام الاستثناءات (Exceptions) في التعبير عن أخطاء البزنس المتوقعة (مثل: باركود غير موجود، رصيد غير كافٍ، وردية كاشير مغلقة). رمي الاستثناءات في .NET مكلف جداً للأداء بسبب بناء الـ Stack Trace وإرهاق مجمع القمامة (GC)، كما أنه يخفي مسارات الفشل عن الـ Type System.
* **كيف يعمل في الكود؟**
  - يعتمد `Result` على Constructor من نوع `protected internal` يفرض قواعد صارمة (Invariants): لا نجاح مع خطأ، ولا فشل مع `Error.None`.
  - يوفر مصانع آمنة للإنشاء (`Result.Success()`، `Result.Failure(...)`، `Result.Create(...)`).
  - يحمي `Result<TValue>` خاصية `Value`؛ بحيث يؤدي استدعاؤها في حالة الفشل إلى رمي `InvalidOperationException` صريح يمنع أخطاء الـ Null Reference.
* **المقايضات (Trade-offs):**
  - *المكاسب:* وضوح تام في عقود الدوال، أداء عالي بدون تكلفة الـ Stack Trace، إجبار المطور عبر المترجم (Compiler) على فحص النجاح والفشل، وتكامل فوري مع معايير HTTP.
  - *التكلفة:* ضرورة فحص `IsSuccess` أو استخدام دوال السلسلة الوظيفية بدلاً من ترك الخطأ يقفز تلقائياً.
* **المفاهيم المرتبطة:** Railway-Oriented Programming, Monads, Null Object Pattern.

---

## 2. البرمجة الوظيفية وسلسلة العمليات (Railway-Oriented Programming - ROP)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Results/ResultExtensions.cs`
* **ماهيته:** توفير دوال توسعة وظيفية (`Match`، `Ensure`، `Map`، `Bind`) لتركيب مسارات العمليات المتسلسلة كمسارين متوازيين: مسار أخضر للنجاح ومسار أحمر للفشل.
* **لماذا تم استخدامه؟** لتبسيط الأكواد المعقدة والتخلص من تكرار فحص `if (!result.IsSuccess)` المتداخل، وضمان معالجة متكاملة لفرعي النجاح والفشل عند مخرجات الـ API.
* **كيف يعمل في الكود؟**
  - `Match`: يجبر المستدعي على تمرير دالتين (`onSuccess` و `onFailure`) لإرجاع نوع موحد `TOutput`، مما يضمن عدم نسيان الخطأ.
  - `Ensure`: يتحقق من شرط إضافي على القيمة داخل النتيجة؛ إذا خالف الشرط يتحول المسار إلى فشل بالخطأ المحدد.
  - `Map`: يحول نوع البيانات داخل النتيجة الناجحة من شكل لآخر دون الحاجة لفحص يدوي.
  - `Bind`: يربط عملية بعملية لاحقة ترجع هي أيضاً `Result`، ويتوقف فوراً (Short-Circuit) إذا فشلت الأولى.
* **المقايضات:**
  - *المكاسب:* كود خطي شديد المقروئية، صياغة إعلانية لقواعد البزنس، وسهولة تامة في إخراج استجابات الـ Minimal APIs والـ Controllers.
  - *التكلفة:* حجز إضافي طفيف للذاكرة نتيجة تمرير الـ Delegates.
* **المفاهيم المرتبطة:** Monadic Binding, Functional Programming in C#.

---

## 3. أساسيات الـ Domain-Driven Design (DDD Primitives)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Domain/Entity.cs`، `Domain/AggregateRoot.cs`، `Domain/ValueObject.cs`، `Domain/DomainEvent.cs`
* **ماهيته:** توفير اللبنات المعمارية المجردة لنمذجة منطق البزنس المعقد.
* **لماذا تم استخدامه؟** لتوحيد سلوك مقارنة هوية الكيانات، وحماية حدود الاتساق المنطقي للتجميعات (Aggregates)، وضمان مساواة كائنات القيمة بهيكليتها وليس بمرجعها.
* **كيف يعمل في الكود؟**
  - `Entity<TId>`: يطبق `IEquatable<Entity<TId>>` للمساواة بالـ `Id`. الكيانات العابرة (`Transient`) التي لم تأخذ `Id` بعد في قاعدة البيانات لا تتساوى إلا إذا تشاركت نفس المرجع في الذاكرة.
  - `AggregateRoot<TId>`: يرث من `Entity<TId>` ويطبق `IAggregateRoot`. يحتوي على قائمة أحداث خاصة `_domainEvents`، ويوفر دوال الإضافة والمسح الآمن (`AddDomainEvent`, `ClearDomainEvents`).
  - `ValueObject`: يفرض المساواة الهيكلية عبر تقييم مكونات الكائن الذرية المستخرجة من دالة `GetEqualityComponents()`.
  - `DomainEvent`: سجل مجرد (`record`) يضمن توليد `EventId` فريد وتوقيت UTC غير قابل للتغيير.
* **المقايضات:**
  - *المكاسب:* حماية متكاملة لقواعد الدومين وتوحيد معايير المقارنة وأمان الذاكرة.
  - *التكلفة:* ضرورة كتابة مكونات المساواة يدوياً في كل Value Object والتفريق الدقيق بين Entity و Aggregate Root.
* **المفاهيم المرتبطة:** Domain-Driven Design, Structural Equality, Event Sourcing Primitives.

---

## 4. واجهات القدرات المتخصصة (Capability Interfaces)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Domain/IAuditableEntity.cs`، `Domain/ISoftDeletable.cs`، `Domain/IActivatable.cs`، `Domain/IAggregateRoot.cs`
* **ماهيته:** واجهات متخصصة تركز على إعطاء الكيان قدرات محددة بدلاً من إجباره على وراثة كلاس موحد ضخم (`BaseEntity`).
* **لماذا تم استخدامه؟** تجنب كلاس الإله (God Object) وتطبيق مبدأ فصل الواجهات (Interface Segregation Principle) وتفضيل التركيب على الوراثة (Composition over Inheritance).
* **كيف يعمل في الكود؟**
  - يختار الكيان قدراته بدقة:
    ```csharp
    public class CashierShift : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable, IActivatable
    ```
  - يتعرف `AuditSaveChangesInterceptor` وفلاتر الاستعلام التلقائية على هذه الواجهات عبر الـ ChangeTracker ويطبق قواعد الحفظ آلياً.
* **المقايضات:**
  - *المكاسب:* جداول قواعد بيانات نظيفة وخالية من الحقول غير المستخدمة، وحرية معمارية كاملة لكل كيان.
  - *التكلفة:* كتابة خصائص الواجهة في كل كيان (وهو أمر بسيط ومباشر).
* **المفاهيم المرتبطة:** Interface Segregation Principle (ISP), Composition over Inheritance.

---

## 5. سلوكيات مسار MediatR (MediatR Pipeline Behaviors)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Application/ValidationPipelineBehavior.cs`، `Application/LoggingPipelineBehavior.cs`، `Application/PerformancePipelineBehavior.cs`
* **ماهيته:** Middleware وسيط لطلبات الـ CQRS يطبق نمط المزيّن (Decorator Pattern) لمعالجة الاهتمامات المشتركة.
* **لماذا تم استخدامه؟** لتجريد معالجات البزنس (Handlers) من الاهتمامات المشتركة وضمان تنفيذ التحقق والتدقيق والمقاييس على كل طلب دون استثناء.
* **كيف يعمل في الكود؟**
  - **سلوك التحقق (Validation):** يجمع كل كلاسات `IValidator<TRequest>` المسجلة، وينفذها تزامناً بـ `Task.WhenAll`، وفي حال وجود أخطاء يوقف المسار فوراً ويرجع `Result.Failure` باستخدام الانعكاس المحسّن للأنواع العامة (`CreateValidationResult`).
  - **سلوك التسجيل (Logging):** يسجل دخول الطلب، ويعترض النتيجة الفاشلة ليسجلها كتحذير `Warning` مع تفاصيل الخطأ، ويلتقط الـ Exceptions ليسجلها كخطأ فادح قبل إعادة رميها.
  - **سلوك الأداء (Performance):** يقيس زمن التنفيذ بـ `Stopwatch`، ويسجل المقاييس في الـ Meters، ويطلق تحذيراً إذا تجاوز الطلب عتبة الـ SLA المحددة.
* **المقايضات:**
  - *المكاسب:* عزل كامل للبنية التحتية عن البزنس، وتأكيد تشغيل التحقق لكل طلب.
  - *التكلفة:* إضافة استدعاءات دوال خفيفة جداً في مسار MediatR.
* **المفاهيم المرتبطة:** Decorator Pattern, Aspect-Oriented Programming (AOP), CQRS.

---

## 6. مقاييس OpenTelemetry الأصيلة (`System.Diagnostics.Metrics`)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Application/PerformancePipelineBehavior.cs`
* **ماهيته:** توليد مقاييس أداء خفيفة ومباشرة في الذاكرة اعتماداً على مكتبات .NET القياسية دون تضمين حزم OpenTelemetry خارجية في BuildingBlocks.
* **لماذا تم استخدامه؟** توفير مقاييس جاهزة للمراقبة عبر Prometheus و Grafana مع الإبقاء على BuildingBlocks خالية من الاعتماد على مكتبات طرف ثالث.
* **كيف يعمل في الكود؟**
  - ينشئ المكتبة `Meter` باسم `"SuperMarket.POS.Core"`.
  - يعرف العداد `pos_requests_total` لتتبع معدل الطلبات المعالجة موسومة باسم الطلب (`request_name`).
  - يعرف المدرج التكراري `pos_request_duration_ms` لتسجيل توزيع أزمنة الاستجابة بالمللي ثانية.
  - يسجل المقاييس داخل كتلة `finally` لضمان التوثيق حتى لو انهار الطلب باستثناء.
* **المقايضات:**
  - *المكاسب:* صفر تبعيات خارجية، أداء فائق بدون تخصيص ذاكرة زائد (Zero-Allocation Telemetry).
  - *التكلفة:* يتطلب من مشاريع الـ Web API تهيئة Exporters لجمع وعرض البيانات.
* **المفاهيم المرتبطة:** OpenTelemetry, Observability, RED Metrics Pattern.

---

## 7. مراقبات الحفظ في EF Core (SaveChanges Interceptors)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Infrastructure/AuditSaveChangesInterceptor.cs`، `Infrastructure/DispatchDomainEventsInterceptor.cs`
* **ماهيته:** اعتراض دورة حياة حفظ البيانات في EF Core لأتمتة التتبع ونشر الأحداث.
* **لماذا تم استخدامه؟** لأتمتة المتطلبات الرقابية وضمان عدم نسيان تسجيل بيانات التدقيق أو الحذف المنطقي أو نشر الأحداث.
* **كيف يعمل في الكود؟**
  - `AuditSaveChangesInterceptor`:
    - يفحص الكيانات المحذوفة أولاً: يحول حالة `EntityState.Deleted` إلى `Modified`، ويفعل `IsDeleted = true` مع توثيق الوقت والمستخدم.
    - يفحص الكيانات المضافة والمعدلة: يسجل `CreatedAt` و `CreatedBy` عند الإضافة، و `UpdatedAt` و `UpdatedBy` عند التعديل.
    - يقفل خصائص الإنشاء صراحة بـ `IsModified = false` عند التعديل لمنع تزوير السجلات التاريخية.
    - يستخدم مجرد `TimeProvider` القابل للاختبار بالكامل بدلاً من `DateTime.UtcNow`.
  - `DispatchDomainEventsInterceptor`:
    - يستخرج التجميعات الحاملة للأحداث من `ChangeTracker`.
    - يمسح الأحداث فوراً قبل نشرها لمنع الحلقات التكرارية.
    - ينشر الأحداث محلياً عبر MediatR `IPublisher` داخل المعاملة.
* **المقايضات:**
  - *المكاسب:* أتمتة كاملة وموثوقية بنسبة 100% في حفظ البيانات وتدقيقها ونشر الأحداث.
  - *التكلفة:* فحص الـ ChangeTracker يستهلك ميكروثوانٍ طفيفة جداً أثناء كل `SaveChanges`.
* **المفاهيم المرتبطة:** EF Core Interception, In-Process Event Dispatching, TimeProvider.

---

## 8. فلاتر الاستعلام العامة للحذف المنطقي (Global Query Filters)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Infrastructure/ModelBuilderExtensions.cs`
* **ماهيته:** تطبيق فلتر تلقائي على كل الجداول المطبقة لـ `ISoftDeletable` لاستبعاد السجلات المحذوفة (`e.IsDeleted == false`) من كل استعلامات `SELECT`.
* **لماذا تم استخدامه؟** منع تسرب السجلات المحذوفة إلى واجهات الكاشير أو التقارير المالية بسبب نسيان المطور كتابة شرط الفحص.
* **كيف يعمل في الكود؟**
  - يبني شجرة تعبيرات (Expression Tree) برمجياً ويحقنها في الـ `ModelBuilder` لجميع الكيانات المستهدفة.
  - يمكن تجاوزه بسهولة عند رغبة الإدارة في فحص المحذوفات عبر استدعاء `.IgnoreQueryFilters()`.
* **المقايضات:**
  - *المكاسب:* حماية تلقائية من تسرب البيانات واستبعاد كتابة شروط التصفية اليدوية.
  - *التكلفة:* يجب على المطور تذكر `.IgnoreQueryFilters()` عند بناء شاشات الاسترجاع أو التدقيق الإداري.
* **المفاهيم المرتبطة:** Expression Trees, EF Core Global Query Filters.

---

## 9. استراتيجية الترقيم المزدوجة (Dual-Strategy Pagination)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Application/PagedList.cs`، `Application/CursorPagedList.cs`، `Application/PaginationParams.cs`، `Application/CursorParams.cs`، `Application/PaginationSettings.cs`
* **ماهيته:** توفير آليتي ترقيم مختلفتين بحسب طبيعة واجهة الاستخدام وحجم البيانات.
* **لماذا تم استخدامه؟** الترقيم الكلاسيكي بالإزاحة (`Skip`/`Take`) يسبب انهياراً في أداء قواعد البيانات عند الجداول المليونية في السوبرماركت بسبب بطء جمل `COUNT(*)` وعمليات الـ Table Scans.
* **كيف يعمل في الكود؟**
  - **الترقيم الكلاسيكي (`PagedList<T>`):** يعتمد على رقم الصفحة وحجمها، وينفذ `CountAsync` ثم `Skip/Take`. مخصص للشاشات الإدارية التي تتطلب قفزاً بين الصفحات وحساب الإجمالي.
  - **ترقيم المؤشرات (`CursorPagedList<T, TCursor>`):** يعتمد على البحث بالفهرس (`WHERE Id > @cursor LIMIT @size + 1`). يعمل بأداء ثابت $O(1)$ دون استعلام `COUNT(*)` ودون أي تأثر بانزلاق البيانات (Zero Data Drift). مخصص لحركات مبيعات الكاشير اللحظية وسجلات الفواتير.
* **المقايضات:**
  - *المكاسب:* أداء مثالي لكل حالة استخدام وحماية الخوادم من هجمات الاستعلامات المفتوحة عبر حدود دفاعية صارمة.
  - *التكلفة:* ترقيم المؤشرات لا يوفر إجمالي الصفحات ولا يسمح بالقفز لصفحة محددة عشوائياً.
* **المفاهيم المرتبطة:** Keyset Pagination, Index Seeks, Defensive Bounds.

---

## 10. معيار RFC 7807 لمعالجة أخطاء الـ HTTP (Problem Details)

### الحالة الراهنة: `Implemented`
* **الموقع في الكود:** `Results/ResultProblemDetailsExtensions.cs`، `Infrastructure/GlobalExceptionHandler.cs`
* **ماهيته:** إخراج استجابات أخطاء الـ HTTP بتنسيق JSON قياسي معتمد عالمياً وفق مواصفة RFC 7807.
* **لماذا تم استخدامه؟** توحيد شكل الأخطاء أمام كافة تطبيقات الواجهات (Web, Mobile, POS Terminals) لتسهيل قراءتها ومعالجتها برمجياً.
* **كيف يعمل في الكود؟**
  - تترجم دالة `ToProblemDetails()` نوع الخطأ في الدومين إلى كود HTTP القياسي (Validation $\rightarrow$ 400, NotFound $\rightarrow$ 404, Conflict $\rightarrow$ 409, Unauthorized $\rightarrow$ 401, Forbidden $\rightarrow$ 403).
  - يعترض `GlobalExceptionHandler` أي استثناء غير متوقع (500)، ويسجله بالـ StackTrace كاملاً في السجلات مع تتبع المعرف `traceId`، بينما يعيد للمستخدم استجابة آمنة ومبهمة لا تكشف أسرار السيرفر.
* **المقايضات:**
  - *المكاسب:* معيار صناعي موحد، حماية أمنية من تسريب تفاصيل الكود أو السيرفر، وسهولة تتبع المشاكل عبر الـ TraceId.
  - *التكلفة:* التزام المطور بتحويل النتائج الفاشلة عبر `.ToProblemDetails()`.
* **المفاهيم المرتبطة:** RFC 7807, IProblemDetailsService, Distributed Tracing.

---

## 11. تقنيات غير مطبقة داخل BuildingBlocks (خارج النطاق المعماري)

| التقنية / النمط | الحالة | المبرر وموقعها المعماري الصحيح |
| :--- | :--- | :--- |
| **نمط صندوق الصادر (Outbox Pattern / RabbitMQ)** | `Not Implemented` داخل BuildingBlocks | أحداث التكامل الخارجية عبر الشبكة تنتمي لمشاريع الـ Infrastructure الخاصة بالخدمات أو لمكتبة رسائل مخصصة، وليس لنواة الدومين المشتركة. |
| **الكاش الموزع (Distributed Caching / Redis)** | `Not Implemented` داخل BuildingBlocks | لم يتم تضمينها عمداً لإبقاء مكتبة النواة خفيفة ومستقلة عن أي أطر عمل خارجية. |
| **ملفات التهجير (Database Migrations)** | `Not Implemented` داخل BuildingBlocks | التهجيرات تخص حصراً قواعد بيانات الخدمات الملموسة (`SuperMarket.Sales.Infrastructure` إلخ). |
