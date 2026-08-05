// صلاحيات المستخدم بتتحقن من الصفحة (Views/Housing/Index.cshtml).
// لو مش موجودة (صفحة قديمة) بنرجع false — أأمن من إظهار زرار مالوش صلاحية.
function hmCan(p) {
  var list = (typeof window !== 'undefined' && Array.isArray(window.NUH_PERMS)) ? window.NUH_PERMS : [];
  return list.indexOf(p) !== -1;
}

// تهريب HTML — اللياوت بيعرّف escHtml، وبنستخدمه لو موجود عشان نفضل متسقين.
function hmEsc(v) {
  if (typeof escHtml === 'function') return escHtml(v);
  return String(v == null ? '' : v)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// أسماء إجراءات سجل دورة الحياة.
// ⚠️ housing_transfer و left_housing و disable_failed مكانش ليهم اسم، فكانوا
//    بيظهروا للمستخدم بالكود الخام زي ما هو مكتوب في قاعدة البيانات.
//    بنجرّب ملف الترجمة الأول، ولو المفتاح مش موجود بنرجع للنص المكتوب هنا.
var HM_ACTION_KEYS = {
  provisioned: 'actionProvisioned',
  reprovisioned: 'actionReprovisioned',
  enabled: 'actionEnabled',
  disabled: 'actionDisabled',
  password_reset: 'actionPasswordReset',
  extension_attrs_synced: 'actionExtensionAttrsSynced'
};

var HM_ACTION_TEXT = {
  provisioned:            { ar: 'تم إنشاء الحساب',            en: 'Account created' },
  reprovisioned:          { ar: 'إعادة إنشاء الحساب',          en: 'Account re-provisioned' },
  enabled:                { ar: 'تم التفعيل',                  en: 'Enabled' },
  disabled:               { ar: 'تم التعطيل',                  en: 'Disabled' },
  disable_failed:         { ar: 'فشل تعطيل الحساب',            en: 'Disable failed' },
  password_reset:         { ar: 'إعادة تعيين كلمة المرور',     en: 'Password reset' },
  extension_attrs_synced: { ar: 'مزامنة الخصائص الإضافية',     en: 'Extension attributes synced' },
  housing_transfer:       { ar: 'نقل سكن',                     en: 'Housing transfer' },
  left_housing:           { ar: 'ترك الإسكان',                 en: 'Left housing' }
};

function hmActionLabel(action) {
  var key = HM_ACTION_KEYS[action];
  if (key) {
    var v = t(key);
    if (v && v !== key) return v;
  }
  var txt = HM_ACTION_TEXT[action];
  if (!txt) return action;
  return (localStorage.getItem('uiLanguage') === 'en') ? txt.en : txt.ar;
}

var API = '/api/HousingAccountManagement';

function getToken() {
  return localStorage.getItem('staffToken') || localStorage.getItem('studentToken');
}

function apiHeaders() {
  return {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer ' + getToken()
  };
}

function showToast(msg, type) {
  var t = document.createElement('div');
  t.style.cssText = 'position:fixed;top:20px;left:50%;transform:translateX(-50%);z-index:9999;padding:12px 24px;border-radius:8px;font-size:14px;font-weight:600;box-shadow:0 4px 12px rgba(0,0,0,0.15);transition:opacity 0.3s;max-width:400px;text-align:center';
  t.style.background = type === 'error' ? '#FEF2F2' : type === 'success' ? '#E1F5EE' : '#FEF3C7';
  t.style.color = type === 'error' ? '#991B1B' : type === 'success' ? '#0F6E56' : '#92400E';
  t.style.border = '1px solid ' + (type === 'error' ? '#FECACA' : type === 'success' ? '#A7F3D0' : '#FDE68A');
  t.textContent = msg;
  document.body.appendChild(t);
  setTimeout(function() { t.style.opacity = '0'; setTimeout(function() { t.remove(); }, 300); }, 3000);
}

function closeModal(id) {
  document.getElementById(id).classList.remove('show');
}

function openModal(id) {
  document.getElementById(id).classList.add('show');
}

function formatDate(d) {
  if (!d) return '-';
  var dt = new Date(d);
  return dt.toLocaleString();
}

function getBadgeClass(status) {
  if (status === 'enabled') return 'badge-enabled';
  if (status === 'disabled') return 'badge-disabled';
  return 'badge-unknown';
}

function getStatusText(status) {
  if (status === 'enabled') return t('adEnabled');
  if (status === 'disabled') return t('adDisabled');
  return t('adUnknown');
}

async function checkAuth(res) {
  if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('studentToken'); localStorage.removeItem('staffUser'); localStorage.removeItem('studentUser'); localStorage.removeItem('token'); localStorage.removeItem('user'); window.location.replace('login.html'); return true; }
  return false;
}

