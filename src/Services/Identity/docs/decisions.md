# ⚖️ سجل القرارات المعمارية (Architecture Decision Records - ADRs)

توثق هذه الصفحة القرارات الهندسية الجوهرية لخدمة `SuperMarket.Identity` وفق معيار **ADR** العالمي:

---

## 🏛️ ADR-ID-001: اعتماد Keycloak كمزود هوية مركزي (Central Identity Provider)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * منصة السوبرماركت تتطلب إدارة موحدة لهويات الموظفين عبر جميع الفروع، دعم سياسات كلمات المرور المعقدة، إدارة الأدوار (RBAC)، وإصدار رموز JWT مشفرة.
  * بناء نظام AuthN يدوي يزيد من المخاطر الأمنية وثغرات الـ Tokens وتكلفة الصيانة.
* **القرار (Decision):**
  * اعتماد **Keycloak (26.x)** كـ Identity Provider مركزي يدعم بروتوكولات **OAuth 2.0 & OpenID Connect (OIDC)** عبر Realm مخصص: `supermarket`.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** حماية كاملة للهويات، إدارة مركزية لجميع فروع وموظفي الشركة، وتوافق تام مع معايير الأمان العالمية.
  * **سلبي:** تشغيل وإدارة حاوية Keycloak في بيئة التطوير والإنتاج.

---

## 🏛️ ADR-ID-002: نمط المصادقة السريعة للكاشير (Fast POS PIN-Based Authentication)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * الكاشير في السوبرماركت يحتاج لتسجيل الدخول في أقل من ثانية، ولا يمكن إلزامه بفتح متصفح واستخدام واجهات تفاعلية معقدة أثناء وقوف العملاء في الطابور.
* **الخيارات المتاحة (Options):**
  1. *Option A:* إلزام الكاشير بتسجيل الدخول ببريده وكلمة المرور الطويلة عبر شاشة متصفح (OAuth PKCE).
  2. *Option B:* مصادقة سريعة عبر (كود الموظف + PIN مشفر + كود جهاز الكاشير) يتم تحويلها برمجياً إلى Keycloak Token.
* **القرار (Decision):**
  * اعتماد **Option B**: إرسال كود الموظف والـ PIN من شاشة الـ POS إلى `Identity.API`، حيث يتم فحص الجهاز، ومطابقة الـ Hash، ثم طلب Access Token مخصص للكاشير من Keycloak.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** سرعة استثنائية في فتح الشاشة والتبديل السريع بين الورديات (Sub-second login).
  * **إيجابي:** حماية الـ PIN بالتشفير القوي (Argon2id/PBKDF2) داخل قاعدة بيانات `identity_db`.

---

## 🏛️ ADR-ID-003: ربط جهاز الكاشير بالفرع والعتاد (POS Terminal Hardware Binding)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * في أنظمة التجزئة، يمنع منعاً باتاً تشغيل تطبيق الـ POS أو تسجيل المبيعات من جهاز غير مصرح له أو خارج نطاق الفرع المخصص.
* **القرار (Decision):**
  * إنشاء كيان `POSRegister` يربط كل محطة بيع برقم تسلسلي فريد وبصمة عتاد (`DeviceFingerprint` / MAC Address)، وربطه إلزامياً بـ `BranchId`.
  * يتم رفض أي طلب تسجيل دخول كاشير إذا كان الجهاز غير مسجل أو معطلاً (`Status != Ready`).
* **النتائج والآثار (Consequences):**
  * **إيجابي:** منع تزوير العمليات وحصر كل نقطة بيع في مسارها المالي والفرعي الدقيق.

---

## 🏛️ ADR-ID-004: التحقق عديم الحالة من الـ Tokens عبر JWKS Caching

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * التحقق من الـ JWT في كل عملية مسح صنف أو دفع قد يسبب ضغطاً هائلاً على خادم Keycloak إذا كان يتم عبر Network Calls متكررة.
* **القرار (Decision):**
  * استخدام التوقيع غير المتماثل (**RS256**) وتفعيل **In-Memory JWKS Caching** داخل حزمة `Microsoft.AspNetCore.Authentication.JwtBearer`.
* **النتائج والآثار (Consequences):**
  * فحص الـ Tokens يتم محلياً في ذاكرة الخدمة في زمن **< 1ms** دون أي اتصال شبكي بـ Keycloak، مما يضمن استمرار وسرعة عمليات البيع.

---

