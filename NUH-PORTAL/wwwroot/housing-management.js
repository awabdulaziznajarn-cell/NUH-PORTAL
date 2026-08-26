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

// ⚠️ أيقونات الخطّ الزمني SVG لا حروف يونيكود: الحرف (✓ ✕ ↻) يُرسَم بخطّ
//    النظام لا بخطّ الصفحة، فيختلف سمكه وحجمه بين ويندوز وماك وبين متصفّح
//    وآخر - وهو أول ما يجعل الشاشة تبدو غير مصقولة.
var TL_ICONS = {
  _default:               '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 8v4l3 2"/></svg>',
  provisioned:            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>',
  reprovisioned:          '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/></svg>',
  enabled:                '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>',
  disabled:               '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"/></svg>',
  disable_failed:         '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v5"/><path d="M12 17h.01"/><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/></svg>',
  password_reset:         '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="10" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>',
  extension_attrs_synced: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/></svg>',
  housing_transfer:       '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3 4 7l4 4"/><path d="M4 7h16"/><path d="m16 21 4-4-4-4"/><path d="M20 17H4"/></svg>',
  left_housing:           '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>',
  person:                 '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>',
  clock:                  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 7.5V12l3 1.8"/></svg>'
};

// نبرة كل إجراء - هي وحدها ما يحمل اللون في الخطّ الزمني.
var TL_TONE = {
  provisioned: 'ok', reprovisioned: 'ok', enabled: 'ok',
  disabled: 'bad', disable_failed: 'bad',
  password_reset: 'warn', left_housing: 'warn',
  extension_attrs_synced: 'info', housing_transfer: 'info'
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
// حقل واحد: تسمية صغيرة فوق قيمة بارزة، وسطر ثانٍ اختياري تحتها.
// ⚠️ السطر الثاني هو ما يسمح بدمج حقلين في واحد («الكلية والمستوى»)، وهو
//    نفس بناء الخلايا ذات السطرين في جداول النظام - قاعدة واحدة لا اثنتان.
function acctField(label, valueHtml, opts) {
  opts = opts || {};
  var v = opts.mono ? '<span class="acct-mono">' + valueHtml + '</span>' : valueHtml;
  return '<div class="acct-f">' +
           '<span class="k">' + label + '</span>' +
           '<span class="v">' + v + '</span>' +
           (opts.sub ? '<span class="v2">' + opts.sub + '</span>' : '') +
         '</div>';
}

// بطاقة إجراء: الوصف قبل الزر. الموظف يقرأ أثر الإجراء قبل أن يضغطه —
// وده اللي كان ناقص: أربعة أزرار بأربعة ألوان بلا كلمة تشرح الفرق بينها.
// ⚠️ الزر يمرّر نفسه (this) إلى الدالة، والدوال تلفّ عملها بـ NuhBusy.run.
//    نداءات هذه الشاشة كلها على الأكتف دايركتوري وتستغرق ثوانيَ، والزر كان
//    يبقى قابلًا للضغط طوالها - فالضغطة الثانية تُنفّذ الإجراء مرة أخرى على
//    الحساب نفسه. والقفل هنا في مُنشئ الزر لا في كل نداء، فأي إجراء يُضاف
//    لاحقًا يرثه بلا أن يتذكّره أحد.
// ⚠️ صفّ لا بطاقة: أربع بطاقات في شبكة عرضها ٢٨٠ للواحدة كانت تنزل صفّين
//    تحت حدّ الشاشة، فلا يرى الموظف الإجراءات أصلًا إلا إن مرّر لتحت.
function acctAction(desc, btnClass, icon, label, btnText, onclick) {
  return '<div class="acct-row' + (btnClass === 'danger' ? ' is-danger' : '') + '">' +
           '<span class="ic">' + icon + '</span>' +
           '<span class="tx"><b>' + label + '</b><span>' + desc + '</span></span>' +
           '<button class="sbtn ' + btnClass + '" onclick="' + onclick + '">' + btnText + '</button>' +
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
    var lastSync = a.ad_last_sync_at ? formatDate(a.ad_last_sync_at) : '';
    var statusBadge = '<span class="badge ' + getBadgeClass(a.ad_status) + '">' + getStatusText(a.ad_status) + '</span>';
    // ⚠️ خلايا بسطرين (.cell-p/.cell-q) - نفس بناء قائمة الطلاب وقائمة
    //    أعضاء هيئة التدريس. السطر الثاني يُكتب دائمًا ويختفي وحده لو فارغ
    //    (قاعدة .cell-q:empty في site.css)، وإلا صار لكل حقل شرط مكتوب بالإيد.
    return '<tr>' +
      '<td class="num">' + i + '</td>' +
      '<td><div class="cell-p">' + hmEsc(a.full_name || a.full_name_english || '-') + '</div>' +
          '<div class="cell-q cell-en">' + hmEsc(a.full_name && a.full_name_english ? a.full_name_english : '') + '</div></td>' +
      // ⚠️ كان style="direction:ltr" مع حساب المحاذاة يدويًا لكل لغة. تعيين dir
      //    على الخانة يقلب معنى محاذاتها، فكان لا بد من استثناء مكتوب بالإيد.
      //    <bdi> يعزل ترتيب الحروف وحده، والمحاذاة تبقى تابعة لاتجاه الصفحة.
      '<td><div class="cell-p cell-num"><bdi>' + hmEsc(a.student_id || '-') + '</bdi></div>' +
          '<div class="cell-q cell-num">' + (a.phone ? '<bdi>' + hmEsc(a.phone) + '</bdi>' : '') + '</div></td>' +
      '<td><div class="cell-p2">' + hmEsc(collegeName(a.college) || '-') + '</div>' +
          '<div class="cell-q">' + hmEsc(a.department ? deptName(a.department) : '') + '</div></td>' +
      '<td><div class="cell-p2"><bdi>' + hmEsc(a.ad_username || '-') + '</bdi></div>' +
          '<div class="cell-q cell-num">' + (lastSync ? '<bdi>' + hmEsc(lastSync) + '</bdi>' : '') + '</div></td>' +
      '<td>' + statusBadge + '</td>' +
      '<td class="num"><div class="ad-actions">' +
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

    // ---- الهوية: الطالب لا الحساب. النافذة عن طالب، والحساب سطر في الدليل.
    //      ⚠️ خمسة حقول كانت مكرَّرة بينها وبين الجدول تحتها: اسم المستخدم
    //         وحالة الحساب والرقم الجامعي والاسم العربي والاسم الإنجليزي.
    //      ⚠️ والاسمان معًا في اللغتين: حساب الدليل يُبنى من الاسم الإنجليزي
    //         وسجلّ الطالب بالعربي، والموظف هنا يطابق بينهما. إخفاء أحدهما
    //         بحسب لغة الواجهة يخفي ما جاء يراه - واسم الشخص بيانات لا ترجمة.
    html += '<div class="acct-id' + (s.ad_status === 'enabled' ? '' : ' is-off') + '">' +
              '<span class="av">' + HM_SVG.user + '</span>' +
              '<span class="who">' +
                '<span class="n1">' + hmEsc(s.full_name || s.full_name_english || '-') + '</span>' +
                (s.full_name && s.full_name_english
                  ? '<span class="n2">' + hmEsc(s.full_name_english) + '</span>' : '') +
              '</span>' +
              statusBadge +
            '</div>';

    // ---- عمودان: بيانات الطالب | بيانات الحساب في الدليل
    var stuCol = '<div class="acct-col">' +
                   '<span class="acct-t">' + t('hs_secStudent') + '</span>' +
                   acctField(t('studentId'), hmEsc(s.student_id || '-'), { mono: true }) +
                   acctField(t('hs_collegeLevel'), hmEsc(collegeName(s.college) || '-'),
                             { sub: s.academic_level ? hmEsc(t('academicLevel') + ' ' + s.academic_level) : '' }) +
                   acctField(t('gender'), hmEsc(acctGender(s.gender))) +
                 '</div>';

    var dirCol;
    if (ad) {
      var groups = (ad.memberOf && ad.memberOf.length)
        ? ad.memberOf.map(function (g) { return '<span class="acct-tag">' + hmEsc(g) + '</span>'; }).join('')
        : '-';
      dirCol = '<div class="acct-col">' +
                 '<span class="acct-t">' + t('hs_secDirectory') + '</span>' +
                 acctField(t('adUsername'), hmEsc(ad.samAccountName || '-'), { mono: true }) +
                 acctField(t('hs_upn'), hmEsc(ad.userPrincipalName || '-'), { mono: true }) +
                 acctField(t('adLastSync'),
                           hmEsc(s.ad_last_sync_at ? formatDate(s.ad_last_sync_at) : '-'), { mono: true }) +
                 acctField(t('hs_groups'), groups) +
                 // مطويّة: الفني يفتحها عند العطل، والموظف لا تزاحمه كل يوم.
                 '<details class="acct-tech"><summary>' + t('hs_techDetails') + '</summary><div class="in">' +
                   acctField(t('hs_dn'), hmEsc(ad.distinguishedName || '-'), { mono: true }) +
                   acctField(t('hs_uac'), hmEsc(acctUac(ad.userAccountControl))) +
                 '</div></details>' +
               '</div>';
    } else {
      dirCol = '<div class="acct-col">' +
                 '<span class="acct-t">' + t('hs_secDirectory') + '</span>' +
                 '<div class="acct-empty">' + t('adNoAccount') + '</div>' +
               '</div>';
    }
    html += '<div class="acct-cols">' + stuCol + dirCol + '</div>';

    // ---- الإجراءات: كل واحد بسطر يشرح أثره.
    //      الإخفاء هنا للراحة فقط - الصلاحية تُفحص على السيرفر في كل نداء.
    if (ad) {
      var acts = '';
      if (hmCan('housing.manageAccounts')) {
        acts += (s.ad_status === 'enabled')
          ? acctAction(t('hs_actDisableDesc'), 'danger', HM_SVG.ban, t('disableAccount'), t('hs_btnDisable'),
                       "performAction(this," + studentId + ",'disable')")
          : acctAction(t('hs_actEnableDesc'), 'ok', HM_SVG.check, t('enableAccount'), t('hs_btnEnable'),
                       "performAction(this," + studentId + ",'enable')");
        acts += acctAction(t('hs_actPwdDesc'), '', HM_SVG.lock, t('resetPassword'), t('hs_btnReset'),
                           "showResetPassword(" + studentId + ")");
        acts += acctAction(t('hs_actRealignDesc'), '', HM_SVG.align, t('reProvision'), t('hs_btnRealign'),
                           "performAction(this," + studentId + ",'re-provision')");
      }
      if (hmCan('housing.syncAd')) {
        acts += acctAction(t('hs_actSyncDesc'), '', HM_SVG.upload, t('syncAttributes'), t('hs_btnSync'),
                           "performAction(this," + studentId + ",'sync-attrs')");
      }
      if (acts) {
        html += '<div class="acct-acts"><span class="acct-t">' + t('hs_secActions') + '</span>' + acts + '</div>';
      }
    }

    body.innerHTML = html;
  } catch (e) {
    body.innerHTML = '<p style="color:var(--red)">' + t('apiError') + ': ' + hmEsc(e.message) + '</p>';
  }
}

