# 🧪 استراتيجية واختبارات الجناح الأول: كائنات الدومين الأساسية
## Domain Primitives Testing Master Plan & Comprehensive Edge Cases

* **المشروع:** `SuperMarket.BuildingBlocks.UnitTests`
* **المكونات المستهدفة:** `Entity<TId>`, `ValueObject`, `AggregateRoot<TId>`, `DomainEvent`
* **إطار العمل والمكتبات:** `xUnit`, `FluentAssertions`
* **المسار المستهدف للكود:** `tests/BuildingBlocks/SuperMarket.BuildingBlocks.UnitTests/Domain/`

---

## 1. الفلسفة الهندسية لاختبار كائنات الدومين (Why Test Primitives?)

في معمارية الـ Clean Architecture والـ Domain-Driven Design (DDD):
كائنات الدومين الأساسية (`Entity`, `ValueObject`, `AggregateRoot`) ليست مجرد كود عادي، بل هي **حراس سلامة الذاكرة والمنطق (Integrity Guardians)**:
1. لو فشلت قاعدة المساواة في الـ `Entity`، ستفقد محركات الـ ORM مثل EF Core القدرة على تتبع الكيانات في الـ `ChangeTracker`.
2. لو اختلت مساواة الـ `ValueObject`، سيعتبر النظام عملتين متطابقتين كأنهما مختلفتان، أو تضيع المفاتيح في قواميس الـ `Dictionary<TKey, TValue>`.
3. لو تسرّبت قائمة أحداث الـ `AggregateRoot`، ستقوم خدمات خارجية بتعديل طابور الأحداث وتخريب نشر الرسائل.

لذلك، يجب ألا نكتفي باختبار المسار السعيد (Happy Path)، بل نختبر **كل الأخطاء النادرة وحالات الحافة (Edge Cases)**.

---

## 2. مصفوفة وسيناريوهات اختبار `Entity<TId>`

### أ. التشريح الداخلي للكائن
* يرث من `IEquatable<Entity<TId>>`.
* يقارن بناءً على الهوية (`Id`) ونوع الكيان الحقيقي (`GetType()`).
* يحتوي على مفهوم **الكيانات المؤقتة (Transient Entities)** التي لم تُحفظ بعد في قاعدة البيانات (`Id == default`).

---

### ب. جدول السيناريوهات وحالات الحافة (Edge Cases Matrix)

| المعرف | اسم السيناريو | المدخلات والشرط | النتيجة المتوقعة (Assertion) | الأهمية الهندسية |
| :--- | :--- | :--- | :--- | :--- |
| **ENT-01** | **نفس المرجع في الذاكرة (Same Reference)** | `entity1` و `entity2` يشيران لنفس الكائن في الـ Heap. | `entity1.Equals(entity2) == true`<br>`(entity1 == entity2) == true` | أسرع مسار للمقارنة (Short-circuit via `ReferenceEquals`). |
| **ENT-02** | **تطابق الهوية والنوع (Same Id & Type)** | كائنان من نفس النوع `Order` بنفس الـ `Guid` مع اختلاف خصائص أخرى. | متطابقان (`==` و `Equals` و `GetHashCode`). | جوهر الـ Entity: الهوية هي التي تحدد الكيان وليس الخصائص. |
| **ENT-03** | **اختلاف الهوية (Different Id)** | كائنان من نوع `Order` بمعرفين مختلفين. | غير متطابقين (`!=` و `Equals == false`). | منع تداخل الكيانات المختلفة. |
| **ENT-04** | **المقارنة مع Null** | `entity.Equals(null)`<br>`entity == null`<br>`null == entity` | `false` دائماً دون رمي `NullReferenceException`. | الأمان ضد القيم الفارغة في كل اتجاهات المقارنة. |
| **ENT-05** | **طرفا المقارنة Null** | `(Entity)null == (Entity)null` | `true`<br>`!=` تعيد `false`. | اتساق مشغلات المقارنة في لغة C#. |
| **ENT-06** | **تطابق الـ Id مع اختلاف النوع (Cross-Entity Trap)** | `Order` و `Customer` يحملان بالصدفة نفس الـ `Guid`! | **`false` قطعاً!** | **Edge Case خطير:** منع تشابه كيانات من دومينات مختلفة. |
| **ENT-07** | **وراثة الكيانات (Inheritance Trap)** | `SpecialOrder` يرث من `Order` ولهما نفس الـ `Id`. | `false`؛ لأن `GetType()` مختلف. | حماية الحدود بين الأنواع الفرعية في DDD. |
| **ENT-08** | **الكيانات المؤقتة (Transient Entities) ⚠️** | كائنان جديدان لم يُحفظا بعد (`Id = Guid.Empty`). | **`false` قطعاً!** | **أخطر Edge Case:** منع اعتبار كائنين جديدين كأنهما نفس الكيان في الـ `HashSet`. |
| **ENT-09** | **فحص دالة `IsTransient`** | كائن بـ `Id = default` وكائن بـ `Id = Guid.NewGuid()`. | تعيد `true` للمؤقت و `false` للمحفوظ. | التحقق من صحة حالة الحفظ قبل الـ Persist. |
| **ENT-10** | **عقد `GetHashCode` المتسق** | كائنان متساويان بحسب `Equals`. | يجب أن ينتجا نفس الـ `GetHashCode()`. | ضروري جداً لعمل الـ `Dictionary` و `HashSet`. |
| **ENT-11** | **هاش الكيان المؤقت (Transient HashCode)** | كائنات مؤقتة متعددة بـ `Id = Guid.Empty`. | لا تنتج نفس الـ Hashcode (تستخدم `base.GetHashCode()`). | منع تصادم الهاش (Hash Collisions) للكيانات الجديدة. |

