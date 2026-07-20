using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Services;

namespace NUH_PORTAL.Controllers
{
    [Route("api/ad-setup")]
    [ApiController]
    [Authorize]
    public class ADSetupController : ControllerBase
    {
        private readonly ActiveDirectoryService _adService;
        private readonly AppDbContext _db;
        private readonly ILogger<ADSetupController> _logger;

        public ADSetupController(ActiveDirectoryService adService, AppDbContext db, ILogger<ADSetupController> logger)
        {
            _adService = adService;
            _db = db;
            _logger = logger;
        }

        [HttpGet("readiness")]
        public async Task<ActionResult> GetReadiness()
        {
            var result = new ADReadinessReport
            {
                Timestamp = DateTime.UtcNow
            };

            result.ADConnectivity = await _adService.CheckHealthAsync();

            if (result.ADConnectivity.IsReachable)
            {
                result.ServiceAccount = await _adService.ValidateServiceAccountBindAsync();

                if (result.ServiceAccount?.BindSuccessful == true)
                {
                    result.OUs = new[]
                    {
                        await ValidateOUAsync("OU=Male,OU=New,OU=Students,DC=globalgroups,DC=com"),
                        await ValidateOUAsync("OU=Female,OU=New,OU=Students,DC=globalgroups,DC=com")
                    };

                    result.Groups = new[]
                    {
                        await ValidateGroupAsync("CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com"),
                        await ValidateGroupAsync("CN=NUH-Student-G,OU=Groups,DC=globalgroups,DC=com")
                    };

                    result.PasswordPolicy = await _adService.GetPasswordPolicyAsync();
                }
            }

            return Ok(result);
        }

        [HttpGet("dry-run/{studentId}")]
        public async Task<ActionResult> GetDryRun(string studentId)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.student_id == studentId && !s.IsDeleted);
            if (student == null)
                return NotFound(new { error = "Student not found", student_id = studentId });

            if (string.IsNullOrEmpty(student.gender))
                return BadRequest(new { error = "Student has no gender set. Cannot determine target OU." });

            var isMale = student.gender.ToLower() == "male";
            var sAMAccountName = "h" + student.student_id;

            var nameParts = (student.full_name_english ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var givenName = nameParts.Length > 0 ? nameParts[0] : sAMAccountName;
            var sn = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : givenName;

            var targetOu = isMale
                ? "OU=Male,OU=New,OU=Students,DC=globalgroups,DC=com"
                : "OU=Female,OU=New,OU=Students,DC=globalgroups,DC=com";

            var targetGroup = isMale
                ? "CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com"
                : "CN=NUH-Student-G,OU=Groups,DC=globalgroups,DC=com";

            var tempPassword = "NUH@" + student.student_id;

            var simulation = new ADDryRunResult
            {
                Student = new
                {
                    student.student_id,
                    student.full_name,
                    student.full_name_english,
                    student.gender,
                    student.college,
                    student.department,
                    student.national_id,
                    student.phone,
                    student.housing_building,
                    student.academic_level
                },
                ProposedAccount = new
                {
                    sAMAccountName,
                    userPrincipalName = $"{sAMAccountName}@globalgroups.com",
                    displayName = student.full_name_english ?? sAMAccountName,
                    givenName = givenName,
                    sn = sn,
                    ou = targetOu,
                    group = targetGroup,
                    extensionAttribute1 = student.student_id,
                    extensionAttribute2 = student.national_id,
                    department = student.college ?? student.department ?? "",
                    title = "Student",
                    physicalDeliveryOfficeName = string.IsNullOrEmpty(student.housing_building)
                        ? ""
                        : $"Building {student.housing_building}",
                    description = $"Housing Student - {student.college ?? "N/A"} - Level {student.academic_level ?? "N/A"}"
                },
                TempPassword = new
                {
                    value = tempPassword,
                    length = tempPassword.Length,
                    requiresChange = true
                },
                LdapPath = $"CN={sAMAccountName},{targetOu}",
                PasswordPolicyCheck = await _adService.ValidatePasswordCompatibilityAsync(tempPassword)
            };

            return Ok(simulation);
        }

