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
  reprovisioned:          { ar: 'إعادة ضبط الحساب',            en: 'Account re-aligned' },
  enabled:                { ar: 'تم التفعيل',                  en: 'Enabled' },
  disabled:               { ar: 'تم التعطيل',                  en: 'Disabled' },
  disable_failed:         { ar: 'فشل تعطيل الحساب',            en: 'Disable failed' },
  password_reset:         { ar: 'إعادة تعيين كلمة المرور',     en: 'Password reset' },
  extension_attrs_synced: { ar: 'تحديث بيانات السكن في الدليل', en: 'Housing data updated in AD' },
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

// أيقونات صغيرة للأزرار — inline عشان ما نحمّلش ملف أيقونات لصورتين.
var HM_SVG = {
  details: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>',
  log:     '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><line x1="8" y1="13" x2="16" y2="13"/><line x1="8" y1="17" x2="13" y2="17"/></svg>',
  user:    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
  lock:    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>',
  ban:     '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="4.9" y1="4.9" x2="19.1" y2="19.1"/></svg>',
  check:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>',
  align:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/></svg>',
  upload:  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>'
};

// خانة في نافذة إدارة الحساب. ltr للنصوص اللاتينية (اسم الدخول، DN، التواريخ):
// <bdi> يعزل ترتيب حروفها من غير ما يقلب محاذاة السطر.
function acctField(label, valueHtml, opts) {
  opts = opts || {};
  var inner = opts.ltr ? '<bdi>' + valueHtml + '</bdi>' : valueHtml;
  if (opts.mono) inner = '<code class="acct-mono"><bdi>' + valueHtml + '</bdi></code>';
  return '<div class="acct-f' + (opts.wide ? ' wide' : '') + '">' +
           '<span class="k">' + label + '</span>' +
           '<span class="v">' + inner + '</span>' +
         '</div>';
}

// بطاقة إجراء: الوصف قبل الزر. الموظف يقرأ أثر الإجراء قبل أن يضغطه —
// وده اللي كان ناقص: أربعة أزرار بأربعة ألوان بلا كلمة تشرح الفرق بينها.
function acctAction(desc, btnClass, icon, label, onclick) {
  return '<div class="acct-action' + (btnClass === 'danger' ? ' warn' : '') + '">' +
           '<p>' + desc + '</p>' +
           '<button class="sbtn ' + btnClass + '" onclick="' + onclick + '">' + icon +
             '<span>' + label + '</span></button>' +
         '</div>';
}

// UAC رقم خام في الدليل. عرضه وحده لا يفيد أحدًا، فنكتب معناه بجانبه.
function acctUac(v) {
  var n = parseInt(v, 10);
  if (isNaN(n)) return '-';
  var names = { 512: 'hs_uacNormal', 514: 'hs_uacDisabled',
                66048: 'hs_uacNoExpire', 66050: 'hs_uacDisabledNoExpire' };
  var k = names[n];
  return k ? (n + ' \u2014 ' + t(k)) : String(n);
}

function acctGender(g) {
  if (g === 'male') return t('male');
  if (g === 'female') return t('female');
  return g || '-';
}

var API = '/api/HousingAccountManagement';

// ⚠️ كان الملف يبعت Authorization: Bearer من localStorage. الشاشة نفسها MVC
//    ومصادقتها بالكوكي، فكان عندنا هويتان بعمرين مختلفين: الصفحة تفتح بالكوكي
//    الحديث بينما نداءات الـ API تُرفَض بتوكن قديم — نفس الشاشة تعمل مرة وترفض
//    مرة. الكوكي يُرسَل تلقائيًا مع كل نداء لنفس الأصل، فهوية واحدة تكفي.
function apiHeaders() {
  return { 'Content-Type': 'application/json' };
}

