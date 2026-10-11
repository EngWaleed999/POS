# ADR-003: التحقق الاستباقي من المفاتيح الخارجية (Pre-flight Validation) مقابل استثناءات قاعدة البيانات (Database Constraint Exceptions)

| Metadata | Details |
| :--- | :--- |
| **Status** | ✅ Accepted |
| **Date** | 2026-10-11 |
| **Author** | Senior Backend Engineer / Architecture Reviewer & Technical Partner |
| **Service** | SuperMarket.Identity |
| **Layer** | Application & Infrastructure (Persistence Ports & CQRS Commands) |
| **Decision Scope** | Data Integrity, Foreign Key Validation, Error Handling, Performance vs Clean Code |

---

## 1. السياق والمشكلة بالضبط (Exact Context & Problem Statement)

عند بناء أوامر الكتابة (Commands) التي تقوم بإنشاء أو ربط كيانات تعتمد على معرفات خارجية (Foreign Keys) — مثل أمر [CreateRoleCommand](file:///d:/dotnet/POS/src/Services/Identity/SuperMarket.Identity.Application/Roles/Commands/CreateRole/CreateRoleCommand.cs) الذي يستقبل قائمة باقات صلاحيات `PermissionGroupIds` لربطها بالدور الجديد — واجهنا السؤال المعماري الأكثر جدلاً في هندسة الـ Backend:

> **كيف نتحقق من أن المعرفات الخارجية (Foreign Keys) الممررة من العميل موجودة فعلياً في قاعدة البيانات قبل محاولة حفظ العلاقة؟**

المشكلة الحقيقية:
العميل (Frontend أو تطبيق خارجي) قد يرسل معرّفاً عشوائياً أو قديماً لحزمة صلاحيات محذوفة (`fake-guid`).
إذا قمنا بإنشاء كائن الربط `RolePermissionGroup` واستدعينا `SaveChangesAsync` مباشرة دون فحص مسبق، سترفض قاعدة البيانات (PostgreSQL) عملية الإدخال وترمي استثناء انتهاك قيد المفتاح الخارجي (`23503 Foreign Key Violation`).

هنا يظهر الصراع بين مدرستين:
1. هل نترك قاعدة البيانات تفحص بنفسها ونلتقط الـ Exception عند حدوثه؟
2. أم نقوم بعمل فحص استباقي (Pre-flight Check) سريع للتأكد من وجود المعرفات قبل محاولة الحفظ؟

---

## 2. التحديات (Competing Challenges & Forces)

واجهنا 4 قوى معمارية متعارضة تتطلب موازنة دقيقة:

1. **حماية الـ Exception Boundary ونقاء الكود:**  
   في معمارية Clean Architecture ونمط `Result Pattern`، الأخطاء الناتجة عن مدخلات غير صحيحة من العميل تُعتبر أخطاء بزنس متوقعة (Expected Failures)، ويجب ألا تتحول إلى استثناءات (Exceptions) غير منضبطة تؤدي إلى `500 Internal Server Error`.
2. **عدم تسريب تفاصيل قاعدة البيانات (Data Abstraction Leakage):**  
   إذا سمحنا للاستثناء بالخروج، فقد تتسرب أسماء الجداول والـ Constraints الداخلية في PostgreSQL (`FK_role_permission_groups_permission_groups_group_id`) إلى العميل، مما يشكل ثغرة استطلاع أمني (Information Disclosure).
3. **أداء اتصالات الشبكة وقاعدة البيانات (Database Roundtrips):**  
   عمل استعلام فحص إضافي قبل الحفظ يعني إرسال طلبين لقاعدة البيانات (طلب للفحص + طلب للحفظ) بدلاً من طلب واحد، مما قد يستهلك جزءاً من وقت الاستجابة (Latency) في الأنظمة ذات الضغط العالي جداً.
4. **منع هدر الذاكرة (Zero-Allocation Efficiency):**  
   لا نريد عند التحقق من وجود المعرفات أن نسحب جداول وكيانات كاملة إلى ذاكرة السيرفر (RAM) لمجرد التأكد من أن المعرّف موجود.

---

## 3. كيف ستحصل المشكلة بالضبط؟ (Failure Workflow Scenario)

لتوضيح خطورة إهمال هذا القرار، هذا سيناريو تفصيلي لما يحدث عملياً في بيئات الإنتاج لو اعتمدنا على قاعدة البيانات وحدها بدون فحص مسبق:

```
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 1: مستخدم يرسل طلب إنشاء دور ومعه معرف حزمة صلاحيات وهمي       │
│ POST /api/roles { "roleName": "Supervisor", "groupIds": ["fake-guid"] } │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 2: الـ Handler ينشئ كائنات الربط بدون فحص ويستدعي SaveChanges   │
│ var link = RolePermissionGroup.Create(role.Id, fakeGuid);              │
│ await _unitOfWork.SaveChangesAsync(cancellationToken);                 │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 3: انفجار استثناء في قاعدة البيانات                             │
│ PostgreSQL ترفض الإدخال وترمي:                                         │
│ NpgsqlException: 23503 foreign_key_violation on table role_permission_groups│
│ EF Core يغلفه بـ DbUpdateException وينهار مسار التنفيذ!                 │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 4: النتيجة الكارثية في الـ Production                            │
│ السيرفر يعيد 500 Internal Server Error للمستخدم كأن السيرفر سقط،       │
│ وسجلات الـ Log تمتلئ بـ Error Spikes وتنبيهات كاذبة لفريق الـ DevOps!   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. الحلول والبدائل مع المقايضات الصريحة (Trade-offs Analysis)

درسنا 3 خيارات معمارية رئيسية:

### الخيار 1: الحل المتفائل (Optimistic Insert & Catch DB Exception)
* **الوصف:** لا نفحص أي شيء مسبقاً، بل نقوم بعمل `Insert` مباشرة. وإذا رمى EF Core استثناء `DbUpdateException`، نفحص داخل الـ Exception عما إذا كان رمز خطأ PostgreSQL هو `23503` ونحوله إلى `Result.Failure`.
* **ماذا نكسب:**  
  - أقصى سرعة ممكنة: استعلام واحد فقط لقاعدة البيانات (Single Roundtrip) في 99% من الحالات السليمة.
* **ماذا نخسر (لماذا رُفض؟):**  
  - استخدام الـ Exceptions للتحكم بمسار الكود (Anti-pattern: Using Exceptions for Flow Control).
  - ربط طبقة التطبيق بأكواد واستثناءات خاصة بمزود محدد (`Npgsql` كود `23503`).
  - صعوبة معرفة أي معرف تحديداً هو الذي فشل إذا كان الجدول يحتوي على أكثر من Foreign Key.

### الخيار 2: نمط الـ Domain الكامل (Load Full Entities into RAM)
* **الوصف:** قراءة كائنات `PermissionGroup` كاملة من قاعدة البيانات وتحميلها في الذاكرة:  
  `var groups = await _context.PermissionGroups.Where(g => ids.Contains(g.Id)).ToListAsync();`
* **ماذا نكسب:**  
  - تطابق كامل مع كتب DDD التقليدية التي تفترض وجود الكيانات كاملة في الذاكرة.
* **ماذا نخسر (لماذا رُفض؟):**  
  - هدر كبير للذاكرة (High Memory Allocations) وسحب أعمدة ونصوص لا نحتاجها لمجرد التأكد من أن الـ ID موجود.

### الخيار 3: الفحص الاستباقي عالي الكفاءة عبر `AllExistAsync` — [الحل المختار]
* **الوصف:** إرسال استعلام فحص خفيف وسريع جداً (`COUNT(*) WHERE id IN (...)`) يفحص وجود المعرفات ويعيد رقماً واحداً فقط قبل محاولة الحفظ.
* **ماذا نكسب:**  
  - ✅ **حماية كاملة من الـ Exceptions:** العملية تعيد دوماً `Result.Failure` نظيف بدون أي انهيار للسيرفر وبدون `500 Error`.
  - ✅ **صفر استهلاك للذاكرة (Zero RAM Allocation):** لا نسحب أي سجلات؛ قاعدة البيانات تعيد رقماً صغيراً واحداً (Integer).
  - ✅ **رسالة خطأ واضحة:** إرجاع `PermissionGroupErrors.OneOrMoreNotFound` يترجم لـ `400 Bad Request` فوري للعميل.
* **ماذا نخسر (التكلفة المقبولة):**  
  - ⚠️ استعلام شبكي خفيف إضافي (`COUNT`) يسبق أمر الحفظ.

---

## 5. الحل الذي اخترناه وطريقة العمل بالتفصيل (The Chosen Architecture)

اعتمدنا رسمياً **نمط الفحص الاستباقي عالي الكفاءة (High-Performance Pre-flight Check)** عبر دالة `AllExistAsync` في [PermissionGroupRepository.cs](file:///d:/dotnet/POS/src/Services/Identity/SuperMarket.Identity.Infrastructure/Persistence/Repositories/PermissionGroupRepository.cs):

```csharp
public async Task<bool> AllExistAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(groupIds);

    var distinctIds = groupIds.Distinct().ToList();
    if (distinctIds.Count == 0)
    {
        return true;
    }

    var existingCount = await DbContext.PermissionGroups
        .CountAsync(g => distinctIds.Contains(g.Id), cancellationToken);

    return existingCount == distinctIds.Count;
}
```

### كيف يعمل هذا الاستعلام في PostgreSQL ولماذا هو فائق السرعة؟
1. **تصفية المعرفات الفريدة (`Distinct`):**  
   لضمان أن المقارنة العددية لا تتأثر إذا أرسل العميل معرفات مكررة.
2. **الترجمة إلى SQL محكم:**  
   يترجم محرك EF Core هذا السطر إلى استعلام SQL واحد دقيق ومباشر:
   ```sql
   SELECT COUNT(*)::int
   FROM identity.permission_groups AS p
   WHERE p.id IN (@id1, @id2, @id3);
   ```
3. **المقارنة الرياضية السريعة (`existingCount == distinctIds.Count`):**  
   - إذا طلبت 3 معرفات (`distinctIds.Count = 3`) وقاعدة البيانات وجدت 3 سجلات مطابقة، فإن الناتج `3 == 3` ويعيد `true` فوراً.
   - إذا كان أحد المعرفات غير موجود، فإن قاعدة البيانات ستجد 2 فقط، فيكون الناتج `2 == 3` ويعيد `false` فوراً.
4. **استخدام الناتج في الـ Handler:**  
   في ملف [CreateRoleCommandHandler.cs](file:///d:/dotnet/POS/src/Services/Identity/SuperMarket.Identity.Application/Roles/Commands/CreateRole/CreateRoleCommandHandler.cs):
   ```csharp
   if (command.PermissionGroupIds is not null && command.PermissionGroupIds.Count > 0)
   {
       var allGroupsExist = await _permissionGroupRepository.AllExistAsync(command.PermissionGroupIds, cancellationToken);
       if (!allGroupsExist)
       {
           return Result.Failure<Guid>(PermissionGroupErrors.OneOrMoreNotFound);
       }
   }
   ```

---

## 6. زاوية المقابلة التقنية (Senior Interview Defense)

لو سُئلت في مقابلة عمل هندسية:  
> **"عند ربط الكيانات بمفاتيح خارجية (Foreign Keys)، هل تفضل فحص وجود المفاتيح مسبقاً (Pre-flight Validation)، أم تعتمد على قيود قاعدة البيانات وتلتقط الاستثناء (Catch DB Exception)؟"**

### الإجابة النموذجية والمقنعة (تبدأ بالمشكلة والصراع أولاً):

> **شرح المشكلة والصراع:**  
> "هذه معضلة كلاسيكية بين **نقاء الكود (Clean Code & Error Handling)** وبين **تقليل استعلامات الشبكة (Minimizing Network Roundtrips)**.  
> إذا اعتمدنا على قاعدة البيانات وحدها وتركناها ترمي `Foreign Key Violation Exception (23503)`، فسنوفر استعلاماً، لكننا نقع في فخ استخدام الاستثناءات للتحكم بمسار البرنامج (Using Exceptions for Flow Control)، ونربط طبقة التطبيق باستثناءات مزود قاعدة بيانات محدد، ونخاطر برمي `500 Server Errors` للمستخدم على خطأ إدخال بسيط.  
>
> **قرارنا الهندسي:**  
> في نظامنا اخترنا **التحقق الاستباقي (Pre-flight Check)** باستخدام استعلام `SELECT COUNT(*) WHERE id IN (...)`.  
>
> **المقايضة ولماذا فضلناها:**  
> نحن ضحينا بطلب شبكي خفيف إضافي لا يتجاوز بضعة ميلي ثانية، لكننا كسبنا:  
> 1. حماية تامة لنمط `Result Pattern` حيث يعود الخطأ كـ `Result.Failure(OneOrMoreNotFound)` ويترجم لـ `400 Bad Request` نظيف ومحترم للمستخدم بدون رمي استثناءات.  
> 2. صفر استهلاك لذاكرة السيرفر، لأن الاستعلام يعيد فقط رقماً واحداً (`Integer`) ولا يسحب أي جداول للذاكرة.  
> 3. عزل طبقة التطبيق تماماً عن تفاصيل واستثناءات PostgreSQL."