        [HttpPost("test-user")]
        public async Task<ActionResult> PostTestUser([FromBody] ADTestUserRequest request)
        {
            var report = new ADTestUserReport
            {
                RequestedSamAccountName = request.StudentId?.StartsWith("h") == true
                    ? request.StudentId
                    : "h" + request.StudentId,
                TargetOu = request.TargetOu ?? "OU=Male,OU=New,OU=Students,DC=globalgroups,DC=com",
                TargetGroup = request.TargetGroup ?? "CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com",
                Timestamp = DateTime.UtcNow
            };

            var sAMAccountName = report.RequestedSamAccountName;
            var password = $"NUH@{request.StudentId}";
            var targetOu = report.TargetOu;
            var targetGroup = report.TargetGroup;
            var userDn = $"CN={sAMAccountName},{targetOu}";

            // Step 0: Check if user already exists
            report.Steps.Add(new ADTestStep
            {
                Step = "PreCheck",
                Description = $"Check if user '{sAMAccountName}' already exists",
                Status = "Running"
            });

            var existingUser = await _adService.GetUserBySamAccountNameAsync(sAMAccountName);
            if (existingUser.Success)
            {
                report.OverallSuccess = false;
                report.Steps[0].Status = "Skipped";
                report.Steps[0].Details = $"User '{sAMAccountName}' already exists in AD. Delete it first or use a different test ID.";
                report.Summary = "ABORTED: User already exists";
                return Ok(report);
            }
            report.Steps[0].Status = "Pass";
            report.Steps[0].Details = $"User '{sAMAccountName}' does not exist — ready to create";

            // Step 1: Create user
            report.Steps.Add(new ADTestStep
            {
                Step = "CreateUser",
                Description = $"Create user '{sAMAccountName}' in OU '{targetOu}'",
                Status = "Running"
            });

            var createRequest = new ADCreateUserRequest
            {
                SamAccountName = sAMAccountName,
                UserPrincipalName = $"{sAMAccountName}@globalgroups.com",
                DisplayName = sAMAccountName,
                GivenName = "Test",
                Surname = "Account",
                TargetOu = targetOu,
                Description = "AD Pre-Production Test Account - Created " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                UserAccountControl = 544
            };

            var createResult = await _adService.CreateADUserAsync(createRequest);
            if (!createResult.Success)
            {
                report.OverallSuccess = false;
                report.Steps.Last().Status = "Fail";
                report.Steps.Last().Error = createResult.Error;
                report.Steps.Last().Details = createResult.ErrorDetails;
                report.Summary = "FAILED at CreateUser";
                return Ok(report);
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Created at DN: {createResult.DistinguishedName}";

            // Step 2: Set password
            report.Steps.Add(new ADTestStep
            {
                Step = "SetPassword",
                Description = "Set password for the test account",
                Status = "Running"
            });

            var passwordResult = await _adService.SetUserPasswordAsync(userDn, password);
            if (!passwordResult.Success)
            {
                report.OverallSuccess = false;
                report.Steps.Last().Status = "Fail";
                report.Steps.Last().Error = passwordResult.Error;
                report.Steps.Last().Details = passwordResult.ErrorDetails;
                report.Summary = "FAILED at SetPassword — cleaning up created user";
                await TryCleanupUser(userDn, sAMAccountName);
                return Ok(report);
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Password set successfully (length: {password.Length})";

            // Step 3: Enable account (remove ACCOUNTDISABLE flag)
            report.Steps.Add(new ADTestStep
            {
                Step = "EnableAccount",
                Description = "Remove ACCOUNTDISABLE flag from userAccountControl",
                Status = "Running"
            });

            var enableResult = await _adService.ModifyUserAccountControlAsync(userDn, 66048);
            if (!enableResult.Success)
            {
                report.OverallSuccess = false;
                report.Steps.Last().Status = "Fail";
                report.Steps.Last().Error = enableResult.Error;
                report.Steps.Last().Details = enableResult.ErrorDetails;
                report.Summary = "FAILED at EnableAccount — cleaning up created user";
                await TryCleanupUser(userDn, sAMAccountName);
                return Ok(report);
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "Account enabled (userAccountControl = 66048)";

            // Step 4: Add to group
            report.Steps.Add(new ADTestStep
            {
                Step = "AddToGroup",
                Description = $"Add user to group '{targetGroup}'",
                Status = "Running"
            });

            var groupResult = await _adService.AddUserToGroupAsync(userDn, targetGroup);
            if (!groupResult.Success)
            {
                report.OverallSuccess = false;
                report.Steps.Last().Status = "Fail";
                report.Steps.Last().Error = groupResult.Error;
                report.Steps.Last().Details = groupResult.ErrorDetails;
                report.Summary = "FAILED at AddToGroup — cleaning up created user";
                await TryCleanupUser(userDn, sAMAccountName);
                return Ok(report);
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Added as member of '{targetGroup}'";

            // Step 5: Read user attributes back
            report.Steps.Add(new ADTestStep
            {
                Step = "ReadUser",
                Description = "Read user attributes from AD for verification",
                Status = "Running"
            });

            var readResult = await _adService.GetUserBySamAccountNameAsync(sAMAccountName);
            if (!readResult.Success)
            {
                report.OverallSuccess = false;
                report.Steps.Last().Status = "Fail";
                report.Steps.Last().Error = readResult.Error;
                report.Summary = "FAILED at ReadUser — cleaning up created user";
                await TryCleanupUser(userDn, sAMAccountName);
                return Ok(report);
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "User attributes read successfully";
            report.ReadUserResult = readResult;

            // Step 6: Delete user
            report.Steps.Add(new ADTestStep
            {
                Step = "DeleteUser",
                Description = "Delete the test user from AD",
                Status = "Running"
            });

            var deleteResult = await _adService.DeleteADUserAsync(userDn);
            if (!deleteResult.Success)
            {
                report.OverallSuccess = false;
                report.Steps.Last().Status = "Fail";
                report.Steps.Last().Error = deleteResult.Error;
                report.Steps.Last().Details = deleteResult.ErrorDetails;
                report.Summary = "FAILED at DeleteUser — manual cleanup required";
                return Ok(report);
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "User deleted from AD";

            // All steps passed
            report.OverallSuccess = true;
            report.Summary = "ALL TESTS PASSED — AD account creation pipeline verified end-to-end";
            return Ok(report);
        }

        [NonAction]
        private async Task TryCleanupUser(string userDn, string samAccountName)
        {
            try
            {
                var deleteResult = await _adService.DeleteADUserAsync(userDn);
                if (deleteResult.Success)
                    _logger.LogWarning("Cleanup: deleted partially-created user {SamAccountName}", samAccountName);
                else
                    _logger.LogWarning("Cleanup FAILED to delete user {SamAccountName}: {Error}", samAccountName, deleteResult.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup threw exception for user {SamAccountName}", samAccountName);
            }
        }

        private async Task<ADObjectValidation> ValidateOUAsync(string ouDn)
        {
            var result = await _adService.SearchObjectByDnAsync(ouDn, "organizationalUnit");
            return new ADObjectValidation
            {
                Dn = ouDn,
                Exists = result.exists,
                Error = result.error
            };
        }

        private async Task<ADObjectValidation> ValidateGroupAsync(string groupDn)
        {
            var result = await _adService.SearchObjectByDnAsync(groupDn, "group");
            return new ADObjectValidation
            {
                Dn = groupDn,
                Exists = result.exists,
                Error = result.error
            };
        }
    }

    public class ADReadinessReport
    {
        public DateTime Timestamp { get; set; }
        public AdHealthResult ADConnectivity { get; set; } = new();
        public ADServiceAccountStatus? ServiceAccount { get; set; }
        public ADObjectValidation[]? OUs { get; set; }
        public ADObjectValidation[]? Groups { get; set; }
        public ADPasswordPolicyResult? PasswordPolicy { get; set; }
    }

    public class ADObjectValidation
    {
        public string Dn { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public string? Error { get; set; }
    }

    public class ADDryRunResult
    {
        public object? Student { get; set; }
        public object? ProposedAccount { get; set; }
        public object? TempPassword { get; set; }
        public string? LdapPath { get; set; }
        public object? PasswordPolicyCheck { get; set; }
    }

    public class ADTestUserRequest
    {
        public string StudentId { get; set; } = string.Empty;
        public string? TargetOu { get; set; }
        public string? TargetGroup { get; set; }
    }

    public class ADTestUserReport
    {
        public string RequestedSamAccountName { get; set; } = string.Empty;
        public string TargetOu { get; set; } = string.Empty;
        public string TargetGroup { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool OverallSuccess { get; set; }
        public string? Summary { get; set; }
        public List<ADTestStep> Steps { get; set; } = new();
        public ADReadUserResult? ReadUserResult { get; set; }
    }

    public class ADTestStep
    {
        public string Step { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Running";
        public string? Error { get; set; }
        public string? Details { get; set; }
    }
}
