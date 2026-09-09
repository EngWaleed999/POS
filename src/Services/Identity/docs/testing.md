# 🧪 استراتيجية الاختبار وضمان الجودة (Testing Strategy)

نتبع هرم الاختبارات القياسي (**Testing Pyramid**) لضمان استقرار خدمة `SuperMarket.Identity` وخلوها من الثغرات الأمنية والمعمارية:

```
          /\
         /  \      E2E Tests (POS Desktop App & Admin UI integration)
        /────\
       /      \    Integration Tests (Testcontainers + WebApplicationFactory)
      /────────\
     /          \  Architecture Tests (NetArchTest)
    /────────────\
   /              \ Unit Tests (xUnit + FluentAssertions + NSubstitute)
  /────────────────\
```

---

## 🔬 1. اختبارات الوحدة (Unit Tests)
* **المسار:** `tests/UnitTests/SuperMarket.Identity.UnitTests/`
* **النطاق:**
  * **اختبار كيانات الـ Domain وقواعد البزنس:**
    * منع تسجيل جهاز POS بدون ربطه بفرع صالح.
    * التحقق من سلامة كود الفرع `BranchCode` (عدم وجود مسافات، صيغة الحروف الكبيرة).
    * فحص تشفير الـ PIN والتأكد من عدم تخزينه كنص صريح.
    * اختبار انتقال حالات الموظف والفرع وإطلاق أحداث المجال المقابلة (`BranchCreatedDomainEvent`).
  * **اختبار معالجات الـ CQRS Handlers:** بعزل تام عبر محاكاة الـ Repositories وخدمات Keycloak باستخدام **NSubstitute**.
  * **التحقق من صحة المدخلات:** عبر اختبار قواعد **FluentValidation** الخاصة بكل Command.

---

## 🐳 2. اختبارات التكامل (Integration Tests with Testcontainers)
* **المسار:** `tests/IntegrationTests/SuperMarket.Identity.IntegrationTests/`
* **التقنية:** `WebApplicationFactory<Program>` مع **Testcontainers for .NET**.
* **كيف تعمل؟**
  * تشغيل حاوية **PostgreSQL حقيقية** داخل Docker مؤقتاً أثناء تنفيذ الاختبارات.
  * تطبيق الـ Migrations الحقيقية وتنفيذ استدعاءات HTTP كاملة للـ Endpoints.
  * فحص صحة تسجيل محطات الكاشير وإدراج أحداث التكامل في جدول `outbox_messages`.
  * محاكاة (Mock) استجابات خادم Keycloak للتأكد من فحص الـ Authorization Policies وعمليات الـ PIN Exchange.

---

## 🏛️ 3. اختبارات المعمارية (Architecture Tests via NetArchTest)
* **المسار:** `tests/ArchitectureTests/SuperMarket.ArchitectureTests/`
* **الهدف:** منع كسر قواعد Clean Architecture برمجياً أثناء التطوير وإلزام المطورين بالمعايير.

```csharp
[Fact]
public void DomainLayer_ShouldNot_DependOn_OtherLayers()
{
    var result = Types.InAssembly(typeof(Branch).Assembly)
        .ShouldNot()
        .HaveDependencyOnAny(
            "SuperMarket.Identity.Application",
            "SuperMarket.Identity.Infrastructure",
            "SuperMarket.Identity.API")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}

[Fact]
public void Handlers_Should_Have_NameEndingWith_Handler()
{
    var result = Types.InAssembly(typeof(CreateBranchCommandHandler).Assembly)
        .That()
        .ImplementInterface(typeof(IRequestHandler<,>))
        .Should()
        .HaveNameEndingWith("Handler")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

---

## 🔒 4. اختبارات الأمان وصلاحيات نقاط البيع (Security Testing)
* **فحص BOLA عبر الفروع:** اختبار محاولة كاشير مسجل في الفرع (A) فتح وردية أو قراءة أجهزة تخص الفرع (B)، والتأكد من إرجاع كود `403 Forbidden`.
* **فحص موافقة المشرف (Supervisor Override):** التأكد من رفض تنفيذ عملية `VoidItem` أو `KickDrawer` عند تقديم رمز كاشير عادي بدلاً من رمز المشرف.
* **فحص حظر الهجمات التكرارية (Brute-Force):** اختبار إرسال 6 محاولات PIN خاطئة متتالية والتأكد من قفل الحساب مؤقتاً بكود `429 Too Many Requests` أو `401 Unauthorized`.
