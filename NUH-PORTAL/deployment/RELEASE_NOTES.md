# Phase 3 Release Notes — Student Self-Service Registration Portal

**Version:** 3.0.0
**Release Date:** 2026-07-15
**Status:** Ready for Pre-Production Deployment

---

## Overview

Phase 3 introduces a Student Self-Service Registration Portal that enables students to register for housing accommodation through a guided, multi-stage approval workflow. The system replaces the manual/admin-only registration process with a secure, auditable student-initiated flow.

## New Features

### OTP-Based Mobile Verification
- SMS-based OTP (6-digit) verification system
- Crypto-random OTP generation with 5-minute TTL
- Rate-limited OTP sending (max attempts, time window)
- Audit trail for all OTP operations

### Registration Workflow
- Self-service registration request submission with student data
- Pre-filled student information from Students table
- JSON-based flexible registration data storage
- Unique request number generation (NUH-YYYY-NNNNNN)

### Multi-Stage Approval Pipeline
| Stage | Role | Action |
|-------|------|--------|
| 1. Pending Supervisor | osamasuper / bakry | First review & approval |
| 2. Pending Cyber | osamacyber | Security review |
| 3. Pending Admin | admin | Final approval |
| 4. Approved | — | Request completed |
| Rejected | Any stage | Request rejected with notes |
| Need More Info | Supervisor | Return to student for updates |
| Resubmitted | Student | Re-enters queue after updates |

### Request Tracking
- My Requests dashboard (admin sees all, users see own)
- Request detail view with full history timeline
- Status: pending_supervisor → pending_cyber → pending_admin → approved/rejected

### Workflow Queue Management
- Role-filtered queue view
- Queue counts endpoint for dashboard badges
- Approve, Reject, Request More Info actions
- Full workflow history with actor tracking

### New Database Tables
| Table | Purpose |
|-------|---------|
| `OTPVerifications` | OTP code storage with hash, expiry, attempt tracking |
| `SMSLogs` | SMS message audit log |
| `StudentDeclarations` | Declaration acceptance records |
| `WorkflowHistory` | Immutable workflow state transitions |
| `RequestAttachments` | File attachments for requests |

### New API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/Otp/send` | Send OTP code |
| POST | `/api/Otp/verify` | Verify OTP code |
| POST | `/api/Registration/start` | Create registration request |
| GET | `/api/Registration/my-requests` | List user requests |
| GET | `/api/Registration/my-requests/{id}` | Request detail |
| POST | `/api/Registration/{id}/declarations` | Accept declarations |
| POST | `/api/Registration/{id}/resubmit` | Resubmit after info request |
| GET | `/api/Workflow/queue` | Get workflow queue |
| GET | `/api/Workflow/queue/counts` | Queue status counts |
| POST | `/api/Workflow/{id}/approve` | Approve request |
| POST | `/api/Workflow/{id}/reject` | Reject request |
| POST | `/api/Workflow/{id}/request-info` | Request more info |
| POST | `/api/Attachment/upload` | Upload attachment |
| GET | `/api/Attachment/{id}/list` | List attachments |
| POST | `/api/Auth/Ping` | Session keepalive |

## Bug Fixes
- **WorkflowController role case mismatch**: [Authorize] and role-switch blocks used TitleCase but JWT generates lowercase roles. Fixed all 4 locations.

## Security

- OTP codes hashed (SHA-256) with per-request salt — never stored in plaintext
- Rate limiting on login endpoint (20 req/min)
- JWT with 15-minute expiry
- Session activity middleware (15-min inactivity timeout)
- Role-based access control on all workflow endpoints
- CORS restricted to production origin
- IP rate limiting with X-Real-IP header support

## Dependencies

- .NET 8.0 runtime required
- SQL Server 2019+ (existing NUH_DB)
- Active Directory (LDAPS on port 636)
- SMS provider (production: replace LogOnly provider)

## Upgrade Impact

### Schema Changes
- 5 new tables (zero existing table structure changes)
- 2 new columns on `Requests` table (`registration_data`, `request_number`)
- 1 new column on `Requests` table (`bulk_request_id`)
- 1 dropped CHECK constraint on `Requests.request_type`
- VARCHAR → NVARCHAR conversion on `Students` and `BulkRequestStudents` (idempotent)

### Data Impact
- No existing data migration required
- Existing requests continue to work
- Existing users, roles, permissions unchanged

## Known Issues

1. **Attachment Upload**: Kestrel multipart form line length limit (100 chars) may reject large attachments. Configure `FormOptions.MultipartBodyLengthLimit` in Program.cs if needed.
2. **Audit Action `attachment_uploaded`**: Not implemented in attachment controller (minor).
3. **SMS Provider**: Default 'LogOnly' provider logs to DB but does not send real SMS. Production deployment requires a registered SMS provider.

## Test Summary

All 12 UAT scenarios passed:
1. Login (3 roles) ✅
2. OTP Flow (send/verify/invalid) ✅
3. Registration + Declarations ✅
4. Request Tracking ✅
5. Workflow Queue ✅
6. Supervisor Approval ✅
7. Cyber Approval ✅
8. Admin Final Approval ✅
9. Need More Info ✅
10. Resubmission ✅
11. Duplicate Prevention ✅
12. Rejection ✅

---

**Prepared by:** NUH Portal Team
**Approved by:** [Project Manager]
