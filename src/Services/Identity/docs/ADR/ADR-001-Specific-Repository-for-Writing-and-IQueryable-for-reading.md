# ADR-001: نموذج الوصول للبيانات الهجين — Specific Repository للكتابة و IQueryable للقراءة

| Metadata | Details |
| :--- | :--- |
| **Status** | ✅ Accepted |
| **Date** | 2026-10-05 |
| **Author** | Senior Backend Engineer / Architecture Reviewer & Technical Partner |
| **Service** | SuperMarket.Identity |
| **Layer** | Application & Infrastructure (Persistence Ports & Adapters) |
| **Decision Scope** | Data Access Architecture, CQRS, Testability, Domain Encapsulation |

---

## 1. السياق والمشكلة (Context & Problem Statement)

عند بناء طبقة التطبيق (`Identity.Application`) بالاعتماد على **Clean Architecture** و **Domain-Driven Design (DDD)** باستخدام **Entity Framework Core (EF Core 9)**، واجهنا السؤال المعماري الأكثر جدلاً في مجتمع .NET:

> **كيف يجب أن تصل طبقة الـ Application إلى قاعدة البيانات دون كسر مبادئ التصميم ودون إدخال تعقيد لا طائل منه؟**

### التحديات المتعارضة (Competing Forces):
1. **حماية قواعد العمل (Domain Invariants):** في DDD، يجب ألا يُعدل أي كيان فرعي (مثل `BranchOperatingHours`) إلا من خلال الـ Aggregate Root (`Branch`). السماح بالوصول المباشر للكيانات الفرعية يفسد البيانات.
2. **مرونة وسرعة الاستعلام (Query Flexibility & Performance):** كل واجهة مستخدم (UI) أو تقرير يتطلب شكلاً مختلفاً من البيانات (DTO Projections). إجبار القراءة على المرور عبر دوال Repository ثابتة يؤدي إلى **Over-fetching** (تحميل بيانات زائدة) أو **Repository Method Explosion** (انفجار عدد الدوال).
3. **عزل التبعيات (Dependency Inversion - DIP):** يجب ألا تعتمد طبقة الـ Application على تفاصيل الـ Infrastructure أو مزود قاعدة بيانات محدد (PostgreSQL/Npgsql).
4. **قابلية الاختبار (Testability):** الحاجة لاختبار منطق تنسيق الـ Handlers بسرعة عبر Unit Tests (Mocking) دون إهمال اختبار صحة استعلامات SQL الحقيقية.
5. **مقاومة التكرار (DRY) والتعقيد الزائد (YAGNI):** عدم كتابة عشرات الملفات والواجهات التي لا تفعل سوى تمرير الاستدعاءات (`Pass-through code`)، مع تجنب فخ التجريد المسرّب (`Leaky Abstraction`).

---

## 2. البدائل التي تم تقييمها (Options Considered)

### الخيار 1: Generic Repository Pattern لجميع العمليات (`IRepository<T>`)
* **الوصف:** واجهة معيارية واحدة لكل الكيانات تحتوي على `GetByIdAsync`, `GetAllAsync`, `Add`, `Update`, `Remove`.
* **لماذا رُفض؟**
  - يعامل جميع الكيانات بنفس الطريقة، مما يكسر مفهوم Aggregate Boundary (يسمح بإنشاء `IRepository<OperatingHours>`).
  - يعجز عن دعم Eager Loading (`Include`) و Projections بدون إضافة معامِلات LINQ Expressions معقدة تُعيدنا إلى تسريب تفاصيل ORM.
  - يُعتبر Anti-pattern فوق EF Core لأنه يكرر وظيفة `DbSet<T>` القائمة أصلاً.

### الخيار 2: كشف `IQueryable<T>` للجميع عبر واجهة `IIdentityUnitOfWork` واحدة
* **الوصف:** واجهة تحتوي على خصائص `IQueryable<T>` لكل جدول + `Add<TEntity>` عامة + `SaveChangesAsync`.
* **لماذا رُفض جزئياً؟**
  - في جهة الكتابة: يسمح للـ Command Handlers باسترجاع الكيان مجتزءاً دون تحميل الـ Navigation Properties الأساسية، فتعمل قواعد العمل على حالة ناقصة بصمت.
  - يسمح بتعديل الكيانات التابعة مباشرة دون المرور بالـ Aggregate Root.
  - يصعّب عمل Unit Tests مع Mocking لمنطق الـ Commands بسبب صعوبة عمل Mock لـ `IQueryable` و `DbSet`.

