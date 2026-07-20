# Phase 3 UAT Summary Report

**Test Date:** 2026-07-15
**Environment:** Development (localhost:5058)
**Database:** NUH_DB (housing.globalgroups.com,1433)
**Tester:** Automated UAT Suite

---

## Test Environment

| Component | Version/Config |
|-----------|---------------|
| Application | .NET 8.0, ASP.NET Core 8.0 |
| Database | SQL Server 2019+ on housing.globalgroups.com,1433 |
| Authentication | JWT with BCrypt password hashing |
| Active Directory | LDAPS on DC-01.globalgroups.com:636 |
| Target OS | Windows Server (IIS/Windows Service) |

## Test Users

| Username | Role | Password |
|----------|------|----------|
| admin | Admin | Test@123 |
| osamasuper | Supervisor | Test@123 |
| osamacyber | Cyber | Test@123 |

## Test Results

| # | Test Case | Steps | Expected | Actual | Result |
|---|-----------|-------|----------|--------|--------|
| 1 | Login (3 roles) | POST /api/Auth/Login with each user | 200 + JWT token | 200 + valid JWT with correct role claim | ✅ PASS |
| 2 | OTP Send | POST /api/Otp/send { mobile } | 200 + expiresAt | 200 with expiresAt timestamp | ✅ PASS |
| 2b | OTP Verify (valid) | POST /api/Otp/verify { mobile, code } | 200 success | 200 verified | ✅ PASS |
| 2c | OTP Verify (invalid) | POST /api/Otp/verify { mobile, code: "000000" } | 400 error | 400 invalid code | ✅ PASS |
| 3 | Registration Create | POST /api/Registration/start { studentId, registrationData } | 200 + requestId | 200 with requestId and requestNumber | ✅ PASS |
| 3b | Declarations | POST /api/Registration/{id}/declarations | 200 accepted | 200 accepted | ✅ PASS |
| 4 | My Requests List | GET /api/Registration/my-requests | 200 + array | 200 with all self_registration requests | ✅ PASS |
| 4b | Request Detail | GET /api/Registration/my-requests/{id} | 200 + detail | 200 with status, student, history | ✅ PASS |
| 5 | Workflow Queue | GET /api/Workflow/queue?stage=pending_supervisor | 200 + list | 200 with pending requests | ✅ PASS |
| 5b | Queue Counts | GET /api/Workflow/queue/counts | 200 + grouped counts | 200 with status groupings | ✅ PASS |
| 6 | Supervisor Approve | POST /api/Workflow/{id}/approve (as supervisor) | 200 → status pending_cyber | 200, status changed to pending_cyber | ✅ PASS |
| 7 | Cyber Approve | POST /api/Workflow/{id}/approve (as cyber) | 200 → status pending_admin | 200, status changed to pending_admin | ✅ PASS |
| 8 | Admin Final Approve | POST /api/Workflow/{id}/approve (as admin) | 200 → status approved | 200, status changed to approved | ✅ PASS |
| 9 | Need More Info | POST /api/Workflow/{id}/request-info (as supervisor) | 200 → status need_more_info | 200, status changed to need_more_info | ✅ PASS |
| 10 | Resubmission | POST /api/Registration/{id}/resubmit (as student) | 200 → status pending_supervisor | 200, status back to pending_supervisor | ✅ PASS |
| 11a | Duplicate Prevention (same student) | POST /api/Registration/start with same student_id | 400 blocked | 400 "already has pending request" | ✅ PASS |
| 11b | Duplicate Prevention (same mobile) | POST /api/Registration/start with same mobile | 400 blocked | 400 "mobile already in use" | ✅ PASS |
| 12 | Rejection | POST /api/Workflow/{id}/reject (as supervisor) | 200 → status rejected | 200, status changed to rejected | ✅ PASS |

## Audit Log Verification

All expected Phase 3 audit actions recorded in AuditLogs:

| Action | Count |
|--------|-------|
| declaration_accepted | 5 |
| info_requested | 1 |
| otp_sent | 6 |
| otp_verified | 2 |
| registration_created | 7 |
| request_resubmitted | 1 |
| workflow_approved | 3 |
| workflow_rejected | 1 |

## Bug Found & Fixed During UAT

**WorkflowController Role Case Mismatch**
- JWT generates role claims in lowercase (`"supervisor"`, `"cyber"`, `"admin"`)
- WorkflowController had TitleCase in `[Authorize(Roles = "Admin,Supervisor,Cyber")]` and switch statements
- All 4 locations fixed to lowercase
- All other controllers already used lowercase roles ✓

## Overall Assessment

**12/12 tests PASSED** — All Phase 3 workflows are functional end-to-end.

Phase 3 is ready for pre-production deployment pending:
1. Production configuration (secrets, connection strings)
2. SMS provider configuration
3. Kestrel multipart form limits adjustment (for attachments)

---

**Tested by:** Automated UAT Suite
**Date:** 2026-07-15
