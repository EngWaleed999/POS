# دراسة حالة معمارية: معضلة الوصول للبيانات في .NET Clean Architecture
## Case Study: The Data Access Architecture Dilemma (Repository vs. IQueryable)

---

## 1. المشكلة (The Problem)

### 1.1 خلفية النقاش والشرارة الأولى
عند تصميم طبقة الـ Application لخدمة **Identity Service** في مشروع الـ POS، قُدّم مقترح أولي يعتمد على كشف واجهة واحدة `IIdentityUnitOfWork` ترجع `IQueryable<T>` لجميع الكيانات مرفقة بجدول مقارنة (Trade-offs) مع الـ **Repository Pattern**.

المطور (القادم من خلفية قوية في **Node.js / Express / NestJS** حيث تُستخدم أدوات مثل Prisma و TypeORM و Services نمطية) لم يقتنع بالجدول، وتحدى المقارنة باعتراضات هندسية دقيقة:

> 1. *"في Repository Pattern نقدر نسوي Generic لكل Aggregate بملف واحد وإذا احتاج ملف معين استعلام مختلف نسوي له، لكن بنسبة 99% ملف واحد يحل مشكلة عدد الملفات!"*
> 2. *"مرونة الاستعلام بفضل الـ Generic لم نعد نحتاج أكثر من دالة."*
> 3. *"قابلية الاختبار (Unit Test) بـ Mock أسهل بكثير في الـ Repository، بينما IQueryable يحتاج In-Memory أو Testcontainers وهذا عيب كبير!"*
> 4. *"استبدال الـ ORM: لو غيرنا الـ ORM في نهج IQueryable سنغير 100 Handler، بينما في الـ Repository نغير ملف التنفيذ فقط!"*
> 5. *"الأداء: نقدر نستخدم IQueryable بداخل الـ Repository!"*
> 6. *"تكرار الاستعلامات: في نهج IQueryable ستتكرر الاستعلامات المتشابهة في أكثر من Handler!"*
> 7. *"أنا قادم من Node.js والوضع هناك مختلف، أين نضع الـ Logic والـ Validation هنا؟"*

### 1.2 تفكيك المشكلة الهندسية العميقة (The Architectural Dilemma)
المشكلة الحقيقية ليست مجرد اختيار نمط برمجي (Design Pattern)، بل هي **تضارب جوهري بين ثلاث قوى تصميمية** عند استخدام **EF Core** داخل **Clean Architecture**:

```
                          [معضلة التصميم]
                                 ▲
                                / \
                               /   \
                              /     \
    [حماية شروط الـ Domain] ◄───────► [مرونة وسرعة الاستعلامات]
             (DDD Aggregates)                (SQL Projections)
                               \   /
                                \ /
                                 ▼
                     [عزل طبقة الـ Infrastructure]
                          (Dependency Inversion)
```

1. **فخ التجريد السطحي (The Redundant Wrapper Trap):**  
   إذا بنينا `IRepository<T>` عام، واكتشفنا أننا بحاجة لـ `Include` و `Where` مرنة، فاضطررنا لإرجاع `IQueryable<T>` من الـ Repository:
   - أصبح الـ Repository مجرد غلاف مفرغ (Wrapper) لا يفعل شيئاً سوى إعادة كتابة ما يقدمه `DbSet<T>` الجاهز من مايكروسوفت.
   - هذا يضيف تعقيداً بلا أي قيمة مضافة.

2. **فخ التجريد المسرّب (The Leaky Abstraction Trap):**  
   `IQueryable` ليس مجرد مصفوفة في الذاكرة؛ إنه **شجرة تعبيرات (Expression Tree)** مرتبطة بمترجم الـ ORM (EF Core Query Provider).
   - إذا كشفت `IQueryable` خارج الـ Repository، فأنت **لم تعزل الـ ORM أبداً**. لو استبدلت EF Core بـ Dapper، ستنهار الـ 100 Handler لأن Dapper لا يفهم `IQueryable` ولا `Include`.

3. **فخ تدمير حدود الـ Aggregate في DDD:**  
   في الـ Domain، ساعات العمل (`BranchOperatingHours`) ليست كياناً مستقلاً؛ هي تابعة للفرع (`Branch`).
   - لو أنشأنا `IRepository<T>` عام أو كشفنا `IQueryable<OperatingHours>`، يستطيع أي مطور في أي Handler تعديل ساعات العمل مباشرة دون المرور بكيان `Branch`، مما يؤدي إلى تجاوز قواعد العمل (مثلاً: ألا يكون وقت الإغلاق قبل الفتح) وفساد البيانات الصامت.

