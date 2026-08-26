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
  // ==========================================================================
  //  حالة الشاشة في الرابط - NuhUrl في js/url-state.js.
  //
  //  ⚠️ الشاشة دي فيها ستّ فلاتر وصفّ الإجراءات فيها **رابط حقيقي** يخرج
  //     منها (/FacultyHousing/Edit/{id}). يعني الدورة الطبيعية هي: فلتر →
  //     دوّر → افتح وحدة → عدّل → ارجع. والرجوع كان بيمسح الستّة ويرجّعك
  //     لأول الشاشة، فتعيد الفلترة من الأول مع كل وحدة.
  //  ⚠️ القيم المسموحة مكتوبة صراحةً: ?status=<script> بيتحط في القائمة
  //     المنسدلة لو اتقبل زي ما هو، وأي قيمة غلط بتخلّي الجدول يطلع فاضي
  //     بلا سبب ظاهر.
  // ==========================================================================
  var TYPES = ['', 'tower', 'villa'];
  var STATUSES = ['', 'occupied', 'vacant', 'out_of_service', 'not_exists', 'pending_sync'];

  var state = {
    type: NuhUrl.one('type', TYPES, ''),
    status: NuhUrl.one('status', STATUSES, ''),
    search: NuhUrl.get('q', ''),
    onlyDeviations: NuhUrl.bool('dev', false),
    onlyOuMismatch: NuhUrl.bool('ou', false),
    onlyDisabled: NuhUrl.bool('off', false),
    tower: NuhUrl.get('tower', ''),
    page: NuhUrl.int('page', 1, 1)
  };
  var STATE_DEFAULTS = { type: '', status: '', q: '', dev: '', ou: '', off: '', tower: '', page: 1, sort: '', asc: '' };

  function syncUrl() {
    NuhUrl.sync({
      type: state.type, status: state.status, q: state.search,
      dev: state.onlyDeviations ? '1' : '', ou: state.onlyOuMismatch ? '1' : '',
      off: state.onlyDisabled ? '1' : '',
      tower: state.tower, page: state.page,
      sort: fhSort.by(), asc: fhSort.by() ? (fhSort.asc() ? '1' : '0') : ''
    }, STATE_DEFAULTS);
  }

  var towersFilled = false;
  var searchTimer = null;

  // ---------- أدوات ----------
  // تهريب HTML — التعريف الوحيد في /js/esc.js
  function esc(s) { return escHtml(s); }

  // ⚠️ التاريخ بيتعرض ميلادي مختصر بأرقام لاتينية زي باقي شاشات النظام.
  //    الوقت مش بيتعرض: تاريخ بداية السكن معناه اليوم، والساعة بتضيّق العمود
  //    وبتوحي بدقة مش موجودة أصلًا في البيانات المستوردة.
  // شكل التاريخ من NuhFmt — التعريف الوحيد في /js/date-format.js
  // ⚠️ كانت بترتيب مقلوب (2026/08/18) عكس باقي النظام (18/08/2026).
  function fmtDate(v) { return NuhFmt.date(v); }

  // ⚠️ الوقت يظهر في سجل الإشغال دون عمود الجدول. في القائمة الوقت ضجيج:
  //    التاريخ وحده يكفي لمعرفة منذ متى. أما في السجل فهو دليل على إجراء —
  //    «مين نفّذ ومتى» بالساعة والدقيقة، وهو ما يُسأل عنه عند المراجعة.
  //    وبنظام ٢٤ ساعة لا ص/م: صيغة واحدة لا تختلف بين العربية والإنجليزية.
  function fmtDateTime(v) { return NuhFmt.dateTime(v); }

  // ⚠️ الهوية والجوال بيتعرضوا مقنّعين في القائمة. دي بيانات شخصية بتتعرض على
  //    شاشة مفتوحة، والقيمة الكاملة موجودة في سجل الوحدة لمن يفتحه — يعني
  //    الوصول ليها لسه ممكن، بس مش معروض لأي حد بيعدّي على الشاشة.
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
      { k: 'ouMismatch', num: d.ouMismatches, label: T('fh_StOuMismatch'), cls: d.ouMismatches ? 'warn' : '' },
      // ⚠️ الحساب المُعطَّل حالة قائمة في الدليل قد تكون مقصودة (إيقاف من
      //    الأمن السيبراني مثلًا)، فنبرته رمادية لا حمراء - نفس منطق شارة
      //    الصفّ. الأحمر للي محتاج تدخّل، ولو حطّيناه هنا اتعوّد المستخدم
      //    يتجاهل الأحمر.
      { k: 'adDisabled', num: d.adDisabled, label: T('fh_StAdDisabled'), cls: '' }
    ];

    document.getElementById('fhStats').innerHTML = cards.map(function (c) {
      // ⚠️ الكارتين دول فلترهم مستقلّ عن حالة الوحدة، فتمييزهم بيتقرا من
      //    الحالة بتاعتهم لا من state.status.
      var on = c.k === 'deviations' ? state.onlyDeviations
             : c.k === 'ouMismatch' ? state.onlyOuMismatch
             : c.k === 'adDisabled' ? state.onlyDisabled
             : (!!c.k && state.status === c.k);
      return '<div class="fh-stat ' + c.cls + (on ? ' is-on' : '') + '" data-k="' + c.k + '">' +
             '<div class="fh-stat-num">' + (c.num || 0) + '</div>' +
             '<div class="fh-stat-label">' + c.label + '</div></div>';
    }).join('');

    // الكارت بيشتغل كفلتر — الضغط عليه تاني بيلغيه
    Array.prototype.forEach.call(document.querySelectorAll('.fh-stat'), function (el) {
      el.addEventListener('click', function () {
        var k = el.getAttribute('data-k');

        // ⚠️ الفلاتر متنافية: أي اختيار يلغي اللي قبله. من غير كده بيجتمع
        //    فلترين — «أسماء مخالفة» مع «بانتظار المزامنة» مثلًا — فيبان
        //    الكارتين مضلّلين مع بعض والنتيجة صفر، فيبدو الفلتر معطّل وهو
        //    شغّال: الشرطين صح ومفيش وحدة بتحققهم مع بعض. اجتماعهم مالوش
        //    معنى هنا، فمنعه في مكان واحد أوضح من شرح نتيجة فاضية للمستخدم.
        // ⚠️ وأي فلتر جديد بيرجّع الصفحة للأول، وإلا المستخدم بيفلتر وهو على
        //    صفحة ٣ فيلاقي الجدول فاضي والنتايج في صفحة مش هيوصلها.
        var prevStatus = state.status;
        var prevDeviations = state.onlyDeviations;
        var prevOu = state.onlyOuMismatch;
        var prevOff = state.onlyDisabled;

        state.page = 1;
        state.status = '';
        state.onlyDeviations = false;
        state.onlyOuMismatch = false;
        state.onlyDisabled = false;

        // ⚠️ كارت «وحدة تنظيمية غير مطابقة» كان بيرجع من غير ما يعمل حاجة:
        //    شكله زي باقي الكروت (مؤشّر يد وحركة عند المرور) فالمستخدم بيدوس
        //    ومحصلش. بقى فلتر زي إخواته - والوحدات اللي بيعدّها هي اللي
        //    حساباتها محتاجة تتنقل بين قسمَي الدليل.
        if (k === 'deviations') state.onlyDeviations = !prevDeviations;
        else if (k === 'ouMismatch') state.onlyOuMismatch = !prevOu;
        else if (k === 'adDisabled') state.onlyDisabled = !prevOff;
        else state.status = (prevStatus === k) ? '' : k;

        document.getElementById('fhStatus').value = state.status;
        load();
      });
    });
  }

  // ---------- الجدول ----------
  // ⚠️ بتاخد الاستجابة كاملة لا الصفوف وحدها: عمود التسلسل محتاج رقم الصفحة
  //    ومقاسها. من غيرهم كان هيبدأ من ١ في كل صفحة، فصفّان مختلفان في
  //    صفحتين يحملوا نفس الرقم - وde ترقيم بيكدب مش ترقيم.
  function renderRows(d) {
    var items = (d && d.items) || [];
    var body = document.getElementById('fhBody');
    if (!items.length) {
      body.innerHTML = '<tr><td colspan="8" class="fh-empty">' + esc(T('fh_NoUnits'))
        + ' ' + esc(T('fh_NoUnitsHint')) + '</td></tr>';
      return;
    }

    // نفس حساب قائمة الطلاب بالحرف
    var seq = ((d.page || 1) - 1) * (d.pageSize || items.length);

    body.innerHTML = items.map(function (u) {
      seq++;
      var flags = '';
      if (!u.nameMatchesStandard) flags += ' <span class="fh-flag" title="' + esc(T('fh_FlagBadNameTitle')) + '">' + esc(T('fh_FlagBadName')) + '</span>';
      if (u.ouGenderMismatch) flags += ' <span class="fh-flag" title="' + esc(u.ouGenderMismatchNote) + '">' + esc(T('fh_FlagOuMismatch')) + '</span>';
      if (u.occupantImported) flags += ' <span class="fh-flag" title="' + esc(T('fh_FlagImportedTitle')) + '">' + esc(T('fh_FlagImported')) + '</span>';
      // ⚠️ حساب مُعطَّل في الدليل كان بيبان في القائمة زيّه زيّ الشغّال
      //    بالظبط. النظام بيعرف الحالة وبيخزّنها من كل مزامنة، وكانت
      //    بتتعرض في المقارنة بالدليل بس - يعني تشوفها لو فتحت المقارنة،
      //    مش وإنت بتتفرّج على الوحدة نفسها.
      // ⚠️ === false لا !u.adAccountEnabled: القيمة الغايبة (undefined) معناها
      //    ماقريناهاش، لا «مُعطَّل» - وشارة حمرا على وحدة سليمة أسوأ من
      //    غياب الشارة.
      if (u.adAccountEnabled === false) flags += ' <span class="fh-flag fh-flag-off" title="' + esc(T('fh_FlagAdDisabledTitle')) + '">' + esc(T('fh_FlagAdDisabled')) + '</span>';

      // ⚠️ الخليّتان من NuhTable.two لا مكتوبتين هنا: هي التعريف الوحيد
      //    للخليّة ذات السطرين في النظام (js/table-state.js)، وقائمة الطلاب
      //    بتنادي نفس الدالة بنفس الخيارات. قبل كده كانت كل شاشة بتكتبها
      //    بإيدها، فافترقن فعلًا: الطلاب .cell-p والوحدات .cell-p2 ومعاه
      //    .fh-name - نفس الخليّة بوزنين في شاشتين بيتقارنوا كل يوم.
      // ⚠️ والاسم الإنجليزي تحت العربي لا بدلًا منه في اللغة الإنجليزية:
      //    مصدره خانة displayName في الدليل، والموظف هنا بيطابق بين المسجَّل
      //    عندنا والمكتوب في الدليل - فإخفاء أحدهما بحسب لغة الواجهة بيخفي
      //    نص المقارنة. واسم الشخص بيانات لا ترجمة.
      var occupant = u.occupantName
        ? NuhTable.two(u.occupantName, u.occupantNameEn, { en: true })
        : '<div class="cell-q fh-muted">' +
            esc(isUnassignable(u) ? T('fh_NotAssignable') : T('fh_NoOccupant')) + '</div>';

      // ⚠️ الهوية فوق والجوال تحته في عمود واحد - نفس بناء عمود «الرقم الجامعي
      //    والجوال» في قائمة الطلاب حرفًا بحرف.
      // ⚠️ والقيم كاملة لا مقنَّعة: كانت بتتعرض بنقط (10••••••10) وحدها في
      //    النظام - قائمة الطلاب بتعرض الرقم والجوال كاملين لنفس الموظف
      //    وبنفس الصلاحية. والتقنيع كان بيخلّي السطرين متفاوتين في الطول
      //    فيبانوا غير مصطفّين، ومابيحميش حاجة: نفس الشاشة فيها زرّ «السجل»
      //    بيعرض القيمة كاملة.
      var idMobile = NuhTable.two(u.occupantNationalId, u.occupantMobile, { num: true });

      return '<tr>' +
        '<td class="num">' + seq + '</td>' +
        '<td class="fh-unit"><div class="cell-p cell-nowrap">' + esc(u.displayName) + '</div></td>' +
        '<td>' + occupant + '</td>' +
        '<td>' + idMobile + '</td>' +
        '<td><div class="cell-p2"><span class="fh-acct">' + esc(u.adAccount) + '</span></div>' +
          (flags ? '<div class="cell-q">' + flags + '</div>' : '') + '</td>' +
        '<td><div class="cell-p2 cell-num cell-nowrap">' +
          (u.occupantSince ? fmtDate(u.occupantSince) : '-') + '</div></td>' +
        '<td>' + statusBadge(u) + '</td>' +
        '<td class="num"><div class="row-actions">' +
          (can('manage') && !isUnassignable(u) && u.occupancyId
            ? '<a class="action-btn action-edit" href="/FacultyHousing/Edit/' + u.id + '">' + esc(T('fh_BtnEdit')) + '</a>' : '') +
          // ⚠️ الزرّ ده محلّ شاشة «اختيار الوحدة» اللي اتشالت: كانت جدولًا
          //    تانيًا لنفس الوحدات مهمّته الوحيدة إنك تختار وحدة. الاختيار
          //    هنا والوحدة قدّامك بصفّها كامل - ومافيش جدولين يتصانوا.
          //
          // ⚠️ وشرطه **مش** مربوط بوجود ساكن، بخلاف «تعديل»: الوحدة الشاغرة
          //    محتاجة الزرّ ده أكتر من المشغولة - هي اللي هتتسكّن. الشرط
          //    الوحيد إن الوحدة قابلة للتسكين أصلًا (مش خارج الخدمة ولا غير
          //    موجودة). لو ربطناه بـ occupancyId زي «تعديل» كانت الوحدات
          //    الشاغرة هتفضل بلا أي مدخل بعد ما الشاشة القديمة اتشالت.
          //
          // ⚠️ واللافتة بتتغيّر مع الحالة: «تغيير الساكن» لمّا يكون فيه ساكن،
          //    و«تسكين» لمّا تكون شاغرة. نفس الصفحة ونفس الرابط، بس الشاشة
          //    نفسها بتفتح على «خدمة جديدة» في الحالة التانية - فلافتة واحدة
          //    كانت هتوعد بحاجة والشاشة تعمل غيرها.
          (can('manage') && !isUnassignable(u)
            ? '<a class="action-btn action-edit" href="/FacultyHousing/Handover/' + u.id + '">' +
              esc(T(u.occupancyId ? 'fh_BtnHandover' : 'fh_BtnAssign')) + '</a>' : '') +
          // ⚠️ الزرّ ظاهر دائمًا للمخوَّل، مش مربوط بشارة خطأ. كان مربوط
          //    بحالة المزامنة وشارة «وحدة تنظيمية غير مطابقة»، والاتنين
          //    بيتحسبوا من المسار المخزَّن **عندنا** لا من الدليل. فلما
          //    اتنقل حساب في الدليل من برّه النظام، فضلت قاعدة بياناتنا
          //    فاكراه في مكانه القديم، فقال «كل حاجة متزامنة» وخبّى الزرّ -
          //    بالظبط في اللحظة اللي كان محتاج فيها. زرّ بيختفي لما البيانات
          //    اللي بيتحسب منها تبقى هي نفسها الغلط مش زرّ.
          // ⚠️ والتشغيل بلا ضرر: العملية قراءة من الدليل ثم كتابة نفس القيم،
          //    فتكرارها مالوش أثر. وبتتقيّد بـ can('sync') زي ما كانت.
          // ⚠️ وبيختفي لو مافيش حساب دومين أصلًا - مافيش حاجة تتزامن معاها.
          (can('sync') && u.adAccount
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

    // ⚠️ البرج المختار بيتظبط هنا لا وقت التهيئة: القائمة بتتملّى من رد
    //    الخادم، فأي قيمة تتحط قبل كده بتتلغي مع أول رسم. والمقارنة بتضمن
    //    إن برج اتشال من النظام مايسيبش القائمة على قيمة مش موجودة.
    if (state.tower) {
      if (towers.indexOf(state.tower) > -1 || towers.indexOf(Number(state.tower)) > -1) {
        sel.value = state.tower;
        if (window.NuhSelect && NuhSelect.enhance) NuhSelect.enhance(sel.parentNode || document);
      } else {
        state.tower = '';
      }
    }
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

  // ترتيب الأعمدة من نسخة واحدة في النظام — NuhTable.sort. الأعمدة معرّفة
  // بـ data-sort على الـ <th>، والخادم هو اللي بيرتّب (الجدول مقسّم صفحات).
  var fhSort = NuhTable.sort('fhTable', function () { state.page = 1; load(); },
    { by: NuhUrl.get('sort', ''), asc: NuhUrl.get('asc', '1') === '1' });

  function load() {
    syncUrl();
    var qs = new URLSearchParams();
    if (state.type) qs.set('type', state.type);
    if (state.status) qs.set('status', state.status);
    if (state.search) qs.set('search', state.search);
    if (state.onlyDeviations) qs.set('onlyDeviations', 'true');
    if (state.onlyOuMismatch) qs.set('onlyOuMismatch', 'true');
    if (state.onlyDisabled) qs.set('onlyDisabled', 'true');
    if (state.tower) qs.set('tower', state.tower);
    qs.set('page', state.page);

    fetch('/api/FacultyHousing/units?' + qs.toString() + fhSort.qs(), { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) { renderStats(d); renderTowers(d.towers); renderRows(d); renderPager(d); })
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
        // ⚠️ اسم الدخول (UPN) هنا لا في الجدول: لاحقته «@nuh.edu.sa» واحدة
        //    في الـ ٢٤٢ وحدة كلها، فتكرارها في كل صفّ حشو لا معلومة. وهنا
        //    يُسأل عنه فعلًا - وحدة بعينها ومعها مسارها في الدليل.
        // ⚠️ والشارة تظهر عند اختلاف مقدّمته عن اسم الحساب فقط: النظام يربط
        //    بـ sAMAccountName، والاختلاف يفسّر أعطال دخول لا يشرحها شيء آخر.
        var upn = d.unit.adUserPrincipalName;
        document.getElementById('fhHistSub').innerHTML =
          '<span class="fh-acct">' + esc(d.unit.adAccount) + '</span>' +
          (upn ? ' &nbsp;·&nbsp; <span class="fh-upn">' + esc(upn) + '</span>' +
                 (d.unit.upnMatchesAccount === false
                   ? ' <span class="fh-flag" title="' + esc(T('fh_FlagUpnMismatchTitle')) + '">'
                     + esc(T('fh_FlagUpnMismatch')) + '</span>' : '')
               : '') +
          (d.distinguishedName ? ' &nbsp;·&nbsp; <span class="fh-dn-inline">'
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
            // الاسم الإنجليزي تحت العربي مباشرةً - نفس بناء الخلية في الجدول
            (o.fullNameEn ? '<div class="tl-en cell-en">' + esc(o.fullNameEn) + '</div>' : '') +
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
  // ⚠️ الرسم نفسه اتنقل لـ js/faculty-audit-log.js: شاشة «تغيير بيانات
  //    ساكن» بقت بتعرض نفس السجل، ونسختين من دالة الرسم كانوا هيفترقوا عند
  //    أول تعديل على شكل البند. الدالة دي بقت بتملا مكانها في الشاشة دي بس.
  function renderChanges(list) {
    list = list || [];
    var body = document.getElementById('fhChgBody');
    var num = document.getElementById('fhChgCount');
    if (num) num.textContent = list.length;
    if (body) body.innerHTML = NuhFacultyLog.items(list);
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
  // ==========================================================================
  //  نتيجة فحص الصلاحيات: خلاصة في سطر، ثم جدول صفٌّ لكل وحدة تنظيمية.
  //
  //  ⚠️ كانت النتيجة ثلاث بطاقات متفاوتة الطول تليها فقرتان شارحتان، فيقرأ
  //     المستخدم التفاصيل قبل الحكم، ويخرج بلا إجابة عن سؤاله الوحيد: هل
  //     التفويض كافٍ أم لا. الحكم الآن أوّل ما يُقرأ، والتفاصيل خلف زرّ.
  //
  //  ⚠️ ونصّ المعالجة (dsacls) لا يظهر إلا عند وجود نقص فعلي. عرضه دائمًا
  //     يجعل الشاشة السليمة تبدو كأنها تطلب تدخّلًا.
  // ==========================================================================
  function accMark(v) {
    return v ? '<span class="fh-acc-y">&#10003;</span>' : '<span class="fh-acc-n">&#10007;</span>';
  }

  function renderAccess(d) {
    var box = document.getElementById('fhAccess');
    document.getElementById('fhPreview').innerHTML = '';
    if (!d.configured) {
      box.innerHTML = '<div class="fh-note red"><b>' + esc(T('fh_OuNotConfigured')) + '</b> - ' + T('fh_OuNotConfiguredHint') + '</div>';
      return;
    }

    var ous = d.ous || [];

    var rows = ous.map(function (o) {
      var attrs = Object.keys(o.attributeWritable || {});
      var okAttrs = attrs.filter(function (k) { return o.attributeWritable[k]; }).length;
      // ⚠️ النقل يخصّ الأقسام المقسّمة بالجنس وحدها؛ «الفلل» مختلطة فلا نقل
      //    منها ولا إليها، وخانتها تُترك شرطة لا «مرفوض».
      var moveIn  = o.isGendered ? accMark(o.canCreateUser === true) : '<span class="fh-acc-na">&mdash;</span>';
      var moveRdn = o.isGendered ? accMark(o.canWriteRdn === true)   : '<span class="fh-acc-na">&mdash;</span>';
      var bad = !o.canRead || !o.allWritable ||
                (o.isGendered && (o.canCreateUser !== true || o.canWriteRdn !== true));

      return '<tr' + (bad ? ' class="bad"' : '') + '>' +
        '<td>' + esc(T(o.labelKey)) +
          (o.ouName ? '<span class="fh-ouname">' + esc(o.ouName) + '</span>' : '') +
          (o.isGendered ? '' : ' <span class="fh-muted" style="font-size:11px">(' + esc(T('fh_AccNotSplit')) + ')</span>') +
        '</td>' +
        '<td>' + accMark(o.canRead) + '</td>' +
        '<td class="fh-acc-cnt' + (okAttrs === attrs.length ? '' : ' fh-acc-n') + '">' +
          okAttrs + ' / ' + attrs.length + '</td>' +
        '<td>' + moveIn + '</td>' +
        '<td>' + moveRdn + '</td>' +
        '</tr>';
    }).join('');

    // ⚠️ حكم واحد لا حكمان: الشاشة كانت تعرض «التفويض مكتمل» بالأخضر ثم نقص
    //    النقل بالكهرماني تحته، فيُقرأ الأول ويُهمل الثاني. الترتيب هنا:
    //    نقص القراءة/الكتابة يمنع كل شيء، ثم نقص النقل، ثم السلامة.
    var moveBad = d.hasGenderedOus && (!d.moveCreateOk || !d.moveRdnOk);
    var vTitle, vSub, vCls;
    if (!d.allOk)      { vCls = 'bad'; vTitle = T('fh_AuthIncomplete');  vSub = T('fh_AuthIncompleteSub'); }
    else if (moveBad)  { vCls = 'bad'; vTitle = T('fh_MoveVerdictBad');  vSub = T('fh_MoveVerdictBadSub'); }
    else               { vCls = 'ok';  vTitle = T('fh_AuthComplete');    vSub = T('fh_AuthCompleteSub'); }

    var fix = '';
    if (d.hasGenderedOus) {
      if (!d.moveCreateOk)   fix = '<div class="fh-note red" style="margin-top:12px">' + T('fh_MoveAuthNo') + '</div>';
      else if (!d.moveRdnOk) fix = '<div class="fh-note red" style="margin-top:12px">' + T('fh_MoveAuthNoRdn') + '</div>';
      // ⚠️ التحفّظ على الحذف يبقى معروضًا حتى في الحالة السليمة: الفحص لا
      //    يشمله، وإخفاؤه يجعل «مكتمل» وعدًا لا يملك النظام إثباته.
      else fix = '<div class="fh-note" style="margin-top:12px">' + T('fh_MoveAuthPartial') + '</div>';
    }

    var details = ous.map(function (o) {
      var chips = function (map) {
        return Object.keys(map || {}).map(function (k) {
          return '<span class="fh-attr ' + (map[k] ? 'y' : 'n') + '">' + esc(k) + '</span>';
        }).join(' ');
      };
      return '<div class="fh-det-box">' +
        '<b>' + esc(T(o.labelKey)) + '</b> &nbsp;<span class="fh-ou-dn" style="display:inline;margin:0">' +
          esc(o.organizationalUnit) + '</span><br>' +
        esc(T('fh_AccAttrs')) + ': ' + chips(o.attributeWritable) +
        (o.isGendered ? ' &nbsp;·&nbsp; ' + esc(T('fh_AccRdn')) + ': ' + chips(o.rdnAttributeWritable) : '') +
        (o.probedAccount ? ' &nbsp;·&nbsp; ' + esc(T('fh_ProbedOn')) +
          '<span class="fh-acct">' + esc(o.probedAccount) + '</span>' : '') +
        (o.error ? '<br><span style="color:var(--red)">' + esc(o.error) + '</span>' : '') +
        '</div>';
    }).join('');

    box.innerHTML =
      '<div class="fh-verdict ' + vCls + '"><span class="fh-vic">' +
        (vCls === 'ok' ? '&#10003;' : '&#10007;') + '</span>' +
        esc(vTitle) + ' <span class="fh-vsub">&mdash; ' + esc(vSub) + '</span></div>' +
      '<table class="fh-acc"><thead><tr>' +
        '<th>' + esc(T('fh_PrevOu')) + '</th>' +
        '<th>' + esc(T('fh_AccRead')) + '</th>' +
        '<th>' + esc(T('fh_AccWrite')) + '</th>' +
        '<th>' + esc(T('fh_AccMoveIn')) + '</th>' +
        '<th>' + esc(T('fh_AccRdn')) + '</th>' +
      '</tr></thead><tbody>' + rows + '</tbody></table>' +
      fix +
      '<button type="button" class="fh-det-btn" id="fhAccDet" aria-expanded="false">' +
        esc(T('fh_AccDetails')) + ' &#9662;</button>' +
      '<div id="fhAccDetBox" hidden>' + details + '</div>';

    var db = document.getElementById('fhAccDet');
    db.addEventListener('click', function () {
      var bx = document.getElementById('fhAccDetBox');
      var open = bx.hasAttribute('hidden');
      if (open) bx.removeAttribute('hidden'); else bx.setAttribute('hidden', '');
      db.setAttribute('aria-expanded', open ? 'true' : 'false');
      db.innerHTML = esc(T('fh_AccDetails')) + (open ? ' &#9652;' : ' &#9662;');
    });
  }

  // ---------- معاينة الاستيراد ----------
  function renderPreview(d) {
    var box = document.getElementById('fhPreview');
    document.getElementById('fhAccess').innerHTML = '';

    if (d.errors && d.errors.length) {
      box.innerHTML = '<div class="fh-note red"><b>' + esc(T('fh_ReadFailed')) + '</b><br>'
        + d.errors.map(esc).join('<br>')
        + '<br><br>' + esc(T('fh_PartialWarn')) + '</div>';
      return;
    }

    // ============================================================
    //  الشرائح بقت فلاتر لا أرقام.
    //
    //  ⚠️ «ستُحدَّث: 1» جنب جدول فيه ٢٤٢ صف بتقول إن فيه وحدة واحدة هتتغيّر
    //     وماتقولش أنهي واحدة. اللي بيراجع المعاينة قبل ما يوافق كان لازم
    //     يمرّ على الصفوف كلها بعينه يدوّر عليها - وde مش مراجعة، ده بحث.
    //     دلوقتي الرقم نفسه زرّ: تدوس عليه فالجدول يفضّى إلا منها.
    //
    //  ⚠️ الفلترة في المتصفح لا بنداء تاني: الصفوف كلها وصلت مع المعاينة
    //     أصلًا، ونداء تاني كان هيقرا الدومين من جديد فيمكن يرجّع نتيجة
    //     مختلفة عن اللي المستخدم بيبصّ عليها.
    // ============================================================
    // ⚠️ isEnum لا (r.action === 4): الخادم مسجّل JsonStringEnumConverter
    //    (Program.cs)، يعني الـ enum بيوصل **نصًّا** («Ignored») لا رقمًا.
    //    المقارنة بالرقم بترجّع false دايمًا - الشريحة بتقول «مُستبعَدة: 1»
    //    (العدّاد محسوب في الخادم فهو صح) والجدول يطلع فاضي.
    //    ⚠️ ونفس الغلط كان موجود قبل التعديل ده في تلوين الصف المستبعَد
    //       (r.action === 4 ? opacity) - عدّى من غير ما حد ياخد باله لأنه
    //       بيخفّت لون صف بس. isEnum موجودة في الملف ده أصلًا لنفس السبب.
    var CHIPS = [
      { k: 'new',  label: T('fh_ChipNew'),        n: d.newCount,        f: function (r) { return isEnum(r.action, 'new', 1); } },
      { k: 'chg',  label: T('fh_ChipUpdated'),    n: d.changedCount,    f: function (r) { return isEnum(r.action, 'changed', 3); } },
      { k: 'same', label: T('fh_ChipUnchanged'),  n: d.unchangedCount,  f: function (r) { return isEnum(r.action, 'unchanged', 2); } },
      { k: 'ign',  label: T('fh_ChipExcluded'),   n: d.ignoredCount,    f: function (r) { return isEnum(r.action, 'ignored', 4); } },
      { k: 'dev',  label: T('fh_ChipDeviations'), n: d.deviationCount,  f: function (r) { return !!r.deviation; } },
      { k: 'occ',  label: T('fh_ChipWithOccupant'), n: d.withOccupantCount, f: function (r) { return !!r.description; } }
    ];
    var pvFilter = '';

    function chipsHtml() {
      return CHIPS.map(function (c) {
        // ⚠️ الشريحة اللي عدّادها صفر مش قابلة للضغط: فلتر بيدّي جدول فاضي
        //    بيخلّي المستخدم يفتكر إن الشاشة بايظة.
        var dead = !(c.n || 0);
        return '<button type="button" class="fh-pvchip' + (pvFilter === c.k ? ' is-on' : '') +
          (dead ? ' is-dead' : '') + '"' + (dead ? ' disabled' : '') +
          ' data-chip="' + c.k + '">' + esc(c.label) + ': ' + (c.n || 0) + '</button>';
      }).join('');
    }

    // ⚠️ نصّ الإجراء بيتبني هنا من قيمة enum جاية من الخادم، مش بيتقرا من
    //    حقل نصّ الخادم كان بيرسمه. الخادم بيرجّع الحالة، والواجهة بترسمها
    //    باللغة المعروضة - فتبديل اللغة بيغيّرها، وتعديل صياغتها بيتعمل في
    //    ملف الموارد مع باقي نصوص الشاشة.
    function impActionText(a) {
      return isEnum(a, 'new', 1) ? T('fh_ImpActNew')
           : isEnum(a, 'changed', 3) ? T('fh_ImpActChanged')
           : isEnum(a, 'ignored', 4) ? T('fh_ImpActIgnored')
           : T('fh_ImpActUnchanged');
    }

    function rowsHtml() {
      var sel = CHIPS.filter(function (c) { return c.k === pvFilter; })[0];
      var list = sel ? d.rows.filter(sel.f) : d.rows;
      if (!list.length) return '<tr><td colspan="6" class="fh-empty">' + esc(T('fh_NoUnitsInOu')) + '</td></tr>';

      return list.map(function (r) {
        // ⚠️ لون الصف من الإجراء: الجديد أخضر واللي هيتغيّر كهرماني والمستبعَد
        //    باهت. الفرق بيتقرا من مسح الجدول بالعين من غير قراءة كل خانة.
        var cls = isEnum(r.action, 'new', 1) ? 'fh-pv-new'
                : isEnum(r.action, 'changed', 3) ? 'fh-pv-chg'
                : isEnum(r.action, 'ignored', 4) ? 'fh-pv-ign' : '';
        // الملاحظة: سبب التغيير أولًا - هو اللي المستخدم محتاجه عشان يوافق.
        // ⚠️ الأسباب بتوصل كمفاتيح موارد وبتتجمّع هنا. و r.deviation نصّ
        //    وصفي جاي من محلّل الأسماء (مش مفتاح)، فبيتعرض كما هو.
        var note = (r.changeNoteKeys && r.changeNoteKeys.length)
          ? r.changeNoteKeys.map(function (k) { return T(k); }).join(' · ')
          : (r.deviation || (r.ignoreReasonKey ? T(r.ignoreReasonKey) : ''));
        return '<tr class="' + cls + '">' +
          '<td style="font-weight:700">' + esc(r.displayName) + '</td>' +
          '<td><span class="fh-acct">' + esc(r.adAccount) + '</span></td>' +
          '<td style="font-size:11.5px">' + esc(T(r.organizationalUnitKey)) +
            (r.ouName ? '<span class="fh-ouname">' + esc(r.ouName) + '</span>' : '') + '</td>' +
          '<td class="fh-name">' + (r.description ? esc(r.description) : '<span class="fh-muted">-</span>') + '</td>' +
          '<td style="font-size:12px">' + esc(impActionText(r.action)) + '</td>' +
          '<td style="font-size:11.5px;color:#93370d">' + esc(note) + '</td>' +
          '</tr>';
      }).join('');
    }

    box.innerHTML =
      (d.truncated
        ? '<div class="fh-note red"><b>' + esc(T('fh_Truncated')) + '</b> ' + T('fh_TruncatedHint') + '</div>'
        : '') +
      // ⚠️ سطر واحد بدل أربع فقرات: الشرح يُقرأ مرّة أو مرّتين في عمر النظام،
      //    والجدول يُقرأ في كل مرّة. من أراده يفتحه.
      '<div class="fh-shortnote">' + esc(T('fh_ImportNoteShort')) +
        '<button type="button" id="fhNoteMore" aria-expanded="false">' + esc(T('fh_HowItWorks')) + '</button></div>' +
      '<div class="fh-note" id="fhNoteFull" hidden>' + T('fh_ImportNote') + '</div>' +
      '<div class="fh-pvchips" id="fhPvChips">' + chipsHtml() + '</div>' +
      '<div style="max-height:420px;overflow:auto;border:1px solid var(--gray-200);border-radius:12px">' +
      '<table><thead><tr><th>' + esc(T('fh_ColUnit')) + '</th><th>' + esc(T('fh_ColAccount')) + '</th><th>' + esc(T('fh_PrevOu')) + '</th>' +
      '<th>' + esc(T('fh_PrevOccupant')) + '</th><th>' + esc(T('fh_PrevAction')) + '</th><th>' + esc(T('fh_PrevNote')) + '</th></tr></thead>' +
      '<tbody id="fhPvBody">' + rowsHtml() + '</tbody></table></div>' +
      '<div style="display:flex;justify-content:flex-end;gap:10px;margin-top:14px">' +
      '<button class="btn btn-primary" id="fhApplyBtn">' + esc(T('fh_ApplyImport')) + '</button></div>';

    // ⚠️ الضغط على الشريحة المختارة تاني بيلغي الفلتر - نفس سلوك كروت
    //    الإحصائيات فوق، عشان الشاشة تتصرّف بطريقة واحدة.
    var nm = document.getElementById('fhNoteMore');
    if (nm) nm.addEventListener('click', function () {
      var full = document.getElementById('fhNoteFull');
      var open = full.hasAttribute('hidden');
      if (open) full.removeAttribute('hidden'); else full.setAttribute('hidden', '');
      nm.setAttribute('aria-expanded', open ? 'true' : 'false');
    });

    document.getElementById('fhPvChips').addEventListener('click', function (ev) {
      var b = ev.target.closest && ev.target.closest('[data-chip]');
      if (!b || b.disabled) return;
      var k = b.getAttribute('data-chip');
      pvFilter = (pvFilter === k) ? '' : k;
      document.getElementById('fhPvChips').innerHTML = chipsHtml();
      document.getElementById('fhPvBody').innerHTML = rowsHtml();
    });

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

  // ⚠️ اللوحة تُفتح قبل الطلب لا بعده: الضغط على زرّ لا يعقبه أثر ظاهر
  //    لثوانٍ يُقرأ على أنه ضغط لم يُسجَّل، فيُعاد الضغط.
  function syncPanel(open) {
    var p = document.getElementById('fhSyncPanel');
    var x = document.getElementById('fhSyncClose');
    if (!p) return;
    if (open) { p.removeAttribute('hidden'); if (x) x.removeAttribute('hidden'); }
    else {
      p.setAttribute('hidden', '');
      if (x) x.setAttribute('hidden', '');
      document.getElementById('fhAccess').innerHTML = '';
      document.getElementById('fhPreview').innerHTML = '';
    }
  }

  function busy(btn, fn, url, targetId) {
    var original = btn.textContent;
    btn.disabled = true;
    btn.textContent = T('fh_Working');
    syncPanel(true);
    fetch(url, { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(fn)
      .catch(function (e) {
        // ⚠️ الخطأ يُكتب في قسم الزرّ الذي أُطلق منه، لا في قسم الفحص دائمًا:
        //    خطأُ مقارنةٍ يظهر تحت «فحص الصلاحيات» يُقرأ على أن الصلاحيات هي العطب.
        document.getElementById(targetId || 'fhAccess').innerHTML =
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
      state.onlyOuMismatch = false;
      state.onlyDisabled = false;
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
      busy(checkBtn, renderAccess, '/api/FacultyHousing/access-check', 'fhAccess');
    });

    var prevBtn = document.getElementById('fhPreviewBtn');
    if (prevBtn) prevBtn.addEventListener('click', function () {
      busy(prevBtn, renderPreview, '/api/FacultyHousing/import/preview', 'fhPreview');
    });

    var syncX = document.getElementById('fhSyncClose');
    if (syncX) syncX.addEventListener('click', function () { syncPanel(false); });

    document.getElementById('fhHistoryOverlay').addEventListener('click', function (e) {
      if (e.target === this) window.fhCloseHistory();
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') window.fhCloseHistory();
    });

    // ⚠️ الخانات الظاهرة لازم توصف اللي معروض: جدول مفلتر وخاناته فاضية
    //    بيتقري كأنه كل الوحدات - وهو جزء منها.
    (function initFromUrl() {
      Array.prototype.forEach.call(document.querySelectorAll('#fhTypeSeg button'), function (b) {
        b.classList.toggle('is-on', b.getAttribute('data-type') === state.type);
      });
      var st = document.getElementById('fhStatus');
      if (st) st.value = state.status;
      var sr = document.getElementById('fhSearch');
      if (sr) sr.value = state.search;
      // ⚠️ قائمة الأبراج بتتملّى من رد الخادم، فقيمتها بتتظبط في renderTowers
      //    لا هنا - لو ظبطناها دلوقتي هتترمي مع أول رسم للقائمة.
    })();

    load();
  });
})();
