# Local Windows Benchmark Runbook

هذا المسار لتشغيل k6 على نفس جهاز Windows الذي يشغّل Identity API وخدمات Docker، عند عدم توفر Load Generator منفصل.

## 1. Install k6 Native

~~~powershell
winget install --id GrafanaLabs.k6 --exact --source winget
~~~

أغلق PowerShell وافتحه مجددًا:

~~~powershell
k6 version
~~~

لا تشغل k6 داخل Docker أو WSL لهذا المسار.

## 2. Start and verify the local application

تأكد من أن PostgreSQL وKeycloak وRedis يعملون، وشغّل Identity API على Windows Release/Production على المنفذ 5080.

~~~powershell
$env:BASE_URL = 'http://127.0.0.1:5080'
Test-NetConnection 127.0.0.1 -Port 5080
Test-NetConnection 127.0.0.1 -Port 8080
~~~

إذا كنت تستخدم منافذ أخرى، اضبط BASE_URL وKEYCLOAK_BASE_URL.

## 3. Provision test accounts

من جذر المستودع، في نافذة PowerShell نفسها:

~~~powershell
.\tests_production\k6\scripts\provision-perf-users.ps1
~~~

يجب أن ينتهي السكربت بنجاح وفحص Authorization:
- Store Manager Reader → HTTP 200
- Cashier → HTTP 403

ينشئ السكربت ملفات البيانات المحلية ويضبط READER_TOKEN وFORBIDDEN_TOKEN وCREATE_STAFF_TOKEN في نافذة PowerShell الحالية. لا تغلق النافذة قبل تشغيل الاختبارات، ولا تطبع/تشارك قيم هذه المتغيرات.

## 4. Authorization Smoke

~~~powershell
.\tests_production\k6\scripts\run-k6.ps1 -Test authz-smoke -Build discovery
~~~

لا تنتقل إلى الحمل إذا فشل أي Check.

## 5. First Staff Read Discovery Run

~~~powershell
$env:PREALLOCATED_VUS = '15'
$env:MAX_VUS = '60'
.\tests_production\k6\scripts\run-k6.ps1 -Test staff-read -Build discovery -RunId read-01 -CaptureTimeSeries
~~~

لا ترفع الـ stages قبل مراجعة p95/p99 وdropped_iterations ومؤشرات ضغط الجهاز.

## 6. Cashier Login and optional Registration

~~~powershell
.\tests_production\k6\scripts\run-k6.ps1 -Test cashier-login -Build discovery -RunId cashier-01
.\tests_production\k6\scripts\run-k6.ps1 -Test staff-registration -Build discovery -RunId register-01
~~~

Registration ينشئ سجلات دائمة في Test DB/Keycloak ويجب عدم تشغيله على بيانات إنتاج.

## 7. Host resource monitoring

خلال الاختبار، افتح نافذة ثانية:

~~~powershell
docker stats
~~~

وفي WSL:

~~~bash
free -h
vmstat 1
~~~

راقب Task Manager خصوصًا k6.exe وDocker Desktop/WSL. إذا تشبع مولد الحمل أو الجهاز ككل، لا تنسب هذا الحد إلى Identity API.

## 8. Interpretation limitation

Because k6 and the service stack share one physical host, classify these results as a co-located single-host benchmark. Use them for bottleneck discovery and controlled Before/After comparisons; do not claim independent production capacity from them.
