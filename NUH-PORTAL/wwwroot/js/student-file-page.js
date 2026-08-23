// ============================================================================
//  شاشة «ملف الطالب» - البحث برقم جامعي أو رقم هوية.
//
//  ⚠️ الشاشة دي بتعرض بيانات موجودة أصلًا في النظام - الجديد هو **التجميع**:
//     الشخصي والأكاديمي والسكن وحساب الشبكة والتعهّد الموقّع في صفحة واحدة
//     قابلة للطباعة. التحقيق بيحتاجهم مع بعض، ولفّ الموظف على أربع شاشات
//     ونسخهم بإيده معناه إنه هينسى واحدة.
//
//  ⚠️ وثيقة التعهّد مش مرسومة هنا: بتتبني من NuhPledgeDoc (js/pledge-doc.js)،
//     نفس الوحدة اللي بترسمها في شاشة تفاصيل الطلب. نسختين لوثيقة رسمية
//     بيفترقوا مع أول تعديل.
//
//  ⚠️ وكل فتح للملف بيتسجّل في سجل العمليات على الخادم
//     (StudentFileService.GetAsync) - مش من هنا. التسجيل اللي في الواجهة
//     بيتشال بسطر في أدوات المطوّر.
// ============================================================================
(function () {
  'use strict';

  // نفس منطق باقي الشاشات: الجلسة كوكي، والتوكن بقايا توافق.
  var token = localStorage.getItem('staffToken');
  function authHeaders() { return token ? { 'Authorization': 'Bearer ' + token } : {}; }

  var form = document.getElementById('sfForm');
  var input = document.getElementById('sfQuery');
  var btn = document.getElementById('sfBtn');
  var alertBox = document.getElementById('sfAlert');
  var result = document.getElementById('sfResult');

  var FILE = null;   // آخر ملف اتحمّل

  function showAlert(msg, type) {
    if (!msg) { alertBox.className = 'alert'; alertBox.textContent = ''; return; }
    alertBox.textContent = msg;
    alertBox.className = 'alert alert-' + (type || 'error');
  }

  // ⚠️ أيقونة لا حروف أولى: الاختصار بالحروف عُرف إنجليزي - «م ص» بالعربي
  //    مالهاش معنى متعارف عليه، وبتنكسر مع الاسم الحرف الواحد أو الأجنبي أو
  //    الفاضي. والأيقونة محايدة وشكلها ثابت مع أي اسم، والاسم الكامل مكتوب
  //    جنبها أصلًا.
  var AVATAR_ICON =
    '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
    'stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
    '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>';

  // ⚠️ .info-* هو مكوّن شبكة البيانات الوحيد في النظام (css/components.css).
  //    كانت هنا نسخة تانية بأسماء تانية وبفرق مقاس في القيمة الرقمية:
  //    ١٣px بدل ١٤px، فرقم الهوية والجوال كانوا أصغر من باقي البيانات في
  //    نفس البطاقة - ودي بالظبط اللي بتحصل مع أي نسخة تانية.
  function field(label, value, mono) {
    return '<div class="info-field"><span class="info-label">' + label + '</span>' +
           '<span class="info-value' + (mono ? ' mono' : '') + '">' + value + '</span></div>';
  }

  function dash(v) { return (v == null || v === '') ? '-' : escHtml(String(v)); }

  // حالة الطلب: الصنف والاسم من NuhWorkflow (js/request-workflow.js) لا من
  // خريطة مكتوبة هنا.
  // ⚠️ المسار القديم والجديد ليهم أسماء مختلفة لنفس المرحلة
  //    (pending_cyber / cyber_review مثلًا)، والتوحيد في Core/RequestWorkflow.cs
  //    وبيوصل الواجهة في __WF. أي خريطة تتكتب هنا هتبقى نسخة تانية تفترق أول
  //    ما تتضاف مرحلة.
  function statusBadge(status) {
    var st = String(status || '').toLowerCase();
    if (!st) return '';
    var cls = NuhWorkflow.canonical(st);
    var label = t(NuhWorkflow.badgeKey(st));
    if (label === NuhWorkflow.badgeKey(st)) label = st;
    return '<span class="badge badge-' + escHtml(cls) + '">' + escHtml(label) + '</span>';
  }

  // ⚠️ نفس مفاتيح req_type_* المستخدمة في شاشة تفاصيل الطلب - مش مفاتيح جديدة.
  function requestTypeName(code) {
    var raw = String(code == null ? '' : code);
    if (!raw) return '';
    var key = 'req_type_' + raw;
    var v = t(key);
    return v === key ? raw : v;
  }

  // ---------------------------------------------------------------- الرسم
  function render(file) {
    FILE = file;
    var s = file.student || {};

    var deletedTag = file.isDeleted
      ? ' <span class="badge badge-rejected">' + tf('sf_deleted', 'سجل محذوف', 'Deleted record') + '</span>'
      : '';

    var head =
      '<div class="sf-audit">' +
        '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg>' +
        '<span>' + tf('sf_auditNote',
          'تم تسجيل فتح هذا الملف في سجل العمليات. الاطّلاع على ملفات الطلاب إجراء موثّق.',
          'Opening this file has been recorded in the audit log. Viewing student files is a logged action.') + '</span>' +
      '</div>' +

      '<div class="sf-head">' +
        '<div class="sf-avatar">' + AVATAR_ICON + '</div>' +
        '<div class="sf-id">' +
          '<h3>' + dash(s.full_name) + deletedTag + '</h3>' +
          '<p>' + tf('rdp_field_studentId', 'الرقم الجامعي', 'Student ID') + ' ' + dash(s.student_id) +
            ' &nbsp;·&nbsp; ' + tf('rdp_field_nationalId', 'رقم الهوية', 'National ID') + ' ' + dash(s.national_id) + '</p>' +
        '</div>' +
        '<div class="sf-actions">' +
          (file.pledge && file.pledge.documented
            ? '<button type="button" class="sf-act primary" onclick="NuhPledgeDoc.print(\'studentFileSheet\')">' +
                '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><path d="M6 14h12v8H6z"/></svg>' +
                tf('sf_printFile', 'طباعة الملف كاملًا', 'Print full file') + '</button>'
            : '') +
          (file.pledge && file.pledge.documented
            ? '<button type="button" class="sf-act" onclick="NuhPledgeDoc.print()">' +
                '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><path d="M6 14h12v8H6z"/></svg>' +
                tf('sf_printPledge', 'طباعة وثيقة التعهّد', 'Print pledge document') + '</button>'
            : '') +
          (file.selectedRequestId
            ? '<a class="sf-act" href="/Requests/Details/' + file.selectedRequestId + '">' +
                tf('sf_openRequest', 'فتح الطلب في الشاشة', 'Open request') + '</a>'
            : '') +
        '</div>' +
      '</div>';

    // ---------- الطلبات ----------
    var reqs = (file.requests || []).map(function (r) {
      var on = r.id === file.selectedRequestId;
      return '<button type="button" class="sf-req' + (on ? ' is-on' : '') + '" data-req="' + r.id + '">' +
        '<span class="n"><span class="sf-reqlbl">' +
          tf('rdp_field_requestNumber', 'رقم الطلب', 'Request No.') + '</span> ' +
          dash(r.requestNumber) + '</span>' +
        '<span class="d">' + escHtml(requestTypeName(r.requestType)) + ' · ' + NuhFmt.date(r.submittedAt) + '</span>' +
        '<span class="s">' +
          (r.hasPledge ? '' : '<span class="sf-nopledge">' + tf('sf_noPledgeShort', 'بلا تعهّد', 'No pledge') + '</span>') +
          statusBadge(r.status) +
        '</span></button>';
    }).join('');

    var reqCard = '<div class="card"><div class="card-header">' +
      '<div class="ch-title">' +
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>' +
      '<span>' + tf('sf_cardRequests', 'طلبات الطالب', 'Student requests') + '</span>' +
      '</div></div><div class="card-body">' +
      (reqs
        ? '<div class="sf-reqs">' + reqs + '</div>'
        : '<div class="sf-empty">' + tf('sf_noRequests', 'لا توجد طلبات مسجَّلة لهذا الطالب', 'No requests recorded for this student') + '</div>') +
      '</div></div>';

    // ---------- البيانات الشخصية ----------
    var personal = '<div class="card"><div class="card-header">' +
      '<div class="ch-title">' +
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>' +
      '<span>' + tf('rdp_card_studentInfo', 'البيانات الشخصية', 'Personal information') + '</span>' +
      '</div></div><div class="card-body"><div class="info-grid">' +
      field(tf('rdp_field_nameAr', 'الاسم بالعربية', 'Name (Arabic)'), dash(s.full_name)) +
      field(tf('rdp_field_nameEn', 'الاسم بالإنجليزية', 'Name (English)'), dash(s.full_name_english)) +
      field(tf('rdp_field_nationalId', 'رقم الهوية', 'National ID'), dash(s.national_id), true) +
      field(tf('rdp_field_mobile', 'الجوال', 'Mobile'), dash(s.phone), true) +
      '</div></div></div>';

    // ---------- الأكاديمية والسكن ----------
    var housing = [];
    if (s.housing_building) housing.push(escHtml(buildingName(s.housing_building)));
    if (s.apartment_number) housing.push(tf('loc_apartment', 'شقة', 'Apt') + ' ' + escHtml(s.apartment_number));
    if (s.room_number) housing.push(tf('loc_room', 'غرفة', 'Room') + ' ' + escHtml(s.room_number));

    var academic = '<div class="card"><div class="card-header">' +
      '<div class="ch-title">' +
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>' +
      '<span>' + tf('sf_cardAcademic', 'البيانات الأكاديمية والإسكان', 'Academic and housing') + '</span>' +
      '</div></div><div class="card-body"><div class="info-grid">' +
      field(tf('rdp_field_college', 'الكلية', 'College'), escHtml(collegeName(s.college))) +
      field(tf('rdp_field_department', 'القسم', 'Department'), escHtml(deptName(s.department))) +
      field(tf('rdp_field_level', 'المستوى', 'Level'), escHtml(levelName(s.academic_level))) +
      field(tf('rdp_field_housing', 'السكن', 'Housing'), housing.length ? housing.join(' · ') : '-') +
      '</div></div></div>';

    // ---------- حساب الشبكة ----------
    // ⚠️ أول حاجة التحقيق بيبدأ منها عادةً، فليها بطاقة لوحدها مش سطر مدفون.
    var ad = file.ad || null;
    var adBody;
    if (!ad) {
      adBody = '<div class="sf-empty">' + tf('sf_noAd', 'لا يوجد حساب شبكة مرتبط بهذا الطالب', 'No network account linked to this student') + '</div>';
    } else if (file.adUnavailable) {
      adBody = '<div class="info-grid">' +
        field(tf('sf_adUser', 'اسم المستخدم', 'Username'), dash(ad.username), true) +
        field(tf('sf_adLastLogon', 'آخر دخول للشبكة', 'Last network sign-in'),
              '<span style="color:var(--amber)">' + tf('sf_adUnavailable', 'تعذّر الوصول للـAD', 'AD unreachable') + '</span>') +
        '</div>';
    } else {
      adBody = '<div class="info-grid">' +
        field(tf('sf_adUser', 'اسم المستخدم', 'Username'), dash(ad.username), true) +
        field(tf('sf_adState', 'حالة الحساب', 'Account state'),
              ad.enabled
                ? '<span class="badge badge-completed">' + tf('sf_adEnabled', 'مفعّل', 'Enabled') + '</span>'
                : '<span class="badge badge-rejected">' + tf('sf_adDisabled', 'معطّل', 'Disabled') + '</span>') +
        field(tf('sf_adLastLogon', 'آخر دخول للشبكة', 'Last network sign-in'),
              ad.lastLogonAt ? NuhFmt.dateTime(ad.lastLogonAt) : tf('sf_adNeverLogged', 'لم يسجّل دخولًا', 'Never signed in')) +
        '</div>';
    }
    var adCard = '<div class="card"><div class="card-header">' +
      '<div class="ch-title">' +
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8"/><path d="M12 17v4"/></svg>' +
      '<span>' + tf('sf_cardAd', 'حساب الشبكة', 'Network account') + '</span>' +
      '</div></div><div class="card-body">' + adBody + '</div></div>';

    // ---------- التعهّد ----------
    // ⚠️ نفس البطاقة والورقة المطبوعة بتاعة شاشة تفاصيل الطلب بالحرف.
    var pledgeCard = NuhPledgeDoc.card({
      requestNumber: file.selectedRequestNumber,
      student: s,
      pledge: file.pledge
    });

    // ⚠️ ورقة الملف الكامل: نفس ورقة التعهّد بأقسام بيانات قبلها. مخفية على
    //    الشاشة (‎.pl-sheet‎) وبتظهر وقت الطباعة بس - الشاشة ليها بطاقاتها فوق.
    // ⚠️ عناوين الورقة عربية دايمًا مهما كانت لغة الواجهة - نفس سبب الوثيقة
    //    نفسها: النصّ المُلزِم عربي (الشرح في js/pledge-doc.js). الشاشة فوق
    //    بتترجم عادي.
    var fileSheet = NuhPledgeDoc.sheet(
      { requestNumber: file.selectedRequestNumber, student: s, pledge: file.pledge },
      file.pledge,
      {
        id: 'studentFileSheet',
        title: 'ملف الطالب',
        sections: [
          {
            title: 'البيانات الشخصية',
            rows: [
              { k: 'الاسم بالعربية', v: dash(s.full_name) },
              { k: 'الاسم بالإنجليزية', v: dash(s.full_name_english) },
              { k: 'الرقم الجامعي', v: dash(s.student_id) },
              { k: 'رقم الهوية', v: dash(s.national_id) },
              { k: 'الجوال', v: dash(s.phone) }
            ]
          },
          {
            title: 'البيانات الأكاديمية والإسكان',
            rows: [
              { k: 'الكلية', v: escHtml(collegeName(s.college)) },
              { k: 'القسم', v: escHtml(deptName(s.department)) },
              { k: 'المستوى', v: escHtml(levelName(s.academic_level)) },
              { k: 'السكن', v: housing.length ? housing.join(' · ') : '-' }
            ]
          },
          {
            title: 'حساب الشبكة',
            rows: ad ? [
              { k: 'اسم المستخدم', v: dash(ad.username) },
              file.adUnavailable ? null : { k: 'حالة الحساب',
                v: ad.enabled ? 'مفعّل' : 'معطّل' },
              { k: 'آخر دخول للشبكة',
                v: file.adUnavailable
                     ? 'تعذّر الوصول للـAD'
                     : (ad.lastLogonAt ? NuhFmt.dateTime(ad.lastLogonAt)
                                       : 'لم يسجّل دخولًا') }
            ] : [ { k: 'حساب الشبكة',
                    v: 'لا يوجد حساب شبكة مرتبط بهذا الطالب' } ]
          }
        ]
      });

    result.innerHTML = head + reqCard + personal + academic + adCard + pledgeCard + fileSheet;

    // تبديل الطلب المعروض
    result.querySelectorAll('.sf-req').forEach(function (el) {
      el.addEventListener('click', function () {
        var id = parseInt(el.getAttribute('data-req'), 10);
        if (id && id !== FILE.selectedRequestId) load(input.value, id);
      });
    });
  }

  // ============================================================================
  //  البحث في الرابط.
  //
  //  ⚠️ كان الملف عايش في ذاكرة الصفحة وبس: أي تحديث (F5) - أو رجوع بزرار
  //     المتصفح بعد ما تفتح الطلب - بيرجّع الشاشة فاضية والموظف يكتب الرقم من
  //     أول وجديد. ودي شاشة الموظف بيفتح فيها ملف ويطبع منه ويقارن، فالتحديث
  //     وارد جدًا.
  //
  //  ⚠️ والرابط بقى كمان **قابل للمشاركة والحفظ**: /StudentFile?q=487799810
  //     بيفتح نفس الملف مباشرة - مفيد لمّا يتبعت في محضر أو يتحفظ في مفضّلة.
  //     والفتح من الرابط بيتسجّل في سجل العمليات زي أي فتح تاني، لأن التسجيل
  //     على الخادم مش عند الضغط على زرار البحث.
  //
  //  ⚠️ replaceState لا pushState: كل بحث مايضيفش خطوة في تاريخ المتصفح، وإلا
  //     زرار الرجوع بيلفّ الموظف على كل رقم كتبه بدل ما يرجّعه للشاشة اللي جه
  //     منها.
  // ============================================================================
  //  ⚠️ الكتابة من NuhUrl (js/url-state.js): الشاشة دي كانت أول واحدة تحفظ
  //     حالتها في الرابط، وبعدها بقت كل الشاشات تعمل نفس الحاجة - فالمنطق
  //     اتنقل لمكان واحد بدل ما يتكرّر في تسع شاشات.
  function syncUrl(q, requestId) {
    NuhUrl.sync({ q: q, requestId: requestId }, {});
  }

  // ---------------------------------------------------------------- التحميل
  async function load(q, requestId) {
    showAlert('');
    btn.disabled = true;
    try {
      var url = '/api/StudentFile?q=' + encodeURIComponent(q) +
                (requestId ? '&requestId=' + requestId : '');
      var res = await fetch(url, { headers: authHeaders() });

      if (res.status === 401) { window.location.replace('/Account/Login'); return; }
      if (res.status === 403) {
        result.innerHTML = '';
        showAlert(tf('sf_forbidden', 'لا تملك صلاحية فتح ملف الطالب.', 'You do not have permission to open student files.'));
        return;
      }

      var data = null;
      try { data = await res.json(); } catch (e) { data = null; }

      if (!res.ok) {
        result.innerHTML = '';
        showAlert((data && data.message) ||
          tf('sf_notFound', 'غير مسجَّل في السكن الجامعي.', 'Not registered in university housing.'));
        return;
      }
      render(data);
      syncUrl(q, data.selectedRequestId);
    } catch (e) {
      result.innerHTML = '';
      showAlert(tf('sf_netError', 'تعذّر الاتصال بالخادم.', 'Could not reach the server.'));
    } finally {
      btn.disabled = false;
    }
  }

  // ---------------------------------------------------------------- الإرسال
  // فتح الشاشة برابط فيه بحث - نفس مسار زرار البحث بالظبط.
  (function initFromUrl() {
    var q = '';
    try { q = new URL(window.location.href).searchParams.get('q') || ''; } catch (e) { q = ''; }
    q = q.trim();
    if (!q) return;
    input.value = q;
    if (!(NuhId.isStudentId(q) || NuhId.isNationalId(q))) { syncUrl('', null); return; }
    var rid = 0;
    try { rid = parseInt(new URL(window.location.href).searchParams.get('requestId') || '0', 10) || 0; } catch (e) { rid = 0; }
    load(q, rid || null);
  })();

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var q = input.value.trim();

    // ⚠️ الفحص بنفس قاعدة الخادم بالحرف (NuhId متولّدة من Core/IdentityRules):
    //    الشاشة بتوفّر على الموظف نداء هيترفض، والخادم هو اللي بيرفض فعلًا.
    var ok = NuhId.isStudentId(q) || NuhId.isNationalId(q);
    input.classList.toggle('error', !ok);
    if (!ok) {
      showAlert(tf('sf_badQuery',
        'اكتب رقمًا جامعيًا (٩ أرقام تبدأ بـ ٤) أو رقم هوية (١٠ أرقام).',
        'Enter a student ID (9 digits starting with 4) or a national ID (10 digits).'));
      return;
    }
    load(q, null);
  });

  input.addEventListener('input', function () {
    input.classList.remove('error');
    showAlert('');
  });
})();
