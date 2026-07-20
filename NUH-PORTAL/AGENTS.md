# NUH Portal - Project Notes

## Phase 3 UAT Results

Tested: 2026-07-15

### API Base URL
- Local dev: `http://localhost:5058` (from launchSettings.json)
- Test users: admin, osamasuper, osamacyber (password: Test@123)

### Bugs Found & Fixed

1. **WorkflowController role case mismatch** (WorkflowController.cs):
   - `[Authorize(Roles = "Admin,Supervisor,Cyber")]` used TitleCase but JWT always uses lowercase (`ClaimTypes.Role` with `.ToLowerInvariant()` in AuthController.cs:355)
   - Fixed: Changed to `[Authorize(Roles = "admin,supervisor,cyber")]`
   - Same issue in role-switch blocks (approve/reject/queue default stage) - fixed all to lowercase
   - All other controllers already use lowercase roles

### UAT Test Results

| # | Test | Result | Notes |
|---|------|--------|-------|
| 1 | Login (3 roles) | ✅ PASS | admin/Admin, osamasuper/Supervisor, osamacyper/Cyber |
| 2 | OTP Flow (send/verify) | ✅ PASS | Send returns 200 with expiresAt; verify with valid code returns 200, invalid returns 400 |
| 3 | Registration + Declarations | ✅ PASS | self_registration type; student lookup from Students table; declarations recorded |
| 4 | Request Tracking | ✅ PASS | my-requests lists all self_registration; detail view shows full history |
| 5 | Workflow Queue | ✅ PASS | Queue filtered by stage; counts endpoint shows grouped statuses |
| 6 | Supervisor Approval | ✅ PASS | pending_supervisor → pending_cyber; logged in AuditLogs |
| 7 | Cyber Approval | ✅ PASS | pending_cyber → pending_admin; logged in AuditLogs |
| 8 | Admin Final Approval | ✅ PASS | pending_admin → approved; full history with 3 approvals recorded |
| 9 | Need More Info | ✅ PASS | pending_supervisor → need_more_info; supervisor triggers request-info |
| 10 | Resubmission | ✅ PASS | need_more_info → pending_supervisor; data updated |
| 11 | Duplicate Prevention | ✅ PASS | Same student with pending request: BLOCKED; Same mobile with pending request: BLOCKED |
| 12 | Rejection | ✅ PASS | pending_supervisor → rejected; full flow works |

### Audit Log Verification
All expected audit actions recorded in AuditLogs table with correct user_ids.

### Fixed: AD Provisioning (2026-07-18)

**Root cause**: Config values contradicted code's `$"{Username}@{Domain}"` pattern.

Code at `ActiveDirectoryService.cs:584`:
```csharp
var userPrincipal = $"{_serviceAccount.Username}@{_config.Domain}";
```

**Broken config** (after first fix attempt):
- `ADServiceAccount.Username = "globalgroups\nuh.portal"` (down-level format)
- `ActiveDirectory.Domain = "globalgroups"` (NetBIOS)
- Constructed: `globalgroups\nuh.portal@globalgroups` — **INVALID**

**Fixed config**:
- `ADServiceAccount.Username = "nuh.portal"` (bare SAM name)
- `ActiveDirectory.Domain = "globalgroups.com"` (FQDN for UPN suffix)
- Constructed: `nuh.portal@globalgroups.com` — **VALID**

**Validation**: Request 96 → completed ✅; AD account `h3698521478` created with UAC=512 (enabled), correct OU (`Male>New>Students`), group membership (`NUH-Student-B`). See `D:\Service\NUH-PORTAL\reports\REPORT-*.md`.

### Fixed: Phase 3 Final Alignment (2026-07-18)

#### 1. Request Number Format
- Changed from `NUH-YYYY-NNNNNN` to `YYYY-NNNNNN` (e.g. `2026-000001`)
- Sequence resets per year automatically
- Removed `REQ-*` and `NUH-*` fallbacks from all frontend/backend
- **File**: `RegistrationService.cs:21-40`, `RequestsController.cs:140,287`, `request-details.html`, `requests.html`

