# k6 Performance Test Starter — SuperMarket POS

هذه الحزمة تختبر أهم مسارات Identity الحالية دون إدخال RabbitMQ أو Outbox أو Hangfire لمجرد إضافتها.

## السيناريوهات

1. `authz-smoke.js`: فحص وظيفي صغير للتحقق من 401 عند غياب/فساد JWT، و403 لمستخدم صحيح لا يملك `users:read`، و200 لمستخدم يملك الصلاحية. ليس Load Test.
2. `staff-read.js`: حمل Open-Model على `GET /api/v1/staff`. يقيس المسار المشترك بين JWT Authentication وDynamic Authorization وRedis وPostgreSQL.
3. `cashier-login.js`: حمل منخفض ومتدرج على `POST /api/v1/auth/cashier-login`. يقيس PBKDF2 وPostgreSQL وKeycloak Token Exchange ضمن مسار تسجيل الدخول الحقيقي.
4. `staff-registration.js`: اختبار اختياري منخفض المعدل على `POST /api/v1/staff`. ينشئ مستخدمين حقيقيين في قاعدة البيانات وKeycloak؛ شغّله فقط على بيئة اختبار قابلة للمسح أو التنظيف.

## 1) تثبيت k6 على جهاز Load Generator

على Windows:

```powershell
winget install k6 --source winget
k6 version
```

هذا الجهاز يجب أن يكون منفصلًا عن اللابتوب الذي يشغّل API وPostgreSQL وKeycloak وRedis. لا تشغّل k6 داخل Docker Compose على جهاز السيرفر نفسه أثناء قياس Capacity.

## 2) تجهيز الاتصال

على جهاز الاختبار، حدّد عنوان اللابتوب داخل الشبكة المحلية، ثم اختبر المنفذ:

```powershell
Test-NetConnection 192.168.1.10 -Port 5080
```

استبدل العنوان والمنفذ بالقيم الفعلية. استخدم عنوان LAN الخاص باللابتوب، لا `localhost`.

`BASE_URL` يجب أن يكون عنوان API المنشور على الشبكة، مثال:

```powershell
$env:BASE_URL = 'http://192.168.1.10:5080'
```

تأكد أن Windows Firewall يسمح بالاتصال بهذا المنفذ من جهاز الاختبار فقط، ولا تكشف PostgreSQL أو Redis أو منافذ إدارة Keycloak أو OTLP إلى شبكة غير موثوقة.

## 3) تجهيز حسابات الاختبار

انسخ ملفات الأمثلة داخل مجلد `data`:

```powershell
Copy-Item .\data\staff-tokens.example.json .\data\staff-tokens.json
Copy-Item .\data\cashiers.example.json .\data\cashiers.json
```

### staff-tokens.json

ضع JWT صالحًا صادرًا من Keycloak لمستخدم موجود محليًا في Identity DB، ويكون Claim `sub` فيه GUID مطابقًا لمعرف المستخدم، ويملك `users:read`. ضع عدة حسابات/توكنات إن أردت تمثيل عدة مستخدمين. يجب أن تكون التوكنات صالحة طوال الاختبار.

كل VU يستخدم عنصرًا من القائمة حسب رقم VU. إذا وضعت Token واحدًا فقط، فهذا اختبار مستخدم واحد/Hot Permission Cache، وليس اختبار تنوع المستخدمين. ضع عددًا من التوكنات مساويًا أو أكبر من `MAX_VUS` لتمثيل مستخدم مستقل لكل VU.

### cashiers.json

