# 🏛️ سجل القرارات المعمارية (ADR-002)
## بنية الـ CQRS، سلوكيات الـ Pipeline، وأتمتة أحداث الدومين في BuildingBlocks

* **الحالة:** معتمد ومطبق (Accepted & Implemented)
* **التاريخ:** 2026-09-14
* **السياق والمشروع:** `SuperMarket.BuildingBlocks` / المنصة المركزية لنقاط البيع (POS)
* **المؤلفون:** Senior Backend Engineer & Architecture Reviewer

---

## 1. Context (سياق المشكلة)

في نظام نقاط بيع وسوبرماركت مركزي يعالج آلاف العمليات في الدقيقة عبر خدمات مصغرة مستقلة (Identity, Sales, Inventory, Operations):
1. نحتاج إلى فصل صارم بين عمليات تعديل البيانات وعمليات الاستعلام (CQRS).
2. نحتاج إلى معالجة الاهتمامات المشتركة (Cross-Cutting Concerns) مثل: التحقق من صحة البيانات (Validation)، تسجيل الأحداث (Structured Logging)، ومراقبة سرعة الاستجابة (Performance SLA Monitoring) دون تكرارها داخل الـ Handlers.
3. نحتاج إلى آلية موثوقة لنشر أحداث الدومين (Domain Events) عند حفظ التغييرات في قاعدة البيانات دون إجبار المطور على تذكر استدعاء MediatR يدوياً ودون تلويث كود الـ Domain.
4. نحتاج إلى جعل عتبة مراقبة البطء (Slow Request Threshold) قابلة للضبط بين الخدمات (مثل 300ms للمبيعات مقابل 2000ms للتقارير) دون الحاجة لإعادة عمل Build للخدمة.

---

## 2. Options Considered (الخيارات الهندسية التي تم تقييمها)

### أولاً: بخصوص معالجة الاهتمامات المشتركة (Cross-Cutting Concerns):
1. **الخيار (أ): كتابة التحقق واللوجز داخل كل Controller أو Handler يدوياً.**
   * *التقييم:* مرفوض تماماً لانتهاكه مبدأ DRY وعرضته للنسيان وتلويث البزنس.
2. **الخيار (ب): استخدام ASP.NET Core Middlewares التقليدية.**
   * *التقييم:* مفيد لطبقة الـ HTTP، لكنه يفتقر لمعرفة نوع الـ Command والـ C# Object والـ Generic Response في طبقة التطبيق.
3. **الخيار (ج - المعتمد): استخدام MediatR Pipeline Behaviors وفق نمط Decorator Pattern.**
   * *التقييم:* موصى به رسمياً في دليل Microsoft eShop المرجعي؛ يعزل الاهتمامات ويعمل مباشرة على مستوى الـ Commands والـ Queries.

### ثانياً: بخصوص نشر أحداث الدومين (Domain Events):
1. **الخيار (أ): النشر اليدوي عبر `_mediator.Publish` في كل Handler.**
   * *التقييم:* مرفوض؛ خطأ بشري واحد كفيل بتعطيل المزامنة المحلية.
2. **الخيار (ب): حقن `IMediator` داخل كيانات الـ Domain لتنشر الحدث لحظة وقوعه.**
   * *التقييم:* مرفوض معمارياً؛ ينتهك نقاء الـ Domain ويربط الكيانات بإطار عمل خارجي.
3. **الخيار (ج - المعتمد): استخدام EF Core `SaveChangesInterceptor`.**
   * *التقييم:* شفاف، مركزي، يراقب `SavingChangesAsync` ويستخرج الأحداث ويمسحها وينشرها آلياً قبل إتمام المعاملة.

### ثالثاً: بخصوص تسجيل المقاييس والـ Logging:
1. **الخيار (أ): دمج الـ Logging والـ Stopwatch والـ Metrics في ملف واحد.**
   * *التقييم:* مرفوض؛ انتهاك لمبدأ المسؤولية الواحدة (SRP) وصعوبة في صيانة واختبار الأداء مستقلاً.
2. **الخيار (ب - المعتمد): فصل `LoggingPipelineBehavior` عن `PerformancePipelineBehavior`.**
   * *التقييم:* ممتاز؛ عزل دورة حياة اللوجز عن قياسات الـ SLA ومقاييس OpenTelemetry.

