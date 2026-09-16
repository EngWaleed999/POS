# استراتيجية الاختبار وضمان الجودة (Testing Strategy) — BuildingBlocks

> **النطاق:** تدقيق التغطية الاختبارية الحالية، توثيق الفجوات، تحديد الأولويات الحرجة، ونماذج عملية لاختبار مكونات `SuperMarket.BuildingBlocks`.

---

## 1. الحالة الراهنة للاختبارات (Current Test Status)

* **حالة التغطية الحالية:** `Gaps Identified` (توجد فجوة بحاجة للإكمال)
* **الواقع البرمجي:** مجلد `tests/` الرئيسي في جذر المشروع فارغ حالياً، ولا توجد اختبارات آلية مكتوبة ومخصصة لمكتبة `SuperMarket.BuildingBlocks`.
* **الإجراء الهندسي المطلوب:** إنشاء مشروع اختبارات مستقل (مثل `tests/BuildingBlocks/SuperMarket.BuildingBlocks.UnitTests`) لقفل الشروط الصارمة واختبار المراقبين وسلوكيات الـ Pipeline.

---

## 2. مصفوفة تحليل الفجوات وأولويات الاختبار (Gap Analysis)

| المكون | التغطية الراهنة | الأولوية الهندسية | السيناريوهات والشروط الواجب اختبارها |
| :--- | :--- | :--- | :--- |
| **`Result` و `Result<TValue>`** | معدومة | **حرجة جداً (Critical)** | التحقق من منع إنشاء نتائج متناقضة، رمي استثناء عند استدعاء `Value` لنتيجة فاشلة، وعمل المعاملات الضمنية (Implicit Operators). |
| **`ResultExtensions` (ROP)** | معدومة | **حرجة جداً (Critical)** | تنفيذ `Match` للفرع الصحيح، تحول `Ensure` للفشل عند كسر الشرط، تحويل القيم بـ `Map`، والإيقاف المبكر بـ `Bind`. |
| **`Entity<TId>` و `ValueObject`** | معدومة | **عالية (High)** | المساواة بالهوية وليس بالمرجع، مساواة الكيانات العابرة (`Transient`)، توافق الـ HashCode، والمساواة المكوناتية لكائنات القيمة. |
| **`ValidationPipelineBehavior`** | معدومة | **عالية (High)** | استمرار التنفيذ عند عدم وجود فواحص، تشغيل الفواحص تزامناً، دمج رسائل الأخطاء في `Error.Validation`، وإرجاع نتيجة فاشلة دون تشغيل الـ Handler. |
| **`PerformancePipelineBehavior`** | معدومة | **عالية (High)** | استدعاء `next()`، قياس الوقت المنقضي، زيادة عداد `pos_requests_total`، إطلاق تحذير عند تجاوز العتبة، وتنفيذ كتلة `finally` عند الانهيار. |
| **`AuditSaveChangesInterceptor`** | معدومة | **حرجة جداً (Critical)** | تسجيل `CreatedAt` و `CreatedBy` عند الإضافة، تسجيل `UpdatedAt` و `UpdatedBy` عند التعديل، منع تعديل بيانات الإنشاء، تحويل الحذف الفعلي لمنطقي، واختبار الـ `TimeProvider`. |
| **`DispatchDomainEventsInterceptor`** | معدومة | **حرجة جداً (Critical)** | استخراج الأحداث من `IAggregateRoot`، تفريغ طابور الأحداث قبل النشر، نشر الأحداث عبر `IPublisher`، والتعامل الآمن مع الطوابير الفارغة. |
| **`PagedList<T>` و `CursorPagedList`** | معدومة | **متوسطة (Medium)** | حسابات الصفحات (`TotalPages`, `HasNextPage`)، القيود الدفاعية للمعاملات (`Clamp`)، واستخراج المؤشر التالي. |

---

## 3. نماذج برمجية عملية لكتابة الاختبارات (xUnit + FluentAssertions + Moq)

### 3.1 اختبار شروط كلاس `Result`
```csharp
[Fact]
public void Constructor_ShouldThrowInvalidOperationException_WhenSuccessInitializedWithError()
{
    // Act
    Action act = () => new TestResult(isSuccess: true, error: Error.Failure("Code", "Desc"));

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*successful result cannot be initialized with an error*");
}

[Fact]
public void Value_ShouldThrowInvalidOperationException_WhenResultIsFailure()
{
    // Arrange
    Result<string> result = Result.Failure<string>(Error.Validation("Code", "Desc"));

    // Act
    Action act = () => _ = result.Value;

    // Assert
    act.Should().Throw<InvalidOperationException>()
       .WithMessage("*The value of a failure result cannot be accessed*");
}
```

### 3.2 اختبار `AuditSaveChangesInterceptor` باستخدام `FakeTimeProvider`
```csharp
[Fact]
public async Task SavingChangesAsync_ShouldSetUtcTimestampAndActor_ForAddedAuditableEntity()
{
    // Arrange
    var fixedTime = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    var fakeTimeProvider = new FakeTimeProvider(fixedTime);
    var userContextMock = new Mock<ICurrentUserContext>();
    userContextMock.Setup(u => u.UserId).Returns("USER_123");

    var interceptor = new AuditSaveChangesInterceptor(userContextMock.Object, fakeTimeProvider);

    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .AddInterceptors(interceptor)
        .Options;

    using var context = new TestDbContext(options);
    var entity = new TestAuditableEntity();
    context.Add(entity);

    // Act
    await context.SaveChangesAsync();

    // Assert
    entity.CreatedAt.Should().Be(fixedTime);
    entity.CreatedBy.Should().Be("USER_123");
}
```

### 3.3 اختبار أمان نشر الأحداث وعدم التكرار في `DispatchDomainEventsInterceptor`
```csharp
[Fact]
public async Task SavingChangesAsync_ShouldClearEventsBeforeDispatching_ToPreventDuplicatePublishing()
{
    // Arrange
    var publisherMock = new Mock<IPublisher>();
    var interceptor = new DispatchDomainEventsInterceptor(publisherMock.Object);

    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .AddInterceptors(interceptor)
        .Options;

    using var context = new TestDbContext(options);
    var aggregate = new TestAggregate(Guid.NewGuid());
    aggregate.AddTestEvent(new TestDomainEvent());
    context.Add(aggregate);

    // Act
    await context.SaveChangesAsync();

    // Assert
    aggregate.DomainEvents.Should().BeEmpty();
    publisherMock.Verify(p => p.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

---

## 4. أوامر تشغيل الاختبارات البرمجية

عند تجهيز مشروع الاختبارات:
```bash
# تشغيل كافة اختبارات الوحدة مع تقرير تفصيلي
dotnet test tests/BuildingBlocks/SuperMarket.BuildingBlocks.UnitTests/

# استخراج تقرير تغطية الكود البرمجي (Code Coverage)
dotnet test --collect:"XPlat Code Coverage"
```