## 🏛️ ADR-ID-005: نمط موافقة المشرف اللحظية (Supervisor Override Pattern)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * بعض العمليات الحساسة في السوبرماركت (مثل إلغاء صنف بعد الفاتورة `Void Item`، تطبيق خصم يدوي، أو فتح الدرج بدون بيع) لا يملك الكاشير صلاحيتها وتتطلب موافقة المشرف دون تسجيل خروج الكاشير من جلسته.
* **القرار (Decision):**
  * توفير نقطة نهاية مخصصة `VerifySupervisorOverride` تقبل كود المشرف ورمزه السري ونوع العملية، وتُصدر رمز موافقة مؤقت قصير الأجل (One-Time Short-Lived Token) يرفقه تطبيق الـ POS مع العملية الموجهة لخدمة `Sales.API`.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** تدقيق مالي كامل (Audit Trail) يوضح من المشرف الذي وافق على الحركة الحساسة، ومن الكاشير المنفذ لها.
  * **إيجابي:** عدم مقاطعة جلسة الكاشير الأساسية.

---

## 🏛️ ADR-ID-006: تطبيق نمط صندوق الصادر (Transactional Outbox Pattern)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * عند إنشاء فرع جديد أو تعطيل موظف، يجب إعلام باقي الميكروسيرفيس (Sales, Inventory, Operations) عبر RabbitMQ. إذا تم الحفظ في قاعدة البيانات وفشل ناقل الرسائل، تصبح بيانات النظام مشتتة.
* **القرار (Decision):**
  * استخدام **MassTransit Transactional Outbox** لحفظ الأحداث في جدول `outbox_messages` داخل نفس الـ Database Transaction، وتكليف Worker بنشرها بشكل موثوق.
* **النتائج والآثار (Consequences):**
  * ضمان عدم فقدان أي حدث تكاملي (Guaranteed At-Least-Once Delivery).

---

## 🏛️ ADR-ID-007: اعتماد نمط الـ Rich Domain Model والـ Encapsulation

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * هل نستخدم Anemic Domain Model بحقول مفتوحة، أم Rich Domain Model مع Private Setters وطرق أعمال واضحة؟
* **القرار (Decision):**
  * اعتماد **Domain-Driven Design (DDD)** مع **Private Setters** وطرق تعديل صريحة (مثل `TransferToBranch`, `Deactivate`, `BindToTerminal`) لحماية صحة قواعد البزنس.
* **النتائج والآثار (Consequences):**
  * منع الحالات غير الصالحة في الذاكرة ومنع التلاعب بالبيانات الحساسة للموظفين والفروع.

---

## 🏛️ ADR-ID-008: معمارية الكيانات القائمة على واجهات القدرات (Capability-Based Entity Architecture) وتطبيق Pragmatic DDD

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * في أنظمة الـ POS، تتشارك العديد من الجداول حقولاً هامة مثل التتبع الزمني (`created_at`, `updated_at`)، والحذف المنطقي (`is_deleted`, `deleted_at`)، وحالة التفعيل (`is_active`).
  * تجميع كل هذه الحقول في كلاس أبوي واحد ضخم (`God BaseEntity`) يفرض حقولاً لا لزوم لها على جداول تاريخية غير قابلة للتعديل أو الحذف (مثل `stock_movements` وسجلات العمليات المالية) مما ينتهك مبدأ **Interface Segregation Principle (ISP)**.
  * يبرز التساؤل أيضاً حول كيفية تطبيق DDD: هل نطبقه بشكل حرفي مفرط (Dogmatic) على كل جدول، أم نعتمد DDD عملي (Pragmatic DDD) يركز على حماية القواعد الحساسة؟
* **الخيارات المتاحة (Options):**
  1. *Option A:* إنشاء `BaseEntity` ضخم يرث منه كل كائن في النظام.
  2. *Option B:* فصل القدرات إلى واجهات برمجية مستقلة ودقيقة (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`) مع كلاس أساسي generic للمقارنة `Entity<TId>`، وتطبيق Pragmatic DDD على الـ Core Bounded Contexts فقط.
* **القرار (Decision):**
  * اعتماد **Option B**:
    1. وضع الكلاس الأساسي `Entity<TId>` في `BuildingBlocks` لتوحيد المقارنة بالهوية (`Id.Equals`).
    2. صياغة عقود مستقلة (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`) يطبق كل كيان ما يحتاجه منها فقط.
    3. أتمتة ملء التواريخ وحماية الحذف المنطقي عبر `AuditSaveChangesInterceptor` في EF Core دون أي كود يدوي في الـ Handlers.
    4. تطبيق **Pragmatic DDD**: استخدام الـ Rich Domain Models و Aggregate Roots في السياقات ذات القواعد المالية والتشغيلية المعقدة (المبيعات، الورديات، الفروع، المخزون)، واستخدام CRUD خفيف واستعلامات مباشرة (CQRS) للجداول المرجعية البسيطة.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** كود نظيف، عالي المقروئية، يلتزم بمبادئ Clean Code و SOLID.
  * **إيجابي:** منع الـ Side-effects والأخطاء المحاسبية الناتجة عن محاولة حذف حركات المخزون أو الفواتير.
  * **إيجابي:** سهولة التوسعة مستقبلاً (مثل إضافة `ITenantAware` لدعم Multi-Tenancy) دون لمس الكود القديم.