---

## 3. مصفوفة وسيناريوهات اختبار `ValueObject`

### أ. التشريح الداخلي للكائن
* لا يملك `Id`.
* تتحدد هويته ومساواته بالقيمة الكاملة لجميع خصائصه الداخلية عبر `GetEqualityComponents()`.
* غير قابل للتعديل (Immutable).

---

### ب. جدول السيناريوهات وحالات الحافة (Edge Cases Matrix)

| المعرف | اسم السيناريو | المدخلات والشرط | النتيجة المتوقعة (Assertion) | الأهمية الهندسية |
| :--- | :--- | :--- | :--- | :--- |
| **VO-01** | **تطابق كافة المكونات (All Components Equal)** | كائنا `Money(100, "SAR")`. | `true` في `Equals` و `==`. | جوهر كائن القيمة: المساواة الهيكلية التامة. |
| **VO-02** | **اختلاف أحد المكونات (Different Component)** | `Money(100, "SAR")` مقابل `Money(100, "USD")`. | `false` و `!=` تعيد `true`. | أي اختلاف طفيف في الخصائص يعني قيمة جديدة تماماً. |
| **VO-03** | **المقارنة مع Null** | `vo.Equals(null)` و `vo == null`. | `false` بدون أخطاء. | الحماية من استثناءات الإشارة الفارغة. |
| **VO-04** | **مكونات داخلية فارغة (Null Internal Components) ⚠️** | كائنا `Address("King Fahd", apartment: null)`. | متطابقان بنجاح دون انهيار في `GetHashCode` أو `Equals`. | **Edge Case خفي:** التأكد من أن `obj?.GetHashCode() ?? 0` تحمي من الـ Null. |
| **VO-05** | **تشابه المكونات واختلاف نوع الكائن** | `BillingAddress("Riyadh")` و `ShippingAddress("Riyadh")`. | `false` قطعاً؛ لاختلاف الـ `GetType()`. | عزل المفاهيم حتى لو تشابهت هياكل البيانات. |
| **VO-06** | **تطابق الـ HashCode في المجموعات** | كائنا قيمة متطابقان يُضافان لـ `HashSet<ValueObject>`. | المجموعة تحتوي عنصراً واحداً فقط (`Count == 1`). | إثبات كفاءة الـ Value Object كمفتاح في هياكل البيانات. |

---

## 4. مصفوفة وسيناريوهات اختبار `AggregateRoot<TId>` و `DomainEvent`

### أ. التشريح الداخلي للكائن
* جذر التجميع هو نقطة الدخول الوحيدة للتعديل وإطلاق أحداث النطاق (Domain Events).
* يحتوي على طابور أحداث داخلي `_domainEvents` يُعرض خارجياً كـ `IReadOnlyCollection`.

---

### ب. جدول السيناريوهات وحالات الحافة (Edge Cases Matrix)

