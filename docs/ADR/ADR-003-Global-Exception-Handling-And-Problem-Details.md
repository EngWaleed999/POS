# 🏛️ سجل القرارات المعمارية (ADR-003)
## معالجة الاستثناءات المركزية، نمط IExceptionHandler، ومعيار ProblemDetails (RFC 7807)

* **الحالة:** معتمد ومخطط للتنفيذ (Accepted & Ready for Implementation)
* **التاريخ:** 2026-09-15
* **السياق والمشروع:** `SuperMarket.BuildingBlocks` / شبكة الخدمات المصغرة لمنظومة POS
* **المؤلفون:** Senior Backend Engineer & Architecture Reviewer

---

## 1. Context (سياق المشكلة الهندسية)

في نظام نقاط بيع وسوبرماركت مركزي يعتمد على معمارية الخدمات المصغرة (Microservices):
1. **تسرب أسرار النظام (Information Disclosure Vulnerability):** عند حدوث انهيار غير معالج (Unhandled Exception) مثل سقوط قاعدة البيانات، أو خطأ اتصال، يعيد السيرفر افتراضياً صفحات تفصيلية أو StackTrace يكشف أسماء الجداول، المسارات، وبنية الكود للمستخدمين أو المهاجمين.
2. **عشوائية هياكل الأخطاء (API Payload Chaos):** بدون معيار موحد، قد يعيد مطور في خدمة الهوية رد خطأ كـ `{ "error": "msg" }`، ومطور في المبيعات كـ `{ "message": "fail", "code": 500 }`، ومطور آخر مجرد نص خالص (Plain Text). هذا يربك تطبيقات الـ Frontend (شاشات الكاشير، تطبيقات الجوال) ويجعل معالجة الأخطاء كابوساً.
3. **فقدان أثر الخطأ في الإنتاج (Lack of Error Traceability):** عندما يبلغ كاشير عن فشل عملية دفع، لا يملك فريق الدعم أي وسيلة للربط بين ما شاهده الكاشير على الشاشة وبين ملايين أسطر السجلات (Logs) المخزنة في السيرفرات دون رقم مرجعي موحد (`TraceId`).
4. **الخلط بين أخطاء البزنس المتوقعة والانهيارات الفادحة:** الخلط بين فشل تحقق من باركود (Business Failure) وبين انهيار اتصال بالخادم (Infrastructure Crash).

---

## 2. Options Considered (الخيارات الهندسية التي تم تقييمها)

### الخيار (1): استخدام Custom Middleware تقليدي (`app.UseMiddleware<ExceptionMiddleware>()`)
* **الوصف:** كتابة كلاس Middleware مخصص يحتوي على كتلة `try-catch` تغلف استدعاء `_next(context)`.
* **الإيجابيات:** مفهوم مألوف في نسخ .NET القديمة (.NET Core 2/3/5/6).
* **التكلفة والعيوب:**
  - يتم تشغيله واستهلاك موارده في كل ريكوست يدخل ويخرج حتى لو لم تحدث أخطاء.
  - يتطلب كتابة كود يدوي للتعامل مع الـ Headers، وإغلاق الـ Streams، وتنسيق JSON.
  - لا يدعم تجزئة المسؤوليات (Chaining) بسهولة؛ كلاس واحد ضخم يحاول معالجة كل أنواع الأخطاء.

### الخيار (2): استخدام فلاتر الاستثناءات في MVC (`IExceptionFilter` / `ExceptionFilterAttribute`)
* **الوصف:** استخدام فلاتر خاصة بـ ASP.NET Core Controllers.
* **الإيجابيات:** تعمل داخل سياق الـ MVC Action.
* **التكلفة والعيوب:**
  - تلتقط فقط الأخطاء التي تحدث داخل الـ Controllers؛ لا تلتقط الأخطاء التي تقع في الـ Middlewares الأخرى أو فلاتر الـ Routing أو الـ Authentication.
  - لا تعمل بشكل أصيل مع Minimal APIs الحديثة.

### الخيار (3 - المعتمد): نمط `IExceptionHandler` المدمج في (.NET 8/9/10) مع `ProblemDetails` (RFC 7807)
* **الوصف:** تطبيق واجهة `IExceptionHandler` القياسية المسجلة في حاوية الخدمات، بالتكامل مع خدمة `IProblemDetailsService` المعتمدة من Microsoft.
* **الإيجابيات:**
  - **Zero Overhead في المسار الناجح:** لا يتدخل المعالج إطلاقاً إلا إذا قُذف استثناء مفاجئ بالفعل.
  - **معيار دولي موحد (RFC 7807):** هيكل JSON قياسي عالمي متوافق مع كافة المتصفحات، المنصات، والفرونت إند.
  - **سلاسل المسؤولية (Chain of Responsibility):** إمكانية تعريف معالج متخصص لاستثناءات قواعد البيانات يليه معالج عام للحالات الأخرى.
  - **حماية أمنية وتتبع:** إخفاء الـ StackTrace عن العميل، وتضمين `TraceId` يطابق تماماً ما يتم تسجيله في Serilog.

