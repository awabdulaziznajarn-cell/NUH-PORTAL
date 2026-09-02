// ⚠️ هذا الملف بقي من البوابة القديمة (HTML ثابت + توكن في localStorage).
//    بعد نقل الشاشة إلى MVC صار فيه عطلان يوقفان الصفحة كلها:
//
//    1) كان يقرأ staffToken من localStorage ويرسله في ترويسة Authorization.
//       لم يعد أي كود يكتب هذا المفتاح بعد توحيد الدخول على الكوكي، فالنتيجة
//       إمّا تحويل فوري إلى صفحة الدخول، أو إرسال "Bearer null" - والترويسة
//       تسبق الكوكي في سياسة NUH_Smart، فيُرفض كل نداء بـ 401 وتظل الشاشة أصفارًا.
//    2) كان setLang يكتب في sidebar-user-name و sidebar-user-role، وهما عنصران
//       من القائمة الجانبية القديمة لا وجود لهما في تخطيط MVC. فيرمي TypeError،
//       و setLang أول سطر في التهيئة - فيتوقف كل ما بعده: الجدول والعدّادات
//       والرسوم و«تنبيهات الأمان» تبقى على «جاري التحميل...» بلا نهاية.
//
//    الكوكي يُرسَل تلقائيًا مع كل نداء لنفس الأصل، فلا حاجة لأي ترويسة.
const currentUser = { role: (window.NUH && NUH.role) || '' };
const _userRole = (currentUser.role || '').toLowerCase();

const actionTranslations = {
  login: 'repj_action_login',
  logout: 'repj_action_logout',
  login_failed: 'repj_action_login_failed',
  set_password: 'repj_action_set_password',
  create_request: 'repj_action_create_request',
  approve_request: 'repj_action_approve_request',
  reject_request: 'repj_action_reject_request',
  create_student: 'repj_action_create_student',
  update_student: 'repj_action_update_student',
  delete_student: 'repj_action_delete_student',
  checkout_student: 'repj_action_checkout_student',
  user_created_ad: 'repj_action_user_created_ad',
  user_updated_ad: 'repj_action_user_updated_ad',
  login_admin_fallback: 'repj_action_login_admin_fallback',
  login_admin_fallback_failed: 'repj_action_login_admin_fallback_failed',
  housing_approve_request: 'repj_action_housing_approve_request',
  housing_reject_request: 'repj_action_housing_reject_request',
  submit_cyber_review: 'repj_action_submit_cyber_review',
  cyber_approve_request: 'repj_action_cyber_approve_request',
  cyber_reject_request: 'repj_action_cyber_reject_request',
  ready_for_provisioning_request: 'repj_action_ready_for_provisioning_request',
  complete_request: 'repj_action_complete_request'
};

function translateAction(action) {
  if (actionTranslations[action]) return t(actionTranslations[action]);
  // fallback: أي إجراء مش في الخريطة المحلية نجيبه من مفاتيح aud_action_ الشاملة
  var k = 'aud_action_' + action;
  var v = t(k);
  return v === k ? action : v;
}

// ⚠️ كان هنا تعريف تاني لـ escHtml **ناقص تهريب العلامة المفردة (')**، ولأنه
//    في النطاق العام كان بيدهس نسخة التخطيط القوية على الصفحة دي وحدها.
//    يعني شاشة التقارير كانت شغّالة بتهريب أضعف من باقي النظام من غير ما
//    يبان أي فرق. التعريف الوحيد دلوقتي في /js/esc.js.

async function apiFetch(url) {
  try {
    const res = await fetch(url, { credentials: 'same-origin' });
    if (res.status === 401) { window.location.replace('/Account/Login'); return null; }
    return await res.json();
  } catch(e) { return null; }
}

function setLang(l) {
  __baseSetLang(l);
  // ⚠️ نسخة تانية من ترويسة التاريخ اللي في التخطيط، وبـ ar-SA اللي بترجّع
  //    هجري في متصفحات - فالشاشة دي وحدها كانت ممكن تعرض تاريخ هجري في
  //    الترويسة والباقي ميلادي.
  document.getElementById('dateNow').textContent = NuhFmt.dateFull(new Date());
  rebuildActionFilter();
  var userFilterEl = document.getElementById('userFilter');
  var allOption = userFilterEl.options[0];
  if (allOption) { allOption.textContent = t('agrp_allUsers'); }
  if (dataLoaded) { updatePagination(totalRecords); renderTable(); }
}

// ⚠️ القائمة من الخادم لا من هنا: /api/AuditLogs/action-groups بترجّع
//    المجموعات اللي للموظف صفوف يشوفها فيها بس. كانت مصفوفة مكتوبة هنا
//    بتعرض لكل دور كل المجموعات - فالمشرف كان يلاقي «مزامنة الدليل النشط»
//    قدّامه وهو لا يرى منها صفًّا واحدًا (ScopedAsync بتخفي إجراءات الإدارات
//    التانية)، فيختارها ويرجع بجدول فاضي ويفتكر الشاشة بايظة.
//    ونصّ كل مفتاح في ملف الترجمة باسم agrp_<key> - فمفيش خريطة تانية هنا.
var actionGroupKeys = [];

async function loadActionGroups() {
  try {
    var res = await fetch('/api/auditlogs/action-groups', { credentials: 'same-origin' });
    if (res.ok) actionGroupKeys = await res.json();
  } catch (e) { /* الفلتر يفضل على «كل العمليات» - أهون من قائمة مكتوبة بالإيد */ }
  rebuildActionFilter();
}

function rebuildActionFilter() {
  var sel = document.getElementById('actionFilter');
  if (!sel) return;
  var currentVal = sel.value;
  sel.innerHTML = '';

  var add = function (value, label) {
    var el = document.createElement('option');
    el.value = value;
    el.textContent = label;
    sel.appendChild(el);
  };

  add('', t('agrp_all'));
  (actionGroupKeys || []).forEach(function (k) { add(k, t('agrp_' + k)); });

  // ⚠️ لو المجموعة المختارة اختفت من النطاق، القيمة بترجع '' لوحدها - يعني
  //    «كل العمليات» لا خيار ميّت شكله سليم.
  sel.value = currentVal;
  if (window.NuhSelect && NuhSelect.refresh) NuhSelect.refresh(sel);
}

function populateUserFilter(users) {
  var sel = document.getElementById('userFilter');
  var currentVal = sel.value;
  // ⚠️ «كل المستخدمين» لا «الكل»: القائمتان متجاورتان، وكلمة «الكل» فيهما
  //    معًا لا تقول أيّهما يفلتر المستخدم وأيّهما يفلتر العملية.
  sel.innerHTML = '<option value="">' + t('agrp_allUsers') + '</option>';
  users.forEach(function(u) {
    var el = document.createElement('option');
    el.value = u.id;
    el.textContent = u.name;
    sel.appendChild(el);
  });
  sel.value = currentVal;
}

