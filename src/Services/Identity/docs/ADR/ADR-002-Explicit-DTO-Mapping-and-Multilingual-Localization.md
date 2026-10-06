# ADR-002: استراتيجية تحويل البيانات (Explicit DTO Mapping) ومعمارية تعدد اللغات (Localization Strategy)

| Metadata | Details |
| :--- | :--- |
| **Status** | ✅ Accepted |
| **Date** | 2026-10-06 |
| **Author** | Senior Backend Engineer / Architecture Reviewer & Technical Partner |
| **Service** | SuperMarket.Identity |
| **Layer** | Application, Domain & API Presentation |
| **Decision Scope** | Object Mapping, CQRS Boundaries, Domain Encapsulation, i18n & l10n Architecture |

---

## 1. السياق والمشكلة (Context & Problem Statement)

عند بناء أولى الـ Use Cases في طبقة التطبيق (`CreateBranch`) وإعداد استعلامات القراءة (Queries) إلى جانب الأوامر (Commands)، واجهنا معضلتين معماريتين متكررتين في مشاريع الـ Enterprise:

### المعضلة الأولى: استراتيجية التحويل بين الـ Entities والـ DTOs
* المعتاد في مشاريع .NET التقليدية هو جلب مكتبة مثل `AutoMapper` لنسخ الحقول آلياً بين الكائنات (`AutoMapper.Map<BranchDto>(branch)`).
* في المقابل، نطبق في هذا المشروع **Domain-Driven Design (DDD)** صارماً: كائنات الدومين مغلفة (`Private Setters`) ولا يمكن إنشاؤها إلا عبر دوال مصانع (`Branch.Create`) تفحص قواعد العمل وتعيد `Result<T>`.
* كما نطبق **CQRS**؛ حيث تفترق احتياجات الكتابة (تغيير حالة محمي بقواعد العمل) عن احتياجات القراءة (عرض شاشات وتقارير سريعة بأقل استهلاك للموارد).
* **السؤال المعماري:** هل نستخدم مكتبة Object Mapper مثل `AutoMapper`، أم نعتمد التحويل الصريح المباشر (Explicit Mapping)؟ وما هي التبعات على الأداء (Allocations) والأمان البرمجي (Compile-time Safety)؟

### المعضلة الثانية: دعم اللغة العربية والإنجليزية (Multilingual Localization)
* النظام حالياً يستخدم اللغة الإنجليزية في مسميات الكود وتعريفات الأخطاء (`BranchErrors.cs`).
* أنظمة نقاط البيع (POS) وتجارة التجزئة في البيئة العربية تتطلب دعماً إلزامياً للغة العربية (شاشات الكاشير، الإيصالات، رسائل الأخطاء للعميل).
* **الخطر المعماري:** الوقوع في فخ حشر نصوص مترجمة وقواميس لغات داخل طبقة الدومين (`Domain Layer`)، مما يلوث منطق العمل بتفاصيل واجهة المستخدم واللغات البشرية.
* **السؤال المعماري:** كيف نصمم دعم تعدد اللغات ليكون قابلاً للتوسع مستقبلاً دون المساس بنواة الدومين؟

---

## 2. البدائل التي تم تقييمها (Options Considered)

### أولاً: خيارات تحويل الـ DTOs (DTO Mapping Options)

#### الخيار 1: استخدام AutoMapper التقليدي المبني على Reflection
* **الوصف:** تسجيل Profiles لـ AutoMapper ونسخ الخصائص تلقائياً في كل الاتجاهات.
* **لماذا رُفض؟**
  1. **كسر التغليف في الـ Commands:** الـ Aggregate Roots ليست حاويات بيانات فارغة. محاولة استخدام AutoMapper مع `Branch.Create(...)` تجبر المطور على فتح الـ Setters أو كتابة Custom Type Converters معقدة ومربكة.
  2. **أخطاء Runtime بدلاً من Compile-Time:** إذا أعيدت تسمية خاصية أثناء Refactoring، لن ينبهك الـ Compiler، وسينفجر التطبيق في وجه المستخدم بـ `AutoMapperMappingException`.
  3. **استهلاك موارد غير مبرر (Allocation & Reflection Overhead):** استخدام Reflection و Boxing/Unboxing لكل كائن في كل طلب يقلل الإنتاجية تحت الضغط العالي (High Throughput).
  4. **صعوبة الـ Debugging:** التحويل يعمل كصندوق أسود (Black Box) يستحيل تتبعه بزر `F11`.

