# الدليل الهندسي: Named Arguments مقابل Positional Arguments في الـ DTO Projections

هذا الملف يشرح بعمق الفارق الجوهري بين **Positional Arguments** و **Named Arguments**، ولماذا يعتبر الاعتماد على الترتيب المكاني ثغرة تصميمية صامتة (Silent Semantic Bug) في أنظمة الـ Backend، مع توضيح المفاهيم بما يسهل فهمها ومشاركتها مع مطوري الـ Node.js / TypeScript.

---

## 1. ما معنى "لو أحد غيّر الترتيب"؟ وهل نقصد الـ Frontend أم الـ Backend؟

> ⚠️ **توضيح حاسم:** المشكلة **ليست في الـ Frontend إطلاقاً**، ولا في ترتيب الـ Properties داخل كائن الـ JSON.

في بروتوكول JSON ومكتبات الـ Serializers (مثل `System.Text.Json` أو `express.json`)، الكائنات عبارة عن Key-Value pairs؛ لا يهم إن جاء `"name"` قبل `"email"` أو العكس، فالـ Frontend سيقرأ `response.data.name` بدون مشاكل.

**المشكلة تقع بنسبة 100% داخل الـ Backend في لغة C# (أو أي لغة Strongly Typed)** أثناء استدعاء دالة البناء (Constructor) الخاصة بالـ DTO داخل استعلام قاعدة البيانات.

---

## 2. الملفات المعنية في هذا السيناريو ووظيفة كل ملف

لدينا ملفان أساسيان يتعاملان مع هذه العملية:

| اسم الملف | المسار | وظيفته ودوره في المعمارية |
| :--- | :--- | :--- |
| **`BranchDetailResponse.cs`** | `Application/Branches/Queries/GetBranchById/` | **عقد البيانات (Data Contract):** هو الـ Record أو الكلاس الذي يحدد شكل البيانات المرجعة من الاستعلام، ويعرّف الـ Constructor والـ Properties. |
| **`GetBranchByIdQueryHandler.cs`** | `Application/Branches/Queries/GetBranchById/` | **معالج الاستعلام (Query Handler):** يستعلم من قاعدة البيانات ويقوم بعمل إسقاط (Projection) للبيانات من كائن الـ Entity إلى كائن الـ Response عبر `.Select()`. |

---

## 3. كيف تحصل المشكلة بالضبط؟ (Workflow الكارثة الصامتة)

المشكلة تسمى في هندسة البرمجيات: **Silent Semantic Bug** ناتج عن **Primitive Obsession / Type Blindness**.

### الكود القديم (Positional Arguments):
```csharp
// في ملف الـ Handler:
.Select(b => new BranchDetailsDto(
    b.Id,
    b.Code.Value,
    b.Name,
    b.Address.Street,     // string رقم 1
    b.Address.City,       // string رقم 2
    b.Address.Region,     // string رقم 3
    b.Address.PostalCode, // string رقم 4
    b.Phone,              // string رقم 5
    b.TaxNumber,          // string رقم 6
    b.Email,              // string رقم 7
    ...))
```

### السيناريو الذي يفجر المشكلة خطوة بخطوة (Step-by-Step):

```
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 1: المطور يعدل تعريف الـ Record في ملف BranchDetailsDto.cs        │
│ قام مطور بترتيب الحقول ألفبائياً أو وضع المدينة قبل الشارع:            │
│ public record BranchDetailsDto(Guid Id, string Code, string Name,     │
│     string City, string Street, ...)  <-- لاحظ تبديل City و Street     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 2: عملية البناء (Build / Compilation)                           │
│ الـ C# Compiler يفحص دالة البناء في Handler:                          │
│ • الباراميتر رقم 4 نوعه string؟ نعم (Street ممرر لـ City).             │
│ • الباراميتر رقم 5 نوعه string؟ نعم (City ممرر لـ Street).             │
│ النتيجة: Build Succeeded بنجاح تام! صفر أخطاء (0 Errors, 0 Warnings)!  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ الخطوة 3: التشغيل في الـ Production والكارثة الصامتة                   │
│ الكود يعمل بدون أي Crash، لكن البيانات مقلوبة:                        │
│ • في قاعدة البيانات: Street = "شارع الزبيري", City = "صنعاء"           │
│ • الناتج في الـ API: City = "شارع الزبيري", Street = "صنعاء"           │
│ • العميل يطبع الفاتورة أو شحنة التوصيل فيجد العنوان مقلوباً!           │
└────────────────────────────────────────────────────────────────────────┘
```

> 💡 **لماذا لم ينبهنا الـ Compiler؟**  
> لأن كلا الحقلين لهما نفس النوع (`string`). الـ Compiler يفحص تطابق **الأنواع (Types)**، ولا يفهم **المعنى (Semantics)**. طالما وضعت `string` مكان `string`، يعتبر الكود صحيحاً!

نفس الكارثة تحصل في:
- `CloseTime` و `OpenTime` (كلاهما `TimeOnly`).
- `IsClosed` و `IsOvernight` (كلاهما `bool`). لو انقلب الترتيب، المحل المفتوح يصبح مغلقاً، والمحل المغلق يصبح مفتوحاً طوال الليل!

---