function showToast(msg, type) {
  var t = document.createElement('div');
  t.style.cssText = 'position:fixed;top:20px;left:50%;transform:translateX(-50%);z-index:9999;padding:12px 24px;border-radius:8px;font-size:14px;font-weight:600;box-shadow:0 4px 12px rgba(0,0,0,0.15);transition:opacity 0.3s;max-width:400px;text-align:center';
  t.style.background = type === 'error' ? '#fef3f2' : type === 'success' ? '#dff6e7' : '#fef0c7';
  t.style.color = type === 'error' ? '#b42318' : type === 'success' ? '#067647' : '#93370d';
  t.style.border = '1px solid ' + (type === 'error' ? '#fecdca' : type === 'success' ? '#a9efc5' : '#fedf89');
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
      '<td><bdi>' + hmEsc(a.student_id || '-') + '</bdi></td>' +
      '<td>' + hmEsc(a.full_name_english || a.full_name || '-') + '</td>' +
      '<td>' + hmEsc(collegeName(a.college) || '-') + '</td>' +
      '<td>' + statusBadge + '</td>' +
      // ⚠️ كان style="direction:ltr" مع حساب المحاذاة يدويًا لكل لغة. تعيين dir
      //    على الخانة يقلب معنى محاذاتها، فكان لا بد من استثناء مكتوب بالإيد.
      //    <bdi> يعزل ترتيب الحروف وحده، والمحاذاة تبقى تابعة لاتجاه الصفحة.
      '<td><bdi>' + hmEsc(a.ad_username || '-') + '</bdi></td>' +
      '<td><bdi>' + hmEsc(lastSync) + '</bdi></td>' +
      '<td><div class="ad-actions">' +
        '<button class="abtn primary" onclick="openDetails(' + a.id + ')">' + HM_SVG.details + '<span>' + t('details') + '</span></button>' +
        '<button class="abtn" onclick="openLifecycle(' + a.id + ')">' + HM_SVG.log + '<span>' + t('lifecycleLog') + '</span></button>' +
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
    var statusBadge = '<span class="badge ' + getBadgeClass(s.ad_status) + '">' + getStatusText(s.ad_status) + '</span>';
    var html = '';

    // ---- ترويسة: مين هذا الحساب ولمن. كانت الشاشة تبدأ بجدول UPN/DN بلا اسم.
    html += '<div class="acct-head">' +
              '<span class="av">' + HM_SVG.user + '</span>' +
              '<span class="who">' +
                '<span class="u"><bdi>' + hmEsc((ad && ad.samAccountName) || s.ad_username || t('adNoAccount')) + '</bdi></span>' +
                '<span class="s">' + hmEsc(s.full_name || s.full_name_english || '-') +
                  ' \u00B7 <bdi>' + hmEsc(s.student_id || '-') + '</bdi></span>' +
              '</span>' +
              statusBadge +
            '</div>';

    if (ad) {
      var groups = (ad.memberOf && ad.memberOf.length)
        ? ad.memberOf.map(function (g) { return '<span class="group-tag"><bdi>' + hmEsc(g) + '</bdi></span>'; }).join('')
        : '-';

      html += '<div class="acct-sec">' +
                '<h4>' + t('hs_secDirectory') + '</h4>' +
                '<div class="acct-grid">' +
                  acctField(t('adUsername'), hmEsc(ad.samAccountName || '-'), { ltr: true }) +
                  acctField(t('hs_upn'), hmEsc(ad.userPrincipalName || '-'), { ltr: true }) +
                  acctField(t('adAccountStatus'), statusBadge) +
                  acctField(t('adLastSync'), hmEsc(s.ad_last_sync_at ? formatDate(s.ad_last_sync_at) : '-'), { ltr: true }) +
                  acctField(t('hs_groups'), groups, { wide: true }) +
                '</div>' +
                // مطويّة: الفني يفتحها عند العطل، والموظف لا تزاحمه كل يوم.
                '<details class="acct-tech"><summary>' + t('hs_techDetails') + '</summary><div class="in">' +
                  '<div class="acct-grid">' +
                    acctField(t('hs_dn'), hmEsc(ad.distinguishedName || '-'), { mono: true, wide: true }) +
                    acctField(t('hs_uac'), hmEsc(acctUac(ad.userAccountControl))) +
                  '</div>' +
                '</div></details>' +
              '</div>';
    } else {
      html += '<div class="acct-sec"><h4>' + t('hs_secDirectory') + '</h4>' +
                '<div class="acct-empty">' + t('adNoAccount') + '</div></div>';
    }

    // ---- معلومات الطالب
    html += '<div class="acct-sec">' +
              '<h4>' + t('personalInfo') + '</h4>' +
              '<div class="acct-grid">' +
                acctField(t('studentId'), hmEsc(s.student_id || '-'), { ltr: true }) +
                acctField(t('fullName'), hmEsc(s.full_name || '-')) +
                acctField(t('fullNameEnglish'), hmEsc(s.full_name_english || '-'), { ltr: true }) +
                acctField(t('college'), hmEsc(collegeName(s.college) || '-')) +
                acctField(t('gender'), hmEsc(acctGender(s.gender))) +
                acctField(t('academicLevel'), hmEsc(s.academic_level || '-')) +
                acctField(t('housingBuilding'), hmEsc(s.housing_building || '-')) +
                acctField(t('roomNumber'), hmEsc(s.room_number || '-')) +
              '</div>' +
            '</div>';

    // ---- الإجراءات: كل واحد ببطاقة تشرح أثره.
    //      الإخفاء هنا للراحة فقط — الصلاحية تُفحص على السيرفر في كل نداء.
    if (ad) {
      var acts = '';
      if (hmCan('housing.manageAccounts')) {
        acts += (s.ad_status === 'enabled')
          ? acctAction(t('hs_actDisableDesc'), 'danger', HM_SVG.ban, t('disableAccount'),
                       "performAction(" + studentId + ",'disable')")
          : acctAction(t('hs_actEnableDesc'), 'ok', HM_SVG.check, t('enableAccount'),
                       "performAction(" + studentId + ",'enable')");
        acts += acctAction(t('hs_actPwdDesc'), '', HM_SVG.lock, t('resetPassword'),
                           "showResetPassword(" + studentId + ")");
        acts += acctAction(t('hs_actRealignDesc'), '', HM_SVG.align, t('reProvision'),
                           "performAction(" + studentId + ",'re-provision')");
      }
      if (hmCan('housing.syncAd')) {
        acts += acctAction(t('hs_actSyncDesc'), '', HM_SVG.upload, t('syncAttributes'),
                           "performAction(" + studentId + ",'sync-attrs')");
      }
      if (acts) {
        html += '<div class="acct-sec"><h4>' + t('hs_secActions') + '</h4>' +
                  '<div class="acct-actions">' + acts + '</div></div>';
      }
    }

    body.innerHTML = html;
  } catch (e) {
    body.innerHTML = '<p style="color:var(--red)">' + t('apiError') + ': ' + hmEsc(e.message) + '</p>';
  }
}