// الصفحة وعدد الصفوف من الرابط زي باقي الفلاتر - كانوا الوحيدين برّه
// (مع الترتيب)، فالرابط المتبعت كان بيفتح بفلاتر صح وصفحة غلط.
var currentPage = NuhUrl.int('page', 1, 1);
var currentPageSize = NuhUrl.int('size', 50, 1);
var totalPages = 1;
var totalRecords = 0;
var cachedUsers = [];
var cachedData = null;
var dataLoaded = false;
var cachedChartData = null;
var topDataLoaded = false;
var execTimeout = null;

function badgeClass(action) {
  var map = {
    'create_student':'badge-green','update_student':'badge-blue','delete_student':'badge-red',
    'checkout_student':'badge-orange','create_request':'badge-green','approve_request':'badge-green',
    'reject_request':'badge-red','housing_approve_request':'badge-blue','housing_reject_request':'badge-red','submit_cyber_review':'badge-purple','cyber_approve_request':'badge-purple','cyber_reject_request':'badge-red','ready_for_provisioning_request':'badge-orange','complete_request':'badge-green','login':'badge-gray','logout':'badge-gray','login_failed':'badge-darkred',
    'set_password':'badge-orange','login_admin_fallback':'badge-purple','login_admin_fallback_failed':'badge-darkred',
    'user_created_ad':'badge-blue','user_updated_ad':'badge-purple'
  };
  return map[action] || 'badge-gray';
}

// شكل التاريخ من NuhFmt - التعريف الوحيد في /js/date-format.js
// ⚠️ كانت الصيغة بتتقلب mm/dd في الإنجليزي - يعني 08/09 تتقرا ٨ سبتمبر عند
//    واحد و٩ أغسطس عند التاني على نفس الشاشة. صيغة واحدة في اللغتين دلوقتي.
function formatDate(v) { return NuhFmt.dateTime(v); }

function getFilters() {
  return {
    userId: document.getElementById('userFilter').value,
    actionGroup: document.getElementById('actionFilter').value,
    fromDate: document.getElementById('fromDate').value,
    toDate: document.getElementById('toDate').value,
    search: document.getElementById('searchBox').value.trim()
  };
}

// ترتيب الأعمدة من نسخة واحدة في النظام - NuhTable.sort. الأعمدة معرّفة بـ
// data-sort على الـ <th>، والخادم بيرتّب على كل السجلات مش الصفحة المعروضة.
var repSort = NuhTable.sort('repTable', function () { loadLogs(1, currentPageSize); },
  { by: NuhUrl.get('sort', ''), asc: NuhUrl.get('asc', '1') === '1' });

function buildUrl(p, ps) {
  var url = '/api/auditlogs?page=' + p + '&pageSize=' + ps;
  var f = getFilters();
  if (f.userId) url += '&userId=' + encodeURIComponent(f.userId);
  if (f.actionGroup) url += '&actionGroup=' + encodeURIComponent(f.actionGroup);
  if (f.fromDate) url += '&fromDate=' + encodeURIComponent(f.fromDate);
  if (f.toDate) url += '&toDate=' + encodeURIComponent(f.toDate);
  if (f.search) url += '&search=' + encodeURIComponent(f.search);
  return url + repSort.qs();
}

// ============================================================================
//  أيقونة الخلفية لبطاقات المؤشّر في هذه الشاشة.
//
//  ⚠️ الخريطة هنا لا ستّة عشر <svg> مكتوبة في الوسم: البطاقات في أربع لوحات
//     وبعضها مكرّر بنفس المعنى (عمليات اليوم في ثلاث لوحات). خريطة واحدة
//     بتخلّي المعنى الواحد ياخد أيقونة واحدة مهما اتكرّر مكانه.
//  ⚠️ والحقن من السكربت لا من الوسم: الأيقونة زخرفة صامتة (aria-hidden)
//     مالهاش محتوى، فوجودها في الوسم بيزوّده بلا فايدة.
// ============================================================================
var RP_ICON = (function () {
  var P = {
    ops:      '<path d="M3 3v18h18"/><path d="M18.7 8l-5.1 5.2-2.8-2.7L7 14.3"/>',
    avg:      '<line x1="3" y1="12" x2="21" y2="12"/><path d="M7 8v8"/><path d="M12 5v14"/><path d="M17 9v6"/>',
    peak:     '<path d="M3 18l6-8 4 5 3-4 5 7z"/>',
    users:    '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/>',
    student:  '<path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/>',
    request:  '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/>',
    kinds:    '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/>',
    fail:     '<path d="M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4"/><path d="M12 17h.01"/>',
    del:      '<polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/>',
    records:  '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.7-4 3-9 3s-9-1.3-9-3"/><path d="M3 5v14c0 1.7 4 3 9 3s9-1.3 9-3V5"/>'
  };
  var BY_ID = {
    statTotOps: P.ops, kpiDailyAvg: P.avg, kpiPeakDay: P.peak,
    statUsers: P.users, statUsersSec: P.users, kpiActiveUsers: P.users,
    kpiStudentTotal: P.student, statStudentOps: P.student, statStudentRecs: P.student,
    kpiRequestTotal: P.request, statRequestOps: P.request, statRequestRecs: P.request,
    kpiStudentKinds: P.kinds, kpiRequestKinds: P.kinds,
    statFailedSec: P.fail, statDeletes: P.del, statTotalRecords: P.records
  };

  function paint() {
    var cards = document.querySelectorAll('.rp-kpi');
    for (var i = 0; i < cards.length; i++) {
      var c = cards[i];
      if (c.querySelector('.chart-ghost')) continue;
      var v = c.querySelector('.v');
      var path = (v && BY_ID[v.id]) || P.ops;   // الافتراضي: رسم بياني
      var g = document.createElement('span');
      g.className = 'chart-ghost';
      g.setAttribute('aria-hidden', 'true');
      g.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" ' +
                    'stroke-linecap="round" stroke-linejoin="round">' + path + '</svg>';
      c.appendChild(g);
    }
  }
  return { paint: paint };
})();

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', RP_ICON.paint);
} else {
  RP_ICON.paint();
}