4. **فخ الثقة الكاذبة في الـ Mocking (The False Confidence Trap):**  
   سهولة عمل Mock لـ `_repo.GetByIdAsync()` تعطي شعوراً زائفاً بالأمان. الـ Mock يختبر تسلسل الاستدعاء فقط، لكنه يعجز عن اكتشاف:
   - أخطاء ترجمة الاستعلام إلى SQL في Postgres.
   - مشكلات الأداء القاتلة مثل $N+1$ Queries.
   - كسر الـ Unique Database Constraints عند التزامن.

---

## 2. الحلول المتاحة (The Alternatives)

| الحل | الفكرة الأساسية | نقاط القوة | نقاط الضعف القاتلة |
| :--- | :--- | :--- | :--- |
| **الحل 1: Generic Repository تقليدي** | واجهة واحدة `IRepository<T>` لكل الكيانات مع دوال CRUD عامة. | • عدد ملفات قليل.<br>• مفهوم مألوف للمبتدئين. | • يكسر حدود الـ Aggregates.<br>• يعجز عن Eager Loading بدون حيل معقدة.<br>• إما يسبب Over-fetching أو يضطر لكشف IQueryable. |
| **الحل 2: كشف IQueryable عبر UnitOfWork للكل** | واجهة `IIdentityUnitOfWork` تكشف كل الجداول كـ `IQueryable`. | • مرونة LINQ كاملة في الـ Handlers.<br>• ملف واجهة واحد فقط.<br>• ممتاز للاستعلامات السريعة. | • غياب حماية الـ Aggregates في مسار الكتابة.<br>• صعوبة بالغة في الـ Mocking للـ Unit Tests.<br>• تكرار الاستعلامات عبر الـ Handlers. |
| **الحل 3: Specific Repositories كلاسيكية للكل** | واجهة لكل Aggregate فيها دوال لكل أمر ولكل استعلام شاشة. | • عزل صارم لقواعد العمل.<br>• Mocking سهل ومباشر. | • انفجار هائل في عدد الملفات والدوال.<br>• بطء شديد في القراءة (Over-fetching) لأنها ترجع Aggregates كاملة للشاشات البسيطة. |
| **الحل 4: النمط الهجين المتوافق مع CQRS [الحل المختار]** | **Specific Repositories** للكتابة (Commands) + **IQueryable** للقراءة (Queries). | • يجمع أفضل ما في الطرفين ويقضي على نقاط ضعفهما.<br>• يحمي الـ Domain كاملاً.<br>• يمنح أقصى سرعة قراءة بـ SQL Projections. | • وجود واجهتين مختلفتين للوصول إلى البيانات. |

---

## 3. لماذا اخترنا الحل الهجين؟ (The Architectural Rationale)

تم اختيار **النمط الهجين (Hybrid Persistence Model)** لأن عمليات الكتابة وعمليات القراءة تخضعان **لقوى ومتطلبات هندسية متناقضة بالكامل (Asymmetric Forces)**:

```
┌──────────────────────────────────────┐      ┌──────────────────────────────────────┐
│        مسار الكتابة (Commands)        │      │        مسار القراءة (Queries)        │
├──────────────────────────────────────┤      ├──────────────────────────────────────┤
│ 1. الهدف: حماية اتساق البيانات.       │      │ 1. الهدف: السرعة والأداء.            │
│ 2. يتطلب تحميل Aggregate كامل.       │      │ 2. يتطلب أعمدة محددة فقط (DTO).      │
│ 3. الاستعلامات قليلة وثابتة (By Id).  │      │ 3. الاستعلامات متنوعة ومتغيرة باستمرار.│
│ 4. يتطلب Change Tracking.            │      │ 4. يتطلب No-Tracking.                │
│ 5. التجريد الصارم يحمي البيزنس.      │      │ 5. التجريد الصارم يخنق الأداء.        │
└──────────────────────────────────────┘      └──────────────────────────────────────┘
```

### كيف فكك هذا الحل جميع اعتراضات المطور؟

#### 1. حل مشكلة "عدد الملفات وتكرار الـ Repository":
* لا ننشئ Repository لكل جدول في قاعدة البيانات (ليس لدينا 18 واجهة).
* ننشئ Repository فقط لـ **Aggregate Roots** (في خدمتنا: `Branch`, `User`, `Role`).
* في الـ Infrastructure، كتبنا كلاس مشترك عام (Base Class):
  ```csharp
  internal abstract class Repository<TAggregate>(IdentityDbContext db) where TAggregate : AggregateRoot
  {
      public void Add(TAggregate entity) => db.Set<TAggregate>().Add(entity);
      public virtual Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken ct) =>
          db.Set<TAggregate>().FirstOrDefaultAsync(e => e.Id == id, ct);
  }
  ```
  **النتيجة:** طبقنا فكرة الـ Generic لتقليل تكرار الكود البرمجي داخلياً، لكن حافظنا على واجهات مخصصة (Specific Contracts) خارجياً لحماية النظام!

