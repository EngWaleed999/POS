# تشغيل k6 على نفس جهاز Windows — وضع الاختبار المحلي

هذا المسار مناسب عندما لا يتوفر جهاز ثانٍ. ثبّت k6 Native على Windows، ولا تشغّله داخل WSL أو Docker. شغّل API والخدمات التابعة في وضع ثابت، ثم أرسل الطلبات إلى `127.0.0.1`.

## 1. التثبيت والتحقق

```powershell
winget install --id GrafanaLabs.k6 --exact --source winget
# أغلق PowerShell وافتحه مجددًا
k6 version
```

مرجع التثبيت: https://grafana.com/docs/k6/latest/set-up/install-k6/

## 2. الاتصال المحلي

إذا كان Identity API يعمل على Windows على المنفذ 5080:

```powershell
$env:BASE_URL = 'http://127.0.0.1:5080'
Test-NetConnection 127.0.0.1 -Port 5080
```

إذا كان API داخل Docker، استخدم المنفذ المنشور على Windows. وإذا كان API داخل WSL2، جرّب `127.0.0.1:<published-port>` أولًا.

## 3. قبل تشغيل الحمل

- ابدأ بالخدمات الضرورية فقط، ثم أضف Observability Stack عند الحاجة.
- لا تنفذ Docker build أو Restore أو اختبارات أخرى بالتزامن.
- افتح Task Manager وراقب CPU وMemory، خصوصًا `k6.exe` وDocker Desktop/WSL.
- في WSL نفّذ `free -h` و`vmstat 1` وراقب Swap In/Out.
- ابدأ بمراحل الحمل الافتراضية المنخفضة في `staff-read.js` ولا ترفعها قبل مراجعة أول تشغيل.
- إذا أصبح k6.exe أو Windows host مشبعًا، أو ظهرت ضغوط CPU/RAM/Swap على الجهاز كله، لا تنسب حد الحمل إلى API.

## 4. تشغيل الاختبار مع حفظ الأدلة

من جذر المشروع، بعد ضبط متغيرات البيئة وملفات البيانات:

```powershell
$env:BASE_URL = 'http://127.0.0.1:5080'
$env:READER_TOKEN = '...' # للـ smoke فقط
$env:FORBIDDEN_TOKEN = '...' # للـ smoke فقط

.\tests\load\k6\scripts\run-k6.ps1 -Test authz-smoke -Build discovery
```

أول حمل Staff Read:

```powershell
$env:BASE_URL = 'http://127.0.0.1:5080'
.\tests\load\k6\scripts\run-k6.ps1 -Test staff-read -Build discovery -CaptureTimeSeries
```

تشغيلات Before وAfter بعد تثبيت الـ commit المناسب:

```powershell
.\tests\load\k6\scripts\run-k6.ps1 -Test staff-read -Build before -RunId run-01 -CaptureTimeSeries
.\tests\load\k6\scripts\run-k6.ps1 -Test staff-read -Build after  -RunId run-01 -CaptureTimeSeries
```

يُنشئ المشغّل ملفات:
- `*-summary.json`: مقاييس k6 المجمعة والـ thresholds/checks.
- `*-console.txt`: المخرجات النصية الكاملة.
- `*-metadata.json`: إصدار k6 وبيانات الجهاز وGit SHA ووقت التشغيل (لا يحفظ Tokens أو كلمات مرور).
- `*-timeseries.json`: نقاط القياس الزمنية عند استخدام `-CaptureTimeSeries`، وقد يكون كبيرًا؛ استخدمه في التشغيلات التشخيصية لا كل تشغيل طويل.

## 5. ما الذي يعنيه هذا الاختبار؟

بما أن k6 والخدمات على نفس الجهاز الفعلي، فالنتيجة تسمى co-located single-host benchmark. تصلح لاكتشاف الاختناقات ومقارنة Before/After تحت نفس الظروف. لا تستخدمها كإثبات مستقل لسعة Production، إلا إذا أثبتت أن مولد الحمل لم يكن هو العامل المحدد. اذكر هذا القيد في التقرير النهائي.
