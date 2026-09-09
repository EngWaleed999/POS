# 📖 دليل خدمة الهوية والفروع (SuperMarket Identity & Organization Service)

## 📌 1. نظرة عامة (Overview)
خدمة **`Identity & Organization Service`** هي الركيزة الأساسية لإدارة الهيكل التنظيمي، الفروع، أجهزة نقاط البيع (POS Registers)، وطاقم الموظفين وصلاحياتهم الحساسة داخل منصة **Enterprise SuperMarket POS & Retail Platform**.

تتبع الخدمة مبدأ **فصل الاهتمامات (Separation of Concerns)** بحيث تفصل بين:
1. **المصادقة والأمان (Authentication - AuthN):** الموكلة لنظام **Keycloak (Identity Provider - IdP)** المعتمد على معايير **OAuth 2.0** و **OpenID Connect (OIDC)**، والتحقق المباشر من الـ JWT Tokens في الـ Backend عبر **`Microsoft.AspNetCore.Authentication.JwtBearer`**.
2. **بيانات الهيكل التنظيمي ونقاط البيع (Organization & Staff Domain):** المدارة عبر تطبيقنا في **ASP.NET Core** والمخزنة في قاعدة بيانات **PostgreSQL (`identity_db`)**.

---

## 🏛️ 2. الهيكل المعماري للخدمة (Architecture at a Glance)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       SuperMarket Client Applications                       │
│             (شاشات الكاشير POS / لوحة تحكم الإدارة / ماسحات الباركود)              │
└───────────────────────┬─────────────────────────────┬───────────────────────┘
                        │ 1. Login (PIN / Credentials)│ 3. API Requests
                        │    (Fast POS Auth Flow)     │    (Bearer JWT Token)
                        ▼                             ▼
       ┌──────────────────────────────┐     ┌─────────────────────────────────┐
       │     Keycloak (AuthN IdP)     │     │      Identity.API (.NET 10)     │
       │                              │     │                                 │
       │  • Realm: supermarket        │     │  • Stateless JWKS Validation    │
       │  • Client: pos-api           │     │  • POS Terminal Device Binding  │
       │  • JWT Signing (RS256 / JWKS)│     │  • Staff & Branch Management    │
       │  • RBAC (Cashier, Supervisor)│     │  • Sensitive Permission Checks  │
       └──────────────────────────────┘     └───────────────┬─────────────────┘
                                                            │
                            ┌───────────────────────────────┴─────────────────┐
                            ▼                                                 ▼
               ┌─────────────────────────┐                       ┌─────────────────────────┐
               │  PostgreSQL (identity_db│                       │   RabbitMQ (Message Bus)│
               │                         │                       │                         │
               │  • branches             │                       │  •                  StaffCreatedEvent    │
               │  • pos_registers        │                       │  • BranchCreatedEvent   │
               │  • staff_members        │                       │  • POSRegisterBoundEvent│
               │  • permissions / roles  │                       │  • Outbox Pattern       │
               └─────────────────────────┘                       └─────────────────────────┘
```

---

## 📂 3. فهرس وثائق المعمارية والتصميم (Architecture Documentation Index)

تم إعداد هذه الوثائق الـ 12 لتكون المرجع الشامل لجميع القرارات، المتطلبات، المعايير الأمنية، والتصميم الهندسي لخدمة الهوية والفروع:

| # | اسم الملف | الوصف المعماري |
|---|---|---|
| 01 | [README.md](./README.md) | هذا الملف (نظرة عامة ودليل البدء وفهرس الوثائق). |
| 02 | [requirements.md](./requirements.md) | المتطلبات الوظيفية وغير الوظيفية لإدارة الفروع، الكاشيرية، ونقاط البيع. |
| 03 | [architecture.md](./architecture.md) | التصميم المعماري بالتفصيل، طبقات Clean Architecture، وتكامل OIDC. |
| 04 | [decisions.md](./decisions.md) | سجل القرارات المعمارية الموثقة (ADRs) المتعلقة بالمصادقة والصلاحيات. |
| 05 | [tradeoffs.md](./tradeoffs.md) | مقارنة البدائل والمفاضلات الهندسية (Native JwtBearer vs Custom IAM). |
| 06 | [security.md](./security.md) | معايير الأمان، تشفير الـ PIN، حماية أجهزة الـ POS، وموافقة المشرف (Override). |
| 07 | [failure-scenarios.md](./failure-scenarios.md) | حالات الفشل، انقطاع الاتصال بـ Keycloak، والتعامل مع الأجهزة غير المصرحة. |
| 08 | [observability.md](./observability.md) | المراقبة، السجلات المنظمة (Structured Logging)، ومقاييس فحص الصحة (Health Checks). |
| 09 | [performance.md](./performance.md) | استراتيجيات الـ In-Memory Caching لصلاحيات الكاشير وزمن التحقق السريع (<10ms). |
| 10 | [problems.md](./problems.md) | المشاكل الهندسية المحلولة وتحديات التبديل السريع بين موظفي الكاشير. |
| 11 | [testing.md](./testing.md) | استراتيجية الاختبارات الآلية للتحقق من الأدوار والسياسات. |
| 12 | [qa-knowledge-base.md](./qa-knowledge-base.md) | قاعدة المعرفة لفحص الجودة، حالات الحافة (Edge Cases)، وسيناريوهات الفحص. |
| 13 | [SYSTEM_SPECIFICATION_AND_ROLES.md](../../../../docs/SYSTEM_SPECIFICATION_AND_ROLES.md) | **وثيقة المواصفات الهندسية ومصفوفة الصلاحيات الشاملة ونموذج البيانات (مستخرجة من ERD و roles).** |

---

## 🏗️ 4. هيكل مجلدات المشروع (Solution Structure)

```
src/Services/Identity/
├── SuperMarket.Identity.API/              # Controllers, Middlewares, Program.cs
├── SuperMarket.Identity.Application/      # CQRS Features (Branches, Registers, Staff), DTOs, Handlers
├── SuperMarket.Identity.Domain/           # Entities (Branch, POSRegister, StaffMember), Enums, Rules
├── SuperMarket.Identity.Infrastructure/   # EF Core, PostgreSQL DbContext, Keycloak Client, RabbitMQ
└── docs/                                  # 12 Architecture & Engineering Documents
```

---

## 🚀 5. تشغيل البنية التحتية المحلية (Quick Start)

خدمة الهوية والفروع تعتمد على الحاويات المعرفة في `deploy/docker/docker-compose.yml`:
* **Keycloak:** على المنفذ `http://localhost:8080` (Realm: `supermarket`).
* **PostgreSQL:** على المنفذ `localhost:5432` (Database: `keycloak_db` و `identity_db`).
* **RabbitMQ:** على المنفذ `5672` ولوحة الإدارة على `http://localhost:15672`.

```bash
# تشغيل حاويات البنية التحتية من مسار deploy/docker
docker compose up -d
```
