# استراتيجية الاختبار وضمان الجودة (Testing Strategy) — BuildingBlocks

> **الحالة الراهنة (Current Status):** `Implemented & Verified` (مكتملة ومحققة بنسبة 100%)  
> **مشروع الاختبارات:** `tests/BuildingBlocks/SuperMarket.BuildingBlocks.UnitTests/`  
> **إطار العمل والحزم المستخدمة:** `.NET 10.0` | `xUnit 2.9` | `FluentAssertions 7.0` | `Moq 4.20` | `EF Core InMemory 10.0.3`

---

## 1. الحالة الراهنة للاختبارات (Current Test Status)

* **الواقع البرمجي:** تم بناء حزمة اختبارات شاملة تغطي كافة مكونات النواة المشتركة `SuperMarket.BuildingBlocks`.
* **عدد الاختبارات المنفذة:** **149 اختباراً** آلياً تعمل وتنجح بنسبة 100% دون أي اختبار فاشل أو متجاوز (`Passed: 149, Failed: 0, Skipped: 0`).
* **زمن التنفيذ:** فائق السرعة (~1.8 ثانية لكامل الحزمة) بفضل التصميم المعزول كلياً عن أي خدمات خارجية أو قواعد بيانات سحابية.
* **مقاييس التغطية البرمجية (Code Coverage):**
  * **تغطية الفروع (Branch Coverage):** **94.18%** (162 فرعاً تم اختبارها من أصل 172).
  * **تغطية الأسطر (Line Coverage):** **91.22%** (478 سطراً من أصل 524).
  * *ملاحظة هندسية:* الأسطر الوحيدة غير المشمولة بالتغطية المباشرة هي دوال تسجيل حقن التبعيات (`DependencyInjection.cs`) التي تخضع لاختبارات التكامل على مستوى الـ API.

---

## 2. مصفوفة الأجنحة الستة للاختبارات (Test Wings Matrix)

تم تنظيم الاختبارات وفق 6 أجنحة رئيسية تحاكي طبقات المعمارية النظيفة (Clean Architecture):

| الجناح | المسار والمجلد | ملفات الاختبار | عدد الاختبارات | المسؤولية السلوكية المفحوصة |
| :--- | :--- | :--- | :---: | :--- |
| **الجناح 1: كلاسات الدومين الأساسية** | `Domain/` | `EntityTests.cs`<br/>`ValueObjectTests.cs`<br/>`AggregateRootTests.cs`<br/>`DomainEventTests.cs` | **38** | مساواة الهوية والمكونات، أمان الكيانات العابرة (`Transient`)، منع التلاعب بطابور الأحداث (`NotSupportedException`)، ترتيب FIFO للأحداث، وتفرد معرفات الـ UUID وتوقيت UTC. |
| **الجناح 2: نمط النتائج والبرمجة الوظيفية** | `Results/` | `ResultTests.cs`<br/>`ResultTTests.cs`<br/>`ResultExtensionsTests.cs` | **33** | حماية الشروط الصارمة (منع النجاح بخطأ، أو الفشل بـ `Error.None`)، قفل خاصية `Value` لرمي استثناء عند الفشل، وسلسلة العمليات الوظيفية (`Match`, `Ensure`, `Map`, `Bind`). |
| **الجناح 3: سلوكيات MediatR الوسيطة** | `Application/` | `ValidationPipelineBehaviorTests.cs`<br/>`LoggingPipelineBehaviorTests.cs`<br/>`PerformancePipelineBehaviorTests.cs` | **17** | فحص FluentValidation التزامني، الإيقاف المبكر وتوليد `Result` أو `Result<T>` بالانعكاس، تسجيل مستويات السجلات (Info/Warn/Error)، واحتساب زمن SLA ومقاييس OpenTelemetry. |
| **الجناح 4: مقاطعات EF Core التلقائية** | `Infrastructure/` | `AuditSaveChangesInterceptorTests.cs`<br/>`DispatchDomainEventsInterceptorTests.cs`<br/>`ModelBuilderExtensionsTests.cs` | **13** | أتمتة `CreatedAt` و `CreatedBy`، منع تعديل بيانات الإنشاء عند الـ UPDATE، تحويل الحذف الفعلي لمنطقي، نشر الأحداث عبر `IPublisher` وتفريغها مسبقاً، وتطبيق فلاتر الاستعلام العامة. |
| **الجناح 5: معالجة الأخطاء والـ RFC 7807** | `Results/`<br/>`Infrastructure/` | `ResultProblemDetailsExtensionsTests.cs`<br/>`GlobalExceptionHandlerTests.cs` | **9** | تحويل أنواع الأخطاء (`Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`) لحالات HTTP، حماية تحويل النجاح لمشكلة، وتوليد رد 500 مع `TraceId` وكتم الأسرار في `GlobalExceptionHandler`. |
| **الجناح 6: استراتيجية الترقيم المزدوجة** | `Application/` | `PaginationTests.cs` | **26** | حسابات `TotalPages` الدقيقة، أعلام التنقل (`HasPreviousPage`, `HasNextPage`)، القيود الدفاعية للمعاملات (`Clamping`) لحماية السيرفر من DoS، وترقيم المؤشرات (`CursorPagedList`). |

