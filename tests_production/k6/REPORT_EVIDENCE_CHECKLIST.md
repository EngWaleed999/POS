# Performance Benchmark Evidence Checklist

لكل سيناريو ولكل نسخة Before/After:

- [ ] *-summary.json
- [ ] *-console.txt
- [ ] *-metadata.json
- [ ] *-timeseries.json للتشغيل التشخيصي الممثل عند الحاجة
- [ ] صورة Task Manager عند أعلى مرحلة حمل، تظهر CPU وMemory وk6.exe
- [ ] docker stats أو مقاييس Prometheus للفترة نفسها
- [ ] free -h وvmstat 1 من WSL إذا كانت الخدمات تعمل داخله
- [ ] لقطات Grafana لمقاييس API وPostgreSQL وRedis وKeycloak المتاحة
- [ ] Trace بطيء واحد على الأقل من Jaeger مع إخفاء البيانات الحساسة
- [ ] SQL execution plan عند وجود Query مشتبه ببطئه
- [ ] Git SHA وإعدادات الموارد وإصدارات الخدمات المستخدمة
- [ ] عدد حسابات الاختبار وأدوارها وحجم بيانات PostgreSQL (بدون بيانات اعتماد)

لا ترسل أو ترفع staff-tokens.json أو cashiers.json أو JWTs أو PINs أو Passwords أو Client Secrets. راجع كل Console/Metadata/Timeseries قبل مشاركتها.
