# 🛡️ استراتيجية الاستعلام، التفويض، والتخزين المؤقت للصلاحيات
## Authorization, Query Execution, and Caching Strategy Master Document

* **الحالة:** معتمد كمرجع معماري للتنفيذ (Approved Architectural Guide)
* **التاريخ:** 2026-09-23
* **المشروع المستهدف:** `SuperMarket.Identity` (Application & Infrastructure Layers)
* **المكونات المرتبطة:** `Role`, `Permission`, `PermissionGroup`, `IdentityDbContext`, `JwtBearer`, `HybridCache`

---

## 1. الغرض من الوثيقة (Purpose & Context)

عند الانتقال من طبقة الدومين (`Domain Layer`) إلى طبقتي التطبيق (`Application`) والبنية التحتية (`Infrastructure`)، تبرز قرارات حرجة تتعلق بكيفية قراءة وفحص الصلاحيات بكفاءة فائقة تناسب بيئة نقاط البيع (POS System).

تهدف هذه الوثيقة إلى حفظ المعرفة المعمارية المتفق عليها والإجابة على الأسئلة الحتمية للتنفيذ:
1. كيف نستعلم عن الصلاحيات في EF Core دون تلويث نقاء كائنات الـ Domain بـ `Navigation Properties`؟
2. أين وكيف يتم فحص الصلاحيات في كل طلب HTTP؟
3. هل نضع الصلاحيات داخل الـ JWT Token أم نستعلم قاعدة البيانات أم نستخدم الـ Cache؟
4. ما هو الفارق العملي بين الـ In-Memory Cache (RAM) و Redis في نظامنا؟
5. ما هو دور الـ DTOs والـ Category في طبقة التطبيق والواجهات؟

---

## 2. معمارية الـ RBAC في الدومين والملاحة في EF Core

### أ. لماذا تم تجريد كائنات الـ Domain من الـ Navigation Properties؟
في كلاسات الدومين (`Role.cs`, `PermissionGroup.cs`):
* **تطبيق قاعدة DDD الصارمة:**
  > *"Reference other Aggregates by ID only, never by direct object reference."*
* **السبب الهندسي:**
  - منع ظاهرة **(Object Graph Trap)**: لو احتوى الكيان `Role` على `List<RolePermissionGroup>`، فبمجرد تحميل الدور في الذاكرة لتعديل اسمه أو حالته، سيقوم الـ ORM بتحميل مئات الكائنات المرتبطة في الـ RAM والـ Change Tracker.
  - الحفاظ على حدود المعاملات (`Transactional Consistency Boundaries`)؛ فتعديل الدور مستقل تماماً عن تعديل مجموعات الصلاحيات.

### ب. كيف يتم الاستعلام في EF Core (Infrastructure Layer)؟
في طبقة الـ Infrastructure، يتم تمثيل جداول الربط كـ `DbSet` مستقل داخل الـ `IdentityDbContext`:
```csharp
public DbSet<Role> Roles => Set<Role>();
public DbSet<Permission> Permissions => Set<Permission>();
public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
public DbSet<PermissionGroupItem> PermissionGroupItems => Set<PermissionGroupItem>();
public DbSet<RolePermissionGroup> RolePermissionGroups => Set<RolePermissionGroup>();
```

#### الاستعلام المثالي عالي الأداء (LINQ with Direct DTO Projection):
```csharp
public async Task<List<string>> GetPermissionsByRoleIdAsync(Guid roleId, CancellationToken ct)
{
    return await (from rpg in _context.RolePermissionGroups
                  join pgi in _context.PermissionGroupItems on rpg.GroupId equals pgi.GroupId
                  join p in _context.Permissions on pgi.PermissionId equals p.Id
                  where rpg.RoleId == roleId
                  select $"{p.Resource}:{p.Action}:{p.Scope}")
                  .AsNoTracking()
                  .Distinct()
                  .ToListAsync(ct);
}
```
* **مميزات هذا الاستعلام:**
  1. `AsNoTracking()`: يلغي تتبع الكيانات ويوفر 100% من استهلاك الـ Change Tracker.
  2. `Direct Projection`: يطلب فقط الأعمدة الثلاثة (`resource`, `action`, `scope`) ولا يحمل الكيانات كاملة في الـ Memory.
  3. كفاءة ترجمة SQL: يُترجم مباشرة إلى استعلام SQL نقي ومفهرس لا يتعدى تنفيذه `0.3ms` على PostgreSQL.