function renderTable() {
  if (!dataLoaded) return;
  var tbody = document.getElementById('logTbody');
  if (!cachedData || !cachedData.length) {
    tbody.innerHTML = '<tr><td colspan="6"><div class="empty-state"><div class="empty-icon">📭</div><div class="empty-text">' + t('repj_empty_noRecords') + '</div></div></td></tr>';
    return;
  }
  tbody.innerHTML = cachedData.map(function(l, i) {
    var rowNum = (currentPage - 1) * currentPageSize + i + 1;
    var bCls = badgeClass(l.action);
    var bLabel = escHtml(translateAction(l.action));
    return '<tr onclick="openModal(cachedData[' + i + '])"><td style="color:#85888e">' + rowNum +
    '</td><td>' + escHtml(l.user?.full_name ?? l.user?.username ?? l.user_id) +
    '</td><td><span class="badge ' + bCls + '">' + bLabel + '</span>' +
    '</td><td>' + escHtml(l.target_table || '') +
    // ⚠️ الاسم لا الرقم: الخادم بيحلّ (جدول + رقم) لاسم مقروء - اسم الطالب
    //    ورقمه الجامعي، أو رقم الطلب، أو «برج 6 - شقة 20». الشاشة دي كانت
    //    بتعرض الرقم الخام وبس، فسطر زي «طباعة وثيقة التعهّد /
    //    StudentDeclarations / 31» مكانش بيقول اتطبعت لمين - وده بالظبط
    //    السؤال اللي السجل موجود عشانه. شاشة «سجل العمليات» كانت بتعرضه
    //    صح، ودي كانت النسخة اللي اتنسيت.
    '</td><td title="' + escHtml(String(l.target_id || '')) + '">' +
      escHtml(l.target_name || String(l.target_id || '')) +
      (l.target_sub ? ' <bdi class="fh-acct">' + escHtml(l.target_sub) + '</bdi>' : '') +
    '</td><td>' + formatDate(l.action_at) +
    '</td></tr>';
  }).join('');
}

function updatePagination(recs) {
  // صفّ الترقيم من النسخة الوحيدة في النظام - NuhTable.pager
  NuhTable.pager('repPager',
    { page: currentPage, pageSize: currentPageSize, total: recs, totalPages: totalPages || 1 },
    {
      summary: t('totalRecords') + ': ' + recs,
      prev: t('rep_51'), next: t('rep_52'),
      sizePrefix: t('rep_49'), sizeSuffix: t('rep_50')
    },
    function (p) { loadLogs(p, currentPageSize); },
    { sizes: [50, 100, 200], onSize: function (n) { loadLogs(1, n); } });

  // ⚠️ statTotalRecords / statStudentRecs / statRequestRecs كانت موجودة في
  //    الصفحة ومحدّش بيملاها إطلاقًا، فبتفضل أصفارًا للأبد. بتتملا هنا من
  //    الصفحة المعروضة: الإجمالي من الخادم، والتفصيل من صفوف الصفحة دي.
  var rows = cachedData || [];
  var stu = 0, req = 0;
  rows.forEach(function (r) {
    var g = actionGroup(r.action);
    if (g === 'student') stu++;
    else if (g === 'request') req++;
  });
  ['statTotalRecords', 'statTotalRecordsMain'].forEach(function (id) { setTxt(id, recs); });
  ['statStudentRecs', 'statStudentRecsMain'].forEach(function (id) { setTxt(id, stu); });
  ['statRequestRecs', 'statRequestRecsMain'].forEach(function (id) { setTxt(id, req); });
}

// ⚠️ كانت هنا updateStats تكتب في statTotalRecords / statStudentRecs /
//    statRequestRecs - ثلاثة معرّفات لا وجود لها في الصفحة إطلاقًا (بطاقات
//    العدّادات اسمها statTotOps / statStudentOps / statRequestOps). فكانت ترمي
//    TypeError داخل loadLogs بعد رسم الجدول مباشرة، فيُبتلع في catch ويُستبدل
//    الجدول برسالة «خطأ اتصال» - والسبب الحقيقي لا علاقة له بالشبكة.
//    البطاقات الست تملأها loadSummaryStats من /api/auditlogs/today-stats.

function openModal(record) {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('modalUser').textContent = record.user?.full_name ?? record.user?.username ?? record.user_id;
  document.getElementById('modalAction').innerHTML = escHtml(translateAction(record.action));
  document.getElementById('modalTable').textContent = record.target_table || '';
  // ⚠️ الاسم والرقم مع بعض في النافذة: النافذة هي المكان اللي بيتفتح لمّا
  //    السطر يبقى محلّ سؤال، فالاتنين مطلوبين - الاسم للي بيراجع، والرقم
  //    للي بيتتبّع تقنيًّا.
  var tgt = document.getElementById('modalTargetId');
  tgt.innerHTML = escHtml(record.target_name || String(record.target_id || '')) +
    (record.target_sub ? ' <bdi class="fh-acct">' + escHtml(record.target_sub) + '</bdi>' : '') +
    ' <span style="color:var(--gray-500);font-size:12px">#' + escHtml(String(record.target_id || '')) + '</span>';
  document.getElementById('modalDate').textContent = formatDate(record.action_at);
  document.getElementById('detailModal').classList.add('open');
}

function closeModal() {
  document.getElementById('detailModal').classList.remove('open');
}

async function loadLogs(page, pageSize) {
  const p = page || currentPage;
  const ps = pageSize || currentPageSize;
  currentPage = p;
  currentPageSize = ps;
  try {
    const res = await fetch(buildUrl(p, ps), { credentials: 'same-origin' });
    if (res.status === 401) { window.location.replace('/Account/Login'); return; }
    const body = await res.json();
    cachedData = body.data;
    totalPages = body.totalPages;
    totalRecords = body.totalRecords;
    dataLoaded = true;
    renderTable();
    updatePagination(body.totalRecords);
    syncUrlParams();
    if (!topDataLoaded) loadTopData();
    if (cachedChartData) { computeKpi(); computeTopWidgets(); generateExecSummary(); }
  } catch(e) {
    dataLoaded = true;
    document.getElementById('logTbody').innerHTML = '<tr><td colspan="6"><div class="empty-state"><div class="empty-icon">⚠️</div><div class="empty-text" style="color:#b42318">' + t('repj_error_connection') + '</div></div></td></tr>';
  }
}

async function loadAuditUsers() {
  try {
    const res = await fetch('/api/auditlogs/users', { credentials: 'same-origin' });
    if (res.ok) {
      const users = await res.json();
      cachedUsers = users;
      populateUserFilter(users);
    }
  } catch(e) {}
}

function applyFilters() {
  // ⚠️ الفلترة بتفتح السجل التفصيلي غصب. السجل مطوي افتراضيًا عشان الصفحة
  //    تقارير لا جدول خام، بس اللي بيضغط «تطبيق» طالب نتيجة بعينها - ولو
  //    فضلت في جدول مخفي بيقرا ده على إن الفلتر مرجّعش حاجة.
  if (typeof toggleLog === 'function') toggleLog(true);
  reloadAll();
}

