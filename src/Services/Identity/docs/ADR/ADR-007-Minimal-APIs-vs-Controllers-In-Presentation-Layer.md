# ADR-007: اعتماد معمارية Minimal APIs مع مجموعات المسارات بدلاً من Controllers في طبقة العرض

| Metadata | Details |
| :--- | :--- |
| **Status** | ✅ Accepted |
| **Date** | 2026-10-17 |
| **Author** | Senior Backend Engineer / Architecture Reviewer & Technical Partner |
| **Service** | Platform-Wide (SuperMarket.Identity, Sales, Inventory, Operations) |
| **Layer** | API / Presentation Layer (`SuperMarket.*.API`) |
| **Related Concepts** | Minimal APIs, Route Groups (`MapGroup`), Extension Methods, RFC 7807 ProblemDetails, CQRS MediatR Dispatching |
| **Decision Scope** | Protocol Adapter Standardization, Memory Allocations, Composition over Inheritance, Elimination of Controller God Objects |

---

## 1. السياق والمشكلة بالضبط (Context & Exact Problem)

في معمارية Clean Architecture و CQRS، تم عزل وتجريد جميع قواعد العمل (Business Logic)، التحقق من صحة المدخلات (FluentValidation)، والعمليات العابرة (Cross-Cutting Concerns) داخل طبقة الـ Application عبر MediatR Pipeline Behaviors.

بالتالي، أصبحت وظيفة طبقة العرض (API / Presentation Layer) مقتصرة تماماً على دور **محول بروتوكولي نقي (Pure Protocol Adapter)**:
1. استقبال طلب HTTP وتفكيك المدخلات (JSON Deserialization & Route Parameter Binding).
2. إرسال Command أو Query إلى `ISender` الخاص بـ MediatR.
3. استلام كائن `Result` أو `Result<T>` وترجمته إلى كود حالة HTTP متوافق مع معايير RFC 7807 / RFC 9457 ProblemDetails.

### الصراع الهندسي الحقيقي:
كيف ننظم نقاط الدخول (Endpoints) في نظام POS عالي التزامن والضغط دون الوقوع في أحد الفخين التاليين:
1. **فخ Controllers الكلاسيكية والوراثة (`ApiControllerBase`):** حيث تتحول فئات الـ Base Controller تدريجياً إلى "God Classes" تفرض وراثة صلبة ومئات الـ Filters ومحركات الـ Reflection الثقيلة لخدمة سطرين من الكود.
2. **فخ الـ Spaghetti Minimal APIs:** حيث تُكتب مئات المسارات داخل ملف `Program.cs` بشكل مسطح وعشوائي دون تقسيم هيكلي مسؤول.

---

## 2. التحديات المتنافسة (Competing Challenges & Forces)

1. **الأداء واستهلاك الذاكرة (Throughput & Zero Unnecessary Allocations):**
   نظام نقاط البيع (POS) في سلاسل السوبرماركت يتعامل مع ضغط هائل أثناء ساعات الذروة. خط سير الـ MVC الكلاسيكي (Controller Activator, Action Descriptors, Model Metadata Providers, Filter Pipelines) يستهلك دورات معالج وحجوزات ذاكرة (Heap Allocations) مع كل طلب لإنشاء كائنات الكنترولر وتفكيكها.
2. **مبدأ التركيب على حساب الوراثة (Composition over Inheritance):**
   الوراثة الصلبة من كلاس وسيط تجعل مشاركة السلوك بين الخدمات صعبة وتخلق ارتباطاً غير مرن (Tight Coupling)، في حين أن دوال التوسعة النقية (Extension Methods) في `BuildingBlocks` توفر تركيباً حراً دون إجبار أي كلاس على الوراثة.
3. **التوثيق ومعايير الصناعة (OpenAPI & Microsoft Reference Standards):**
   ضرورة الالتزام بأحدث توصيات Microsoft الرسمية في .NET 10 والمرجع المعماري المعتمد لأنظمة الخدمات المصغرة الحديثة (`dotnet/eShop`).