---

## 3. حقيقة كلفة الـ Joins وأداء قاعدة البيانات (Reality vs Myth)

| وجه المقارنة | التصميم المباشر (Flat RBAC) | تصميم المجموعات الوسيطة (Group RBAC) |
|:---|:---|:---|
| **عدد الـ Joins** | 1 Join | 3 Joins |
| **عدد السجلات في الجداول** | 50 – 200 سجل | 100 – 500 سجل |
| **حجم البيانات في التخزين** | < 50 KB | < 150 KB |
| **زمن التنفيذ في PostgreSQL** | ~ 0.15 ms | ~ 0.35 ms |
| **المرونة الإدارية وإعادة الاستخدام** | ضعيفة جداً وتكرار يدوي | فائقة وتجميع في حزم وظيفية |

* **الخلاصة الهندسية:** فارق الـ `0.2ms` بين 1 Join و 3 Joins هو فارق نظري لا يشكل أي عنق زجاجة لقاعدة البيانات لأن الجداول خفيفة جداً ومحملة مسبقاً في الـ Shared Buffer Cache لقاعدة البيانات.
* **الأهم معمارياً:** هذا الاستعلام يُنفذ **مرة واحدة فقط عند تسجيل الدخول (Login)**، ولا يُكرر مع كل عملية بيع!

---

## 4. أنماط استهلاك وفحص الصلاحيات (Authorization Execution Patterns)

### النمط الأول: وضع الصلاحيات في التوكن (Stateless Token Claims) — [الموصى به لـ POS]
* **كيف يعمل؟**
  1. يسجل الكاشير دخوله عبر PIN (`AuthenticateCashierPinCommand`).
  2. يُنفذ استعلام الـ Joins مرة واحدة فقط، وتُجمع أسماء الصلاحيات في مصفوفة نصوص.
  3. تُحشى الصلاحيات داخل الـ JWT Payload كـ Claims:
     ```json
     {
       "sub": "user-guid-001",
       "role": "Cashier",
       "branch_id": "branch-guid-101",
       "permissions": [
         "sales:create:branch",
         "sales:read:own",
         "shifts:open:own",
         "shifts:close:own"
       ]
     }
     ```
  4. في كل الـ 10,000 طلب بيع اللاحقة للكاشير خلال ورديته:
     - الـ API يقرأ التوكن ويفحص الصلاحيات **محلياً في الذاكرة في زمن < 0.001ms**.
     - **صفر اتصالات بقاعدة البيانات، وصفر اتصالات بـ Redis!**
* **متى نستخدمه؟**
  - عندما يكون عدد صلاحيات الدور معقولاً (< 30 إلى 40 صلاحية)، ليبقى حجم التوكن صغيراً (< 2KB) ولا يثقل ترويسات الـ HTTP.

---

### النمط الثاني: التخزين المؤقت الموزع (Cache-Backed Authorization)
* **متى نحتاجه؟**
  - إذا كان الدور يمتلك أكثر من 80 صلاحية وحجم الـ JWT أصبح كبيراً ويثقل الشبكة.
  - هنا يحتوي التوكن على `role_id` فقط، ويتم جلب الصلاحيات من الـ Cache.

#### مقارنة حاسمة: الـ RAM المدمج (`IMemoryCache`) مقابل خادم `Redis`

```
┌────────────────────────────────────────────────────────────────────────┐
│                   In-Memory Cache (Application RAM)                    │
│   • يعيش داخل الـ Process الخاص بـ .NET                                  │
│   • السرعة: خارقة (نانوثواني - Nanoseconds)                             │
│   • التكلفة: صفر بنية تحتية إضافية                                      │
│   • المشكلة: منعزل محلياً؛ التعديل على سيرفر لا يراه السيرفر الآخر      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      Distributed Cache (Redis)                         │
│   • يعيش في سيرفر / حاوية مستقلة متصلة بالشبكة                          │
│   • السرعة: سريعة جداً (1-2 ميلي ثانية عبر الشبكة)                      │
│   • التكلفة: تشغيل حاوية وإدارة اتصالات ومساحة ذاكرة                    │
│   • الميزة: Single Source of Truth؛ كاش موحد لجميع الـ API Replicas     │
└────────────────────────────────────────────────────────────────────────┘
```

