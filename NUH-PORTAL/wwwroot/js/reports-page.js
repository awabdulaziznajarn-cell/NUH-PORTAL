const token = localStorage.getItem('staffToken');
if (!token) window.location.replace('/Account/Login');
const currentUser = JSON.parse(localStorage.getItem('staffUser') || '{}');
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

function escHtml(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function logout() {
  localStorage.removeItem('staffToken');
  localStorage.removeItem('staffUser');
  window.location.replace('/Account/Login');
}

async function apiFetch(url) {
  try {
    const res = await fetch(url, { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return null; }
    return await res.json();
  } catch(e) { return null; }
}

function setLang(l) {
  __baseSetLang(l);
  var d = new Date();
  document.getElementById('dateNow').textContent = l === 'ar'
    ? d.toLocaleDateString('ar-SA', {weekday:'long', year:'numeric', month:'long', day:'numeric'})
    : d.toLocaleDateString('en-US', {weekday:'long', year:'numeric', month:'long', day:'numeric'});
  document.getElementById('sidebar-user-name').textContent = currentUser.full_name || currentUser.username || '';
  document.getElementById('sidebar-user-role').textContent = currentUser.role || '';
  rebuildActionFilter();
  var userFilterEl = document.getElementById('userFilter');
  var allOption = userFilterEl.options[0];
  if (allOption) { allOption.textContent = t('all'); }
  if (dataLoaded) { updatePagination(totalRecords); renderTable(); }
}

var actionFilterOptions = [
  { value: '', label: t('repj_filter_all') },
  { value: 'login', label: t('repj_filter_login') },
  { value: 'student', label: t('repj_filter_student') },
  { value: 'request', label: t('repj_filter_request') },
  { value: 'password', label: t('repj_filter_password') },
  { value: 'logout', label: t('repj_filter_logout') },
  { value: 'ad', label: t('repj_filter_ad') }
];

function rebuildActionFilter() {
  var sel = document.getElementById('actionFilter');
  var currentVal = sel.value;
  sel.innerHTML = '';
  (actionFilterOptions || []).forEach(function(opt) {
    var el = document.createElement('option');
    el.value = opt.value;
    el.textContent = opt.label;
    sel.appendChild(el);
  });
  sel.value = currentVal;
}

function populateUserFilter(users) {
  var sel = document.getElementById('userFilter');
  var currentVal = sel.value;
  sel.innerHTML = '<option value="">' + t('all') + '</option>';
  users.forEach(function(u) {
    var el = document.createElement('option');
    el.value = u.id;
    el.textContent = u.name;
    sel.appendChild(el);
  });
  sel.value = currentVal;
}

var currentPage = 1;
var currentPageSize = 50;
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

function formatDate(dateStr) {
  if (!dateStr) return '';
  var d = new Date(dateStr);
  if (isNaN(d.getTime())) return '';
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  var y = d.getFullYear();
  var mo = String(d.getMonth() + 1).padStart(2, '0');
  var da = String(d.getDate()).padStart(2, '0');
  var h = String(d.getHours()).padStart(2, '0');
  var mi = String(d.getMinutes()).padStart(2, '0');
  return lang === 'ar' ? da + '/' + mo + '/' + y + ' ' + h + ':' + mi : mo + '/' + da + '/' + y + ' ' + h + ':' + mi;
}

function getFilters() {
  return {
    userId: document.getElementById('userFilter').value,
    actionGroup: document.getElementById('actionFilter').value,
    fromDate: document.getElementById('fromDate').value,
    toDate: document.getElementById('toDate').value,
    search: document.getElementById('searchBox').value.trim()
  };
}

function buildUrl(p, ps) {
  var url = '/api/auditlogs?page=' + p + '&pageSize=' + ps;
  var f = getFilters();
  if (f.userId) url += '&userId=' + encodeURIComponent(f.userId);
  if (f.actionGroup) url += '&actionGroup=' + encodeURIComponent(f.actionGroup);
  if (f.fromDate) url += '&fromDate=' + encodeURIComponent(f.fromDate);
  if (f.toDate) url += '&toDate=' + encodeURIComponent(f.toDate);
  if (f.search) url += '&search=' + encodeURIComponent(f.search);
  return url;
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
    return '<tr onclick="openModal(cachedData[' + i + '])"><td style="color:#8891A8">' + rowNum +
    '</td><td>' + escHtml(l.user?.full_name ?? l.user?.username ?? l.user_id) +
    '</td><td><span class="badge ' + bCls + '">' + bLabel + '</span>' +
    '</td><td>' + escHtml(l.target_table || '') +
    '</td><td>' + escHtml(String(l.target_id || '')) +
    '</td><td>' + formatDate(l.action_at) +
    '</td></tr>';
  }).join('');
}