4. **عزل الميزات (Vertical Slice Separation):**
   الحاجة لأن تكون كل ميزة (Branches, Roles, Staff) مستقلة تماماً في ملفها الخاص دون الاعتماد على كلاسات مركزية ضخمة.

---

## 3. كيف ستحصل المشكلة لو تم اختيار Controllers + BaseController؟ (Failure Analysis)

لو اعتمدنا نمط `ApiControllerBase` في أنظمتنا، تقع الكارثة المعمارية عبر المسارات التالية:

1. **انزلاق الـ BaseController إلى God Object:**
   مع مرور الوقت ونمو المشروع، يبدأ المطورون بحشر خدمات إضافية داخل `ApiControllerBase` (مثل جلب هوية المستخدم الحالي، دوال تشفير مساعدة، خدمات الـ Caching والـ Logging). النتيجة: كل Controller في النظام يرث عشرات التبعيات التي لا يحتاجها، منتهكاً مبدأ المسؤولية الواحدة (SRP) ومبدأ فصل الواجهات (ISP).
2. **استحالة مشاركة الكود في BuildingBlocks:**
   كلاس `ControllerBase` يرتبط بحزم ASP.NET Core MVC الكاملة، مما يمنع نقل دوال المساعدة لـ `SuperMarket.BuildingBlocks` المشتركة الخفيفة دون تلويثها بتبعيات ثقيلة.
3. **عائق التحول المستقبلي إلى Native AOT:**
   تعتمد Controllers على التوليد الديناميكي وفحص الكلاسات عبر الـ Reflection في الـ Runtime، وهو ما يتعارض بشدة مع التجميع المسبق عالي السرعة (Native Ahead-Of-Time Compilation) المدعوم بقوة في Minimal APIs.

---

## 4. الحلول والبدائل مع المقايضات (Trade-offs Analysis)

### البديل 1: Controllers كلاسيكية مشتقة من `ApiControllerBase`
* **المكاسب:** مألوفة للمطورين التقليديين، توجيه تلقائي عبر أسماء الكلاسات (`[Route("api/v1/[controller]")]`).
* **الخسائر والمقايضات:** وراثة صلبة (Inheritance)، استهلاك ذاكرة أعلى، خط أنابيب MVC ثقيل، خطر تحول الكلاس إلى God Class، ومخالفة للمرجع المعماري الحديث لمايكروسوفت.

### البديل 2: مكتبات طرف ثالث بنمط REPR (مثل FastEndpoints أو Ardalis.Endpoints)
* **المكاسب:** عزل كامل، كلاس مستقل لكل Endpoint.
* **الخسائر والمقايضات:** ارتهان لمكتبات خارجية (Vendor Lock-in)، مخاطر توقف الصيانة مع التحديثات السنوية لـ .NET، وحاجة لتدريب أي مهندس جديد ينضم للمشروع.

### البديل 3 (المعتمد): Minimal APIs مع مجموعات المسارات (`MapGroup`) ودوال التوسعة
* **المكاسب:**
  * مطابقة 100% لمعمارية مايكروسوفت المرجعية (`dotnet/eShop`).
  * تجميع مسارات كل ميزة في كلاس ساكن مستقل (Vertical Slice) عبر دالة توسعة نقية على `IEndpointRouteBuilder`.
  * استهلاك ذاكرة أقل وسرعة استجابة أعلى (Bypasses MVC Pipeline completely).
  * الاعتماد الكامل على **Composition (دوال نقية في BuildingBlocks)** بدلاً من الوراثة.
  * جاهزية كاملة لـ Native AOT و .NET 10.
* **المقايضة التي ندفعها:**
  * تتطلب انضباطاً صارماً في تنظيم مجلدات `Endpoints/` وتوثيق الـ OpenAPI Metadata يدوياً لكل مسار.

---

## 5. الحل الذي اخترناه وطريقة العمل بالتفصيل (The Chosen Architecture)

اعتمدنا نهج **Minimal APIs المنظمة رأسياً عبر Route Groups في كلاسات ساكنة مستقلة**، مع ربط كل ميزة كدالة توسعة لـ `IEndpointRouteBuilder`.