// ⚠️ الصفحة كلها بتتبع الفلتر مش الجدول وحده. قبل كده «تطبيق» كان بيحدّث آخر
//    جدول بس، والبطاقات والرسم والقوائم فوقه يفضلوا على «اليوم» و«آخر ٧ أيام» -
//    فالمستخدم يختار ١١-١٦ أغسطس ويشوف عمودًا على ١٨ أغسطس فوق نتيجة صحيحة
//    تحت. صفحة اسمها «مركز التقارير» وفلترها بيحرّك خُمسها.
function reloadAll() {
  topDataLoaded = false;
  cachedTopData = [];
  loadLogs(1, currentPageSize);
  loadSummaryStats();
  loadCharts();
  loadTopData();
}

function onFilterChange() {
  reloadAll();
}

// ⚠️ لاحقة المدة مكتوبة مرة واحدة: البطاقات والرسوم والقوائم والجدول لازم
//    يقيسوا نفس المدة بالحرف. لو كل نداء بنى الوسائط بنفسه كان الجدول يقول
//    ١٩ عملية والرسم يرسم عمودًا في يوم بره المدة - والمستخدم يقرا ده على إن
//    الفلتر مشتغلش أصلًا.
function rangeQs(prefix) {
  var f = document.getElementById('fromDate').value;
  var t = document.getElementById('toDate').value;
  var qs = '';
  if (f) qs += (qs || prefix) + (qs ? '&' : '') + 'fromDate=' + encodeURIComponent(f);
  if (t) qs += (qs ? '&' : prefix) + 'toDate=' + encodeURIComponent(t);
  return qs;
}
function hasRange() {
  return !!(document.getElementById('fromDate').value || document.getElementById('toDate').value);
}

async function loadSummaryStats() {
  const data = await apiFetch('/api/auditlogs/today-stats' + rangeQs('?'));
  if (!data) return;
  document.getElementById('statTotOps').textContent = data.todayTotalOps || 0;
  document.getElementById('statUsers').textContent = data.todayActiveUsers || 0;
  document.getElementById('statStudentOps').textContent = data.todayStudentOps || 0;
  document.getElementById('statRequestOps').textContent = data.todayRequestOps || 0;
  document.getElementById('statFailed').textContent = data.todayFailedLogins || 0;
  document.getElementById('statDeletes').textContent = data.todayDeletes || 0;
  applyPeriodLabels();
}

// ⚠️ البطاقة اللي مكتوب تحتها «اليوم» بتقيس المدة المختارة لمّا تتحدّد - فلو
//    النصّ فضل «اليوم» كان الرقم يتقرا غلط تمامًا. النصّ بيتبع المصدر.
function applyPeriodLabels() {
  var txt = hasRange() ? t('rpx_inRange') : null;
  document.querySelectorAll('[data-period]').forEach(function (el) {
    el.textContent = txt || el.getAttribute('data-period');
  });
}

// انتظار محدود لوصول مكتبة الرسوم. المهلة مقصودة: بعدها نعرض رسالة واضحة
// بدل انتظار بلا نهاية أمام مربعات فارغة.
function waitForChartLib(maxMs) {
  return new Promise(function (resolve) {
    if (typeof Chart !== 'undefined') { resolve(true); return; }

    // ⚠️ الملف نفسه فشل في التحميل (onerror في الصفحة) - مفيش فايدة من
    //    الانتظار. من غير الفحص ده الموظف بيقعد عشر ثوانٍ قدام مربّعات فاضية
    //    ويقراها بطئًا، وهي في الحقيقة ملف ناقص على الخادم.
    if (window.__chartLibFailed) { resolve(false); return; }

    var waited = 0, step = 120;
    var iv = setInterval(function () {
      if (typeof Chart !== 'undefined') { clearInterval(iv); resolve(true); }
      else if (window.__chartLibFailed) { clearInterval(iv); resolve(false); }
      else if ((waited += step) >= maxMs) { clearInterval(iv); resolve(false); }
    }, step);
  });
}

async function loadCharts() {
  const data = await apiFetch('/api/auditlogs/chart-data' + rangeQs('?'));
  if (!data) return;
  cachedChartData = data;

  // ⚠️ المكتبة محلية وبوسم async، فقد تصل بعد البيانات. ننتظرها هنا وحدها
  //    لا توقف بقية الصفحة. (التحميل الاحتياطي من الإنترنت اتشال -
  //    الشرح في Views/Reports/Index.cshtml.)
  //    وإن لم تصل (خادم أو جهاز بلا منفذ للإنترنت) تبقى المربعات الأربعة فارغة
  //    بلا كلمة تشرح السبب، فيظن المستخدم أن لا بيانات لديه. نقولها صراحة.
  if (!(await waitForChartLib(10000))) {
    // ⚠️ الرسمان الباقيان بس: «عمليات الطلاب» و«عمليات الطلبات» بقوا أعمدة
    //    أفقية بـ CSS، فمش محتاجين المكتبة ولا بيتأثروا بغيابها.
    ['chartOps7', 'chartLogins30'].forEach(function (id) {
      var c = document.getElementById(id);
      if (c && c.parentNode) {
        c.parentNode.innerHTML = '<div style="padding:28px 14px;text-align:center;color:var(--gray-500);' +
          'font-size:12.5px;line-height:1.9">' + t('rep_chartsUnavailable') + '</div>';
      }
    });
    if (dataLoaded) { computeKpi(); computeTopWidgets(); generateExecSummary(); }
    return;
  }

  if (data.last7Days && data.last7Days.length) {
    var labels = data.last7Days.map(function(d) { return d.date.slice(5, 10); });
    var counts = data.last7Days.map(function(d) { return d.count; });
    renderChart('chartOps7', 'bar', labels, counts, '#166a45');
  }
  if (data.login30 && data.login30.length) {
    var labels = data.login30.map(function(d) { return d.date.slice(5, 10); });
    var counts = data.login30.map(function(d) { return d.count; });
    renderChart('chartLogins30', 'line', labels, counts, '#dba102');
  }
  // ⚠️ كانت دونات. اتحوّلت أعمدة أفقية لسببين: الدونة على ٢-٣ أرقام بترسم
  //    تلات شرايح وتحتاج وسيلة إيضاح تحتها عشان تقراها - الأعمدة بتكتب الاسم
  //    والرقم جنب بعض. وأسماء الإجراءات عربية، فالأعمدة الرأسية كانت هتقصّها.
  renderHBars('barsStudents', data.studentOps, 'kpiStudentTotal', 'kpiStudentKinds');
  renderHBars('barsRequests', data.requestOps, 'kpiRequestTotal', 'kpiRequestKinds');
  if (dataLoaded) { computeKpi(); computeTopWidgets(); generateExecSummary(); }
}

