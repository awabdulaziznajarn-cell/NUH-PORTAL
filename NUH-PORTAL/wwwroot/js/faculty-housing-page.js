// شاشة وحدات سكن أعضاء هيئة التدريس — الوحدات، سجل الإشغال، والاستيراد من الدومين.
(function () {
  'use strict';

  // ⚠️ النصوص تأتي من SharedResource عبر window.FH_T اللي بتملاه الشاشة.
  //    كتابتها هنا مباشرة كانت تعني أن تبديل اللغة لا يغيّر شيئًا في الصفحة،
  //    وأن أي تعديل على صياغة يحتاج تعديل كود بدل تعديل ملف الموارد.
  function T(k) { return (window.FH_T && window.FH_T[k]) || k; }
  function can(p) { return !!(window.FH_CAN && window.FH_CAN[p]); }

  // ⚠️ onlyNeedsConfirm اتشال: الكارت اللي كان بيفعّله اتشال، فما بقاش له
  //    مصدر في الواجهة. المعامل نفسه لسه في الـ API وهيتستخدم مع شاشة «تأكيد
  //    الإشغال» — بس حالة مالهاش من يغيّرها بتضلّل أي حد يقرا الكود بعدها.
  var state = { type: '', status: '', search: '', onlyDeviations: false, tower: '', page: 1 };
  var towersFilled = false;
  var searchTimer = null;

  // ---------- أدوات ----------
  function esc(s) {
    if (s === null || s === undefined) return '';
    return String(s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  // ⚠️ التاريخ بيتعرض ميلادي مختصر بأرقام لاتينية زي باقي شاشات النظام.
  //    الوقت مش بيتعرض: تاريخ بداية السكن معناه اليوم، والساعة بتضيّق العمود
  //    وبتوحي بدقة مش موجودة أصلًا في البيانات المستوردة.
  function fmtDate(v) {
    if (!v) return '-';
    var d = new Date(v);
    if (isNaN(d.getTime())) return '-';
    var p = function (n) { return (n < 10 ? '0' : '') + n; };
    return d.getFullYear() + '/' + p(d.getMonth() + 1) + '/' + p(d.getDate());
  }

  // ⚠️ الوقت يظهر في سجل الإشغال دون عمود الجدول. في القائمة الوقت ضجيج:
  //    التاريخ وحده يكفي لمعرفة منذ متى. أما في السجل فهو دليل على إجراء —
  //    «مين نفّذ ومتى» بالساعة والدقيقة، وهو ما يُسأل عنه عند المراجعة.
  //    وبنظام ٢٤ ساعة لا ص/م: صيغة واحدة لا تختلف بين العربية والإنجليزية.
  function fmtDateTime(v) {
    if (!v) return '-';
    var d = new Date(v);
    if (isNaN(d.getTime())) return '-';
    var p = function (n) { return (n < 10 ? '0' : '') + n; };
    return fmtDate(v) + ' - ' + p(d.getHours()) + ':' + p(d.getMinutes());
  }

  // ⚠️ الهوية والجوال بيتعرضوا مقنّعين في القائمة. دي بيانات شخصية بتتعرض على
  //    شاشة مفتوحة، والقيمة الكاملة موجودة في سجل الوحدة لمن يفتحه — يعني
  //    الوصول ليها لسه ممكن، بس مش معروض لأي حد بيعدّي على الشاشة.
  function mask(v) {
    if (!v) return '-';
    var s = String(v).trim();
    var masked = s.length <= 4 ? s
      : s.slice(0, 2) + '•'.repeat(Math.max(2, s.length - 4)) + s.slice(-2);

    // ⚠️ لازم يتلف في عنصر اتجاهه LTR ومعزول. النقط (•) محايدة الاتجاه، فجوّه
    //    صفحة RTL المتصفح بيعيد ترتيب أطراف الرقم: "05••••••37" بتترسم
    //    "37••••••05" — يعني المستخدم بيقرا رقم مقلوب ويفتكره الرقم الحقيقي.
    //    unicode-bidi:isolate بتمنع الاندماج ده مع النص العربي حواليه.
    return '<span class="fh-num">' + esc(masked) + '</span>';
  }

  // ⚠️ الـ enum بييجي من الـ API إما رقم أو نص، والنص بيبقى PascalCase
  //    ("OutOfService") مش زي القيمة المخزّنة في القاعدة ("out_of_service").
  //    المقارنة الحرفية على "out_of_service" كانت بتفشل دايمًا، فكل الصفوف
  //    كانت بتقع على آخر شرط وتتعلّم «في انتظار المزامنة» — بينما العدّاد فوق
  //    بييجي محسوب من السيرفر وبيقول صفر. التناقض ده كان أول علامة على الباق.
  //    الحل: نوحّد الشكل (حروف صغيرة، من غير شرطة سفلية) قبل أي مقارنة.
  function isEnum(value, name, num) {
    if (value === null || value === undefined) return false;
    if (typeof value === 'number') return value === num;
    return String(value).toLowerCase().replace(/_/g, '') === name;
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

  function isUnassignable(u) {
    return isEnum(u.status, 'outofservice', 2) || isEnum(u.status, 'notexists', 3);
  }

  // ---------- الإحصائيات ----------
  function renderStats(d) {
    var cards = [
      { k: '', num: d.totalUnits, label: T('fh_StTotal'), cls: '' },
      { k: 'occupied', num: d.occupied, label: T('fh_StOccupied'), cls: 'ok' },
      { k: 'vacant', num: d.vacant, label: T('fh_StVacant'), cls: '' },
      // ⚠️ «غير موجودة» و«خارج الخدمة» و«محتاجة تأكيد» اتشالوا من الكروت.
      //    الأولانيين ما بقاش ليهم معنى بعد ما الاستيراد بقى بيقرا من الدومين:
      //    الوحدة اللي مش موجودة مالهاش حساب أصلًا فمابتظهرش خالص. والتالتة
      //    هترجع مع شاشة «تأكيد الإشغال» — قبلها هتفضل صفر لسنة كاملة.
      { k: 'pending_sync', num: d.pendingSync, label: T('fh_StPendingSync'), cls: d.pendingSync ? 'bad' : '' },
      { k: 'deviations', num: d.nameDeviations, label: T('fh_StNameDeviations'), cls: d.nameDeviations ? 'warn' : '' },
      { k: 'ouMismatch', num: d.ouMismatches, label: T('fh_StOuMismatch'), cls: d.ouMismatches ? 'warn' : '' }
    ];

    document.getElementById('fhStats').innerHTML = cards.map(function (c) {
      var on = c.k === 'deviations'
            ? state.onlyDeviations
            : (!!c.k && c.k !== 'ouMismatch' && state.status === c.k);
      return '<div class="fh-stat ' + c.cls + (on ? ' is-on' : '') + '" data-k="' + c.k + '">' +
             '<div class="fh-stat-num">' + (c.num || 0) + '</div>' +
             '<div class="fh-stat-label">' + c.label + '</div></div>';
    }).join('');

    // الكارت بيشتغل كفلتر — الضغط عليه تاني بيلغيه
    Array.prototype.forEach.call(document.querySelectorAll('.fh-stat'), function (el) {
      el.addEventListener('click', function () {
        var k = el.getAttribute('data-k');
        if (k === 'ouMismatch') return;            // عدّاد للعرض — الفلتر عليه غير مضاف بعد

        // ⚠️ الفلاتر متنافية: أي اختيار يلغي اللي قبله. من غير كده بيجتمع
        //    فلترين — «أسماء مخالفة» مع «بانتظار المزامنة» مثلًا — فيبان
        //    الكارتين مضلّلين مع بعض والنتيجة صفر، فيبدو الفلتر معطّل وهو
        //    شغّال: الشرطين صح ومفيش وحدة بتحققهم مع بعض. اجتماعهم مالوش
        //    معنى هنا، فمنعه في مكان واحد أوضح من شرح نتيجة فاضية للمستخدم.
        // ⚠️ وأي فلتر جديد بيرجّع الصفحة للأول، وإلا المستخدم بيفلتر وهو على
        //    صفحة ٣ فيلاقي الجدول فاضي والنتايج في صفحة مش هيوصلها.
        var prevStatus = state.status;
        var prevDeviations = state.onlyDeviations;

        state.page = 1;
        state.status = '';
        state.onlyDeviations = false;

        if (k === 'deviations') state.onlyDeviations = !prevDeviations;
        else state.status = (prevStatus === k) ? '' : k;

        document.getElementById('fhStatus').value = state.status;
        load();
      });
    });
  }

  // ---------- الجدول ----------
  function renderRows(items) {
    var body = document.getElementById('fhBody');
    if (!items || !items.length) {
      body.innerHTML = '<tr><td colspan="8" class="fh-empty">' + esc(T('fh_NoUnits'))
        + ' ' + esc(T('fh_NoUnitsHint')) + '</td></tr>';
      return;
    }

    body.innerHTML = items.map(function (u) {
      var flags = '';
      if (!u.nameMatchesStandard) flags += ' <span class="fh-flag" title="' + esc(T('fh_FlagBadNameTitle')) + '">' + esc(T('fh_FlagBadName')) + '</span>';
      if (u.ouGenderMismatch) flags += ' <span class="fh-flag" title="' + esc(u.ouGenderMismatchNote) + '">' + esc(T('fh_FlagOuMismatch')) + '</span>';
      if (u.occupantImported) flags += ' <span class="fh-flag" title="' + esc(T('fh_FlagImportedTitle')) + '">' + esc(T('fh_FlagImported')) + '</span>';

      var occupant = u.occupantName
        ? '<span>' + esc(u.occupantName) + '</span>'
        : (isUnassignable(u)
            ? '<span class="fh-muted">' + esc(T('fh_NotAssignable')) + '</span>'
            : '<span class="fh-muted">' + esc(T('fh_NoOccupant')) + '</span>');

      return '<tr>' +
        '<td class="fh-unit">' + esc(u.displayName) + '</td>' +
        '<td><span class="fh-acct">' + esc(u.adAccount) + '</span>' + flags + '</td>' +
        '<td class="fh-name">' + occupant + '</td>' +
        '<td>' + mask(u.occupantNationalId) + '</td>' +
        '<td>' + mask(u.occupantMobile) + '</td>' +
        '<td>' + (u.occupantSince ? fmtDate(u.occupantSince) : '-') + '</td>' +
        '<td>' + statusBadge(u) + '</td>' +
        '<td><div class="row-actions">' +
          (can('manage') && !isUnassignable(u) && u.occupancyId
            ? '<a class="action-btn action-edit" href="/FacultyHousing/Edit/' + u.id + '">' + esc(T('fh_BtnEdit')) + '</a>' : '') +
          (can('sync') && !isEnum(u.syncState, 'synced', 1)
            ? '<button class="action-btn action-toggle" data-sync="' + u.id + '">' + esc(T('fh_BtnRetrySync')) + '</button>' : '') +
          '<button class="action-btn action-edit" data-hist="' + u.id + '">' + esc(T('fh_BtnHistory')) + '</button>' +
        '</div></td></tr>';
    }).join('');

    Array.prototype.forEach.call(body.querySelectorAll('[data-hist]'), function (b) {
      b.addEventListener('click', function () { openHistory(b.getAttribute('data-hist')); });
    });
    Array.prototype.forEach.call(body.querySelectorAll('[data-sync]'), function (b) {
      b.addEventListener('click', function () { pushToAd(b.getAttribute('data-sync'), b); });
    });
  }

  // ⚠️ القائمة بتتملا مرة واحدة بس: إعادة بنائها مع كل تحميل بتصفّر اختيار
  //    المستخدم أول ما يفلتر على برج — فيختار برج ٦ وتلاقي القائمة رجعت «كل الأبراج».
  function renderTowers(towers) {
    if (towersFilled || !towers || !towers.length) return;
    var sel = document.getElementById('fhTower');
    if (!sel) return;
    sel.insertAdjacentHTML('beforeend', towers.map(function (t) {
      return '<option value="' + t + '">' + esc(T('fh_Tower')) + ' ' + t + '</option>';
    }).join(''));
    towersFilled = true;
  }

  // ⚠️ البناء من NuhTable.pager - النسخة الوحيدة لصفّ الترقيم في النظام.
  //    كانت مكتوبة هنا بالكامل، وشاشة تغيير الساكن بلا صفّ إطلاقًا.
  function renderPager(d) {
    NuhTable.pager('fhPager', d, {
      show: T('fh_PagerShow'), to: T('fh_PagerTo'), of: T('fh_PagerOf'),
      page: T('fh_Page'), pageOf: T('fh_PageOf'),
      prev: T('fh_Prev'), next: T('fh_Next')
    }, function (p) { state.page = p; load(); });
  }

  function load() {
    var qs = new URLSearchParams();
    if (state.type) qs.set('type', state.type);
    if (state.status) qs.set('status', state.status);
    if (state.search) qs.set('search', state.search);
    if (state.onlyDeviations) qs.set('onlyDeviations', 'true');
    if (state.tower) qs.set('tower', state.tower);
    qs.set('page', state.page);

    fetch('/api/FacultyHousing/units?' + qs.toString(), { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) { renderStats(d); renderTowers(d.towers); renderRows(d.items); renderPager(d); })
      .catch(function (e) {
        document.getElementById('fhBody').innerHTML =
          '<tr><td colspan="8" class="fh-empty">' + esc(T('fh_LoadError')) + esc(e.message) + '</td></tr>';
      });
  }

  // ---------- سجل الوحدة ----------
  function openHistory(id) {
    var ov = document.getElementById('fhHistoryOverlay');
    document.getElementById('fhHistBody').innerHTML = '<div class="fh-empty">' + esc(T('fh_Loading')) + '</div>';
    // ⚠️ العودة لتبويب السكّان مع كل فتحة: من يفتح وحدة أخرى يريد شاغلها أولًا،
    //    لا أن يجد نفسه في تبويب تعديلات وحدة سابقة.
    window.fhHistTab('occ');
    ov.classList.add('open');

    fetch('/api/FacultyHousing/units/' + id, { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) {
        document.getElementById('fhHistTitle').textContent = T('fh_HistoryTitle') + ' - ' + (d.unit.displayName || '');
        document.getElementById('fhHistSub').innerHTML =
          '<span class="fh-acct">' + esc(d.unit.adAccount) + '</span>' +
          (d.distinguishedName ? ' &nbsp;·&nbsp; <span style="font-family:Consolas,monospace;font-size:10.5px;direction:ltr">'
            + esc(d.distinguishedName) + '</span>' : '');

        if (!d.history || !d.history.length) {
          document.getElementById('fhHistBody').innerHTML =
            '<div class="fh-empty">' + esc(T('fh_NoHistory')) + '</div>';
          return;
        }

        // ⚠️ البيانات في صفوف مُعنوَنة لا في فقرة متصلة. الفقرة كانت تخلط
        //    الهوية بالجوال بالكلية في سطر واحد، فالعين تقرأ كتلة نصّ لا بيانات.
        //    كل حقل هنا له عنوان ثابت في مكان ثابت، فالمقارنة بين شاغلين تصير
        //    نظرة واحدة بدل قراءة سطرين كاملين.
        function row(label, value, mono) {
          if (!value) return '';
          return '<div class="oc-row"><span class="oc-k">' + label + '</span>' +
                 '<span class="oc-v' + (mono ? ' fh-num' : '') + '">' + esc(value) + '</span></div>';
        }

        var html = '<div class="tl">' + d.history.map(function (o) {
          var period = fmtDateTime(o.startDate) + ' ← ' +
                       (o.endDate ? fmtDateTime(o.endDate) : T('fh_UntilNow'));

          var body =
            row(T('fh_Period'), period) +
            row(T('fh_ColNationalId'), o.nationalId, true) +
            row(T('fh_ColMobile'), o.mobile, true) +
            row(T('fh_College'), o.college) +
            row(T('fh_Department'), o.department) +
            row(T('fh_TicketNo'), o.ticketNo, true) +
            row(T('fh_EndReason'), o.endReasonNote) +
            row(T('fh_ExecutedBy'), o.createdByName);

          var warn = o.importedFromAd
            ? '<div class="oc-warn">' + esc(T('fh_ImportedWarn')) + '</div>'
            : '';

          return '<div class="tl-item' + (o.isCurrent ? ' now' : '') + '">' +
            '<div class="tl-dot"></div><div class="tl-card">' +
            '<div class="tl-top"><b>' + esc(o.fullNameAr) + '</b>' +
            (o.isCurrent ? '<span class="badge badge-occupied">' + esc(T('fh_CurrentOccupant')) + '</span>'
                         : '<span class="badge badge-vacant">' + esc(T('fh_EndedOccupancy')) + '</span>') + '</div>' +
            '<div class="oc-grid">' + body + '</div>' + warn +
            '</div></div>';
        }).join('') + '</div>';

        document.getElementById('fhHistBody').innerHTML = html;
        renderChanges(d.changes || []);

        // رابط التقرير الكامل - الرقم هو ما يُصفّى به، والاسم للعرض في الشريحة
        var rep = document.getElementById('fhFullReport');
        if (rep) {
          rep.href = '/AuditLog?group=faculty&unit=' + encodeURIComponent(id)
                   + '&unitName=' + encodeURIComponent(d.unit.displayName || '');
        }
      })
      .catch(function (e) {
        document.getElementById('fhHistBody').innerHTML =
          '<div class="fh-empty">' + esc(T('fh_HistoryError')) + esc(e.message) + '</div>';
        renderChanges([]);
      });
  }

  // ==========================================================================
  //  تبويب «التعديلات» — من غيّر وماذا كانت القيمة قبل التغيير.
  //
  //  ⚠️ سجل الإشغال (التبويب الأول) يصف *الحاضر*: من يسكن الوحدة الآن ومن
  //     سكنها قبله بقيمهم الحالية. فلو صُحّح رقم هوية بعد التسجيل، ظهر السجل
  //     صحيحًا كأنه لم يُصحَّح قط - لا القيمة القديمة ولا من صحّحها ولا متى.
  //     هذا التبويب يقرأ سجل العمليات نفسه، وهو ما يُطلب في تقرير المساءلة.
  //
  //  ⚠️ أسماء الإجراءات والحقول من NuhAudit لا مكتوبة هنا: شاشة سجل العمليات
  //     تعرض الأسماء نفسها، فلو كُتبت في الملفين لتُرجمت في أحدهما وبقيت خامة
  //     في الآخر.
  // ==========================================================================
  function renderChanges(list) {
    var body = document.getElementById('fhChgBody');
    var num = document.getElementById('fhChgCount');
    if (num) num.textContent = list.length;
    if (!body) return;

    if (!list.length) {
      body.innerHTML = '<div class="fh-empty">' + esc(T('fh_NoAuditLog')) + '</div>';
      return;
    }

    body.innerHTML = list.map(function (a) {
      var rows = (a.fields || []).map(function (f) {
        return '<tr><td>' + esc(NuhAudit.fieldText(f.fieldName)) + '</td>' +
               '<td class="ch-old">' + esc(f.oldValue == null || f.oldValue === '' ? T('fh_ChgEmpty') : f.oldValue) + '</td>' +
               '<td class="ch-new">' + esc(f.newValue == null || f.newValue === '' ? T('fh_ChgEmpty') : f.newValue) + '</td></tr>';
      }).join('');

      var meta = fmtDateTime(a.actionAt) +
                 (a.actorName ? ' &nbsp;·&nbsp; ' + esc(a.actorName) : '') +
                 (a.ipAddress ? ' &nbsp;·&nbsp; <span style="direction:ltr;display:inline-block">' + esc(a.ipAddress) + '</span>' : '');

      return '<div class="ch-item">' +
        '<div class="ch-top"><span class="ch-act">' + esc(NuhAudit.actionText(a.action)) + '</span>' +
        '<span class="ch-meta">' + meta + '</span></div>' +
        (rows
          ? '<table class="ch-tbl"><tr><th>' + esc(T('fh_ChgField')) + '</th><th>' +
            esc(T('fh_ChgOld')) + '</th><th>' + esc(T('fh_ChgNew')) + '</th></tr>' + rows + '</table>'
          : '') +
      '</div>';
    }).join('');
  }

  // تبديل تبويبَي نافذة السجل
  window.fhHistTab = function (which) {
    var occ = which !== 'chg';
    document.getElementById('fhHistBody').style.display = occ ? '' : 'none';
    document.getElementById('fhChgBody').style.display = occ ? 'none' : '';
    document.getElementById('fhTabOcc').classList.toggle('active', occ);
    document.getElementById('fhTabChg').classList.toggle('active', !occ);
  };


  // ⚠️ الدالة دي اتشالت بالغلط لما شاشة التسليم اتنقلت لملف منفصل، وفضل
  //    الزرار في الجدول بينادي عليها. النتيجة: ضغطة بلا أي رد فعل — خطأ في
  //    الكونسول ومفيش حاجة على الشاشة. الزرار اللي مابيعملش حاجة أسوأ من
  //    الزرار اللي بيقول «فشل»: الأول بيخلّي المستخدم يفتكر إن العملية تمّت.
  //
  //    وعشان كده كل نتيجة هنا بتتعرض صراحةً — نجاح أو فشل بسببه.
  function pushToAd(unitId, btn) {
    var original = btn ? btn.textContent : '';
    if (btn) { btn.disabled = true; btn.textContent = T('fh_Working'); }

    var msg = document.getElementById('fhMsg');
    if (msg) msg.innerHTML = '';

    fetch('/api/FacultyHousing/units/' + unitId + '/push-ad',
          { method: 'POST', credentials: 'same-origin' })
      .then(function (r) {
        return r.json().then(function (j) {
          if (!r.ok) throw new Error(j.message || j.title || ('HTTP ' + r.status));
          return j;
        });
      })
      .then(function (d) {
        if (msg) {
          msg.innerHTML = '<div class="fh-note ' + (d.success ? 'green' : 'red') + '">' +
            esc(d.success ? T('fh_PushOk') : T('fh_PushFail')) +
            (d.error ? '<br>' + esc(d.error) : '') + '</div>';
        }
        load();   // الحالة في الجدول لازم تتحدّث مع النتيجة
      })
      .catch(function (e) {
        if (msg) {
          msg.innerHTML = '<div class="fh-note red">' +
            esc(T('fh_PushFail')) + '<br>' + esc(e.message) + '</div>';
        }
      })
      .finally(function () {
        if (btn) { btn.disabled = false; btn.textContent = original; }
      });
  }

  window.fhCloseHistory = function () {
    document.getElementById('fhHistoryOverlay').classList.remove('open');
  };

  // ---------- فحص الصلاحية ----------
  function renderAccess(d) {
    var box = document.getElementById('fhAccess');
    if (!d.configured) {
      box.innerHTML = '<div class="fh-note red"><b>' + esc(T('fh_OuNotConfigured')) + '</b> - ' + T('fh_OuNotConfiguredHint') + '</div>';
      return;
    }

    var cards = d.ous.map(function (o) {
      var cls = o.canRead && o.allWritable ? 'ok' : 'bad';
      var attrs = Object.keys(o.attributeWritable || {}).map(function (k) {
        return '<span class="fh-attr ' + (o.attributeWritable[k] ? 'y' : 'n') + '">' + esc(k) + '</span>';
      }).join('');

      return '<div class="fh-ou ' + cls + '">' +
        '<div class="fh-ou-title"><span>' + esc(o.label) + '</span>' +
        (o.canRead ? '<span class="badge badge-occupied">' + esc(T('fh_ReadOk')) + '</span>'
                   : '<span class="badge badge-none">' + esc(T('fh_ReadFail')) + '</span>') + '</div>' +
        '<div class="fh-ou-dn">' + esc(o.organizationalUnit) + '</div>' +
        (attrs ? '<div class="fh-attrs">' + attrs + '</div>' : '') +
        (o.probedAccount ? '<div style="font-size:11px;color:var(--gray-500);margin-top:7px">'
          + esc(T('fh_ProbedOn')) + '<span class="fh-acct">' + esc(o.probedAccount) + '</span></div>' : '') +
        (o.error ? '<div style="font-size:11.5px;color:var(--red);margin-top:7px">' + esc(o.error) + '</div>' : '') +
        '</div>';
    }).join('');

    box.innerHTML = '<div class="fh-imp-grid">' + cards + '</div>' +
      (d.allOk
        ? '<div class="fh-note green">' + T('fh_AuthComplete') + '</div>'
        : '<div class="fh-note red">' + T('fh_AuthIncomplete') + '</div>');
  }

  // ---------- معاينة الاستيراد ----------
  function renderPreview(d) {
    var box = document.getElementById('fhPreview');

    if (d.errors && d.errors.length) {
      box.innerHTML = '<div class="fh-note red"><b>' + esc(T('fh_ReadFailed')) + '</b><br>'
        + d.errors.map(esc).join('<br>')
        + '<br><br>' + esc(T('fh_PartialWarn')) + '</div>';
      return;
    }

    var chips = [
      [T('fh_ChipNew'), d.newCount], [T('fh_ChipUpdated'), d.changedCount], [T('fh_ChipUnchanged'), d.unchangedCount],
      [T('fh_ChipExcluded'), d.ignoredCount], [T('fh_ChipDeviations'), d.deviationCount], [T('fh_ChipWithOccupant'), d.withOccupantCount]
    ].map(function (c) {
      return '<span class="badge badge-sync" style="margin-inline-end:6px">' + c[0] + ': ' + (c[1] || 0) + '</span>';
    }).join('');

    var rows = d.rows.map(function (r) {
      var cls = r.action === 4 ? 'style="opacity:.6"' : '';
      return '<tr ' + cls + '>' +
        '<td style="font-weight:700">' + esc(r.displayName) + '</td>' +
        '<td><span class="fh-acct">' + esc(r.adAccount) + '</span></td>' +
        '<td style="font-size:11.5px">' + esc(r.organizationalUnit) + '</td>' +
        '<td class="fh-name">' + (r.description ? esc(r.description) : '<span class="fh-muted">-</span>') + '</td>' +
        '<td style="font-size:12px">' + esc(r.actionLabel) + '</td>' +
        '<td style="font-size:11.5px;color:#93370d">' + (r.deviation ? esc(r.deviation) : (r.ignoreReason ? esc(r.ignoreReason) : '')) + '</td>' +
        '</tr>';
    }).join('');

    box.innerHTML =
      (d.truncated
        ? '<div class="fh-note red"><b>' + esc(T('fh_Truncated')) + '</b> ' + T('fh_TruncatedHint') + '</div>'
        : '') +
      '<div style="margin-bottom:12px">' + chips + '</div>' +
      '<div style="max-height:420px;overflow:auto;border:1px solid var(--gray-200);border-radius:12px">' +
      '<table><thead><tr><th>' + esc(T('fh_ColUnit')) + '</th><th>' + esc(T('fh_ColAccount')) + '</th><th>' + esc(T('fh_PrevOu')) + '</th>' +
      '<th>' + esc(T('fh_PrevOccupant')) + '</th><th>' + esc(T('fh_PrevAction')) + '</th><th>' + esc(T('fh_PrevNote')) + '</th></tr></thead>' +
      '<tbody>' + (rows || '<tr><td colspan="6" class="fh-empty">' + esc(T('fh_NoUnitsInOu')) + '</td></tr>') +
      '</tbody></table></div>' +
      '<div style="display:flex;justify-content:flex-end;gap:10px;margin-top:14px">' +
      '<button class="btn btn-primary" id="fhApplyBtn">' + esc(T('fh_ApplyImport')) + '</button></div>';

    var applyBtn = document.getElementById('fhApplyBtn');
    if (applyBtn) applyBtn.addEventListener('click', applyImport);
  }

  function applyImport() {
    var btn = document.getElementById('fhApplyBtn');
    btn.disabled = true;
    btn.textContent = T('fh_Applying');

    fetch('/api/FacultyHousing/import/apply', { method: 'POST', credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) {
        var ok = !d.errors || !d.errors.length;
        document.getElementById('fhPreview').innerHTML =
          '<div class="fh-note ' + (ok ? 'green' : 'red') + '">' +
          (ok ? '<b>' + esc(T('fh_ImportOk')) + '</b><br>' : '<b>' + esc(T('fh_ImportFail')) + '</b><br>') +
          esc(T('fh_UnitsCreated')) + ': <b>' + (d.unitsCreated || 0) + '</b> · ' +
          esc(T('fh_UnitsUpdated')) + ': <b>' + (d.unitsUpdated || 0) + '</b> · ' +
          esc(T('fh_OccOpened')) + ': <b>' + (d.occupanciesOpened || 0) + '</b>' +
          (d.errors && d.errors.length ? '<br>' + d.errors.map(esc).join('<br>') : '') +
          '</div>';
        load();
      })
      .catch(function (e) {
        document.getElementById('fhPreview').innerHTML =
          '<div class="fh-note red">' + esc(T('fh_ApplyError')) + esc(e.message) + '</div>';
      });
  }

  function busy(btn, fn, url) {
    var original = btn.textContent;
    btn.disabled = true;
    btn.textContent = T('fh_Working');
    fetch(url, { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(fn)
      .catch(function (e) {
        document.getElementById('fhAccess').innerHTML =
          '<div class="fh-note red">' + esc(T('fh_ApplyError')) + esc(e.message) + '</div>';
      })
      .finally(function () { btn.disabled = false; btn.textContent = original; });
  }

  // ---------- الربط ----------
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

    document.getElementById('fhTower').addEventListener('change', function (e) {
      state.tower = e.target.value;
      state.page = 1;
      load();
    });

    document.getElementById('fhStatus').addEventListener('change', function (e) {
      state.status = e.target.value;
      state.onlyDeviations = false;
      state.page = 1;
      load();
    });

    // ⚠️ تأخير قبل البحث: من غيره كل حرف بيبعت طلب، فكتابة رقم هوية = ١٠ طلبات
    //    والنتيجة اللي بتوصل الأخيرة مش بالضرورة بتاعة آخر حرف.
    document.getElementById('fhSearch').addEventListener('input', function (e) {
      clearTimeout(searchTimer);
      var v = e.target.value;
      searchTimer = setTimeout(function () { state.search = v; state.page = 1; load(); }, 350);
    });

    var checkBtn = document.getElementById('fhCheckBtn');
    if (checkBtn) checkBtn.addEventListener('click', function () {
      busy(checkBtn, renderAccess, '/api/FacultyHousing/access-check');
    });

    var prevBtn = document.getElementById('fhPreviewBtn');
    if (prevBtn) prevBtn.addEventListener('click', function () {
      busy(prevBtn, renderPreview, '/api/FacultyHousing/import/preview');
    });

    document.getElementById('fhHistoryOverlay').addEventListener('click', function (e) {
      if (e.target === this) window.fhCloseHistory();
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') window.fhCloseHistory();
    });

    load();
  });
})();