async function loadStats() {
  try {
    var res = await fetch(API + '/stats', { headers: apiHeaders() });
    if (await checkAuth(res)) return;
    var data = await res.json();
    var grid = document.getElementById('statsGrid');
    grid.innerHTML = '';
    var stats = [
      { label: t('totalAccounts'), value: data.withAccounts || data.with_accounts || 0 },
      { label: t('accountsEnabled'), value: data.enabled || 0 },
      { label: t('accountsDisabled'), value: data.disabled || 0 },
      { label: t('syncedToday'), value: data.syncedLast24h || data.synced_last_24h || 0 },
      { label: t('studentsWithoutAccounts'), value: data.withoutUsername || data.without_username || 0 },
      { label: t('totalStudents'), value: data.totalStudents || data.total_students || 0 }
    ];
    stats.forEach(function(s) {
      grid.innerHTML += '<div class="stat-card"><div class="stat-label">' + s.label + '</div><div class="stat-value">' + s.value + '</div></div>';
    });
  } catch (e) {
    console.error('Failed to load stats', e);
  }
}

// نخزّن الصفوف المحمّلة علشان الترتيب يتم على الجهة (client-side) — الشاشة بتحمّل الكل مرة واحدة
var housingAccounts = [];
var housingSort = { by: '', asc: false };

async function loadAccounts() {
  var tbody = document.getElementById('accountsTableBody');
  var loading = document.getElementById('loadingIndicator');
  var empty = document.getElementById('emptyState');
  loading.style.display = 'block';
  empty.style.display = 'none';
  tbody.innerHTML = '';

  try {
    var statusFilter = document.getElementById('statusFilter').value;
    var url = API + '?';
    if (statusFilter) url += 'status=' + encodeURIComponent(statusFilter);

    var res = await fetch(url, { headers: apiHeaders() });
    if (!res.ok) throw new Error('HTTP ' + res.status);

    var accounts = await res.json();
    loading.style.display = 'none';
    housingAccounts = accounts || [];

    if (housingAccounts.length === 0) {
      empty.style.display = 'block';
      return;
    }

    renderHousingRows();
  } catch (e) {
    loading.style.display = 'none';
    empty.style.display = 'block';
    empty.innerHTML = '<p>' + t('apiError') + ': ' + e.message + '</p>';
  }
}

// مقارنة للترتيب: التاريخ رقميًا، والباقي نصيًا مع دعم الأرقام والعربي
function housingCmp(a, b) {
  var f = housingSort.by, asc = housingSort.asc;
  if (f === 'ad_last_sync_at') {
    var da = a[f] ? new Date(a[f]).getTime() : 0;
    var db = b[f] ? new Date(b[f]).getTime() : 0;
    return asc ? da - db : db - da;
  }
  var va = (a[f] == null ? '' : String(a[f]));
  var vb = (b[f] == null ? '' : String(b[f]));
  var r = va.localeCompare(vb, undefined, { numeric: true, sensitivity: 'base' });
  return asc ? r : -r;
}

function renderHousingRows() {
  var tbody = document.getElementById('accountsTableBody');
  var rows = housingAccounts.slice();
  if (housingSort.by) rows.sort(housingCmp);
  var html = '';
  rows.forEach(function(a) {
    var lastSync = a.ad_last_sync_at ? formatDate(a.ad_last_sync_at) : '-';
    var statusBadge = '<span class="badge ' + getBadgeClass(a.ad_status) + '">' + getStatusText(a.ad_status) + '</span>';
    html += '<tr>' +
      '<td>' + (a.student_id || '-') + '</td>' +
      '<td>' + (a.full_name_english || a.full_name || '-') + '</td>' +
      '<td>' + (collegeName(a.college) || '-') + '</td>' +
      '<td>' + statusBadge + '</td>' +
      '<td style="direction:ltr;text-align:' + (document.documentElement.dir === 'ltr' ? 'left' : 'right') + '">' + (a.ad_username || '-') + '</td>' +
      '<td>' + lastSync + '</td>' +
      '<td><div class="ad-actions">' +
        '<button class="btn btn-primary btn-sm" onclick="openDetails(' + a.id + ')">' + t('details') + '</button>' +
        '<button class="btn btn-outline btn-sm" onclick="openLifecycle(' + a.id + ')">' + t('lifecycleLog') + '</button>' +
      '</div></td>' +
    '</tr>';
  });
  tbody.innerHTML = html;
  updateHousingSortIndicators();
}

