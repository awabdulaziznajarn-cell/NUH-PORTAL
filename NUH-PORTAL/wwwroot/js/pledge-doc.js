// ============================================================================
//  وثيقة التعهّد - العرض على الشاشة، والورقة المطبوعة، والطباعة نفسها.
//
//  ⚠️ الوحدة دي بتخدم شاشتين: تفاصيل الطلب، وملف الطالب (الأمن السيبراني).
//     الاتنين بيعرضوا **نفس** الوثيقة وبيطبعوها بنفس الورقة. لو الكود اتكتب
//     في الشاشتين، كانت هتبقى نسختين لوثيقة رسمية - وأول تعديل في واحدة
//     بيخلّي الورقة المطبوعة من شاشة تختلف عن التانية لنفس الطالب.
//
//  ⚠️ والوحدة بتعتمد على: escHtml (js/esc.js) و NuhFmt (js/date-format.js)
//     و tf() من الشاشة. أي شاشة بتحمّلها لازم تحمّلهم قبلها.
//
//  الشكل كله في css/components.css تحت .pl-* - ومعاه قواعد الطباعة.
// ============================================================================
var NuhPledgeDoc = (function () {
  'use strict';

  // ============================================================================
  //  وثيقة التعهّد — البنود المجمَّدة والجملة المكتوبة بخطّ الطالب.
  //
  //  ⚠️ البنود المعروضة هنا هي **المحفوظة وقت الموافقة**، مش اللي في شاشة
  //     القوائم المرجعية دلوقتي. ودي كل الفكرة: المدير بيعدّل البنود في أي وقت،
  //     فاللي المراجع بيشوفه في الشاشة دي ممكن يكون بند الطالب أصلًا ماشافهوش.
  //
  //  ⚠️ والشريط اللي فوق (مطابق / تغيّرت) بيتبني من pledge.termsChanged اللي
  //     الخادم بيحسبها بمقارنة البصمة المحفوظة ببصمة البنود الحالية
  //     (Services/PledgeService.cs). الواجهة مابتحسبش حاجة.
  //
  //  الشكل كله في css/components.css تحت .pl-* — ومعاه قواعد الطباعة.
  // ============================================================================

  // اسم الطالب ورقمه وبصمة بنوده، مكرّرين ومايلين على الوثيقة.
  // ⚠️ ردع بصري لا حماية: أي صورة أو نسخة — حتى مقصوصة — بتفضل شايلة اسم
  //    صاحبها وبصمة بنوده. الحماية الحقيقية هي البصمة في قاعدة البيانات.
  function tile(student, hash) {
    var parts = [];
    if (student.full_name) parts.push(student.full_name);
    if (student.student_id) parts.push(student.student_id);
    if (hash) parts.push(String(hash).slice(0, 8));
    var label = escHtml(parts.join(' · '));
    if (!label) return '';
    var unit = '<span class="pl-tile-unit">' + label + '</span>';
    var row = '<div class="pl-tile-row">' + unit + unit + unit + '</div>';
    var rows = '';
    for (var i = 0; i < 7; i++) rows += row;
    // ⚠️ طبقتين: الخارجية بتقصّ (overflow) والداخلية هي المايلة والأكبر من
    //    الورقة. قبل كده القصّ كان على الورقة نفسها - و overflow:hidden على
    //    عنصر بيتقسم على أكتر من صفحة بيخلّي المتصفح **يقصّه** بدل ما يقسّمه،
    //    فملف الطالب كان بيتطبع صفحة واحدة والتوقيع مقصوص.
    return '<div class="pl-tile" aria-hidden="true"><div class="pl-tile-in">' +
           rows + '</div></div>';
  }

  // البصمة في مجموعات رباعية — أسهل في المقارنة بالعين وأصعب في الغلط.
  function hashText(hash, len) {
    if (!hash) return '-';
    return String(hash).slice(0, len || 16).replace(/(.{4})/g, '$1 ').trim();
  }

  function card(r) {
    var p = r.pledge;
    var s = r.student || {};
    // ⚠️ .ch-title بتلمّ الأيقونة والعنوان في عنصر واحد - من غيرها
    //    justify-content:space-between بتاعة .card-header بتبعد العنوان
    //    للطرف التاني (الشرح في css/components.css).
    var head = '<div class="card"><div class="card-header">' +
      '<div class="ch-title">' +
        '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="M9 12l2 2 4-4"/></svg>' +
        '<span>' + tf('rdp_card_pledge', 'التعهّد والإقرار', 'Pledge & Declaration') + '</span>' +
      '</div>' +
      '</div><div class="card-body" style="padding:18px">';

    // ⚠️ الحالتين دول مش نفس الحاجة: الأولى مفيش تعهّد أصلًا، والتانية فيه
    //    تعهّد بس بلا نصّ محفوظ (اتوقّع قبل تفعيل التوثيق). خلطهم بيخلّي
    //    المراجع يفتكر إن الطالب ما وقّعش وهو وقّع.
    if (!p) {
      return head + '<div class="pl-doc"><div class="pl-empty">' +
        '<svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/></svg>' +
        '<div>' + tf('rdp_pledge_none', 'لا يوجد تعهّد موثّق لهذا الطلب', 'No documented pledge for this request') +
        '<br><span style="font-size:12px">' + tf('rdp_pledge_noneWhy', 'قُدِّم قبل تفعيل توثيق التعهّد في النظام', 'Submitted before pledge documentation was enabled') +
        '</span></div></div></div></div></div>';
    }

    if (!p.documented) {
      return head + '<div class="pl-doc"><div class="pl-empty">' +
        '<svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/></svg>' +
        '<div>' + tf('rdp_pledge_undocumented', 'وافق الطالب على التعهّد، دون حفظ نصّ البنود', 'The student accepted the pledge, without a stored copy of the terms') +
        '<br><span style="font-size:12px">' + NuhFmt.dateTime(p.acceptedAt) + '</span></div></div></div></div></div>';
    }

    var changed = !!p.termsChanged;
    var chip = changed
      ? '<span class="pl-chip warn"><svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v4"/><path d="M12 17h.01"/><path d="M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/></svg>' +
          tf('rdp_pledge_changed', 'البنود تغيّرت بعد التوقيع', 'Terms changed after signing') + '</span>'
      : '<span class="pl-chip ok"><svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>' +
          tf('rdp_pledge_match', 'مطابق للبنود الحالية', 'Matches current terms') + '</span>';

    var note = changed
      ? '<div class="pl-note"><svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></svg><span>' +
        tf('rdp_pledge_changedNote',
           'البنود في شاشة القوائم المرجعية بقت مختلفة عن دي. المُلزِم لهذا الطلب هو النصّ المحفوظ أدناه، لا النصّ الحالي.',
           'The terms in the lookups screen now differ from these. What binds this request is the stored text below, not the current text.') +
        '</span></div>'
      : '';

    var terms = (p.terms || []).map(function (line) {
      return '<li>' + escHtml(line) + '</li>';
    }).join('');

    var who = [];
    if (s.full_name) who.push(escHtml(s.full_name));
    if (s.student_id) who.push(tf('rdp_field_studentId', 'الرقم الجامعي', 'Student ID') + ' <b>' + escHtml(s.student_id) + '</b>');

    return head +
      '<div class="pl-doc" id="pledgeDoc">' +
        tile(s, p.termsHash) +
        '<div class="pl-seal" aria-hidden="true"><img src="/nu-logo.svg" alt="">' +
          '<span class="pl-seal-txt">' + tf('rdp_pledge_sealTop', 'تعهّد موثّق', 'Verified pledge') +
          ' · ' + escHtml(p.policyVersion || '') + '</span></div>' +

        '<div class="pl-top">' +
          '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="var(--navy)" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/><path d="M9 15l2 2 4-4"/></svg>' +
          '<div><div class="pl-ttl">' + tf('rdp_pledge_docTitle', 'وثيقة التعهّد', 'Pledge document') + '</div>' +
          '<div class="pl-sub">' + tf('rdp_pledge_docSub',
              'النصّ كما وافق عليه الطالب - محفوظ ولا يتغيّر بتعديل البنود لاحقًا',
              'The text as accepted by the student - stored and unaffected by later edits') + '</div></div>' +
          chip +
          '<button type="button" class="pl-print-btn" onclick="printPledge()">' +
            '<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><path d="M6 14h12v8H6z"/></svg>' +
            tf('rdp_pledge_print', 'طباعة الوثيقة', 'Print document') + '</button>' +
        '</div>' +
        note +
        '<div class="pl-body">' +
          '<div class="pl-cap">' + tf('rdp_pledge_termsCap', 'البنود المعروضة وقت الموافقة', 'Terms shown at the time of acceptance') + '</div>' +
          '<ol class="pl-list">' + terms + '</ol>' +
          '<div class="pl-sig">' +
            '<div class="pl-lbl">' + tf('rdp_pledge_typedLbl', 'الإقرار المكتوب بخطّ الطالب', 'Declaration typed by the student') + '</div>' +
            '<div class="pl-txt">«' + escHtml(p.typedConfirmation || '') + '»</div>' +
            '<div class="pl-who">' + who.join(' &nbsp;·&nbsp; ') + '<br>' + NuhFmt.dateTime(p.acceptedAt) + '</div>' +
          '</div>' +
        '</div>' +
        '<div class="pl-meta">' +
          '<div>' + tf('rdp_pledge_hash', 'بصمة البنود', 'Terms fingerprint') + '<b class="pl-mono">' + escHtml(hashText(p.termsHash)) + '</b></div>' +
          '<div>' + tf('rdp_pledge_version', 'النسخة', 'Version') + '<b class="pl-mono">' + escHtml(p.policyVersion || '-') + '</b></div>' +
          '<div>' + tf('rdp_pledge_count', 'عدد البنود', 'Terms count') + '<b>' + (p.terms || []).length + '</b></div>' +
          '<div>' + tf('rdp_pledge_ip', 'عنوان الجهاز', 'Device address') + '<b class="pl-mono">' + escHtml(p.ipAddress || '-') + '</b></div>' +
        '</div>' +

      '</div>' + sheet(r, p) + '</div></div>';
  }

  // ============================================================================
  //  ورقة التعهّد المطبوعة - تخطيط مستقل عن بطاقة الشاشة.
  //
  //  ⚠️ ليه تخطيط تاني بدل ما نطبع البطاقة نفسها:
  //     اللي على الشاشة **واجهة استخدام**: شارة حالة خضرا، ترويسة بطاقة،
  //     خلفيات ملوّنة، حواف مدوّرة، أرقام في دواير. الحاجات دي بتقول للموظف
  //     «إنت في نظام». ونفس الحاجات مطبوعة بتقول للي ماسك الورقة «دي صورة
  //     شاشة» - مش وثيقة رسمية.
  //
  //     فالورقة ليها تخطيطها: ترويسة جهة، عنوان بخط ذهبي، ديباجة «أقر أنا
  //     الموقّع أدناه»، بنود بترقيم عربي، صندوق توقيع بختم، وتذييل فيه البصمة
  //     وطريقة التحقّق.
  //
  //  ⚠️ والاتنين بيتبنوا من **نفس البيانات** (نفس الـ p): مفيش نصّ مكتوب
  //     مرتين، الاختلاف في العرض بس. لو البيانات اتغيّرت، الاتنين بيتغيّروا.
  //
  //  الشكل كله في css/components.css تحت .pl-sh-* .
  // ============================================================================
  // ⚠️ opts بتخلّي نفس الورقة تخدم غرضين من غير نسخة تانية:
  //      • بلا opts  = وثيقة التعهّد وبس (شاشة تفاصيل الطلب)
  //      • opts.sections = ملف الطالب كامل: بياناته وطلبه **وبعدين** التعهّد
  //    الترويسة والختم والعلامة المتكرّرة والتذييل واحدة في الحالتين - لأنها
  //    نفس الوثيقة الرسمية، اللي بيتغيّر هو اللي جوّاها.
  // ============================================================================
  //  ترويسة المعاملات الرسمية.
  //
  //  ⚠️ الشكل ده مش اختيار تصميم - ده نموذج الترويسة المعتمد في مراسلات
  //     الجامعة: ثلاثة أعمدة، الجهة يمين والشعار في النص وخانات القيد شمال.
  //     الوثيقة اللي طالعة من النظام لازم تبقى نفس شكل الورق اللي بيتداول
  //     في الجامعة، وإلا اللي بيستلمها بيتعامل معاها كمطبوعة من موقع.
  //
  //  ⚠️ اسم الإدارة بيتغيّر حسب اللي داخل: المشرف «إدارة الإسكان الجامعي»،
  //     والأمن السيبراني «إدارة الأمن السيبراني»، والأدمن «إدارة تقنية
  //     المعلومات». بيتحسب في السيرفر (NUH.printDept في Views/Shared/_Layout)
  //     لأن الأدوار كليمات على الكوكي والواجهة ماتقراهاش.
  //
  //  ⚠️ رقم الطلب وتاريخ اليوم بيتقروا من النظام ومايتعدّلوش. خانة
  //     «المرفقات» بس هي اللي الموظف بيكتبها قبل ما يدوس طباعة - **وماتتخزنش**،
  //     لأن عدد المرفقات اللي هيتبعت مع المعاملة الورقية قرار الديوان لا قرار
  //     النظام.
  // ============================================================================
  // ⚠️ بترجّع فاضي لو NUH.printDept مش موجودة، مش اسم إدارة افتراضي.
  //    السبب: اسم إدارة **غلط** على وثيقة رسمية موقّعة أسوأ من سطر ناقص.
  //    لو الاسم غاب، اللي بيطبع بيلاحظ ويسأل. لو طلع اسم تاني، الورقة بتخرج
  //    منسوبة لجهة مالهاش علاقة ومحدّش بياخد باله.
  //    والأسماء نفسها في Resources/SharedResource (doc_dept*) - مكان واحد.
  function dept() {
    try { if (window.NUH && NUH.printDept) return NUH.printDept; } catch (e) { }
    return '';
  }

  // ⚠️ التقويم أم القرى من المتصفح لا بحساب تقريبي: التحويل الحسابي (تقسيم
  //    على ٣٥٤٫٣٧) بيفرق يوم أو اتنين عن التقويم الرسمي، ووثيقة رسمية بتاريخ
  //    غلط بيوم أسوأ من وثيقة بخانة فاضية. Intl فيه أم القرى نفسه.
  function todayHijri() {
    try {
      var f = new Intl.DateTimeFormat('ar-SA-u-ca-islamic-umalqura-nu-latn',
        { day: '2-digit', month: '2-digit', year: 'numeric' });
      var out = {};
      f.formatToParts(new Date()).forEach(function (x) { out[x.type] = x.value; });
      if (out.day && out.month && out.year) return [out.day, out.month, String(out.year).replace(/[^0-9]/g, '')];
    } catch (e) { }
    return ['', '', ''];
  }
  function todayGreg() {
    var d = new Date();
    function z(n) { return (n < 10 ? '0' : '') + n; }
    return [z(d.getDate()), z(d.getMonth() + 1), String(d.getFullYear())];
  }

  // ⚠️ خانتين مختلفتين لا واحدة، والفرق مقصود:
  //
  //      fld()  = فراغ الموظف بيملاه بإيده (المرفقات). خطّ منقّط على الشاشة
  //               يقول «اكتب هنا»، وcontenteditable.
  //      val()  = قيمة **النظام عارفها** (رقم الطلب، تاريخ اليوم). بتتعرض
  //               وبس - لا contenteditable ولا خطّ منقّط.
  //
  //    الرقم والتاريخ كانوا قابلين للكتابة، وde كان غلط: الرقم عندنا في
  //    قاعدة البيانات والتاريخ عند النظام - وخانة مفتوحة على قيمة النظام
  //    عارفها معناها إن الورقة ممكن تخرج برقم طلب مايخصّهاش أو بتاريخ
  //    مش تاريخ الطباعة، من غير ما حد يلاحظ. الورقة دي بتتحفظ في ملف.
  //
  //    ⚠️ ولسه فيه اختيار التقويم (هـ/م) - ده اختيار **عرض** لنفس اليوم،
  //       مش تعديل للقيمة.
  function fld(cls, txt, w) {
    return '<span class="pl-lh-f ' + cls + '" contenteditable="true" spellcheck="false"' +
           (w ? ' style="min-width:' + w + '"' : '') + '>' + escHtml(txt || '') + '</span>';
  }
  function val(cls, txt, w) {
    return '<span class="pl-lh-v ' + cls + '"' +
           (w ? ' style="min-width:' + w + '"' : '') + '>' + escHtml(txt || '') + '</span>';
  }

  function letterhead(reqNo) {
    var h = todayHijri();
    return '<div class="pl-lh">' +
      '<div class="pl-lh-org">' +
        '<span>المملكة العربية السعودية</span>' +
        '<span>وزارة التعليم</span>' +
        '<b>جامعة نجران</b>' +
        (dept() ? '<span class="pl-lh-dept">' + escHtml(dept()) + '</span>' : '') +
      '</div>' +
      '<div class="pl-lh-logo"><img src="/nu-logo.svg" alt=""></div>' +
      '<div class="pl-lh-refs">' +
        // ⚠️ «رقم الطلب» لا «الرقم»: ده رقمنا إحنا من قاعدة البيانات، لا رقم
        //    الصادر بتاع الديوان. مقروء لا مكتوب - الرقم مايتغيّرش على ورقة
        //    مستخرجة من النظام.
        '<div class="pl-lh-row"><span class="k">رقم الطلب</span><span class="c">:</span>' +
          val('pl-lh-num', reqNo || '-', '92px') + '</div>' +
        '<div class="pl-lh-row"><span class="k">التاريــخ</span><span class="c">:</span>' +
          '<span class="pl-lh-date">' +
            val('pl-lh-d', h[0], '26px') + '<i>/</i>' + val('pl-lh-m', h[1], '26px') + '<i>/</i>' +
            val('pl-lh-y', h[2], '44px') +
            // ⚠️ زرّ التقويم مايتطبعش (pl-noprint): الورقة بتخرج بالتاريخ
            //    بالتقويم اللي الموظف اختاره، مش بأداة الاختيار.
            '<b class="pl-lh-cal">هـ</b>' +
            '<button type="button" class="pl-lh-swap pl-noprint" title="تبديل التقويم">⇄</button>' +
          '</span>' +
        '</div>' +
        '<div class="pl-lh-row"><span class="k">المرفقات</span><span class="c">:</span>' + fld('pl-lh-att', '', '68px') + '</div>' +
      '</div>' +
    '</div>';
  }

  function sheet(r, p, opts) {
    if (!p || !p.documented) return '';
    var s = r.student || {};
    var o = opts || {};
    var sheetId = o.id || 'pledgeSheet';

    var warn = p.termsChanged
      ? '<div class="pl-sh-warn">' + 'تنبيه: عُدِّلت بنود التعهّد في النظام بعد تاريخ هذا التوقيع. البنود الواردة أدناه هي المُلزِمة لهذا الطلب.' + '</div>'
      : '';

    var terms = (p.terms || []).map(function (line) {
      return '<li>' + escHtml(line) + '</li>';
    }).join('');

    function row(lbl, val) {
      return '<span>' + lbl + '</span><span><b>' + val + '</b></span>';
    }

    // أقسام البيانات (ملف الطالب) - قبل بنود التعهّد.
    var sections = (o.sections || []).map(function (sec) {
      var rows = (sec.rows || []).filter(function (x) { return x; }).map(function (x) {
        return '<div class="pl-sh-row"><span class="k">' + x.k + ':</span>' +
               '<span class="v">' + x.v + '</span></div>';
      }).join('');
      if (!rows) return '';
      return '<div class="pl-sh-sec">' +
        '<div class="pl-sh-sectitle">' + sec.title + '</div>' +
        '<div class="pl-sh-rows">' + rows + '</div></div>';
    }).join('');

    // ⚠️ طبقة .pl-sh-scale جوّه الورقة: هي اللي بتتصغّر وبتشيل الإطار
    //    والمحتوى، والورقة نفسها بياخد ارتفاعها من المحتوى **بعد** التصغير.
    //    كده الورقة بتدخل في الصفحة فعلًا - مفيش تقسيم ولا قصّ. الشرح في print().
    // ⚠️ الورقة عربية دايمًا مهما كانت لغة الواجهة - dir و lang مثبّتين،
    //    والعناوين عربية مش من ملفات الترجمة.
    //
    //    السبب مش تفضيل: النصّ **المُلزِم** عربي. البنود بتتجمّد بالعربي
    //    والبصمة بتتحسب عليه (Core/PledgeRules)، وجملة الإقرار اللي الطالب
    //    كتبها عربية، والختم عربي. لما الواجهة كانت بتقلب الورقة للإنجليزي،
    //    كانت بتطلع وثيقة بعناوين إنجليزية واتجاه من الشمال، وجواها بنود
    //    وجملة إقرار عربية - فالسطور بتتقلب والأرقام بتسيب أماكنها، والنتيجة
    //    ورقة مالهاش شكل وثيقة رسمية بأي لغة.
    //
    //    الترجمة مكانها الشاشة (بطاقة التعهّد فوق) - الوثيقة المطبوعة لأ.
    return '<div class="pl-sheet" id="' + sheetId + '" dir="rtl" lang="ar">' +
      '<div class="pl-sh-scale">' +
      tile(s, p.termsHash) +
      // ⚠️ الإطار الداخلي عنصر حقيقي مش زخرفة: بيدّي الخطّ التاني وركنين من
      //    علامات الأركان الأربعة (الشرح في components.css).
      '<div class="pl-sh-frame">' +

      letterhead(r.requestNumber) +

      '<div class="pl-sh-title">' +
        (o.title || 'وثيقة تعهّد وإقرار') + '</div>' +

      // ⚠️ سطر المراجع اللي كان تحت العنوان اتشال بالكامل، وكان فيه تلات
      //    حاجات كلها اتنقلت لمكانها الصح:
      //      • رقم الطلب     -> خانة «رقم الطلب» في الترويسة فوق
      //      • تاريخ التوقيع -> صفوف صندوق التوقيع تحت (وهو مكانه الطبيعي)
      //      • النسخة        -> صفوف صندوق التوقيع كمان
      //    الحاجات التلاتة كانت مكرّرة: نفس القيمة مكتوبة مرتين على ورقة
      //    واحدة. والوثيقة اللي بتقول نفس الرقم مرتين بتخلّي اللي بيراجعها
      //    يقارنهم بدل ما يقراهم.

      warn +

      '<div class="pl-sh-body">' +
        sections +
        // ⚠️ في ملف الطالب البنود جزء من ملف أكبر، فبتاخد عنوان قسم زي باقي
        //    الأقسام. وفي وثيقة التعهّد لوحدها الديباجة هي أول الكلام.
        (sections ? '<div class="pl-sh-sectitle">' +
            'التعهّد والإقرار' + '</div>' : '') +
        '<div class="pl-sh-pre">' + 'أقرّ أنا الموقّع أدناه بأنني اطّلعت على بنود التعهّد التالية الخاصة بالإسكان الجامعي، وفهمت مضمونها، ووافقت عليها موافقة كاملة:' + '</div>' +
        '<ol class="pl-sh-list">' + terms + '</ol>' +

        '<div class="pl-sh-sig">' +
          // ⚠️ علامة تانية جوّه صندوق التوقيع: لما الملف ياخد ورقتين، ورقة
          //    التوقيع هي أهم ورقة - وعلامة الورقة الأولى مابتوصلهاش.
          //    العلامة دي بتمشي مع الصندوق في أي ورقة يقع فيها.
          tile(s, p.termsHash, 'pl-tile-sig') +
          '<div class="pl-sh-siglbl">' + 'الإقرار المكتوب بخطّ الطالب' + '</div>' +
          '<div class="pl-sh-sigtxt">«' + escHtml(p.typedConfirmation || '') + '»</div>' +
          '<div class="pl-sh-sigrows">' +
            row('الاسم', escHtml(s.full_name || '-')) +
            row('الرقم الجامعي', escHtml(s.student_id || '-')) +
            row('تاريخ التوقيع', NuhFmt.dateTime(p.acceptedAt)) +
            // ⚠️ «النسخة» نزلت هنا من سطر المراجع اللي كان تحت العنوان: هي
            //    بتوصف نسخة البنود اللي الطالب وقّع عليها، فمكانها جنب
            //    التوقيع لا في ترويسة الوثيقة.
            row('النسخة', escHtml(p.policyVersion || '-')) +
            // ⚠️ «عنوان الجهاز» (IP) اتشال من الورقة المطبوعة عن قصد.
            //    الورقة دي بتتداول ورقيًّا وبتتصوّر وبتتبعت، وعنوان الجهاز
            //    بيانات أمنية مالهاش لازمة على وثيقة تعهّد - الجهة اللي
            //    بتستلم الورقة مابتعملش حاجة بيه.
            //    ⚠️ وهو **لسه محفوظ في النظام**: باين في بطاقة التعهّد على
            //       الشاشة (card فوق) وفي سجل التدقيق. الحذف من العرض
            //       المطبوع لا من البيانات.
          '</div>' +
          '<div class="pl-sh-seal" aria-hidden="true"><img src="/nu-logo.svg" alt="">' +
            '<span>' + 'تعهّد موثّق' + '<br>' +
            escHtml(p.policyVersion || '') + '</span></div>' +
        '</div>' +
      '</div>' +

      '<div class="pl-sh-foot">' +
        '<div class="pl-sh-fp">' +
          '<span>' + 'بصمة البنود' + ':<b>' + escHtml(hashText(p.termsHash, 24)) + '</b></span>' +
          '<span>' + 'عدد البنود' + ':<b>' + (p.terms || []).length + '</b></span>' +
        '</div>' +
        '<div class="pl-sh-note">' +
          'وثيقة مستخرجة من نظام بوابة الإسكان الجامعي بجامعة نجران. للتحقّق من صحتها تُطابَق بصمة البنود أعلاه مع سجل الطلب في النظام.' +
          '<br>' + 'تاريخ الطباعة' + ': <span class="pl-sh-printed"></span>' +
        '</div>' +
      '</div>' +
    '</div></div></div>';
  }

  // ⚠️ التبديل بيقرا **تاريخ النهاردة** بالتقويم التاني من جديد، لا بيحوّل
  //     الأرقام المعروضة. النتيجة واحدة دلوقتي (الخانة مقروءة أصلًا)، بس ده
  //     بيخلّي المصدر واحد: التاريخ جاي من ساعة الجهاز في الحالتين.
  document.addEventListener('click', function (ev) {
    var btn = ev.target && ev.target.closest && ev.target.closest('.pl-lh-swap');
    if (!btn) return;
    var box = btn.closest('.pl-lh-date');
    if (!box) return;
    var cal = box.querySelector('.pl-lh-cal');
    var toGreg = cal.textContent.trim() === 'هـ';
    var v = toGreg ? todayGreg() : todayHijri();
    cal.textContent = toGreg ? 'م' : 'هـ';
    box.querySelector('.pl-lh-d').textContent = v[0];
    box.querySelector('.pl-lh-m').textContent = v[1];
    box.querySelector('.pl-lh-y').textContent = v[2];
  });

  // ============================================================================
  //  طباعة الوثيقة.
  //
  //  ⚠️ الإخفاء بالتعليم من هنا مش بقاعدة CSS: بنمشي من الورقة لفوق لحد
  //     <body> ونعلّم **إخوة** كل أب في الطريق. كده القائمة الجانبية والترويسة
  //     وباقي البطاقات بتتشال من التخطيط فعلًا (display:none)، والورقة بتاخد
  //     الصفحة من أولها.
  //
  //     الإخفاء بـ visibility بيسيب العناصر واخدة مساحتها، فالوثيقة كانت
  //     بتطلع ووراها ورقتين فاضيين - واللي بيطبع مايكتشفهمش غير من الطابعة.
  //
  //  ⚠️ والآباء بيتصفّروا كمان (.pl-print-keep): إخفاء القائمة الجانبية
  //     مابيشلش **مساحتها**، والحاوية الرئيسية عندها هامش بعرض القائمة وسقف
  //     عرض. من غير التصفير الوثيقة بتتطبع في تلتين عرض الورقة، فبتطول
  //     وتتقسم على ورقتين - وده اللي كان بيحصل فعلًا.
  //
  //  ⚠️ والتصغير التلقائي: بنقيس ارتفاع الورقة **بعد** ما تاخد مقاس A4
  //     الحقيقي (٨٢‏١مم عرض)، ونقارنه بارتفاع منطقة الطباعة (٢٦٩مم). لو زاد،
  //     بنصغّر بـ transform ونسحب الفرق بهامش سالب - من غير الهامش السالب
  //     المساحة الأصلية بتفضل محجوزة والصفحة التانية بتطلع برضه رغم التصغير.
  //
  //     وفيه حدّ أدنى للتصغير (٦٥٪): تحته النصّ بيبقى غير مقروء على الورق،
  //     وساعتها الأفضل تنزل على ورقتين بخط سليم - والبنود مضبوطة بـ
  //     break-inside:avoid فمفيش بند بيتقسم في النص.
  // ============================================================================
  var PL_MM = 96 / 25.4;         // بكسل لكل مليمتر عند ٩٦ نقطة/بوصة

  // ⚠️ أبعاد الورقة في مكان واحد. قبل كده كان عرض الورقة مكتوب '182mm' في
  //    نصّ الدالة وارتفاعها 260 في ثابت تاني، والاتنين محسوبين على هامش
  //    @page بقيمة 14mm. لما الهامش بقى صفر (عشان نشيل ترويسة المتصفح
  //    وتذييله) الرقمين بقوا غلط: الصفحة بقت 210mm والورقة لسه بتتقصّ على
  //    182mm، فكان بيفضل ٢٨ مليمتر بيضا على الجنبين والمحتوى بيتصغّر أكتر
  //    من اللازم عشان يدخل في ارتفاع محسوب غلط - فالتذييل يختفي.
  var PL_PAD_MM  = 10;           // حشو الورقة نفسها - بديل هامش @page
  var PL_PAGE_W  = 210;          // عرض A4
  // ⚠️ ٧ مليمتر أمان: الرقم النظري بيخلّي الورقة تملا الصفحة بالظبط، وأي
  //    كسر بكسل أو فرق في هوامش الطابعة بيرمي سطر على صفحة تانية فاضية.
  var PL_PAGE_H  = 297 - (2 * PL_PAD_MM) - 7;   // ٢٧٠
  // ⚠️ الورقة اللي عليها transform **مابتتقسّمش** على أكتر من صفحة: المتصفح
  //    بيتعامل معاها كوحدة واحدة ويقصّ اللي زايد. وده اللي كان بيحصل - الملف
  //    الكامل بيتطبع صفحة واحدة والتوقيع والختم مقصوصين.
  //
  //    فالقاعدة بقت صريحة:
  //      • التصغير يدخّل كل حاجة في ورقة واحدة (لحد ٦٠٪) - وده المطلوب:
  //        ملف تحقيقي في ورقة واحدة أسهل في الأرشفة من ورقتين.
  //      • أقل من كده الخطّ بيبقى غير مقروء، فبنشيل الـ transform خالص
  //        عشان الورقة تتقسّم طبيعي بدل ما تتقصّ.
  //
  //    والـ transform بيتحطّ من الجافاسكريبت لا من الـ CSS: لو كان مكتوب في
  //    الـ CSS بـ scale(1) الورقة تفضل «وحدة واحدة» حتى وهي مش متصغّرة،
  //    وتتقصّ برضه.
  var PL_MIN_SCALE = 0.60;

  // ⚠️ بتاخد معرّف الورقة: الصفحة ممكن يكون فيها ورقتين (التعهّد لوحده،
  //    والملف الكامل) وكل زرار بيطبع بتاعته.
  function doPrint(id) {
    var sheet = document.getElementById(id || 'pledgeSheet');
    if (!sheet) return;
    var inner = sheet.querySelector('.pl-sh-scale');
    if (!inner) return;

    var stamp = sheet.querySelector('.pl-sh-printed');
    if (stamp) stamp.textContent = NuhFmt.dateTime(new Date());

    // إخفاء كل ما عدا الورقة، وتصفير الآباء
    var hidden = [], kept = [];
    for (var el = sheet; el && el !== document.body && el.parentElement; el = el.parentElement) {
      if (el !== sheet) { el.classList.add('pl-print-keep'); kept.push(el); }
      var kids = el.parentElement.children;
      for (var i = 0; i < kids.length; i++) {
        if (kids[i] !== el) { kids[i].classList.add('pl-print-hide'); hidden.push(kids[i]); }
      }
    }
    sheet.classList.add('is-printing');
    document.body.classList.add('printing-pledge');

    // ---------- القياس والتصغير ----------
    // ⚠️ ليه طبقة داخلية وارتفاع مثبَّت للورقة:
    //    المحاولات اللي قبل كده كانت بتصغّر **الورقة نفسها** وتسحب الفرق
    //    بهامش سالب. والنتيجة اختلفت من متصفح لمتصفح: نسخة بتقصّ الزايد
    //    وتطبع صفحة واحدة ناقصة (اللي ظهر فعلًا)، ونسخة بتقسّمه على صفحتين
    //    رغم التصغير. السبب إن سلوك العنصر المتحوَّل (transform) في تقسيم
    //    الصفحات مش مضمون.
    //
    //    دلوقتي: الطبقة الداخلية هي اللي بتتصغّر، والورقة بتاخد ارتفاع =
    //    ارتفاع المحتوى **بعد** التصغير. يعني الورقة أقصر من الصفحة فعلًا،
    //    فمفيش حاجة تتقسّم ولا تتقصّ مهما كان المتصفح.
    inner.style.transform = '';
    inner.style.transformOrigin = '';
    sheet.style.height = '';
    sheet.style.overflow = '';
    // ⚠️ العرض الكامل للصفحة والحشو من نفس الثوابت، عشان يستحيل يفارقوا
    //    ارتفاع الطباعة المحسوب فوق. الحشو كان في الـ CSS ورقم العرض هنا،
    //    وde بالظبط اللي خلّاهم يفترقوا أول مرة.
    sheet.style.width = PL_PAGE_W + 'mm';
    sheet.style.padding = PL_PAD_MM + 'mm';
    var h = inner.getBoundingClientRect().height;
    var maxH = PL_PAGE_H * PL_MM;

    if (h > maxH) {
      var scale = maxH / h;
      if (scale >= PL_MIN_SCALE) {
        inner.style.transformOrigin = 'top center';
        inner.style.transform = 'scale(' + scale + ')';
        // ⚠️ + حشو الورقة. box-sizing:border-box في base.css، يعني الارتفاع
        //    اللي بنكتبه هنا **بيشمل** الحشو - فلو كتبنا ارتفاع المحتوى
        //    المصغّر لوحده، المساحة الفعلية للمحتوى بتبقى أقصر منه بمقدار
        //    الحشو (٢٠مم)، و overflow:hidden تحته بتقصّ الفرق. وde اللي كان
        //    بيبلع تذييل الورقة (بصمة البنود وتاريخ الطباعة) بالظبط.
        sheet.style.height = Math.ceil((h * scale) + (2 * PL_PAD_MM * PL_MM)) + 'px';
        sheet.style.overflow = 'hidden';
      }
      // أقلّ من الحدّ: مفيش تصغير - الورقة تتقسّم طبيعي على أكتر من صفحة،
      // وده أحسن من خطّ مايتقراش.
    }
    sheet.style.width = '';

    var done = false;
    function clear() {
      if (done) return; done = true;
      document.body.classList.remove('printing-pledge');
      sheet.classList.remove('is-printing');
      hidden.forEach(function (n) { n.classList.remove('pl-print-hide'); });
      kept.forEach(function (n) { n.classList.remove('pl-print-keep'); });
      inner.style.transform = '';
      inner.style.transformOrigin = '';
      sheet.style.height = '';
      sheet.style.overflow = '';
      sheet.style.width = '';
      sheet.style.padding = '';
    }
    window.addEventListener('afterprint', clear, { once: true });
    setTimeout(clear, 3000);   // شبكة أمان: afterprint مش مضمونة في كل المتصفحات

    window.print();
  }

  // ============================================================================
  //  معاينة قبل الطباعة.
  //
  //  ⚠️ ليه معاينة أصلًا: خانات الترويسة (الرقم/التاريخ/المرفقات) عايشة جوّه
  //     الورقة، والورقة display:none على الشاشة - بتظهر لحظة الطباعة بس.
  //     يعني من غير الخطوة دي الموظف مايقدرش يوصل للخانات خالص.
  //
  //  ⚠️ وليه معاينة الورقة نفسها لا نموذج جانبي: الموظف بيكتب في **الوثيقة**
  //     اللي هتطلع من الطابعة، فاللي بيشوفه هو اللي بيتطبع بالحرف. النموذج
  //     الجانبي كان هيخلّيه يكتب في مكان ويراهن إن القيم هتوصل مكان تاني.
  //
  //  ⚠️ والمعاينة **مش** بتلمس منطق الطباعة: doPrint زي ما هي بالحرف بقياسها
  //     وتصغيرها، والمعاينة طبقة فوقها بتنادي عليها. منطق التصغير ده اتظبط
  //     على تلات متصفحات، وأي لمسة فيه معناها إعادة الضبط من الأول.
  // ============================================================================
  function preview(id) {
    var sheetId = id || 'pledgeSheet';
    var sheet = document.getElementById(sheetId);
    if (!sheet) return;
    if (document.querySelector('.pl-pv')) return;   // مفتوحة أصلًا

    var stamp = sheet.querySelector('.pl-sh-printed');
    if (stamp) stamp.textContent = NuhFmt.dateTime(new Date());

    var ov = document.createElement('div');
    ov.className = 'pl-pv';
    ov.innerHTML =
      '<div class="pl-pv-bar">' +
        '<span class="pl-pv-hint">رقم الطلب والتاريخ مقروءان من النظام. اكتب عدد المرفقات إن وُجدت، ثم اطبع.</span>' +
        '<button type="button" class="pl-pv-btn pl-pv-cancel">إلغاء</button>' +
        '<button type="button" class="pl-pv-btn primary pl-pv-ok">طباعة</button>' +
      '</div><div class="pl-pv-scroll"></div>';
    document.body.appendChild(ov);

    // ⚠️ الورقة **بتتنقل** جوّه المعاينة ولا تتنسخ: النسخة كانت هتخلّي الموظف
    //    يكتب في نسخة والطباعة تاخد الأصل الفاضي. وبنفتكر مكانها الأصلي عشان
    //    نرجّعها بالظبط - doPrint بتمشي من الورقة لفوق على آبائها الحقيقيين.
    var home = sheet.parentNode, next = sheet.nextSibling;
    ov.querySelector('.pl-pv-scroll').appendChild(sheet);
    sheet.classList.add('is-preview');
    document.body.classList.add('pl-pv-open');

    function restore() {
      sheet.classList.remove('is-preview');
      if (home) home.insertBefore(sheet, next);
      document.body.classList.remove('pl-pv-open');
      ov.remove();
      document.removeEventListener('keydown', onKey);
    }
    function onKey(e) { if (e.key === 'Escape') restore(); }
    document.addEventListener('keydown', onKey);

    ov.querySelector('.pl-pv-cancel').addEventListener('click', restore);
    ov.addEventListener('click', function (e) { if (e.target === ov) restore(); });
    ov.querySelector('.pl-pv-ok').addEventListener('click', function () {
      restore();
      // ⚠️ بعد رجوع الورقة لمكانها في الشجرة، لأن doPrint بتعلّم إخوة آبائها.
      //    من غير الانتظار ده المتصفح ممكن يقيس الورقة وهي لسه في المعاينة.
      setTimeout(function () { doPrint(sheetId); }, 30);
    });

    // ⚠️ التركيز على «المرفقات» لا على رقم الطلب: هي الخانة الوحيدة اللي
    //    بقت قابلة للكتابة، وتركيز على خانة مقروءة مابيعملش حاجة.
    var first = sheet.querySelector('.pl-lh-att');
    if (first) setTimeout(function () { first.focus(); }, 40);
  }

  // ⚠️ print بقت هي المعاينة: كل الشاشات بتنادي NuhPledgeDoc.print()، وتغيير
  //    الاسم في كل مكان كان هيسيب شاشة واحدة منسية بتطبع من غير ترويسة مكتملة.
  return { card: card, sheet: sheet, print: preview, printNow: doPrint, tile: tile, hashText: hashText };
})();
