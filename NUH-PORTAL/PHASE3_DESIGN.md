# PHASE 3 — Student Self-Service Registration Portal
## Design Document

| Author | Date | Version | Status |
|--------|------|---------|--------|
| NUH Portal Team | 2026-07-15 | 1.0 | Draft |

---

## Table of Contents

1. [Overview](#1-overview)
2. [System Architecture](#2-system-architecture)
3. [User Flow & Page Navigation](#3-user-flow--page-navigation)
4. [Database Schema (ERD)](#4-database-schema-erd)
5. [API Design](#5-api-design)
6. [UI Mockups](#6-ui-mockups)
7. [Security Controls](#7-security-controls)
8. [Scalability Design (10,000+ Students)](#8-scalability-design-10000-students)
9. [Database Indexing Strategy](#9-database-indexing-strategy)
10. [Audit Requirements](#10-audit-requirements)
11. [SMS Notification Framework](#11-sms-notification-framework)
12. [Appendices](#12-appendices)

---

## 1. Overview

### 1.1 Purpose

Phase 3 introduces a **Student Self-Service Registration Portal** that allows students to register for housingaccommodation through a guided workflow. The system replaces the current manual/admin-only registration process with a student-initiated flow that includes identity verification, policy acknowledgement, and multi-stage approval.

### 1.2 Scope

- Mobile number verification via OTP
- Student declarations and AUP acknowledgement
- Self-service registration request submission
- Multi-stage approval workflow (Student → Supervisor → Cyber → Admin)
- Request tracking with status visibility
- SMS notifications at each workflow transition
- Integration with existing Phase 1 (Users, Auth, Requests) and Phase 2 (AD provisioning, Notifications)

### 1.3 Actors & Roles

| Actor | Role | Description |
|-------|------|-------------|
| Student | `student` (new) | Self-registers, tracks request status |
| Supervisor | `supervisor` | Reviews and approves/rejects registration requests |
| Cyber Security | `cyber` | Reviews and approves/rejects |
| Admin | `admin` | Final approval and provisioning trigger |
| System | — | Sends SMS, logs audit, manages timeouts |

### 1.4 Workflow Summary

```
Student → Supervisor → Cyber Security → Admin → Provisioned
   │          │              │              │
   ▼          ▼              ▼              ▼
  OTP      Review &      Security       Final Approval
Verify     Approve       Review         + AD Provision
```

---

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────┐
│                    Student Browser                    │
│           (Mobile / Desktop / RTL Arabic)            │
└────────────────────────┬────────────────────────────┘
                         │ HTTPS
                         ▼
┌──────────────────────────────────────────────────────┐
│              IIS 10 — ASP.NET Core 8.0                │
│  ┌──────────┐ ┌──────────┐ ┌──────────────────────┐  │
│  │Phase 1-2 │ │Phase 3   │ │ Middleware Pipeline   │  │
│  │Controllers│ │Controllers│ │ Auth · CORS · Rate   │  │
│  └──────────┘ └──────────┘ │ Limit · Audit · ETag │  │
│                            └──────────────────────┘  │
└────────────────────────┬─────────────────────────────┘
                         │
           ┌─────────────┼─────────────┐
           ▼             ▼             ▼
    ┌──────────┐  ┌──────────┐  ┌──────────┐
    │  SQL     │  │  SMS     │  │  AD      │
    │ Server   │  │ Gateway  │  │ (LDAP)   │
    │ 2019     │  │ Provider │  │          │
    └──────────┘  └──────────┘  └──────────┘
```

### 2.2 Technology Stack (Additions to Existing)

| Component | Technology | Justification |
|-----------|-----------|---------------|
| OTP Generation | `RandomNumberGenerator` (crypto-safe) | 6-digit numeric, time-limited |
| SMS Gateway | Pluggable interface (`ISmsProvider`) | Default: placeholder/logging; production: SMS provider adapter |
| Rate Limiting | In-memory `RateLimiter` + DB persistance | Prevent OTP brute-force and registration spam |
| Background Jobs | `IHostedService` / `System.Threading.Channels` | Async SMS dispatch, cleanup of expired OTPs |
| Caching | `IMemoryCache` (OTP attempts, rate-limit counters) | Reduce DB roundtrips for transient counters |
| PDF Rendering | `DinkToPdf` or browser-based export | AUP acknowledgement receipt |

### 2.3 New Services

| Service | Responsibility |
|---------|---------------|
| `OtpService` | Generate, validate, expire OTP codes; rate-limit per phone/ip |
| `SmsService` | Queue and send SMS through configured provider; log to `SmsLogs` |
| `RegistrationWorkflowService` | Orchestrate the 4-stage approval; enforce state machine |
| `DeclarationService` | Manage student declarations versioning and acknowledgement |
| `AupService` | Serve AUP PDF, record acknowledgement with timestamp and IP |

---

## 3. User Flow & Page Navigation

### 3.1 Complete Page Flow

```
[Login Page]
    │
    ▼
[Dashboard] ──────────► [Register for Housing]
    │                          │
    │                          ▼
    │                 [Mobile Verification]
    │                 • Enter phone number
    │                 • Receive OTP via SMS
    │                 • Verify OTP (6-digit)
    │                          │
    │                          ▼
    │                 [Student Declarations]
    │                 • Review declaration statements
    │                 • Check agreement checkboxes
    │                 • Submit declarations
    │                          │
    │                          ▼
    │                 [AUP Acknowledgement]
    │                 • View/Download AUP PDF
    │                 • Check "I have read and agree"
    │                 • Submit acknowledgement
    │                          │
    │                          ▼
    │                 [Registration Form]
    │                 • Personal info (pre-filled from ID)
    │                 • College / Department / Level
    │                 • Housing preferences
    │                 • Submit request
    │                          │
    │                          ▼
    │                 [Request Submitted]
    │                 • Confirmation screen
    │                 • Reference number displayed
    │                 • SMS confirmation sent
    │
    ▼
[My Requests] ◄─────────────────┘
• List of all requests with status
• Click to view timeline/status
• Status: Pending_Supervisor → Pending_Cyber → Pending_Admin → Approved/Rejected
```

### 3.2 State Machine

```
                    ┌──────────────┐
                    │  DRAFT       │
                    │ (not started)│
                    └──────┬───────┘
                           │ Submit
                           ▼
                    ┌──────────────┐
           ┌───────►│ PENDING_     │◄────────┐
           │        │ SUPERVISOR   │         │
           │        └──────┬───────┘         │
           │             │ │                 │
           │      Approve │ │ Reject         │
           │             ▼ ▼                 │
           │        ┌──────────────┐         │
           │        │ PENDING_     │         │
           │        │ CYBER        │         │
           │        └──────┬───────┘         │
           │             │ │                 │
           │      Approve │ │ Reject         │
           │             ▼ ▼                 │
           │        ┌──────────────┐         │
           │        │ PENDING_     │         │
           │        │ ADMIN        │         │
           │        └──────┬───────┘         │
           │             │ │                 │
           │      Approve │ │ Reject         │
           │             ▼ ▼    ┌────────┐  │
           │        ┌────────┐  │REJECTED│──┘
           │        │APPROVED│  └────────┘
           │        └──┬─────┘
           │           │ Provision
           │           ▼
           │        ┌────────────┐
           │        │PROVISIONED │
           │        └────────────┘
           │
           └── Re-submit from Rejected
```

### 3.3 User Stories

| ID | Actor | Story |
|----|-------|-------|
| US-01 | Student | I want to register my mobile number and verify it via OTP so that the system can contact me. |
| US-02 | Student | I want to read and agree to declarations so that I acknowledge the housing rules. |
| US-03 | Student | I want to view and acknowledge the AUP so that I understand acceptable use. |
| US-04 | Student | I want to submit a housing registration request with my details and preferences. |
| US-05 | Student | I want to track my request status so that I know where it is in the workflow. |
| US-06 | Supervisor | I want to review pending registration requests and approve or reject them. |
| US-07 | Cyber | I want to review registration requests for security compliance. |
| US-08 | Admin | I want to perform final approval and trigger AD provisioning. |
| US-09 | System | I want to send SMS notifications when the workflow advances or is rejected. |

---

## 4. Database Schema (ERD)

### 4.1 New Tables

#### 4.1.1 `OTPVerifications`

```sql
CREATE TABLE OTPVerifications (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    phone_number    NVARCHAR(20)    NOT NULL,
    otp_code        NVARCHAR(6)     NOT NULL,       -- cryptographic hash of OTP
    otp_hash        NVARCHAR(128)   NOT NULL,       -- SHA-256 hash of (code + salt)
    salt            NVARCHAR(32)    NOT NULL,       -- per-request random salt
    expires_at      DATETIME2       NOT NULL,       -- TTL: 5 minutes from creation
    verified_at     DATETIME2       NULL,           -- null until verified
    attempts        INT             NOT NULL DEFAULT 0,  -- max 5 attempts
    ip_address      NVARCHAR(45)    NULL,
    session_id      NVARCHAR(128)   NULL,
    created_at      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by      INT             NULL REFERENCES Users(Id)
);
```

**Design notes**:
- OTP is stored as a **hash** (`otp_hash = SHA256(otp_code + salt)`), never in plaintext.
- `otp_code` stores the last 4 digits of the phone number for display masking only (e.g., "...4789").
- `expires_at` = `SYSUTCDATETIME() + 5 minutes`.
- `attempts` incremented per failed verification; max 5 then invalidated.
- Cleanup job removes records older than 24 hours.

#### 4.1.2 `StudentDeclarations`

```sql
CREATE TABLE StudentDeclarations (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    declaration_text_ar  NVARCHAR(2000) NOT NULL,   -- Arabic text
    declaration_text_en  NVARCHAR(2000) NOT NULL,   -- English text
    is_active       BIT             NOT NULL DEFAULT 1,
    sort_order      INT             NOT NULL DEFAULT 0,
    created_by      INT             NOT NULL REFERENCES Users(Id),
    created_at      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_by      INT             NULL REFERENCES Users(Id),
    updated_at      DATETIME2       NULL
);
```

**Design notes**:
- Declarations are **versioned**: when edited, old rows are soft-retired (`is_active = 0`) and new rows inserted.
- A student's acknowledgement references the exact declaration row at time of agreement.

#### 4.1.3 `StudentDeclarationAcknowledgements`

```sql
CREATE TABLE StudentDeclarationAcknowledgements (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    student_id      INT             NOT NULL REFERENCES Students(Id),
    declaration_id  INT             NOT NULL REFERENCES StudentDeclarations(Id),
    agreed          BIT             NOT NULL DEFAULT 1,
    agreed_at       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ip_address      NVARCHAR(45)    NULL,
    user_agent      NVARCHAR(500)   NULL
);
```

#### 4.1.4 `AupAcknowledgements`

```sql
CREATE TABLE AupAcknowledgements (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    student_id      INT             NOT NULL REFERENCES Students(Id),
    aup_version     NVARCHAR(20)    NOT NULL,       -- e.g., "2026-07-01-v1"
    agreed          BIT             NOT NULL DEFAULT 1,
    agreed_at       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ip_address      NVARCHAR(45)    NULL,
    user_agent      NVARCHAR(500)   NULL,
    pdf_checksum    NVARCHAR(64)    NULL            -- SHA-256 of PDF at time of agreement
);
```

#### 4.1.5 `WorkflowHistory`

```sql
CREATE TABLE WorkflowHistory (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    request_id      INT             NOT NULL REFERENCES Requests(Id),
    from_status     NVARCHAR(50)    NULL,           -- previous status
    to_status       NVARCHAR(50)    NOT NULL,       -- new status
    action          NVARCHAR(50)    NOT NULL,       -- 'submit','approve','reject','return'
    performed_by    INT             NOT NULL REFERENCES Users(Id),
    performed_at    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    comments        NVARCHAR(2000)  NULL,
    ip_address      NVARCHAR(45)    NULL,
    user_agent      NVARCHAR(500)   NULL
);
```

**Design notes**:
- Immutable audit trail. No UPDATE or DELETE — only INSERT.
- Supplies data for the request tracking timeline UI and audit reports.

#### 4.1.6 `SmsLogs`

```sql
CREATE TABLE SmsLogs (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    phone_number    NVARCHAR(20)    NOT NULL,
    message         NVARCHAR(1000)  NOT NULL,
    message_type    NVARCHAR(50)    NOT NULL,       -- 'otp','status_update','rejection','approval'
    reference_id    INT             NULL,           -- FK to source (e.g., OTPVerifications.Id, Requests.Id)
    reference_type  NVARCHAR(50)    NULL,           -- 'otp','request'
    provider        NVARCHAR(50)    NOT NULL DEFAULT 'log_only',
    provider_message_id NVARCHAR(200) NULL,          -- SMS provider's message ID
    status          NVARCHAR(20)    NOT NULL DEFAULT 'pending',  -- 'pending','sent','failed','delivered'
    sent_at         DATETIME2       NULL,
    delivered_at    DATETIME2       NULL,
    error_message   NVARCHAR(1000)  NULL,
    cost            DECIMAL(10, 4)  NULL,           -- cost per message in SAR
    created_at      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
```

### 4.2 Extended Tables (Changes to Existing)

#### 4.2.1 `Students` — New Columns

```sql
ALTER TABLE Students ADD
    phone_verified      BIT             NOT NULL DEFAULT 0,
    phone_verified_at   DATETIME2       NULL,
    otp_verification_id INT             NULL REFERENCES OTPVerifications(Id),
    registration_status NVARCHAR(50)    NULL,       -- tracks registration step
    aup_acknowledged    BIT             NOT NULL DEFAULT 0,
    aup_acknowledged_at DATETIME2       NULL,
    declarations_acknowledged BIT       NOT NULL DEFAULT 0,
    declarations_acknowledged_at DATETIME2 NULL;
```

#### 4.2.2 `Requests` — New Status Values

Add to the existing status enum/values:
- `pending_supervisor`
- `pending_cyber`
- `pending_admin`
- `approved` (exists)
- `rejected` (exists)
- `provisioned` (exists from Phase 2)

### 4.3 Entity Relationship Diagram (Textual)

```
┌───────────────────┐       ┌───────────────────────┐
│     Students      │       │    OTPVerifications    │
├───────────────────┤       ├───────────────────────┤
│ PK Id             │◄──────│ FK student_id?         │
│ phone             │       │ PK Id                  │
│ phone_verified    │       │ phone_number          │
│ phone_verified_at │       │ otp_hash + salt       │
│ otp_verification_id│───►│ expires_at             │
│ registration_status│      │ verified_at            │
│ aup_acknowledged  │       │ attempts               │
│ declarations_ack  │       └───────────────────────┘
│ ...               │
└────────┬──────────┘
         │
         │ 1 ──── * ┌───────────────────────────┐
         ├─────────►│ StudentDeclarationsAck      │
         │          ├───────────────────────────┤
         │          │ PK Id                     │
         │          │ FK student_id             │
         │          │ FK declaration_id         │
         │          │ agreed_at, ip, user_agent │
         │          └────────────┬──────────────┘
         │                       │
         │                       │ * ──── 1
         │                       ▼
         │          ┌───────────────────────────┐
         │          │   StudentDeclarations      │
         │          ├───────────────────────────┤
         │          │ PK Id                     │
         │          │ declaration_text_ar/en    │
         │          │ is_active, sort_order     │
         │          │ created_by, updated_by    │
         │          └───────────────────────────┘
         │
         │ 1 ──── * ┌───────────────────────────┐
         ├─────────►│   AupAcknowledgements      │
         │          ├───────────────────────────┤
         │          │ PK Id                     │
         │          │ FK student_id             │
         │          │ aup_version               │
         │          │ agreed_at, ip, user_agent │
         │          │ pdf_checksum              │
         │          └───────────────────────────┘
         │
         │ 1 ──── * ┌───────────────────────────┐      ┌───────────────────────────┐
         └─────────►│        Requests            │      │     WorkflowHistory       │
                    ├───────────────────────────┤      ├───────────────────────────┤
                    │ PK Id                     │◄─────│ FK request_id             │
                    │ student_id (existing)     │      │ PK Id                     │
                    │ ...                       │      │ from_status, to_status    │
                    │ status (extended)         │      │ action, performed_by      │
                    │ registration_data (JSON)  │      │ comments, performed_at     │
                    └───────────────────────────┘      │ ip_address, user_agent    │
                                                       └───────────────────────────┘

┌───────────────────────────┐
│         SmsLogs           │
├───────────────────────────┤
│ PK Id                     │
│ phone_number              │
│ message                   │
│ message_type              │
│ reference_id/type         │
│ provider_message_id       │
│ status, sent_at           │
│ cost (SAR)                │
└───────────────────────────┘
```

---

## 5. API Design

### 5.1 Base URL

```
https://housing.nu.edu.sa/api/
```

### 5.2 Authentication

All endpoints (except health) require `Authorization: Bearer <jwt>`.
OTP endpoints use a temporary session token issued at step start.

### 5.3 Endpoints

#### 5.3.1 OTP Verification

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `POST` | `/api/otp/send` | Send OTP to mobile number | Student JWT |
| `POST` | `/api/otp/verify` | Verify OTP code | Student JWT |
| `GET`  | `/api/otp/status` | Check verification status for current user | Student JWT |

**`POST /api/otp/send`**

```json
Request:  { "phone_number": "966512345678" }
Response: { "success": true, "masked_phone": "+966 ***** 5678", "expires_in_seconds": 300,
            "message": "OTP sent successfully" }
Errors:   400 — invalid phone; 429 — too many requests
```

**`POST /api/otp/verify`**

```json
Request:  { "phone_number": "966512345678", "otp_code": "483921" }
Response: { "success": true, "message": "Phone verified", "token": "session-token..." }
Errors:   400 — invalid/expired OTP; 401 — max attempts exceeded
```

#### 5.3.2 Student Declarations

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET`  | `/api/declarations` | List active declarations | Student JWT |
| `POST` | `/api/declarations/acknowledge` | Submit acknowledgement | Student JWT |

**`POST /api/declarations/acknowledge`**

```json
Request:  { "declaration_ids": [1, 2, 3, 4, 5] }
Response: { "success": true, "acknowledged_count": 5 }
```

#### 5.3.3 AUP

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET`  | `/api/aup/current` | Get current AUP metadata + download URL | Student |
| `GET`  | `/api/aup/pdf` | Download AUP PDF | Student |
| `POST` | `/api/aup/acknowledge` | Acknowledge AUP | Student |

**`POST /api/aup/acknowledge`**

```json
Request:  { "aup_version": "2026-07-01-v1", "pdf_checksum": "sha256..." }
Response: { "success": true, "acknowledged_at": "2026-07-15T10:30:00Z" }
```

#### 5.3.4 Registration Request

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `POST` | `/api/registration` | Submit new registration request | Student (must have verified phone + acknowledged) |
| `GET`  | `/api/registration/{id}` | Get request details | Student (own) / Supervisor / Cyber / Admin |
| `PUT`  | `/api/registration/{id}` | Update draft request | Student |
| `DELETE` | `/api/registration/{id}` | Cancel draft | Student |

**`POST /api/registration`**

```json
Request: {
    "college": "Engineering",
    "department": "Computer Engineering",
    "academic_level": "Bachelor",
    "housing_building": "Building A",
    "room_number": null,
    "apartment_number": null,
    "has_disabilities": false,
    "special_needs_notes": null,
    "emergency_contact_name": "Ahmed Ali",
    "emergency_contact_phone": "966501234567"
}
Response: {
    "success": true,
    "request_id": 1042,
    "reference_number": "REG-2026-07-1042",
    "status": "pending_supervisor"
}
```

#### 5.3.5 Workflow / Request Tracking

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET`  | `/api/workflow/requests` | List pending requests (for approvers) | Supervisor/Cyber/Admin |
| `GET`  | `/api/workflow/requests/{id}` | Get request + timeline + history | Student / Approver |
| `POST` | `/api/workflow/requests/{id}/approve` | Approve at current stage | Role-gated |
| `POST` | `/api/workflow/requests/{id}/reject` | Reject at current stage | Role-gated |
| `GET`  | `/api/workflow/requests/{id}/history` | Get full workflow timeline | Student / Approver |

**`POST /api/workflow/requests/{id}/approve`**

```json
Request:  { "comments": "Verified documents, approved." }
Response: {
    "success": true,
    "new_status": "pending_cyber",
    "next_approver_role": "cyber",
    "message": "Request approved and forwarded to Cyber Security"
}
```

**`POST /api/workflow/requests/{id}/reject`**

```json
Request:  { "comments": "Missing documents — please resubmit" }
Response: { "success": true, "new_status": "rejected", "message": "Request rejected" }
```

#### 5.3.6 SMS

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `POST` | `/api/sms/send` | Send ad-hoc SMS (admin only) | Admin |

#### 5.3.7 Admin/Management

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET`  | `/api/admin/declarations` | List all declarations | Admin |
| `POST` | `/api/admin/declarations` | Create/edit declaration | Admin |
| `PUT`  | `/api/admin/declarations/{id}` | Update declaration | Admin |
| `POST` | `/api/admin/aup/upload` | Upload new AUP PDF | Admin |
| `GET`  | `/api/admin/sms-logs` | View SMS logs with filters | Admin |
| `GET`  | `/api/admin/workflow-stats` | Dashboard stats for Phase 3 | Admin |

### 5.4 Common Response Envelope

All endpoints return:

```json
{
    "success": true|false,
    "data": { ... },          // when success
    "error": {                 // when !success
        "code": "OTP_EXPIRED",
        "message": "The OTP code has expired. Please request a new one.",
        "details": { ... }
    },
    "meta": {
        "page": 1,
        "page_size": 50,
        "total": 234
    }
}
```

---

## 6. UI Mockups

### 6.1 Page Inventory

| # | Page | Route | Description |
|---|------|-------|-------------|
| 1 | `/register/phone.html` | Mobile Verification | Enter phone, receive & verify OTP |
| 2 | `/register/declarations.html` | Student Declarations | Review & agree to declarations |
| 3 | `/register/aup.html` | AUP Acknowledgement | View/download AUP PDF, acknowledge |
| 4 | `/register/form.html` | Registration Form | Fill in housing registration details |
| 5 | `/register/confirmation.html` | Confirmation | Success screen with reference number |
| 6 | `/register/my-requests.html` | My Requests | List of student's requests with status |
| 7 | `/register/request-status.html` | Request Status | Timeline view of a single request |
| 8 | `/admin/workflow-queue.html` | Workflow Queue | Approver's list of pending requests |
| 9 | `/admin/request-review.html` | Request Review | Approver's detail + approve/reject |
| 10 | `/admin/declarations-mgmt.html` | Declarations Mgmt | CRUD for declarations (Phase 2 sidebar) |
| 11 | `/admin/aup-mgmt.html` | AUP Management | Upload/version AUP PDF |
| 12 | `/admin/sms-logs.html` | SMS Logs | View sent SMS messages |

### 6.2 Wireframe Descriptions

#### 6.2.1 Mobile Verification (`/register/phone.html`)

```
┌──────────────────────────────────────────┐
│  ← Dashboard                               │
│                                            │
│  ● ● ○ ○ ○ ○    (Step 1 of 5)             │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │       📱  التحقق من رقم الجوال         │  │
│  │       Mobile Verification              │  │
│  │                                        │  │
│  │  رقم الجوال / Phone Number             │  │
│  │  ┌──────────────────────────────────┐  │  │
│  │  │ +966  [  5  ] [  1  ] [  2  ] [   │  │
│  │  └──────────────────────────────────┘  │  │
│  │                                        │  │
│  │  [  أرسل رمز التحقق  ]                  │  │
│  │    Send Verification Code               │  │
│  │                                        │  │
│  │  ──── أو / OR ────                      │  │
│  │                                        │  │
│  │  رمز التحقق / Verification Code        │  │
│  │  ┌──────────────────────────────────┐  │  │
│  │  │ [  ] [  ] [  ] [  ] [  ] [  ]    │  │  │
│  │  └──────────────────────────────────┘  │  │
│  │                                        │  │
│  │  [  تأكيد / Verify  ]                  │  │
│  │                                        │  │
│  │  ⏱  سيتم إعادة الإرسال بعد 0:45       │  │
│  │    Resend in 0:45                      │  │
│  └──────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

#### 6.2.2 Registration Form (`/register/form.html`)

```
┌──────────────────────────────────────────┐
│  ● ● ● ● ○ ○    (Step 4 of 5)           │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │  نموذج التسجيل / Registration Form    │  │
│  │                                        │  │
│  │  ─── المعلومات الشخصية / Personal ───  │  │
│  │  الاسم: عبدالله محمد الأحمري           │  │
│  │  الرقم الجامعي: 442012345             │  │
│  │  الهوية: 1098765432                   │  │
│  │  الجوال: +966 512345678 ✓ (موثق)      │  │
│  │                                        │  │
│  │  ─── المعلومات الأكاديمية / Academic ─  │  │
│  │  الكلية / College    [ Engineering ▼ ]  │  │
│  │  القسم / Department [ Computer ▼    ]   │  │
│  │  المستوى / Level    [ Bachelor ▼    ]   │  │
│  │                                        │  │
│  │  ─── تفضيلات السكن / Housing ────────  │  │
│  │  المبنى المفضل     [ Building A ▼   ]  │  │
│  │  ملاحظات خاصة      [______________]     │  │
│  │                                        │  │
│  │  ─── emergency contact ───────────────  │  │
│  │  اسم جهة الاتصال  [_________________]   │  │
│  │  رقم الجوال        [_________________]   │  │
│  │                                        │  │
│  │  [  إرسال الطلب / Submit Request  ]    │  │
│  └──────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

#### 6.2.3 Request Timeline (`/register/request-status.html?id=1042`)

```
┌──────────────────────────────────────────┐
│  طلبي / My Request  #REG-2026-07-1042     │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │  الحالة: قيد مراجعة المشرف           │  │
│  │  Status: Pending Supervisor           │  │
│  │                                        │  │
│  │  ●●●●●○○○○○  60% Complete             │  │
│  └──────────────────────────────────────┘  │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │  سير العمل / Workflow                 │  │
│  │                                        │  │
│  │  ✅  تم  | 10:30 15/7 | تقديم الطلب   │  │
│  │     Submitted     |  Date             │  │
│  │        │                               │  │
│  │  ⏳  جاري | المشرف     ──── Supervisor  │  │
│  │     Pending  |                         │  │
│  │        │                               │  │
│  │  ⏸  في الانتظار | الأمن السيبراني      │  │
│  │     Waiting     |  Cyber               │  │
│  │        │                               │  │
│  │  ⏸  في الانتظار | المشرف العام         │  │
│  │     Waiting     |  Admin               │  │
│  └──────────────────────────────────────┘  │
│                                            │
│  ┌──────────────────────────────────────┐  │
│  │  تفاصيل الطلب / Request Details       │  │
│  │  الكلية: الهندسة                      │  │
│  │  القسم: هندسة الحاسب                  │  │
│  │  المبنى: مبنى A                       │  │
│  │  تاريخ التقديم: 2026-07-15            │  │
│  └──────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

#### 6.2.4 Approver Workflow Queue (`/admin/workflow-queue.html`)

```
┌──────────────────────────────────────────┐
│  ← Dashboard                              │
│                                            │
│  قائمة طلبات الانتظار                      │
│  Pending Requests                          │
│                                            │
│  ┌──────┬──────────┬───────┬────────┬───┐  │
│  │ #    │ Student  │Date   │College │   │  │
│  ├──────┼──────────┼───────┼────────┤   │  │
│  │ 1042 │ أحمد     │15/7   │هندسة   │[عرض]│  │
│  │ 1043 │ سارة     │15/7   │طب      │[عرض]│  │
│  │ 1044 │ محمد     │14/7   │علوم    │[عرض]│  │
│  └──────┴──────────┴───────┴────────┴───┘  │
│                                            │
│  [ 1 ] [ 2 ] [ 3 ] ... [ 10 ]             │
└──────────────────────────────────────────┘
```

---

## 7. Security Controls

### 7.1 OTP Security

| Control | Implementation |
|---------|---------------|
| Code length | 6 digits (1,000,000 combinations) |
| Hashing | SHA-256 with per-request random 32-byte salt — OTP never stored in plaintext |
| TTL | 5 minutes — deleted from cache and invalidated in DB |
| Max attempts | 5 per phone number per session — auto-invalidate after limit |
| Rate limiting | 3 sends per phone per 10 minutes; 10 sends per IP per 10 minutes |
| Brute-force protection | Exponential backoff after 3 failed attempts; CAPTCHA after 5 |
| Session binding | OTP tied to session ID + IP address (validated at verify) |
| Masking | Never display full phone number — show only last 4 digits |

### 7.2 CSRF / XSS

| Control | Implementation |
|---------|---------------|
| CSRF | Anti-forgery tokens on all state-changing requests; `SameSite=Strict` cookies |
| XSS | Content-Security-Policy header; output encoding (Razor auto-encode); `X-XSS-Protection: 1; mode=block` |
| CORS | Restrict to production origin; no wildcard |

### 7.3 Rate Limiting

```
OTP Send:        3/hour per phone, 10/hour per IP
Registration:    1 per student per semester (enforced by DB unique constraint + app check)
Approve/Reject:  30/minute per user (global)
API General:     60/minute per authenticated user
```

### 7.4 Data Protection

- OTP code: never stored — only SHA-256 hash
- Phone numbers: encrypted at rest (AES-256-GCM) in `Students.phone` via SQL column encryption or app-layer encryption
- AUP PDFs: stored with SHA-256 checksum; integrity verified on download
- PII in logs: masked — `SmsLogs.phone_number` stores masked value (last 4 digits) for non-OTP messages; full number only in encrypted `OTPVerifications.phone_number`

### 7.5 Session & Token Security

- JWT access token: 15-minute expiry
- Refresh token: 7-day expiry, stored hashed in DB
- OTP verification grants a temporary session token (5-minute TTL) to complete registration
- All workflow actions require fresh JWT re-validation

---

## 8. Scalability Design (10,000+ Students)

### 8.1 Expected Load Profile

| Metric | Value |
|--------|-------|
| Total students | 10,000–12,000 |
| Concurrent users (peak registration) | ~500 |
| Registrations per day (peak) | ~200 |
| OTP sends per day (peak) | ~600 |
| Workflow actions per day | ~600 |
| SMS per day (peak) | ~800 |
| Audit log entries per day | ~2,000 |

### 8.2 Database Scalability

**Read optimization:**
- Covering indexes on all query patterns (see Section 9)
- Read replicas for reporting queries (WorkflowHistory, SmsLogs)
- Pagination with keyset (cursor) pagination for large lists instead of `OFFSET`/`FETCH`

**Write optimization:**
- Batch INSERT for audit logs
- Asynchronous SMS logging — INSERT with `OUTPUT inserted.Id`, fire-and-forget
- OTP cleanup via background job (runs every 5 minutes, deletes expired + verified > 24h)

**Table partitioning:**
- `WorkflowHistory`: partition by month on `performed_at`
- `SmsLogs`: partition by month on `created_at`
- `AuditLogs` (Phase 1): extend partitioning if not already in place

### 8.3 Caching Strategy

| Data | Cache | TTL | Invalidated |
|------|-------|-----|-------------|
| Active declarations | `IMemoryCache` | 1 hour | On declaration CRUD |
| AUP metadata | `IMemoryCache` | 1 hour | On AUP upload |
| OTP attempt counters | `IMemoryCache` | 10 minutes | Auto-expire |
| Rate-limit counters | `IMemoryCache` (sliding) | Window duration | Auto-expire |
| Workflow stats (admin dashboard) | `IDistributedCache` | 5 minutes | Time-based |

### 8.4 Concurrency

- Optimistic concurrency on `Requests` table using `rowversion` / `timestamp` column — prevents double-approval
- Pessimistic locking on OTP verification — `UPDLOCK, ROWLOCK` on the specific `OTPVerifications` row during verification
- Idempotency keys on registration submission — prevent duplicate submissions

### 8.5 SMS Provider Resilience

- Primary SMS provider with automatic failover to secondary
- Retry queue: up to 3 retries with exponential backoff (30s, 2min, 5min)
- Daily SMS budget cap: fail-safe to prevent billing overrun
- Bulk SMS: batched sends (max 100 recipients per API call) for notifications

### 8.6 Background Job Architecture

```
┌─────────────┐     ┌──────────────┐     ┌──────────────┐
│ Registration │────►│ Channel<T>   │────►│ SmsProcessor │
│ Workflow     │     │ (queue)      │     │ (IHostedService)
└─────────────┘     └──────────────┘     └──────┬───────┘
                                                │
                                    ┌───────────┴───────────┐
                                    ▼                       ▼
                            ┌──────────────┐       ┌──────────────┐
                            │ SMS Provider  │       │   SmsLogs    │
                            │ (HTTP API)    │       │   (INSERT)   │
                            └──────────────┘       └──────────────┘
```

---

## 9. Database Indexing Strategy

### 9.1 Index Definitions

#### `OTPVerifications`

```sql
-- Fast lookup by phone + status for verification
CREATE INDEX IX_OTPVerifications_phone_status
    ON OTPVerifications(phone_number, verified_at)
    WHERE verified_at IS NULL;

-- Lookup by session for rate limiting
CREATE INDEX IX_OTPVerifications_session_ip
    ON OTPVerifications(session_id, ip_address, created_at)
    INCLUDE (attempts);

-- Cleanup expired OTPs
CREATE INDEX IX_OTPVerifications_expires
    ON OTPVerifications(expires_at)
    WHERE verified_at IS NULL;
```

#### `StudentDeclarations`

```sql
-- Active declarations sorted for display
CREATE INDEX IX_StudentDeclarations_active
    ON StudentDeclarations(sort_order)
    INCLUDE (declaration_text_ar, declaration_text_en)
    WHERE is_active = 1;
```

#### `StudentDeclarationAcknowledgements`

```sql
-- Check if student has acknowledged
CREATE UNIQUE INDEX IX_StudentDeclAck_student_declaration
    ON StudentDeclarationAcknowledgements(student_id, declaration_id);
```

#### `AupAcknowledgements`

```sql
-- Latest acknowledgement per student
CREATE INDEX IX_AupAck_student_date
    ON AupAcknowledgements(student_id, agreed_at DESC);
```

#### `WorkflowHistory`

```sql
-- Timeline for a request
CREATE INDEX IX_WorkflowHistory_request
    ON WorkflowHistory(request_id, performed_at DESC);

-- Filter by performer
CREATE INDEX IX_WorkflowHistory_performer
    ON WorkflowHistory(performed_by, performed_at DESC);

-- Partition-aligned clustered index
CREATE CLUSTERED INDEX IX_WorkflowHistory_performed_at
    ON WorkflowHistory(performed_at);
```

#### `SmsLogs`

```sql
-- Lookup by reference
CREATE INDEX IX_SmsLogs_reference
    ON SmsLogs(reference_type, reference_id, created_at DESC);

-- SMS status monitoring
CREATE INDEX IX_SmsLogs_status
    ON SmsLogs(status, created_at)
    INCLUDE (phone_number, message_type);

-- Partition-aligned clustered index
CREATE CLUSTERED INDEX IX_SmsLogs_created_at
    ON SmsLogs(created_at);
```

#### `Requests` (Extended)

```sql
-- Workflow queue by status + date for approvers
CREATE INDEX IX_Requests_workflow_status
    ON Requests(status, created_at DESC)
    INCLUDE (student_id)
    WHERE status IN ('pending_supervisor', 'pending_cyber', 'pending_admin');
```

#### `Students` (New indexes for Phase 3)

```sql
-- Lookup by phone for OTP (helps find existing student by phone)
CREATE INDEX IX_Students_phone
    ON Students(phone)
    WHERE phone IS NOT NULL;
```

### 9.2 Index Maintenance

| Index | Maintenance | Frequency |
|-------|-------------|-----------|
| `IX_OTPVerifications_expires` (filtered) | Rebuild after large cleanup | Weekly |
| `IX_WorkflowHistory_request` | Reorganize if fragmentation > 30% | Monthly |
| `IX_SmsLogs_status` | Rebuild after cleanup job | Monthly |

---

## 10. Audit Requirements

### 10.1 Audit Events (Phase 3 Specific)

| Event | Logged To | Data Captured |
|-------|-----------|---------------|
| OTP sent | `AuditLogs` + `SmsLogs` | phone (masked), IP, session |
| OTP verified | `AuditLogs` | phone (masked), success/fail, attempt count |
| Declarations acknowledged | `AuditLogs` | student_id, declaration_ids, IP |
| AUP acknowledged | `AuditLogs` | student_id, aup_version, pdf_checksum |
| Registration submitted | `AuditLogs` + `WorkflowHistory` | full request payload (PII excluded for audit) |
| Registration approved | `AuditLogs` + `WorkflowHistory` | approver, comments, new status |
| Registration rejected | `AuditLogs` + `WorkflowHistory` | approver, comments, reason |
| SMS sent | `AuditLogs` + `SmsLogs` | type, reference, status, cost |

### 10.2 Audit Log Schema (extends Phase 1 `AuditLogs`)

New `action_group` values for Phase 3:
- `otp` — all OTP operations
- `declaration` — declaration acknowledgement
- `aup` — AUP operations
- `registration` — registration CRUD
- `workflow` — approve/reject/submit

### 10.3 Data Retention

| Table | Retention | Cleanup |
|-------|-----------|---------|
| `OTPVerifications` | 24 hours after expiry | Background job every 5 min |
| `SmsLogs` | 90 days | Monthly archive job |
| `WorkflowHistory` | Permanent | Never delete |
| `StudentDeclarationAcknowledgements` | Permanent | Never delete |
| `AupAcknowledgements` | Permanent | Never delete |
| `AuditLogs` (Phase 1) | 1 year | Archive to cold storage |

---

## 11. SMS Notification Framework

### 11.1 Provider Interface

```csharp
public interface ISmsProvider
{
    string ProviderName { get; }
    Task<SmsResult> SendAsync(SmsMessage message);
    Task<SmsStatus> CheckStatusAsync(string providerMessageId);
}

public class SmsMessage
{
    public string To { get; set; }              // e.g., "966512345678"
    public string Body { get; set; }            // Message content
    public string? SenderId { get; set; }       // e.g., "NUHousing"
    public SmsPriority Priority { get; set; }
}

public class SmsResult
{
    public bool Success { get; set; }
    public string? ProviderMessageId { get; set; }
    public decimal? Cost { get; set; }
    public string? Error { get; set; }
}
```

### 11.2 Notification Templates

| Event | Template (Arabic) | Template (English) |
|-------|-------------------|--------------------|
| OTP | `رمز التحقق الخاص بك هو: {code}. صالح لمدة 5 دقائق.` | `Your verification code: {code}. Valid for 5 minutes.` |
| Submitted | `تم استلام طلب التسجيل الخاص بك رقم {ref}. سنقوم بإعلامك عند التحديث.` | `Your registration request #{ref} has been received. We will notify you of updates.` |
| Supervisor Approved | `تمت الموافقة على طلبك من قبل المشرف. قيد مراجعة الأمن السيبراني.` | `Your request has been approved by supervisor. Pending cyber security review.` |
| Cyber Approved | `تمت مراجعة طلبك أمنياً. قيد انتظار الموافقة النهائية من الإدارة.` | `Your request has passed security review. Pending admin approval.` |
| Approved | `تمت الموافقة على طلب التسجيل رقم {ref}.` | `Your registration request #{ref} has been approved.` |
| Rejected | `عذراً، تم رفض طلب التسجيل رقم {ref}. السبب: {reason}.` | `Sorry, your registration request #{ref} was rejected. Reason: {reason}.` |
| Provisioned | `تم تفعيل حساب شبكة السكن الخاص بك. اسم المستخدم: {username}` | `Your housing network account has been activated. Username: {username}` |

### 11.3 Queue Processing

```
Registration Workflow Service
    │
    ├──► Request approved/rejected
    ├──► Enqueue SmsWorkItem { phone, template, params }
    │
    ▼
SmsBackgroundService (Channel<SmsWorkItem>)
    │
    ├──► Dequeue item
    ├──► Insert SmsLogs (status = 'pending')
    ├──► Call ISmsProvider.SendAsync()
    ├──► Update SmsLogs (status = 'sent' | 'failed')
    │
    ▼
RetryPolicy: 3 attempts (30s / 2min / 5min backoff)
```

---

## 12. Appendices

### 12.1 Configuration Keys (New for Phase 3)

| Key | Default | Description |
|-----|---------|-------------|
| `Sms.Provider` | `"LogOnly"` | SMS provider implementation (`LogOnly`, `Twilio`, `SMSGateway`) |
| `Sms.SenderId` | `"NUHousing"` | SMS sender name |
| `Sms.MaxPerDay` | `1000` | Daily SMS budget cap |
| `Sms.RetryCount` | `3` | Max SMS retries |
| `Sms.RetryBaseDelaySeconds` | `30` | Initial retry delay |
| `Otp.Length` | `6` | OTP digit length |
| `Otp.TtlMinutes` | `5` | OTP validity period |
| `Otp.MaxAttempts` | `5` | Max failed verification attempts |
| `Otp.RateLimit.Phone` | `3` | Max OTP sends per phone per window |
| `Otp.RateLimit.PhoneWindowMinutes` | `10` | Rate limit window |
| `Otp.RateLimit.Ip` | `10` | Max OTP sends per IP per window |
| `Registration.MaxPerSemester` | `1` | Max registration requests per student per semester |
| `Workflow.AutoEscalateDays` | `3` | Days after which an approver is auto-reminded |

### 12.2 Phase 3 Integration Points

| Existing Component | Phase 3 Integration |
|--------------------|---------------------|
| `AuthController` | Add `student` role; extend JWT to carry `registration_step` |
| `NotificationsController` | Create in-app notification when workflow advances |
| `ActiveDirectoryService` | Trigger AD provisioning on admin approval (final step) |
| `AccountLifecycleLogs` | Log provisioning trigger as lifecycle event |
| `HousingAccountManagementController` | Student accounts appear after provisioning |
| `Sidebar.js` | Add "My Requests" link for student role |
| `i18n.js` | Add Phase 3 translation keys |
| `AuditLogsController` | Extend filters for Phase 3 action groups |

### 12.3 Deployment Order

1. **Database**: Create new tables, indexes, seed declarations
2. **Backend**: Deploy new controllers, services, background jobs
3. **Frontend**: Deploy registration pages, workflow queue pages
4. **Config**: Set SMS provider, rate limits, AUP PDF
5. **Test**: Full workflow from student registration to provisioning
6. **Go-Live**: Enable student registration on portal

---

*End of Phase 3 Design Document*