async function performAction(btn, studentId, action) {
  var confirmMsgs = {
    'enable': t('confirmEnable'),
    'disable': t('confirmDisable'),
    're-provision': t('confirmReProvision'),
    'sync-attrs': t('confirmSync')
  };

  // الأحمر للإجراء الذي يقطع خدمة عن الطالب فقط. «إعادة ضبط الحساب» إجراء
  // تصحيحي وليس خطرًا، وتلوينه أحمر يعوّد المستخدم على تجاهل اللون الأحمر.
  var dangerous = (action === 'disable');
  // ⚠️ القفل بعد التأكيد لا قبله: نافذة التأكيد تحجب الضغط أصلًا، ولو قفلنا
  //    قبلها بقي الزر مقفولًا بلا داعٍ لو ألغى المستخدم.
  if (!await NuhDialog.confirm({ message: confirmMsgs[action] || t('confirm'), danger: dangerous })) return;

  return NuhBusy.run(btn, async function () {
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
  });
}

// ⚠️ كانت تمسح محتوى النافذة وتضع حقلًا عاريًا وزرّين بلا أي إطار أو عنوان،
//    فتبدو كأن الصفحة تعطّلت وظهر نموذج من صفحة أخرى. الآن بنفس بناء بطاقة
//    إدارة الحساب: ترويسة .acct-id ثم قسم مفصول بخطّ، وزر رجوع واضح.
// ⚠️ ولا أنماط داخل السمة style: كلّ ما يخصّ شكل النموذج في .acct-form
//    داخل css/components.css - مكان واحد لا مكانان.
function showResetPassword(studentId) {
  var body = document.getElementById('detailsModalBody');
  body.innerHTML =
    '<div class="acct-id is-plain">' +
      '<span class="av">' + HM_SVG.lock + '</span>' +
      '<span class="who">' +
        '<span class="n1">' + t('resetPassword') + '</span>' +
        '<span class="n2">' + t('hs_actPwdDesc') + '</span>' +
      '</span>' +
    '</div>' +
    '<div class="acct-form">' +
      '<label>' +
        '<span class="k">' + t('newPassword') + '</span>' +
        '<input type="password" id="newPwd" autocomplete="new-password" ' +
          'data-i18n-placeholder="newPasswordPlaceholder">' +
      '</label>' +
      '<p class="note">' + (t('passwordMinLength') || '') + '</p>' +
      '<div class="btns">' +
        '<button class="sbtn primary" id="pwdConfirmBtn" onclick="resetPassword(this,' + studentId + ')">' +
          HM_SVG.check + '<span>' + t('confirm') + '</span></button>' +
        '<button class="sbtn" onclick="openDetails(' + studentId + ')">' + t('cancel') + '</button>' +
      '</div>' +
    '</div>';
  var f = document.getElementById('newPwd');
  if (f) {
    // Enter داخل الحقل ينفّذ — كان لازم الوصول للزر بالفأرة كل مرة
    // ⚠️ Enter يمرّ من الزر نفسه لا من الحقل، وإلا كان القفل بلا أثر:
    //    المستخدم يضغط Enter مرتين فينفَّذ الإجراء مرتين.
    f.addEventListener('keydown', function (e) {
      if (e.key === 'Enter') resetPassword(document.getElementById('pwdConfirmBtn'), studentId);
    });
    setTimeout(function () { f.focus(); }, 40);
  }
}

