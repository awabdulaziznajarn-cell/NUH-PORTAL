const token = localStorage.getItem('staffToken');
if (!token) window.location.replace('/Account/Login');
const currentUser = JSON.parse(localStorage.getItem('staffUser') || '{}');
const _userRole = (currentUser.role || '').toLowerCase();
if (_userRole !== 'supervisor') {
  document.body.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:100vh;font-size:18px;color:var(--red)">' + t('accessDenied') + '</div>';
}

window.onLanguageChange = function(l) {
  var d = new Date();
  document.getElementById('dateNow').textContent = l === 'ar'
    ? d.toLocaleDateString('ar-SA', {weekday:'long', year:'numeric', month:'long', day:'numeric'})
    : d.toLocaleDateString('en-US', {weekday:'long', year:'numeric', month:'long', day:'numeric'});
  document.title = t('updateStudentStatus');
  loadHistory();
};

function escHtml(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function showToast(msg, type) {
  var el = document.getElementById('toast');
  el.textContent = msg;
  el.className = 'toast show toast-' + (type || 'success');
  setTimeout(function() { el.classList.remove('show'); }, 4000);
}

function selectStatus(el) {
  document.querySelectorAll('.status-option').forEach(function(s) { s.classList.remove('selected'); });
  el.classList.add('selected');
  el.querySelector('input[type=radio]').checked = true;
  updateSubmitBtnState();
}

function updateFileName(input) {
  var nameEl = document.getElementById('fileName');
  nameEl.textContent = input.files.length > 0 ? input.files[0].name : '';
}

function clearErrors() {
  document.querySelectorAll('.field-error').forEach(function(el) { el.classList.remove('show'); });
  document.querySelectorAll('.input-wrap input.error, .input-wrap select.error, textarea.error').forEach(function(el) { el.classList.remove('error'); });
}

function showError(id) {
  document.getElementById(id).classList.add('show');
  var input = document.getElementById(id.replace('Error', ''));
  if (input) input.classList.add('error');
}

var pendingSubmitData = null;
var studentLoaded = false;

function updateSubmitBtnState() {
  var sn = document.getElementById('studentNumber').value.trim();
  var statusEl = document.querySelector('input[name=statusType]:checked');
  var notes = document.getElementById('notes').value.trim();
  var canSubmit = studentLoaded && statusEl && notes && notes.length >= 5;
  var btn = document.getElementById('submitBtn');
  btn.disabled = !canSubmit;
  if (canSubmit) {
    btn.style.background = 'var(--navy)';
  } else {
    btn.style.background = '';
  }
}

function closeConfirmDialog() {
  document.getElementById('confirmDialog').classList.remove('show');
  pendingSubmitData = null;
}

function resetForm() {
  document.getElementById('studentNumber').value = '';
  document.querySelectorAll('.status-option').forEach(function(s) { s.classList.remove('selected'); });
  document.querySelectorAll('input[name=statusType]').forEach(function(r) { r.checked = false; });
  document.getElementById('notes').value = '';
  document.getElementById('attachmentFile').value = '';
  document.getElementById('fileName').textContent = '';
  document.getElementById('studentInfo').style.display = 'none';
  clearErrors();
  pendingSubmitData = null;
  studentLoaded = false;
  updateSubmitBtnState();
}

function getStatusLabel(value) {
  var labels = {
    graduated: t('studentStatusGraduated'),
    dismissed: t('studentStatusDismissed'),
    transferred: t('studentStatusTransferred'),
    left_housing: t('studentStatusLeftHousing')
  };
  return labels[value] || value;
}

async function handleSubmit() {
  clearErrors();
  var studentNumber = document.getElementById('studentNumber').value.trim();
  var statusEl = document.querySelector('input[name=statusType]:checked');
  var notes = document.getElementById('notes').value.trim();
  var file = document.getElementById('attachmentFile').files[0];
  var valid = true;

  if (!studentNumber) { showError('studentNumberError'); valid = false; }
  if (!statusEl) { showError('statusTypeError'); valid = false; }
  if (!notes || notes.length < 5) { showError('notesError'); valid = false; }

  if (!valid) return;

  document.getElementById('submitBtn').disabled = true;
  document.getElementById('submitBtn').innerHTML = '<span>...</span>';

  try {
    var res = await fetch('/api/students', { headers: { 'Authorization': 'Bearer ' + token } });
    if (!res.ok) { showToast(t('connectionError'), 'error'); enableSubmitBtn(); return; }
    var students = await res.json();
    var student = Array.isArray(students) ? students.find(function(s) { return s.student_id === studentNumber; }) : null;
    if (!student) { showToast(t('studentNotFound'), 'error'); enableSubmitBtn(); return; }

    var statusLabel = getStatusLabel(statusEl.value);
    var adUsername = student.ad_username || '--';

    document.getElementById('confirmUsername').textContent = adUsername;
    document.getElementById('confirmStatus').textContent = statusLabel;
    document.getElementById('confirmDialog').classList.add('show');

    pendingSubmitData = {
      studentNumber: studentNumber,
      statusType: statusEl.value,
      notes: notes,
      file: file
    };
  } catch (e) {
    showToast(t('connectionError'), 'error');
  } finally {
    enableSubmitBtn();
  }
}

function enableSubmitBtn() {
  document.getElementById('submitBtn').disabled = false;
  var btnText = t('confirmAction');
  document.getElementById('submitBtn').innerHTML = '<span>' + btnText + '</span>';
}

async function executeSubmit() {
  closeConfirmDialog();
  if (!pendingSubmitData) return;

  document.getElementById('submitBtn').disabled = true;
  document.getElementById('submitBtn').innerHTML = '<span>...</span>';

  try {
    var formData = new FormData();
    formData.append('studentNumber', pendingSubmitData.studentNumber);
    formData.append('statusType', pendingSubmitData.statusType);
    formData.append('notes', pendingSubmitData.notes);
    if (pendingSubmitData.file) formData.append('file', pendingSubmitData.file);

    const res = await fetch('/api/supervisor/student-status', {
      method: 'POST',
      headers: { 'Authorization': 'Bearer ' + token },
      body: formData
    });

    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    if (res.status === 403) { showToast(t('accessDenied'), 'error'); return; }

    var result = await res.json();
    if (!res.ok) {
      showToast(result.message || t('errorOccurred'), 'error');
      return;
    }

    if (result.warning) {
      showToast(result.message + '. ' + result.warning, 'error');
    } else {
      showToast(result.message || t('statusSaved'), 'success');
    }

    resetForm();
    loadHistory();
  } catch (e) {
    showToast(t('connectionError'), 'error');
  } finally {
    enableSubmitBtn();
  }
}

var userMap = {};
async function loadUsers() {
  try {
    var res = await fetch('/api/users', { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.ok) {
      var data = await res.json();
      data.forEach(function(u) { userMap[u.id] = u.full_name || u.username || ''; });
    }
  } catch(e) {}
}

async function loadHistory() {
  try {
    var res = await fetch('/api/supervisor/student-status/recent', { headers: { 'Authorization': 'Bearer ' + token } });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    var data = await res.json();
    var list = document.getElementById('historyList');
    if (!data || !data.length) {
      list.innerHTML = '<div style="text-align:center;padding:30px;color:var(--gray-500)">' + t('noData') + '</div>';
      return;
    }
    var icons = { graduated: '\uD83C\uDF93', dismissed: '\u274C', transferred: '\uD83D\uDEEB\uFE0F', left_housing: '\uD83C\uDFE0' };
    list.innerHTML = data.map(function(a) {
      var lang = (document.getElementById('html-root') || document.documentElement).getAttribute('lang') || 'ar';
      var label = getStatusLabel(a.statusType);
      var icon = icons[a.statusType] || '\uD83D\uDD35';
      var badgeClass = 'badge-status-' + a.statusType;
      var dateObj = new Date(a.createdDate);
      var dateStr = dateObj.toLocaleDateString(lang === 'ar' ? 'ar-SA' : 'en-US', {year:'numeric', month:'short', day:'numeric'});
      var timeStr = dateObj.toLocaleTimeString(lang === 'ar' ? 'ar-SA' : 'en-US', {hour:'2-digit', minute:'2-digit'});
      var userName = userMap[a.createdBy] || t('unknown');
      var attachHtml = '';
      if (a.attachmentFileName) {
        attachHtml = '<div style="margin-top:6px;font-size:12px;color:var(--navy)">\uD83D\uDCCE <a href="/uploads/student-status/' + a.id + '/' + encodeURIComponent(a.attachmentFileName) + '" target="_blank" style="color:var(--navy);text-decoration:underline">' + escHtml(a.attachmentFileName) + '</a></div>';
      }
      return '<div class="history-item" style="display:block;padding:14px 16px;border-bottom:1px solid var(--gray-100)">' +
        '<div style="margin-bottom:8px"><span class="badge ' + badgeClass + '" style="font-size:13px;padding:4px 12px">' + icon + ' ' + escHtml(label) + '</span></div>' +
        '<div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;font-size:13px;color:var(--navy-dark)">' +
          '<div><span style="color:var(--gray-500);font-size:12px">' + t('studentNumber') + '</span><br><strong>' + escHtml(a.studentNumber) + '</strong></div>' +
          (a.studentName ? '<div><span style="color:var(--gray-500);font-size:12px">' + t('studentName') + '</span><br>' + escHtml(a.studentName) + '</div>' : '') +
          '<div><span style="color:var(--gray-500);font-size:12px">' + t('by') + '</span><br>' + escHtml(userName) + '</div>' +
          '<div><span style="color:var(--gray-500);font-size:12px">' + t('date') + '</span><br>' + dateStr + '</div>' +
          '<div><span style="color:var(--gray-500);font-size:12px">' + t('time') + '</span><br>' + timeStr + '</div>' +
        '</div>' +
        (a.notes ? '<div style="margin-top:8px;padding:8px 12px;background:var(--gray-50);border-radius:8px;font-size:13px;color:var(--gray-700)"><span style="color:var(--gray-500);font-size:12px;font-weight:600">' + t('notes') + '</span><br>' + escHtml(a.notes) + '</div>' : '') +
        attachHtml +
      '</div>';
    }).join('');
  } catch(e) {
    document.getElementById('historyList').innerHTML = '<div style="text-align:center;padding:30px;color:var(--gray-500)">' + t('connectionError') + '</div>';
  }
}

document.getElementById('studentNumber').addEventListener('blur', async function() {
  var val = this.value.trim();
  studentLoaded = false;
  updateSubmitBtnState();
  if (!val) return;
  try {
    var res = await fetch('/api/students', { headers: { 'Authorization': 'Bearer ' + token } });
    if (!res.ok) return;
    var students = await res.json();
    var list = Array.isArray(students) ? students : [];
    var found = list.find(function(s) { return s.student_id === val; });
    var info = document.getElementById('studentInfo');
    var text = document.getElementById('studentInfoText');
    if (found) {
      studentLoaded = true;
      var st = found.student_status || 'active';
      var stLabel = getStatusLabel(st);
      if (st === 'active') stLabel = t('studentStatusActive');
      var badgeClass = 'badge-status-' + st;
      var adLine = '';
      if (found.ad_username) {
        var adStatusText = found.ad_status === 'disabled' ? t('adDisabled') : t('adEnabled');
        adLine = '<br><span style="font-size:12px;color:var(--gray-500)">' + escHtml(found.ad_username) + ' (' + adStatusText + ')</span>';
      }
      text.innerHTML = '<span style="color:#0F6E56;font-weight:700">\u2713</span> ' + escHtml(found.full_name) + ' \u2014 ' +
        t('currentStatus') + ': <span class="badge ' + badgeClass + '">' + stLabel + '</span>' + adLine;
      info.style.display = 'block';
      info.style.background = '';
    } else {
      text.innerHTML = '\u2717 ' + t('studentNotFound');
      info.style.display = 'block';
      info.style.background = '#FEF2F2';
      setTimeout(function() { info.style.display = 'none'; info.style.background = ''; }, 3000);
    }
  } catch(e) {}
  updateSubmitBtnState();
});

document.getElementById('notes').addEventListener('input', updateSubmitBtnState);
document.getElementById('studentNumber').addEventListener('input', function() {
  studentLoaded = false;
  updateSubmitBtnState();
});

document.getElementById('sidebar-user-name').textContent = currentUser.full_name || currentUser.username || '';
document.getElementById('sidebar-user-role').textContent = currentUser.role || '';
document.querySelector('.logout-btn').onclick = function() {
  localStorage.removeItem('staffToken');
  localStorage.removeItem('staffUser');
  window.location.replace('/Account/Login');
};

setLang(localStorage.getItem('uiLanguage') || 'ar');
document.title = t('updateStudentStatus');
loadUsers().then(function() { loadHistory(); });