// ⚠️ الدالة دي كانت مكتوبة هنا وفي لوحة التحكم بنفس السطور بالظبط، وكانت
//    بتحط محاور على الدونات كمان (فبيظهر جنبها محور رأسي فيه 0 و1 بلا معنى).
//    بقت في js/chart-theme.js - شكل واحد لكل رسوم النظام.
function renderChart(id, type, labels, data, bgColors, totalLabel) {
  return NuhChart.render(id, type, labels, data, bgColors, totalLabel);
}

// أعمدة أفقية بديلة عن الدونة لما التصنيفات قليلة وأسماؤها عربية طويلة.
// rows = [{action, count}] - بترجّع الإجمالي وعدد الأنواع للمؤشرات فوقها.
function renderHBars(hostId, rows, totalId, kindsId) {
  var host = document.getElementById(hostId);
  if (!host) return;
  rows = (rows || []).slice().sort(function (a, b) { return b.count - a.count; });
  var total = rows.reduce(function (s, r) { return s + (r.count || 0); }, 0);
  if (totalId) { var te = document.getElementById(totalId); if (te) te.textContent = total; }
  if (kindsId) { var ke = document.getElementById(kindsId); if (ke) ke.textContent = rows.length; }
  if (!rows.length) {
    host.innerHTML = '<div class="empty-state">' + t('noData') + '</div>';
    return;
  }
  var max = rows[0].count || 1;
  host.innerHTML = rows.map(function (r) {
    var pct = Math.max(2, (r.count / max) * 100);
    return '<div class="hb">' +
             '<span class="hb-l" title="' + escHtml(translateAction(r.action)) + '">' +
               escHtml(translateAction(r.action)) + '</span>' +
             '<span class="hb-t"><i style="width:' + pct.toFixed(1) + '%"></i></span>' +
             '<b class="hb-v">' + r.count + '</b>' +
           '</div>';
  }).join('');
}

async function loadAlerts() {
  const data = await apiFetch('/api/auditlogs/alerts');
  var el = document.getElementById('alertsBody');
  if (!data || data.length === 0) {
    var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
    el.innerHTML = '<div style="padding:8px;color:var(--gray-500);text-align:center">' + t('noSecurityAlerts') + '</div>';
    return;
  }
  el.innerHTML = data.map(function(a) {
    var bg = a.severity === 'high' ? '#fef3f2' : '#fffaeb';
    var clr = a.severity === 'high' ? '#b42318' : '#93370d';
    var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
    return '<div style="display:flex;align-items:center;gap:10px;padding:10px 12px;margin-bottom:8px;border-radius:10px;background:' + bg + ';border:1px solid ' + clr + '20">' +
      '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="' + clr + '" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>' +
      '<div><div style="font-weight:600;font-size:13px;color:' + clr + '">' + (lang === 'ar' ? a.message_ar : a.message_en) + '</div>' +
      '<div style="font-size:11px;color:' + clr + 'CC">' + t('count') + ': ' + a.count + '</div></div></div>';
  }).join('');
  generateExecSummary();
}

async function exportExcel() {
  try {
    var url = '/api/auditlogs/export';
    var f = getFilters();
    var qs = [];
    if (f.userId) qs.push('userId=' + encodeURIComponent(f.userId));
    if (f.actionGroup) qs.push('actionGroup=' + encodeURIComponent(f.actionGroup));
    if (f.fromDate) qs.push('fromDate=' + encodeURIComponent(f.fromDate));
    if (f.toDate) qs.push('toDate=' + encodeURIComponent(f.toDate));
    if (f.search) qs.push('search=' + encodeURIComponent(f.search));
    if (qs.length) url += '?' + qs.join('&');
    const res = await fetch(url, { credentials: 'same-origin' });
    // ⚠️ كان `if (!res.ok) return;` بلا أي رسالة: يضغط المستخدم فلا يحدث شيء
    //    إطلاقًا ولا يعرف هل الملف قيد التحضير أم أن الإجراء فشل.
    if (!res.ok) { NuhDialog.error(t('rep_expFailed')); return; }
    var blob = await res.blob();
    if (blob.size === 0) { NuhDialog.alert(t('rep_expEmpty')); return; }
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'AuditLogs.xlsx';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(a.href);
  } catch(e) { NuhDialog.error(t('rep_expFailed')); }
}

function exportPdf() {
  var activeGroup = document.querySelector('.tab-btn.active')?.getAttribute('data-group') || '';
  var typeMap = { '': 'activity', 'login': 'login', 'student': 'student', 'request': 'request' };
  var reportType = typeMap[activeGroup] || 'activity';

  // ⚠️ كان يرسل النوع فقط. المستخدم يحصر النتائج في آخر ٧ أيام أو في مستخدم
  //    بعينه ثم يضغط «PDF» فيخرج له التقرير كاملًا بلا أي فلتر - والفرق لا
  //    يظهر إلا لمن يقرأ التقرير بعناية. الخادم يقبل هذه الفلاتر أصلًا.
  var f = getFilters();
  var url = '/api/auditlogs/report-html?type=' + encodeURIComponent(reportType);
  if (f.userId) url += '&userId=' + encodeURIComponent(f.userId);
  if (f.fromDate) url += '&fromDate=' + encodeURIComponent(f.fromDate);
  if (f.toDate) url += '&toDate=' + encodeURIComponent(f.toDate);
  // ⚠️ تقويم العرض بيتبعت مع التقرير كمان: كان الموظف يبدّل لهجري ويطبع،
  //    فيراجع ورقة ميلادية على شاشة هجرية ويفتكر إن البيانات نفسها غلط.
  url += '&calendar=' + encodeURIComponent(NuhFmt.mode());

  // ⚠️ نافذة محجوبة من المتصفح كانت تعني «لا شيء يحدث» بلا تفسير.
  var w = window.open(url, '_blank');
  if (!w) NuhDialog.alert(t('rep_expBlocked'));
}

/* ====== Phase 2: Date Presets ====== */
function applyDatePreset(days) {
  document.querySelectorAll('.date-preset-btn').forEach(function(b) { b.classList.toggle('active', b.getAttribute('data-days') === days); });
  var now = new Date();
  var from = '', to = '';
  if (days) {
    var adjust = 3; // KSA offset hours
    now.setHours(now.getHours() + adjust);
    to = now.toISOString().slice(0, 10);
    if (days === 'this-month') { from = now.getFullYear() + '-' + String(now.getMonth()+1).padStart(2,'0') + '-01'; }
    else if (days === 'last-month') {
      var d = new Date(now.getFullYear(), now.getMonth(), 0);
      from = d.getFullYear() + '-' + String(d.getMonth()+1).padStart(2,'0') + '-01';
      to = d.getFullYear() + '-' + String(d.getMonth()+1).padStart(2,'0') + '-' + String(d.getDate()).padStart(2,'0');
    } else if (days === 'this-year') { from = now.getFullYear() + '-01-01'; }
    else {
      var ms = parseInt(days) * 86400000;
      var past = new Date(now.getTime() - ms);
      from = past.toISOString().slice(0, 10);
    }
  } else {
    document.getElementById('fromDate').value = '';
    document.getElementById('toDate').value = '';
    onFilterChange();
    return;
  }
  document.getElementById('fromDate').value = from;
  document.getElementById('toDate').value = to;
  onFilterChange();
}