function sortHousing(field) {
  if (housingSort.by === field) { housingSort.asc = !housingSort.asc; }
  else { housingSort.by = field; housingSort.asc = true; }
  renderHousingRows();
}

function updateHousingSortIndicators() {
  ['student_id', 'full_name_english', 'college', 'ad_status', 'ad_username', 'ad_last_sync_at'].forEach(function (f) {
    var el = document.getElementById('hsort-' + f);
    if (el) el.textContent = (housingSort.by === f) ? (housingSort.asc ? ' ▲' : ' ▼') : '';
  });
}

async function openDetails(studentId) {
  var body = document.getElementById('detailsModalBody');
  body.innerHTML = '<div class="loading"></div>';
  openModal('detailsModal');

  try {
    var res = await fetch(API + '/' + studentId, { headers: apiHeaders() });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    var data = await res.json();

    var s = data.student || {};
    var ad = data.adDetails;

    var html = '';

    if (ad) {
      html += '<h4 style="margin-bottom:12px;font-size:15px">AD ' + t('accountManagement') + '</h4>';
      html += '<div class="detail-grid">' +
        '<div class="detail-item"><label>' + t('adUsername') + '</label><span>' + (ad.samAccountName || '-') + '</span></div>' +
        '<div class="detail-item"><label>UPN</label><span style="direction:ltr;display:inline-block">' + (ad.userPrincipalName || '-') + '</span></div>' +
        '<div class="detail-item"><label>DN</label><span style="direction:ltr;display:inline-block;font-size:11px;word-break:break-all">' + (ad.distinguishedName || '-') + '</span></div>' +
        '<div class="detail-item"><label>' + t('adAccountStatus') + '</label><span class="badge ' + getBadgeClass(s.ad_status) + '">' + getStatusText(s.ad_status) + '</span></div>' +
        '<div class="detail-item"><label>' + t('adLastSync') + '</label><span>' + (s.ad_last_sync_at ? formatDate(s.ad_last_sync_at) : '-') + '</span></div>' +
        '<div class="detail-item"><label>UAC</label><span>' + (ad.userAccountControl || '-') + '</span></div>' +
      '</div>';

      if (ad.memberOf && ad.memberOf.length > 0) {
        html += '<div style="margin-top:12px"><label style="font-size:11px;color:var(--gray-500);display:block;margin-bottom:4px">' + t('groupDn') + '</label>';
        html += ad.memberOf.map(function(g) { return '<span class="badge badge-unknown" style="margin:2px">' + g + '</span>'; }).join('');
        html += '</div>';
      }

      // الأزرار حسب الصلاحية — الفحص الحقيقي على السيرفر في كل نداء،
      // وده بيمنع بس إن المستخدم يضغط زرار هيرجّع "غير مصرح".
      html += '<div style="margin-top:16px;display:flex;gap:8px;flex-wrap:wrap">';
      if (hmCan('housing.manageAccounts')) {
        if (s.ad_status === 'enabled') {
          html += '<button class="btn btn-danger btn-sm" onclick="performAction(' + studentId + ',\'disable\')">' + t('disableAccount') + '</button>';
        } else {
          html += '<button class="btn btn-success btn-sm" onclick="performAction(' + studentId + ',\'enable\')">' + t('enableAccount') + '</button>';
        }
        html += '<button class="btn btn-warning btn-sm" onclick="showResetPassword(' + studentId + ')">' + t('resetPassword') + '</button>';
        html += '<button class="btn btn-outline btn-sm" onclick="performAction(' + studentId + ',\'re-provision\')">' + t('reProvision') + '</button>';
      }
      if (hmCan('housing.syncAd')) {
        html += '<button class="btn btn-outline btn-sm" onclick="performAction(' + studentId + ',\'sync-attrs\')">' + t('syncAttributes') + '</button>';
      }
      html += '</div>';
    } else {
      html += '<p style="color:var(--gray-500)">' + t('adNoAccount') + '</p>';
    }

    html += '<hr style="margin:16px 0;border-color:var(--gray-100)">';
    html += '<h4 style="margin-bottom:12px;font-size:15px">' + t('personalInfo') + '</h4>';
    html += '<div class="detail-grid">' +
      '<div class="detail-item"><label>' + t('studentId') + '</label><span>' + (s.student_id || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('fullNameEnglish') + '</label><span>' + (s.full_name_english || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('fullName') + '</label><span>' + (s.full_name || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('college') + '</label><span>' + (collegeName(s.college) || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('gender') + '</label><span>' + (s.gender || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('academicLevel') + '</label><span>' + (s.academic_level || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('housingBuilding') + '</label><span>' + (s.housing_building || '-') + '</span></div>' +
      '<div class="detail-item"><label>' + t('roomNumber') + '</label><span>' + (s.room_number || '-') + '</span></div>' +
    '</div>';

    body.innerHTML = html;
  } catch (e) {
    body.innerHTML = '<p style="color:var(--red)">' + t('apiError') + ': ' + e.message + '</p>';
  }
}

async function performAction(studentId, action) {
  var confirmMsgs = {
    'enable': t('confirmEnable'),
    'disable': t('confirmDisable'),
    're-provision': t('confirmReProvision'),
    'sync-attrs': t('confirmSync')
  };

  if (!confirm(confirmMsgs[action] || t('confirm'))) return;

  try {
    var res = await fetch(API + '/' + studentId + '/' + action, {
      method: 'POST',
      headers: apiHeaders()
    });

    var data = await res.json();
    if (res.ok) {
      showToast(data.message || t('success'), 'success');
      closeModal('detailsModal');
      loadAccounts();
      loadStats();
    } else {
      showToast(data.message || data.error || t('errorOccurred'), 'error');
    }
  } catch (e) {
    showToast(t('apiError'), 'error');
  }
}

function showResetPassword(studentId) {
  var body = document.getElementById('detailsModalBody');
  body.innerHTML = '<h4 style="margin-bottom:16px;font-size:15px">' + t('resetPassword') + '</h4>' +
    '<div style="margin-bottom:12px">' +
      '<label style="display:block;font-size:13px;font-weight:600;margin-bottom:4px">' + t('newPassword') + '</label>' +
      '<input type="password" id="newPwd" style="width:100%;padding:10px 12px;border:1px solid var(--gray-300);border-radius:8px;font-family:inherit;font-size:14px" data-i18n-placeholder="newPasswordPlaceholder">' +
    '</div>' +
    '<button class="btn btn-primary" onclick="resetPassword(' + studentId + ')">' + t('confirm') + '</button>' +
    ' <button class="btn btn-outline" onclick="openDetails(' + studentId + ')">' + t('cancel') + '</button>';
}

async function resetPassword(studentId) {
  var pwd = document.getElementById('newPwd').value;
  if (!pwd || pwd.length < 8) {
    showToast(t('passwordMinLength') || 'Password must be at least 8 characters', 'error');
    return;
  }

  try {
    var res = await fetch(API + '/' + studentId + '/reset-password', {
      method: 'POST',
      headers: apiHeaders(),
      body: JSON.stringify({ newPassword: pwd })
    });
    var data = await res.json();
    if (res.ok) {
      showToast(t('success'), 'success');
      closeModal('detailsModal');
    } else {
      showToast(data.message || data.error || t('errorOccurred'), 'error');
    }
  } catch (e) {
    showToast(t('apiError'), 'error');
  }
}

async function openLifecycle(studentId) {
  var body = document.getElementById('lifecycleModalBody');
  body.innerHTML = '<div class="loading"></div>';
  openModal('lifecycleModal');

  try {
    var res = await fetch('/api/students/' + studentId + '/lifecycle', { headers: apiHeaders() });
    if (await checkAuth(res)) return;
    if (!res.ok) throw new Error('HTTP ' + res.status);
    var data = await res.json();
    var logs = data.logs || [];

    if (logs.length === 0) {
      body.innerHTML = '<p style="color:var(--gray-500);text-align:center;padding:20px">' + t('noData') + '</p>';
      return;
    }

    // ⚠️ الكلاسات .log-entry/.log-icon/.log-details مالهاش CSS في المشروع، فكل
    //    سطر كان بيطلع ملزوق في اللي بعده. الستايل اتضاف في Views/Housing/Index.cshtml،
    //    والتفاصيل و«مين نفّذ» والتاريخ و IP بقوا في أسطر منفصلة بدل سطر واحد متصل.
    var ACTION_ICONS = {
      provisioned: '\u2713', reprovisioned: '\u2713', enabled: '\u2713',
      disabled: '\u2715', disable_failed: '!', password_reset: '\u26BF',
      extension_attrs_synced: '\u21BB', housing_transfer: '\u2194', left_housing: '\u2192'
    };

    var html = '<div class="log-list">';
    logs.forEach(function (l) {
      var action = l.action || '';
      var iconClass = action === 'provisioned' ? 'created' : action;
      var actionLabel = hmActionLabel(action);

      var meta = '';
      if (l.performerName) meta += '<span>' + t('by') + ' ' + hmEsc(l.performerName) + '</span>';
      meta += '<span>' + formatDate(l.performedAt) + '</span>';
      if (l.ipAddress) meta += '<span class="log-ip">IP: ' + hmEsc(l.ipAddress) + '</span>';

      html += '<div class="log-entry">' +
          '<div class="log-icon ' + iconClass + '">' + (ACTION_ICONS[action] || '\u21BB') + '</div>' +
          '<div class="log-details">' +
            '<div class="log-action">' + hmEsc(actionLabel) + '</div>' +
            (l.details ? '<div class="log-desc">' + hmEsc(l.details) + '</div>' : '') +
            '<div class="log-meta">' + meta + '</div>' +
          '</div>' +
        '</div>';
    });
    html += '</div>';
    body.innerHTML = html;
  } catch (e) {
    body.innerHTML = '<p style="color:var(--red)">' + t('apiError') + ': ' + e.message + '</p>';
  }
}

function openADSearch() {
  document.getElementById('adSearchInput').value = '';
  document.getElementById('adSearchResults').innerHTML = '';
  openModal('adSearchModal');
}

async function performADSearch() {
  var q = document.getElementById('adSearchInput').value.trim();
  var resultsDiv = document.getElementById('adSearchResults');
  if (!q) return;
  resultsDiv.innerHTML = '<div class="loading"></div>';

  try {
    var res = await fetch(API + '/search?q=' + encodeURIComponent(q) + '&max=50', { headers: apiHeaders() });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    var data = await res.json();
    var users = data.users || [];

    if (users.length === 0) {
      resultsDiv.innerHTML = '<p style="color:var(--gray-500);text-align:center;padding:20px">' + t('noResults') + '</p>';
      return;
    }

    var html = '<table><thead><tr>' +
      '<th>' + t('username') + '</th>' +
      '<th>' + t('fullNameEnglish') + '</th>' +
      '<th>' + t('adAccountStatus') + '</th>' +
      '<th>UPN</th>' +
    '</tr></thead><tbody>';

    users.forEach(function(u) {
      var enabledBadge = u.accountEnabled
        ? '<span class="badge badge-enabled">' + t('adEnabled') + '</span>'
        : '<span class="badge badge-disabled">' + t('adDisabled') + '</span>';
      html += '<tr>' +
        '<td style="direction:ltr">' + u.samAccountName + '</td>' +
        '<td>' + (u.displayName || '-') + '</td>' +
        '<td>' + enabledBadge + '</td>' +
        '<td style="direction:ltr">' + (u.userPrincipalName || '-') + '</td>' +
      '</tr>';
    });

    html += '</tbody></table>';
    html += '<p style="font-size:12px;color:var(--gray-500);margin-top:8px">' + t('total') + ': ' + (data.total || users.length) + '</p>';
    resultsDiv.innerHTML = html;
  } catch (e) {
    resultsDiv.innerHTML = '<p style="color:var(--red)">' + t('apiError') + ': ' + e.message + '</p>';
  }
}

window.onLanguageChange = function(l) {
  loadStats();
  loadAccounts();
};

document.addEventListener('DOMContentLoaded', function() {
  setLang(currentLang || 'ar');
  loadStats();
  loadAccounts();
  document.title = t('housingManagement');
});