---

## 3. Decision (القرار الهندسي المعتمد)

1. **اعتماد `IExceptionHandler` المركزي:** بناء كلاس `GlobalExceptionHandler` داخل `SuperMarket.BuildingBlocks` يطبق واجهة `Microsoft.AspNetCore.Diagnostics.IExceptionHandler`.
2. **اعتماد معيار RFC 7807 (`ProblemDetails`):** توحيد جميع ردود أخطاء الـ 500 لتخرج بصيغة ProblemDetails القياسية.
3. **فصل المسارات:**
   - **أخطاء البزنس المتوقعة:** لا تصل إطلاقاً إلى الـ Exception Handler؛ بل تعود كـ `Result.Failure` وتُحول في الـ Endpoint إلى ProblemDetails بحالة HTTP مناسبة (`400`, `404`, `409`).
   - **الانهيارات غير المتوقعة (Unhandled Exceptions):** يلتقطها `GlobalExceptionHandler`، يسجلها في Serilog كـ `LogError` مع الـ StackTrace والـ TraceId، ثم يعيد للعميل استجابة آمنة بحالة `500 Internal Server Error` تحتوي على رسالة عامة والـ `TraceId` فقط.
4. **تضمين مرجع الويب في BuildingBlocks:** إضافة `<FrameworkReference Include="Microsoft.AspNetCore.App" />` إلى `SuperMarket.BuildingBlocks.csproj` لتمكين كافة الخدمات من استخدام البنية التحتية المركزية للـ Web بسطر واحد.

---

## 4. Why (لماذا اخترنا هذا الحل؟)

* **الأمان أولاً (Security by Design):** منع تسرب أي بيانات داخلية (Database connection strings, table names, file paths) إلى العميل.
* **الملاحظة والرصد (Observability):** الربط اللحظي بين شاشة المستخدم وسجلات الخادم عبر الـ `TraceId` المعياري (W3C TraceContext).
* **سهولة الصيانة (Maintainability):** إعداد الـ Exception Handling في أي ميكروسيرفيس جديدة يتطلب سطرين فقط في `Program.cs`:
  ```csharp
  builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
  builder.Services.AddProblemDetails();
  // ...
  app.UseExceptionHandler();
  ```

---

## 5. Trade-offs (المقايضات والتكلفة)

| المكاسب المحققة (+) | التكلفة والمقايضات المقبولة (-) |
| :--- | :--- |
| حماية أمنية كاملة من تسرب أسرار النظام. | لن يرى المبرمج الـ StackTrace مباشرة في شاشة Postman بالـ Production، بل يجب عليه فتحه في Seq عبر الـ TraceId. |
| هيكل استجابة أخطاء متطابق وموحد في كافة خدمات المنظومة. | إضافة كائنات ProblemDetails تضيف بضعة بايتات إضافية لحجم الـ JSON مقارنة بالنص الخام. |
| أداء عالي (Zero-Allocation على الطلبات الناجحة). | يتطلب إضافة مرجع الـ ASP.NET Core Web Framework لمشروع الـ BuildingBlocks. |

---

## 6. Consequences (الآثار المعمارية والتشغيلية)

* **على المعمارية:** حماية المنظومة كلياً من الانهيارات غير الممسوكة، وتوحيد لغة الحوار مع الـ Frontend.
* **على التطوير:** المطور لم يعد بحاجة لكتابة كتل `try-catch` عامة في أي Controller أو Handler.
* **على العمليات والدعم الفني:** عند حدوث مشكلة لكاشير في أي فرع، يكفي أخذ الـ `TraceId` والبحث به في أدوات المراقبة (Seq / Kibana) للوصول للسبب الجذري خلال ثوانٍ.

---

## 7. Reconsideration Conditions (شروط إعادة النظر)

* يعاد النظر في هذا القرار إذا تحولت الخدمات الداخلية بالكامل إلى بروتوكول **gRPC** ثنائي (Binary Protocol) بدلاً من REST/HTTP، حيث يتطلب gRPC معالجة استثناءات عبر `RpcException` و `gRPC Interceptors` بدلاً من RFC 7807 HTTP ProblemDetails.
