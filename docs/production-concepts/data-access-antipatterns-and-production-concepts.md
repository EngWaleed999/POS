# دليل المفاهيم الإنتاجية: أسرار الوصول للبيانات وأخطاء المبتدئين في .NET
## Production Concepts: EF Core Internals, Unit of Work & Common Anti-Patterns

> **الهدف من هذا الدليل:**  
> توثيق الكواليس العميقة لمحرك EF Core، وتوضيح أين وكيف تُعالج البيانات في الذاكرة والشبكة، وتفكيك أشهر الأخطاء الشائعة (Anti-Patterns) التي تروج لها الشروحات السطحية، مع توضيح الفروق الجوهرية لمن ينتقل من بيئة **Node.js** إلى **.NET Clean Architecture**.

---

## 1. أين وكيف تُخزن بيانات `Add` و `Remove` في الرام؟ (تحت غطاء EF Core)

سألت: *"أنت قلت إن Add و Remove يتم تخزينهما في الرام ثم يتم تجميعهما، أين يوجد هذا الكود وفي أي ملف؟"*

### الجواب:
هذا الكود ليس مكتوباً في ملف عادي داخل مشروعنا؛ **بل هو النواة الداخلية لمكتبة EF Core مفتوحة المصدر من مايكروسوفت (`Microsoft.EntityFrameworkCore.dll`).**

### الخريطة الداخلية لـ EF Core في الذاكرة:
عندما تستدعي في مشروعنا:
```csharp
_branchRepository.Add(branch);
```
فإن هذا الاستدعاء يمر بالمكونات الداخلية التالية داخل ذاكرة التطبيق (RAM):

```
[كود التطبيق: Application]
      │
      │ 1. استدعاء Add(branch)
      ▼
[DbSet<Branch>] (في EF Core)
      │
      │ 2. SetEntityState(branch, EntityState.Added)
      ▼
[ChangeTracker] (محرك مراقبة الحالة في الذاكرة)
      │
      ▼
[StateManager.cs] (مدير الحالة الداخلي)
      │
      │ يحفظ الكيان داخل قاموس في الذاكرة:
      │ Dictionary<object, InternalEntityEntry> _entries;
      ▼
[النتيجة في الرام فقط]:
┌────────────────────────────────────────────────────────────┐
│ Entity: Branch { Id: 123, Name: "صنعاء" }                 │
│ State:  EntityState.Added                                  │
│ IsPersistedInDatabase: FALSE                               │
│ Network Requests Sent: 0                                   │
└────────────────────────────────────────────────────────────┘
```

### متى وكيف يتم التجميع والإرسال إلى PostgreSQL؟
يحدث هذا فقط في ملف:
`IdentityDbContext.SaveChangesAsync()` (الذي استدعيته عبر `_unitOfWork.SaveChangesAsync()`).

**ماذا يحدث في ذلك الكسر من الثانية؟**
1. **DetectChanges (الكشف عن التغييرات):** يمر الـ `StateManager` على جميع الكائنات الموجودة في القاموس في الذاكرة.
2. **Compilation إلى SQL:** يحول الكائنات التي حالتها `Added` إلى `INSERT`، والكائنات `Modified` إلى `UPDATE`، والكائنات `Deleted` إلى `DELETE`.
3. **Batch Execution (التجميع الذكي في طلب واحد):** عبر كلاس في EF Core اسمه `IModificationCommandBatchFactory`:
   بدلاً من إرسال 10 استعلامات منفصلة عبر كابل الشبكة، يقوم بدمجها كلها في **Network Payload واحدة**:
   ```sql
   BEGIN TRANSACTION;
     INSERT INTO branches (id, name, ...) VALUES (...);
     INSERT INTO branch_operating_hours (id, branch_id, ...) VALUES (...);
   COMMIT;
   ```
4. **التنفيذ:** تُرسل هذه الحزمة دفعة واحدة إلى PostgreSQL، وتُنفذ ذرياً في ميكرو-ثوانٍ.

---

## 2. أشهر 5 أخطاء وفخاخ قاتلة يقع فيها المبتدئون (Anti-Patterns)

---

### الخطأ القاتل #1: وضع `SaveChangesAsync` داخل كل دالة CRUD
**الكود الكارثي المنتشر في دورات المبتدئين:**
```csharp
// ❌ خطأ فادح في بيئة الإنتاج:
public async Task AddAsync(Branch branch)
{
    await _context.Branches.AddAsync(branch);
    await _context.SaveChangesAsync(); // 👈 الكارثة هنا!
}

public async Task RemoveAsync(Branch branch)
{
    _context.Branches.Remove(branch);
    await _context.SaveChangesAsync(); // 👈 وهنا أيضاً!
}
```