#### الخيار 2: استخدام مكتبات التوليد في وقت الترجمة (Source Generators مثل Riok.Mapperly)
* **الوصف:** مكتبات تولد كود C# صريح أثناء الـ Build بدون Reflection.
* **التقييم:** ممتازة جداً وأسرع من AutoMapper بمراحل، وتوفر Compile-time Safety، لكنها تظل تبعية خارجية (External Dependency) إضافية لسنا بحاجة إليها طالما أن استعلاماتنا تتم عبر LINQ Projections الصريحة.

#### الخيار 3: التحويل الصريح بالاعتماد على CQRS (Explicit Manual Mapping & LINQ Projections) — [المختار]
#### كيف تجيب باحترافية (The Senior Architect Answer):

> "قسمت قراري بناءً على نمط CQRS:
> 
> **1. في جانب الكتابة (Commands / Writes):**
> نحن نطبق Domain-Driven Design (DDD). الـ Aggregate Roots لدينا مغلفة ومحمية (Private Setters و Encapsulation) ويتم إنشاؤها عبر دوال مصانع (`Branch.Create`) تفحص قواعد البزنس (Invariants).
> مكتبات مثل AutoMapper صُممت للنسخ الأعمى للخصائص (`prop = prop`)، واستخدامها مع كائنات الدومين يكسر الـ Encapsulation أو يجبرك على كتابة Configurations معقدة وغير ضرورية. لذلك نستدعي Factory Method بالـ Parameters الصريحة في سطرين نظيفين ومحميين في الـ Compile-Time.
>
> **2. في جانب القراءة (Queries / Reads):**
> استخدمت **Direct LINQ Projection (`.Select()`)** مباشرة من الـ DbContext إلى الـ DTO.
> هذا يجعل الـ ORM يترجم الاستعلام مباشرة إلى SQL يجلب فقط الأعمدة المحددة في الـ DTO (منع مشكلة الـ Over-fetching).
> هذا الخيار أعطانا:
> 1. صفر Reflection وصفر استهلاك للذاكرة (Zero-Allocation).
> 2. حماية كاملة في الـ Compile-Time (لو تغير اسم عمود في الـ DTO، الـ Compiler سيكتشفه فوراً ولن ينفجر في الـ Production كما يفعل AutoMapper)."

#### متى نستخدم Mapper بالفعل؟ (متى يكون له مبرر؟)
1. **في الأنظمة القديمة أو الـ Anemic Models:** عندما يكون لديك كائنات CRUD تحتوي 40 حقل مسطح بدون قواعد دومين معقدة.
2. **عند التحويل بين عقود طرف ثالث (Third-Party APIs):** عندما تستقبل JSON خارجي ضخم به عشرات الحقول المعقدة وتريد تحويله لـ DTO داخلي.
3. **إذا استخدمنا تقنية حديثة مثل `Riok.Mapperly`:** وهي مكتبة لا تعتمد على Reflection، بل تستخدم **C# Source Generators** لتوليد كود C# عادي أثناء الـ Build.


---

### ثانياً: خيارات معمارية اللغات (Localization Options)

#### الخيار 1: حشر النصوص العربية والإنجليزية داخل الـ Domain Errors
* **الوصف:** تخزين النصوص المترجمة مباشرة في كلاسات الأخطاء (مثل `Error.Conflict("الفرع مكرر / Branch duplicated")`).
* **لماذا رُفض؟** خرق صارم لمبدأ Separation of Concerns؛ الدومين مسؤول عن منطق البزنس وليس عن طريقة عرض الرسالة للمستخدم النهائي.

#### الخيار 2: معمارية اللغات ثلاثية الطبقات (Layered 3-Tier Localization Architecture) — [المختار]
* **الوصف:** جعل نواة الدومين محايدة لغوياً بالكامل (Language-Agnostic)، وتوزيع مسؤولية الترجمة على 3 مستويات واضحة.

---

## 3. القرار المعماري (The Decisions)