#### الحل الهجين المعتمد مستقبلاً (.NET 9/10 `HybridCache`):
عند التوسع لعدة سيرفرات، سنستخدم `Microsoft.Extensions.Caching.Hybrid`:
- يقرأ أولاً من الـ RAM المحلي (L1 Cache) بسرعة النانوثانية.
- إذا لم يجد المفتاح، يسأل Redis (L2 Cache).
- عند تعديل الصلاحيات، يقوم بنشر إشعار إبطال آلي (Invalidation) لكل السيرفرات عبر Redis Pub/Sub.

---

## 5. استخدام الـ DTOs في طبقة التطبيق (Application Layer)

تلتزم طبقة التطبيق بالقواعد المعمارية التالية:
1. **عدم تسريب كيانات الدومين:** يُمنع إرجاع `Role` أو `Permission` إلى الـ Controllers.
2. **الـ DTOs المعتمدة:**
   - `RoleDto(Guid Id, string RoleName, string? Description, bool IsActive)`
   - `PermissionDto(Guid Id, string Resource, string Action, string Scope, string Category)`
   - `PermissionGroupDto(Guid Id, string GroupName, string? Description, List<PermissionDto> Items)`
   - `UserStaffResponseDto(...)`

---

## 6. وظيفة ومكان استخدام الصلاحيات والـ `Category`

### أ. في أي طبقة تُستخدم الصلاحيات؟
1. **في طبقة الـ API (التحكم في المداخل):**
   فوق الـ Endpoints باستخدام Policy أو Custom Attribute:
   ```csharp
   [HasPermission("sales:create:branch")]
   [HttpPost("checkout")]
   public async Task<IResult> CheckoutInvoice(...)
   ```
2. **في طبقة الـ Application (حماية مسار المعالجة):**
   عبر MediatR Pipeline Behavior (`AuthorizationPipelineBehavior`):
   - يفحص هل الـ Command يطبق واجهة `IRequirePermission`؟
   - يتحقق من وجود الصلاحية في claims المستخدم الحالي؛ وإذا غابت، يقطع الطلب فوراً ويعيد `Result.Failure(Error.Forbidden)`.

### ب. ماذا تمثل `Category` بالضبط؟
الصلاحية الذرية تتكون من: `resource:action:scope`.
أما `Category` فهي **تصنيف إداري وظيفي (Grouping / UI Categorization)** (مثل: `Sales`, `Inventory`, `StaffManagement`, `Finance`, `Security`).

#### فوائدها الميدانية:
1. **في واجهات المستخدم (Admin Dashboard UI):**
   - تنظيم عرض 80+ صلاحية في واجهة مدير النظام داخل **Tabs أو Accordions** مقسمة حسب القسم الوظيفي، بدلاً من قائمة مسطحة طويلة ومشتتة.
2. **في إنشاء المجموعات (Permission Groups):**
   - تمكين مدير النظام من تحديد كافة صلاحيات قسم معين بضغطة زر واحدة لإضافتها إلى حزمة وظيفية.

---

## 7. ملخص القرارات للتنفيذ الفوري

1. **الـ Domain:** يظل نقياً بدون Navigation Properties.
2. **الـ Infrastructure:** استعلام الصلاحيات يكتب بـ LINQ Joins صريح ومسقط مباشرة إلى نصوص بـ `AsNoTracking()`.
3. **الـ Auth Flow:** الصلاحيات تُحشى داخل الـ **JWT Token Claims** عند الـ Login، ولا يتم استعلام الـ DB أو الـ Cache في طلبات البيع اليومية.
4. **الـ DTOs:** كل مخرجات الـ Handlers تكون كائنات DTO نقية ومحكمة.
