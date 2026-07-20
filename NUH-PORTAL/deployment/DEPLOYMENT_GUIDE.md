# Phase 3 Deployment Guide

## Overview
Deploying Phase 3 — Student Self-Service Registration Portal with multi-stage approval workflow.

## Package Contents

| Artifact | Path | Description |
|----------|------|-------------|
| Build | `deployment/build/` | Compiled application binaries (Release) |
| Full Migration | `deployment/migrations/Phase3_FullMigration.sql` | Idempotent full DB migration (all 10 migrations) |
| Upgrade Migration | `deployment/migrations/Phase2_to_Phase3_Upgrade.sql` | Phase 2 → Phase 3 upgrade only |
| Sprint 2 Update | `deployment/migrations/Phase3_Sprint2_Update.sql` | Incremental Phase 3 update (RequestAttachments table) |
| Startup Migrations | `deployment/migrations/Phase3_Startup_Migrations.sql` | Runtime migrations from Program.cs (idempotent) |
| Rollback | `deployment/rollback/Phase3_Rollback.sql` | Full rollback script |
| UAT Report | `AGENTS.md` | UAT test results |
| Release Notes | `deployment/RELEASE_NOTES.md` | Phase 3 release notes |

## Pre-Deployment Checklist

- [ ] Take database full backup
- [ ] Note current DB schema version (check `__EFMigrationsHistory` table)
- [ ] Verify `appsettings.json` connection string does NOT contain `#{DB_PASSWORD}#` (should be replaced with real password)
- [ ] Verify Jwt:Key is a strong 32+ character secret (NOT the placeholder)
- [ ] Verify ADServiceAccount:Password is correct
- [ ] Send maintenance notification to users
- [ ] Disable incoming traffic (if using load balancer)

## Deployment Steps

### 1. Deploy Database Changes

Option A — Automated (recommended):
- The application applies startup migrations automatically (Program.cs:186-220)
- EF Core migrations run on first request when database is not up-to-date

Option B — Manual (for air-gapped or scheduled maintenance):
```sql
:r deployment/migrations/Phase2_to_Phase3_Upgrade.sql
:r deployment/migrations/Phase3_Startup_Migrations.sql
```

### 2. Deploy Application Binaries

1. Backup existing deployment directory
2. Copy contents of `deployment/build/` to the production IIS application folder
3. Restore `appsettings.json` with production values (see Configuration section)
4. Set proper file permissions (IIS_IUSRS read/execute)
5. Configure IIS application pool (verify .NET CLR version = "No Managed Code")

### 3. Configure Environment Variables (preferred over appsettings.json)

```powershell
# On the deployment server, set these environment variables:
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=housing.globalgroups.com,1433;Database=NUH_DB;User Id=svc.sql;Password=REAL_PASSWORD;TrustServerCertificate=True;", "Machine")
[Environment]::SetEnvironmentVariable("Jwt__Key", "<strong-32-char-secret>", "Machine")
[Environment]::SetEnvironmentVariable("ADServiceAccount__Password", "<real-password>", "Machine")
```

Or use a production-specific `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=NUH_DB;User Id=svc.sql;Password=REAL_PASSWORD;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "<strong-32-char-secret>",
    "Issuer": "NUH-PORTAL",
    "Audience": "NUH-PORTAL-USERS"
  },
  "ADServiceAccount": {
    "Username": "nuh.portal",
    "Password": "<real-password>"
  },
  "AllowedOrigin": "https://housing.nu.edu.sa"
}
```

### 4. Verify Deployment

1. Check health endpoint: `GET /api/Health` → `{"status":"Healthy"}`
2. Verify login works for all roles: admin, supervisor, cyber
3. Verify OTP send/verify endpoints
4. Verify workflow queue loads for supervisor

## Configuration

| Key | Production Value | Notes |
|-----|-----------------|-------|
| `ConnectionStrings:DefaultConnection` | Database connection string | Replace `#{DB_PASSWORD}#` |
| `Jwt:Key` | 32+ char random secret | Must match across all app instances |
| `Jwt:Issuer` | `NUH-PORTAL` | Keep as-is |
| `Jwt:Audience` | `NUH-PORTAL-USERS` | Keep as-is |
| `AllowedOrigin` | `https://housing.nu.edu.sa` | Frontend URL |
| `ADServiceAccount:Username` | `nuh.portal` | AD service account |
| `ADServiceAccount:Password` | Production AD password | Store securely |
| `ActiveDirectory:RoleMappings` | AD group → app role mappings | Verify groups exist in AD |

## Rollback Procedure

If issues are detected, run the rollback:

1. Run `deployment/rollback/Phase3_Rollback.sql` against the database
2. Restore previous application build from backup
3. Verify rollback: all Phase 3 tables dropped, Requests columns reverted
4. Notify users of rollback

## Post-Deployment Verification

- [ ] Health endpoint returns 200
- [ ] Login works for all 3 roles
- [ ] OTP send + verify flow functional
- [ ] Registration request creation works
- [ ] Workflow queue shows pending requests
- [ ] Approve/Reject flow works
- [ ] Audit logs are being written
- [ ] Swagger UI accessible (if dev mode)
- [ ] Phase 3 KPI counters visible on dashboard
