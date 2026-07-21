using MapsterMapper;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.ADSetup;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // أدوات تشخيص AD — اتنقلت من ADSetupController
    public class ADSetupService : AppServiceBase, IADSetupService
    {
        private readonly IRepository<Student> _students;
        private readonly ActiveDirectoryService _adService;
        private readonly ILogger<ADSetupService> _logger;

        public ADSetupService(
            IRepository<Student> students,
            ActiveDirectoryService adService,
            ILogger<ADSetupService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _adService = adService;
            _logger = logger;
        }

        public async Task<ADReadinessReport> GetReadinessAsync()
        {
            var result = new ADReadinessReport { Timestamp = DateTime.UtcNow };

            result.ADConnectivity = await _adService.CheckHealthAsync();

            if (result.ADConnectivity.IsReachable)
            {
                result.ServiceAccount = await _adService.ValidateServiceAccountBindAsync();

                if (result.ServiceAccount?.BindSuccessful == true)
                {
                    result.OUs = new[]
                    {
                        await ValidateObjectAsync("OU=Male,OU=New,OU=Students,DC=globalgroups,DC=com", "organizationalUnit"),
                        await ValidateObjectAsync("OU=Female,OU=New,OU=Students,DC=globalgroups,DC=com", "organizationalUnit")
                    };

                    result.Groups = new[]
                    {
                        await ValidateObjectAsync("CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com", "group"),
                        await ValidateObjectAsync("CN=NUH-Student-G,OU=Groups,DC=globalgroups,DC=com", "group")
                    };

                    result.PasswordPolicy = await _adService.GetPasswordPolicyAsync();
                }
            }

            return result;
        }

        public async Task<ADDryRunResult> GetDryRunAsync(string studentId)
        {
            var student = await _students.FindAsync(s => s.student_id == studentId && !s.IsDeleted)
                ?? throw UserFriendlyException.NotFound("Student not found");

            if (string.IsNullOrEmpty(student.gender))
                throw new UserFriendlyException("Student has no gender set. Cannot determine target OU.", 400);

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

            return new ADDryRunResult
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
        }

        public async Task<ADTestUserReport> RunTestUserAsync(ADTestUserRequest request)
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
                return report;
            }
            report.Steps[0].Status = "Pass";
            report.Steps[0].Details = $"User '{sAMAccountName}' does not exist — ready to create";

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
                return Fail(report, createResult.Error, createResult.ErrorDetails, "FAILED at CreateUser");

            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Created at DN: {createResult.DistinguishedName}";

            report.Steps.Add(new ADTestStep
            {
                Step = "SetPassword",
                Description = "Set password for the test account",
                Status = "Running"
            });

            var passwordResult = await _adService.SetUserPasswordAsync(userDn, password);
            if (!passwordResult.Success)
            {
                var r = Fail(report, passwordResult.Error, passwordResult.ErrorDetails, "FAILED at SetPassword — cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Password set successfully (length: {password.Length})";

            report.Steps.Add(new ADTestStep
            {
                Step = "EnableAccount",
                Description = "Remove ACCOUNTDISABLE flag from userAccountControl",
                Status = "Running"
            });

            var enableResult = await _adService.ModifyUserAccountControlAsync(userDn, 66048);
            if (!enableResult.Success)
            {
                var r = Fail(report, enableResult.Error, enableResult.ErrorDetails, "FAILED at EnableAccount — cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "Account enabled (userAccountControl = 66048)";

            report.Steps.Add(new ADTestStep
            {
                Step = "AddToGroup",
                Description = $"Add user to group '{targetGroup}'",
                Status = "Running"
            });

            var groupResult = await _adService.AddUserToGroupAsync(userDn, targetGroup);
            if (!groupResult.Success)
            {
                var r = Fail(report, groupResult.Error, groupResult.ErrorDetails, "FAILED at AddToGroup — cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Added as member of '{targetGroup}'";

            report.Steps.Add(new ADTestStep
            {
                Step = "ReadUser",
                Description = "Read user attributes from AD for verification",
                Status = "Running"
            });

            var readResult = await _adService.GetUserBySamAccountNameAsync(sAMAccountName);
            if (!readResult.Success)
            {
                var r = Fail(report, readResult.Error, null, "FAILED at ReadUser — cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "User attributes read successfully";
            report.ReadUserResult = readResult;

            report.Steps.Add(new ADTestStep
            {
                Step = "DeleteUser",
                Description = "Delete the test user from AD",
                Status = "Running"
            });

            var deleteResult = await _adService.DeleteADUserAsync(userDn);
            if (!deleteResult.Success)
                return Fail(report, deleteResult.Error, deleteResult.ErrorDetails, "FAILED at DeleteUser — manual cleanup required");

            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "User deleted from AD";

            report.OverallSuccess = true;
            report.Summary = "ALL TESTS PASSED — AD account creation pipeline verified end-to-end";
            return report;
        }

        // ----------------------------- Helpers -----------------------------

        private static ADTestUserReport Fail(ADTestUserReport report, string? error, string? details, string summary)
        {
            report.OverallSuccess = false;
            report.Steps.Last().Status = "Fail";
            report.Steps.Last().Error = error;
            report.Steps.Last().Details = details;
            report.Summary = summary;
            return report;
        }

        private async Task TryCleanupUserAsync(string userDn, string samAccountName)
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

        private async Task<ADObjectValidation> ValidateObjectAsync(string dn, string objectClass)
        {
            var result = await _adService.SearchObjectByDnAsync(dn, objectClass);
            return new ADObjectValidation
            {
                Dn = dn,
                Exists = result.exists,
                Error = result.error
            };
        }
    }
}