### القرار 1: رفض AutoMapper واعتماد التحويل الصريح المقسم بـ CQRS

اعتمدنا رسمياً **عدم إدخال أي مكتبة Object Mapping تقليدية**، وتطبيق الاستراتيجية التالية:

```text
                       ┌───────────────────────────────────────────────┐
                       │          CQRS DTO Strategy                    │
                       └──────────────────────┬────────────────────────┘
                                              │
                    ┌─────────────────────────┴─────────────────────────┐
                    ▼                                                   ▼
         [جانب الكتابة - Commands]                           [جانب القراءة - Queries]
      • استدعاء صريح لدوال المصنع                       • Direct LINQ Projection (.Select)
      • Branch.Create(code, name, ...)                  • يُترجم مباشرة إلى SQL دقيق
      • يحمي الـ Invariants والـ Encapsulation           • يمنع الـ Over-fetching
      • أمان كامل في Compile-Time                       • صفر استهلاك للذاكرة (Zero-Allocation)
```

1. **في جانب الكتابة (Commands / Writes):**
   * الـ Command DTO يحمل أنواعاً أولية (`Primitive Types: string, int, Guid`).
   * الـ Handler يستدعي مصانع الـ Value Objects والـ Entity يدوياً:
     ```csharp
     var branchResult = Branch.Create(code, command.Name, address, command.Phone, ...);
     ```
   * لا يوجد أي كود وسيط ميكانيكي؛ الكود صريح بنسبة 100%، يخضع لفحص الـ Compiler، ويحترم التغليف.

2. **في جانب القراءة (Queries / Reads):**
   * نستعلم مباشرة عبر `IIdentityReadDbContext` مع الإسقاط الصريح (LINQ Projection):
     ```csharp
     var dtos = await _readDb.Branches
         .Where(...)
         .Select(b => new BranchSummaryDto(b.Id, b.Name, b.Code))
         .ToListAsync(ct);
     ```
   * يترجم الـ EF Core هذا الإسقاط مباشرة إلى SQL يحتوي فقط على الأعمدة المطلوبة (`SELECT id, name, code FROM branches`).

---

### القرار 2: تطبيق معمارية اللغات ثلاثية المستويات (3-Tier Localization)

لضمان دعم اللغتين العربية والإنجليزية مستقبلاً دون المساس بنواة النظام، اعتمدنا التقسيم المعماري التالي:

