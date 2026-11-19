# Evidence checklist — أرسل هذه الأدلة لإعداد التقرير النهائي

لكل سيناريو ولكل نسخة Before/After:

- [ ] ملف `*-summary.json`
- [ ] ملف `*-console.txt`
- [ ] ملف `*-metadata.json`
- [ ] `*-timeseries.json` للتشغيل التشخيصي الممثل، إن تم تفعيله
- [ ] صورة Task Manager أثناء أعلى مرحلة حمل، تظهر CPU وMemory و`k6.exe`
- [ ] `docker stats` أثناء الاختبار أو مقاييس Prometheus للفترة نفسها
- [ ] `free -h` و`vmstat 1` من WSL إذا كانت الخدمات تعمل داخله
- [ ] لقطة Grafana للـ API وPostgreSQL وKeycloak وRedis
- [ ] Trace واحد بطيء من Jaeger لطلب ممثل، مع إخفاء أي بيانات حساسة
- [ ] SQL execution plan عند الاشتباه في Query بطيء
- [ ] SHA للكود وإعدادات الموارد ونسخ الخدمات المستخدمة

لا ترسل ملفات `staff-tokens.json` أو `cashiers.json` أو JWTs أو Passwords أو Client Secrets. أرسل نتائج القياس فقط بعد مراجعة عدم وجود أسرار فيها.
