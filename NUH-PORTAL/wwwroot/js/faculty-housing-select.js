// اختيار الوحدة قبل تغيير ساكنها.
//
// ⚠️ شاشة وسيطة لأن بند القائمة الجانبية لا يحمل رقم وحدة. البديل - رابط
//    مباشر برقم ثابت، أو نموذج يُفتح بلا وحدة محدَّدة - كلاهما بلا معنى.
(function () {
  'use strict';

  var timer = null;
  var towersFilled = false;

  // ⚠️ حالة الفلاتر في كائن واحد: قراءة القيم من عناصر الصفحة عند كل طلب
  //    تجعل كل فلتر يعرف عن الآخر، وأي فلتر يُضاف لاحقًا يحتاج تعديل الطلب.
  var state = { type: '', status: '', tower: '', search: '', page: 1 };

  function T(k) { return (window.FH_T && window.FH_T[k]) || k; }
  function el(id) { return document.getElementById(id); }

  function esc(s) {
    if (s === null || s === undefined) return '';
    return String(s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  // نفس منطق شاشة القائمة: القيمة قد تصل رقمًا أو نصًّا بصيغة PascalCase
  function isEnum(value, name, num) {
    if (value === null || value === undefined) return false;
    if (typeof value === 'number') return value === num;
    return String(value).toLowerCase().replace(/_/g, '') === name;
  }

  function isUnassignable(u) {
    return isEnum(u.status, 'outofservice', 2) || isEnum(u.status, 'notexists', 3);
  }

  function statusBadge(u) {
    if (isEnum(u.status, 'outofservice', 2))
      return '<span class="badge badge-oos">' + esc(T('fh_StOutOfService')) + '</span>';
    if (isEnum(u.status, 'notexists', 3))
      return '<span class="badge badge-none">' + esc(T('fh_StNotExists')) + '</span>';
    if (u.syncState !== undefined && u.syncState !== null && !isEnum(u.syncState, 'synced', 1))
      return '<span class="badge badge-sync">' + esc(T('fh_StPendingSync')) + '</span>';
    if (u.occupancyId) return '<span class="badge badge-occupied">' + esc(T('fh_StOccupied')) + '</span>';
    return '<span class="badge badge-vacant">' + esc(T('fh_StVacant')) + '</span>';
  }

  // ⚠️ القائمة بتتملا مرة واحدة بس: إعادة بنائها مع كل تحميل بتصفّر اختيار
  //    المستخدم أول ما يفلتر على برج — فيختار برج ٦ وتلاقي القائمة رجعت «كل الأبراج».
  function renderTowers(towers) {
    if (towersFilled || !towers || !towers.length) return;
    var sel = el('fhTower');
    if (!sel) return;
    sel.insertAdjacentHTML('beforeend', towers.map(function (t) {
      return '<option value="' + t + '">' + esc(T('fh_Tower')) + ' ' + t + '</option>';
    }).join(''));
    // ⚠️ لا نُعلم select-field.js بالخيارات الجديدة: عنده MutationObserver
    //    يتابع القائمة الأصلية ويحدّث الواجهة وحده.
    towersFilled = true;
  }

  function render(items) {
    var body = el('fhBody');
    if (!items || !items.length) {
      body.innerHTML = '<tr><td colspan="5" class="fh-empty">' + esc(T('fh_NoUnits')) + '</td></tr>';
      return;
    }

    body.innerHTML = items.map(function (u) {
      // ⚠️ الوحدة خارج الخدمة أو غير الموجودة لا تُسكَّن، فلا يُعرض لها زر
      //    اختيار. عرضه ثم رفض الخادم للطلب بعد ملء النموذج إهدار لوقت المستخدم.
      var action = isUnassignable(u)
        ? '<span class="fh-muted">' + esc(T('fh_NotAssignable')) + '</span>'
        : '<a class="action-btn action-on" href="/FacultyHousing/Handover/' + u.id + '">' +
          esc(T('fh_BtnSelect')) + '</a>';

      return '<tr>' +
        '<td class="fh-unit">' + esc(u.displayName) + '</td>' +
        '<td><span class="fh-acct">' + esc(u.adAccount) + '</span></td>' +
        '<td>' + (u.occupantName
          ? esc(u.occupantName)
          : '<span class="fh-muted">' + esc(T('fh_NoOccupant')) + '</span>') + '</td>' +
        '<td>' + statusBadge(u) + '</td>' +
        '<td><div class="row-actions">' + action + '</div></td>' +
        '</tr>';
    }).join('');
  }

  // ⚠️ كانت الشاشة تُحمّل صفحة واحدة بحد أقصى ٢٥ نتيجة بلا صفّ ترقيم، فتعرض
  //    برجَي ١ و٢ فقط من ٢٤٢ وحدة ولا سبيل للوصول إلى الباقي. الآن ترقيم كامل
  //    بصفّ NuhTable.pager نفسه المستخدم في قائمة أعضاء هيئة التدريس.
  function renderPager(d) {
    NuhTable.pager('fhPager', d, {
      show: T('fh_PagerShow'), to: T('fh_PagerTo'), of: T('fh_PagerOf'),
      page: T('fh_Page'), pageOf: T('fh_PageOf'),
      prev: T('fh_Prev'), next: T('fh_Next')
    }, function (p) { state.page = p; load(); });
  }

  function load() {
    var qs = new URLSearchParams({ page: String(state.page), pageSize: '25' });
    if (state.type) qs.set('type', state.type);
    if (state.status) qs.set('status', state.status);
    if (state.tower) qs.set('tower', state.tower);
    if (state.search) qs.set('search', state.search);

    fetch('/api/FacultyHousing/units?' + qs.toString(), { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) { renderTowers(d.towers); render(d.items); renderPager(d); })
      .catch(function (e) {
        el('fhBody').innerHTML =
          '<tr><td colspan="5" class="fh-empty">' + esc(T('fh_LoadError')) + esc(e.message) + '</td></tr>';
      });
  }

  document.addEventListener('DOMContentLoaded', function () {
    Array.prototype.forEach.call(document.querySelectorAll('#fhTypeSeg button'), function (b) {
      b.addEventListener('click', function () {
        Array.prototype.forEach.call(document.querySelectorAll('#fhTypeSeg button'),
          function (x) { x.classList.remove('is-on'); });
        b.classList.add('is-on');
        state.type = b.getAttribute('data-type');
        state.page = 1;
        load();
      });
    });

    el('fhStatus').addEventListener('change', function (e) { state.status = e.target.value; state.page = 1; load(); });
    el('fhTower').addEventListener('change', function (e) { state.tower = e.target.value; state.page = 1; load(); });

    // ⚠️ تأخير قبل البحث: بدونه كل حرف يُرسل طلبًا، وقد تصل نتيجة حرف سابق
    //    بعد نتيجة حرف لاحق فتظهر قائمة لا تطابق ما كُتب.
    el('fhSearch').addEventListener('input', function (e) {
      clearTimeout(timer);
      var v = e.target.value.trim();
      timer = setTimeout(function () { state.search = v; state.page = 1; load(); }, 350);
    });

    load();
  });
})();
