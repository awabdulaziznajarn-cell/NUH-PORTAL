# Phase 2 Technical Design Document

## Housing Account Lifecycle Management & System Enhancement

**Status:** Design Only — No code changes, migrations, or deployments
**Based On:** Phase 1 — validated stable production system (2026-07-13)
**Label:** Phase1-ADProvisioning-Stable

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current Architecture Analysis](#2-current-architecture-analysis)
3. [Phase 2 Scope & Objectives](#3-phase-2-scope--objectives)
4. [Database Design](#4-database-design)
5. [API Design](#5-api-design)
6. [UI Design](#6-ui-design)
7. [Workflow Design](#7-workflow-design)
8. [Notification Design](#8-notification-design)
9. [AD Integration Design](#9-ad-integration-design)
10. [Files Requiring Modification](#10-files-requiring-modification)
11. [Risks & Mitigations](#11-risks--mitigations)
12. [Implementation Plan](#12-implementation-plan)
13. [Rollback Strategy](#13-rollback-strategy)
14. [Future Cleanup Planning](#14-future-cleanup-planning-pre-handover)

---

## 1. Executive Summary

Phase 1 successfully delivered the core workflow: Student Registration → Housing/Cyber approvals → AD Provisioning → Completion.

Phase 2 extends the system to manage the **full housing account lifecycle** beyond initial creation. Key additions include:
- Housing account search, view, and manual management (enable/disable by supervisor)
- Re-provisioning workflow for failed or deleted accounts
- Building transfer workflow to move accounts between OUs/groups when students change buildings
- Extension attribute tracking to link AD accounts to student records
- Dedicated Housing Account Management page and per-student housing account status badge
- Lifecycle history display on the student page for full audit traceability
- Enhanced audit logging for all housing account operations

---

## 2. Current Architecture Analysis

### 2.1 Database Schema (Current)

```
Users            Requests          Students           Notifications
┌──────────┐    ┌──────────────┐  ┌────────────────┐  ┌──────────────┐
│ Id       │    │ Id           │  │ Id             │  │ Id           │
│ username │    │ student_id   │  │ student_id     │  │ request_id   │
│ full_name│    │ status       │  │ full_name      │  │ channel      │
│ email    │    │ request_type │  │ national_id    │  │ recipient_role│
│ role     │    │ submitted_by │  │ ad_username    │  │ message      │
│ ...      │    │ ...          │  │ status         │  │ status       │
└──────────┘    │ reviewed_by  │  │ student_status │  │ sent_at      │
                │ completed_by │  │ ...            │  └──────────────┘
                │ ...          │  │ IsDeleted      │
                └──────────────┘  │ DeletedBy      │  StudentStatusActions
                                  │ ...            │  ┌─────────────────┐
      BulkRequests                └────────────────┘  │ Id              │
      ┌──────────────────┐        AuditLogs           │ StudentId       │
      │ Id               │        ┌──────────────┐     │ StatusType      │
      │ RequestNumber    │        │ Id           │     │ Notes           │
      │ FileName         │        │ user_id      │     │ PendingADAction │
      │ RecordCount      │        │ action       │     │ ADActionCompleted│
      │ Status           │        │ target_table │     │ ADActionDate    │
      └──────────────────┘        │ target_id    │     └─────────────────┘
                                  │ action_at    │
      BulkRequestStudents         │ ip_address   │
      ┌──────────────────┐        │ user_agent   │
      │ Id               │        └──────────────┘
      │ BulkRequestId    │
      │ StudentID        │        AuditChangeLogs
      │ ...              │        ┌──────────────┐
      └──────────────────┘        │ Id           │
                                  │ AuditLogId   │
                                  │ FieldName    │
                                  │ OldValue     │
                                  │ NewValue     │
                                  └──────────────┘
```

### 2.2 Request Status Flow

```
submitted → housing_approved / housing_rejected
housing_approved → cyber_review
cyber_review → cyber_approved / cyber_rejected
cyber_approved → ready_for_provisioning
ready_for_provisioning → completed  ← AD account created here
```

### 2.3 AD Provisioning Service Flow

```
ProvisionAsync():
  1. Check if AD account exists (sAMAccountName = "h{student_id}")
     → If exists → SKIP (return warning)
  2. Create user in OU based on gender
  3. Set password (NUH@{student_id})
  4. Enable account (UAC = 544 → 66112)
  5. Add to group based on gender
  6. Update student.ad_username
  7. Log StudentStatusAction
  8. Audit log
```

### 2.4 Current Gaps

| Gap | Impact |
|-----|--------|
| No AD account management UI | Cannot enable/disable student AD accounts |
| No AD account search | Cannot look up AD status from the application |
| No re-provisioning | If provisioning fails or is skipped, manual DB fix needed |
| No extension attribute linkage | No stable link between AD account and student record |
| No AD-specific dashboard | No visibility into AD account health |
| No AD activity history per student | Cannot audit AD operations per student |
| Hard-coded OU/group paths | Configuration changes require recompilation |
| Hard-coded password format | NUH@{student_id} — no configurability |

---

## 3. Phase 2 Scope & Objectives

### 3.1 Objectives

1. **Housing Account Management**: Enable admins and supervisors to disable and enable student housing accounts from the UI.
2. **Housing Account Visibility**: Provide a searchable housing account list and per-student account status.
3. **Re-provisioning Workflow**: Allow re-provisioning when accounts fail or need recreation.
4. **Building Transfer Workflow**: Allow moving a student's housing account to a different building's OU/group when the student changes residence.
5. **Lifecycle History**: Display full account lifecycle timeline per student (created, enabled, disabled, building transferred, re-provisioned).
6. **Extension Attribute Tracking**: Store `RequestId` and `StudentId` in AD extension attributes for traceability.
7. **Audit Enhancement**: Full audit trail for every housing account operation with before/after values.
8. **Configurability**: Move OU paths, group DNs, and password policy to configuration.

### 3.2 Out of Scope

- Self-service student password reset
- Admin-initiated password reset
- Status-driven auto-disable (disable AD only through explicit supervisor action)
- AD group management UI (beyond the provisioning group)
- Real-time AD sync (polling/push)
- Multi-domain support
- SAML/SSO integration
- Mobile app

---

## 4. Database Design

### 4.1 New Tables

#### `AccountLifecycleLogs` — Tracks every AD operation per student

```sql
CREATE TABLE AccountLifecycleLogs (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    StudentId       INT NOT NULL,              -- FK → Students.Id
    ActionType      NVARCHAR(50) NOT NULL,      -- 'created','enabled','disabled',
                                               -- 'group_updated','deleted','re_provisioned',
                                               -- 'building_transferred'
    PerformedBy     INT NOT NULL,               -- FK → Users.Id
    PerformedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Result          NVARCHAR(10) NOT NULL,      -- 'success','failed','skipped'
    ErrorMessage    NVARCHAR(500) NULL,         -- error details if failed
    Details         NVARCHAR(MAX) NULL,         -- JSON with before/after state
    TransferReason  NVARCHAR(100) NULL,         -- required for building_transferred:
                                               -- 'Student Request','Maintenance','Room Change',
                                               -- 'Administrative Decision','Disciplinary Action','Other'
    
    CONSTRAINT FK_AccountLifecycleLogs_Students FOREIGN KEY (StudentId) REFERENCES Students(Id),
    CONSTRAINT FK_AccountLifecycleLogs_Users FOREIGN KEY (PerformedBy) REFERENCES Users(Id)
);
```

#### `ADConfigurations` — Moves hard-coded AD paths to DB

```sql
CREATE TABLE ADConfigurations (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    ConfigKey       NVARCHAR(100) NOT NULL UNIQUE,
    ConfigValue     NVARCHAR(500) NOT NULL,
    Description     NVARCHAR(255) NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    UpdatedBy       INT NULL,
    UpdatedAt       DATETIME2 NULL DEFAULT GETUTCDATE()
);

-- Seed data
INSERT INTO ADConfigurations (ConfigKey, ConfigValue, Description) VALUES
('OU.Male',           'OU=Male,OU=New,OU=Students,DC=globalgroups,DC=com',
                      'Target OU for male student accounts'),
('OU.Female',         'OU=Female,OU=New,OU=Students,DC=globalgroups,DC=com',
                      'Target OU for female student accounts'),
('Group.Male',        'CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com',
                      'Security group for male students'),
('Group.Female',      'CN=NUH-Student-G,OU=Groups,DC=globalgroups,DC=com',
                      'Security group for female students'),
('Password.Format',   'NUH@{student_id}',
                      'Password generation template; {student_id} is replaced'),
('UAC.Enabled',       '66112',
                      'UAC value for enabled accounts (PASSWD_CANT_CHANGE + DONT_EXPIRE)'),
('UAC.Disabled',      '514',
                      'UAC value for disabled accounts (ACCOUNTDISABLE)'),
('ExtensionAttr.StudentId', 'extensionAttribute1',
                      'AD attribute to store student_id'),
('ExtensionAttr.RequestId', 'extensionAttribute2',
                      'AD attribute to store request_id');
```

### 4.2 Modified Tables

#### `Students` — New columns for AD account status tracking

```sql
ALTER TABLE Students ADD
    ad_status           NVARCHAR(20) NULL,  -- 'active','disabled','not_created','failed'
    ad_last_sync_at     DATETIME2 NULL,     -- last time AD was checked/updated
    ad_extension_attr1  NVARCHAR(255) NULL, -- student_id stored in AD (mirror)
    ad_extension_attr2  NVARCHAR(255) NULL; -- request_id stored in AD (mirror)
```

### 4.3 Indexes

```sql
CREATE INDEX IX_AccountLifecycleLogs_StudentId ON AccountLifecycleLogs(StudentId);
CREATE INDEX IX_AccountLifecycleLogs_ActionType ON AccountLifecycleLogs(ActionType);
CREATE INDEX IX_AccountLifecycleLogs_PerformedAt ON AccountLifecycleLogs(PerformedAt DESC);
CREATE INDEX IX_Students_ad_status ON Students(ad_status) WHERE ad_status IS NOT NULL;
```

### 4.4 Entity Framework Models

#### New: `AccountLifecycleLog.cs`

```csharp
[Table("AccountLifecycleLogs")]
public class AccountLifecycleLog
{
    public int Id { get; set; }

    [Column("StudentId")]
    public int StudentId { get; set; }

    [Column("ActionType")]
    public string ActionType { get; set; } = string.Empty;  // created/enabled/disabled/deleted/building_transferred

    [Column("PerformedBy")]
    public int PerformedBy { get; set; }

    [Column("PerformedAt")]
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [Column("Result")]
    public string Result { get; set; } = string.Empty;  // success/failed/skipped

    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("Details")]
    public string? Details { get; set; }  // JSON payload

    [Column("TransferReason")]
    public string? TransferReason { get; set; }  // Required for building_transferred

    public Student? Student { get; set; }
    public User? Performer { get; set; }
}
```

#### New: `ADConfiguration.cs`

```csharp
[Table("ADConfigurations")]
public class ADConfiguration
{
    public int Id { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

#### Modified: `Student.cs` — Add fields

```csharp
// New fields to add to existing Student model
public string? ad_status { get; set; }
public DateTime? ad_last_sync_at { get; set; }
public string? ad_extension_attr1 { get; set; }
public string? ad_extension_attr2 { get; set; }
```

#### `AppDbContext.cs` — Add DbSets

```csharp
public DbSet<AccountLifecycleLog> AccountLifecycleLogs { get; set; }
public DbSet<ADConfiguration> ADConfigurations { get; set; }
```

---

## 5. API Design

### 5.1 New Endpoints

#### `GET /api/ad/students/search?q=...&status=...&page=1&pageSize=50`

Search and list AD student accounts with pagination.

**Response:**
```json
{
  "total": 150,
  "page": 1,
  "pageSize": 50,
  "items": [
    {
      "studentId": 216,
      "studentNumber": "999999999",
      "fullName": "...",
      "adUsername": "h999999999",
      "adStatus": "active",
      "adLastSyncAt": "2026-07-13T08:47:38Z",
      "provisioningRequestId": 111,
      "accountEnabled": true,
      "memberOf": ["NUH-Student-B"],
      "lastAction": "created",
      "lastActionAt": "2026-07-13T08:47:38Z",
      "disabledAt": null,
      "daysDisabled": null
    }
  ]
}
```

#### `GET /api/ad/students/{studentId}`

Get detailed AD info for a specific student, including real-time check against AD.

**Response:**
```json
{
  "studentId": 216,
  "studentNumber": "999999999",
  "fullName": "...",
  "adUsername": "h999999999",
  "adStatus": "active",
  "accountEnabled": true,
  "distinguishedName": "CN=h999999999,OU=Male,...",
  "userPrincipalName": "h999999999@globalgroups.com",
  "memberOf": ["NUH-Student-B"],
  "userAccountControl": 66112,
  "whenCreated": "2026-07-13T08:47:38Z",
  "extensionAttribute1": "999999999",
  "extensionAttribute2": "111",
  "actionHistory": [
    { "actionType": "created", "result": "success", "performedAt": "...", "performedBy": "admin" },
    { "actionType": "building_transferred", "result": "success",
      "performedAt": "...", "performedBy": "supervisor",
      "transferReason": "Student Request",
      "details": "{ \"previousOu\": \"OU=Female,...\", \"newOu\": \"OU=Male,...\" }" }
  ]
}
```

#### `POST /api/ad/students/{studentId}/disable`

Disable a student's AD account. Available to `admin` and `supervisor` roles.

**Request:** `{ "reason": "طالب متخرج" }`

**Response:** `{ "success": true, "actionType": "disabled", "previousUac": 66112, "newUac": 514 }`

#### `POST /api/ad/students/{studentId}/enable`

Enable a student's AD account. Available to `admin` and `supervisor` roles.

**Request:** `{ "reason": "تمت إعادة القيد" }`

**Response:** `{ "success": true, "actionType": "enabled", "previousUac": 514, "newUac": 66112 }`

#### `POST /api/ad/students/{studentId}/transfer-building`

Transfer a student's AD account to a different building's OU and group. Available to `admin` and `supervisor` roles.

**Request:**
```json
{
  "newBuilding": "بنين 2",
  "newGender": "male",
  "reason": "انتقال إلى مبنى آخر"
}
```

**Response:**
```json
{
  "success": true,
  "actionType": "building_transferred",
  "previousOu": "OU=Female,...",
  "newOu": "OU=Male,...",
  "previousGroup": "NUH-Student-G",
  "newGroup": "NUH-Student-B"
}
```

#### `POST /api/ad/students/{studentId}/re-provision`

Re-attempt provisioning (for failed accounts or to update attributes).

**Request:** `{ "force": false }` — if `true`, recreates even if account exists

**Response:**
```json
{
  "success": true,
  "actionType": "re_provisioned",
  "adUsername": "h999999999",
  "previousStatus": "failed",
  "newStatus": "active"
}
```

#### `GET /api/ad/config`

Return all AD configuration keys.

**Response:**
```json
{
  "items": [
    { "configKey": "OU.Male", "configValue": "OU=Male,...", "description": "..." }
  ]
}
```

#### `PUT /api/ad/config`

Update an AD configuration value (admin only).

**Request:** `{ "configKey": "Password.Format", "configValue": "Hous@{student_id}" }`

#### `POST /api/ad/verify-connection`

Test AD connectivity and return diagnostic info.

**Response:**
```json
{
  "reachable": true,
  "bindSuccessful": true,
  "domainController": "DC-01.globalgroups.com",
  "port": 636,
  "serverTime": "..."
}
```

### 5.2 Modified Endpoints

#### `PUT /api/requests/{id}/review` — Update

When transitioning to `completed`:
- In addition to calling `ProvisionAsync()`
- Also write `extensionAttribute1` and `extensionAttribute2` on the AD account
- Set `student.ad_status = 'active'`
- Set `student.ad_last_sync_at = DateTime.UtcNow`
- Record an `AccountLifecycleLog` with `ActionType = 'created'`

### 5.3 Request/Response Models

```csharp
public class ADStudentSearchResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ADStudentItem> Items { get; set; } = new();
}

public class ADStudentItem
{
    public int StudentId { get; set; }
    public string? StudentNumber { get; set; }
    public string? FullName { get; set; }
    public string? AdUsername { get; set; }
    public string? AdStatus { get; set; }
    public DateTime? AdLastSyncAt { get; set; }
    public int? ProvisioningRequestId { get; set; }
    public bool AccountEnabled { get; set; }
    public List<string> MemberOf { get; set; } = new();
    public string? LastAction { get; set; }
    public DateTime? LastActionAt { get; set; }
    public DateTime? DisabledAt { get; set; }      // Timestamp of most recent disable action
    public int? DaysDisabled { get; set; }          // Calculated: (DateTime.UtcNow - DisabledAt).Days
}

public class ADStudentDetail : ADStudentItem
{
    public string? DistinguishedName { get; set; }
    public string? UserPrincipalName { get; set; }
    public int UserAccountControl { get; set; }
    public DateTime? WhenCreated { get; set; }
    public string? ExtensionAttribute1 { get; set; }
    public string? ExtensionAttribute2 { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public List<ADActionHistoryItem> ActionHistory { get; set; } = new();
}

public class ADActionHistoryItem
{
    public string ActionType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
    public string? PerformedBy { get; set; }
    public string? Details { get; set; }
    public string? TransferReason { get; set; }  // Populated for building_transferred actions
}

public class ADActionRequest
{
    public string? Reason { get; set; }
}

public class ADReProvisionRequest
{
    public bool Force { get; set; }
}

public class ADTransferBuildingRequest
{
    public string NewBuilding { get; set; } = string.Empty;
    public string? NewGender { get; set; }
    public string? Reason { get; set; }                          // Free-text reason
    public string? TransferReason { get; set; }                   // Allowed: 'Student Request','Maintenance','Room Change','Administrative Decision','Disciplinary Action','Other'
}

public class ADActionResponse
{
    public bool Success { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? AdUsername { get; set; }
}

public class ADConfigUpdateRequest
{
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
}

public class ADVerifyConnectionResponse
{
    public bool Reachable { get; set; }
    public bool BindSuccessful { get; set; }
    public string? DomainController { get; set; }
    public int Port { get; set; }
    public string? Error { get; set; }
}
```

### 5.4 Controller Structure

```csharp
// New controller
[Authorize(Roles = "admin,supervisor")]
[Route("api/ad")]
[ApiController]
public class HousingAccountManagementController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ActiveDirectoryService _adService;
    private readonly ADProvisioningService _provisioningService;
    private readonly ILogger<HousingAccountManagementController> _logger;

    // GET  /api/ad/students/search
    // GET  /api/ad/students/{studentId}
    // POST /api/ad/students/{studentId}/disable
    // POST /api/ad/students/{studentId}/enable
    // POST /api/ad/students/{studentId}/re-provision
    // POST /api/ad/students/{studentId}/transfer-building
    // GET  /api/ad/config
    // PUT  /api/ad/config
    // POST /api/ad/verify-connection
}
```

---

## 6. UI Design

### 6.1 New Page: `housing-management.html`

**Purpose:** Centralized housing account management for all students.

**URL:** `/housing-management.html`

**Layout:** Uses the existing sidebar + topbar layout pattern.

**Sections:**

#### 6.1.1 Housing Accounts Dashboard

```
┌──────────────────────────────────────────────────────────────────┐
│  [Topbar: Housing Account Management | lang toggle | notif bell]  │
├──────────────────────────────────────────────────────────────────┤
│  Stats Row:                                                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐            │
│  │ Total    │ │ Active   │ │ Disabled │ │ Pending  │            │
│  │ Accounts │ │ Accounts │ │ Accounts │ │ Provision│            │
│  │   150    │ │   142    │ │    5     │ │    3     │            │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘            │
├──────────────────────────────────────────────────────────────────┤
│  Search Bar: [___________________________] [Filter: All ▼]      │
├──────────────────────────────────────────────────────────────────┤
│  Table:                                                         │
│  ┌───┬───────┬──────────┬────────┬────────┬────────┬──────────┬──────────────┐│
│  │ # │ ID    │ Name     │ Username│ Status │ Last   │ Days     │ Actions      ││
│  │   │       │          │        │        │ Sync   │ Disabled │              ││
│  ├───┼───────┼──────────┼────────┼────────┼────────┼──────────┼──────────────┤│
│  │ 1 │999999 │ محمد...  │h999999 │ Active │ 07/13  │ —        │ [▼]          ││
│  │ 2 │888888 │ أحمد...  │h888888 │Disabled│ 07/12  │ 7 Days   │ [▼]          ││
│  └───┴───────┴──────────┴────────┴────────┴────────┴──────────┴──────────────┘│
│  Pagination: [1] [2] [3] ... [10]                               │
└──────────────────────────────────────────────────────────────────┘
```

**Actions dropdown per row:**
- `Enable / Disable` — toggle account enabled state (admin or supervisor)
- `View Details` — goes to student account detail view
- `Re-provision` — re-run provisioning with force option
- `Transfer Building` — move account to a different building's OU/group

#### 6.1.2 Student Account Detail (Modal or Section)

```
┌────────────────────────────────────────────────────────────────┐
│ Housing Account Details — h999999999                           │
│ ┌─────────────┐  ┌──────────────────────────────────────────┐ │
│ │Student Info │  │ Account Status: ● Active                 │ │
│ │ID: 999999999│  │ Building: مبنى 1                          │ │
│ │Name: ...    │  │ Username: h999999999                      │ │
│ │College: ... │  │ DN: CN=h999999999,OU=Male,...             │ │
│ │Status:active│  │ Groups: NUH-Student-B                     │ │
│ └─────────────┘  │ Created: 2026-07-13 08:47                 │ │
│                  │ Last Transferred: —                        │ │
│                  └──────────────────────────────────────────┘ │
│                                                              │
│ Quick Actions:                                               │
│ [Disable] [Enable] [Re-provision] [Transfer Building]        │
│                                                              │
│ Action History:                                              │
│ ┌──────────┬──────────┬──────────┬──────────┬──────────────┐ │
│ │ Type     │ Result   │ Date     │ Performed│ Details      │ │
│ ├──────────┼──────────┼──────────┼──────────┼──────────────┤ │
│ │created   │ success  │07/13 8:47│ admin    │ Created...   │ │
│ │enabled   │ success  │07/14 9:00│ adm      │ UAC→66112   │ │
│ │bldg_xfer │ success  │07/15 10:30│ super    │ OU:F→M      │ │
│ └──────────┴──────────┴──────────┴──────────┴──────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

#### 6.1.3 AD Config Page (Tab/Section, Admin Only)

```
┌────────────────────────────────────────────────────────────────┐
│ AD Configuration                              [Test Connection]│
│                                                              │
│ ┌──────────────────────┬────────────────────────┬──────────┐  │
│ │ Key                  │ Value                  │ Actions  │  │
│ ├──────────────────────┼────────────────────────┼──────────┤  │
│ │ OU.Male              │ OU=Male,OU=New,...     │ [Edit]   │  │
│ │ Password.Format      │ NUH@{student_id}      │ [Edit]   │  │
│ └──────────────────────┴────────────────────────┴──────────┘  │
│                                                              │
│ Connection Status: ● Connected (DC-01.globalgroups.com:636)  │
└────────────────────────────────────────────────────────────────┘
```

### 6.2 Modified Page: `students.html`

**Add:** Housing Account Status column in the student table and lifecycle history section.

**6.2.1 Housing Account Status Column**

```
.───┬───────┬───────┬───────┬───────┬──────────┬───────┐
│ # │  ID   │ Name  │College│Status │ Hsg Acct │Actions│
│   │       │       │       │       │ Status   │       │
├───┼───────┼───────┼───────┼───────┼──────────┼───────┤
│ 1 │999999 │ ....  │ ....  │ Active│ Active   │ [▼]  │
│   │       │       │       │       │ ●        │       │
└───┴───────┴───────┴───────┴───────┴──────────┴───────┘
```

Housing Account Status Badge colors:
- `● Active` — green badge (student has an active housing account)
- `● Disabled` — gray badge (housing account disabled)
- `● Not Created` — orange badge (no housing account yet)
- `● Failed` — red badge (provisioning failed)

**6.2.2 Lifecycle History Section (Per-Student Modal/Tab)**

When an admin or supervisor clicks a student row (or a "History" action), a modal or expandable section displays the full lifecycle timeline sorted **descending by date** (most recent first):

```
┌───────────────────────────────────────────────────────────────┐
│ Lifecycle History — Student: 999999999 (محمد أحمد)           │
│                                                              │
│ 🟠 2026-07-15 10:30 — Building Transferred                   │
│   Performed by: supervisor                                   │
│   Transfer Reason: Student Request                           │
│   Details: Male OU → Female OU, Group: NUH-Student-B → G    │
│                                                              │
│ 🔴 2026-07-14 09:00 — Disabled                               │
│   Performed by: admin                                        │
│   Details: UAC: 66112 → 514                                  │
│                                                              │
│ 🔵 2026-07-14 08:30 — Enabled                                │
│   Performed by: supervisor                                   │
│   Details: UAC: 514 → 66112                                  │
│                                                              │
│ 🟢 2026-07-13 08:47 — Account Created                        │
│   Performed by: admin (via provisioning)                     │
│   Details: OU=Male, New, Students, DN: CN=h999999999,...    │
│                                                              │
│ ┌───────────────────────────────────────────────────────────┐ │
│ │ No older entries found.                                  │ │
│ └───────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────┘
```

**Color and Icon Legend:**

| Action Type | Icon | Color | CSS Class |
|---|---|---|---|
| ACCOUNT_CREATED | 🟢 | Green (`#28a745`) | `history-created` |
| ACCOUNT_DISABLED | 🔴 | Red (`#dc3545`) | `history-disabled` |
| ACCOUNT_ENABLED | 🔵 | Blue (`#007bff`) | `history-enabled` |
| BUILDING_TRANSFER | 🟠 | Orange (`#fd7e14`) | `history-transfer` |
| (others / default) | ⚪ | Gray (`#6c757d`) | `history-default` |

The history section calls `GET /api/ad/students/{studentId}` and renders the `actionHistory` array as a chronological timeline. Entries are sorted **descending by PerformedAt** (newest first). Each entry shows:
- Color-coded icon according to action type
- Action type display name (localised via i18n)
- Result (success/failed/skipped)
- Performed at date/time
- Performed by user name
- Transfer reason (if action type is `building_transferred`)
- Details (JSON parsed to human-readable text)

### 6.3 Modified Page: `request-details.html`

**Add:** Housing Account card in the detail view (after completion):

```
┌────────────────────────────────────────────────────────────┐
│ Housing Account                                            │
│ ───────────────────────────────────────────                │
│ Username:  h999999999                    Status: ● Active  │
│ Created:   2026-07-13 08:47                                │
│ Building:  مبنى 1                                          │
│ Actions: [Disable] [View in Housing Manager]               │
└────────────────────────────────────────────────────────────┘
```

### 6.4 Sidebar: `sidebar.js`

Add a new navigation entry:

```
SYSTEM
┌──────────────────────┐
│ 🏠 Housing Accts Mgmt│  ← NEW
│ 📋 Audit Log         │
│ 📄 Reports           │
└──────────────────────┘
```

Visible to `admin` and `supervisor` roles.

### 6.5 Column Mapping for `students.html` Table

| Current Column | New Column Added |
|---|---|
| ID | — |
| Name | — |
| National ID | — |
| Phone | — |
| College | — |
| Gender | — |
| Level | — |
| Status | **Housing Account Status** |
| Actions | — |

---

## 7. Workflow Design

### 7.1 Extended Request Status Flow

```
submitted → housing_approved / housing_rejected
housing_approved → cyber_review
cyber_review → cyber_approved / cyber_rejected
cyber_approved → ready_for_provisioning
ready_for_provisioning → completed
                           ↓
                    AD Account Created
                    student.ad_status = 'active'
                    Extension attributes written
                    ↓
              [Post-Completion AD Actions (Manual)]
                    ↓
          ┌──────────────────────────────┐
          │  Enable / Disable            │  ← Manual actions by
          │  Re-provision                │     admin or supervisor
          └──────────────────────────────┘
```

### 7.2 Re-provisioning Workflow

```
Admin/Supervisor clicks "Re-provision"
  │
  ├──→ Check AD account exists
  │       │
  │       ├── Does NOT exist → Fresh provisioning
  │       │                    (same as Phase 1 create)
  │       │
  │       └── Exists AND force=false → Return warning
  │                                    "Account already exists"
  │
  ├──→ force=true → Delete existing AD account
  │                  Create new AD account
  │                  Set password
  │                  Enable account
  │                  Add to group
  │                  Update extension attributes
  │                  Log AccountLifecycleLog
  │
  └──→ Update student.ad_username, ad_status
       Audit log entry
```

### 7.3 Enable/Disable Workflow

```
Admin/Supervisor clicks "Disable"
  │
  ├──→ Confirmation modal:
  │     ┌─────────────────────────────────────────────────────┐
  │     │ 🛑 Are you sure you want to disable this            │
  │     │    Housing Network Account?                         │
  │     ├─────────────────────────────────────────────────────┤
  │     │ Student ID:    440708570                            │
  │     │ Student Name:  محمد أحمد                            │
  │     │ AD Username:   h440708570                           │
  │     │                                                     │
  │     │ Disable Reason: [طالب متخرج                  ▼]     │
  │     │                                                     │
  │     │         [Confirm Disable]        [Cancel]           │
  │     └─────────────────────────────────────────────────────┘
  │     * Disable action does NOT execute until [Confirm Disable] is clicked
  │
  ├──→ Confirmed → ADService.ModifyUserAccountControlAsync(dn, 514)
  │                  ├── Success → student.ad_status = 'disabled'
  │                  │            AccountLifecycleLog logged
  │                  │            Notification sent
  │                  └── Failed  → Show error, no DB change
  │
  └──→ Audit log entry

Admin/Supervisor clicks "Enable"
  ──→ Mirror of Disable with UAC = 66112
       student.ad_status = 'active'
```

### 7.4 Building Transfer Workflow

```
Admin/Supervisor clicks "Transfer Building"
  │
  ├──→ Building selection modal:
  │     "Transfer student محمد أحمد (h999999999) to another building"
  │     Current Building: [مبنى 1] — Read-only
  │     New Building:     [مبنى 2 ▼] — Dropdown of available buildings
  │     New Gender:       [Male ▼]    — Auto-filled from building, editable
  │     Transfer Reason:  [Student Request ▼] — Required dropdown
  │                       Options: Student Request, Maintenance, Room Change,
  │                                Administrative Decision, Disciplinary Action, Other
  │     Reason (optional): [________________]
  │     [Confirm] [Cancel]
  │
  ├──→ Confirmed →
  │     1. Read target OU/group from ADConfigurations (based on new building/gender)
  │     2. ADService.MoveUserAsync(currentDn, targetOu)
  │     3. Update group membership:
  │        Remove from old group → Add to new group
  │     4. Update student building/gender fields if applicable
  │     5. Log AccountLifecycleLog with ActionType = 'building_transferred'
  │        TransferReason = selected reason
  │        Details JSON: { previousOu, newOu, previousGroup, newGroup, reason }
  │     6. Send notification
  │
  └──→ On failure → Show error, rollback partial AD changes
                      No DB changes committed
```

---

## 8. Notification Design

### 8.1 New Notification Types

| Event | Recipients | Message (Arabic/English) | Channel |
|---|---|---|---|---|
| Housing account disabled | admin, supervisor | `تم تعطيل حساب {adUsername} للطالب {studentId}` | in_app |
| Housing account enabled | admin, supervisor | `تم تفعيل حساب {adUsername} للطالب {studentId}` | in_app |
| Housing account building transferred | admin, supervisor | `تم نقل حساب {adUsername} للطالب {studentId} إلى مبنى جديد` | in_app |
| Housing provisioning failed | admin | `فشل إنشاء حساب للطالب {studentId}: {error}` | in_app |
| Housing re-provisioned | admin, supervisor | `تم إعادة إنشاء حساب للطالب {studentId}` | in_app |

### 8.2 Notification Channel

Currently only `in_app`. For Phase 2, remain with `in_app` only. Email/SMS channels are deferred.

### 8.3 Implementation in Notification Model

No new fields needed in the `Notification` model — existing fields (`request_id`, `channel`, `recipient_role`, `message`, `status`) are sufficient. For AD-specific notifications not tied to a request, `request_id` may be 0 or use a sentinel value (e.g., `-1` for system notifications).

---

## 9. AD Integration Design

### 9.1 Extension Attribute Strategy

Use two AD extension attributes to create a stable link between AD accounts and student records:

| Attribute | Content | Purpose |
|---|---|---|
| `extensionAttribute1` | Student number (e.g., `999999999`) | Allow reverse lookup: given an AD account, find the student |
| `extensionAttribute2` | Request ID that created the account (e.g., `111`) | Track which request triggered the provisioning |

**Implementation:**

In `ADProvisioningService.ProvisionAsync()`, after account creation:
```csharp
// Write extension attributes
var modRequest = new ModifyRequest(
    userDn,
    DirectoryAttributeOperation.Replace,
    "extensionAttribute1", student.student_id
);
connection.SendRequest(modRequest);

modRequest = new ModifyRequest(
    userDn,
    DirectoryAttributeOperation.Replace,
    "extensionAttribute2", requestId.ToString()
);
connection.SendRequest(modRequest);
```

In the new `GetUserByStudentIdAsync()` method:
```csharp
// Search AD by extensionAttribute1 = student_id
var searchRequest = new SearchRequest(
    baseDn,
    $"(extensionAttribute1={student.student_id})",
    SearchScope.Subtree,
    "distinguishedName", "sAMAccountName", ...
);
```

### 9.2 New ActiveDirectoryService Methods

```csharp
// Account management
Task<ADOperationResult> DisableUserAsync(string distinguishedName);
Task<ADOperationResult> EnableUserAsync(string distinguishedName);

// Extension attribute management
Task<ADOperationResult> SetExtensionAttributeAsync(string distinguishedName, string attributeName, string value);
Task<ADOperationResult> ClearExtensionAttributeAsync(string distinguishedName, string attributeName);

// Student-specific search
Task<ADReadUserResult> GetUserByStudentIdAsync(string studentId);

// Building transfer
Task<ADOperationResult> MoveUserToOuAsync(string distinguishedName, string targetOu);
Task<ADOperationResult> UpdateGroupMembershipAsync(string userDn, string oldGroupDn, string newGroupDn);

// Re-provisioning (Delete already exists)
Task<ADOperationResult> DeleteADUserAsync(string distinguishedName);  // already exists
```

### 9.3 Updated ADProvisioningService

```csharp
public class ADProvisioningService
{
    // Existing method — modified to write extension attributes
    public async Task<ADProvisioningResult> ProvisionAsync(
        Student student, int actorId, int? requestId = null,
        string? ipAddress = null, string? userAgent = null);

    // New methods
    public async Task<ADOperationResult> DisableStudentAccountAsync(
        Student student, int actorId, string? reason = null,
        string? ipAddress = null, string? userAgent = null);

    public async Task<ADOperationResult> EnableStudentAccountAsync(
        Student student, int actorId, string? reason = null,
        string? ipAddress = null, string? userAgent = null);

    public async Task<ADProvisioningResult> ReProvisionAsync(
        Student student, int actorId, bool force = false,
        int? requestId = null, string? ipAddress = null, string? userAgent = null);

    public async Task<ADAccountStatusResult> GetStudentAccountStatusAsync(
        Student student);

    // Building transfer
    public async Task<ADTransferBuildingResult> TransferBuildingAsync(
        Student student, int actorId, string newBuilding, string? newGender,
        string? transferReason = null, string? reason = null,
        string? ipAddress = null, string? userAgent = null);
}
```

### 9.4 Configuration-Driven Paths

Replace hard-coded values in `ADProvisioningService` with `ADConfiguration` lookups:

```csharp
// Current (hard-coded):
var targetOu = isMale
    ? "OU=Male,OU=New,OU=Students,DC=globalgroups,DC=com"
    : "OU=Female,OU=New,OU=Students,DC=globalgroups,DC=com";

// Phase 2 (config-driven):
var ouKey = isMale ? "OU.Male" : "OU.Female";
var targetOu = await GetConfigAsync(ouKey);
// Fall back to appsettings.json AD:Provisioning:OU.Male if not in DB
```

### 9.5 Password Policy Integration

The `ActiveDirectoryService` already has `GetPasswordPolicyAsync()` and `ValidatePasswordCompatibilityAsync()`. Phase 2 will:
- Use the password format from `ADConfigurations` (e.g., `"NUH@{student_id}"`)
- Fall back to a generated strong password if the format doesn't meet policy

---

## 10. Files Requiring Modification

### 10.1 New Files

| File | Purpose |
|---|---|---|
| `Controllers/HousingAccountManagementController.cs` | New API controller for all housing account management endpoints |
| `Models/AccountLifecycleLog.cs` | New entity for lifecycle action history (includes `building_transferred`) |
| `Models/ADConfiguration.cs` | New entity for AD config key-values |
| `wwwroot/housing-management.html` | New Housing Account Management page |
| `wwwroot/housing-management.js` | JS for Housing Account Management page — search, actions, building transfer, enhanced disable confirmation modal |

### 10.2 Modified Files

| File | Changes |
|---|---|---|
| `Models/Student.cs` | Add `ad_status`, `ad_last_sync_at`, `ad_extension_attr1`, `ad_extension_attr2` |
| `Data/AppDbContext.cs` | Add `DbSet<AccountLifecycleLog>`, `DbSet<ADConfiguration>` |
| `Controllers/RequestsController.cs` | Update `completed` branch to write extension attributes and set `ad_status` |
| `Controllers/StudentsController.cs` | Return `ad_status`, `disabledAt`, `daysDisabled` in student responses; add housing account status filter; add lifecycle history endpoint |
| `Services/ADProvisioningService.cs` | Write extension attributes, add disable/enable/re-provision/building-transfer methods, use config-driven paths |
| `Services/ActiveDirectoryService.cs` | Add `DisableUserAsync`, `EnableUserAsync`, `SetExtensionAttributeAsync`, `GetUserByStudentIdAsync`, `MoveUserToOuAsync`, `UpdateGroupMembershipAsync` |
| `wwwroot/students.html` | Add Housing Account Status column + badge; add Lifecycle History modal/tab |
| `wwwroot/request-details.html` | Add Housing Account card after completion |
| `wwwroot/sidebar.js` | Add Housing Account Mgmt nav item |
| `wwwroot/dashboard.html` | Add housing account stats to dashboard |
| `wwwroot/i18n.js` | Add new translation keys for Housing Account Management |
| `appsettings.json` | Optionally add `AD:Provisioning` config section (fallback values) |

### 10.3 Migration Files

| File | Purpose |
|---|---|
| `Migrations/XXXXXX_AddAccountLifecycleLogs.cs` | Create `AccountLifecycleLogs` table with `TransferReason` column |
| `Migrations/XXXXXX_AddADConfigurations.cs` | Create `ADConfigurations` table + seed data |
| `Migrations/XXXXXX_AddStudentADFields.cs` | Add `ad_status`, `ad_last_sync_at`, `ad_extension_attr1`, `ad_extension_attr2` to Students |
| `Migrations/XXXXXX_AddADIndexes.cs` | Add performance indexes |

---

## 11. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| AD credentials/password exposed in frontend | Low | Critical | All AD operations go through backend API only; frontend never handles passwords. |
| Extension attribute conflict | Low | Medium | Check for existing values before writing; log conflicts. Use consistent format (student number only, no prefix). |
| Disabling wrong AD account | Low | Critical | Require confirmation modal showing student name + ID + AD username before any destructive action. Log all actions. |
| AD service account lacks permissions | Medium | High | Add `POST /api/ad/verify-connection` diagnostic endpoint. Report specific permission errors in UI. |
| Race condition: simultaneous AD operations | Low | Medium | All AD operations are synchronous per-request. Database locking on `AccountLifecycleLogs` prevents double-writes. |
| Re-provisioning with force=true deletes wrong account | Low | Critical | `force=true` requires extra confirmation. Modal shows full account details. Only available to admin role. |
| Large number of AD accounts (performance) | Low | Low | Paginate AD search results (50 per page). AD lookups use cached group info. |
| ADConfigurations table drift from appsettings | Medium | Low | DB values take precedence. Appsettings serve as fallback defaults. Admin UI shows both sources. |
| Building transfer moves user to wrong OU/group | Low | Critical | Require confirmation modal showing current and target building details. Log all before/after values for rollback. Only available to admin and supervisor roles. |

---

## 12. Implementation Plan

### Phase 2 — Estimated: 4-5 days

#### Day 1: Database & Models
- [ ] Create `AccountLifecycleLog.cs` model (includes `TransferReason` field)
- [ ] Create `ADConfiguration.cs` model
- [ ] Add fields to `Student.cs`
- [ ] Update `AppDbContext.cs`
- [ ] Create EF Core migration
- [ ] Seed `ADConfigurations` data
- [ ] Verify migration applies cleanly on a test DB

#### Day 2: Backend — ActiveDirectoryService Extensions
- [ ] Add `DisableUserAsync(distinguishedName)` method
- [ ] Add `EnableUserAsync(distinguishedName)` method
- [ ] Add `SetExtensionAttributeAsync(dn, attrName, value)` method
- [ ] Add `GetUserByStudentIdAsync(studentId)` method
- [ ] Add `MoveUserToOuAsync(distinguishedName, targetOu)` method
- [ ] Add `UpdateGroupMembershipAsync(userDn, oldGroupDn, newGroupDn)` method
- [ ] Add `WriteExtensionAttributesAsync(dn, studentId, requestId)` method
- [ ] Update `ADProvisioningService.ProvisionAsync()` to write extension attributes
- [ ] Add config-driven OU/group lookup to `ADProvisioningService`

#### Day 3: Backend — ADManagementController
- [ ] Create `ADManagementController.cs`
- [ ] Implement `GET /api/ad/students/search`
- [ ] Implement `GET /api/ad/students/{studentId}`
- [ ] Implement `POST /api/ad/students/{studentId}/disable`
- [ ] Implement `POST /api/ad/students/{studentId}/enable`
- [ ] Implement `POST /api/ad/students/{studentId}/re-provision`
- [ ] Implement `POST /api/ad/students/{studentId}/transfer-building`
- [ ] Implement `GET /api/ad/config` + `PUT /api/ad/config`
- [ ] Implement `POST /api/ad/verify-connection`
- [ ] Implement `ADProvisioningService.ReProvisionAsync()`
- [ ] Implement `ADProvisioningService.DisableStudentAccountAsync()`
- [ ] Implement `ADProvisioningService.EnableStudentAccountAsync()`
- [ ] Implement `ADProvisioningService.GetStudentAccountStatusAsync()`
- [ ] Implement `ADProvisioningService.TransferBuildingAsync()`

#### Day 4: Backend — Controller Modifications
- [ ] Update `RequestsController.cs` — write extension attributes on completion
- [ ] Update `StudentsController.cs` — expose `ad_status` in responses
- [ ] Add AD status filter to `GET /api/students`

#### Day 5: Frontend — New Page & Modifications
- [ ] Create `wwwroot/housing-management.html` (full page with table, search, pagination, building transfer modal)
- [ ] Create `wwwroot/housing-management.js` (search, actions, building transfer, enhanced disable confirmation modal)
- [ ] Update `wwwroot/students.html` — add Housing Account Status column + Lifecycle History modal
- [ ] Update `wwwroot/request-details.html` — add Housing Account card
- [ ] Update `wwwroot/sidebar.js` — add Housing Account Mgmt nav item
- [ ] Update `wwwroot/dashboard.html` — add housing account stats
- [ ] Update `wwwroot/i18n.js` — add ~40 new translation keys

#### Day 6: Testing & Hardening
- [ ] Test enable/disable flow end-to-end
- [ ] Test re-provisioning flow (with force=true/false)
- [ ] Test building transfer flow (normal transfer, error rollback)
- [ ] Test config update via UI
- [ ] Test extension attribute read/write
- [ ] Test lifecycle history display on student page
- [ ] Test error scenarios (AD down, no permission, duplicate)
- [ ] Verify all audit logs
- [ ] Verify all notifications
- [ ] Run existing tests to confirm no regressions
- [ ] Code review
- [ ] Backup pre-deployment state
- [ ] Deploy to staging → validate → deploy to production

---

## 13. Rollback Strategy

### Pre-Rollback (Before Phase 2 Deployment)
1. Full backup of `D:\Projects` and `D:\Service` (use same process as Phase 1 backup)
2. Export current `ADConfigurations` seed data as SQL script
3. Document current AD OU/group paths

### During Rollback (If Phase 2 Fails)
1. Stop `NUH-PORTAL-API-Service`
2. Restore `D:\Service\wwwroot` files from backup (HTML, JS)
3. Restore `D:\Service` published binaries from backup
4. Execute rollback SQL script:
   ```sql
   DROP TABLE IF EXISTS AccountLifecycleLogs;
   DROP TABLE IF EXISTS ADConfigurations;
   ALTER TABLE Students DROP COLUMN IF EXISTS ad_status;
   ALTER TABLE Students DROP COLUMN IF EXISTS ad_last_sync_at;
   ALTER TABLE Students DROP COLUMN IF EXISTS ad_extension_attr1;
   ALTER TABLE Students DROP COLUMN IF EXISTS ad_extension_attr2;
   ```
5. Restore `D:\Projects` source code from backup (optional, for dev)
6. Start `NUH-PORTAL-API-Service`
7. Verify dashboard loads, requests display, AD provisioning still works
8. Run E2E test: create request → complete → verify AD account

### Note on Non-Rollback AD Changes
The following AD changes made during Phase 2 are **not rolled back** by the database rollback script:

| AD Change | Impact After Rollback |
|---|---|
| `extensionAttribute1` (student_id) | Harmless — readable but unused |
| `extensionAttribute2` (request_id) | Harmless — readable but unused |
| OU moves from building transfer | Account remains in new OU — manual move-back required if needed |
| Group membership changes from building transfer | Account remains in new group — manual re-assignment required if needed |
| AD enable/disable state changes | Account retains its enabled/disabled state — no DB dependency |

---

## 14. Future Cleanup Planning (Pre-Handover)

This section documents test-data cleanup tasks that must be completed **before final project handover**. No implementation is required during Phase 2; these are operational procedures.

### 14.1 Rationale

During development and UAT, the system accumulates test records that must not reach production or be handed over to the client. A structured cleanup ensures a clean baseline.

### 14.2 Cleanup Checklist

| Category | Items to Clean | Method | Responsible |
|---|---|---|---|
| **Test Students** | Students created with placeholder names (e.g., `test_*`, `dev_*`, `xxxx`) or flagged via a custom field | Delete or mark `IsDeleted = 1` via SQL or admin UI | Developer / QA |
| **Test Requests** | Requests linked to test students — includes all related status actions, notifications, and audit logs | Cascade delete or soft-delete with the parent student | Developer / QA |
| **Test AD Users** | AD accounts created for test students (prefixed `h...`) | Manually disable and delete via AD Users & Computers or `ADProvisioningService.DeleteADUserAsync()` | AD Administrator |
| **Test Notifications** | Notifications with `recipient_role = 'system'` or linked to deleted test requests | Delete from `Notifications` table | Developer / QA |
| **Test Lifecycle Logs** | `AccountLifecycleLogs` entries created for test students | Delete via SQL `DELETE FROM AccountLifecycleLogs WHERE StudentId IN (...)` | Developer / QA |
| **Test Configuration** | Any `ADConfigurations` rows added for testing (e.g., temporary OU paths) | Delete or restore to default seed values | Developer |

### 14.3 Execution Order

1. **Delete AD users** (external system — requires AD admin)
2. **Delete lifecycle logs** (no FK dependency on other cleanup)
3. **Delete notifications** linked to test students/requests
4. **Delete requests** (cascades status actions, audit logs)
5. **Delete or soft-delete students**
6. **Restore configuration defaults** — reset `ADConfigurations` to seed values
7. **Verify** — confirm no test records remain in any table

### 14.4 Verification Query

```sql
-- Check for remaining test data before handover
SELECT 'Test Students' AS Category, COUNT(*) AS Count FROM Students 
  WHERE full_name LIKE 'test%' OR full_name LIKE 'dev%' OR IsDeleted = 0 AND student_id LIKE '0%'
UNION ALL
SELECT 'Test Requests', COUNT(*) FROM Requests WHERE student_id IN 
  (SELECT Id FROM Students WHERE full_name LIKE 'test%' OR full_name LIKE 'dev%')
UNION ALL
SELECT 'Test Lifecycle Logs', COUNT(*) FROM AccountLifecycleLogs WHERE StudentId IN 
  (SELECT Id FROM Students WHERE full_name LIKE 'test%' OR full_name LIKE 'dev%');
```

### 14.5 Notes

- Run cleanup on the **staging environment first**, then mirror the same steps on production.
- Take a **full database backup** before running any DELETE operations.
- Coordinate AD user deletion with the infrastructure team (AD admin credentials required).
- After cleanup, run the full E2E test suite to confirm the system still functions correctly with real data only.

---

## Appendix A: New Translation Keys (i18n.js)

```javascript
// Housing Account Management
'hsgManagement':           { ar: 'إدارة الحسابات السكنية', en: 'Housing Account Management' },
'hsgTotalAccounts':        { ar: 'إجمالي الحسابات', en: 'Total Accounts' },
'hsgActiveAccounts':       { ar: 'الحسابات النشطة', en: 'Active Accounts' },
'hsgDisabledAccounts':     { ar: 'الحسابات المعطلة', en: 'Disabled Accounts' },
'hsgPendingProvision':     { ar: 'بانتظار الإنشاء', en: 'Pending Provisioning' },
'hsgSearchPlaceholder':    { ar: 'بحث برقم الطالب أو اسم المستخدم...', en: 'Search by Student ID or Username...' },
'hsgFilterAll':            { ar: 'الكل', en: 'All' },
'hsgFilterActive':         { ar: 'نشط', en: 'Active' },
'hsgFilterDisabled':       { ar: 'معطل', en: 'Disabled' },
'hsgFilterFailed':         { ar: 'فاشل', en: 'Failed' },
'hsgTableStudentId':       { ar: 'الرقم الجامعي', en: 'Student ID' },
'hsgTableName':            { ar: 'الاسم', en: 'Name' },
'hsgTableUsername':        { ar: 'اسم المستخدم', en: 'Username' },
'hsgTableStatus':          { ar: 'الحالة', en: 'Status' },
'hsgTableLastSync':        { ar: 'آخر مزامنة', en: 'Last Sync' },
'hsgTableActions':         { ar: 'الإجراءات', en: 'Actions' },
'hsgTableBuilding':        { ar: 'المبنى', en: 'Building' },
'hsgStatusActive':         { ar: 'نشط', en: 'Active' },
'hsgStatusDisabled':       { ar: 'معطل', en: 'Disabled' },
'hsgStatusNotCreated':     { ar: 'غير منشأ', en: 'Not Created' },
'hsgStatusFailed':         { ar: 'فشل الإنشاء', en: 'Failed' },
'hsgBtnEnable':            { ar: 'تفعيل', en: 'Enable' },
'hsgBtnDisable':           { ar: 'تعطيل', en: 'Disable' },
'hsgBtnReProvision':       { ar: 'إعادة إنشاء', en: 'Re-provision' },
'hsgBtnTransferBuilding':  { ar: 'نقل مبنى', en: 'Transfer Building' },
'hsgBtnViewDetails':       { ar: 'عرض التفاصيل', en: 'View Details' },
'hsgBtnViewHistory':       { ar: 'عرض السجل', en: 'View History' },
'hsgConfirmDisable':       { ar: 'هل أنت متأكد من تعطيل حساب {username}؟', en: 'Are you sure you want to disable {username}?' },
'hsgConfirmEnable':        { ar: 'هل أنت متأكد من تفعيل حساب {username}؟', en: 'Are you sure you want to enable {username}?' },
'hsgConfirmTransfer':      { ar: 'هل أنت متأكد من نقل حساب {username} إلى {newBuilding}؟', en: 'Are you sure you want to transfer {username} to {newBuilding}?' },
'hsgAccountDisabled':      { ar: 'تم تعطيل الحساب بنجاح', en: 'Account disabled successfully' },
'hsgAccountEnabled':       { ar: 'تم تفعيل الحساب بنجاح', en: 'Account enabled successfully' },
'hsgReProvisionSuccess':   { ar: 'تم إعادة إنشاء الحساب بنجاح', en: 'Account re-provisioned successfully' },
'hsgTransferSuccess':      { ar: 'تم نقل الحساب إلى المبنى الجديد بنجاح', en: 'Account transferred to new building successfully' },
'hsgNoAccount':            { ar: 'لا يوجد حساب سكني', en: 'No Housing Account' },
'hsgConnectionOk':         { ar: 'الاتصال بخادم AD سليم', en: 'AD Connection OK' },
'hsgConnectionFail':       { ar: 'فشل الاتصال بخادم AD', en: 'AD Connection Failed' },
'hsgLifecycleHistory':     { ar: 'سجل دورة الحياة', en: 'Lifecycle History' },
'hsgActionCreated':        { ar: 'إنشاء حساب', en: 'Account Created' },
'hsgActionDisabled':       { ar: 'تعطيل', en: 'Disabled' },
'hsgActionEnabled':        { ar: 'تفعيل', en: 'Enabled' },
'hsgActionReProvisioned':  { ar: 'إعادة إنشاء', en: 'Re-provisioned' },
'hsgActionBuildingTransferred': { ar: 'نقل مبنى', en: 'Building Transferred' },
'hsgActionDeleted':        { ar: 'حذف', en: 'Deleted' },
'hsgPerformedBy':          { ar: 'بواسطة', en: 'Performed by' },
'hsgResultSuccess':        { ar: 'نجاح', en: 'Success' },
'hsgResultFailed':         { ar: 'فشل', en: 'Failed' },
'hsgResultSkipped':        { ar: 'تخطي', en: 'Skipped' },

// Transfer Reason (Building Transfer)
'hsgTransferReason':       { ar: 'سبب النقل', en: 'Transfer Reason' },
'hsgTransferStudentReq':   { ar: 'طلب الطالب', en: 'Student Request' },
'hsgTransferMaintenance':  { ar: 'صيانة', en: 'Maintenance' },
'hsgTransferRoomChange':   { ar: 'تغيير غرفة', en: 'Room Change' },
'hsgTransferAdminDecision':{ ar: 'قرار إداري', en: 'Administrative Decision' },
'hsgTransferDisciplinary': { ar: 'إجراء تأديبي', en: 'Disciplinary Action' },
'hsgTransferOther':        { ar: 'أخرى', en: 'Other' },

// Days Disabled
'hsgDaysDisabled':         { ar: 'أيام التعطيل', en: 'Days Disabled' },
'hsgDaysDisabledFormat':   { ar: '{days} أيام', en: '{days} Days' },

// Disable Confirmation Dialog
'hsgConfirmDisableTitle':  { ar: 'تعطيل حساب الشبكة السكنية', en: 'Disable Housing Network Account' },
'hsgConfirmDisablePrompt': { ar: 'هل أنت متأكد من تعطيل حساب الشبكة السكنية؟', en: 'Are you sure you want to disable this Housing Network Account?' },
'hsgLblStudentId':         { ar: 'الرقم الجامعي', en: 'Student ID' },
'hsgLblStudentName':       { ar: 'اسم الطالب', en: 'Student Name' },
'hsgLblAdUsername':        { ar: 'اسم المستخدم', en: 'AD Username' },
'hsgLblDisableReason':     { ar: 'سبب التعطيل', en: 'Disable Reason' },
'hsgBtnConfirmDisable':    { ar: 'تأكيد التعطيل', en: 'Confirm Disable' },

// Lifecycle History Colors (used as CSS class references in i18n context)
'hsgHistoryCreated':       { ar: 'إنشاء حساب', en: 'Account Created' },
'hsgHistoryDisabled':      { ar: 'تعطيل', en: 'Disabled' },
'hsgHistoryEnabled':       { ar: 'تفعيل', en: 'Enabled' },
'hsgHistoryBuildingTransfer': { ar: 'نقل مبنى', en: 'Building Transferred' },
```

## Appendix B: AD Account Lifecycle State Diagram

```
                    ┌─────────────┐
                    │ Not Created │
                    │ (no acct)   │
                    └──────┬──────┘
                           │
                    Request completed
                    ADProvisioningService
                           │
                           ▼
                    ┌─────────────┐
              ┌────→│   Active    │←────┐
              │     │ (enabled)   │     │
              │     └──┬──────┬───┘     │
              │        │      │         │
       Admin/superv    │      │  Admin/supervisor
       "Disable"       │      │  "Enable"
              │        │      │         │
              │        ▼      ▼         │
              │  ┌──────────────┐       │
              │  │  Disabled    │       │
              │  └──────┬───────┘       │
              │         │               │
              │  Admin/supervisor       │
              │  "Re-provision"         │
              │         │               │
              │         ▼               │
              │  ┌──────────────┐       │
              │  │   Active     │       │
              │  │ (recreated)  │       │
              │  └──────────────┘       │
              │                         │
              │  Admin/supervisor       │
              │  "Transfer Building"    │
              │         │               │
              │         ▼               │
              │  ┌──────────────┐       │
              └──│   Active     │───────┘
                 │ (new OU/grp) │
                 └──────────────┘

Additional transitions:
  Active  → Failed   (Re-provision with force=true, delete + recreate)
  Failed  → Active   (Re-provision, successful)
  Any     → Deleted  (admin clicks Delete — removes AD account, ad_username set to null)
  Active  → Active   (Building Transfer — account remains active but moves to new OU/group)

Note: All state transitions are manual (admin/supervisor action).
No automatic status-driven transitions occur.
```

---

*End of Phase 2 Design Document*
*Date: 2026-07-13*
*Status: Design Only — No Implementation Changes Applied*
