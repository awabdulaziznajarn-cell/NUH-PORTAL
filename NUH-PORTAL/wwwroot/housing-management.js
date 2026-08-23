// صلاحيات المستخدم بتتحقن من الصفحة (Views/Housing/Index.cshtml).
// لو مش موجودة (صفحة قديمة) بنرجع false — أأمن من إظهار زرار مالوش صلاحية.
function hmCan(p) {
  var list = (typeof window !== 'undefined' && Array.isArray(window.NUH_PERMS)) ? window.NUH_PERMS : [];
  return list.indexOf(p) !== -1;
}

// تهريب HTML — التعريف الوحيد في /js/esc.js المحمَّل من التخطيط.
// ⚠️ كان فيه نسخة احتياطية هنا «لو escHtml مش موجودة» — والاحتياطي ده هو
//    بالظبط اللي بيخلّي النسخ تتكاثر: بيفضل مكتوب سنين ومحدّش بيعرف إذا كان
//    بيشتغل ولا لأ، ولو اتصلّحت القاعدة في مكانها الأصلي ما بيتصلّحش هو.
//    الصفحة دي بتتحمّل من التخطيط دايمًا، فالاحتياطي مالوش أي حالة.
function hmEsc(v) { return escHtml(v); }

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
  extension_attrs_synced: { ar: 'تحديث بيانات الإسكان في الـAD', en: 'Housing data updated in AD' },
  housing_transfer:       { ar: 'نقل سكن',                     en: 'Housing transfer' },
  left_housing:           { ar: 'ترك السكن',                   en: 'Left housing' }
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
  t.style.background = type === 'error' ? '#fef3f2' : type === 'success' ? '#dff6e7' : 'var(--gold-pale)';
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
  return NuhFmt.dateTime(dt);
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

// ==========================================================================
//  جدول حسابات السكن — الفلترة والترتيب والترقيم كلهم على الخادم.
//
//  ⚠️ الشاشة كانت بتنادي GET /api/HousingAccountManagement اللي بيرجّع **كل**
//     الحسابات في رد واحد، وترسمهم كلهم، وترتّبهم في المتصفح. ومع كده كان في
//     نقطة /paged موجودة على الخادم من الأصل ومحدش بيناديها.
//
//     والترتيب في المتصفح مش بس مسألة أداء: مع الترقيم بيبقى غلط صريح — بيرتّب
//     الصفحة اللي قدامك بس، فأول اسم أبجديًّا في صفحة ٢ ممكن يسبق آخر اسم في
//     صفحة ١. عشان كده الترتيب اتنقل للخادم بالكامل.
//
//  ⚠️ الحالة في كائن واحد: لو كل فلتر قرا قيمته من عنصره وقت بناء الطلب،
//     يبقى كل فلتر لازم يعرف عن التاني، وأي فلتر جديد يتضاف بعد كده يحتاج
//     تعديل في بناء الطلب.
// ==========================================================================
var housingAccounts = [];

// ==========================================================================
//  حالة الشاشة في الرابط - NuhUrl في js/url-state.js.
//
//  ⚠️ المفارقة اللي كانت هنا: الشاشة كانت بتحفظ **حالة لوحة المزامنة**
//     (مفتوحة/مقفولة) في تخزين المتصفح، وماكانتش بتحفظ البحث ولا الفلتر ولا
//     الصفحة. يعني بتفتكر الزينة وبتنسى الشغل.
// ==========================================================================
var HS_STATUSES = ['', 'enabled', 'disabled', 'unknown'];

var hsState = {
  page: NuhUrl.int('page', 1, 1),
  pageSize: 25,
  filter: NuhUrl.get('q', ''),
  status: NuhUrl.one('status', HS_STATUSES, ''),
  total: 0,
  totalPages: 1
};
var HS_DEFAULTS = { page: 1, q: '', status: '', sort: '', asc: '' };

function hsSyncUrl() {
  NuhUrl.sync({
    page: hsState.page, q: hsState.filter, status: hsState.status,
    sort: hsSort.by(), asc: hsSort.by() ? (hsSort.asc() ? '1' : '0') : ''
  }, HS_DEFAULTS);
}

// حالات الجدول (تحميل / لا نتائج / خطأ) — التعريف الوحيد في /js/table-state.js
var hsTable = NuhTable.bind('accountsTableBody', 7);