```text
┌────────────────────────────────────────────────────────────────────────┐
│ المستوى 1: الدومين محايد لغوياً (Language-Agnostic Core)               │
│ • يطلق رموز أخطاء آلية ثابتة: "Branch.CodeAlreadyExists"               │
│ • النص الإنجليزي المرفق هو مجرد Fallback تقني للمطورين.               │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ المستوى 2: طبقة الـ API Presentation (الترجمة عند الحدود الخارجية)     │
│ • فحص ترويسة الطلب: Accept-Language: ar-YE أو en-US                    │
│ • استخدام ASP.NET Core Localization (IStringLocalizer أو قاموس JSON)   │
│ • تحويل الكود "Branch.CodeAlreadyExists" إلى نص عربي للواجهة.           │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ المستوى 3: بيانات الدومين متعددة اللغات (Multilingual Business Data)   │
│ • تخزين الأسماء التجارية بالحقول المزدوجة (NameAr / NameEn)           │
│ • أو عبر Value Object مخصص: LocalizedName { Ar, En } للطباعة والفواتير│
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. الفوائد والمكاسب التقنية (Rationale & Benefits)

1. **الأداء الفائق ومبدأ Zero-Allocation:**
   * التخلص من الـ Reflection الخاص بـ AutoMapper وفر دورات معالجة معتبرة لكل طلب.
   * الـ LINQ Projection يمنع مشكلة **Over-fetching** الشائعة في الـ ORMs، حيث لا تُجلب من PostgreSQL إلا الأعمدة المحددة في الـ DTO.
2. **الأمان الصارم أثناء التطوير (Compile-time Safety):**
   * أي تعديل في أسماء الحقول أو أنواعها يتم كشفه فوراً في محرر الأكواد والـ Build، مما يقضي على فئة كاملة من أخطاء الـ Runtime المباغتة في الإنتاج.
3. **وضوح الكود وقابلية الصيانة (Clean Code & Debuggability):**
   * يستطيع أي مطور تتبع مسار البيانات بالضغط على `F12` أو التنفيذ خطوة بخطوة بالـ `F11` دون المرور بدهاليز الـ Expression Trees المعقدة.
4. **توسع نظيف للغات (Decoupled i18n):**
   * بقاء الدومين معتمداً على **Error Codes** ثابتة يتيح إضافة لغات جديدة (عربي، فرنسي، أوردو) بمجرد إضافة ملف ترجمة في الـ API دون لمس حرف واحد في منطق العمل أو الـ Handlers.

---

## 5. التبعات والمقايضات (Consequences & Trade-offs)

* **ما كسبناه:**
  - أداء فائق في القراءة والكتابة.
  - خلو المشروع من التبعيات الخارجية غير الضرورية (`No AutoMapper dependency`).
  - احترام تام لقواعد الـ Encapsulation في DDD.
  - معمارية لغات نقية ومطابقة لأفضل ممارسات Enterprise.
* **ما دفعناه في المقابل (Trade-offs):**
  - كتابة أسطر يدوية إضافية في الـ Queries (`.Select(x => new Dto(...))`) بدلاً من استدعاء سطر واحد سحري (`_mapper.Map`). نعتبر هذا استثماراً إيجابياً ومطلوباً لحماية وضوح النظام وأمانه.

---

## 6. متى يُعاد النظر في هذا القرار؟ (Reconsideration Conditions)

يُعاد النظر في استخدام أداة Mapping آلية فقط في الحالات التالية:
1. إذا احتوى النظام على شاشات CRUD ضخمة جداً ذات كائنات تزيد عن 50 حقلاً مسطحاً بدون قواعد دومين (Anemic Forms).
2. في هذه الحالة، **لن نستخدم AutoMapper**، بل سنعتمد مكتبة مبنية على **Source Generators** في وقت الترجمة مثل `Riok.Mapperly`.

---

## 7. دليل المقابلات التقنية (Interview Defense / Elevator Pitch)

إذا سُئلت في مقابلة عمل هندسية رفيعة المستوى:
> *"لماذا لم تستخدم AutoMapper لتحويل الـ Entities إلى DTOs في مشروعك؟ وكيف بنيتم دعم اللغات المتعددة؟"*

### الإجابة الهندسية النموذجية:

> "قسمت القرار بناءً على طبيعة العمليات وفق نمط **CQRS**:
>
> **أولاً في جانب الكتابة (Commands):**
> نطبق مبادئ DDD الصارمة؛ الـ Entities مغلفة وتحمي قواعد عملها عبر Factory Methods (`Branch.Create`). مكتبات مثل AutoMapper صُممت للنسخ الأعمى للخصائص (`prop = prop`)، واستخدامها يتعارض مع الـ Encapsulation ويجبرك على فتح الـ Setters أو إدخال إعدادات معقدة. فضلنا استدعاء دوال المصانع صراحة لتحقيق أمان كامل في الـ Compile-Time.
>
> **ثانياً في جانب القراءة (Queries):**
> اعتمدنا **Direct LINQ Projection (`.Select`)** مباشرة من قاعدة البيانات إلى الـ DTOs.
> هذا يضمن ترجمة الاستعلام في SQL لجلب الأعمدة المطلوبة فقط (منع الـ Over-fetching)، ويعطينا سرعة قصوى بدون Reflection وبدون استهلاك ذاكرة وسيطة (Zero-Allocation)، مع حماية من أخطاء الـ Runtime لو تغيرت مسميات الحقول.
>
> **بخصوص تعدد اللغات (Localization):**
> صممنا النظام ليكون **Language-Agnostic** في الدومين؛ فالـ Domain لا يعرف أي لغة بشرية، بل يطلق **Machine-Readable Error Codes** (مثل `Branch.CodeAlreadyExists`).
> والترجمة تتم عند حدود الـ API عبر `Accept-Language Header` و `IStringLocalizer`، بينما تُحفظ البيانات التجارية المزدوجة (عربي/إنجليزي) كحقول مخصصة في الكيان لخدمة متطلبات طباعة الفواتير."
