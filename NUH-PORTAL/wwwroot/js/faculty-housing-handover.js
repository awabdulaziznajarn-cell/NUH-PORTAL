// شاشة تسليم وحدة سكن أعضاء هيئة التدريس لشاغل جديد.
//
// ⚠️ النموذج نفسه مكتوب في Handover.cshtml لا يُبنى هنا: بناؤه من JavaScript
//    كان يعني تمرير كل تسمية عبر قاموس، وهو التفاف على SharedLocalizer.
//    هذا الملف يتولّى المتغيّر فقط: تحميل بيانات الوحدة، والتحقّق، والحفظ،
//    ثم عرض الفرق والكتابة في الدومين.
(function () {
  'use strict';

  var UNIT = window.FH_UNIT_ID;
  // ⚠️ 'handover' يغيّر الساكن، و'edit' يصحّح بياناته دون تغييره. الفرق يظهر
  //    هنا في نقطة الحفظ فقط: القسمان اللي يميّزان التسليم محذوفان من الصفحة
  //    أصلًا في وضع التعديل، فلا حاجة لإخفائهما بالسكربت.
  var MODE = window.FH_MODE || 'handover';
  var IS_EDIT = MODE === 'edit';

  function T(k) { return (window.FH_T && window.FH_T[k]) || k; }
  function canSync() { return window.FH_CAN_SYNC === true || window.FH_CAN_SYNC === 'true'; }
  function el(id) { return document.getElementById(id); }
  function val(id) { var e = el(id); return e ? e.value.trim() : ''; }   // العنصر قد لا يوجد في وضع التعديل

  // ⚠️ خطأ تحقّق ASP.NET يصل في j.errors كقاموس حقل ← رسائل، وعنوانه العام
  //    "One or more validation errors occurred" لا يدل على شيء. عرض العنوان
  //    وحده كان يعني أن المستخدم يرى رفضًا بلا سبب ولا حقل - وهو ما حدث فعلًا.
  //    نستخرج أول رسالة لكل حقل ونعرضها مسبوقة باسمه.
  function serverError(j, status) {
    if (!j) return 'HTTP ' + status;
    if (j.errors && typeof j.errors === 'object') {
      var parts = Object.keys(j.errors).map(function (f) {
        var m = j.errors[f];
        return f + ': ' + (Array.isArray(m) ? m[0] : m);
      });
      if (parts.length) return parts.join(' · ');
    }
    return j.message || j.detail || j.title || ('HTTP ' + status);
  }

  // ⚠️ ربط الحدث لا يفترض وجود العنصر. الشاشة تعرض حقولًا مختلفة حسب الوضع
  //    (بطاقتا المعاملة وإنهاء السكن محذوفتان من الصفحة في وضع التعديل)،
  //    فأي ربط مباشر على عنصر غائب يوقف السكربت كله عند أول سطر - وهو ما
  //    حدث فعلًا: الشاشة ظهرت فارغة برسالة addEventListener of null.
  function on(id, ev, fn) {
    var e = el(id);
    if (e) e.addEventListener(ev, fn);
  }

  // ⚠️ التحقّق والقيمة من NuhPhone، مع بديل بسيط لو الملف لم يُحمَّل — حتى لا
  //    تتعطّل الشاشة كليًا بسبب سكربت واحد.
  function phoneOk() {
    var v = val('hoMobile');
    return window.NuhPhone && NuhPhone.isValid ? NuhPhone.isValid(v) : /^5\d{8}$/.test(v);
  }
  function phoneValue() {
    var v = val('hoMobile');
    return window.NuhPhone && NuhPhone.normalize ? NuhPhone.normalize(v) : v;
  }

  // تهريب HTML — التعريف الوحيد في /js/esc.js
  function esc(s) { return escHtml(s); }

  // ⚠️ المخزَّن 966XXXXXXXXX والحقل يعرض ٩ أرقام تبدأ بـ 5 خلف البادئة +966.
  //    القديم مخزَّن 05XXXXXXXX، فيُعرض بعد إزالة الصفر - ومجرّد الحفظ يحوّله
  //    للصيغة الموحّدة عبر NuhPhone. وهذه هي الطريقة التي تُصحَّح بها الأرقام
  //    القديمة: تفتح الوحدة وتحفظ، دون كتابة شيء.
  function localMobile(v) {
    var d = String(v || '').replace(/\D/g, '');
    if (d.indexOf('966') === 0) d = d.slice(3);
    if (d.indexOf('0') === 0) d = d.slice(1);
    return d;
  }

  // ⚠️ الكلية والقسم من /api/lookups عبر Lookups المشترك، لا نص حر ولا نسخة
  //    ثانية من منطق الجلب. والقسم تابع للكلية: قائمة الأقسام كاملةً قبل
  //    اختيار كلية تعني إمكان اختيار قسم لا يتبعها — بيانات متضاربة تمرّ دون
  //    أن يلاحظها أحد. القاعدة نفسها المطبَّقة في شاشة تسجيل الطالب.
  var deptGate = null;

  function initLookups(keepCollege, keepDept) {
    if (typeof Lookups === 'undefined') return;   // الملف لم يُحمَّل: الحقول تبقى فارغة لا معطّلة

    deptGate = Lookups.dependent({
      parent: el('hoCollege'),
      child: el('hoDept'),
      url: function (id) { return '/api/lookups/departments' + (id ? ('?collegeId=' + id) : ''); },
      fallbackUrl: '/api/lookups/departments',
      waitText: T('fh_SelectCollegeFirst'),
      chooseText: T('fh_SelectDepartment'),
      keep: keepDept || ''
    });

    Lookups.fetchList('/api/lookups/colleges').then(function (items) {
      if (!items || !items.length) return;
      // ⚠️ المطابقة بالاسم لا بالرمز: المحفوظ في السجل اسم معروض (وهو ما يُكتب
      //    في الدليل)، والمستورَد منه نصّ حرّ قديم قد لا يطابق أي عنصر. عندها
      //    تبقى القائمة على خيارها الأول ويظهر للمستخدم أن القيمة تحتاج اختيارًا.
      var match = keepCollege
        ? items.filter(function (i) { return i.name === keepCollege || i.code === keepCollege; })[0]
        : null;
      Lookups.fill(el('hoCollege'), items, { keep: match ? match.code : '' });
      if (deptGate) deptGate.refresh();
    });
  }

  // ⚠️ المرسَل إلى الخادم هو الاسم المعروض لا الرمز: وجهته الوحيدة خاصيتا
  //    company و department في الدليل، وهما تحملان اسمًا معروضًا لا رمزًا.
  //    (لو احتجنا لاحقًا تقارير بالكلية، يُخزَّن الرمز ويُترجَم عند الكتابة.)
  function selText(id) {
    var e = el(id);
    if (!e || e.selectedIndex < 0) return null;
    if (!e.value) return null;
    var o = e.options[e.selectedIndex];
    return o ? o.textContent.trim() : null;
  }

  function todayIso() {
    var d = new Date(), p = function (n) { return (n < 10 ? '0' : '') + n; };
    return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate());
  }

  // ---------- تحميل بيانات الوحدة ----------
  function init() {
    fetch('/api/FacultyHousing/units/' + UNIT, { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) { render(d); })
      .catch(function (e) {
        el('fhLoad').innerHTML = esc(T('fh_HandoverError')) + esc(e.message);
      });
  }

  function render(d) {
    var u = d.unit;
    var hasOccupant = !!u.occupancyId;

    el('fhUnitSub').innerHTML =
      esc(u.displayName) + ' &nbsp;·&nbsp; <span class="fh-acct">' + esc(u.adAccount) + '</span>';

    // ⚠️ نفس d.changes اللي جت في الاستجابة دي - مافيش نداء تاني. الخادم
    //    بيرجّع سجل العمليات مع بيانات الوحدة في طلب واحد أصلًا.
    paintLastChange(d);

    // ⚠️ الخيارات المعروضة تتبع حالة الوحدة: وحدة بلا شاغل لا يصحّ عليها
    //    «تغيير الشاغل»، ووحدة مشغولة لا يصحّ عليها «خدمة جديدة». عرض خيار
    //    لا يقبله الخادم يعني رسالة خطأ بعد ملء النموذج كاملًا.
    if (IS_EDIT) {
      // ⚠️ الحقول تُملأ بالمسجّل حاليًا: التعديل يبدأ من الموجود لا من فراغ.
      //    نموذج فارغ كان سيجعل تصحيح حقل واحد يتطلّب إعادة كتابة الباقي.
      if (!hasOccupant) {
        el('fhLoad').textContent = T('fh_NoCurrentOccupant');
        return;
      }
      el('hoName').value = u.occupantName || '';
      el('hoNid').value = u.occupantNationalId || '';
      el('hoMobile').value = localMobile(u.occupantMobile);
      el('hoGender').value = u.occupantGender || '';
      el('hoStart').value = (u.occupantSince || '').slice(0, 10) || todayIso();

      // ⚠️ الكلية والقسم من نفس الاستجابة لا من طلب ثانٍ: التفاصيل المطلوبة
      //    وصلت كلها في history ضمن أول نداء، وطلب إضافي لنفس المسار يضاعف
      //    الحمل ويفتح فجوة زمنية قد تُظهر الحقلين فارغين للحظة.
      var cur = (d.history || []).filter(function (o) { return o.isCurrent; })[0];
      initLookups(cur ? cur.college : '', cur ? cur.department : '');
    } else {
      var types = hasOccupant
        ? [['ChangeOccupant', window.FH_TYPES.change], ['StopService', window.FH_TYPES.stop]]
        : [['NewService', window.FH_TYPES.newSvc]];

      el('hoType').innerHTML = types.map(function (t) {
        return '<option value="' + t[0] + '">' + esc(t[1]) + '</option>';
      }).join('');

      el('hoStart').value = todayIso();
      el('hoEnd').value = todayIso();
      if (hasOccupant) el('hoCurName').value = u.occupantName || '';

      on('hoType', 'change', syncSections);
      // ⚠️ NuhSelect بيطلق change على الـ select الأصلي بنفسه (select-field.js)،
      //    فالربط ده شغّال مع القائمة المزخرفة زي العادية بالظبط.
      on('hoReason', 'change', syncReasonNote);
      syncReasonNote();
      syncSections();
      initLookups('', '');
    }

    // ⚠️ منع الحروف أثناء الكتابة لا عند الحفظ: تصحيح حقل بعد ملء النموذج
    //    كاملًا أسوأ من منع الخطأ لحظة وقوعه.
    ['hoTicket', 'hoNid'].forEach(function (k) {
      on(k, 'input', function () { this.value = this.value.replace(/\D/g, ''); });
    });

    // ⚠️ الجوال يتولّاه NuhPhone المشترك لا هذه الشاشة: قاعدة الرقم السعودي
    //    (٩ أرقام تبدأ بـ 5، ومعالجة اللصق بصيغة 05 أو +966) معرّفة مرة واحدة
    //    في js/phone-field.js. كتابتها هنا كانت ستعيد المشكلة التي كُتب ذلك
    //    الملف لحلّها: صيغة مختلفة في كل شاشة.
    if (window.NuhPhone && NuhPhone.attach && el('hoMobile')) NuhPhone.attach(el('hoMobile'));

    on('hoSave', 'click', submit);

    el('fhLoad').style.display = 'none';
    el('fhWrap').style.display = '';
  }

  function syncSections() {
    var stop = val('hoType') === 'StopService';
    var card = el('hoNewCard');
    if (card) card.style.display = stop ? 'none' : '';
  }

  // ============================================================================
  //  ربط «بيان السبب» بـ «سبب آخر».
  //
  //  ⚠️ بنمسح المكتوب لمّا المستخدم يخرج من «سبب آخر»، مش بنقفل الخانة وبس.
  //     لو سبناه: حد يكتب بيان، يغيّر رأيه ويختار «انتهاء التعاقد»، فيتحفظ
  //     صف سببه «انتهاء التعاقد» وبيانه كلام عن سبب تاني خالص - والخانة
  //     مقفولة فمش شايف اللي هيتبعت.
  //
  //  ⚠️ والحقل بيتقفل بـ disabled: ده بيمنع الكتابة **وبيمنع الإرسال** كمان
  //     لو حد وصل له بالكيبورد.
  // ============================================================================
  function syncReasonNote() {
    var sel = el('hoReason'), note = el('hoReasonNote');
    if (!sel || !note) return;
    var isOther = sel.value === 'Other';
    note.disabled = !isOther;
    if (!isOther) { note.value = ''; note.classList.remove('error'); }
    var req = el('hoReasonNoteReq');
    if (req) req.style.display = isOther ? '' : 'none';
    var hint = el('hoReasonNoteHint');
    if (hint) hint.style.display = isOther ? 'none' : '';
  }

  // ==========================================================================
  //  سطر «آخر تعديل» — الرسم في js/faculty-audit-log.js المشترك.
  //
  //  ⚠️ العنوان الفرعي للنافذة اسم الوحدة لا رقمها: اللي بيفتح السجل عايز
  //     يتأكد إنه بيبصّ على «برج ٦ - شقة ٢٠»، ورقم الصف في قاعدة البيانات
  //     مايقولش له حاجة.
  // ==========================================================================
  function paintLastChange(d) {
    var u = (d && d.unit) || {};
    var sub = [u.displayName, u.adAccount].filter(Boolean).join('  ·  ');
    NuhFacultyLog.mount('hoLastChg', (d && d.changes) || [], sub);
  }

  // ⚠️ بعد الحفظ بنعيد جلب الوحدة عشان السطر يشمل العملية اللي اتعملت للتوّ.
  //    من غير كده السطر بيفضل بيقول «آخر تعديل» بتاع اللي قبلك - وإنت لسه
  //    حافظ دلوقتي، وde أسوأ من إنه مايظهرش خالص.
  function refreshLastChange() {
    fetch('/api/FacultyHousing/units/' + UNIT, { credentials: 'same-origin' })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (d) { if (d) paintLastChange(d); })
      .catch(function () { /* السطر يفضل على قيمته القديمة - مش مبرّر لرسالة خطأ */ });
  }

  // ---------- التحقّق ثم الحفظ ----------
  function markBad(ids) {
    ['hoTicket', 'hoName', 'hoNid', 'hoMobile', 'hoStart', 'hoEnd', 'hoReasonNote'].forEach(function (k) {
      var e = el(k);
      if (e) e.classList.toggle('error', ids.indexOf(k) >= 0);
    });
  }

  function submit() {
    var type = IS_EDIT ? 'ChangeOccupant' : val('hoType');
    var stop = !IS_EDIT && type === 'StopService';
    // قسم «إنهاء الإشغال» بيظهر في كل الأنواع ما عدا «خدمة جديدة»
    var closing = !IS_EDIT && type !== 'NewService';
    var bad = [];

    if (!IS_EDIT && !val('hoTicket')) bad.push('hoTicket');
    // ⚠️ «سبب آخر» من غير بيان = صف في السجل بيقول «السبب مش واحد من التلاتة»
    //    وبس. الخانة اللي بتتفتح مخصوص للحالة دي لازم تتملى فيها.
    if (!IS_EDIT && closing && val('hoReason') === 'Other' && !val('hoReasonNote'))
      bad.push('hoReasonNote');
    if (!stop) {
      if (!val('hoName')) bad.push('hoName');
      if (val('hoNid').length !== 10) bad.push('hoNid');
      if (!phoneOk()) bad.push('hoMobile');
      if (!val('hoStart')) bad.push('hoStart');
    }
    markBad(bad);
    if (bad.length) {
      el('hoMsg').innerHTML = '<div class="fh-note red">' + esc(T('fh_Required')) + '</div>';
      return;
    }
    el('hoMsg').innerHTML = '';

    var body = {
      requestType: type,
      ticketNo: IS_EDIT ? null : val('hoTicket'),
      fullNameAr: stop ? null : val('hoName'),
      gender: val('hoGender') || null,
      nationalId: stop ? null : val('hoNid'),
      mobile: stop ? null : phoneValue(),
      college: selText('hoCollege'),
      department: selText('hoDept'),
      startDate: stop ? null : (val('hoStart') || null),
      endDate: IS_EDIT ? null : (val('hoEnd') || null),
      endReason: IS_EDIT ? null : (val('hoReason') || null),
      endReasonNote: IS_EDIT ? null : (val('hoReasonNote') || null),
      pushToAd: false
    };

    var btn = el('hoSave');
    btn.disabled = true;
    btn.textContent = T('fh_Saving');

    fetch('/api/FacultyHousing/units/' + UNIT + (IS_EDIT ? '/occupant' : '/handover'), {
      method: IS_EDIT ? 'PUT' : 'POST', credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    })
      .then(function (r) {
        return r.json().then(function (j) {
          // ⚠️ رسالة الرفض تُعرض كما وردت من الخادم: قواعد الهوية والجوال ورقم
          //    المعاملة محلّها الخدمة، وتكرار نصّها هنا يُنتج نصّين قد يفترقان.
          if (!r.ok) throw new Error(serverError(j, r.status));
          return j;
        });
      })
      .then(function () {
        el('hoMsg').innerHTML =
          '<div class="fh-note green">' + esc(T(IS_EDIT ? 'fh_SavedEdit' : 'fh_SavedPending')) + '</div>';
        // ⚠️ النموذج يُقفل بعد الحفظ: السجل أُنشئ فعلًا، وأي تعديل بعده يجب
        //    أن يكون تسليمًا جديدًا لا كتابة فوق سجل قائم.
        el('hoActions').style.display = 'none';
        disableForm();
        refreshLastChange();
        loadDiff();
      })
      .catch(function (e) {
        el('hoMsg').innerHTML =
          '<div class="fh-note red">' + esc(T('fh_HandoverError')) + esc(e.message) + '</div>';
      })
      .finally(function () { btn.disabled = false; btn.textContent = T('fh_Save'); });
  }

  function disableForm() {
    Array.prototype.forEach.call(
      document.querySelectorAll('#fhWrap input, #fhWrap select'),
      function (e) { e.disabled = true; });
  }

  // ---------- الفرق ثم الكتابة ----------
  function loadDiff() {
    var box = el('hoDiff');
    box.innerHTML = '<div class="fh-empty">' + esc(T('fh_Loading')) + '</div>';

    fetch('/api/FacultyHousing/units/' + UNIT + '/ad-diff', { credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) {
        if (d.error) { box.innerHTML = '<div class="fh-note red">' + esc(d.error) + '</div>'; return; }

        // ⚠️ الملاحظة بقت **تحت** القيمة الجديدة لا بدلها. كان مكتوب
        //    (l.note || l.newValue): أي سطر ليه ملاحظة كانت القيمة الجديدة
        //    بتختفي وراها. وde بان مع سطر الـ OU: الملاحظة بتقول إن فيه
        //    تعارض، والمسار اللي الحساب هيتنقل له - وهو اللي المسؤول محتاج
        //    يشوفه قبل ما يوافق - مكانش بيظهر خالص.
        // ⚠️ ومسارات الـ DN بتتلفّ في <bdi dir="ltr">: نصّ لاتيني فيه فواصل
        //    جوّه خانة عربية بيتقلب ترتيبه (OU=MALE,DC=nuh بتبان مقلوبة).
        var rows = d.lines.map(function (l) {
          var isDn = l.attribute === 'OU';
          function cell(v) {
            if (!v) return '-';
            if (!isDn) return esc(v);
            // ⚠️ المعروض أول جزء من المسار (OU=FEMALE) والمسار الكامل في
            //    title. المسار كامل ٥٦ حرفًا، والجزء اللي بيفرق بين القديم
            //    والجديد كلمة واحدة في أوّله - عرض الاتنين كاملين كان بيخلّي
            //    العين تقارن سطرين متطابقين تقريبًا عشان تلاقي الفرق.
            var head = String(v).split(',')[0];
            return '<bdi dir="ltr" class="fh-dn" title="' + esc(v) + '">' + esc(head) + '</bdi>';
          }
          var note = l.note ? '<span class="fh-diffnote">' + esc(l.note) + '</span>' : '';
          return '<tr><td class="attr">' + esc(l.attribute) + '</td>' +
            '<td class="' + (l.willChange ? 'fh-old' : 'fh-same') + '">' + cell(l.currentValue) + '</td>' +
            '<td class="' + (l.willChange ? 'fh-new' : 'fh-same') + '">' +
              cell(l.newValue) + note + '</td></tr>';
        }).join('');

        box.innerHTML =
          '<div class="form-card">' +
            '<div class="form-card-header">' +
              '<div class="form-card-header-icon">' +
                '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
                'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
                '<polyline points="17 1 21 5 17 9"/><path d="M3 11V9a4 4 0 0 1 4-4h14"/>' +
                '<polyline points="7 23 3 19 7 15"/><path d="M21 13v2a4 4 0 0 1-4 4H3"/></svg>' +
              '</div>' +
              '<div class="form-card-title">' + esc(T('fh_SecDiff')) + '</div>' +
            '</div>' +
            '<div class="fh-diff"><table><thead><tr>' +
              '<th>' + esc(T('fh_DiffAttr')) + '</th>' +
              '<th>' + esc(T('fh_DiffCurrent')) + '</th>' +
              '<th>' + esc(T('fh_DiffNew')) + '</th>' +
            '</tr></thead><tbody>' + rows + '</tbody></table></div>' +
          '</div>' +
          (d.changeCount === 0
            ? '<div class="fh-note">' + esc(T('fh_DiffNone')) + '</div>'
            : '<div class="fh-actions">' +
                '<span style="align-self:center;font-size:12.5px;color:var(--gray-500)">' +
                  d.changeCount + ' ' + esc(T('fh_DiffCount')) + '</span>' +
                (canSync()
                  ? '<button class="btn btn-primary" id="hoPush">' + esc(T('fh_ApplyToAd')) + '</button>'
                  : '') +
              '</div>');

        var push = el('hoPush');
        if (push) push.addEventListener('click', pushToAd);
      })
      .catch(function (e) {
        box.innerHTML = '<div class="fh-note red">' + esc(T('fh_HandoverError')) + esc(e.message) + '</div>';
      });
  }

  function pushToAd() {
    var btn = el('hoPush');
    btn.disabled = true;
    btn.textContent = T('fh_Working');

    fetch('/api/FacultyHousing/units/' + UNIT + '/push-ad', { method: 'POST', credentials: 'same-origin' })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (d) {
        el('hoDiff').innerHTML =
          '<div class="fh-note ' + (d.success ? 'green' : 'red') + '">' +
          esc(d.success ? T('fh_PushOk') : T('fh_PushFail')) +
          // ⚠️ النقل بين الـ OU بيتقال صريح: ده تغيير في مكان الحساب في
          //    الدليل مش خانة، والمسؤول لازم يعرف إنه حصل عشان يقدر يراجعه.
          (d.movedToOu
            ? '<br>' + esc(T('fh_PushMoved')) + ' <bdi dir="ltr" class="fh-dn">' + esc(d.movedToOu) + '</bdi>'
            : '') +
          (d.error ? '<br>' + esc(d.error) : '') + '</div>' +
          '<div class="fh-actions"><a class="btn btn-primary" href="/FacultyHousing">' +
          esc(T('fh_UnitsList')) + '</a></div>';
        // الكتابة في الدومين عملية في السجل زي غيرها - والنقل بين الـ OU معاها
        refreshLastChange();
      })
      .catch(function (e) {
        el('hoDiff').innerHTML =
          '<div class="fh-note red">' + esc(T('fh_HandoverError')) + esc(e.message) + '</div>';
      })
      .finally(function () { btn.disabled = false; btn.textContent = T('fh_ApplyToAd'); });
  }

  document.addEventListener('DOMContentLoaded', init);
})();