## 4. مقارنة مع بيئة Node.js / TypeScript (لشرحها لزميلك)

في بيئة **JavaScript / Node.js**، هذه المشكلة كلاسيكية ومشهورة جداً.

### الطريقة السيئة في JS (Positional):
```javascript
// تعريف الدالة
function createBranch(id, code, name, street, city, phone, email) { ... }

// استدعاء الدالة
createBranch(1, "B01", "Main", "Sana'a", "Al-Zubairy", "123456", "a@a.com");
// لو عكست "Sana'a" و "Al-Zubairy" لا توجد أي أداة في JavaScript تمنع الكارثة!
```

### الطريقة المعتمدة في Node.js / TypeScript الحديثة (Object Destructuring / Named Options):
في Node.js لا يمررون 7 نصوص بالترتيب أبداً، بل يمررون كائناً (Object):
```typescript
// في TypeScript / Node.js:
interface BranchOptions {
  street: string;
  city: string;
  phone: string;
}

function createBranch({ street, city, phone }: BranchOptions) { ... }

// عند الاستدعاء:
createBranch({
  city: "Sana'a",        // الترتيب هنا لا يهم إطلاقاً!
  street: "Al-Zubairy",
  phone: "123456"
});
```

في C#، الـ **Named Arguments** هي المقابل الحرفي لهذه الممارسة لحماية الكود من أخطاء الترتيب.

---

## 5. شرح الطريقة الجديدة بالتفصيل: ما قبل النقطتين وما بعدهما

الكود الجديد مكتوب هكذا:
```csharp
.Select(b => new BranchDetailResponse(
    Id: b.Id,
    Code: b.Code.Value,
    Name: b.Name,
    Street: b.Address.Street,
    City: b.Address.City,
    Region: b.Address.Region,
    PostalCode: b.Address.PostalCode,
    Phone: b.Phone,
    TaxNumber: b.TaxNumber,
    Email: b.Email,
    Currency: b.Currency,
    IsActive: b.IsActive,
    CreatedAt: b.CreatedAt,
    OperatingHours: b.OperatingHours
        .OrderBy(h => h.DayOfWeek)
        .Select(h => new BranchOperatingHoursResponse(
            DayOfWeek: h.DayOfWeek,
            OpenTime: h.OpenTime,
            CloseTime: h.CloseTime,
            IsClosed: h.IsClosed,
            IsOvernight: !h.IsClosed && h.CloseTime < h.OpenTime))
        .ToList()))
```

### تشريح السطر الواحد: ماذا يمثل كل طرف؟

دعنا نأخذ هذا السطر كمثال:
```csharp
City: b.Address.City,
```

```
       الجانب الأيسر (قبل النقطتين)             الجانب الأيمن (بعد النقطتين)
              [ City ]                 :          [ b.Address.City ]
                 │                                        │
                 ▼                                        ▼
    اسم الباراميتر في الـ DTO             القيمة أو التعبير القادم من الكيان
 (Parameter Name in Target Record)             (Source Value / Expression)
```

1. **الجانب الأيسر (`City:`):**  
   يمثل **اسم الباراميتر الرسمي** الموجود في دالة بناء كائن الهدف `BranchDetailResponse`.  
   أنت تقول للـ Compiler: *"أنا أطلب إسناد هذه القيمة إلى المتغير المسمى تحديداً `City`، بغض النظر عن موقعه أو ترتيبه بين المعاملات"*.

2. **الجانب الأيمن (`b.Address.City`):**  
   يمثل **القيمة أو التعبير البرمجي** المستخرج من كائن الدومين `b` (أو عمود الجدول في قاعدة البيانات) المراد وضعه في هذا المتغير.

---

## 6. الفوائد الهندسية الثلاث لهذه الطريقة (Architectural Benefits)

### الفائدة 1: مناعة تامة ضد أخطاء الترتيب (Order-Agnostic Safety)
حتى لو قمت بكتابة `City:` في أول سطر و `Id:` في آخر سطر، أو قام مطور آخر بإعادة ترتيب الباراميترات داخل ملف الـ Record، فإن C# ستقوم بربط كل قيمة باسمها الصحيح 100%.

### الفائدة 2: حماية وقت الترجمة مع إعادة التسمية (Compile-Time Refactoring Safety)
لو قام مطور بتغيير اسم الخاصية في `BranchDetailResponse` من `Street` إلى `StreetAddress`:
- في الطريقة القديمة (Positional): الكود سيبني بنجاح لو كان الترتيب متطابقاً، أو ينهار في وقت غير متوقع.
- في الطريقة الجديدة (Named): سيعطيك الـ Compiler فوراً خطأ صريح:  
  `CS1739: The best overload for 'BranchDetailResponse' does not have a parameter named 'Street'`.  
  الخطأ يظهر في شاشتك فوراً قبل أن يصل الكود إلى الـ Git أو الـ Production.

### الفائدة 3: قراءة ذاتية للكود (Self-Documenting Code)
عندما يقرأ أي مهندس جديد الـ Handler، سيعرف فوراً أين تذهب كل خاصية دون الحاجة إلى فتح ملف الـ Record للعد على أصابعه: "هل المتغير السابع هو البريد أم رقم الهاتف؟".
