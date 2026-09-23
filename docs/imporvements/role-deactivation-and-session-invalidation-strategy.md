# 💡 تحسين مؤجل: استراتيجية تعطيل الأدوار والإلغاء الفوري للجلسات النشطة
## Role Deactivation & Instant Session Invalidation Strategy

* **الحالة:** مؤجل للتطوير المستقبلي (Deferred Architecture Improvement)
* **التاريخ:** 2026-09-23
* **المشروع المستهدف:** `SuperMarket.Identity` (Domain & Infrastructure)
* **المكونات المرتبطة:** `Role`, `RoleDeactivatedDomainEvent`, `Keycloak Admin Client`, `Distributed Token Blacklist`

---

## 1. سياق المشكلة والواقع الحالي (Current Behavior)

في المعمارية الحالية المعتمدة على **Stateless JWT Tokens**:
1. عند تسجيل دخول الموظف، يحصل على `Access Token` مشفر وموقع رقمياً (RS256) يحمل صلاحياته ودوره، بصلاحية زمنية محددة (مثلاً 15 دقيقة).
2. الـ API يفحص التوكنات بطريقة **عديمة الحالة (Stateless via JWKS)** محلياً في الذاكرة دون الرجوع لقاعدة البيانات في كل طلب (كما هو موثق في `ADR-ID-004`).
3. **السلوك الحالي عند التعطيل:**
   - عند قيام مدير النظام بتعطيل دور وظيفي (`Role.Deactivate()`) في قاعدة البيانات:
   - المستخدمون الذين يحملون جلسات نشطة وتوكنات سارية **لن يتم طردهم فورياً**؛ بل سيستمر الـ API بقبول طلباتهم حتى ينتهي الـ Access Token الخاص بهم.
   - بمجرد انتهاء التوكن ومحاولة الـ Client تجديده عبر الـ Refresh Token، سيفشل التجديد لأن Keycloak أو الـ Backend سيتحقق من حالة الحساب والدور ويرفض العملية.

---

## 2. المخاطر الأمنية وحالات الاستخدام (Security Risk)

في سيناريوهات معينة في السوبرماركت:
- **اكتشاف اختراق أو احتيال مالي:** إذا تم رصد تلاعب على كاشيرات دور معين، قد يتطلب الأمر تجميد الدور وطردهم **لحظياً بالثانية** دون الانتظار حتى انتهاء الـ 15 دقيقة للتوكن.
- هذا السلوك يتطلب آلية **الإلغاء القسري الفوري (Instant Revocation)**.

---

## 3. الحل المعماري المستقبلي المقترح (Future Architecture Design)

عند تقرير تفعيل هذه الميزة، سيتم تطبيق الخطوات التالية:

### أ. على مستوى طبقة الـ Domain:
1. تحويل `Role` إلى `AggregateRoot<Guid>` ليتمكن من إطلاق أحداث المجال.
2. إضافة حدث دومين مخصص:
   ```csharp
   public sealed record RoleDeactivatedDomainEvent(Guid RoleId, DateTimeOffset DeactivatedAt) : IDomainEvent;
   ```
3. إطلاق الحدث داخل دالة `Deactivate()`:
   ```csharp
   public Result Deactivate()
   {
       if (!IsActive)
           return Result.Failure(RoleErrors.AlreadyDeactivated);

       IsActive = false;
       AddDomainEvent(new RoleDeactivatedDomainEvent(Id, DateTimeOffset.UtcNow));
       return Result.Success();
   }
   ```

### ب. على مستوى طبقة التطبيق والـ Infrastructure:
إنشاء معالج للحدث `RoleDeactivatedDomainEventHandler` ينفذ إحدى استراتيجيتين (أو كلاهما):

1. **الاستراتيجية الأولى (Keycloak Admin Session Invalidation):**
   - استدعاء Keycloak Admin REST API لإنهاء جميع جلسات المستخدمين المسندين لهذا الدور:
     `POST /admin/realms/{realm}/users/{id}/logout`
2. **الاستراتيجية الثانية (Redis Token Blacklisting):**
   - نشر مفتاح في Redis يحمل طابعاً زمنياً:
     `Key: revoked_roles:{role_id}` -> `Value: deactivated_timestamp`
   - يقوم الـ Authorization Middleware بمقارنة وقت إصدار التوكن (`iat` claim) مع وقت الإلغاء؛ وإذا كان التوكن صادراً قبل الإلغاء يتم رفضه فوراً بـ `401 Unauthorized`.

---

## 4. سبب تأجيل الميزة في الـ Sprint الحالي (Engineering Justification)

1. **التركيز وإنهاء نطاق الـ Domain:** هدف الـ Sprint الحالي هو بناء واستقرار كيانات الـ Domain الأساسية وقواعد البيانات للفروع والمستخدمين دون التورط في تعقيدات الربط الشبكي للـ Blacklisting.
2. **فترة صلاحية التوكنات قصيرة:** يتم ضبط الـ Access Token للكاشير على مدة قصيرة (5–10 دقائق)، وهي نافذة مقبولة في المرحلة الأولى من النظام.
3. **تجنب التعقيد غير المبرر (YAGNI / Anti-Overengineering):** بناء Token Blacklisting يتطلب Redis و Distributed Interceptor؛ تأجيله يوفر سرعة إنجاز ويمنع تشتيت الفريق.