async function resetPassword(btn, studentId) {
  var pwd = document.getElementById('newPwd').value;
  if (!pwd || pwd.length < 8) {
    showToast(t('passwordMinLength') || 'Password must be at least 8 characters', 'error');
    return;
  }

  return NuhBusy.run(btn, async function () {
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
  });
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
      body.innerHTML = '<div class="nuh-tl-empty">' + t('noData') + '</div>';
      return;
    }

    // ⚠️ خطّ زمني (.nuh-tl) لا قائمة مسطّحة: المكوّن مشترك في css/components.css
    //    والشرح كامل هناك. والأيقونات SVG لا حروف يونيكود (كانت ✓ و✕ و↻)،
    //    لأن الحرف يُرسَم بخطّ النظام لا بخطّنا فيختلف شكله من جهاز لآخر.
    var html = '<div class="nuh-tl">';
    var lastDay = null;

    logs.forEach(function (l, i) {
      var action = l.action || '';

      // ⚠️ التاريخ يُكتب مرة لكل يوم لا مرة لكل سطر. والمفتاح من NuhFmt.date
      //    نفسها التي تُكتب بها الترويسة، فلو بدّل الموظف التقويم إلى الهجري
      //    تغيّر التجميع معه - ولا يبقى عنوان بتقويم وصفوفه بتقويم آخر.
      var dayKey = NuhFmt.date(l.performedAt);
      if (dayKey !== lastDay) {
        lastDay = dayKey;
        html += '<div class="nuh-tl-day"><b>' + hmEsc(NuhFmt.dateFull(l.performedAt)) + '</b><i></i></div>';
      }

      // ⚠️ الوقت أوّل سطر البيانات لا في طرف سطر العنوان: هناك كان يقع تحت
      //    شريط التمرير فيُقصّ، ويترك فراغًا واسعًا بينه وبين العنوان.
      var meta = '<span class="nuh-tl-time">' + TL_ICONS.clock +
                 '<span>' + hmEsc(NuhFmt.time(l.performedAt)) + '</span></span>';
      if (l.performerName) meta += '<span class="sp"></span>' + TL_ICONS.person +
                                   '<span>' + hmEsc(l.performerName) + '</span>';
      if (l.ipAddress) meta += '<span class="sp"></span><span class="nuh-tl-ip">' + hmEsc(l.ipAddress) + '</span>';

      html += '<div class="nuh-tl-item">' +
          '<div class="nuh-tl-dot ' + (TL_TONE[action] || '') + '">' + (TL_ICONS[action] || TL_ICONS._default) + '</div>' +
          '<div class="nuh-tl-body">' +
            '<div class="nuh-tl-top">' +
              '<span class="nuh-tl-title">' + hmEsc(hmActionLabel(action)) + '</span>' +
            '</div>' +
            (l.details ? '<div class="nuh-tl-desc">' + hmEsc(l.details) + '</div>' : '') +
            '<div class="nuh-tl-meta">' + meta + '</div>' +
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