/* ====== Phase 3 & 4: Top Users & Top Actions ====== */
var cachedTopData = [];

async function loadTopData() {
  try {
    var res = await fetch('/api/auditlogs?page=1&pageSize=500&sort=action_at&order=desc' + rangeQs('&'), { credentials: 'same-origin' });
    if (!res.ok) return;
    var body = await res.json();
    if (body.data) cachedTopData = body.data;
    topDataLoaded = true;
    computeTopWidgets();
    computeKpi();
    generateExecSummary();
  } catch(e) {}
}

function computeTopWidgets() {
  if (!cachedTopData.length && !cachedData) return;
  var source = cachedTopData.length ? cachedTopData : (cachedData || []);
  var userMap = {};
  var actionMap = {};
  source.forEach(function(r) {
    // ⚠️ String(null) = "null" - وده اللي كان بيظهر في «أكثر المستخدمين نشاطًا».
    //    العمليات اللي بينفّذها النظام نفسه (إرسال رمز تحقق مثلًا) مالهاش مستخدم.
    var name = r.user?.full_name || r.user?.username ||
               (r.user_id ? String(r.user_id) : t('rpx_systemUser'));
    userMap[name] = (userMap[name] || 0) + 1;
    actionMap[r.action] = (actionMap[r.action] || 0) + 1;
  });
  var topUsers = Object.entries(userMap).sort(function(a,b) { return b[1]-a[1]; }).slice(0, 5);
  var topActions = Object.entries(actionMap).sort(function(a,b) { return b[1]-a[1]; }).slice(0, 5);
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('topUsersList').innerHTML = topUsers.length ? topUsers.map(function(u) {
    return '<li class="mini-list-item" onclick="applyUserFilter(\'' + escHtml(u[0]) + '\')"><span class="name">' + escHtml(u[0]) + '</span><span class="count">' + u[1] + '</span></li>';
  }).join('') : '<li style="font-size:12px;color:var(--gray-400)">' + t('noData') + '</li>';
  document.getElementById('topActionsList').innerHTML = topActions.length ? topActions.map(function(a) {
    return '<li class="mini-list-item" onclick="applyActionFilter(\'' + escHtml(a[0]) + '\')"><span class="name">' + escHtml(translateAction(a[0])) + '</span><span class="count">' + a[1] + '</span></li>';
  }).join('') : '<li style="font-size:12px;color:var(--gray-400)">' + t('noData') + '</li>';

  // ⚠️ نفس المصدر بالظبط (source) مصفّى على مجموعة واحدة. التبويبات كانت
  //    بتقول «أي إجراء اتعمل» وما بتقولش «مين عمله»، والسؤال التاني هو اللي
  //    بيتسأل فعلًا. مفيش نداء إضافي: الـ ٥٠٠ سجل محمّلين أصلًا.
  renderTopUsersFor('topUsersStudent', source, 'student');
  renderTopUsersFor('topUsersRequest', source, 'request');
}

// ⚠️ قايمة إجراءات المجموعة من __AUDIT_GROUPS المحقونة في التخطيط، ومصدرها
//    Core/AuditActionGroups.cs. لو اتكتبت هنا كانت هتبقى نسخة تانية تفترق أول
//    ما يتضاف إجراء جديد - وده حصل فعلًا في السيرفر قبل كده.
function actionsInGroup(group) {
  var g = (window.__AUDIT_GROUPS || {})[group];
  return Array.isArray(g) ? g : [];
}

function renderTopUsersFor(hostId, source, group) {
  var host = document.getElementById(hostId);
  if (!host) return;

  var actions = actionsInGroup(group);
  var map = {};
  source.forEach(function (r) {
    if (actions.indexOf(r.action) === -1) return;
    var name = r.user?.full_name || r.user?.username ||
               (r.user_id ? String(r.user_id) : t('rpx_systemUser'));
    map[name] = (map[name] || 0) + 1;
  });

  var top = Object.entries(map).sort(function (a, b) { return b[1] - a[1]; }).slice(0, 5);
  host.innerHTML = top.length ? top.map(function (u) {
    return '<li class="mini-list-item" onclick="applyUserFilter(\'' + escHtml(u[0]) + '\')">' +
           '<span class="name">' + escHtml(u[0]) + '</span>' +
           '<span class="count">' + u[1] + '</span></li>';
  }).join('') : '<li style="font-size:12px;color:var(--gray-500)">' + t('noData') + '</li>';
}

function applyUserFilter(name) {
  var user = cachedUsers.find(function(u) { return (u.name === name || u.full_name === name); });
  if (user) { document.getElementById('userFilter').value = user.id; onFilterChange(); }
}

// ⚠️ كانت هنا سلسلة if مكتوبة بالإيد تعيد كتابة كل مجموعة إجراء إجراء -
//    نسخة خامسة من نفس القوائم، وكانت ناقصة faculty كلها. المصدر الوحيد
//    __AUDIT_GROUPS المحقونة في التخطيط من Core/AuditActionGroups.
function groupOfAction(action) {
  var g = window.__AUDIT_GROUPS || {};
  for (var k in g) {
    if (Object.prototype.hasOwnProperty.call(g, k) &&
        Array.isArray(g[k]) && g[k].indexOf(action) !== -1) return k;
  }
  return '';
}

function applyActionFilter(action) {
  document.getElementById('actionFilter').value = groupOfAction(action);
  onFilterChange();
}

