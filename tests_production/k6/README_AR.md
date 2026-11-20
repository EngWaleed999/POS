# k6 Performance Test Starter — SuperMarket POS

هذه الحزمة تختبر مسارات Identity الفعلية: Authorization، قراءة الموظفين، دخول الكاشير، وتسجيل الموظفين. لا تضيف RabbitMQ أو Outbox أو Hangfire لمجرد وجودها.

## السيناريوهات

| الملف | الهدف |
|---|---|
| authz-smoke.js | فحص 401 عند غياب/فساد JWT، و403 لمستخدم Cashier، و200 لمستخدم Store Manager |
| staff-read.js | Open-model load على GET /api/v1/staff ويغطي JWT + Dynamic Authorization + Redis + PostgreSQL |
| cashier-login.js | حمل محافظ على POST /api/v1/auth/cashier-login ويغطي PIN hashing + PostgreSQL + Keycloak Token Exchange |
| staff-registration.js | اختبار اختياري منخفض المعدل ينشئ حسابات فعلية في Keycloak وPostgreSQL |

## 1. المتطلبات

- Docker Desktop وWSL2 يعملان، وPostgreSQL وKeycloak وRedis متاحون.
- Identity API يعمل على http://127.0.0.1:5080.
- k6 مثبت Native على Windows عند استخدام الاختبار المحلي.
- قاعدة بيانات وRealm مخصصان للاختبار، وليس لبيانات إنتاج.

إذا كانت قاعدة البيانات جديدة، تأكد من تطبيق migrations وتشغيل IdentityDataSeeder مرة واحدة في بيئة Development قبل تشغيل API في Production للاختبار. الـ seeder ينشئ حساب Realm التجريبي admin بكلمة المرور الافتراضية admin123 ما لم تكن قد غيّرتها.

## 2. إنشاء بيانات الاختبار تلقائيًا

من جذر المشروع، في نافذة PowerShell:

~~~powershell
$env:BASE_URL = 'http://127.0.0.1:5080'
.\tests_production\k6\scripts\provision-perf-users.ps1
~~~

السكربت:
1. يصدر JWT لحساب System Admin من Realm supermarket عبر Client pos-terminal. لا يستخدم Master Realm token مع Identity API.
2. يتحقق من التوكن بالاتصال الفعلي بـ GET /api/v1/roles.
3. ينشئ/يعيد استخدام Branch مخصص برمز PERF-HQ-01 من خلال Branch API.
4. ينشئ/يعيد استخدام POS Register نشطًا. لا يوجد POS Register endpoint في Identity API حاليًا؛ لذلك يستخدم psql داخل حاوية PostgreSQL لهذه الخطوة فقط.
5. ينشئ/يعيد استخدام Store Manager Readers وCashier Users عبر POST /api/v1/staff.
6. يضبط Cashier PIN عبر PUT /api/v1/staff/{id}/pin.
7. يصدر JWT لكل Reader وواحدًا من Cashier.
8. يتحقق من النتيجة الفعلية: Reader يحصل على 200 وCashier يحصل على 403.
9. يحفظ ملفات JSON محلية دون UTF-8 BOM ويضبط متغيرات البيئة في نافذة PowerShell الحالية.

الإعداد الافتراضي: 10 Readers و5 Cashiers. يمكن تغييره:

~~~powershell
.\tests_production\k6\scripts\provision-perf-users.ps1 -ReaderCount 20 -CashierCount 5
~~~

الافتراضي لحساب Identity Admin هو admin/admin123، وهو حساب تجريبي ينشئه IdentityDataSeeder. عند تغييره، مرر IdentityAdminUsername وIdentityAdminPassword أو عرّف IDENTITY_ADMIN_USERNAME وIDENTITY_ADMIN_PASSWORD.

مهم: استخدم السكربت فقط على Test Realm/Test Database. ينشئ حسابات وBranch وRegister فعلية. لا يطبع JWT أو PIN في الطرفية.

الملفات التي ينشئها السكربت محليًا:

~~~text
tests_production/k6/data/staff-tokens.json
tests_production/k6/data/cashiers.json
~~~

