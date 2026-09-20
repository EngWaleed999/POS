# ⚖️ سجل القرارات المعمارية (Architecture Decision Records - ADRs)

توثق هذه الصفحة القرارات الهندسية الجوهرية لخدمة `SuperMarket.Identity` وفق معيار **ADR** العالمي:

### 📑 فهرس القرارات المعمارية المعتمدة (ADR Index)

| المعرف (ID) | عنوان القرار (Decision Title) | الحالة (Status) | التأثير المعماري (Architectural Impact) |
|---|---|---|---|
| **ADR-ID-001** | [اعتماد Keycloak كمزود هوية مركزي](#-adr-id-001-اعتماد-keycloak-كمزود-هوية-مركزي-central-identity-provider) | مُعتمد (Approved) | تفويض الـ AuthN لمعيار عالمي وإلغاء الحاجة لبناء خادم هويات يدوي. |
| **ADR-ID-002** | [نمط المصادقة السريعة للكاشير (Fast PIN Login)](#-adr-id-002-نمط-المصادقة-السريعة-للكاشير-fast-pos-pin-based-authentication) | مُعتمد (Approved) | تسجيل دخول في أقل من ثانية لشاشات الـ POS بدون واجهات متصفح. |
| **ADR-ID-003** | [ربط جهاز الكاشير بالفرع والعتاد (Terminal Binding)](#-adr-id-003-ربط-جهاز-الكاشير-بالفرع-والعتاد-pos-terminal-hardware-binding) | مُعتمد (Approved) | منع تزوير العمليات وحصر كل نقطة بيع في مسارها المالي والفرعي. |
| **ADR-ID-004** | [التحقق عديم الحالة من الـ Tokens عبر JWKS Caching](#-adr-id-004-التحقق-عديم-الحالة-من-الـ-tokens-عبر-jwks-caching) | مُعتمد (Approved) | فحص الـ Tokens محلياً في الذاكرة في زمن < 1ms بدون Network Calls. |
| **ADR-ID-005** | [نمط موافقة المشرف اللحظية (Supervisor Override)](#-adr-id-005-نمط-موافقة-المشرف-اللحظية-supervisor-override-pattern) | مُعتمد (Approved) | توثيق حركات الإلغاء والخصم الاستثنائي دون مقاطعة جلسة الكاشير. |
| **ADR-ID-006** | [تطبيق نمط صندوق الصادر (Transactional Outbox)](#-adr-id-006-تطبيق-نمط-صندوق-الصادر-transactional-outbox-pattern) | مُعتمد (Approved) | ضمان تسليم رسائل RabbitMQ ذرياً داخل نفس الـ Transaction. |
| **ADR-ID-007** | [اعتماد نمط الـ Rich Domain Model والـ Encapsulation](#-adr-id-007-اعتماد-نمط-الـ-rich-domain-model-والـ-encapsulation) | مُعتمد (Approved) | حماية قواعد البزنس ومنع الحالات غير الصالحة في الذاكرة. |
| **ADR-ID-008** | [معمارية الكيانات القائمة على واجهات القدرات و Pragmatic DDD](#-adr-id-008-معمارية-الكيانات-القائمة-على-واجهات-القدرات-capability-based-entity-architecture-وتطبيق-pragmatic-ddd) | مُعتمد (Approved) | استبدال الـ God Base Class بـ Traits دقيقة (`IAuditableEntity`, إلخ). |
| **ADR-ID-009** | [حماية التغليف عبر التطبيق الصريح للواجهات (Explicit Interfaces)](#-adr-id-009-حماية-تغليف-واجهات-القدرات-عبر-التطبيق-الصريح-explicit-interface-implementation-for-capability-interfaces) | مُعتمد (Approved) | منع المطورين من تعديل `IsDeleted` يدوياً مع استمرار عمل Interceptors. |
| **ADR-ID-010** | [الحظر الأمني الآلي بعد 3 محاولات فاشلة وتزامن CCTV](#-adr-id-010-منظومة-الحظر-الأمني-الآلي-بعد-تكرار-المحاولات-الفاشلة-automatic-account-lockout--security-alerting) | مُعتمد (Approved) | حماية الـ PIN من التخمين وتوثيق توقيت بالثانية لمطابقة كاميرات المراقبة. |
| **ADR-ID-011** | [إلزامية رقم الهاتف واختيارية البريد الإلكتروني للعمالة](#-adr-id-011-إدارة-بيانات-الاتصال-وهوية-الكاشير-mandatory-phone-number--optional-email) | مُعتمد (Approved) | مطابقة واقع السوبرماركت لإرسال الـ PIN والتنبيهات عبر SMS. |
| **ADR-ID-012** | [الفصل بين مصادقة الهوية وإدارة الورديات المرنة (Soft Shifts)](#-adr-id-012-الفصل-المعماري-بين-مصادقة-الهوية-وإدارة-الورديات-المرنة-soft-shift-scheduling-vs-auth-boundaries) | مُعتمد (Approved) | منع قفل الشاشة قسرياً بوجه العملاء في الطوابير وتفويض الوردية لـ Sales. |
| **ADR-ID-013** | [اعتماد المفاتيح المركبة والقيود الفريدة لسلامة البيانات](#-adr-id-013-اعتماد-المفاتيح-المركبة-والقيود-الفريدة-لسلامة-البيانات-التصريحية-composite-keys--unique-constraints) | مُعتمد (Approved) | تسريع الاستعلامات ومنع Race Conditions على مستوى محرك PostgreSQL. |
| **ADR-ID-014** | [فصل أحداث الدومين عن أحداث التكامل ونمط الـ Outbox](#-adr-id-014-معمارية-فصل-أحداث-الدومين-عن-أحداث-التكامل-ونمط-الـ-outbox-in-memory-domain-events-vs-distributed-integration-events) | مُعتمد (Approved) | حماية استقلالية الـ Domain ومبدأ DIP بربط Interceptor بـ MediatR. |
| **ADR-ID-015** | [استراتيجية التحقق ذات المستويين (Two-Tier Validation)](#-adr-id-015-استراتيجية-التحقق-من-صحة-البيانات-ذات-المستويين-two-tier-validation-pipeline-fluentvalidation-vs-domain-invariants) | مُعتمد (Approved) | منع ظاهرة Error Ping-Pong بحصر فحص الصيغ في FluentValidation وحراسة الـ Domain. |

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

---

## 🏛️ ADR-ID-009: حماية تغليف واجهات القدرات عبر التطبيق الصريح (Explicit Interface Implementation for Capability Interfaces)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * عند وراثة كيان `User` لواجهات مثل `IAuditableEntity`, `ISoftDeletable`, `IActivatable`، تتطلب هذه الواجهات وجود خصائص `{ get; set; }` لتتمكن مراقبات EF Core (`AuditSaveChangesInterceptor`) من كتابة التواريخ وتحويل الحذف آلياً.
  * إذا تم تطبيق هذه الخصائص كـ `public get; set;` تقليدية، فإن ذلك يخلق ثغرة تغليف فادحة (Encapsulation Leak)، حيث يستطيع أي مبرمج كتابة `user.IsDeleted = true` أو تعديل `CreatedAt` يدوياً متجاوزاً دوال البزنس الرسمية وقواعد التدقيق.
* **الخيارات المتاحة (Options):**
  1. *Option A:* الإبقاء على الخصائص `public get; set;` والاعتماد على وعي المطورين (غير آمن).
  2. *Option B:* إلغاء الواجهات وكتابة كود التدقيق والحذف يدوياً داخل كل دالة ومستودع (ينتهك DRY ويزيد التكرار).
  3. *Option C:* استخدام **التطبيق الصريح للواجهات (Explicit Interface Implementation)**: جعل الخصائص العامة `private set` للمطور العادي، وجعل الـ Setters التابعة للواجهة صريحة ومقتصرة على مراقبات EF Core.
* **القرار (Decision):**
  * اعتماد **Option C**: جعل الخصائص الأساسية في الكيان للقراءة فقط من الخارج (`public bool IsDeleted { get; private set; }`)، وتطبيق الـ Setters صراحة عبر الواجهة:
    `bool ISoftDeletable.IsDeleted { get => IsDeleted; set => IsDeleted = value; }`
* **النتائج والآثار (Consequences):**
  * **إيجابي:** حماية مطلقة للتغليف؛ يمنع المترجم (Compiler) أي تعديل مباشر لهذه الحقول ويجبر المطور على استخدام دوال الأعمال الرسمية (`SoftDelete`, `Activate`, `Deactivate`).
  * **إيجابي:** استمرار عمل `AuditSaveChangesInterceptor` في EF Core بسلاسة ودون أي عوائق.

---

## 🏛️ ADR-ID-010: منظومة الحظر الأمني الآلي بعد تكرار المحاولات الفاشلة (Automatic Account Lockout & Security Alerting)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * في بيئة السوبرماركت، يحاول بعض الموظفين أحياناً تخمين أو استخدام الرمز السري السريع (PIN) لزملائهم لفتح الدرج أو تمرير مبيعات احتيالية أثناء غياب زميلهم.
  * ترك إدخال الـ PIN مفتوحاً دون حد أقصى يعرض حسابات الكاشير لهجمات التخمين الآلي واليدوي (Brute-Force).
* **الخيارات المتاحة (Options):**
  1. *Option A:* تتبع المحاولات الفاشلة في ذاكرة Redis الخارجية بـ TTL مؤقت.
  2. *Option B:* تثبيت العداد وحالة القفل داخل الكيان وجدول قاعدة البيانات (`access_failed_count`, `lockout_end`) مع إطلاق حدث أمني.
* **القرار (Decision):**
  * اعتماد **Option B**:
    1. إضافة حقلي `access_failed_count` (عداد) و `lockout_end` (تاريخ فك الحظر) مباشرة في جدول `users`.
    2. عند تسجيل 3 محاولات فاشلة متتالية (`RecordFailedLogin`): يُقفل الحساب تلقائياً لمدة **15 دقيقة**، ويُطلق الكيان فوراً حدثاً أمنياً: `UserLockedOutDomainEvent`.
    3. يحمل الحدث معرف المستخدم، واسم الدخول، وتوقيت الحظر بالثانية بدقة لربطه مع كاميرات المراقبة (CCTV Sync) فوق نقطة البيع لإثبات هوية المحتال.
    4. تصفير العداد فور نجاح تسجيل الدخول (`RecordLogin`)، مع توفير دالة فك حظر يدوي استثنائي للمشرف (`Unlock`).
* **النتائج والآثار (Consequences):**
  * **إيجابي:** حماية حاسمة ضد هجمات الـ Brute-Force وتأمين حسابات الكاشيرية.
  * **إيجابي:** سجل تدقيق قانوني وجنائي يتيح مراجعة تسجيلات الفيديو عند حدوث محاولة اختراق.

---

## 🏛️ ADR-ID-011: إدارة بيانات الاتصال وهوية الكاشير (Mandatory Phone Number & Optional Email)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * عند إنشاء موظف جديد بواسطة الـ IT أو الـ HR، يحتاج النظام لقناة اتصال فورية وموثوقة لإرسال بيانات الدخول، والرمز السري السريع (PIN)، وتنبيهات الحظر الأمني.
  * في قطاع التجزئة، يمتلك جميع موظفي الكاشير هواتف محمولة لتلقي رسائل SMS، بينما لا يمتلك أغلبهم بريداً إلكترونياً رسمياً للشركة.
* **القرار (Decision):**
  * اعتماد **رقم الهاتف (`phone_number`) كحقل إلزامي وفريد (`VARCHAR(30) NOT NULL UNIQUE`)** لكل موظف.
  * جعل **البريد الإلكتروني (`email`) حقلاً اختيارياً (`VARCHAR(100) NULL`)** مخصصاً للمدراء والمشرفين للتقارير والمراسلات الإدارية.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** ضمان وجود وسيلة اتصال حقيقية لإرسال الـ PIN وإشعارات الترحيب عبر الرسائل النصية SMS.
  * **إيجابي:** مرونة في توظيف عمالة الكاشير دون إلزامهم بإنشاء حسابات بريد إلكتروني.

---

## 🏛️ ADR-ID-012: الفصل المعماري بين مصادقة الهوية وإدارة الورديات المرنة (Soft Shift Scheduling vs Auth Boundaries)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * برز مقترح يقضي بمنع الكاشير من إدخال الـ PIN أو فتح الشاشة خارج ساعات ورديته المجدولة بدقة لمنع استخدام حسابات الزملاء.
  * دراسة واقع السوبرماركت أظهرت أن الإغلاق الصارم يسبب كوارث تشغيلية: تعليق طوابير العملاء عند استمرار خدمة الزبائن بعد انتهاء وقت الوردية، ومنع التغطية والتبديل الطارئ بين الموظفين عند المرض أو الغياب المفاجئ.
* **القرار (Decision):**
  * حصر مسؤولية **خدمة الهوية (`Identity Service`) في المصادقة البحتة (Authentication)**: التأكد من صحة الموظف، نشاط حسابه، صحة الـ PIN، وعدم وجود حظر أمني.
  * تفويض إدارة جداول الورديات لخدمة المبيعات والتشغيل (`Sales & Shifts Service`) بنمط **الورديات المرنة (Soft Shift Scheduling)**:
    1. تفتح الوردية يدوياً بطلب الكاشير وتوثق وقت البدء الفعلي.
    2. لا تُغلق الوردية قسرياً في وجه العميل، بل تظل مفتوحة حتى ينهي الكاشير طابوره، ويطابق عهدة الصندوق النقدي، ويطبع تقرير Z يدوياً.
    3. السماح بتغطية الورديات الطارئة عبر ميزة **موافقة المشرف (Supervisor Override)**.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** الحفاظ على مبدأ Single Responsibility وعدم توريط خدمة الهوية في منطق تشغيل المبيعات اليومية.
  * **إيجابي:** سلاسة تامة في خدمة عملاء المتجر دون أي توقف مفاجئ لنقاط البيع.

---

## 🏛️ ADR-ID-013: اعتماد المفاتيح المركبة والقيود الفريدة لسلامة البيانات التصريحية (Composite Keys & Unique Constraints)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * استخدام معرفات بديلة عشوائية (Surrogate Keys) في جداول الربط البحتة (مثل `role_perm_id` في `role_permissions`) يستهلك مساحة تخزين زائدة ويفرض إنشاء فهارس ثانوية مكررة.
  * إغفال القيود الفريدة المركبة في جداول الصلاحيات وساعات العمل يفتح الباب لثغرات الـ Race Conditions والتكرار غير المقصود مما يسبب انهيار استعلامات `SingleAsync()` في EF Core.
* **القرار (Decision):**
  * اعتماد **المفتاح الأساسي المركب (Composite Primary Key)** `(role_id, permission_id)` كـ PK وحيد لجدول `role_permissions` وحذف العمود الزائد `role_perm_id`.
  * فرض **قيود سلامة البيانات التصريحية (Declarative Unique Constraints)** على مستوى محرك PostgreSQL:
    1. `UNIQUE (resource, action, scope)` في جدول `permissions`.
    2. `UNIQUE (branch_id, day_of_week)` في جدول `branch_operating_hours`.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** استغلال الـ Clustered Index Locality لتسريع استعلامات استرجاع صلاحيات الدور بنسبة 50%.
  * **إيجابي:** حماية سلامة البيانات ومنع إدراج سجلات مكررة حتى في أقصى ظروف التزامن (Concurrency).

---

## 🏛️ ADR-ID-014: معمارية فصل أحداث الدومين عن أحداث التكامل ونمط الـ Outbox (In-Memory Domain Events vs Distributed Integration Events)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * تحتاج الكيانات (مثل `User`) لإشعار أجزاء أخرى من النظام عند حدوث تغييرات هامة (مثل إنشاء موظف، قفل حساب، نقل فرع).
  * إذا قامت طبقة الـ Domain باستدعاء MediatR مباشرة أو حقن MassTransit/RabbitMQ في الكيان، فإن ذلك ينتهك مبدأ انعكاس التبعية (Dependency Inversion) ويعزل الكيان عن كونه Pure C# Model، ويجعل كتابة الـ Unit Tests بالغة الصعوبة.
  * إذا فشل خادم الرسائل بعد حفظ البيانات في قاعدة البيانات، تصبح البيانات مشتتة بين الخدمات.
* **الخيارات المتاحة (Options):**
  1. *Option A:* استدعاء واجهات الرسائل الخارجية مباشرة من دوال الكيان.
  2. *Option B:* إطلاق أحداث MediatR متزامنة مباشرة من الـ Application Handlers دون تسجيلها في الكيان.
  3. *Option C:* اعتماد نمط **Collection-based Domain Events داخل `AggregateRoot`** مع تفريغ ونشر الأحداث عبر **EF Core Interceptor (`DispatchDomainEventsInterceptor`)** إلى MediatR محلياً، وتفويض النشر الموزع عبر RabbitMQ إلى **MassTransit Transactional Outbox**.
* **القرار (Decision):**
  * اعتماد **Option C**:
    1. يمتلك الكلاس الأساسي `AggregateRoot<TId>` قائمة داخلية خاصة `List<IDomainEvent> _domainEvents`.
    2. يقوم الكيان بتسجيل الحدث عبر `AddDomainEvent(...)` دون معرفة كيفية أو مكان نشره (Pure Domain).
    3. قبل أو بعد حفظ التغييرات في EF Core (`SaveChangesAsync`)، يقوم `DispatchDomainEventsInterceptor` باستخراج الأحداث ونشرها محلياً داخل نفس المعاملة (In-Memory via MediatR).
    4. إذا كان الحدث يتطلب إشعار خدمات خارجية (مثل Sales أو Notifications)، يقوم الـ Domain Event Handler في طبقة التطبيق بتحويله إلى `IntegrationEvent` وتخزينه في جدول `outbox_messages` لنشره بشكل موثوق ومضمون عبر RabbitMQ.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** استقلالية تامة لطبقة الـ Domain عن أي حزم خارجية (MediatR, RabbitMQ).
  * **إيجابي:** سهولة اختبار منطق الكيانات والتحقق من صدور الأحداث في اختبارات الوحدة (`user.DomainEvents.Should().ContainSingle(...)`).
  * **إيجابي:** ضمان عدم فقدان رسائل التكامل الموزعة (Guaranteed Delivery) بفضل الـ Outbox Pattern.

---

## 🏛️ ADR-ID-015: استراتيجية التحقق من صحة البيانات ذات المستويين (Two-Tier Validation: Pipeline FluentValidation vs Domain Invariants)

* **الحالة (Status):** مُعتمد (Approved).
* **السياق (Context):**
  * في نماذج إنشاء الكيانات (مثل `User.Create`)، تبرز معضلة التحقق: هل نكتفي بفحوصات `if (string.IsNullOrWhiteSpace(...))` المتسلسلة؟
  * الاعتماد الحصري على الفحص السريع المتسلسل (Fail-Fast Short-Circuiting) داخل دالة الإنشاء يسبب مشكلة **Error Ping-Pong** في واجهات المستخدم (تنبيه المستخدم بخطأ واحد تلو الآخر في كل محاولة إرسال).
  * كذلك، الفحوصات السطحية داخل الكيان (مثل فحص وجود `@` و `.` في الإيميل) تفتح ثغرات أمنية وبيانات مشوهة، وتورط الكيان في تفاصيل تنسيق نصوص ومحارف لا علاقة لها بقواعد البزنس الجوهرية.
* **الخيارات المتاحة (Options):**
  1. *Option A:* الاعتماد الحصري على Guard Clauses متسلسلة داخل دوال الـ Factory بالكيان.
  2. *Option B:* تحويل كل خاصية نصية إلى Value Object نقي (`Email`, `PhoneNumber`, `Username`).
  3. *Option C:* تطبيق نمط **التحقق ثنائي الطبقات (Two-Tier Validation Strategy)**:
     - **المستوى الأول (Application Layer):** خط أنابيب التحقق الشامل عبر **FluentValidation** داخل MediatR Pipeline، يقوم بفحص البنية، الصيغ المعقدة (Regex/RFC)، ويجمع **جميع الأخطاء دفعة واحدة** لإعادتها للعميل بتقرير موحد (ValidationProblemDetails - 400 Bad Request).
     - **المستوى الثاني (Domain Layer):** حراسة دفاعية سريعة لقواعد البزنس الحتمية (Defensive Domain Invariants) تمنع وجود كائن في الذاكرة بحالة مكسورة حتى لو تم استدعاؤه من Seeding أو Tests أو Background Jobs.
* **القرار (Decision):**
  * اعتماد **Option C**:
    1. تفويض فحص الصيغ وتجميع أخطاء واجهات الإدخال لـ FluentValidation في طبقة التطبيق.
    2. احتفاظ الكيان بحراسة صلبة سريعة للقيم غير المقبولة مطلقاً، مع تجنب منطق الفحص السطحي الهش داخل الكيان.
* **النتائج والآثار (Consequences):**
  * **إيجابي:** تجربة مستخدم واستهلاك شبكي ممتاز؛ العميل يتلقى قائمة كاملة بالأخطاء دفعة واحدة بدلاً من التكرار المزعج.
  * **إيجابي:** فصل واضح للمسؤوليات؛ طبقة التطبيق تفحص صحة المدخلات، وطبقة الدومين تحمي تكامل قواعد الأعمال.
  * **إيجابي:** منع الثغرات الناتجة عن الفحوصات الساذجة أو حقن المحارف الخبيثة.