#### 2. حل مشكلة "الاختبارات (Mocking vs Testcontainers)":
* **في الـ Commands:** الـ Handler يحتوي على خطوات منطقية (تحقق من الكود -> استدعاء Domain -> حفظ). هذا يُختبر بـ **Unit Test مع Mock** لـ `IBranchRepository` في أجزاء من الميلي ثانية.
* **في الـ Queries:** القراءة لا تحتوي على منطق بيزنس، بل استعلام SQL. هنا لا فائدة من الـ Mock؛ الأمان الحقيقي هو اختبار الاستعلام الفعلي ضد PostgreSQL حقيقي باستخدام **Testcontainers**.
* **النتيجة:** كل جزء يُختبر بالأداة المناسبة لطبيعته.

#### 3. حل مشكلة "تكرار الاستعلامات المشابهة":
* بدلاً من إضافة دوال في Repository، نستخدم **LINQ Extension Methods**:
  ```csharp
  public static IQueryable<Branch> WhereActiveInCity(this IQueryable<Branch> q, string city)
      => q.Where(b => b.IsActive && b.Address.City == city);
  ```
* **النتيجة:** حققنا مبدأ **DRY** بالكامل مع الحفاظ على مرونة وقوة الـ LINQ.

---

## 4. إجابات أسئلة التحقق كحالات دراسية واقعية (Real-World Applications)

خلال النقاش، طُرحت 3 أسئلة معمارية حاسمة لاختبار رسوخ الفهم:

---

### الحالة الأولى: `DeactivateBranchHandler`
> **السؤال:** Handler يحتاج يتحقق أن الفرع لا يحتوي على موظفين نشطين قبل تعطيله، هل يستخدم `IBranchRepository` أم `IIdentityReadDbContext`؟
* **إجابة المطور:** *"سوف نستعمل IBranchRepository لأنه فيه تحقق ويجب أن نحمي الـ Rule."*
* **التقييم والعمق الهندسي:**
  - **الإجابة صحيحة 100%.**
  - **السبب المعماري الكامل:** عملية التعطيل هي **Command** (تغيير حالة). الـ Handler يسترجع الـ Aggregate كاملاً عبر `IBranchRepository.GetByIdAsync(id)` (الذي يضمن تحميل كل ما يلزم)، ثم يستدعي دالة البيزنس في الكيان `branch.Deactivate()`. الكيان نفسه هو الذي يفحص شروطه ويرمي خطأ إذا وُجد موظفون، ثم يُحفظ التعديل عبر `_unitOfWork.SaveChangesAsync()`.
  - لو استخدمنا `ReadDbContext` (المخصص للقراءة بـ NoTracking)، فلن نستطيع تعديل الكيان ولا حفظه في الـ Change Tracker!

---

### الحالة الثانية: شاشة "عرض عدد الموظفين في كل فرع"
> **السؤال:** لو جاء طلب شاشة تعرض عدد الموظفين في كل فرع، لماذا لا نضيف دالة `GetBranchesWithStaffCount()` في `IBranchRepository`؟
* **إجابة المطور:** *"الموضوع قراءة فقط لا يوجد شيء حساس، فلو استعملنا Repository سنضطر لكتابة دالة لكل شاشة، ومن ناحية الأداء نستعمل IQueryable لتتم المعالجة في Database وتأتي جاهزة بـ AsNoTracking."*
* **التقييم والعمق الهندسي:**
  - **إجابة ممتازة وناضجة هندسياً.**
  - **السبب المعماري الكامل:** لو وضعناها في الـ Repository، إما أن نحمّل كائنات `Branch` وجميع موظفيهم في الذاكرة للعد (كارثة أداء وذاكرة: **Over-fetching**)، أو نضيف دالة متخصصة ترجع DTO داخل واجهة الـ Aggregate مما يشوه مسؤوليتها.
  - عبر `IIdentityReadDbContext`، يكتب الـ Query Handler استعلام Projection مباشر:
    ```csharp
    var result = await readDb.Branches
        .Select(b => new BranchStaffCountDto(b.Id, b.Name, b.Staff.Count()))
        .ToListAsync(ct);
    ```
    فتترجمها قاعدة البيانات إلى استعلام `SELECT b.Id, b.Name, COUNT(s.Id) ... GROUP BY` فائق السرعة وبأقل حجم نقل شبكي ممكن.

---