#### لماذا هذا الكود يُدمر الأنظمة الإنتاجية؟
1. **تدمير مبدأ الـ Unit of Work تماماً:**
   الـ Unit of Work وظيفته تجميع عدة تغييرات وإرسالها معاً. بوضعك للحفظ داخل الـ Repository، أصبحت كل عملية معزولة وتنفصل عن غيرها.
2. **غياب الذرية (Loss of Atomicity & Data Corruption):**
   تخيل سيناريو: "تسجيل موظف جديد وتعيين صلاحياته وخصم رسوم التسجيل".
   - تم استدعاء `userRepo.AddAsync()` ← **تم الحفظ في DB**.
   - تم استدعاء `permissionRepo.AddAsync()` ← **فشل السيرفر أو انقطعت الشبكة!**
   - **النتيجة:** أصبح لديك مستخدم في قاعدة البيانات بدون صلاحيات! أصبحت الداتابيز فاسدة وغير متسقة.
3. **الثرثرة الشبكية القاتلة للأداء (Chatty I/O):**
   بدل أن ترسل العملية كاملة في رحلة شبكية واحدة (1 Roundtrip)، أرسلت 4 أو 5 اتصالات متتالية، مما يرفع زمن الاستجابة (Latency) ويستهلك Connection Pool الخاص بقاعدة البيانات سريعاً.

---

### الخطأ القاتل #2: استخدام `Task AddAsync` بدلاً من `void Add`
**السؤال الشائع:** *"التعامل مع الداتابيز بطيء، لماذا نجعل دالة Add متزامنة (Synchronous) بدون Task؟"*

#### الحقيقة التقنية:
دالة `Add` في EF Core **لا تتصل بالشبكة ولا بالداتابيز أبداً**. هي فقط تضيف مؤشر الكائن في `Dictionary` داخل الذاكرة (RAM)، وهذه العملية تأخذ **أقل من 2 ميكرو ثانية**.

#### لماذا حذرت مايكروسوفت من `AddAsync`؟
في توثيق مايكروسوفت الرسمي لـ EF Core:
> *"Use `AddAsync` only when using value generators that need asynchronous database access (such as HiLo sequence generator). In all other cases, use the synchronous `Add` method. Using `AddAsync` unnecessarily introduces overhead by allocating an async Task state machine in memory."*

بما أننا نستخدم معرفات `Guid` نولدها في التطبيق، فإن استخدام `AddAsync` يستهلك الذاكرة لإنشاء Task State Machine في الرام لعملية لا تنتظر أي I/O!

---

### الخطأ القاتل #3: الإصرار على كتابة واستدعاء دالة `Update`
**السلوك الخاطئ:**
```csharp
// ❌ استدعاء غير ضروري ومضر:
var branch = await _branchRepository.GetByIdAsync(id);
branch.Rename("الفرع الجديد");
_branchRepository.Update(branch); // 👈 استدعاء Update هنا فخ!
await _unitOfWork.SaveChangesAsync();
```

#### لماذا هو فخ؟
* في ASP.NET Core، الطلب يعمل في بيئة متصلة (**Connected Scenario**). الكيان الذي جلبته بـ `GetByIdAsync` يكون متتبعاً تلقائياً عبر الـ **Snapshot Change Tracker**.
* عند استدعاء `SaveChangesAsync`، يقارن EF Core الحالة الأصلية بالحالة الجديدة، ويكتشف أن **عمود الاسم فقط هو الذي تغير**.
* فيولد SQL دقيق:
  ```sql
  UPDATE branches SET name = @newName WHERE id = @id;
  ```
* **لكن لو استدعيت `_context.Update(branch)`:**
  أنت تجبر الـ Change Tracker على وضع علامة `Modified` على **جميع أعمدة الجدول بلا استثناء**! فيرسل استعلاماً ضخماً يحدث رقم الهاتف والضريبة والعنوان وتاريخ الإنشاء حتى لو لم يلمسها أحد.

---

### الخطأ القاتل #4: نسيان `AsNoTracking()` في استعلامات القراءة
**السلوك الخاطئ:**
استخدام `IQueryable` أو الـ DbContext في شاشات العرض وقوائم الجداول دون إيقاف التتبع.