| المعرف | اسم السيناريو | المدخلات والشرط | النتيجة المتوقعة (Assertion) | الأهمية الهندسية |
| :--- | :--- | :--- | :--- | :--- |
| **AGG-01** | **إضافة حدث دومين سليم (Add Valid Event)** | استدعاء `AddDomainEvent(new OrderPlacedEvent())`. | يزداد عدد الأحداث من 0 إلى 1، والحدث موجود بالقائمة. | المسار السعيد لتسجيل ما حدث في النطاق. |
| **AGG-02** | **حماية إضافة حدث Null (Defensive Guard) ⚠️** | استدعاء `AddDomainEvent(null!)`. | يرمي `ArgumentNullException` فوراً. | منع تلويث طابور الأحداث بعناصر فارغة تكسر النشر لاحقاً. |
| **AGG-03** | **الحفاظ على الترتيب الزمني (FIFO Order)** | إضافة `EventA` ثم `EventB` ثم `EventC`. | القائمة تحتوي الأحداث بنفس ترتيب إضافتها. | حماية التسلسل المنطقي لمعالجة الأحداث (Event Ordering). |
| **AGG-04** | **حذف حدث موجود (Remove Event)** | إضافة حدثين ثم حذف أحدهما بـ `RemoveDomainEvent`. | يتبقى الحدث الآخر فقط (`Count == 1`). | إتاحة التراجع عن حدث قبل الحفظ إذا تطلب البزنس ذلك. |
| **AGG-05** | **حذف حدث غير موجود** | استدعاء `RemoveDomainEvent` لحدث لم يُضف أصلاً. | القائمة تبقى كما هي دون تغيير ودون رمي استثناءات. | السلوك الآمن للعمليات المتكررة (Idempotent Removal). |
| **AGG-06** | **تفريغ طابور الأحداث (Clear Domain Events)** | استدعاء `ClearDomainEvents()` بعد إضافة أحداث. | `DomainEvents` تصبح فارغة تماماً (`Count == 0`). | **أساسي جداً:** يمنع إعادة نشر الأحداث وتكرار الرسائل في المعاملات المالية. |
| **AGG-07** | **منع تسريب التعديل الخارجي (Encapsulation Leak Protection) ⚠️** | محاولة عمل Cast للخاصية `DomainEvents` إلى `List<IDomainEvent>` واستدعاء `.Add(...)`. | يرمي `NotSupportedException`؛ لأنها مغلفة بـ `AsReadOnly()`. | **أقوى اختبار كبسلة:** منع أي كود خارجي من التلاعب بأحداث الجذر. |
| **AGG-08** | **توليد بيانات الحدث التلقائية (DomainEvent Defaults)** | إنشاء كائن يرث من `DomainEvent`. | `EventId` غير فارغ (`Guid != Guid.Empty`)، وتوقيت `OccurredOn` بتوقيت UTC الحالي. | ضمان معرّف تتبع فريد لمنع تكرار الرسائل (Deduplication / Idempotency). |

---

## 5. خطة التنفيذ المنظمة خطوة بخطوة (Execution Steps)

1. **الخطوة 1: إنشاء وتجهيز مشروع الاختبارات:**
   * إنشاء مشروع `SuperMarket.BuildingBlocks.UnitTests` بإطار عمل `xUnit` مستهدفاً `.NET 10`.
   * تثبيت حزم:
     - `FluentAssertions` (للصياغة التعبيرية المقروءة).
     - `NSubstitute` (للمحاكاة لاحقاً).
   * إضافة `ProjectReference` إلى `SuperMarket.BuildingBlocks.csproj`.
   * ربط المشروع بملف الحل [SuperMarketPOS.slnx](file:///e:/dotnet/POS/SuperMarketPOS.slnx).

2. **الخطوة 2: إنشاء نماذج اختبار مساعدة (Test Fakes):**
   * كلاس كيان وهمي `TestEntity : Entity<Guid>` لاختبار الـ Entity.
   * كلاس كيان مشتق `SpecialTestEntity : TestEntity` لاختبار الوراثة.
   * كائن قيمة وهمي `TestValueObject : ValueObject` (مثلاً عنوان أو نقود).
   * جذر تجميع وهمي `TestAggregateRoot : AggregateRoot<Guid>`.
   * حدث دومين وهمي `TestDomainEvent : DomainEvent`.

3. **الخطوة 3: كتابة وتنفيذ ملفات الاختبارات بالترتيب:**
   * أولاً: `EntityTests.cs` (تغطية 11 سيناريو).
   * ثانياً: `ValueObjectTests.cs` (تغطية 6 سيناريوهات).
   * ثالثاً: `AggregateRootTests.cs` و `DomainEventTests.cs` (تغطية 8 سيناريوهات).

4. **الخطوة 4: تشغيل الاختبارات الآلية محلياً وفي الـ CI:**
   * تشغيل `dotnet test` والتحقق من نجاح **25 اختباراً بنسبة 100%**.
   * التأكد من أن الـ GitHub Actions CI يلتقط هذه الاختبارات ويشغلها تلقائياً.