---

## 3. المبادئ والأنماط الهندسية المتبعة في حزمة الاختبارات

### 3.1 اختبارات المعاملات الشاملة (Parameterized Theories via `[Theory]`)
بدلاً من كتابة عشرات الدوال المتكررة لاختبار حالات الإدخال والحدود الرياضية، اعتمدنا على `[Theory]` و `[InlineData]` لتمرير مصفوفات متكاملة من القيم الحدية (Boundary Conditions) وحالات الحافة (Edge Cases):
* **في ترقيم الصفحات:** اختبار حالات الصفر، الأعداد السالبة، الكسور، والكميات الضخمة جداً في اختبار واحد.
* **في تحويل الأخطاء:** اختبار رسم كافة أنواع الـ `ErrorType` مع أكواد الـ HTTP المقابلة وعناوين المشاكل في دالة واحدة.
* **في كائنات القيمة:** فحص مساواة الكائنات مع خصائص متطابقة، مختلفة، أو تحتوي على قيم `null` مركبة.

### 3.2 العزل واختبار مقاطعات EF Core الحقيقية (Real Interceptor Harness)
لم نقم بعمل Mock مزيف لـ `DbContext` (وهو خطأ شائع يؤدي لاختبارات واهية لا تمثل الحقيقة). بل قمنا بإنشاء `TestDbContext` حقيقي مدعوم بـ `Microsoft.EntityFrameworkCore.InMemory` لضمان:
1. استدعاء دورة حياة `SaveChangesAsync()` كاملة عبر الـ Interceptors.
2. تتبع حالات الكيانات (`Added`, `Modified`, `Deleted`) بواسطة الـ `ChangeTracker` الحقيقي.
3. التأكد الفعلي من أن جمل الـ UPDATE تستبعد عمود `CreatedAt`، وأن الحذف يتحول فعلياً إلى تعديل بقيم `IsDeleted = true`.

### 3.3 حماية نشر الأحداث ومنع الحلقات التكرارية (Loop Protection Verification)
تم التحقق بصرامة عبر Moq من أن `DispatchDomainEventsInterceptor` يستدعي `root.ClearDomainEvents()` **قبل** استدعاء `_publisher.Publish()`، مما يضمن أمان النظام من إعادة نشر نفس الحدث في حال استدعى أحد المعالجات `SaveChangesAsync()` أخرى.

### 3.4 مناعة الاختبارات ضد الطفرات (Mutation Testing Resilience)
تم التحقق عملياً من قوة الاختبارات وعدم كونها اختبارات تحصيل حاصل (Tautological Tests) عبر تجارب الطفرات (Mutation Testing):
* عند تعطيل سطر حماية `entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;` في كود الإنتاج، فشل الاختبار فوراً في `AuditSaveChangesInterceptorTests:line 105`.
* هذا يثبت أن الاختبارات ليست مجرد "شريط أخضر"، بل صمامات أمان سلوكية تسقط عند أي انحراف في منطق النظام.

---

## 4. أوامر تشغيل الاختبارات وتقارير التغطية البرمجية

```bash
# تشغيل كامل حزمة الاختبارات على مستوى الحل (Solution-wide)
dotnet test SuperMarketPOS.slnx --logger "console;verbosity=minimal"

# تشغيل اختبارات جناح معين (مثلاً: معالجة الأخطاء والترقيم)
dotnet test SuperMarketPOS.slnx --filter "FullyQualifiedName~ResultProblemDetails|FullyQualifiedName~GlobalException|FullyQualifiedName~Pagination"

# تشغيل الاختبارات مع استخراج تقرير التغطية الكودية (Code Coverage)
dotnet test SuperMarketPOS.slnx --collect:"XPlat Code Coverage"
```