function updatePagination(recs) {
  document.getElementById('pageIndicator').textContent = currentPage + ' / ' + (totalPages || 1);
  document.getElementById('prevPageBtn').disabled = currentPage <= 1;
  document.getElementById('nextPageBtn').disabled = currentPage >= totalPages;
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('totalRecordsLabel').textContent = t('totalRecords') + ': ' + recs;
}

function updateStats(stats) {
  if (!stats) return;
  document.getElementById('statTotalRecords').textContent = stats.totalRecords || 0;
  document.getElementById('statStudentRecs').textContent = stats.studentOperations || 0;
  document.getElementById('statRequestRecs').textContent = stats.requestOperations || 0;
}

function openModal(record) {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('modalUser').textContent = record.user?.full_name ?? record.user?.username ?? record.user_id;
  document.getElementById('modalAction').innerHTML = escHtml(translateAction(record.action));
  document.getElementById('modalTable').textContent = record.target_table || '';
  document.getElementById('modalTargetId').textContent = String(record.target_id || '');
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
    const res = await fetch(buildUrl(p, ps), { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    const body = await res.json();
    cachedData = body.data;
    totalPages = body.totalPages;
    totalRecords = body.totalRecords;
    dataLoaded = true;
    renderTable();
    updatePagination(body.totalRecords);
    updateStats(body.stats);
    syncUrlParams();
    if (!topDataLoaded) loadTopData();
    if (cachedChartData) { computeKpi(); computeTopWidgets(); generateExecSummary(); }
  } catch(e) {
    dataLoaded = true;
    document.getElementById('logTbody').innerHTML = '<tr><td colspan="6"><div class="empty-state"><div class="empty-icon">⚠️</div><div class="empty-text" style="color:#991B1B">' + t('repj_error_connection') + '</div></div></td></tr>';
  }
}

async function loadAuditUsers() {
  try {
    const res = await fetch('/api/auditlogs/users', { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.ok) {
      const users = await res.json();
      cachedUsers = users;
      populateUserFilter(users);
    }
  } catch(e) {}
}

function applyFilters() {
  loadLogs(1, currentPageSize);
}

function onFilterChange() {
  loadLogs(1, currentPageSize);
}

async function loadSummaryStats() {
  const data = await apiFetch('/api/auditlogs/today-stats');
  if (!data) return;
  document.getElementById('statTotOps').textContent = data.todayTotalOps || 0;
  document.getElementById('statUsers').textContent = data.todayActiveUsers || 0;
  document.getElementById('statStudentOps').textContent = data.todayStudentOps || 0;
  document.getElementById('statRequestOps').textContent = data.todayRequestOps || 0;
  document.getElementById('statFailed').textContent = data.todayFailedLogins || 0;
  document.getElementById('statDeletes').textContent = data.todayDeletes || 0;
}

async function loadCharts() {
  const data = await apiFetch('/api/auditlogs/chart-data');
  if (!data) return;
  cachedChartData = data;
  if (data.last7Days && data.last7Days.length) {
    var labels = data.last7Days.map(function(d) { return d.date.slice(5, 10); });
    var counts = data.last7Days.map(function(d) { return d.count; });
    renderChart('chartOps7', 'bar', labels, counts, '#1B2A5E');
  }
  if (data.login30 && data.login30.length) {
    var labels = data.login30.map(function(d) { return d.date.slice(5, 10); });
    var counts = data.login30.map(function(d) { return d.count; });
    renderChart('chartLogins30', 'line', labels, counts, '#C9A84C');
  }
  if (data.studentOps && data.studentOps.length) {
    var labels = data.studentOps.map(function(d) { return d.action; });
    var counts = data.studentOps.map(function(d) { return d.count; });
    var colors = ['#1B2A5E','#C9A84C','#991B1B'];
    renderChart('chartStudents', 'doughnut', labels, counts, colors);
  }
  if (data.requestOps && data.requestOps.length) {
    var labels = data.requestOps.map(function(d) { return d.action; });
    var counts = data.requestOps.map(function(d) { return d.count; });
    var colors = ['#0F6E56','#C9A84C','#991B1B'];
    renderChart('chartRequests', 'doughnut', labels, counts, colors);
  }
  if (dataLoaded) { computeKpi(); computeTopWidgets(); generateExecSummary(); }
}

var chartInstances = {};

function renderChart(id, type, labels, data, bgColors) {
  if (chartInstances[id]) chartInstances[id].destroy();
  var ctx = document.getElementById(id).getContext('2d');
  chartInstances[id] = new Chart(ctx, {
    type: type,
    data: {
      labels: labels,
      datasets: [{
        data: data,
        backgroundColor: bgColors || '#1B2A5E',
        borderColor: '#1B2A5E',
        borderWidth: 1.5,
        tension: 0.3,
        fill: false
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: { y: { beginAtZero: true, ticks: { stepSize: 1 } } }
    }
  });
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
    var bg = a.severity === 'high' ? '#FEF2F2' : '#FFF7ED';
    var clr = a.severity === 'high' ? '#991B1B' : '#92400E';
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
    const res = await fetch(url, { headers: { 'Authorization': 'Bearer ' + token } });
    if (!res.ok) return;
    var blob = await res.blob();
    if (blob.size === 0) return;
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'AuditLogs.xlsx';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(a.href);
  } catch(e) {}
}

function exportPdf() {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  var activeGroup = document.querySelector('.report-tab.active')?.getAttribute('data-group') || '';
  var typeMap = { '': 'activity', 'login': 'login', 'student': 'student', 'request': 'request' };
  var reportType = typeMap[activeGroup] || 'activity';
  window.open('/api/auditlogs/report-html?type=' + reportType, '_blank');
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
    var res = await fetch('/api/auditlogs?page=1&pageSize=500&sort=action_at&order=desc', { headers: { 'Authorization': 'Bearer ' + token } });
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
    var name = r.user?.full_name || r.user?.username || String(r.user_id);
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
}

function applyUserFilter(name) {
  var user = cachedUsers.find(function(u) { return (u.name === name || u.full_name === name); });
  if (user) { document.getElementById('userFilter').value = user.id; onFilterChange(); }
}

function applyActionFilter(action) {
  var val = '';
  if (action === 'login' || action === 'login_failed' || action === 'login_admin_fallback' || action === 'login_admin_fallback_failed') val = 'login';
  else if (action === 'logout') val = 'logout';
  else if (action === 'set_password') val = 'password';
  else if (action === 'create_student' || action === 'update_student' || action === 'delete_student' || action === 'checkout_student') val = 'student';
  else if (action === 'create_request' || action === 'approve_request' || action === 'reject_request' ||
           action === 'housing_approve_request' || action === 'housing_reject_request' ||
           action === 'submit_cyber_review' ||
           action === 'cyber_approve_request' || action === 'cyber_reject_request') val = 'request';
  else if (action === 'user_created_ad' || action === 'user_updated_ad') val = 'ad';
  document.getElementById('actionFilter').value = val;
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
}

/* ====== Phase 6: Drill-down Analytics ====== */
function applyDrill(type) {
  document.getElementById('actionFilter').value = '';
  document.getElementById('userFilter').value = '';
  document.getElementById('fromDate').value = '';
  document.getElementById('toDate').value = '';
  document.getElementById('searchBox').value = '';
  document.querySelectorAll('.report-tab').forEach(function(t) { t.classList.remove('active'); });
  if (type === 'tot') { document.querySelector('.report-tab[data-group=""]').classList.add('active'); }
  else if (type === 'users') { document.querySelector('.report-tab[data-group=""]').classList.add('active'); }
  else if (type === 'student') {
    document.querySelector('.report-tab[data-group="student"]').classList.add('active');
    document.getElementById('actionFilter').value = 'student';
  } else if (type === 'request') {
    document.querySelector('.report-tab[data-group="request"]').classList.add('active');
    document.getElementById('actionFilter').value = 'request';
  } else if (type === 'fail') {
    document.querySelector('.report-tab[data-group="login"]').classList.add('active');
    document.getElementById('actionFilter').value = 'login';
  } else if (type === 'delete') {
    document.querySelector('.report-tab[data-group=""]').classList.add('active');
  }
  onFilterChange();
}

/* ====== Phase 7: Shareable URL ====== */
function syncUrlParams() {
  var f = getFilters();
  var p = new URLSearchParams();
  if (f.userId) p.set('user', f.userId);
  if (f.actionGroup) p.set('action', f.actionGroup);
  if (f.fromDate) p.set('from', f.fromDate);
  if (f.toDate) p.set('to', f.toDate);
  if (f.search) p.set('q', f.search);
  var g = document.querySelector('.report-tab.active')?.getAttribute('data-group') || '';
  if (g) p.set('tab', g);
  var qs = p.toString();
  var url = window.location.pathname + (qs ? '?' + qs : '');
  window.history.replaceState(null, '', url);
}

function loadFromUrlParams() {
  var p = new URLSearchParams(window.location.search);
  if (p.has('user')) document.getElementById('userFilter').value = p.get('user');
  if (p.has('action')) document.getElementById('actionFilter').value = p.get('action');
  if (p.has('from')) document.getElementById('fromDate').value = p.get('from');
  if (p.has('to')) document.getElementById('toDate').value = p.get('to');
  if (p.has('q')) document.getElementById('searchBox').value = p.get('q');
  if (p.has('tab')) {
    document.querySelectorAll('.report-tab').forEach(function(t) { t.classList.toggle('active', t.getAttribute('data-group') === p.get('tab')); });
    if (p.get('tab') === 'security') {
      document.getElementById('logTbody').innerHTML = '<tr><td colspan="6" style="text-align:center;padding:40px"><div class="spinner"></div></td></tr>';
      loadAlerts();
      return;
    }
  }
}

/* ====== Phase 8: Executive Summary ====== */
function generateExecSummary() {
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

// زر الخروج بقى فورم /Account/Logout في اللياوت الموحد — مفيش override هنا

document.getElementById('prevPageBtn').addEventListener('click', function() {
  if (currentPage > 1) loadLogs(currentPage - 1, currentPageSize);
});

document.getElementById('nextPageBtn').addEventListener('click', function() {
  if (currentPage < totalPages) loadLogs(currentPage + 1, currentPageSize);
});

document.getElementById('pageSizeSelect').addEventListener('change', function() {
  loadLogs(1, parseInt(this.value));
});

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

document.querySelectorAll('.report-tab').forEach(function(tab) {
  tab.addEventListener('click', function() {
    document.querySelectorAll('.report-tab').forEach(function(t) { t.classList.remove('active'); });
    this.classList.add('active');
    var group = this.getAttribute('data-group');
    if (group === 'security') {
      document.getElementById('logTbody').innerHTML = '<tr><td colspan="6" style="text-align:center;padding:40px"><div class="spinner"></div></td></tr>';
      loadAlerts();
      return;
    }
    document.getElementById('actionFilter').value = group;
    onFilterChange();
  });
});

document.querySelectorAll('.date-preset-btn').forEach(function(btn) {
  btn.addEventListener('click', function() { applyDatePreset(this.getAttribute('data-days')); });
});

document.querySelectorAll('.stat-card-clickable').forEach(function(card) {
  card.addEventListener('click', function() { applyDrill(this.getAttribute('data-drill')); });
});

async function fetchNotifs() {
  try {
    const res = await fetch('/api/notifications?role=' + encodeURIComponent(_userRole), { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return null; }
    return await res.json();
  } catch(e) { return null; }
}
async function loadNotifDropdown() {
  const data = await fetchNotifs();
  const list = document.getElementById('notif-dropdown-list');
  if (!data) { list.innerHTML = '<div class="notif-empty">' + t('repj_notif_apiError') + '</div>'; return; }
  list.innerHTML = data.length ? data.map(function(n) {
    return '<div class="notif-item" onclick="markNotifRead(' + n.id + ',this)" data-id="' + n.id + '">' +
      '<div class="notif-item-dot" style="background:' + (n.status === 'pending' ? '#1B2A5E' : '#CBD5E1') + ';"></div>' +
      '<div>' +
        '<div class="notif-item-text">' + escHtml(n.message) + '</div>' +
        '<div class="notif-item-time">' + new Date(n.sent_at || Date.now()).toLocaleDateString('ar-SA') + '</div>' +
      '</div>' +
    '</div>';
  }).join('') : '<div class="notif-empty">' + t('repj_notif_empty') + '</div>';
}
async function loadUnreadCount() {
  try {
    const res = await fetch('/api/notifications/unread-count?role=' + encodeURIComponent(_userRole), { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    var d = await res.json();
    var badge = document.getElementById('notif-badge');
    var dot = document.querySelector('.notif-dot');
    if (d && d.count > 0) { badge.textContent = d.count; badge.classList.add('show'); if (dot) dot.classList.remove('hidden'); } else { badge.classList.remove('show'); if (dot) dot.classList.add('hidden'); }
  } catch(e) {}
}
function toggleNotifDropdown(e) {
  e.stopPropagation();
  var dd = document.getElementById('notif-dropdown');
  var isOpen = dd.classList.contains('open');
  document.querySelectorAll('.notif-dropdown.open').forEach(function(d) { d.classList.remove('open'); });
  if (!isOpen) { dd.classList.add('open'); if (!dd.dataset.loaded) { loadNotifDropdown(); dd.dataset.loaded = '1'; } }
}
async function markNotifRead(id, el) {
  try { await fetch('/api/notifications/read', { method: 'PATCH', headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' }, body: JSON.stringify([id]) }); el.querySelector('.notif-item-dot').style.background = '#CBD5E1'; loadUnreadCount(); } catch(e) {}
}
async function markAllNotifRead() {
  var items = document.querySelectorAll('#notif-dropdown-list .notif-item[data-id]'), ids = [];
  items.forEach(function(el) { ids.push(parseInt(el.dataset.id)); });
  if (!ids.length) return;
  try { await fetch('/api/notifications/read', { method: 'PATCH', headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' }, body: JSON.stringify(ids) }); document.querySelectorAll('#notif-dropdown-list .notif-item-dot').forEach(function(d) { d.style.background = '#CBD5E1'; }); loadUnreadCount(); } catch(e) {}
}
document.addEventListener('click', function(e) { if (!e.target.closest('.notif-wrapper')) { document.querySelectorAll('.notif-dropdown.open').forEach(function(d) { d.classList.remove('open'); }); } });

loadFromUrlParams();
setLang(localStorage.getItem('uiLanguage') || 'ar');
rebuildActionFilter();
loadLogs(1, currentPageSize);
loadAuditUsers();
loadSummaryStats();
loadCharts();
loadAlerts();
loadUnreadCount();
setInterval(loadUnreadCount, 30000);
