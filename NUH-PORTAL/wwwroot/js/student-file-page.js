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
  var inputBox = document.getElementById('sfInput');
  var detect = document.getElementById('sfDetect');
  var btn = document.getElementById('sfBtn');
  var alertBox = document.getElementById('sfAlert');
  var result = document.getElementById('sfResult');

  var FILE = null;   // آخر ملف اتحمّل

  // ==========================================================================
  //  تمييز نوع المدخل: رقم جامعي / رقم هوية / رمز تحقّق.
  //
  //  ⚠️ الصيغ التلاتة مالهاش أي تداخل، فالموظف مابيختارش نوعًا قبل البحث:
  //     الرمز وحده هو اللي فيه حروف، والرقم الجامعي ٩ أرقام تبدأ بـ ٤،
  //     والهوية ١٠ أرقام. والقواعد كلها متولّدة من الخادم (NuhId من
  //     Core/IdentityRules، وNuhPledge من Core/PledgeRules) - مفيش قاعدة
  //     مكتوبة هنا تقدر تفارق اللي الخادم بيفحص بيه.
  //
  //  ⚠️ والسقف مقصود: من غيره الموظف بيكتب أرقامًا بلا نهاية ويستنى ردًّا
  //     مرفوضًا. ١٠ أرقام بتغطّي الهوية والرقم الجامعي، والرمز ١٢ خانة.
  // ==========================================================================
  var MAX_DIGITS = 10;

  function kindOf(v) {
    var raw = String(v || '').trim();
    if (!raw) return { kind: '' };

    if (NuhPledge.looksLikeCode(raw)) {
      return { kind: 'code', ready: NuhPledge.codeComplete(raw) };
    }
    var d = raw.replace(/\D/g, '');
    if (NuhId.isStudentId(d)) return { kind: 'sid', ready: true };
    if (NuhId.isNationalId(d)) return { kind: 'nid', ready: true };
    return { kind: 'partial', digits: d.length };
  }

  // ⚠️ القناع بيكتب في الحقل وهو بيكتب: حروف كبيرة وشرطات كل أربعة للرمز،
  //    وأرقام بس بسقف للباقي. الشرطات بتزيد طول النصّ، فموضع المؤشّر بيتحسب
  //    بعدد الخانات المحفوظة قبله لا بموضعه الخام - من غير كده بيقفز خانة
  //    كل ما شرطة تتزاد والكتابة بتبقى غير محتملة.
  function mask() {
    var before = input.value.slice(0, input.selectionStart || 0);
    var raw = input.value;
    var out, kept, pos;

    if (NuhPledge.looksLikeCode(raw)) {
      kept = NuhPledge.codeNormalize(before).length;
      out = NuhPledge.codeFormat(raw);
      pos = kept + Math.max(0, Math.floor((kept - 1) / NuhPledge.CODE.GROUP));
      if (out !== raw) { input.value = out; try { input.setSelectionRange(pos, pos); } catch (e) { } }
      return;
    }

    out = raw.replace(/\D/g, '').slice(0, MAX_DIGITS);
    if (out !== raw) {
      kept = Math.min(before.replace(/\D/g, '').length, MAX_DIGITS);
      input.value = out;
      try { input.setSelectionRange(kept, kept); } catch (e) { }
    }
  }

  var KIND_TEXT = {
    sid:  ['sid',  ['sf_kindStudentId',  'رقم جامعي', 'Student ID'],
                   ['sf_kindOpensFile',  'يفتح ملف الطالب', 'Opens the student file']],
    nid:  ['nid',  ['sf_kindNationalId', 'رقم هوية', 'National ID'],
                   ['sf_kindOpensFile',  'يفتح ملف الطالب', 'Opens the student file']],
    code: ['code', ['sf_kindDocCode',    'رمز تحقّق', 'Verification code'],
                   ['sf_kindVerifies',   'يتحقّق من الوثيقة ثم يفتح ملفها', 'Verifies the document, then opens its file']]
  };

  function paintDetect() {
    var r = kindOf(input.value);
    inputBox.classList.toggle('is-code', r.kind === 'code');
    inputBox.classList.toggle('is-num', r.kind === 'sid' || r.kind === 'nid' || r.kind === 'partial');

    var t = KIND_TEXT[r.kind];
    if (!t) { detect.innerHTML = ''; return; }

    detect.innerHTML =
      '<span class="sf-chip ' + t[0] + '">' +
        '<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>' +
        tf(t[1][0], t[1][1], t[1][2]) + '</span>' +
      '<span class="say">' + tf(t[2][0], t[2][1], t[2][2]) + '</span>' +
      (r.ready ? '' : '<span class="say">' +
        tf('sf_kindIncomplete', '(غير مكتمل)', '(incomplete)') + '</span>');
  }

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
  // ⚠️ بيظهر بس لمّا يكون الوصول للملف بـ**رمز**: الملف واحد في الحالتين،
  //    واللي بيفرق إن الرمز بيجاوب سؤالًا زيادة - «الورقة اللي في إيدي صادرة
  //    عن النظام؟». في البحث بالرقم مفيش سؤال كده، فمفيش شريط.
  function verifiedBar(v) {
    if (!v) return '';
    return '<div class="sf-verified">' +
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>' +
      '<span>' + tf('sf_verifiedTitle', 'وثيقة صادرة عن النظام', 'Document issued by the system') +
        '<span class="sub">' +
          tf('sf_verifiedCode', 'الرمز', 'Code') + ' ' + escHtml(v.code || '') + ' · ' +
          tf('sf_verifiedReq', 'الطلب', 'Request') + ' ' + escNum(v.requestNumber) + ' · ' +
          tf('sf_verifiedSigned', 'وُقّع', 'Signed') + ' ' + NuhFmt.dateTime(v.acceptedAt) + ' · ' +
          tf('sf_verifiedVersion', 'نسخة البنود', 'Terms version') + ' ' + escHtml(v.policyVersion || '-') +
        '</span></span></div>';
  }

  function render(file) {
    FILE = file;
    var s = file.student || {};

    var deletedTag = file.isDeleted
      ? ' <span class="badge badge-rejected">' + tf('sf_deleted', 'سجل محذوف', 'Deleted record') + '</span>'
      : '';

    var head =
      verifiedBar(file.verified) +
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
            ' &nbsp;·&nbsp; ' + tf('rdp_field_nationalId', 'الهوية الوطنية / الإقامة', 'National ID / Iqama') + ' ' + dash(s.national_id) + '</p>' +
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
          escNum(r.requestNumber) + '</span>' +
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
      field(tf('rdp_field_nationalId', 'الهوية الوطنية / الإقامة', 'National ID / Iqama'), dash(s.national_id), true) +
      field(tf('rdp_field_mobile', 'الجوال', 'Mobile'), dash(s.phone), true) +
      '</div></div></div>';

    // ⚠️ نصوص الورقة المطبوعة من Resources/SharedResource بمفاتيح doc_* -
    //    والقيمة عربية في ملفّي الترجمة الاتنين، لأن الوثيقة عربية دايمًا.
    //    الشرح الكامل في js/pledge-doc.js عند tdoc().
    function tdoc(key, fallback) {
      try { var v = t(key); if (v && v !== key) return v; } catch (e) { }
      return fallback;
    }

    // ---------- الأكاديمية والسكن ----------
    // ⚠️ نوع السكن مشتقّ من جنس الطالب لا حقل مستقلّ في قاعدة البيانات:
    //    الإسكان مفصول بالجنس أصلًا (Core/GenderScope)، فحقل تاني للنوع
    //    معناه معلومة واحدة في مكانين ممكن يفترقوا. ودي نفس الدالة اللي
    //    بتقرا منها الشاشة والورقة المطبوعة - مكان واحد.
    function housingTypeLabel() {
      return ((s.gender || '') + '').toLowerCase() === 'female'
        ? tf('sf_housingFemale', 'سكن الطالبات', "Women's housing")
        : tf('sf_housingMale', 'سكن الطلاب', "Men's housing");
    }

    // ⚠️ الدور والشقة والغرفة من housingUnitText في i18n.js - وهي الدالة
    //    نفسها التي تقرأ منها قائمة الطلاب وشاشة تفاصيل الطلب. كانت مكتوبة
    //    هنا يدويًا، وأسقطت النسخة اليدوية **الدور**: فتعرض الشاشة والوثيقة
    //    المطبوعة «مبنى 66 · شقة 5 · غرفة 17» بينما يعرض الطالب نفسه في
    //    شاشة الطلبات «مبنى 66 · الدور 1 · شقة 5 · غرفة 17». وفي ترقيم سكن
    //    الطالبات - حيث تبدأ الشقق من ١ في كل دور - لا تدلّ الوثيقة بغير
    //    الدور على غرفة بعينها أصلًا.
    var housing = [];
    if (s.housing_building) housing.push(escHtml(buildingName(s.housing_building)));
    var __unit = housingUnitText(s);
    if (__unit) housing.push(escHtml(__unit));

    var academic = '<div class="card"><div class="card-header">' +
      '<div class="ch-title">' +
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>' +
      '<span>' + tf('sf_cardAcademic', 'البيانات الأكاديمية والإسكان', 'Academic and housing') + '</span>' +
      '</div></div><div class="card-body"><div class="info-grid">' +
      field(tf('rdp_field_college', 'الكلية', 'College'), escHtml(collegeName(s.college))) +
      field(tf('rdp_field_department', 'القسم', 'Department'), escHtml(deptName(s.department))) +
      field(tf('rdp_field_level', 'المستوى', 'Level'), escHtml(levelName(s.academic_level))) +
      field(tf('sf_housingType', 'نوع السكن', 'Housing type'), housingTypeLabel()) +
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
    // ⚠️ full: true - دي شاشة التحقيق، فهي وحدها اللي بتعرض بصمة البنود
    //    والنسخة وعدد البنود وعنوان الجهاز. شاشة الطلب بتعرض الرمز بس،
    //    عشان القيمة الواحدة تفضل معروضة في مكان واحد (الشرح في
    //    js/pledge-doc.js عند تعريف card).
    var pledgeCard = NuhPledgeDoc.card({
      requestNumber: file.selectedRequestNumber,
      student: s,
      pledge: file.pledge
    }, { full: true });

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
        title: tdoc('doc_title_studentFile', 'ملف الطالب'),
        // ⚠️ أربعة أقسام لا تلاتة، وبتترسم في عمودين (pl-sh-grid):
        //    الشخصية جنب الأكاديمية، والإسكان جنب حساب الشبكة. القسمين
        //    الأولانيين أربع خانات لكل واحد والتانيين تلاتة، فالصفّين
        //    متساويين والورقة بتدخل في ورقة واحدة بدل ورقتين.
        //
        //    ⚠️ والعناوين هنا عربية مكتوبة مش من ملفات الترجمة: الورقة
        //       المطبوعة عربية دايمًا مهما كانت لغة الواجهة (الشرح في
        //       js/pledge-doc.js). الشاشة فوق هي اللي بتترجم.
        sections: [
          {
            title: tdoc('doc_secPersonal', 'البيانات الشخصية'),
            rows: [
              { k: tdoc('doc_fNameAr', 'الاسم بالعربية'), v: dash(s.full_name) },
              // ⚠️ wide: الاسم بالإنجليزية أطول قيمة في الورقة، ومن غيرها
              //    بيلفّ على سطرين جوّه عمود ضيّق.
              { k: tdoc('doc_fNameEn', 'الاسم بالإنجليزية'), v: dash(s.full_name_english), wide: true },
              { k: tdoc('doc_fNationalId', 'الهوية الوطنية / الإقامة'), v: dash(s.national_id) },
              { k: tdoc('doc_fMobile', 'الجوال'), v: dash(s.phone) }
            ]
          },
          {
            title: tdoc('doc_secAcademic', 'البيانات الأكاديمية'),
            rows: [
              // ⚠️ الرقم الجامعي مكانه هنا لا في البيانات الشخصية: هو رقم
              //    **قيد أكاديمي** تصدره الجامعة، مش هوية شخصية.
              { k: tdoc('doc_fStudentId', 'الرقم الجامعي'), v: dash(s.student_id) },
              { k: tdoc('doc_fCollege', 'الكلية'), v: escHtml(collegeName(s.college)) },
              { k: tdoc('doc_fDept', 'القسم'), v: escHtml(deptName(s.department)) },
              { k: tdoc('doc_fLevel', 'المستوى'), v: escHtml(levelName(s.academic_level)) }
            ]
          },
          {
            title: tdoc('doc_secHousing', 'الإسكان الجامعي'),
            rows: [
              { k: tdoc('doc_fHousingType', 'نوع السكن'), v: housingTypeLabel() },
              { k: tdoc('doc_fHousing', 'السكن'), v: housing.length ? housing.join(' · ') : '-' }
            ]
          },
          {
            title: tdoc('doc_secAd', 'حساب شبكة الإسكان'),
            rows: ad ? [
              { k: tdoc('doc_fAdUser', 'اسم المستخدم'), v: dash(ad.username) },
              file.adUnavailable ? null : { k: tdoc('doc_fAdState', 'حالة الحساب'),
                v: ad.enabled ? 'مفعّل' : 'معطّل' },
              { k: tdoc('doc_fAdLast', 'آخر دخول للشبكة'),
                v: file.adUnavailable
                     ? 'تعذّر الوصول للـAD'
                     : (ad.lastLogonAt ? NuhFmt.dateTime(ad.lastLogonAt)
                                       : 'لم يسجّل دخولًا') }
            ] : [ { k: tdoc('doc_secAd', 'حساب شبكة الإسكان'),
                    v: tdoc('doc_noAd', 'لا يوجد حساب شبكة مرتبط بهذا الطالب') } ]
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
    mask(); paintDetect();
    // ⚠️ الرمز مقبول هنا زي الرقم: صفحة /Verify بتحوّل الموظف المسجّل على
    //    الشاشة دي والرمز في الرابط، فلو رفضناه كان هيقف على شاشة فاضية.
    var k = kindOf(q);
    if (k.kind === 'partial' || !k.ready) { syncUrl('', null); return; }
    var rid = 0;
    try { rid = parseInt(new URL(window.location.href).searchParams.get('requestId') || '0', 10) || 0; } catch (e) { rid = 0; }
    load(q, rid || null);
  })();

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var q = input.value.trim();

    // ⚠️ الفحص بنفس قواعد الخادم بالحرف (NuhId من Core/IdentityRules،
    //    وNuhPledge من Core/PledgeRules): الشاشة بتوفّر على الموظف نداء
    //    هيترفض، والخادم هو اللي بيرفض فعلًا.
    var k = kindOf(q);
    var ok = k.ready === true;
    inputBox.classList.toggle('error', !ok);
    if (!ok) {
      showAlert(tf('sf_badQuery',
        'اكتب رقمًا جامعيًا (٩ أرقام تبدأ بـ ٤) أو رقم هوية (١٠ أرقام) أو رمز التحقّق المطبوع على الوثيقة.',
        'Enter a student ID (9 digits starting with 4), a national ID (10 digits), or the verification code printed on the document.'));
      return;
    }
    load(q, null);
  });

  input.addEventListener('input', function () {
    inputBox.classList.remove('error');
    showAlert('');
    mask();
    paintDetect();
  });
  input.addEventListener('paste', function () { setTimeout(function () { mask(); paintDetect(); }, 0); });
  paintDetect();
})();