### الخيار 3: Specific Repositories منفصلة لكل عملية قراءة وكتابة
* **الوصف:** كتابة واجهة مخصصة لكل Aggregate تحتوي على دوال لكل أمر ولكل استعلام شاشة (مثل `GetBranchesForDropdownAsync`).
* **لماذا رُفض جزئياً؟**
  - ممتاز لجهة الكتابة، لكنه كارثي لجهة القراءة.
  - يؤدي إلى تضخم هائل في عدد الدوال بالواجهات عند كل تعديل بسيط في شاشات العرض.
  - يجبر القراءة على جلب Aggregates كاملة ثم تحويلها إلى DTOs في الذاكرة مما يسبب استهلاكاً غير مبرر للذاكرة وبطء في الاستعلامات.

### الخيار 4: النموذج الهجين المتوافق مع CQRS (Hybrid Persistence Model) — [المختار]
* **الوصف:** فصل كامل في استراتيجية الوصول للبيانات بين مسار الكتابة ومسار القراءة.

---

## 3. القرار المعماري (The Decision)

اعتمدنا رسمياً **النموذج الهجين المتوافق مع مبدأ CQRS**:

```
                              ┌─────────────────────────────────────────┐
                              │        Application Layer Use Cases      │
                              └──────────────────┬──────────────────────┘
                                                 │
                   ┌─────────────────────────────┴─────────────────────────────┐
                   ▼                                                           ▼
       [Write Side: Commands]                                      [Read Side: Queries]
  ┌─────────────────────────────────┐                         ┌─────────────────────────────────┐
  │   I{Aggregate}Repository        │                         │      IIdentityReadDbContext     │
  │   + IUnitOfWork                 │                         │   (AsNoTracking IQueryable)     │
  └────────────────┬────────────────┘                         └────────────────┬────────────────┘
                   │                                                           │
                   │ Returns: Full Aggregates                                  │ Returns: Fast DTO Projections
                   ▼                                                           ▼
       ┌───────────────────────┐                                   ┌───────────────────────┐
       │ PostgreSQL (Tracking) │                                   │ Postgres (NoTracking) │
       └───────────────────────┘                                   └───────────────────────┘
```

### القواعد المعمارية الإلزامية لهذا القرار:

1. **جهة الكتابة (Commands Path):**
   - تُدار حصراً عبر **Specific Repositories** لكل **Aggregate Root فقط** (مثل `IBranchRepository`, `IUserRepository`, `IRoleRepository`).
   - يُمنع إنشاء Repository لأي كيان تابع (Child Entity).
   - دوال الـ Repository لا تُرجع `IQueryable` أبداً؛ بل تُرجع الكيان كاملاً (`Task<Branch?>`) أو نتائج منطقية (`Task<bool> ExistsByCodeAsync`).
   - دالة `GetByIdAsync` مسؤولة مسؤولية كاملة عن تحميل كل الـ Child Collections اللازمة لحماية شروط العمل (Invariants).
   - يتم تقليل التكرار البرمجي في الـ Infrastructure عبر Base Class داخلية:
     `Repository<TAggregate> where TAggregate : AggregateRoot`.
   - واجهة `IUnitOfWork` منفصلة وتحتوي فقط على `SaveChangesAsync` لضمان إتمام المعاملة الذرية (Atomic Transaction) على مستوى الطلب كاملاً.

2. **جهة القراءة (Queries Path):**
   - تُدار عبر واجهة قراءة مخصصة: `IIdentityReadDbContext`.
   - تكشف الجداول كـ `IQueryable<T>` بوضعية `AsNoTracking` تلقائياً.
   - يمنع منعاً باتاً استدعاء `Add`, `Update`, `Remove`, أو `SaveChangesAsync` في Query Handlers.
   - يجب أن تستخدم الـ Handlers دالة `.Select()` لتحويل البيانات مباشرة إلى DTOs على مستوى قاعدة البيانات (SQL Projections).
   - الاستعلامات المشتركة أو المعقدة يُعاد استخدامها عبر **LINQ Extension Methods**.

---

## 4. المبررات الفنية (Architectural Rationale)