### المخطط المعماري رفيع المستوى للمنظومة (High-Level Architecture Diagram)

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                          1. Client Applications                             │
│       (POS Cashier Terminals - Store Manager Mobile - Backoffice Web)       │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ HTTP POST/GET/PUT/DELETE (JSON)
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    2. Presentation Layer (ASP.NET Core)                     │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │                     Fast Endpoint Route Matching                        │ │
│ └────────────────────────────────────┬────────────────────────────────────┘ │
│                                      │                                      │
│        ┌─────────────────────────────┼─────────────────────────────┐        │
│        ▼                             ▼                             ▼        │
│ ┌───────────────┐             ┌───────────────┐             ┌─────────────┐ │
│ │BranchEndpoints│             │ RoleEndpoints │             │StaffEndpoint│ │
│ │/api/v1/branches             │ /api/v1/roles │             │/api/v1/staff│ │
│ └───────┬───────┘             └───────┬───────┘             └──────┬──────┘ │
│         │                             │                            │        │
│         └─────────────────────────────┼────────────────────────────┘        │
│                                       ▼                                     │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │ Parameter Binding: Request DTO (JSON Body) + CancellationToken + ISender│ │
│ └────────────────────────────────────┬────────────────────────────────────┘ │
└──────────────────────────────────────┼──────────────────────────────────────┘
                                       │ sender.Send(command, ct)
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        3. Application Layer (CQRS)                          │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │                   MediatR Dispatcher & Inversion Port                   │ │
│ └────────────────────────────────────┬────────────────────────────────────┘ │
│                                      │                                      │
│ ┌────────────────────────────────────▼────────────────────────────────────┐ │
│ │ Pipeline Behaviors (ValidationPipelineBehavior -> LoggingBehavior)       │ │
│ └────────────────────────────────────┬────────────────────────────────────┘ │
│                                      │ Passes validated Command / Query     │
│        ┌─────────────────────────────┴─────────────────────────────┐        │
│        ▼                                                           ▼        │
│ ┌───────────────────────────┐                         ┌───────────────────┐ │
│ │      Command Handlers     │                         │   Query Handlers  │ │
│ │  (Mutations via Repos)    │                         │(Read-Only Proj.)  │ │
│ └─────────────┬─────────────┘                         └─────────┬─────────┘ │
└───────────────┼─────────────────────────────────────────────────┼───────────┘
                │                                                 │
                ▼                                                 │
┌───────────────────────────────┐                                 │
│        4. Domain Layer        │                                 │
│ ┌───────────────────────────┐ │                                 │
│ │ Aggregate Roots & Entities│ │                                 │
│ │ (Branch, Role, User)      │ │                                 │
│ └─────────────┬─────────────┘ │                                 │
│               │ Enforces      │                                 │
│ ┌─────────────▼─────────────┐ │                                 │
│ │ Domain Invariants & Rules │ │                                 │
│ └───────────────────────────┘ │                                 │
└───────────────┬───────────────┘                                 │
                │ SaveChangesAsync                                │
                ▼                                                 ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      5. Infrastructure & Persistence                        │
