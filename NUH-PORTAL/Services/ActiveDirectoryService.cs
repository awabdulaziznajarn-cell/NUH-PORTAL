using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Options;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services
{
    public class ActiveDirectoryService
    {
        private readonly ActiveDirectoryConfig _config;
        private readonly ADServiceAccountConfig _serviceAccount;
        private readonly ILogger<ActiveDirectoryService> _logger;
        // ⚠️ IMemoryCache و CacheDuration اتشالوا مع كاش مجموعات الـ AD.
        //    الكاش ده كان بيتكتب مع كل تسجيل دخول وماحدش بيقرا منه ولا مرة —
        //    كان بيخدم استنتاج الدور من المجموعات، واستنتاج الدور نفسه اتشال.
        private static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromSeconds(30);
        private const int MaxRetries = 1;

        private DateTime _circuitBreakerExpires = DateTime.MinValue;
        private readonly object _circuitBreakerLock = new();

        public ActiveDirectoryService(
            IOptions<ActiveDirectoryConfig> config,
            IOptions<ADServiceAccountConfig> serviceAccount,
            ILogger<ActiveDirectoryService> logger)
        {
            _config = config.Value;
            _serviceAccount = serviceAccount.Value;
            _logger = logger;
        }

        public Task<AdAuthResult> AuthenticateAsync(string username, string password, string? clientIp = null)
        {
            return Task.Run(() => Authenticate(username, password, clientIp));
        }

        public Task<AdHealthResult> CheckHealthAsync()
        {
            return Task.Run(() =>
            {
                var result = new AdHealthResult();

                try
                {
                    using var connection = CreateConnection(5);
                    connection.Bind();
                    result.IsReachable = true;
                }
                catch (LdapException ex) when (ex.ErrorCode == 49)
                {
                    result.IsReachable = true;
                }
                catch (Exception ex)
                {
                    result.IsReachable = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        private AdAuthResult Authenticate(string username, string password, string? clientIp)
        {
            // لو الـ AD متعطّل من الكونفيج (زي بيئة التطوير) → نتخطّاه ونروح مباشرة للـ local fallback
            if (!_config.Enabled)
            {
                _logger.LogInformation("AD disabled via config (ActiveDirectory:Enabled=false) - using local fallback for {Username}", username);
                return new AdAuthResult { IsAdAvailable = false };
            }

            var result = new AdAuthResult { IsAdAvailable = !IsCircuitBreakerOpen() };

            if (!result.IsAdAvailable)
            {
                _logger.LogWarning("AD circuit breaker open, skipping AD auth for {Username}", username);
                return result;
            }

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return AttemptAuthentication(username, password, clientIp);
                }
                catch (LdapException ex) when (attempt < MaxRetries && IsTransient(ex))
                {
                    _logger.LogWarning(ex, "Transient AD error on attempt {Attempt} for {Username}, retrying...",
                        attempt + 1, username);
                }
                catch (Exception ex)
                {
                    // فشل اتصال بالـ AD (مثلاً DC مش متاح → Win32 "wait operation timed out").
                    // منرميش الاستثناء لفوق — نسجّله ونكمّل؛ ولو دي آخر محاولة نخرج من اللوب
                    // ونروح للـ local fallback بدل ما الدخول يقع بـ 500.
                    _logger.LogWarning(ex, "AD connection failure on attempt {Attempt} for {Username} - falling back to local",
                        attempt + 1, username);
                }
            }

            _logger.LogError("AD authentication failed after {MaxRetries} retries for {Username}", MaxRetries, username);
            OpenCircuitBreaker();
            return new AdAuthResult { IsAdAvailable = false };
        }

        private AdAuthResult AttemptAuthentication(string username, string password, string? clientIp)
        {
            var result = new AdAuthResult();

            using var connection = CreateConnection();

            var userPrincipal = $"{username}@{_config.Domain}";
            _logger.LogInformation("TRACE: LDAP Bind target: {UserPrincipal}, DC: {DomainController}:{Port}, SSL: {Ssl}, Protocol: LDAPv3",
                userPrincipal, _config.DomainController, _config.Port, true);

            try
            {
		
_logger.LogInformation(
        "TRACE LOGIN: Username={Username}, UserPrincipal={UserPrincipal}, PasswordLength={Length}",
        username,
        userPrincipal,
        password?.Length ?? 0
    );

                connection.Bind(new NetworkCredential(userPrincipal, password));
                result.IsAuthenticated = true;
                _logger.LogInformation("TRACE: LDAP Bind succeeded for {UserPrincipal}", userPrincipal);
            }
            catch (LdapException ex)
            {
                _logger.LogInformation("TRACE: LDAP Bind failed. ErrorCode: {ErrorCode}, ServerErrorMessage: {ServerMsg}",
                    ex.ErrorCode, ex.ServerErrorMessage ?? "(null)");
                if (ex.ErrorCode == 81)
                    result.IsAdAvailable = false;
                result.ErrorSubCode = TryExtractErrorData(ex.ServerErrorMessage);
                LogAuthFailure(username, clientIp, ex, result.ErrorSubCode);
                return result;
            }

            result.Details = GetUserDetails(connection, username);

            // ⚠️ مافيش استنتاج للدور من مجموعات الدليل، فمابنجيبش memberOf هنا خالص.
            //    الدور بيتحدد من شاشة «إدارة المستخدمين» وبس — والدليل مسؤوليته
            //    يتأكد من الباسورد ويجيب البيانات الشخصية.
            _logger.LogInformation(
                "AD login successful for {Username}, IP: {ClientIp}",
                username, clientIp ?? "unknown");

            return result;
        }

        private LdapConnection CreateConnection(int timeoutSeconds = 10)
        {
            var identifier = new LdapDirectoryIdentifier(_config.DomainController, _config.Port);
            var connection = new LdapConnection(identifier) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = true;
            connection.SessionOptions.VerifyServerCertificate = (conn, cert) =>
            {
                _logger.LogInformation("TRACE: Certificate validation callback invoked. Cert subject: {Subject}, ValidateCertificate config: {Validate}",
                    cert?.Subject ?? "null", _config.ValidateCertificate);
                return ValidateServerCertificate(conn, cert);
            };

            return connection;
        }

        private bool ValidateServerCertificate(LdapConnection conn, X509Certificate cert)
        {
            if (!_config.ValidateCertificate)
            {
                _logger.LogInformation("TRACE: Certificate validation skipped (ValidateCertificate=false)");
                return true;
            }

            
try
{
    using var chain = new X509Chain();
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

    var cert2 = new X509Certificate2(cert);

    var result = chain.Build(cert2);

    _logger.LogInformation(
        "TRACE: Certificate validation result: {Result}, chain error count: {Errors}",
        result,
        chain.ChainStatus?.Length ?? 0
    );

    if (chain.ChainStatus != null && chain.ChainStatus.Length > 0)
    {
        foreach (var status in chain.ChainStatus)

                    {
                        _logger.LogInformation("TRACE:   Chain status: {Status} - {Info}", status.Status, status.StatusInformation);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AD server certificate validation failed for {Controller}", _config.DomainController);
                return false;
            }
        }

        private AdUserDetails? GetUserDetails(LdapConnection connection, string username)
        {
            var baseDn = BuildBaseDn();
            var escapedUsername = EscapeLdapSearchFilter(username);

            _logger.LogInformation("TRACE: LDAP search baseDN: {BaseDn}, filter: (sAMAccountName={EscapedUsername}), scope: Subtree, attrs: displayName,mail,department,mobile,title",
                baseDn, escapedUsername);

            var searchRequest = new SearchRequest(
                baseDn,
                $"(sAMAccountName={escapedUsername})",
                SearchScope.Subtree,
                "displayName", "mail", "department", "mobile", "title"
            );

            SearchResponse? response;
            try
            {
                response = connection.SendRequest(searchRequest) as SearchResponse;
            }
            catch (Exception ex)
            {
                _logger.LogInformation("TRACE: LDAP search threw exception: {ExType}: {ExMsg}", ex.GetType().FullName, ex.Message);
                return null;
            }

            var entry = response?.Entries?.Cast<SearchResultEntry>().FirstOrDefault();
            if (entry == null)
            {
                _logger.LogWarning("AD user not found in directory: {Username}", username);
                return null;
            }

            _logger.LogInformation("TRACE: LDAP search found entry. Attributes count: {Count}, DN: {DistinguishedName}",
                entry.Attributes.Count, entry.DistinguishedName ?? "(null)");

            foreach (var attrName in new[] { "displayName", "mail", "department", "mobile", "title" })
            {
                var values = entry.Attributes[attrName]?.GetValues(typeof(string));
                if (values != null)
                    _logger.LogInformation("TRACE:   attr '{Attr}' ({Count} values): {Values}",
                        attrName, values.Length, string.Join("; ", values.Cast<string>()));
                else
                    _logger.LogInformation("TRACE:   attr '{Attr}': (not present)", attrName);
            }

            return new AdUserDetails
            {
                Username = username,
                DisplayName = GetAttributeValue(entry, "displayName") ?? username,
                Email = GetAttributeValue(entry, "mail") ?? $"{username}@{_config.Domain}",
                Department = GetAttributeValue(entry, "department") ?? string.Empty,
                Mobile = GetAttributeValue(entry, "mobile") ?? string.Empty,
                JobTitle = GetAttributeValue(entry, "title") ?? string.Empty
            };
        }

        private string BuildBaseDn()
        {
            var parts = _config.Domain.Split('.');
            return string.Join(",", parts.Select(p => $"DC={p}"));
        }

        private void LogAuthFailure(string username, string? clientIp, LdapException ex, string? subCode)
        {
            _logger.LogWarning(
                "AD authentication failed for {Username}. ErrorCode: {ErrorCode}, SubCode: {SubCode}, IP: {ClientIp}",
                username, ex.ErrorCode, subCode ?? "none", clientIp ?? "unknown");
        }

        private static string? TryExtractErrorData(string? serverErrorMessage)
        {
            if (string.IsNullOrEmpty(serverErrorMessage)) return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                serverErrorMessage, @"data\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
        }

        private void OpenCircuitBreaker()
        {
            lock (_circuitBreakerLock)
            {
                _circuitBreakerExpires = DateTime.UtcNow.Add(CircuitBreakerDuration);
                _logger.LogWarning("AD circuit breaker opened until {Expires}", _circuitBreakerExpires);
            }
        }

        private bool IsCircuitBreakerOpen()
        {
            lock (_circuitBreakerLock)
            {
                return DateTime.UtcNow < _circuitBreakerExpires;
            }
        }

        private static bool IsTransient(LdapException ex)
        {
            return ex.ErrorCode is 0 or 80 or 85 or 91 or 64 or 100;
        }

        // الخصائص الوحيدة المسموح للتطبيق كتابتها على حساب الطالب عبر
        // SetUserExtensionAttributesAsync — قائمة بيضاء بالاسم الكامل.
        // مبنية على معيار جامعة نجران لحسابات الطلاب في nuh.edu.sa.
        // ملاحظة: extensionAttribute1-15 غير موجودة في schema هذا الدومين
        // (تحتاج امتداد schema الخاص بـ Exchange) فلم تُدرَج.
        private static readonly HashSet<string> AllowedSyncAttributes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "description",   // الاسم العربي الكامل
                "displayName",   // الاسم الإنجليزي الكامل
                "givenName",     // الاسم الأول
                "initials",      // الأحرف الأولى للأسماء الوسطى
                "sn",            // اسم العائلة
                "employeeID",    // رقم الهوية الوطنية
                "mobile",        // رقم الجوال
                "company",       // الكلية
                "department"     // القسم
            };

        private static string EscapeLdapSearchFilter(string input)
        {
            return input
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00")
                .Replace("/", "\\2f");
        }

        private static string? GetAttributeValue(SearchResultEntry entry, string attributeName)
        {
            return entry.Attributes[attributeName]?.GetValues(typeof(string))?.FirstOrDefault() as string;
        }

        public Task<ADServiceAccountStatus> ValidateServiceAccountBindAsync()
        {
            return Task.Run(() =>
            {
                var result = new ADServiceAccountStatus();

                if (string.IsNullOrEmpty(_serviceAccount.Username) || string.IsNullOrEmpty(_serviceAccount.Password))
                {
                    result.BindSuccessful = false;
                    result.Error = "ADServiceAccount credentials are not configured in appsettings.json.";
                    return result;
                }

                try
                {
                    using var connection = CreateConnection(10);
                    var userPrincipal = $"{_serviceAccount.Username}@{_config.Domain}";
                    connection.Bind(new NetworkCredential(userPrincipal, _serviceAccount.Password));
                    result.BindSuccessful = true;
                }
                catch (LdapException ex)
                {
                    result.BindSuccessful = false;
                    result.Error = $"LDAP bind failed. ErrorCode: {ex.ErrorCode}, Message: {ex.Message}";
                }
                catch (Exception ex)
                {
                    result.BindSuccessful = false;
                    result.Error = $"Bind failed: {ex.Message}";
                }

                return result;
            });
        }

        public Task<(bool exists, string? error)> SearchObjectByDnAsync(string distinguishedName, string expectedObjectClass)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                        return ((bool exists, string? error))(false, "Service account not configured or bind failed.");

                    var searchRequest = new SearchRequest(
                        distinguishedName,
                        $"(objectClass={expectedObjectClass})",
                        SearchScope.Base,
                        "distinguishedName", "name"
                    );

                    var response = connection.SendRequest(searchRequest) as SearchResponse;
                    if (response == null)
                        return ((bool exists, string? error))(false, "No response from AD.");

                    var entry = response.Entries?.Cast<SearchResultEntry>().FirstOrDefault();
                    if (entry != null)
                        return ((bool exists, string? error))(true, null);

                    return ((bool exists, string? error))(false, $"Object with DN '{distinguishedName}' not found in AD. Verify the OU/Group exists.");
                }
                catch (LdapException ex) when (ex.ErrorCode == 32)
                {
                    return ((bool exists, string? error))(false, $"Object with DN '{distinguishedName}' does not exist in AD (LDAP error 32: no such object).");
                }
                catch (Exception ex)
                {
                    return ((bool exists, string? error))(false, $"Search error: {ex.Message}");
                }
            });
        }

        public Task<ADPasswordPolicyResult> GetPasswordPolicyAsync()
        {
            return Task.Run(() =>
            {
                var result = new ADPasswordPolicyResult();

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var baseDn = BuildBaseDn();
                    var searchRequest = new SearchRequest(
                        baseDn,
                        "(objectClass=domainDNS)",
                        SearchScope.Base,
                        "minPwdLength", "pwdProperties", "minPwdAge", "maxPwdAge", "pwdHistoryLength"
                    );

                    var response = connection.SendRequest(searchRequest) as SearchResponse;
                    var entry = response?.Entries?.Cast<SearchResultEntry>().FirstOrDefault();

                    if (entry != null)
                    {
                        var minLen = GetAttributeValue(entry, "minPwdLength");
                        var pwdProps = GetAttributeValue(entry, "pwdProperties");
                        var pwdHist = GetAttributeValue(entry, "pwdHistoryLength");

                        result.MinLength = minLen != null ? int.Parse(minLen) : 7;
                        result.ComplexityEnabled = pwdProps != null && (int.Parse(pwdProps) & 1) == 1;
                        result.PwdHistoryLength = pwdHist != null ? int.Parse(pwdHist) : 0;

                        result.DomainPoliciesFound = true;
                    }
                    else
                    {
                        result.Error = "Could not read domain password policy. Defaulting to safe assumptions.";
                        result.MinLength = 8;
                        result.ComplexityEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    result.Error = $"Failed to query password policy: {ex.Message}";
                    result.MinLength = 8;
                    result.ComplexityEnabled = true;
                }

                return result;
            });
        }

        public Task<ADPasswordCompatibilityResult> ValidatePasswordCompatibilityAsync(string password)
        {
            return Task.Run(async () =>
            {
                var policy = await GetPasswordPolicyAsync();
                var result = new ADPasswordCompatibilityResult
                {
                    Password = password,
                    Length = password.Length,
                    Policy = policy
                };

                result.MeetsMinimumLength = password.Length >= policy.MinLength;
                result.HasUpperCase = password.Any(char.IsUpper);
                result.HasLowerCase = password.Any(char.IsLower);
                result.HasDigit = password.Any(char.IsDigit);
                result.HasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

                if (policy.ComplexityEnabled)
                {
                    var categoryCount = 0;
                    if (result.HasUpperCase) categoryCount++;
                    if (result.HasLowerCase) categoryCount++;
                    if (result.HasDigit) categoryCount++;
                    if (result.HasSpecialChar) categoryCount++;
                    result.MeetsComplexity = categoryCount >= 3;
                }
                else
                {
                    result.MeetsComplexity = true;
                }

                result.Compatible = result.MeetsMinimumLength && result.MeetsComplexity;

                if (!result.Compatible)
                {
                    var issues = new List<string>();
                    if (!result.MeetsMinimumLength)
                        issues.Add($"Minimum length is {policy.MinLength}, but password is {password.Length} characters.");
                    if (!result.MeetsComplexity)
                        issues.Add("Password does not meet complexity requirements (must contain 3 of 4: uppercase, lowercase, digit, special char).");
                    result.Issues = issues;
                }

                return result;
            });
        }

        private LdapConnection? CreateManagementConnection()
        {
            if (string.IsNullOrEmpty(_serviceAccount.Username) || string.IsNullOrEmpty(_serviceAccount.Password))
            {
                _logger.LogWarning("ADServiceAccount not configured. Cannot perform management operations.");
                return null;
            }

            try
            {
                var connection = CreateConnection(10);
                var userPrincipal = $"{_serviceAccount.Username}@{_config.Domain}";
                connection.Bind(new NetworkCredential(userPrincipal, _serviceAccount.Password));
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind with AD service account {Username}", _serviceAccount.Username);
                return null;
            }
        }

        public Task<ADOperationResult> CreateADUserAsync(ADCreateUserRequest request)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "CreateUser" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var newDn = $"CN={request.SamAccountName},{request.TargetOu}";

                    var attrList = new List<DirectoryAttribute>
                    {
                        new DirectoryAttribute("objectClass", "user", "top"),
                        new DirectoryAttribute("sAMAccountName", request.SamAccountName),
                        new DirectoryAttribute("userPrincipalName", request.UserPrincipalName),
                        new DirectoryAttribute("displayName", request.DisplayName)
                    };

                    if (!string.IsNullOrEmpty(request.GivenName))
                        attrList.Add(new DirectoryAttribute("givenName", request.GivenName));
                    if (!string.IsNullOrEmpty(request.Initials))
                        attrList.Add(new DirectoryAttribute("initials", request.Initials));
                    if (!string.IsNullOrEmpty(request.Surname))
                        attrList.Add(new DirectoryAttribute("sn", request.Surname));

                    attrList.Add(new DirectoryAttribute("userAccountControl", request.UserAccountControl.ToString()));

                    // description يحمل الاسم العربي الكامل حسب معيار الجامعة
                    if (!string.IsNullOrEmpty(request.Description))
                        attrList.Add(new DirectoryAttribute("description", request.Description));

                    if (!string.IsNullOrEmpty(request.EmployeeId))
                        attrList.Add(new DirectoryAttribute("employeeID", request.EmployeeId));
                    if (!string.IsNullOrEmpty(request.Mobile))
                        attrList.Add(new DirectoryAttribute("mobile", request.Mobile));
                    if (!string.IsNullOrEmpty(request.Company))
                        attrList.Add(new DirectoryAttribute("company", request.Company));
                    if (!string.IsNullOrEmpty(request.Department))
                        attrList.Add(new DirectoryAttribute("department", request.Department));

                    var addRequest = new AddRequest(newDn, attrList.ToArray());
                    connection.SendRequest(addRequest);

                    result.Success = true;
                    result.DistinguishedName = newDn;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    result.StackTrace = ex.StackTrace;
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                    result.StackTrace = ex.StackTrace;
                }

                return result;
            });
        }

        public Task<ADOperationResult> SetUserPasswordAsync(string distinguishedName, string password)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "SetPassword" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var encodedPassword = Encoding.Unicode.GetBytes("\"" + password + "\"");
                    var modifyRequest = new ModifyRequest(
                        distinguishedName,
                        DirectoryAttributeOperation.Replace,
                        "unicodePwd",
                        encodedPassword
                    );

                    connection.SendRequest(modifyRequest);
                    result.Success = true;
                    result.DistinguishedName = distinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> ModifyUserAccountControlAsync(string distinguishedName, int userAccountControl)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "ModifyUserAccountControl" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var modifyRequest = new ModifyRequest(
                        distinguishedName,
                        DirectoryAttributeOperation.Replace,
                        "userAccountControl",
                        userAccountControl.ToString()
                    );

                    connection.SendRequest(modifyRequest);
                    result.Success = true;
                    result.DistinguishedName = distinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> AddUserToGroupAsync(string userDistinguishedName, string groupDistinguishedName)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "AddToGroup" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var modifyRequest = new ModifyRequest(
                        groupDistinguishedName,
                        DirectoryAttributeOperation.Add,
                        "member",
                        userDistinguishedName
                    );

                    connection.SendRequest(modifyRequest);
                    result.Success = true;
                    result.DistinguishedName = groupDistinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> DeleteADUserAsync(string distinguishedName)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "DeleteUser" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var deleteRequest = new DeleteRequest(distinguishedName);
                    connection.SendRequest(deleteRequest);
                    result.Success = true;
                    result.DistinguishedName = distinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADReadUserResult> GetUserBySamAccountNameAsync(string samAccountName)
        {
            return Task.Run(() =>
            {
                var result = new ADReadUserResult();

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var baseDn = BuildBaseDn();
                    var escaped = EscapeLdapSearchFilter(samAccountName);
                    var searchRequest = new SearchRequest(
                        baseDn,
                        $"(sAMAccountName={escaped})",
                        SearchScope.Subtree,
                        "distinguishedName", "sAMAccountName", "userPrincipalName",
                        "displayName", "givenName", "sn", "userAccountControl",
                        "memberOf", "extensionAttribute1", "extensionAttribute2",
                        "description", "department", "title", "mail", "whenCreated",
                        // ⚠️ الخصائص التلاتة دي مكانتش مطلوبة في البحث، فكانت بترجع
                        //    فاضية دايمًا حتى لو ليها قيمة في الدومين. الـ LDAP
                        //    بيرجّع الخصائص المطلوبة بالاسم بس — مش كل حاجة.
                        //    سكن أعضاء هيئة التدريس بيقراهم للاستيراد وفحص التطابق.
                        "employeeID", "mobile", "company"
                    );

                    var response = connection.SendRequest(searchRequest) as SearchResponse;
                    var entry = response?.Entries?.Cast<SearchResultEntry>().FirstOrDefault();
                    if (entry == null)
                    {
                        result.Success = false;
                        result.Error = $"User with sAMAccountName '{samAccountName}' not found.";
                        return result;
                    }

                    result.Success = true;
                    result.DistinguishedName = entry.DistinguishedName;
                    result.SamAccountName = GetAttributeValue(entry, "sAMAccountName") ?? "";
                    result.UserPrincipalName = GetAttributeValue(entry, "userPrincipalName") ?? "";
                    result.DisplayName = GetAttributeValue(entry, "displayName") ?? "";
                    result.GivenName = GetAttributeValue(entry, "givenName") ?? "";
                    result.Surname = GetAttributeValue(entry, "sn") ?? "";
                    result.Description = GetAttributeValue(entry, "description") ?? "";
                    result.Department = GetAttributeValue(entry, "department") ?? "";
                    result.EmployeeId = GetAttributeValue(entry, "employeeID") ?? "";
                    result.Mobile = GetAttributeValue(entry, "mobile") ?? "";
                    result.Company = GetAttributeValue(entry, "company") ?? "";
                    result.ExtensionAttribute1 = GetAttributeValue(entry, "extensionAttribute1") ?? "";

                    var uacStr = GetAttributeValue(entry, "userAccountControl") ?? "0";
                    int.TryParse(uacStr, out var uac);
                    result.UserAccountControl = uac;
                    result.AccountEnabled = (uac & 2) == 0;

                    var groups = new List<string>();
                    var memberOfValues = entry.Attributes["memberOf"]?.GetValues(typeof(string));
                    if (memberOfValues != null)
                    {
                        foreach (string dn in memberOfValues)
                        {
                            var cnPart = dn.Split(',').FirstOrDefault(p => p.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase));
                            if (cnPart != null)
                                groups.Add(cnPart.Trim()[3..]);
                        }
                    }
                    result.MemberOf = groups;

                    result.AttributesRaw = new Dictionary<string, List<string>>();
                    foreach (var key in entry.Attributes.AttributeNames)
                    {
                        var name = key.ToString()!;
                        var vals = entry.Attributes[name]?.GetValues(typeof(string));
                        if (vals != null)
                            result.AttributesRaw[name] = vals.Cast<string>().ToList();
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> EnableUserAsync(string distinguishedName)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "EnableUser" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var searchRequest = new SearchRequest(
                        distinguishedName,
                        "(objectClass=user)",
                        SearchScope.Base,
                        "userAccountControl"
                    );

                    var searchResponse = connection.SendRequest(searchRequest) as SearchResponse;
                    var entry = searchResponse?.Entries?.Cast<SearchResultEntry>().FirstOrDefault();
                    if (entry == null)
                    {
                        result.Success = false;
                        result.Error = $"User '{distinguishedName}' not found.";
                        return result;
                    }

                    var uacStr = GetAttributeValue(entry, "userAccountControl") ?? "514";
                    int.TryParse(uacStr, out var currentUac);
                    var newUac = currentUac & ~2;

                    var modifyRequest = new ModifyRequest(
                        distinguishedName,
                        DirectoryAttributeOperation.Replace,
                        "userAccountControl",
                        newUac.ToString()
                    );

                    connection.SendRequest(modifyRequest);
                    result.Success = true;
                    result.DistinguishedName = distinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> DisableUserAsync(string distinguishedName)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "DisableUser" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var searchRequest = new SearchRequest(
                        distinguishedName,
                        "(objectClass=user)",
                        SearchScope.Base,
                        "userAccountControl"
                    );

                    var searchResponse = connection.SendRequest(searchRequest) as SearchResponse;
                    var entry = searchResponse?.Entries?.Cast<SearchResultEntry>().FirstOrDefault();
                    if (entry == null)
                    {
                        result.Success = false;
                        result.Error = $"User '{distinguishedName}' not found.";
                        return result;
                    }

                    var uacStr = GetAttributeValue(entry, "userAccountControl") ?? "512";
                    int.TryParse(uacStr, out var currentUac);
                    var newUac = currentUac | 2;

                    var modifyRequest = new ModifyRequest(
                        distinguishedName,
                        DirectoryAttributeOperation.Replace,
                        "userAccountControl",
                        newUac.ToString()
                    );

                    connection.SendRequest(modifyRequest);
                    result.Success = true;
                    result.DistinguishedName = distinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADMoveUserResult> MoveUserAsync(string currentDistinguishedName, string newParentOu)
        {
            return Task.Run(() =>
            {
                var result = new ADMoveUserResult();

                try
                {
                    var cn = currentDistinguishedName.Split(',').FirstOrDefault() ?? "";
                    var newDn = $"{cn},{newParentOu}";

                    var userPrincipal = $"{_serviceAccount.Username}@{_config.Domain}";
                    using var de = new System.DirectoryServices.DirectoryEntry($"LDAP://{_config.DomainController}/{currentDistinguishedName}",
                        userPrincipal, _serviceAccount.Password);
                    using var newParent = new System.DirectoryServices.DirectoryEntry($"LDAP://{_config.DomainController}/{newParentOu}",
                        userPrincipal, _serviceAccount.Password);
                    de.MoveTo(newParent);

                    result.Success = true;
                    result.NewDistinguishedName = newDn;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADSearchUsersResult> SearchUsersAsync(string searchText, int maxResults = 50)
        {
            return Task.Run(() =>
            {
                var result = new ADSearchUsersResult();

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var baseDn = BuildBaseDn();
                    var escaped = EscapeLdapSearchFilter(searchText);
                    var filter = $"(|(sAMAccountName=*{escaped}*)(displayName=*{escaped}*)(givenName=*{escaped}*)(sn=*{escaped}*))";

                    var searchRequest = new SearchRequest(
                        baseDn,
                        $"(&(objectClass=user){filter})",
                        SearchScope.Subtree,
                        "distinguishedName", "sAMAccountName", "displayName",
                        "userPrincipalName", "userAccountControl", "mail",
                        "department", "title", "description"
                    );

                    searchRequest.SizeLimit = maxResults;

                    var response = connection.SendRequest(searchRequest) as SearchResponse;
                    if (response == null)
                    {
                        result.Success = true;
                        return result;
                    }

                    var entries = response.Entries?.Cast<SearchResultEntry>().ToList() ?? new();
                    foreach (var entry in entries)
                    {
                        var uacStr = GetAttributeValue(entry, "userAccountControl") ?? "0";
                        int.TryParse(uacStr, out var uac);

                        result.Users.Add(new ADSearchUserEntry
                        {
                            DistinguishedName = entry.DistinguishedName,
                            SamAccountName = GetAttributeValue(entry, "sAMAccountName") ?? "",
                            DisplayName = GetAttributeValue(entry, "displayName") ?? "",
                            UserPrincipalName = GetAttributeValue(entry, "userPrincipalName") ?? "",
                            Email = GetAttributeValue(entry, "mail") ?? "",
                            Department = GetAttributeValue(entry, "department") ?? "",
                            AccountEnabled = (uac & 2) == 0
                        });
                    }

                    result.Success = true;
                    result.TotalResults = result.Users.Count;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> SetUserExtensionAttributesAsync(string distinguishedName, Dictionary<string, string> extensionAttributes)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "SetExtensionAttributes" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    foreach (var kvp in extensionAttributes)
                    {
                        var key = kvp.Key;

                        // مطابقة بالاسم الكامل — كانت مطابقة بالبادئة، وده كان بيسمح
                        // بأي خاصية تبدأ بـ "department" مثلًا زي "departmentNumber".
                        if (!AllowedSyncAttributes.Contains(key))
                        {
                            _logger.LogWarning("Blocked attempt to set disallowed attribute '{Attr}' on {Dn}", key, distinguishedName);
                            continue;
                        }

                        // ⚠️ الخاصية الفاضية تُحذف ولا تُكتب فارغة. الدليل يرفض
                        //    Replace بقيمة "" ويردّ بـ «Error in attribute conversion
                        //    operation»، ويُلغي العملية كلها لا الخاصية وحدها — فخانة
                        //    واحدة فارغة كانت تُفشل كتابة الخصائص الخمس مجتمعة.
                        //    الحذف هو المقابل الصحيح للقيمة الفارغة في LDAP.
                        var isEmpty = string.IsNullOrEmpty(kvp.Value);
                        var modifyRequest = isEmpty
                            ? new ModifyRequest(distinguishedName, DirectoryAttributeOperation.Delete, key)
                            : new ModifyRequest(distinguishedName, DirectoryAttributeOperation.Replace, key, kvp.Value);

                        try
                        {
                            connection.SendRequest(modifyRequest);
                        }
                        catch (DirectoryOperationException ex)
                            when (isEmpty && ex.Response?.ResultCode == ResultCode.NoSuchAttribute)
                        {
                            // ⚠️ الخاصية فارغة أصلًا في الدليل: حذف غير الموجود يردّ
                            //    بخطأ، والنتيجة المطلوبة متحققة سلفًا. تجاهله هنا وحده
                            //    — وبشرط isEmpty — حتى لا يبتلع خطأ كتابة حقيقيًا.
                        }
                    }

                    result.Success = true;
                    result.DistinguishedName = distinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        public Task<ADOperationResult> RemoveUserFromGroupAsync(string userDistinguishedName, string groupDistinguishedName)
        {
            return Task.Run(() =>
            {
                var result = new ADOperationResult { Operation = "RemoveFromGroup" };

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Success = false;
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    var modifyRequest = new ModifyRequest(
                        groupDistinguishedName,
                        DirectoryAttributeOperation.Delete,
                        "member",
                        userDistinguishedName
                    );

                    connection.SendRequest(modifyRequest);
                    result.Success = true;
                    result.DistinguishedName = groupDistinguishedName;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Success = false;
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        // =================================================================
        //  سكن أعضاء هيئة التدريس — قراءة الوحدات وفحص الصلاحية
        // =================================================================

        // قراءة كل حسابات المستخدمين اللي جوّه OU معيّنة.
        //
        // ⚠️ بصفحات (PageResultRequestControl) مش استعلام واحد. الـ DC بيرجّع
        //    ١٠٠٠ صف كحد أقصى للطلب الواحد **وبيقطع الباقي من غير أي خطأ** —
        //    فاستعلام عادي على OU فيها ١٢٠٠ حساب بيرجع ١٠٠٠ ويبان كأنه نجح،
        //    والـ ٢٠٠ الباقيين بيختفوا بصمت. ده بالظبط نوع الغلط اللي مايتكتشفش
        //    غير بعد شهور لما حد يسأل «فين الفيلا الفلانية؟».
        public Task<ADOuUsersResult> SearchOuUsersAsync(string organizationalUnitDn, int maxResults = 2000)
        {
            return Task.Run(() =>
            {
                var result = new ADOuUsersResult { OrganizationalUnit = organizationalUnitDn };

                if (string.IsNullOrWhiteSpace(organizationalUnitDn))
                {
                    result.Error = "OU path is empty.";
                    return result;
                }

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    // objectCategory=person عشان نستبعد كائنات الكمبيوتر — دي
                    // بتورّث objectClass=user فبتيجي في النتيجة من غير الفلتر ده
                    var request = new SearchRequest(
                        organizationalUnitDn,
                        "(&(objectClass=user)(objectCategory=person))",
                        SearchScope.Subtree,
                        "distinguishedName", "sAMAccountName", "description",
                        "employeeID", "mobile", "company", "department",
                        "userAccountControl", "whenCreated");

                    var pageControl = new PageResultRequestControl(500);
                    request.Controls.Add(pageControl);
                    // من غيره الـ DC ممكن يرجّع إحالات (referrals) بدل نتائج.
                    // ⚠️ الاسم الكامل ضروري: SearchOption موجود في
                    //    System.DirectoryServices.Protocols وفي System.IO كمان،
                    //    و System.IO بييجي تلقائي مع implicit usings — فالاسم
                    //    المختصر بيبقى غامض والكومبايلر بيرفضه (CS0104).
                    request.Controls.Add(new SearchOptionsControl(
                        System.DirectoryServices.Protocols.SearchOption.DomainScope));

                    while (true)
                    {
                        if (connection.SendRequest(request) is not SearchResponse response)
                            break;

                        foreach (SearchResultEntry entry in response.Entries)
                        {
                            var uacStr = GetAttributeValue(entry, "userAccountControl") ?? "0";
                            int.TryParse(uacStr, out var uac);

                            result.Users.Add(new ADOuUser
                            {
                                SamAccountName = GetAttributeValue(entry, "sAMAccountName") ?? "",
                                DistinguishedName = entry.DistinguishedName,
                                Description = GetAttributeValue(entry, "description"),
                                EmployeeId = GetAttributeValue(entry, "employeeID"),
                                Mobile = GetAttributeValue(entry, "mobile"),
                                Company = GetAttributeValue(entry, "company"),
                                Department = GetAttributeValue(entry, "department"),
                                UserAccountControl = uac,
                                AccountEnabled = (uac & 2) == 0,
                                WhenCreated = ParseAdTimestamp(GetAttributeValue(entry, "whenCreated"))
                            });
                        }

                        if (result.Users.Count >= maxResults)
                        {
                            result.Truncated = true;
                            break;
                        }

                        var pageResponse = response.Controls
                            .OfType<PageResultResponseControl>().FirstOrDefault();
                        if (pageResponse == null || pageResponse.Cookie.Length == 0)
                            break;

                        pageControl.Cookie = pageResponse.Cookie;
                    }

                    result.Success = true;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                    _logger.LogError(ex, "SearchOuUsers failed for {Ou}", organizationalUnitDn);
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                    _logger.LogError(ex, "SearchOuUsers failed for {Ou}", organizationalUnitDn);
                }

                return result;
            });
        }

        // فحص: حساب الخدمة بيقدر يقرا من الـ OU دي؟ وبيقدر يكتب في أنهي خصائص؟
        //
        // ⚠️ الفحص من غير ما نكتب أي حاجة. بنقرا الخاصية المحسوبة
        //    allowedAttributesEffective — الدومين بيحسبها لكل كائن وبيرجّع فيها
        //    الخصائص اللي **الحساب المتصل حاليًا** له حق تعديلها فعلًا.
        //    البديل (نكتب قيمة ونشوف نجحت ولا لأ) معناه إننا بنعدّل بيانات حقيقية
        //    عشان نختبر، وده مرفوض على بيانات إنتاج.
        public Task<ADOuAccessResult> CheckOuAccessAsync(string organizationalUnitDn, IEnumerable<string> requiredAttributes)
        {
            return Task.Run(() =>
            {
                var required = requiredAttributes?.ToList() ?? new List<string>();
                var result = new ADOuAccessResult { OrganizationalUnit = organizationalUnitDn };

                if (string.IsNullOrWhiteSpace(organizationalUnitDn))
                {
                    result.Error = "OU path is empty.";
                    return result;
                }

                try
                {
                    using var connection = CreateManagementConnection();
                    if (connection == null)
                    {
                        result.Error = "Service account not configured or bind failed.";
                        return result;
                    }

                    // (١) الـ OU نفسها موجودة؟
                    var ouRequest = new SearchRequest(
                        organizationalUnitDn, "(objectClass=*)", SearchScope.Base, "distinguishedName");
                    if (connection.SendRequest(ouRequest) is not SearchResponse ouResponse
                        || ouResponse.Entries.Count == 0)
                    {
                        result.Error = "الوحدة التنظيمية مش موجودة أو مفيش صلاحية قراءة عليها.";
                        return result;
                    }
                    result.OuExists = true;

                    // (٢) نقرا حساب واحد جوّاها — ده بيثبت القراءة
                    var probeRequest = new SearchRequest(
                        organizationalUnitDn,
                        "(&(objectClass=user)(objectCategory=person))",
                        SearchScope.Subtree,
                        "distinguishedName", "sAMAccountName", "allowedAttributesEffective");
                    probeRequest.Controls.Add(new PageResultRequestControl(1));

                    if (connection.SendRequest(probeRequest) is not SearchResponse probeResponse
                        || probeResponse.Entries.Count == 0)
                    {
                        result.CanRead = true;
                        result.Error = "الوحدة التنظيمية مفيهاش حسابات — القراءة شغّالة بس مافيش كائن نختبر عليه الكتابة.";
                        return result;
                    }

                    result.CanRead = true;
                    var probe = probeResponse.Entries[0];
                    result.ProbedAccount = GetAttributeValue(probe, "sAMAccountName");

                    // (٣) الخصائص اللي حساب الخدمة يقدر يكتبها على الكائن ده
                    var writable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var effective = probe.Attributes["allowedAttributesEffective"]?.GetValues(typeof(string));
                    if (effective != null)
                        foreach (string a in effective) writable.Add(a);

                    foreach (var attr in required)
                        result.AttributeWritable[attr] = writable.Contains(attr);

                    // ⚠️ لو الخاصية دي مارجعتش أصلًا يبقى الحساب مالوش حق كتابة
                    //    على الكائن ده خالص — مش إن الفحص فشل. نفرّق بين الاتنين
                    //    عشان الرسالة اللي المستخدم يشوفها تبقى صح.
                    result.EffectiveAttributesReturned = writable.Count;
                }
                catch (DirectoryOperationException ex)
                {
                    result.Error = $"LDAP error: {ex.Message}";
                    if (ex.Response != null)
                        result.ErrorDetails = $"Server error: {ex.Response.ErrorMessage}";
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                }

                return result;
            });
        }

        // whenCreated بييجي بصيغة الدومين "20240312091500.0Z". بناخد أول ١٤ خانة
        // ونقراهم UTC — أبسط وأمتن من محاولة مطابقة الصيغة كلها بكسورها.
        private static DateTime? ParseAdTimestamp(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length < 14) return null;
            return DateTime.TryParseExact(raw[..14], "yyyyMMddHHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt) ? dt : null;
        }
    }

    public class ADOuUser
    {
        public string SamAccountName { get; set; } = string.Empty;
        public string? DistinguishedName { get; set; }
        public string? Description { get; set; }
        public string? EmployeeId { get; set; }
        public string? Mobile { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public int UserAccountControl { get; set; }
        public bool AccountEnabled { get; set; }
        public DateTime? WhenCreated { get; set; }
    }

    public class ADOuUsersResult
    {
        public bool Success { get; set; }
        public string OrganizationalUnit { get; set; } = string.Empty;
        public List<ADOuUser> Users { get; set; } = new();
        // وصلنا للسقف وفي كمان — بيتعرض كتحذير مش بيعدّي بصمت
        public bool Truncated { get; set; }
        public string? Error { get; set; }
        public string? ErrorDetails { get; set; }
    }

    public class ADOuAccessResult
    {
        public string OrganizationalUnit { get; set; } = string.Empty;
        public bool OuExists { get; set; }
        public bool CanRead { get; set; }
        // اسم الحساب اللي اتفحص عليه — عشان المستخدم يعرف الفحص اتعمل على إيه
        public string? ProbedAccount { get; set; }
        public Dictionary<string, bool> AttributeWritable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int EffectiveAttributesReturned { get; set; }
        public string? Error { get; set; }
        public string? ErrorDetails { get; set; }

        public bool AllWritable => AttributeWritable.Count > 0 && AttributeWritable.Values.All(v => v);
    }

    public class AdAuthResult
    {
        public bool IsAuthenticated { get; set; }
        public bool IsAdAvailable { get; set; } = true;
        public AdUserDetails? Details { get; set; }
        public string? ErrorSubCode { get; set; }
    }

    public class AdUserDetails
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
    }

    public class AdHealthResult
    {
        public bool IsReachable { get; set; }
        public string? Error { get; set; }
    }

    public class ADServiceAccountStatus
    {
        public bool BindSuccessful { get; set; }
        public string? Error { get; set; }
    }

    public class ADPasswordPolicyResult
    {
        public bool DomainPoliciesFound { get; set; }
        public int MinLength { get; set; } = 8;
        public bool ComplexityEnabled { get; set; } = true;
        public int PwdHistoryLength { get; set; }
        public string? Error { get; set; }
    }

    public class ADPasswordCompatibilityResult
    {
        public string Password { get; set; } = string.Empty;
        public int Length { get; set; }
        public ADPasswordPolicyResult? Policy { get; set; }
        public bool MeetsMinimumLength { get; set; }
        public bool MeetsComplexity { get; set; }
        public bool HasUpperCase { get; set; }
        public bool HasLowerCase { get; set; }
        public bool HasDigit { get; set; }
        public bool HasSpecialChar { get; set; }
        public bool Compatible { get; set; }
        public List<string>? Issues { get; set; }
    }

    public class ADCreateUserRequest
    {
        public string SamAccountName { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? GivenName { get; set; }
        public string? Initials { get; set; }
        public string? Surname { get; set; }
        public string TargetOu { get; set; } = string.Empty;
        public int UserAccountControl { get; set; } = 512;
        public string? Description { get; set; }

        // خصائص معيار جامعة نجران لحسابات الطلاب
        public string? EmployeeId { get; set; }   // employeeID ← رقم الهوية
        public string? Mobile { get; set; }       // mobile     ← رقم الجوال
        public string? Company { get; set; }      // company    ← الكلية بالعربي
        public string? Department { get; set; }   // department ← القسم بالعربي
    }

    public class ADOperationResult
    {
        public string Operation { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ErrorDetails { get; set; }
        public string? StackTrace { get; set; }
        public string? DistinguishedName { get; set; }
    }

    public class ADReadUserResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? DistinguishedName { get; set; }
        // employeeID / mobile / company — بيتقروا للاستيراد وفحص التطابق
        public string? EmployeeId { get; set; }
        public string? Mobile { get; set; }
        public string? Company { get; set; }
        public string SamAccountName { get; set; } = string.Empty;
        public string? UserPrincipalName { get; set; }
        public string? DisplayName { get; set; }
        public string? GivenName { get; set; }
        public string? Surname { get; set; }
        public string? Description { get; set; }
        public string? Department { get; set; }
        public string? ExtensionAttribute1 { get; set; }
        public int UserAccountControl { get; set; }
        public bool AccountEnabled { get; set; }
        public List<string>? MemberOf { get; set; }
        public Dictionary<string, List<string>>? AttributesRaw { get; set; }
    }

    public class ADMoveUserResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ErrorDetails { get; set; }
        public string? NewDistinguishedName { get; set; }
    }

    public class ADSearchUsersResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int TotalResults { get; set; }
        public List<ADSearchUserEntry> Users { get; set; } = new();
    }

    public class ADSearchUserEntry
    {
        public string DistinguishedName { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UserPrincipalName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public bool AccountEnabled { get; set; }
    }
}