| الخاصية | جهة الكتابة (Commands) | جهة القراءة (Queries) | لماذا اختلف النهجان؟ |
| :--- | :--- | :--- | :--- |
| **الهدف الأساسي** | حماية الاتساق وصحة البيانات (Consistency & Invariants) | كفاءة وسرعة جلب البيانات (Latency & Throughput) | قوى التصميم المتناقضة تفرض أدوات مختلفة. |
| **وحدة البيانات** | Aggregate Root كامل محملاً في الذاكرة | Projected DTO (أعمدة محددة فقط) | الكتابة تتطلب رؤية الحالة كاملة؛ القراءة تحتاج فقط ما يظهر على الشاشة. |
| **تتبع الحالة (Tracking)** | مطلوب (Change Tracker) | معطل (`AsNoTracking`) | توفير 40-60% من استهلاك الذاكرة والمعالج في القراءة. |
| **استراتيجية الاختبار** | Unit Tests سريعة بـ Mock للـ Repository | Integration Tests بـ Testcontainers للاستعلام الفعلي | مسار الكتابة فيه تفريعات منطقية تُختبر بـ Mock، ومسار القراءة قيمته في صحة SQL. |
| **الاستقلال عن ORM** | معزول بالكامل خلف واجهة نقية | مرتبط بـ LINQ Provider بقرار واعٍ | كلفة عزل القراءة أعلى بكثير من عائدها، وإذا لزم الأمر ننتقل إلى Dapper في Handler واحد. |

---

## 5. المقايضات (Trade-offs: What We Gained vs What We Sacrificed)

### ما كسبناه (Gains):
* **صفر تلاعب في الـ Invariants:** مستحيل لمطور تعديل ساعات عمل أو رصيد أو صلاحيات دون تفعيل قواعد الـ Domain Entity.
* **حماية تامة من Over-fetching:** استعلامات القراءة تطلب من PostgreSQL الأعمدة اللازمة للـ DTO فقط عبر SQL Projection.
* **اختبارات وحدة فائقة السرعة للمنطق الحرج:** نستطيع اختبار 100 سيناريو فشل ونجاح للـ Command Handlers بـ Mock بسيط وسريع.
* **كود نظيف و DRY:** تخلصنا من انفجار دوال الـ Repositories في القراءة، واستخدمنا Base Class لتقليل كود الـ Repositories في الكتابة.

### ما ضحينا به (Sacrifices / Liabilities):
* **وجود واجهتين مختلفتين للبيانات:** يحتاج المطور الجديد إلى فهم واضح متى يحقن `I{Entity}Repository` ومتى يحقن `IIdentityReadDbContext`.
* **اعتماد طبقة Application على حزمة EF Core Abstractions:** لاستخدام `ToListAsync` و `FirstOrDefaultAsync` في جهة القراءة، تم السماح لطبقة Application بالاعتماد على حزمة `Microsoft.EntityFrameworkCore` المجردة (بدون تضمين Npgsql أو التفاصيل التحتية).

---

## 6. متى يُعاد النظر في هذا القرار؟ (Reconsideration Conditions)

يجب إعادة فتح ومراجعة هذا الـ ADR في الحالات التالية فقط:
1. **إذا تحولت الخدمة إلى CRUD بحت:** إذا تم تفريغ الـ Domain من القواعد وأصبح مجرد نقل جداول، يُلغى الـ Repository ويُعتمد `DbContext` مباشرة للكل لتقليل الطبقات.
2. **إذا ظهرت اختناقات أداء حادة في القراءة:** يتم استبدال استعلامات القراءة في `IIdentityReadDbContext` لـ Handler معين باستخدام **Dapper** مع كتابة استعلام SQL يدوي، دون أي تغيير في جهة الـ Command Repositories.
3. **إذا تقرر تقسيم قاعدة البيانات (Polyglot Persistence):** كأن تُحفظ سجلات الـ Events في MongoDb والجداول في Postgres؛ وقتها تنعزل Repositories الكتابة بسلاسة تامة.

---

## 7. المراجع المعمارية المعتمدة (References)

1. **Eric Evans (2003)** - *Domain-Driven Design: Tackling Complexity in the Heart of Software* (Chapter 6: The Life Cycle of a Domain Object - Repositories).
2. **Vaughn Vernon (2013)** - *Implementing Domain-Driven Design* (Chapter 12: Repositories & Chapter 4: CQRS).
3. **Martin Fowler** - *Catalog of Patterns of Enterprise Application Architecture* ([Repository Pattern](https://martinfowler.com/eaaCatalog/repository.html) & [CQRS Bliki](https://martinfowler.com/bliki/CQRS.html)).
4. **Microsoft Architecture Guides** - *.NET Microservices: Architecture for Containerized .NET Applications* ([Applying Simplified CQRS and DDD Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/apply-simplified-microservice-cqrs-ddd-patterns) & [CQRS Reads with Dapper/EF](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads)).
5. **Vladimir Khorikov (2020)** - *Unit Testing Principles, Practices, and Patterns* (Manning Publications - Domain vs Orchestration Testing).