│ ┌───────────────────────────┐                         ┌───────────────────┐ │
│ │ EF Core IdentityDbContext │◄────────────────────────┤IIdentityReadDbCont│ │
│ │ (Audit Interceptor)       │                         │ (No-Tracking SQL) │ │
│ └─────────────┬─────────────┘                         └─────────┬─────────┘ │
│               │                                                 │           │
│               └──────────────────────┬──────────────────────────┘           │
│                                      ▼                                      │
│                       ┌─────────────────────────────┐                       │
│                       │    PostgreSQL 17 Database   │                       │
│                       └─────────────────────────────┘                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
                                       │ Returns Result<T> / Result
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│               6. Result Pattern & HTTP Response Translation                 │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │  BuildingBlocks: ResultProblemDetailsExtensions.ToProblemDetails()      │ │
│ └────────────────────────────────────┬────────────────────────────────────┘ │
│                                      │                                      │
│        ┌─────────────────────────────┴─────────────────────────────┐        │
│        ▼                                                           ▼        │
│ ┌───────────────────────────┐                         ┌───────────────────┐ │
│ │   Success (IsSuccess)     │                         │ Failure (IsFailure│ │
│ │ 200 OK / 201 Created / 204│                         │ RFC 7807 Details  │ │
│ │ (with Location header)    │                         │ (400, 404, 409)   │ │
│ └───────────────────────────┘                         └───────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. كيف سوف يكون سير العمل بعد اختيارنا له بالضبط؟ (Workflow & Execution Lifecycle)

1. **التهيئة وقت الإقلاع (Startup Registration):**
   * في [Program.cs](file:///d:/dotnet/POS/src/Services/Identity/SuperMarket.Identity.API/Program.cs)، يتم تسجيل المسارات بسطر واحد واضح لكل ميزة:
     ```csharp
     app.MapBranchEndpoints();
     app.MapRoleEndpoints();
     app.MapPermissionGroupEndpoints();
     ```
   * يقوم ASP.NET Core بتجميع الـ Request Delegates وتجهيز شجرة التوجيه مسبقاً (Zero runtime delegate compilation).
2. **استقبال الطلب وتنفيذه (Request Execution):**
   * يصل طلب HTTP إلى المسار المحدد.
   * يقوم محرك الـ Parameter Binding بحقن كائن الطلب تلقائياً من الـ Body، وحقن `ISender` و `CancellationToken` من حاوية الخدمات دون وساطة Reflection ثقيلة.
   * يرسل الـ Endpoint الكائن فوراً لـ MediatR.
   * يمر الطلب عبر خط أنابيب التحقق (`ValidationPipelineBehavior`) لضمان صحة البيانات هيكلياً.
3. **إرجاع النتيجة (Response Translation):**
   * عند النجاح: يُرجع الـ Endpoint رمز الحالة القياسي (`Results.Ok`, `Results.CreatedAtRoute`, أو `Results.NoContent`).
   * عند الفشل: تقوم دالة [ToProblemDetails()](file:///d:/dotnet/POS/src/BuildingBlocks/SuperMarket.BuildingBlocks/Results/ResultProblemDetailsExtensions.cs) بتحويل كائن `Error` إلى استجابة RFC 7807 كاملة الحقول مع كود الحالة المناسب (`400`, `404`, `409`).

---

## 7. زاوية المقابلة التقنية (Senior Interview Defense)

> **سؤال المقابلة:** "لماذا استغنيتم عن Controllers و BaseController في مشروعكم واعتمدتم Minimal APIs مع أن النظام تجاري ضخم (Enterprise POS)؟"

**الإجابة الدفاعية للمهندس المحترف:**
"في معمارية Clean Architecture و CQRS، طبقة العرض هي مجرد **Protocol Adapter**. كل منطق التحقق والعمليات العابرة وحقن الخدمات يقع داخل MediatR Pipeline في طبقة Application. وبالتالي، فإن كود الـ Endpoint لا يتجاوز سطرين (إرسال الأمر وترجمة الـ Result).
استخدام Controllers في هذا السياق يفرض علينا طبقة MVC ثقيلة (Controller Activator, Action Descriptors, Model Binders, Action Filters) تستهلك دورات معالج وتخصيصات ذاكرة دون أي عائد معماري. علاوة على ذلك، أثبتت التجربة أن `BaseController` يمثل فخاً يغري المطورين بتحويله إلى **God Object** يخرق مبدأ المسؤولية الواحدة عبر وراثة صلبة (Inheritance).
لذلك، اعتمدنا نهج مايكروسوفت المرجعي الحديث في `dotnet/eShop`: **Minimal APIs مقسمة رأسياً عبر Route Groups في دوال توسعة مستقلة**، والاعتماد على **Composition** من خلال Pure Extension Methods في `BuildingBlocks` لترجمة النتائج إلى استجابات RFC 7807، مما حقق أقصى أداء مع الحفاظ التام على فصل الاهتمامات وقابلية الصيانة."
