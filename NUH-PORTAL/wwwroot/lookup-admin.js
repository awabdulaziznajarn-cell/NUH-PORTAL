// وحدة إدارة قائمة مرجعية واحدة (شاشة مستقلة لكل نوع). بتتهيّأ بـ LookupAdmin.init(cfg).
// cfg = { apiBase, fields:[], cols:[], isTerm:bool, needsColleges:bool, titleKey }
(function () {
  var CFG = null;
  var state = { items: [], colleges: [] };
  var LANG = 'ar';

  function esc(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;'); }
  function authHeaders() { var tk = localStorage.getItem('staffToken'); return tk ? { 'Authorization': 'Bearer ' + tk } : {}; }
  function apiList() { return CFG.apiBase; }
  function apiItem(id) { return CFG.apiBase + '/' + id; }
  function mGet(id) { return document.getElementById('m_' + id); }

  var COL_LABEL = { code: 'lk_col_code', arName: 'lk_col_arName', enName: 'lk_col_enName', gender: 'lk_col_gender', college: 'lk_col_college', order: 'lk_col_order', active: 'lk_col_active', arText: 'lk_col_arText', enText: 'lk_col_enText' };

  function showToast(msg, isError) {
    var el = document.getElementById('toast'); if (!el) return;
    document.getElementById('toast-msg').textContent = msg;
    el.style.background = isError ? '#b42318' : '#067647';
    el.classList.add('show');
    setTimeout(function () { el.classList.remove('show'); }, 3500);
  }

  function genderText(g) { if (g === 'male') return t('reg_optMale'); if (g === 'female') return t('reg_optFemale'); return '—'; }
  function collegeNameById(id) {
    if (id == null) return '—';
    for (var i = 0; i < state.colleges.length; i++) { if (state.colleges[i].id === id) return LANG === 'en' ? state.colleges[i].enName : state.colleges[i].arName; }
    return '—';
  }

  function cellValue(it, col) {
    switch (col) {
      case 'code': return esc(it.code);
      case 'arName': return esc(it.arName);
      case 'enName': return esc(it.enName);
      case 'arText': return esc(it.arText);
      case 'enText': return esc(it.enText);
      case 'gender': return genderText(it.gender);
      case 'college': return esc(collegeNameById(it.collegeId));
      case 'order': return esc(it.displayOrder);
      case 'active': return '<label class="lk-switch" title="' + (it.isActive ? t('lk_active_yes') : t('lk_active_no')) + '"><input type="checkbox"' + (it.isActive ? ' checked' : '') + ' onchange="LookupAdmin.toggleActive(' + it.id + ')"><span class="lk-slider"></span></label>';
      default: return '';
    }
  }

  function renderTable() {
    var cols = CFG.cols;
    var head = '<tr><th>#</th>' + cols.map(function (c) { return '<th>' + t(COL_LABEL[c]) + '</th>'; }).join('') + '<th>' + t('lk_col_actions') + '</th></tr>';
    document.getElementById('lkHead').innerHTML = head;

    var body = '';
    state.items.forEach(function (it, idx) {
      body += '<tr class="' + (it.isActive ? '' : 'lk-inactive') + '">' +
        '<td>' + (idx + 1) + '</td>' +
        cols.map(function (c) { return '<td>' + cellValue(it, c) + '</td>'; }).join('') +
        '<td><div class="row-actions">' +
          '<button class="action-btn action-edit" onclick="LookupAdmin.openModal(' + it.id + ')">' + t('lk_edit') + '</button>' +
          '<button class="action-btn action-delete" onclick="LookupAdmin.delItem(' + it.id + ')">' + t('lk_delete') + '</button>' +
        '</div></td></tr>';
    });
    document.getElementById('lkBody').innerHTML = body ||
      '<tr><td colspan="' + (cols.length + 2) + '" style="text-align:center;color:var(--gray-500);padding:28px">' + t('lk_noItems') + '</td></tr>';
  }

  function loadList() {
    var titleEl = document.getElementById('lkListTitle');
    if (titleEl) titleEl.textContent = t(CFG.titleKey);
    return fetch(apiList(), { headers: authHeaders() }).then(function (res) {
      if (res.status === 401) { localStorage.removeItem('staffToken'); window.location.replace('/Account/Login'); return; }
      if (!res.ok) { showToast(t('lk_loadError'), true); return; }
      return res.json().then(function (data) { state.items = data; renderTable(); });
    }).catch(function () { showToast(t('lk_loadError'), true); });
  }

  function ensureColleges() {
    if (!CFG.needsColleges || state.colleges.length) return Promise.resolve();
    return fetch('/api/admin/colleges', { headers: authHeaders() })
      .then(function (res) { return res.ok ? res.json() : []; })
      .then(function (data) { state.colleges = data || []; })
      .catch(function () { /* صامت */ });
  }

  function applyModalFields() {
    document.querySelectorAll('#lkOverlay .e-field[data-fld]').forEach(function (el) {
      el.hidden = CFG.fields.indexOf(el.getAttribute('data-fld')) === -1;
    });
  }

  function fillCollegeSelect() {
    var sel = mGet('collegeId'); if (!sel) return;
    var cur = sel.value;
    sel.innerHTML = '<option value="">' + t('lk_selectCollege') + '</option>' +
      state.colleges.map(function (c) { return '<option value="' + c.id + '">' + esc(LANG === 'en' ? c.enName : c.arName) + '</option>'; }).join('');
    sel.value = cur;
  }

  function clearErrors() {
    document.querySelectorAll('#lkOverlay .field-error').forEach(function (e) { e.classList.remove('show'); });
    document.querySelectorAll('#lkOverlay input, #lkOverlay select').forEach(function (e) { e.classList.remove('error'); });
  }

  function openModal(id) {
    clearErrors();
    applyModalFields();
    var run = function () {
      if (CFG.needsColleges) fillCollegeSelect();
      var isEdit = id != null;
      document.getElementById('lkModalTitle').textContent = isEdit ? t('lk_editTitle') : t('lk_addTitle');
      mGet('id').value = isEdit ? id : '';
      var it = isEdit ? state.items.filter(function (x) { return x.id === id; })[0] : null;
      ['code', 'arName', 'enName', 'arText', 'enText', 'displayOrder'].forEach(function (f) {
        var el = mGet(f); if (el) el.value = it ? (it[f] == null ? '' : it[f]) : (f === 'displayOrder' ? 0 : '');
      });
      if (mGet('gender')) mGet('gender').value = it && it.gender ? it.gender : '';
      if (mGet('collegeId')) mGet('collegeId').value = it && it.collegeId != null ? String(it.collegeId) : '';
      mGet('isActive').value = it ? (it.isActive ? 'true' : 'false') : 'true';
      document.getElementById('lkOverlay').classList.add('show');
    };
    if (CFG.needsColleges) ensureColleges().then(run); else run();
  }

  function closeModal() { document.getElementById('lkOverlay').classList.remove('show'); }

  function validate() {
    var ok = true;
    var required = CFG.isTerm ? ['arText', 'enText'] : ['code', 'arName', 'enName'];
    required.forEach(function (f) {
      var el = mGet(f), err = document.getElementById('m-err-' + f);
      if (!el.value.trim()) { el.classList.add('error'); if (err) err.classList.add('show'); ok = false; }
      else { el.classList.remove('error'); if (err) err.classList.remove('show'); }
    });
    return ok;
  }

  function buildPayload() {
    var order = parseInt(mGet('displayOrder').value, 10); if (isNaN(order)) order = 0;
    var active = mGet('isActive').value === 'true';
    if (CFG.isTerm) {
      return { arText: mGet('arText').value.trim(), enText: mGet('enText').value.trim(), displayOrder: order, isActive: active };
    }
    var p = { code: mGet('code').value.trim(), arName: mGet('arName').value.trim(), enName: mGet('enName').value.trim(), displayOrder: order, isActive: active };
    if (CFG.fields.indexOf('gender') !== -1) p.gender = mGet('gender').value || null;
    if (CFG.fields.indexOf('collegeId') !== -1) p.collegeId = mGet('collegeId').value ? parseInt(mGet('collegeId').value, 10) : null;
    return p;
  }

  function saveItem() {
    if (!validate()) return;
    var btn = document.getElementById('lkSaveBtn');
    btn.classList.add('loading');
    var id = mGet('id').value;
    var isEdit = !!id;
    fetch(isEdit ? apiItem(id) : apiList(), {
      method: isEdit ? 'PUT' : 'POST',
      headers: Object.assign({ 'Content-Type': 'application/json' }, authHeaders()),
      body: JSON.stringify(buildPayload())
    }).then(function (res) {
      if (res.status === 401) { localStorage.removeItem('staffToken'); window.location.replace('/Account/Login'); return; }
      if (res.ok) { closeModal(); loadList(); showToast(t('lk_saved')); }
      else { return res.json().catch(function () { return {}; }).then(function (err) { showToast(err.message || t('lk_saveError'), true); }); }
    }).catch(function () { showToast(t('lk_loadError'), true); })
      .then(function () { btn.classList.remove('loading'); });
  }

  // بناء payload كامل من عنصر مخزّن (للتوجيل — بنبعت نفس بيانات العنصر وبنقلب isActive بس)
  function buildPayloadFrom(it) {
    if (CFG.isTerm) {
      return { arText: it.arText, enText: it.enText, displayOrder: it.displayOrder, isActive: it.isActive };
    }
    var p = { code: it.code, arName: it.arName, enName: it.enName, displayOrder: it.displayOrder, isActive: it.isActive };
    if (CFG.fields.indexOf('gender') !== -1) p.gender = it.gender || null;
    if (CFG.fields.indexOf('collegeId') !== -1) p.collegeId = (it.collegeId != null) ? it.collegeId : null;
    return p;
  }

  // تفعيل/إلغاء تفعيل بضغطة على التوجيل + إشعار
  function toggleActive(id) {
    var it = state.items.filter(function (x) { return x.id === id; })[0];
    if (!it) return;
    var newVal = !it.isActive;
    var payload = buildPayloadFrom(it);
    payload.isActive = newVal;
    fetch(apiItem(id), {
      method: 'PUT',
      headers: Object.assign({ 'Content-Type': 'application/json' }, authHeaders()),
      body: JSON.stringify(payload)
    }).then(function (res) {
      if (res.status === 401) { localStorage.removeItem('staffToken'); window.location.replace('/Account/Login'); return; }
      if (res.ok) {
        it.isActive = newVal;
        renderTable();
        showToast(newVal ? t('lk_activated') : t('lk_deactivated'));
      } else {
        renderTable(); // رجّع التوجيل لحالته
        return res.json().catch(function () { return {}; }).then(function (err) { showToast(err.message || t('lk_saveError'), true); });
      }
    }).catch(function () { renderTable(); showToast(t('lk_loadError'), true); });
  }

  function delItem(id) {
    if (!confirm(t('lk_confirmDelete'))) return;
    fetch(apiItem(id), { method: 'DELETE', headers: authHeaders() }).then(function (res) {
      if (res.status === 401) { localStorage.removeItem('staffToken'); window.location.replace('/Account/Login'); return; }
      if (res.ok) { loadList(); showToast(t('lk_deleted')); }
      else { return res.json().catch(function () { return {}; }).then(function (err) { showToast(err.message || t('lk_saveError'), true); }); }
    }).catch(function () { showToast(t('lk_loadError'), true); });
  }

  function init(cfg) {
    CFG = cfg;
    LANG = ((document.getElementById('html-root') || document.documentElement).getAttribute('lang') === 'en') ? 'en' : 'ar';
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeModal(); });
    window.onLanguageChange = function () { LANG = (document.documentElement.getAttribute('lang') === 'en') ? 'en' : 'ar'; renderTable(); };
    ensureColleges().then(loadList);
  }

  window.LookupAdmin = { init: init, openModal: openModal, closeModal: closeModal, saveItem: saveItem, delItem: delItem, toggleActive: toggleActive };
})();