هذه الملفات مستثناة من Git. لا ترفعها أبدًا.

## 3. Authorization Smoke

نفّذ في نفس نافذة PowerShell التي شغّلت فيها سكربت التجهيز:

~~~powershell
.\tests_production\k6\scripts\run-k6.ps1 -Test authz-smoke -Build discovery
~~~

النتيجة المتوقعة: 401، 401، 403، 200. لا تبدأ Load Test إذا فشل أي Check.

## 4. أول Load Test — Staff Read

~~~powershell
$env:PREALLOCATED_VUS = '15'
$env:MAX_VUS = '60'
.\tests_production\k6\scripts\run-k6.ps1 -Test staff-read -Build discovery -RunId read-01 -CaptureTimeSeries
~~~

الـ stages الافتراضية تستهدف 5 ثم 10 ثم 20 ثم 30 Iterations/s، وكل Iteration تحتوي HTTP request واحدًا. راقب http_reqs الفعلي، p50/p90/p95/p99، errors، checks، وdropped_iterations.

الـ token pool الافتراضي يحتوي 10 مستخدمين. يمكن زيادة ReaderCount عند الحاجة إلى توزيع أوسع للمستخدمين.

## 5. Cashier Login

~~~powershell
.\tests_production\k6\scripts\run-k6.ps1 -Test cashier-login -Build discovery -RunId cashier-01
~~~

يبدأ الاختبار عند 1 ثم 2 ثم 3 ثم 5 Login attempts/s. كل Login ناجح يحدّث LastLogin ويتصل بـ Keycloak. لا تقارن زمنه مع GET كأنهما نفس workload.

## 6. Staff Registration — اختياري

الـ provisioner يضبط متغيرات البيئة المطلوبة في نافذة PowerShell الحالية. الاختبار ينشئ نحو 6 مستخدمين:

~~~powershell
.\tests_production\k6\scripts\run-k6.ps1 -Test staff-registration -Build discovery -RunId register-01
~~~

شغّله فقط على بيانات اختبار قابلة للتنظيف. غيّر RunId في كل تشغيل لتجنب تكرار Username/Phone/Email.

## 7. المراقبة

أثناء الاختبار:

~~~powershell
docker stats
~~~

داخل WSL:

~~~bash
free -h
vmstat 1
~~~

PostgreSQL:

~~~sql
SELECT state, COUNT(*)
FROM pg_stat_activity
GROUP BY state;
~~~

افتح Grafana/Prometheus/Jaeger واحفظ Trace ممثلًا للطلبات البطيئة. لا تشغّل SQL logging التفصيلي أثناء قياس الأداء النهائي؛ استخدمه في جلسة تشخيص منفصلة.

## 8. Before vs After

ثبّت SHAs وإعدادات التشغيل نفسها في النسختين. شغّل Before وAfter بالتتابع وليس معًا، وكرر كل حالة 3 مرات على الأقل بعد warm-up. لا تغيّر Docker limits أو GC أو Logging أو حجم البيانات بين المقارنتين.

احفظ النتائج في tests_production/k6/results/before وafter. الـ runner ينشئ Summary وConsole وMetadata، ويمكنه إنشاء JSON time-series عند استخدام -CaptureTimeSeries.

## 9. التشغيل على نفس اللابتوب

عند عدم توفر جهاز ثانٍ، شغّل k6 Native على Windows واستخدم 127.0.0.1. هذه النتائج co-located single-host benchmark: صالحة لاكتشاف الاختناقات ومقارنة Before/After تحت ظروف ثابتة، لكنها ليست إثباتًا مستقلًا لسعة Production لأن مولد الحمل والخدمات يتشاركون CPU والذاكرة.

راجع LOCAL_WINDOWS_RUNBOOK.md للأوامر المتسلسلة.

## 10. التقرير النهائي

قالب التقرير: ../../docs/performance/REPORT_TEMPLATE.md.
قائمة الأدلة: REPORT_EVIDENCE_CHECKLIST.md.

لا تكتب أي رقم أداء أو نسبة تحسن قبل توفر ملفات القياس الفعلية.