async function performAction(studentId, action) {
  var confirmMsgs = {
    'enable': t('confirmEnable'),
    'disable': t('confirmDisable'),
    're-provision': t('confirmReProvision'),
    'sync-attrs': t('confirmSync')
  };

  // الأحمر للإجراء الذي يقطع خدمة عن الطالب فقط. «إعادة ضبط الحساب» إجراء
  // تصحيحي وليس خطرًا، وتلوينه أحمر يعوّد المستخدم على تجاهل اللون الأحمر.
  var dangerous = (action === 'disable');
  if (!await NuhDialog.confirm({ message: confirmMsgs[action] || t('confirm'), danger: dangerous })) return;

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

// ⚠️ كانت تمسح محتوى النافذة وتضع حقلًا عاريًا وزرّين بلا أي إطار أو عنوان،
//    فتبدو كأن الصفحة تعطّلت وظهر نموذج من صفحة أخرى. الآن بطاقة لها ترويسة
//    وزر رجوع واضح، بنفس لغة باقي الشاشة.
function showResetPassword(studentId) {
  var body = document.getElementById('detailsModalBody');
  body.innerHTML =
    '<div class="acct-head">' +
      '<span class="av">' + HM_SVG.lock + '</span>' +
      '<span class="who">' +
        '<span class="u">' + t('resetPassword') + '</span>' +
        '<span class="s">' + t('hs_actPwdDesc') + '</span>' +
      '</span>' +
    '</div>' +
    '<div class="acct-sec">' +
      '<label class="acct-f" style="display:block">' +
        '<span class="k">' + t('newPassword') + '</span>' +
        '<input type="password" id="newPwd" autocomplete="new-password" ' +
          'style="width:100%;height:44px;padding:0 12px;border:1.5px solid var(--gray-300);' +
          'border-radius:10px;font-family:inherit;font-size:14px;background:var(--gray-50,#f5f5f6)" ' +
          'data-i18n-placeholder="newPasswordPlaceholder">' +
      '</label>' +
      '<p style="font-size:11.5px;color:var(--gray-500);line-height:1.75;margin-top:8px">' +
        (t('passwordMinLength') || '') + '</p>' +
    '</div>' +
    '<div style="display:flex;gap:10px">' +
      '<button class="sbtn primary" onclick="resetPassword(' + studentId + ')">' +
        HM_SVG.check + '<span>' + t('confirm') + '</span></button>' +
      '<button class="sbtn" onclick="openDetails(' + studentId + ')">' + t('cancel') + '</button>' +
    '</div>';
  var f = document.getElementById('newPwd');
  if (f) {
    // Enter داخل الحقل ينفّذ — كان لازم الوصول للزر بالفأرة كل مرة
    f.addEventListener('keydown', function (e) { if (e.key === 'Enter') resetPassword(studentId); });
    setTimeout(function () { f.focus(); }, 40);
  }
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
        // نفس قاعدة الجدول الرئيسي: <bdi> بدل direction:ltr على الخانة، وتهريب
        // القيم لأنها قادمة من الدليل وتدخل الصفحة مباشرة.
        '<td><bdi>' + hmEsc(u.samAccountName || '-') + '</bdi></td>' +
        '<td>' + hmEsc(u.displayName || '-') + '</td>' +
        '<td>' + enabledBadge + '</td>' +
        '<td><bdi>' + hmEsc(u.userPrincipalName || '-') + '</bdi></td>' +
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