/* ====== Phase 5: KPI Analytics ====== */
function computeKpi() {
  if (!cachedChartData) return;
  var d7 = cachedChartData.last7Days || [];
  var total7 = d7.reduce(function(s,d) { return s + d.count; }, 0);
  var avg = d7.length ? Math.round(total7 / d7.length) : 0;
  document.getElementById('kpiDailyAvg').textContent = avg;
  var peak = d7.reduce(function(b,d) { return (!b || d.count > b.count) ? d : b; }, null);
  document.getElementById('kpiPeakDay').textContent = peak ? peak.date.slice(5,10) : '-';
  var d30 = cachedChartData.login30 || [];
  var lastLogin = d30[d30.length-1];
  document.getElementById('kpiLastTime').textContent = lastLogin ? lastLogin.date.slice(5,10) : '-';
  var userCount = new Set((cachedTopData.length ? cachedTopData : (cachedData || [])).map(function(r) { return r.user_id; })).size;
  document.getElementById('kpiActiveUsers').textContent = userCount || document.getElementById('statUsers').textContent || '0';

  // إجمالي السبع أيام تحت بطاقة «إجمالي العمليات اليوم» - الرقم اليومي وحده
  // مايقولش إذا كان اليوم ده عالي ولا واطي.
  setTxt('kpiTotal7', total7 + ' ' + t('rpx_inLast7'));

  // تبويب تسجيل الدخول
  var login30 = d30.reduce(function (s, d) { return s + d.count; }, 0);
  setTxt('kpiLoginTotal', login30);
  setTxt('kpiLoginAvg', d30.length ? Math.round(login30 / d30.length) : 0);

  // ⚠️ نسخ مكرّرة من نفس الرقم في تبويب الأمان: المستخدم لازم يشوفه وهو واقف
  //    هناك، ومصدره واحد (today-stats) فما فيش خطر افتراق.
  setTxt('statFailedSec', document.getElementById('statFailed')?.textContent || '0');
  setTxt('statUsersSec',  document.getElementById('statUsers')?.textContent || '0');
}

function setTxt(id, v) { var e = document.getElementById(id); if (e) e.textContent = v; }

// تصنيف الإجراء لمجموعته - من __AUDIT_GROUPS لا من شروط مكتوبة بالإيد.
// ⚠️ الشرط القديم كان `a.indexOf('request') !== -1` لمجموعة الطلبات: أي إجراء
//    جديد فيه كلمة request يقع فيها بلا قصد، وأي إجراء طلب لا تحمله لا يقع.
function actionGroup(a) { return groupOfAction(a) || 'other'; }

/* ====== Phase 6: Drill-down Analytics ====== */
function applyDrill(type) {
  document.getElementById('actionFilter').value = '';
  document.getElementById('userFilter').value = '';
  document.getElementById('fromDate').value = '';
  document.getElementById('toDate').value = '';
  document.getElementById('searchBox').value = '';
  document.querySelectorAll('.tab-btn').forEach(function(t) { t.classList.remove('active'); });
  if (type === 'tot') { document.querySelector('.tab-btn[data-group=""]').classList.add('active'); }
  else if (type === 'users') { document.querySelector('.tab-btn[data-group=""]').classList.add('active'); }
  else if (type === 'student') {
    document.querySelector('.tab-btn[data-group="student"]').classList.add('active');
    document.getElementById('actionFilter').value = 'student';
  } else if (type === 'request') {
    document.querySelector('.tab-btn[data-group="request"]').classList.add('active');
    document.getElementById('actionFilter').value = 'request';
  } else if (type === 'fail') {
    document.querySelector('.tab-btn[data-group="login"]').classList.add('active');
    document.getElementById('actionFilter').value = 'login';
  } else if (type === 'delete') {
    document.querySelector('.tab-btn[data-group=""]').classList.add('active');
  }
  // ⚠️ اللوحة تتبع التبويب اللي اتفعّل فوق، وإلا بقى تبويب مضيء ولوحة تانية ظاهرة.
  var act = document.querySelector('.tab-btn.active');
  if (act) showReportPanel(act.getAttribute('data-panel') || 'overview');
  onFilterChange();
}

/* ====== الرابط القابل للإرسال ====== */
// ⚠️ الكتابة من NuhUrl (js/url-state.js) لا بـ replaceState مكتوبة هنا:
//    كانت بتبني الاستعلام من الصفر فبتمسح أي مفتاح مش من بتاعها، والصفحة
//    وعدد الصفوف والترتيب كانوا ناقصين أصلًا - فالرابط بيوصل لزميل بنفس
//    الفلاتر لكن على صفحة ١ وترتيب تاني: نتيجة صح لسؤال تاني.
var REP_DEFAULTS = { user: '', action: '', from: '', to: '', q: '', tab: '',
                     page: 1, size: 50, sort: '', asc: '' };

function syncUrlParams() {
  var f = getFilters();
  var active = document.querySelector('.tab-btn.active');
  NuhUrl.sync({
    user: f.userId, action: f.actionGroup,
    from: f.fromDate, to: f.toDate, q: f.search,
    tab: (active && active.getAttribute('data-group')) || '',
    page: currentPage, size: currentPageSize,
    sort: repSort.by(), asc: repSort.by() ? (repSort.asc() ? '1' : '0') : ''
  }, REP_DEFAULTS);
}

function loadFromUrlParams() {
  var p = new URLSearchParams(window.location.search);
  if (p.has('user')) document.getElementById('userFilter').value = p.get('user');
  if (p.has('action')) document.getElementById('actionFilter').value = p.get('action');
  if (p.has('from')) document.getElementById('fromDate').value = p.get('from');
  if (p.has('to')) document.getElementById('toDate').value = p.get('to');
  if (p.has('q')) document.getElementById('searchBox').value = p.get('q');
  if (p.has('tab')) {
    document.querySelectorAll('.tab-btn').forEach(function(t) { t.classList.toggle('active', t.getAttribute('data-group') === p.get('tab')); });
    if (p.get('tab') === 'security') {
      document.getElementById('logTbody').innerHTML = '<tr><td colspan="6" style="text-align:center;padding:40px"><div class="spinner"></div></td></tr>';
      loadAlerts();
      return;
    }
  }
}

/* ====== Phase 8: Executive Summary ====== */
function generateExecSummary() {
  // ⚠️ اتوقّفت عن قصد. كانت بتعيد كتابة نفس أرقام البطاقات اللي فوقها في جملة
  //    («إجمالي العمليات 129، بمتوسط 26 يوميًا، عدد عمليات اليوم 52…») -
  //    تكرار بيطوّل الصفحة ولا بيضيف معلومة. الدالة والعنصر باقيان عشان أي
  //    نداء قديم ما يرميش، ولو احتجناها ترجع بسطر واحد.
  return;
  /* eslint-disable no-unreachable */
  if (!cachedChartData && !dataLoaded) return;
  if (execTimeout) clearTimeout(execTimeout);
  execTimeout = setTimeout(function() {
    var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
    var el = document.getElementById('execSummary');
    var txt = document.getElementById('execSummaryText');
    var parts = [];
    if (cachedChartData) {
      var d7 = cachedChartData.last7Days || [];
      var total7 = d7.reduce(function(s,d) { return s + d.count; }, 0);
      var avg7 = d7.length ? Math.round(total7 / d7.length) : 0;
      parts.push(t('repj_exec_last7Prefix') + total7 + t('repj_exec_last7Mid') + avg7 + t('repj_exec_last7Suffix'));
    }
    var tot = document.getElementById('statTotOps').textContent;
    if (tot && tot !== '0') {
      parts.push(t('repj_exec_todayOpsPrefix') + tot + t('repj_exec_todayOpsSuffix'));
    }
    var userEl = document.getElementById('statUsers');
    if (userEl.textContent !== '0') {
      parts.push(t('repj_exec_activeUsersPrefix') + userEl.textContent + t('repj_exec_activeUsersSuffix'));
    }
    if (cachedChartData && cachedChartData.last7Days && cachedChartData.last7Days.length) {
      var peak = cachedChartData.last7Days.reduce(function(b,d) { return (!b || d.count > b.count) ? d : b; }, null);
      if (peak) {
        parts.push(t('repj_exec_peakDayPrefix') + peak.date.slice(5,10) + ' (' + peak.count + t('repj_exec_peakDaySuffix'));
      }
    }
    var alertEl = document.getElementById('alertsBody');
    if (alertEl && alertEl.textContent.indexOf(t('noSecurityAlerts')) === -1 && alertEl.textContent.indexOf('No security') === -1 && alertEl.textContent.indexOf('✓') === -1 && alertEl.textContent !== t('loading') && alertEl.textContent !== 'Loading...') {
      parts.push(t('repj_exec_securityAlerts'));
    }
    if (parts.length) {
      el.style.display = 'block';
      txt.textContent = parts.join(' ');
    } else {
      el.style.display = 'none';
    }
  }, 300);
}

