# AcxiomCRM

**AcxiomCRM** is an enterprise-grade Customer Relationship Management (CRM) application built on **ASP.NET Core 8 MVC**, **Entity Framework Core**, and **ASP.NET Core Identity**. The system manages the full sales lifecycle from prospective lead capture through qualification, customer account conversion, opportunity pipeline tracking, scheduled follow-up engagements, activity auditing, and REST API access.

---

## 🏛 Architecture Overview

AcxiomCRM follows a layered software architecture:

```
AcxiomCRM/
├── Controllers/         # MVC Controllers & Secured API Controllers
│   ├── Api/             # REST Endpoints (/api/customers, /api/leads, etc.)
│   └── ...
├── Data/                # ApplicationDbContext & Automated DbInitializer
├── Models/              # Core CRM Domain Entities & Application Identity
├── ViewModels/          # Strongly-typed UI View Models with Data Annotations
├── DTOs/                # Data Transfer Objects decoupled from EF entities
├── Services/            # Business Logic, Scoping, Auditing, and CSV Exporters
└── Views/               # Razor Views styled with Bootstrap 5 & Chart.js
```

---

## 🚀 Key Modules & Capabilities

1. **Authentication & Identity Security**
   - Built on ASP.NET Core Identity (`ApplicationUser` / `ApplicationRole`).
   - Strong PBKDF2 adaptive password hashing; plain text passwords are never stored or logged.
   - Strict password complexity enforcement (min 8 characters, uppercase, lowercase, number, special character).
   - Automated account lockout after 5 consecutive failed attempts (15-minute lockout).
   - Anti-Forgery token protection (`[ValidateAntiForgeryToken]`) across all state-altering forms.

2. **Role-Based Authorization & Data Scoping**
   - **Administrator**: Full system visibility, user & role administration, lockout unlock, audit logs, and global CRM records.
   - **Manager**: Team-wide sales visibility, pipeline performance monitoring, conversion metrics, and management reports.
   - **Sales Executive**: Scoped access restricted to their own assigned leads, customers, opportunities, and follow-ups.

3. **Customer Management**
   - Comprehensive customer profile tracking (Code, Name, Email, Phone, Company, Address, Status, Representative).
   - Client-side and server-side validation with strict email and phone format checking.
   - Uniqueness enforcement for customer email and phone to prevent duplicate accounts.
   - Complete relationship history showing linked opportunities, follow-ups, and recorded activities.

4. **Lead Management & Conversion Workflow**
   - Track prospective leads across sources (Website, Referral, Trade Show, Cold Call, etc.) and statuses (`New`, `Contacted`, `Qualified`, `Unqualified`, `Converted`, `Lost`).
   - Dedicated **Lead Conversion Workflow**: promotes qualified leads to active Customers and/or pipeline Opportunities in a single audited step.

5. **Opportunity Management & Interactive Pipeline Board**
   - Opportunity tracking with deal stages: `Qualification`, `Proposal`, `Negotiation`, `Won`, `Lost`.
   - Automatic derivation of **Weighted Pipeline Forecast**: `(Amount × Probability) / 100`.
   - Business validations: Amount must be `> 0`, Probability must be `0–100%`, and Expected Close Date cannot be in the past for active deals.
   - Dual views: Filterable table view and visual **Kanban Pipeline Board**.

6. **Follow-Up Management**
   - Schedule calls, meetings, emails, and tasks linked to Customers and Leads.
   - Status tracking (`Planned`, `Completed`, `Missed`, `Cancelled`) with automated Overdue and Today status indicators.
   - Business validation preventing scheduled dates earlier than today for planned activities.
   - Built-in completion workflow to capture outcome notes and chain the next follow-up.

7. **Activity History & Audit Trails**
   - Logged timeline of phone calls, meetings, emails, and tasks.
   - Append-only compliance **Audit Log** capturing user, timestamp, IP address, module, action, record ID, and change details.
   - Filterable by user, module, action, and date range with RFC-4180 CSV export.

8. **Executive Reports & Data Export**
   - Stage-wise and owner-wise Sales Pipeline reports.
   - Lead qualification and opportunity win rate conversion metrics.
   - Representative workload and activity reports.
   - One-click CSV export hub for customer, lead, opportunity, and audit datasets.

9. **REST API**
   - Secured RESTful endpoints exposing JSON DTOs:
     - `POST /api/auth/login` - Authenticate and obtain session cookie
     - `POST /api/auth/logout` - Invalidate session
     - `GET /api/customers` - List / search customers
     - `GET /api/customers/{id}` - Retrieve customer by ID
     - `POST /api/customers` - Create customer
     - `PUT /api/customers/{id}` - Update customer
     - `DELETE /api/customers/{id}` - Remove customer
     - `GET /api/leads` - List leads
     - `POST /api/leads` - Create lead
     - `GET /api/opportunities` - List opportunities
     - `POST /api/opportunities` - Create opportunity
     - `GET /api/followups` - List follow-ups
     - `POST /api/followups` - Create follow-up
     - `GET /api/reports/pipeline` - Stage-wise pipeline report data

---

## 🔐 Pre-Seeded Test Credentials

On first run, the database automatically initializes with sample data and test accounts:

| Role | Email | Password | Access Scope |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiomcrm.com` | `Admin@12345` | Full system administration, users, audit, reports |
| **Manager** | `manager@acxiomcrm.com` | `Manager@12345` | Team pipeline, management analytics, CRM records |
| **Sales Executive** | `sales@acxiomcrm.com` | `Sales@12345` | Assigned records, individual dashboard |
| **Sales Executive** | `sales2@acxiomcrm.com` | `Sales@12345` | Assigned records, individual dashboard |

---

## 🛠 Running the Application (Step-by-Step)

### Option 1: One-Click Execution (Recommended)
You can directly run or test the application using the pre-configured launcher scripts:
- **To Run the Web App:** Double-click [`run.bat`](file:///c:/Users/satap/OneDrive/Documents/AXIOM%20CODE%20FILE/run.bat) (or run `./run.bat` in your terminal).
- **To Run the Test Suite:** Double-click [`test.bat`](file:///c:/Users/satap/OneDrive/Documents/AXIOM%20CODE%20FILE/test.bat) (or run `./test.bat` in your terminal).

### Option 2: Using Terminal / PowerShell Commands
In PowerShell:
```powershell
# 1. Run the Acceptance Tests
& "$HOME\AppData\Local\Microsoft\dotnet\dotnet.exe" test AcxiomCRM.sln

# 2. Run the AcxiomCRM Web App
& "$HOME\AppData\Local\Microsoft\dotnet\dotnet.exe" run --project AcxiomCRM/AcxiomCRM.csproj
```

Once started, open your web browser at:
👉 **`http://localhost:5000`** or **`https://localhost:5001`**


---

## 🧪 Test Suite & Acceptance Verification

The project includes an **xUnit** test suite (`tests/AcxiomCRM.Tests`) covering all business rules and the 14 criteria of the Acceptance Scenario:
- Model validation for mandatory fields, valid email patterns, and 10-digit phone requirements.
- Business rule rejection when Opportunity Amount is `<= 0`.
- Business rule rejection when Probability is `< 0` or `> 100`.
- Close date validation preventing past dates on active opportunities.
- Follow-up date validation preventing scheduling in the past.
- Scoping rules guaranteeing Sales Executives only access assigned records.
- Manager and Admin privilege hierarchy.
- Audit log entry completeness.

Run the test suite at any time via:
```bash
dotnet test AcxiomCRM.sln
```
