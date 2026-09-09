# ⚡ وثيقة الأداء وقابلية التوسع لنقاط البيع (Performance & Scalability)

## 📌 1. أداء التحقق عديم الحالة (Sub-Millisecond Stateless Auth)

* **السر وراء السرعة الفائقة لشاشات الكاشير:** التحقق من صحة توقيع الـ JWT لا يستدعي أي استعلام شبكي إلى Keycloak أثناء عمليات البيع.
* يتم سحب المفاتيح العامة (JWKS) وتخزينها في ذاكرة تطبيق .NET تلقائياً مع تحديث دوري، مما يجعل زمن التحقق من الـ Token **أقل من 1 ميلي ثانية (Sub-millisecond)** حتى مع ذروة آلاف عمليات المسح في الفروع.

---

## 🗄️ 2. استراتيجية فهارس قاعدة البيانات (PostgreSQL Indexing Strategy)

لضمان تنفيذ استعلامات الفروع والكاشير وأجهزة الـ POS في زمن **< 5ms**:

```sql
-- 1. فهرس فريد للبحث عن الفروع بكود الفرع (مثل BR-01)
CREATE UNIQUE INDEX idx_branches_branch_code ON branches (branch_code);

-- 2. فهرس مركب للبحث عن أجهزة الكاشير النشطة في فرع محدد
CREATE INDEX idx_pos_registers_branch_status 
ON pos_registers (branch_id, status);

-- 3. فهرس فريد لرقم جهاز الكاشير وبصمته لمنع التكرار
CREATE UNIQUE INDEX idx_pos_registers_number ON pos_registers (register_number);
CREATE UNIQUE INDEX idx_pos_registers_fingerprint ON pos_registers (device_fingerprint);

-- 4. فهرس فريد لكود الموظف لتسريع عملية الـ PIN Login
CREATE UNIQUE INDEX idx_staff_employee_code ON staff_members (employee_code);

-- 5. فهرس مركب لجلب موظفي الفرع حسب الدور (كاشير / مشرف)
CREATE INDEX idx_staff_branch_role ON staff_members (branch_id, role);

-- 6. فهرس جزئي لمعالجة رسائل الـ Outbox غير المنشورة فقط
CREATE INDEX idx_outbox_unprocessed 
ON outbox_messages (created_at) 
WHERE processed_at IS NULL;
```

---

## 🚀 3. التخزين المؤقت الموزع (Distributed Caching via Redis)

نطبق نمط **Cache-Aside Pattern** لبيانات الأجهزة والفروع:
1. **بيانات الفرع وأجهزة الكاشير المصرحة:**
   * عند بدء تشغيل جهاز الكاشير، يفحص النظام **Redis** أولاً:
     * **Cache Hit:** استرجاع بيانات الفرع والـ POS في زمن **< 1ms**.
     * **Cache Miss:** جلب السجل من PostgreSQL وتخزينه في Redis مع صلاحية (TTL = 1 ساعة).
2. **إبطال الكاش (Cache Invalidation):** عند تعطيل كاشير أو نقل موظف أو تعديل حالة جهاز POS، يتم إبطال المفتاح فوراً (`pos:register:{number}` أو `staff:session:{id}`) لضمان الأمان اللحظي.

---

## ⚙️ 4. تحسينات الذاكرة وإدارة الموارد في .NET 10

1. **`AsNoTracking()` في كافة استعلامات القراءة:** لتفادي تكلفة الـ EF Core Change Tracker في جلب بيانات الموظفين والفروع.
2. **استخدام `ValueTask`:** في عمليات فحص الصلاحيات وذاكرة الكاش لتقليل تخصيصات الذاكرة (Zero-Heap Allocations).
3. **Database Connection Pooling:** ضبط الحد الأقصى للاتصالات عبر Npgsql لتفادي استنزاف موارد PostgreSQL وقت ذروة الورديات.
4. **`ReadOnlySpan<char>`:** لمعالجة وفحص نصوص الـ Barcodes وأرقام الـ Terminals دون تخصيص نصوص جديدة في الذاكرة.