/* ====== Phase 9: Performance - debounced search ====== */
var searchDebounce = null;
document.getElementById('searchBox').addEventListener('keyup', function(e) {
  if (e.key === 'Enter') { if (searchDebounce) clearTimeout(searchDebounce); onFilterChange(); return; }
  if (searchDebounce) clearTimeout(searchDebounce);
  searchDebounce = setTimeout(function() { onFilterChange(); }, 400);
});

// زر الخروج بقى فورم /Account/Logout في اللياوت الموحد - مفيش override هنا

// ⚠️ مفيش مستمعات للسابق/التالي/عدد الصفوف هنا: صفّ الترقيم بيربطها بنفسه
//    وقت الرسم (NuhTable.pager)، فلو اتكتبت هنا كمان هتشتغل مرتين.

// ⚠️ السجل التفصيلي مطوي افتراضيًا: الصفحة تقارير، والجدول الخام له شاشته
//    الخاصة (سجل العمليات). الحالة متحفوظة عشان اللي محتاجه مفتوح ما يفتحهوش
//    كل مرة - والبيانات بتتحمّل في الحالتين لأن الأعداد في الرأس بتتغذّى منها.
function toggleLog(force) {
  var box = document.getElementById('logDetails');
  var btn = document.getElementById('logToggle');
  if (!box || !btn) return;
  var open = (typeof force === 'boolean') ? force : box.hasAttribute('hidden');
  if (open) box.removeAttribute('hidden'); else box.setAttribute('hidden', '');
  btn.textContent = t(open ? 'rpx_hideLog' : 'rpx_showLog');
  try { localStorage.setItem('reportsLogOpen', open ? '1' : '0'); } catch (e) {}
}
toggleLog(localStorage.getItem('reportsLogOpen') === '1');

document.getElementById('userFilter').addEventListener('change', onFilterChange);
document.getElementById('actionFilter').addEventListener('change', onFilterChange);
document.getElementById('fromDate').addEventListener('change', onFilterChange);
document.getElementById('toDate').addEventListener('change', onFilterChange);

document.getElementById('modalCloseBtn').addEventListener('click', closeModal);
document.getElementById('detailModal').addEventListener('click', function(e) {
  if (e.target === this) closeModal();
});
document.addEventListener('keydown', function(e) {
  if (e.key === 'Escape') closeModal();
});

// ⚠️ التبويب كان بيغيّر فلتر الجدول وبس، وباقي الصفحة يفضل زي ما هو - فتقف
//    على «عمليات الطلبات» وقدّامك رسم «تسجيل الدخول» فاضي. بقى بيبدّل لوحة
//    كاملة: مؤشرات التبويب ورسمه، والجدول تحت بيتفلتر معاه.
function showReportPanel(name) {
  document.querySelectorAll('.rp-panel').forEach(function (p) {
    p.classList.toggle('on', p.id === 'panel-' + name);
  });
  var tabEl = document.querySelector('.tab-btn[data-panel="' + name + '"]');
  var lbl = document.getElementById('tableScopeLabel');
  if (lbl && tabEl) lbl.textContent = tabEl.textContent.trim();
}

document.querySelectorAll('.tab-btn').forEach(function(tab) {
  tab.addEventListener('click', function() {
    document.querySelectorAll('.tab-btn').forEach(function(t) { t.classList.remove('active'); });
    this.classList.add('active');
    showReportPanel(this.getAttribute('data-panel') || 'overview');
    var group = this.getAttribute('data-group');
    if (group === 'security') {
      loadAlerts();
      // ⚠️ الجدول مابيتفضّاش هنا زي الأول: تنبيهات الأمان بقى لها مكانها في
      //    لوحة الأمان، والجدول تحتها بيفضل شغّال بفلتره - إفراغه كان بيخلّي
      //    التبويب ده الوحيد اللي بيوقف السجل بلا سبب.
      document.getElementById('actionFilter').value = '';
    } else {
      document.getElementById('actionFilter').value = group;
    }
    onFilterChange();
  });
});

document.querySelectorAll('.date-preset-btn').forEach(function(btn) {
  btn.addEventListener('click', function() { applyDatePreset(this.getAttribute('data-days')); });
});

document.querySelectorAll('.stat-card-clickable').forEach(function(card) {
  card.addEventListener('click', function() { applyDrill(this.getAttribute('data-drill')); });
});

// ⚠️ كانت هنا نسخة كاملة من كود الإشعارات (fetchNotifs / loadNotifDropdown /
//    loadUnreadCount / toggleNotifDropdown / markNotifRead / markAllNotifRead)
//    تعيد تعريف نفس الدوال الموجودة في التخطيط المشترك وتعمل على نفس عناصره -
//    نسختان من منطق واحد ومؤقّتان يعملان معًا. حُذفت، والتخطيط هو المسؤول.

loadFromUrlParams();
setLang(localStorage.getItem('uiLanguage') || 'ar');
rebuildActionFilter();
loadActionGroups();
// ⚠️ الترتيب مقصود: النداءات الست تنطلق معًا، لكن المتصفح يحدّ عدد الاتصالات
//    المتزامنة لكل خادم. البطاقات الست وتنبيهات الأمان استعلامات صغيرة ويراها
//    المستخدم أول ما تفتح الصفحة، فتسبق. سجل العمليات والرسوم أثقل فتليها.
loadSummaryStats();
loadAlerts();
loadLogs(currentPage, currentPageSize);
loadCharts();
loadAuditUsers();