// ترتيب الأعمدة — نفس النسخة الوحيدة. الأعمدة معرّفة بـ data-sort في الشاشة.
// ⚠️ الترتيب بيرجّع الصفحة لواحد: لو فضلت على صفحة ٧ بعد ما غيّرت الترتيب،
//    اللي هتشوفه هو الصفحة السابعة من ترتيب جديد — نتيجة صحيحة بس مالهاش
//    معنى للي دوس عشان يشوف الأول أو الآخر.
var hsSort = NuhTable.sort('hsTable', function () { hsState.page = 1; loadAccounts(); },
  { by: NuhUrl.get('sort', ''), asc: NuhUrl.get('asc', '1') === '1' });

function hsUrl() {
  var u = API + '/paged?page=' + hsState.page + '&pageSize=' + hsState.pageSize;
  if (hsState.filter) u += '&filterText=' + encodeURIComponent(hsState.filter);
  if (hsState.status) u += '&status=' + encodeURIComponent(hsState.status);
  u += hsSort.qs();
  return u;
}

async function loadAccounts() {
  hsSyncUrl();
  hsTable.loading();
  try {
    var res = await fetch(hsUrl(), { headers: apiHeaders() });
    if (!res.ok) throw new Error('HTTP ' + res.status);

    var body = await res.json();
    hsTable.done();
    housingAccounts   = body.items || [];
    hsState.total     = body.totalCount || 0;
    hsState.totalPages = body.totalPages || 1;

    renderHousingRows();
    renderHousingPager();
  } catch (e) {
    hsTable.error(t('apiError') + ': ' + e.message);
  }
}

// صفّ الترقيم كامل من نسخة واحدة في النظام — NuhTable.pager
function renderHousingPager() {
  NuhTable.pager('hsPager',
    { page: hsState.page, pageSize: hsState.pageSize, total: hsState.total, totalPages: hsState.totalPages },
    {
      summary: t('hs_pageInfoFmt')
        .replace('{0}', hsState.page)
        .replace('{1}', hsState.totalPages)
        .replace('{2}', hsState.total),
      prev: t('aud_prev'), next: t('aud_next')
    },
    function (pg) { hsState.page = pg; loadAccounts(); });
}

function renderHousingRows() {
  // ⚠️ الترقيم المتسلسل بيكمّل عبر الصفحات — الصف الأول في صفحة ٢ رقمه ٢٦
  //    مش ١، وإلا الرقم بيقول للموظف إنه رجع لأول القائمة.
  var i = (hsState.page - 1) * hsState.pageSize;
  var html = (housingAccounts || []).map(function (a) {
    i++;
    var lastSync = a.ad_last_sync_at ? formatDate(a.ad_last_sync_at) : '-';
    var statusBadge = '<span class="badge ' + getBadgeClass(a.ad_status) + '">' + getStatusText(a.ad_status) + '</span>';
    return '<tr>' +
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
  }).join('');

  hsTable.rows(html, t('hs_noStudents'));
}

// أي تغيير في فلتر بيرجّع الصفحة لواحد — الصفحة ٧ من نتيجة أضيق غالبًا مش موجودة
function hsFilterChanged() { hsState.page = 1; loadAccounts(); }

(function wireHousingFilters() {
  var statusEl = document.getElementById('statusFilter');
  var searchEl = document.getElementById('hsSearch');

  if (statusEl) statusEl.addEventListener('change', function () {
    hsState.status = statusEl.value; hsFilterChanged();
  });

  // ⚠️ مهلة قصيرة بعد آخر حرف بدل نداء مع كل ضغطة زرار: الكتابة السريعة كانت
  //    هتبعت طلب لكل حرف، وردودهم بترجع بترتيب مش مضمون — فآخر رد وصل ممكن
  //    يكون لأقدم كلمة اتكتبت.
  if (searchEl) {
    var timer = null;
    searchEl.addEventListener('input', function () {
      clearTimeout(timer);
      timer = setTimeout(function () {
        hsState.filter = searchEl.value.trim();
        hsFilterChanged();
      }, 350);
    });
    searchEl.addEventListener('keydown', function (e) {
      if (e.key !== 'Enter') return;
      e.preventDefault();
      clearTimeout(timer);
      hsState.filter = searchEl.value.trim();
      hsFilterChanged();
    });
  }
})();


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

  // الخانات الظاهرة توصف اللي معروض - الشرح في js/url-state.js
  var sb = document.getElementById('hsSearch');
  if (sb) sb.value = hsState.filter;
  var sf = document.getElementById('statusFilter');
  if (sf) { sf.value = hsState.status; if (window.NuhSelect && NuhSelect.enhance) NuhSelect.enhance(sf.parentNode || document); }

  loadStats();
  loadAccounts();
  document.title = t('housingManagement');
});