#### ما الثمن الذي يدفعه السيرفر؟
1. **استهلاك الذاكرة (Memory Bloat):** لكل سجل يُجلب من قاعدة البيانات، يقوم EF Core بإنشاء كائنين في الرام: الكائن الأصلي، ونسخة Snapshot منه لمراقبة التغييرات.
2. **ضغط على الـ Garbage Collector:** عند قراءة 1,000 سجل في تقرير، يحجز الـ Change Tracker آلاف الكائنات في الذاكرة مما يسبب بطء النظام بالكامل وتجمده المؤقت أثناء تنظيف الرام (GC Pauses).
3. **الحل الاحترافي:** في جهة القراءة (Queries)، استخدام `AsNoTracking()` يوفر ما بين **40% إلى 60% من استهلاك الذاكرة والوقت**.

---

### الخطأ القاتل #5: وهم الأمان عبر الـ Mocking الكاذب لقواعد البيانات
**السلوك الخاطئ:**
عمل Mock لـ `DbSet` أو `IRepository` وظن أن الاختبار الأخضر يعني أن الكود سيعمل على سيرفر الإنتاج.

#### ماذا يخفي الـ Mock عنك؟
* **أخطاء ترجمة LINQ:** دوال مثل `.ToString()` داخل استعلام LINQ ستعمل بنجاح في الـ Mock، لكن في Postgres الحقيقي ستفجر التطبيق بخطأ: `InvalidOperationException: The LINQ expression could not be translated`.
* **انتهاك الـ Unique Indexes:** عند إرسال كود فرع مكرر في نفس اللحظة (Race Condition)، الـ Mock لن يكشف التكرار، بينما سيرفر Postgres سيرمي `DbUpdateException`.

---

## 3. التحول الفكري: من عالم Node.js إلى .NET Clean Architecture

| وجه المقارنة | عالم Node.js (غالباً: Prisma / Drizzle / Knex) | عالم .NET (Clean Architecture + EF Core) |
| :--- | :--- | :--- |
| **طبيعة الـ ORM** | **عديم الحالة (Stateless):** الاستعلام يرجع Plain Object (POJO) ينفصل عن الداتابيز فوراً. | **ممتلئ بالحالة (Stateful):** الكيان يظل محجوزاً في الـ Identity Map والـ Change Tracker. |
| **التتبع (Tracking)** | كل شيء هو `NoTracking` افتراضياً 100%. | التتبع مفعّل افتراضياً لحماية دورة حياة الكيان، ونعطله عمداً بـ `AsNoTracking`. |
| **أسلوب التعديل** | **صريح (Explicit):** مجبر دائماً على كتابة `prisma.user.update(...)`. | **ضمني (Implicit/Dirty Checking):** تعدل الكيان ككائن C#، و `SaveChanges` تكتشف التعديل وتكتب الـ SQL. |
| **حدود المعاملات** | تستخدم `$transaction([...])` يدوياً لكل عملية مركبة. | نمط الـ **Unit of Work** مدمج في صميم `DbContext`؛ كل عمليات الطلب تندمج في Transaction واحدة تلقائياً. |
| **نموذج البيانات** | **Anemic Data Model:** مجرد Interfaces أو Schemas خالية من أي Logic. | **Rich Domain Model:** الكيان يغلق خصائصه (`private set`) ويحمي قواعد البيزنس بدواله الخاصة. |

---

## 4. قاموس المفاهيم الذهبية للمهندس (Senior Cheat Sheet)

1. **Unit of Work:**  
   هو النمط المسؤول عن مراقبة كل الكيانات المعدلة في جلسة العمل، وضمان إرسالها وحفظها في قاعدة البيانات في **دفعة واحدة (Batch)** ومعاملة واحدة (Atomic Transaction).
2. **Change Tracker:**  
   المحرك الداخلي في الذاكرة الذي يعمل كـ "كاميرا مراقبة" تسجل أي تغيير يحدث على خصائص الكيانات المتصلة به.
3. **Dirty Checking:**  
   المقارنة التلقائية التي يقوم بها EF Core بين الحالة الأصلية للكيان (Snapshot) وحالته الحالية لمعرفة الأعمدة التي تغيرت فقط دون غيرها.
4. **Chatty I/O:**  
   مشكلة معمارية تحدث عند إرسال عدة استدعاءات شبكية صغيرة ومتتالية لقاعدة البيانات بدلاً من تجميعها وإرسالها دفعة واحدة.
5. **Connected Scenario:**  
   النمط المتبع في الـ Web APIs الحديثة: استرجاع الكيان، تعديل حالته في الذاكرة، ثم حفظه في نفس نطاق الطلب (`Scoped DbContext`).
