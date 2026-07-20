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

    if (!accounts || accounts.length === 0) {
      empty.style.display = 'block';
      return;
    }

    accounts.forEach(function(a) {
      var lastSync = a.ad_last_sync_at ? formatDate(a.ad_last_sync_at) : '-';
      var statusBadge = '<span class="badge ' + getBadgeClass(a.ad_status) + '">' + getStatusText(a.ad_status) + '</span>';
      tbody.innerHTML += '<tr>' +
        '<td>' + (a.student_id || '-') + '</td>' +
        '<td>' + (a.full_name_english || a.full_name || '-') + '</td>' +
        '<td>' + (a.college || '-') + '</td>' +
        '<td>' + statusBadge + '</td>' +
        '<td style="direction:ltr;text-align:' + (document.documentElement.dir === 'ltr' ? 'left' : 'right') + '">' + (a.ad_username || '-') + '</td>' +
        '<td>' + lastSync + '</td>' +
        '<td><div class="ad-actions">' +
          '<button class="btn btn-primary btn-sm" onclick="openDetails(' + a.id + ')" data-i18n="details">تفاصيل</button>' +
          '<button class="btn btn-outline btn-sm" onclick="openLifecycle(' + a.id + ')" data-i18n="lifecycleLog">السجل</button>' +
        '</div></td>' +
      '</tr>';
    });
  } catch (e) {
    loading.style.display = 'none';
    empty.style.display = 'block';
    empty.innerHTML = '<p>' + t('apiError') + ': ' + e.message + '</p>';
  }
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

      html += '<div style="margin-top:16px;display:flex;gap:8px;flex-wrap:wrap">';
      if (s.ad_status === 'enabled') {
        html += '<button class="btn btn-danger btn-sm" onclick="performAction(' + studentId + ',\'disable\')">' + t('disableAccount') + '</button>';
      } else {
        html += '<button class="btn btn-success btn-sm" onclick="performAction(' + studentId + ',\'enable\')">' + t('enableAccount') + '</button>';
      }
      html += '<button class="btn btn-warning btn-sm" onclick="showResetPassword(' + studentId + ')">' + t('resetPassword') + '</button>';
      html += '<button class="btn btn-outline btn-sm" onclick="performAction(' + studentId + ',\'re-provision\')">' + t('reProvision') + '</button>';
      html += '<button class="btn btn-outline btn-sm" onclick="performAction(' + studentId + ',\'sync-attrs\')">' + t('syncAttributes') + '</button>';
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
      '<div class="detail-item"><label>' + t('college') + '</label><span>' + (s.college || '-') + '</span></div>' +
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

    var html = '<div style="max-height:400px;overflow-y:auto">';
    logs.forEach(function(l) {
      var iconMap = {
        'provisioned': 'created',
        'reprovisioned': 'reprovisioned',
        'enabled': 'enabled',
        'disabled': 'disabled',
        'password_reset': 'password_reset',
        'extension_attrs_synced': 'extension_attrs_synced'
      };
      var iconClass = iconMap[l.action] || '';
      var actionLabel = {
        'provisioned': t('actionProvisioned'),
        'reprovisioned': t('actionReprovisioned'),
        'enabled': t('actionEnabled'),
        'disabled': t('actionDisabled'),
        'password_reset': t('actionPasswordReset'),
        'extension_attrs_synced': t('actionExtensionAttrsSynced')
      }[l.action] || l.action;

      html += '<div class="log-entry">' +
        '<div class="log-icon ' + iconClass + '">' +
          (l.action === 'enabled' || l.action === 'provisioned' || l.action === 'reprovisioned' ? '✓' :
           l.action === 'disabled' ? '✗' :
           l.action === 'password_reset' ? '🔑' : '🔄') +
        '</div>' +
        '<div class="log-details">' +
          '<div class="log-action">' + actionLabel + '</div>' +
          '<div class="log-meta">' +
            (l.performerName ? t('by') + ' ' + l.performerName + ' | ' : '') +
            formatDate(l.performedAt) +
            (l.details ? ' | ' + l.details : '') +
            (l.ipAddress ? ' | IP: ' + l.ipAddress : '') +
          '</div>' +
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