أنشئ حسابات Cashier تجريبية حقيقية (يفضل 5 حسابات على الأقل كبداية، واجعل عدد الحسابات مساويًا أو أكبر من `maxVUs` إذا أردت مستخدمًا مختلفًا لكل VU، واضبط PIN لكل حساب، واستخدم Register نشطًا ومتوافقًا مع Branch الحساب. ضع username وPIN وRegister GUID لكل حساب. الاختبار يستخدم عمليات Login ناجحة حتى لا يتسبب في Lockout.

لا تضع JWT أو PIN أو كلمة مرور حقيقية في Git. ملفات البيانات الحقيقية مستثناة من Git بواسطة `.gitignore`.

## 4) فحص Authorization قبل الحمل

في PowerShell:

```powershell
$env:BASE_URL = 'http://192.168.1.10:5080'
$env:READER_TOKEN = 'PASTE_VALID_USERS_READ_JWT'
$env:FORBIDDEN_TOKEN = 'PASTE_VALID_JWT_WITHOUT_USERS_READ'
k6 run .\scenarios\authz-smoke.js
```

النتائج الصحيحة المتوقعة: 401، 401، 403، 200 بهذا الترتيب. إذا فشل هذا الفحص، لا تبدأ Load Test.

## 5) أول Load Test — Staff Read

ابدأ بمعدل منخفض. نفّذ من جهاز Load Generator:

```powershell
$env:BASE_URL = 'http://192.168.1.10:5080'
New-Item -ItemType Directory -Force .\results\before | Out-Null
k6 run --tag build=before --summary-export .\results\before\staff-read-01.json .\scenarios\staff-read.js
```

السكربت يبدأ من 1 iteration/s ثم يصعد تدريجيًا إلى 5 ثم 10 ثم 20 ثم 30 iteration/s، مع مرحلة نزول. هذا يعادل تقريبًا RPS لأن كل Iteration فيها HTTP Request واحد. لا تبدأ بتعديل الأرقام قبل أول نتيجة.

ملاحظة: `ramping-arrival-rate` هو Open Model، ولا تضف `sleep()` داخل الـ iteration. راقب `dropped_iterations`؛ إذا ظهرت، افحص قدرة مولد الحمل وعدد VUs قبل تفسير النتيجة على أنها حد API.

### تعديل الحمل

في `staff-read.js` عدّل:

```javascript
{ target: 5, duration: '1m' },
{ target: 10, duration: '1m' },
{ target: 20, duration: '1m' },
{ target: 30, duration: '1m' },
```

`target` هو عدد Iterations في الثانية لأن `timeUnit` تساوي `1s`. ارفع درجة واحدة فقط في كل مرة. لا ترفع الحمل إذا كانت CPU أو RAM أو PostgreSQL أو Keycloak قد وصلت إلى Saturation.

يمكن تعديل إعداد VUs من PowerShell دون تغيير الملف:

```powershell
$env:PREALLOCATED_VUS = '20'
$env:MAX_VUS = '80'
```

إذا احتجت VUs أكثر، زدها تدريجيًا وتأكد أن جهاز الاختبار نفسه ليس مختنقًا.

## 6) Cashier Login Load Test

```powershell
$env:BASE_URL = 'http://192.168.1.10:5080'
New-Item -ItemType Directory -Force .\results\before | Out-Null
k6 run --tag build=before --summary-export .\results\before\cashier-login-01.json .\scenarios\cashier-login.js
```

هذا السيناريو محافظ عمدًا: 1 ثم 2 ثم 3 ثم 5 Login attempts/s. ابدأ به كما هو. كل محاولة ناجحة تكتب LastLogin في قاعدة البيانات وتنفذ Token Exchange في Keycloak، ولذلك لا تقارنه مع GET Read على أنه نفس نوع العمل.

تأكد أن Token Lifetimes ومدة الاختبار لا تسبب انتهاء صلاحية أي توكن داخلي أو حساب اختبار. لا تستخدم حسابات حقيقية.

## 7) Staff Registration — اختياري ومدمّر للبيانات

هذا السيناريو ينشئ حسابات جديدة بالفعل. شغّله فقط على Test Realm وقاعدة بيانات اختبار، وبعد التأكد من وجود Role فعال في Keycloak وIdentity DB، وToken يملك `users:create`.

```powershell
$env:BASE_URL = 'http://192.168.1.10:5080'
$env:CREATE_STAFF_TOKEN = 'PASTE_VALID_USERS_CREATE_JWT'
$env:ROLE_ID = 'EXISTING_ROLE_GUID'
$env:BRANCH_ID = ''
$env:TEST_INITIAL_PASSWORD = 'Disposable-Password-Only'
$env:RUN_ID = '4721'
k6 run --tag build=before --summary-export .\results\before\staff-registration-01.json .\scenarios\staff-registration.js
```

السيناريو مضبوط مبدئيًا على طلب واحد كل 10 ثوانٍ لمدة دقيقة (نحو 6 مستخدمين). غيّر `RUN_ID` في كل تشغيل إلى رقم جديد من 4–6 أرقام لتجنب تكرار Username/Phone/Email. لا تشغّله على بيانات إنتاج، ولا ترفع معدل الإنشاء قبل توفير خطة تنظيف.

## 8) كيف تقرأ النتيجة

ركّز على:

- `http_req_duration`: افحص p50 وp90 وp95 وp99، وليس Average فقط.
- `http_reqs`: إجمالي الطلبات ومعدلها الفعلي.
- `http_req_failed`: نسبة فشل HTTP.
- `checks`: هل رجعت الاستجابات المتوقعة؟
- `dropped_iterations`: هل استطاع k6 بدء كل Iterations المجدولة؟
- `vus` و`vus_max`: موارد مولد الحمل المستخدمة.

لا تعتبر RPS المستهدف هو RPS المنفذ؛ اعتمد على `http_reqs` والمدة الفعلية وراقب `dropped_iterations`.

## 9) ماذا تراقب على السيرفر بالتزامن؟

في نافذة منفصلة على السيرفر:

```powershell
docker stats
```

داخل WSL:

```bash
free -h
vmstat 1
```

PostgreSQL أثناء/بعد التجربة:

```sql
SELECT state, COUNT(*)
FROM pg_stat_activity
GROUP BY state;
```

ثم افتح Grafana/Prometheus/Jaeger الموجودين في المشروع. قارن Trace بطيئًا عن طريق `X-Trace-Id` من الاستجابة، إن كان متاحًا. لا تفعّل Logging تفصيليًا لكل SQL أثناء قياس الأداء النهائي؛ يمكن استخدامه في جلسة تشخيص منفصلة.

## 10) Before vs After

احفظ النتائج خارج مجلدات البناء، ولا تشغّل نسختي API معًا على نفس الموارد.

1. ثبّت `BEFORE_SHA` و`AFTER_SHA` في التقرير.
2. شغّل Before بالإعدادات نفسها والبيانات نفسها، وخزّن النتائج في `results/before`.
3. أوقف النسخة، شغّل After بالإعدادات نفسها، ثم خزّن النتائج في `results/after`.
4. كرر كل تجربة ثلاث مرات على الأقل. الأفضل تنفيذ تشغيلات متناوبة (Before ثم After ثم Before ثم After) بعد Warm-up.
5. لا تغيّر Docker limits أو GC أو Logging أو حجم البيانات بين النسختين. إذا أردت قياس أثر إعداد Server GC، اجعله تجربة مستقلة.

لا تقارن فقط Commit `2edd821` مع `1895a3b` على أنها مقارنة لإصلاحات Identity: الـ Commit الأخير أضاف إعدادات موارد Compose وServer GC وProduction Logging، أي أنه يغيّر عوامل أداء أخرى. اختر Commit ما قبل إصلاح Identity الفعلي وCommit بعده، وثبّت Runtime Configuration نفسها في الجانبين.

## 11) الملفات الحساسة والنتائج

- لا ترفع `data/staff-tokens.json` أو `data/cashiers.json`.
- لا ترفع نتائج تحتوي Authorization Headers أو Tokens. Summary export عادةً لا يحتاج حفظ Headers، لكن راجع أي Raw output قبل نشره.
- لا تستخدم `FLUSHALL` على Redis. امسح مفاتيح مستخدمي الاختبار وحدها عند إجراء تجربة Cache باردة.
- لا تعلن Maximum Capacity من تشغيل واحد أو من تشغيل على بيئة محلية. اذكر CPU/RAM والنسخة وإعدادات الاختبار.

## 12) إذا لم يتوفر جهاز ثانٍ: k6 على نفس Windows

يمكنك تثبيت k6 مباشرة على Windows وتشغيله من نفس اللابتوب. استخدم هذا كـ **co-located local benchmark** لا كقياس Production Capacity مستقل؛ لأن k6 ينافس API وPostgreSQL وKeycloak وRedis على CPU والذاكرة الفعلية نفسها.

راجع `LOCAL_WINDOWS_RUNBOOK.md` للتثبيت والأوامر، واستخدم `scripts/run-k6.ps1` لحفظ Summary وConsole وMetadata، وفعّل `-CaptureTimeSeries` فقط عند الحاجة للتحليل الزمني. عند التشغيل المحلي يكون عنوان API عادةً:

```powershell
$env:BASE_URL = 'http://127.0.0.1:5080'
```

## 13) التقرير النهائي

قالب التقرير الشامل موجود في `../../../docs/performance/REPORT_TEMPLATE.md`، وقائمة الأدلة المطلوبة في `REPORT_EVIDENCE_CHECKLIST.md`.

بعد انتهاء الاختبارات، اجمع ملفات Summary وMetadata وConsole وقياسات الموارد ولقطات Grafana وTraces المناسبة في مجلد النتائج. سنستخدمها لإعداد تقرير يتضمن النتائج الرقمية، مقارنة Before/After، تفسير Capacity، Root Cause المدعوم بالأدلة، القيود، والقرارات الهندسية المقترحة. لن نكتب نتائج أو نسب تحسن قبل توفر القياسات الفعلية.