### الحالة الثالثة: لماذا `IUnitOfWork` واجهة منفصلة وليست دالة داخل الـ Repository؟
> **السؤال:** لماذا لا نضع `SaveChanges` داخل `IBranchRepository` وتكون دالة واحدة؟
* **إجابة المطور:** *"لأنه ليس كل العمليات تتطلب Transaction."*
* **التقييم والتصحيح الهندسي الدقيق (Crucial Architectural Distinction):**
  - السبب ليس غياب الـ Transaction، بل **معاملات التعديل متعددة الكيانات (Multi-Aggregate Atomic Consistency)**!
  - **سيناريو الفشل الكارثي:** لنفترض أن لدينا Use Case ينقل موظفاً من فرع ويعدل إحصائيات فرع آخر في نفس الطلب.
    - لو كانت دالة `SaveChanges` داخل الـ Repository:
      ```csharp
      _branchRepository.SaveChanges(); // حفظ الفرع الأول بنجاح!
      // انقطع الاتصال بقاعدة البيانات أو حدث خطأ أثناء حفظ المستخدم:
      _userRepository.SaveChanges();   // ❌ فشل!
      ```
    - **النتيجة:** قاعدة البيانات أصبحت في حالة متناقضة ومشوهة (Inconsistent / Corrupted State) لأن جزءاً حُفظ والآخر فشل!
  - **الحل:** فصل `IUnitOfWork` يضمن أن كل الـ Repositories تشترك في نفس جلسة العمل (Scoped Context)، ولا يتم استدعاء الحفظ إلا مرة واحدة في نهاية الـ Handler عبر `_unitOfWork.SaveChangesAsync()` لتتم العملية كلها أو تفشل كلها ذرّياً (**Atomicity**).

---

## 5. ميزان المقايضات: ماذا كسبنا وماذا خسرنا؟ (Trade-offs Balance Sheet)

في الهندسة البرمجية لا توجد حلول سحرية كاملة؛ كل قرار هو عبارة عن مقايضة مدروسة:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                   الميزان المعماري                                    │
├───────────────────────────────────────────┬────────────────────────────────────────────┤
│           ما كسبناه (Gains)               │          ما دفعناه كثمن (Sacrifices)       │
├───────────────────────────────────────────┼────────────────────────────────────────────┤
│ ✅ حماية مطلقة لقواعد الـ Domain           │ ⚠️ وجود واجهتين للبيانات في الـ Application │
│    مستحيل تعديل أي كيان فرعي بالخطأ       │    (يحتاج المطور لفهم CQRS للتفريق بينهما) │
├───────────────────────────────────────────┼────────────────────────────────────────────┤
│ ✅ أقصى أداء ممكن في القراءة (Zero Waste) │ ⚠️ تسريب حزمة EF Core Abstractions للقراءة │
│    استعلامات SQL Projections مباشرة إلى DTO│    طبقة الـ Application تعتمد على LINQ     │
├───────────────────────────────────────────┼────────────────────────────────────────────┤
│ ✅ سرعة اختبارات الوحدة (Fast Unit Tests)  │ ⚠️ استعلامات القراءة تحتاج Integration Test│
│    الـ Command Handlers تُختبر بـ Mock سهل │    لا يمكن الاكتفاء بالـ Unit Test لاختبارها│
├───────────────────────────────────────────┼────────────────────────────────────────────┤
│ ✅ التخلص من تضخم دوال الـ Repositories   │ ⚠️ كتابة Base Class إضافية في الـ Infra   │
│    لا حاجة لملفات جديدة مع كل شاشة عرض     │    تتطلب فهماً جيداً لـ Generics في C#     │
└───────────────────────────────────────────┴────────────────────────────────────────────┘
```

---

## 6. نماذج ذهنية للمهندس المتقدم (Senior Mental Models)

### الانتقال من Node.js إلى .NET Clean Architecture:
1. **في Node.js:** غالباً ما تكون الكيانات (Schemas) مجرد أكياس بيانات غبية (Anemic Data Bags)، والمنطق مكدس في Services ضخمة.  
   **في .NET DDD:** الكيان هو الحارس (Rich Domain Model). لا أحد يغير خاصية إلا عبر Method داخل الكيان.
2. **قاعدة Martin Fowler الذهبية:**  
   > *"لا تخفِ لغة استعلام قوية وغنية مثل LINQ/SQL خلف واجهة عقيمة ومحدودة مثل `GetByX` و `GetByY`."*
3. **قاعدة Eric Evans في DDD:**  
   > *"الـ Repositories مخصصة فقط للـ Aggregate Roots لحماية حدود المعاملات، وليست مرآة لكل جدول في قاعدة البيانات."*
4. **حكمة الـ Senior Engineer:**  
   > *"التجريد ليس هدفاً بحد ذاته. التجريد الذي يخفي ما نحتاجه هو عبء، والتجريد الذي يفشل في حماية ما يجب حمايته هو إهمال."*