#### 2. Workflow Terminology
Updated all visible UI labels for `pending_supervisor`, `pending_cyber`, `pending_admin`:
- `pending_supervisor` → `موافقة إدارة الإسكان` / `Housing Approval`
- `pending_cyber` → `مراجعة إدارة الأمن السيبراني` / `Cyber Review`
- `pending_admin` → `جاهز لإنشاء حساب شبكة السكن` / `Ready For Housing Network Account Creation`
- `ready_for_provisioning` → `جاهز لإنشاء حساب شبكة السكن`
- `completed` → `مكتمل`
- **Files**: `workflow-queue.html`, `my-requests.html`, `request-review.html`, `track-request.html`, `request-details.html`

#### 3. Review History - Student Name
- History now shows actual student name instead of `طالب` for user-role actors
- **Files**: `WorkflowController.cs:168`, `RegistrationController.cs:205`

#### 4. Housing Account Management - Admin Only
- `[Authorize(Roles = "admin,supervisor")]` → `[Authorize(Roles = "admin")]` in `HousingAccountManagementController.cs:11`
- Sidebar hides Housing Management for supervisor role in `sidebar.js:40`
- Action buttons (Enable/Disable/Reset Password) hidden for non-admin in `request-details.html:688-690`

#### 5. Notifications for Self-Registration
- All workflow steps now create Notifications (previously: NONE for self-registration):
  - Submission → `supervisor` notified
  - Supervisor approval → `cyber` notified
  - Cyber approval → `admin` notified
  - Admin approval (completion) → `admin` notified
  - Rejections → `admin` notified
- **File**: `RegistrationService.cs` (all approval/rejection methods)

#### 6. AD Provisioning for Self-Registration
- Self-registration workflow changed: `pending_admin` → `ready_for_provisioning` → AD created → `completed`
- Previously: `pending_admin` → `approved` (NO AD creation)
- `ADProvisioningService` injected into `RegistrationService`
- **File**: `RegistrationService.cs:240-297`, `Program.cs:163-166`

#### 7. Lifecycle History
- AD provisioning writes lifecycle records (`AccountLifecycleLogs`) for self-registration
- API endpoint `/api/students/{id}/lifecycle` returns data wrapped as `{ logs: [...] }`
- ✅ Verified: 2 lifecycle records (provisioned + extension_attrs_synced) written on completion

### Open Issues
1. **Attachment upload** - Kestrel form line length limit (100 chars) needs configuration in Program.cs
2. **OTP code extraction** - SMS parsing from DB works but Arabic rendering in console is garbled (cosmetic)
3. No `attachment_uploaded` audit action in code (minor)

### Key Implementation Details
- RegistrationService handles all workflow business logic (RegistrationService.cs)
- WorkflowController routes based on role (WorkflowController.cs)
- OTP codes are 6 digits, stored in SmsLogs table
- Audit logs use AuditLogs table with action, user_id, action_at columns
- Duplicate checks only apply to active statuses (pending_supervisor, pending_cyber, pending_admin, need_more_info)
- Database: NUH_DB on housing.globalgroups.com,1433
- **AD provisioning**: Code at `ActiveDirectoryService.cs:584` constructs UPN as `{Username}@{Domain}` — so `ADServiceAccount.Username` must be bare SAM (e.g. `nuh.portal`), `ActiveDirectory.Domain` must be FQDN (e.g. `globalgroups.com`). Group membership and OU assignment are based on student attributes (gender, department). Three `ready_for_provisioning` requests available for testing: id=96 (done), 105, 141.
- **Self-registration workflow**: `pending_supervisor` → `pending_cyber` → `pending_admin` → `ready_for_provisioning` → `completed` (with AD provisioning at final step)
- **Request number format**: `YYYY-NNNNNN` (prefix-less, resets yearly)
- **Notifications**: Self-registration now creates Notification records at every workflow transition (all roles)