---

## 3. Decision (القرار النهائي المعتمد)

1. اعتماد واجهات دلالية صريحة: `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>` مرتبطة صراحة بنمط `Result` و `Result<T>`.
2. بناء ثلاث سلوكيات متتالية في الـ Pipeline:
   - `PerformancePipelineBehavior`: قياس الوقت بساعة الإيقاف، تسجيل مقاييس OpenTelemetry، ومراقبة عتبة الـ SLA.
   - `LoggingPipelineBehavior`: تسجيل مسار الطلب، والتمييز الذكي بين تحذيرات البزنس وانهيارات النظام.
   - `ValidationPipelineBehavior`: فحص الطلب عبر FluentValidation بشكل متوازٍ (`Task.WhenAll`) والإيقاف الفوري (Short-Circuit) عند وجود أخطاء.
3. تطبيق **Options Pattern** عبر `PerformanceSettings` وحقن `IOptions<PerformanceSettings>` بقيمة افتراضية دفاعية (`500ms`) لجعل عتبة الأداء ديناميكية وقابلة للضبط من `appsettings.json`.
4. بناء `DispatchDomainEventsInterceptor` لنشر الـ Domain Events محلياً عبر MediatR `IPublisher` مع مسح الأحداث قبل النشر لمنع التكرار (Idempotency).

---

## 4. Why (لماذا اخترنا هذا التصميم؟)

* **التوافق التام مع توثيق وممارسات Microsoft الرسمية:**
  - موثق في [Microsoft eShop Architecture Guide: MediatR Behaviors](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api).
  - استخدام `System.Diagnostics.Metrics.Meter` كمعيار OpenTelemetry أصيل في .NET دون تثبيت باكيجات خارجية ثقيلة في الـ BuildingBlocks.
* **الحماية من الانهيار (Resilience):** استخدام كتلة `finally` في الـ PerformanceBehavior يضمن تسجيل أزمنة الطلبات حتى لو حدث انهيار غير متوقع (`Unhandled Exception`).
* **استقلالية الـ Domain:** يظل نطاق العمل نقياً تماماً وخالياً من أي ارتباط بـ EF Core أو MediatR.

---

## 5. Trade-offs (المقايضات والتكلفة)

| المكاسب المحققة (+) | التكلفة والمقايضات المقبولة (-) |
| :--- | :--- |
| عزل كامل للمسؤوليات (Clean Code & SRP). | إضافة طبقات استدعاء دوال خفيفة في الـ Pipeline (Microseconds). |
| منع النسيان البشري لأخطاء التحقق أو نشر الأحداث بنسبة 100%. | استخدام Reflection بسيط داخل مصنع الـ Validation للتعامل مع Generic Types. |
| توافق فوري مع أدوات المراقبة العالمية (Prometheus/Grafana/Seq). | ضرورة تهيئة الـ OpenTelemetry Exporters في مشروع الـ API المستضيف. |
| مرونة تعديل سرعة النظام لكل خدمة من ملف الإعدادات. | الحاجة لكتابة كلاس إعدادات وحقن `IOptions<T>`. |

---

## 6. Consequences (الآثار المترتبة على النظام)

* **على المعمارية:** طبقة الـ Handlers أصبحت نحيفة جداً وخالية من أي كود فحص أو قياس أداء، وتركز حصراً على منطق البزنس.
* **على التطوير:** المطور فقط ينشئ `ICommand` ويكتب له `AbstractValidator`، وكل شيء آخر يحدث تلقائياً.
* **على العمليات والتشغيل:** إمكانية رصد أي طلب يتجاوز عتبة الـ 500ms في لوحات تحكم Grafana أو سيرفرات Seq في ثانية واحدة.
* **على الأمان:** منع تمرير أي بيانات غير مطابقة لقواعد التحقق إلى قواعد البيانات.

---

## 7. Reconsideration Conditions (متى نعيد النظر في هذا القرار؟)

* إذا تطلب النظام لاحقاً نشر أحداث خارج حدود الخدمة عبر الشبكة (Cross-Microservices)، سنقوم بتوسيع الـ Interceptor لتطبيق **Outbox Pattern** وحفظ الأحداث في جدول `outbox_messages` لإرسالها عبر RabbitMQ كـ **Integration Events**.
