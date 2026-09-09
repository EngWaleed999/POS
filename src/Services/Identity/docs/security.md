# 🔒 وثيقة الأمان والترخيص (Security & Authorization Architecture)

## 📌 1. نموذج الأمان عديم الثقة لنقاط البيع (Zero-Trust POS Security Architecture)

في بيئة السوبرماركت ومحطات نقاط البيع (POS)، نطبق مبدأ **Zero-Trust**: لا نثق بأي طلب حتى لو كان قادماً من شبكة الفرع الداخلية:
1. **كل اتصال مشفر:** عبر TLS 1.3 بين أجهزة الـ POS والـ API Gateway والخدمات الخلفية.
2. **التحقق الثنائي من الجهاز والمستخدم:** لا يكفي أن يحمل الكاشير رمزاً صحيحاً، بل يجب أن يكون الجهاز (`POSRegister`) مسجلاً ونشطاً في نفس الفرع (`BranchId`).
3. **مبدأ أقل الصلاحيات (Least Privilege):** الكاشير يمتلك صلاحيات البيع فقط، ولا يمكنه إلغاء صنف أو فتح الدرج يدوياً إلا بموافقة إلكترونية موثقة من المشرف.

---

## 🔑 2. التشفير والتحقق الرياضي من الـ Tokens (Cryptographic Token Validation)

تعتمد خدمة `SuperMarket.Identity.API` على خوارزمية **RS256 (RSA Signature with SHA-256)**:
* **Keycloak (Private Key):** يقوم بتوقيع الـ JWT بالمفتاح السري الخاص به.
* **Backend Services (Public Key):** تقوم بالتحقق من صحة التوقيع عبر المفتاح العام المسحوب من:
  `http://keycloak:8080/realms/supermarket/protocol/openid-connect/certs`

```csharp
// ضبط التحقق الآمن في ASP.NET Core
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/supermarket";
        options.Audience = "pos-api";
        options.RequireHttpsMetadata = false; // true في بيئة Production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://localhost:8080/realms/supermarket",
            ValidateAudience = true,
            ValidAudience = "pos-api",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30) // مهلة 30 ثانية لتفاوت توقيت السيرفرات
        };
    });
```

---

## 🛡️ 3. الحماية ضد ثغرات وتحديات نقاط البيع (POS Security Top Controls)

| التهديد الأمني | وصف الخطر في السوبرماركت | آلية الحماية المطبقة في النظام |
|---|---|---|
| **Brute-Force PIN Attack** | محاولة تخمين الرمز السري للكاشير (4 أرقام) على شاشة البيع. | قفل الشاشة مؤقتاً لمدة 5 دقائق بعد 5 محاولات فاشلة متتالية، وإرسال تنبيه فوري للمشرف. |
| **Rogue POS Terminal** | توصيل لابتوب أو جهاز خارجي بشبكة الفرع وتسجيل مبيعات وهمية. | فحص بصمة العتاد (`DeviceFingerprint` / MAC Address) ورفض أي طلب قادم من جهاز غير مسجل كـ `POSRegister`. |
| **Cash Drawer Theft** | فتح درج الكاشير بدون عملية بيع وسرقة النقدية. | منع أمر `No-Sale Drawer Kick` إلا بصلاحية المشرف (`POS.Drawer.KickNoSale`) وتوثيقه في `AuditLogService`. |
| **Unauthorized Void/Discount** | إلغاء صنف بعد استلام النقدية من العميل لاختلاس المبلغ. | إيقاف العملية وإلزام شاشة الـ POS بطلب موافقة المشرف (Supervisor Override Token) قبل قبول الإلغاء. |
| **Broken Object Level Auth (BOLA)** | كاشير في الفرع A يفتح وردية أو يطبع تقرير Z لفرع B. | التحقق الإلزامي من تطابق `BranchId` الموجود في الـ JWT مع الفرع المستهدف في الـ Request. |

---

## 👥 4. الصلاحيات المبنية على السياسات (Policy-Based Authorization)

نترجم الأدوار الوظيفية داخل السوبرماركت إلى سياسات وصلاحيات دقيقة (**PBAC**):

```csharp
// تعريف السياسات في Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CashierPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Cashier", "Supervisor", "BranchManager", "Admin"));

    options.AddPolicy("SupervisorPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Supervisor", "BranchManager", "Admin"));

    options.AddPolicy("BranchManagerPolicy", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("BranchManager", "Admin"));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Admin"));
});
```

---

## 🔏 5. حماية البيانات الحساسة وتشفير الـ PIN

1. **تشفير الرمز السري السريع (PIN Hashing):**
   * الرمز السري للكاشير والمشرف (4-6 أرقام) لا يخزن أبداً كنص صريح (Plaintext).
   * يتم تشفيره باستخدام خوارزمية **Argon2id** مع Salt فريد وعالي العشوائية لكل موظف.
2. **حجب البيانات في السجلات (Log Redaction):**
   * منع تسجيل كلمات المرور، رموز الـ PIN، أرقام بطاقات مدى/الائتمان، أو الـ Tokens داخل ملفات الـ Logs باستخدام Serilog Destructuring & Masking.
