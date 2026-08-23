-- ============================================================================
--  المسار المحفوظ عندنا لكل وحدة عليها شارة «وحدة تنظيمية غير مطابقة».
--
--  الغرض: نقارن اللي مخزَّن في قاعدة بياناتنا باللي في الدليل فعلًا.
--  لو الاتنين مختلفين، يبقى ده سبب خطأ «The object does not exist».
-- ============================================================================
SELECT
    u.Id,
    u.AdAccount,
    u.SyncState,
    u.AdDistinguishedName                              AS [المسار المحفوظ عندنا],
    CASE
        WHEN u.AdDistinguishedName LIKE '%OU=MALE,%'   THEN 'MALE'
        WHEN u.AdDistinguishedName LIKE '%OU=FEMALE,%' THEN 'FEMALE'
        ELSE '(غير مقسّم)'
    END                                                AS [القسم حسب المحفوظ],
    o.FullNameAr                                       AS [الشاغل الحالي],
    o.Gender                                           AS [جنس الشاغل],
    u.LastSyncedAt,
    u.LastSyncError
FROM dbo.FacultyUnits u
LEFT JOIN dbo.FacultyOccupancies o
       ON o.UnitId = u.Id AND o.EndDate IS NULL
WHERE u.AdAccount IN ('bu6ap20', 'bu10ap17')
ORDER BY u.AdAccount;

-- ============================================================================
--  وللمقارنة، شغّل ده في PowerShell على وحدة تحكّم الدومين:
--
--      Get-ADUser -Filter "SamAccountName -eq 'bu6ap20'"  -Properties DistinguishedName |
--          Select-Object SamAccountName, DistinguishedName
--      Get-ADUser -Filter "SamAccountName -eq 'bu10ap17'" -Properties DistinguishedName |
--          Select-Object SamAccountName, DistinguishedName
-- ============================================================================
