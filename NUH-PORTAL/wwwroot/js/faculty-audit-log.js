// ==========================================================================
//  faculty-audit-log.js — سجل عمليات وحدة السكن: التعريف الوحيد لعرضه.
//
//  ⚠️ الملف ده اتعمل لأن نفس السجل بقى مطلوب في شاشتين:
//        • قائمة الوحدات      — نافذة السجل بتبويبيها (الشاغلون / التعديلات)
//        • تغيير بيانات ساكن  — سطر «آخر تعديل» والسجل الكامل بضغطة
//     ونسخ دالة الرسم في الملفين كان معناه إن أول تعديل على شكل السطر
//     (إضافة عمود، تغيير صيغة التاريخ) يتعمل في واحدة وتُنسى التانية —
//     فيبقى نفس السجل بشكلين لنفس المستخدم على بُعد ضغطتين.
//
//  ⚠️ والبيانات مش بتتجاب هنا: الشاشتين بينادوا /api/FacultyHousing/units/{id}
//     أصلًا وبيرجّع d.changes ضمن نفس الاستجابة. الوحدة دي بتاخد المصفوفة
//     الجاهزة وترسمها — طلب تاني لنفس المسار كان هيضاعف الحمل ويفتح فجوة
//     زمنية تخلّي السطر يقول حاجة والجدول يقول حاجة تانية.
//
//  ⚠️ أسماء الإجراءات والحقول والقيم كلها من NuhAudit (js/audit-labels.js)،
//     اللي بيقرا من نفس ملفات الـ resx. مافيش نصّ عربي مكتوب هنا.
//
//  ⚠️ الشكل خطّ زمني (.nuh-tl) - نفس مكوّن سجل إجراءات حساب الطالب بالحرف.
//     كان جدولًا بثلاثة أعمدة داخل بطاقة داخل النافذة، فالموظف ينتقل بين
//     تبويبَي «الشاغلون» و«التعديلات» في **نفس النافذة** فيجد شكلين مختلفين
//     لنفس السؤال: مين غيّر إيه وإمتى. الشكل الواحد هو ما يجعل الشاشة تبدو
//     مصقولة، لا حجم الخطّ ولا لون الحدود.
//
//  ⚠️ والعمود الأوسط («قبل») كان منحرفًا عن رأسه: قاعدة عامة في site.css
//     توسّط كل خلايا الجداول، و.chg-tbl th وحده كان مكتوبًا فيه text-align.
//     الخطّ الزمني ما فيهوش أعمدة أصلًا فالانحراف مستحيل يتكرّر.
//
//  الشكل في css/components.css تحت .nuh-tl* و .chg* و .fh-modal* و .fh-lastchg*.
// ==========================================================================
var NuhFacultyLog = (function () {
  'use strict';

  function T(k) { return (window.FH_T && window.FH_T[k]) || k; }
  function esc(v) { return escHtml(v); }
  function dt(v) { return NuhFmt.dateTime(v); }

  // ==========================================================================
  //  أيقونات الخطّ الزمني ونبرته.
  //
  //  ⚠️ SVG لا حروف يونيكود: الحرف (✓ ✕ ↻) يُرسَم بخطّ النظام لا بخطّ الصفحة،
  //     فيختلف سمكه وحجمه بين ويندوز وماك - وهو أول ما يجعل الشاشة تبدو غير
  //     مصقولة.
  //
  //  ⚠️ ومكتوبة هنا لا في ملف مشترك مع سجل حساب الطالب: المفردات مختلفة
  //     تمامًا (إجراءات وحدة سكن مقابل إجراءات حساب في الدليل)، فمافيش قيمة
  //     مكرَّرة تتوحَّد - أسماء الأصناف (ok/bad/warn/info) هي المشتركة وهي
  //     معرَّفة مرة واحدة في css/components.css.
  // ==========================================================================
  var ICONS = {
    _default:                  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 8v4l3 2"/></svg>',
    faculty_service_started:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>',
    faculty_service_stopped:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>',
    faculty_handover:          '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3 4 7l4 4"/><path d="M4 7h16"/><path d="m16 21 4-4-4-4"/><path d="M20 17H4"/></svg>',
    faculty_occupant_updated:  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.1" stroke-linecap="round" stroke-linejoin="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"/></svg>',
    faculty_ad_push:           '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>',
    faculty_ad_push_failed:    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 9v5"/><path d="M12 17h.01"/><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/></svg>',
    faculty_ad_ou_move:        '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 9 2 12l3 3"/><path d="M9 5l3-3 3 3"/><path d="M15 19l-3 3-3-3"/><path d="M19 9l3 3-3 3"/><path d="M2 12h20"/><path d="M12 2v20"/></svg>',
    faculty_ad_dn_drift:       '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/><path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/></svg>',
    faculty_import_applied:    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>',
    clock:                     '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 7.5V12l3 1.8"/></svg>',
    person:                    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>'
  };

  // النبرة هي وحدها ما يحمل اللون - النصّ والحدود محايدة.
  var TONE = {
    faculty_service_started: 'ok',
    faculty_occupant_updated: 'ok',
    faculty_handover: 'info',
    faculty_ad_push: 'info',
    faculty_ad_ou_move: 'info',
    faculty_import_applied: 'info',
    faculty_service_stopped: 'warn',
    faculty_ad_dn_drift: 'warn',
    faculty_ad_push_failed: 'bad'
  };

  // معلومات المنفّذ: الوقت ثم الاسم ثم عنوان الجهاز.
  // ⚠️ عنوان الجهاز بيفضل معروض هنا عن قصد — دي شاشة داخلية للمساءلة، مش
  //    الورقة المطبوعة اللي بتتداول برّه (وثيقة التعهّد شالته لنفس السبب
  //    بالعكس). «مين عدّل» من غير «منين» ناقصة في أي مراجعة أمنية.
  // ⚠️ والوقت في سطر البيانات لا في طرف سطر العنوان: هناك كان يقع تحت شريط
  //    التمرير فيُقصّ - نفس السبب اللي نقله في سجل حساب الطالب.
  function actorMeta(a) {
    var m = '<span class="nuh-tl-time">' + ICONS.clock +
            '<span>' + esc(NuhFmt.time(a.actionAt)) + '</span></span>';
    if (a.actorName) m += '<span class="sp"></span>' + ICONS.person +
                          '<span>' + esc(a.actorName) + '</span>';
    if (a.ipAddress) m += '<span class="sp"></span><span class="nuh-tl-ip">' +
                          esc(a.ipAddress) + '</span>';
    return m;
  }

  // صفّ حقل واحد: اسمه، ثم قيمته القديمة مشطوبة، ثم سهم، ثم الجديدة.
  // ⚠️ المكوّن (.chg) موجود أصلًا في components.css ومستعمَل في سجل الطلبات -
  //    مش نسخة تانية منه. والسهم مرسوم بحدود CSS فبينقلب مع اتجاه الصفحة
  //    لوحده، والحرف ← كان هيحتاج نسخة لكل اتجاه.
  function fieldRows(a) {
    var rows = (a.fields || []).map(function (f) {
      // ⚠️ الفرق بين «كانت فارغة فاتملت» و«ما اتغيّرتش» هو جوهر السطر، فالفارغ
      //    بيتكتب «(فارغ)» بلون باهت لا بيتساب بياض.
      var oldEmpty = (f.oldValue == null || f.oldValue === '');
      var newEmpty = (f.newValue == null || f.newValue === '');
      return '<div class="chg-row">' +
               '<span class="chg-lbl">' + esc(NuhAudit.fieldText(f.fieldName)) + '</span>' +
               '<span class="chg">' +
                 '<span class="chg-old' + (oldEmpty ? ' chg-empty' : '') + '">' +
                   esc(oldEmpty ? T('fh_ChgEmpty') : NuhAudit.valueText(f.oldValue, f.fieldName)) + '</span>' +
                 '<i class="chg-arrow"></i>' +
                 '<span class="chg-new' + (newEmpty ? ' chg-empty' : '') + '">' +
                   esc(newEmpty ? T('fh_ChgEmpty') : NuhAudit.valueText(f.newValue, f.fieldName)) + '</span>' +
               '</span>' +
             '</div>';
    }).join('');
    return rows ? '<div class="chg-list wide">' + rows + '</div>' : '';
  }

  // قائمة العمليات كاملة — الأحدث أولًا (الترتيب من الخادم).
  function items(list) {
    list = list || [];
    if (!list.length) return '<div class="nuh-tl-empty">' + esc(T('fh_NoAuditLog')) + '</div>';

    var html = '<div class="nuh-tl">';
    var lastDay = null;

    list.forEach(function (a) {
      // ⚠️ التاريخ يُكتب مرة لكل يوم لا مرة لكل سطر. والمفتاح من NuhFmt.date
      //    نفسها التي تُكتب بها الترويسة، فلو بدّل الموظف التقويم إلى الهجري
      //    تغيّر التجميع معه - ولا يبقى عنوان بتقويم وصفوفه بتقويم آخر.
      var dayKey = NuhFmt.date(a.actionAt);
      if (dayKey !== lastDay) {
        lastDay = dayKey;
        html += '<div class="nuh-tl-day"><b>' + esc(NuhFmt.dateFull(a.actionAt)) + '</b><i></i></div>';
      }

      html += '<div class="nuh-tl-item">' +
          '<div class="nuh-tl-dot ' + (TONE[a.action] || '') + '">' +
            (ICONS[a.action] || ICONS._default) + '</div>' +
          '<div class="nuh-tl-body">' +
            '<div class="nuh-tl-top">' +
              '<span class="nuh-tl-title">' + esc(NuhAudit.actionText(a.action)) + '</span>' +
            '</div>' +
            '<div class="nuh-tl-meta">' + actorMeta(a) + '</div>' +
            fieldRows(a) +
          '</div>' +
        '</div>';
    });

    return html + '</div>';
  }

  // ==========================================================================
  //  سطر «آخر تعديل» — بيتعرض دايمًا بلا ضغطة.
  //
  //  ⚠️ ده مش اختصار للسجل، ده إجابة على سؤال مختلف. السجل بيجاوب «إيه اللي
  //     حصل على الوحدة دي»، والسطر بيجاوب «مين آخر واحد لمسها وإمتى» — وهو
  //     السؤال اللي بيتسأل وإنت داخل تعدّل. زرّ لوحده مابيحلّهاش: لازم
  //     تفتكر تدوس عليه، والنسيان هو المشكلة أصلًا.
  // ==========================================================================
  function lastLine(list) {
    list = list || [];
    if (!list.length) return '';
    var a = list[0];

    return '<div class="fh-lastchg">' +
      '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
        'stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<circle cx="12" cy="12" r="10"/><path d="M12 8v4l3 3"/></svg>' +
      '<span class="fh-lastchg-txt">' + esc(T('fh_LastChange')) + ' ' +
        '<b>' + esc(NuhAudit.actionText(a.action)) + '</b>' +
        (a.actorName ? ' — <b>' + esc(a.actorName) + '</b>' : '') +
        ' — ' + dt(a.actionAt) +
      '</span>' +
      '<span class="fh-lastchg-sp"></span>' +
      '<button type="button" class="fh-lastchg-btn" data-fhlog="1">' +
        esc(T('fh_ViewFullLog')) + ' (' + list.length + ')</button>' +
    '</div>';
  }

  // ==========================================================================
  //  نافذة السجل الكامل.
  //
  //  ⚠️ بتتبني عند أول فتحة وبتتشال عند الإغلاق — مش بتفضل في الصفحة مخفية.
  //     الشاشة دي بتعيد رسم السطر بعد كل حفظ، ونافذة قديمة سايبة في الشجرة
  //     كانت هتفضل شايلة السجل بتاع قبل الحفظ.
  // ==========================================================================
  function open(list, subtitle) {
    close();

    // ⚠️ نفس أصناف نافذة السجل في شاشة قائمة الوحدات بالحرف
    //    (.modal-overlay.open و .fh-modal و .modal-close) — النافذتين لازم
    //    يبقوا نفس النافذة شكلًا، والصنف .open هو اللي بيعرض لا .is-open.
    var ov = document.createElement('div');
    ov.className = 'modal-overlay open';
    ov.id = 'fhLogOverlay';
    ov.innerHTML =
      '<div class="fh-modal" role="dialog" aria-modal="true">' +
        '<div class="fh-modal-head">' +
          '<div>' +
            '<div class="card-title">' + esc(T('fh_TabChanges')) + '</div>' +
            (subtitle ? '<div class="fh-modal-sub">' + esc(subtitle) + '</div>' : '') +
          '</div>' +
          '<button type="button" class="modal-close" aria-label="' + esc(T('close')) + '">' +
            '<span aria-hidden="true">&times;</span></button>' +
        '</div>' +
        '<div class="fh-modal-body">' + items(list) + '</div>' +
      '</div>';
    document.body.appendChild(ov);

    ov.querySelector('.modal-close').addEventListener('click', close);
    // الضغط على الخلفية يقفل، والضغط جوّه النافذة لأ
    ov.addEventListener('click', function (e) { if (e.target === ov) close(); });
    document.addEventListener('keydown', onKey);
  }

  function onKey(e) { if (e.key === 'Escape') close(); }

  function close() {
    var ov = document.getElementById('fhLogOverlay');
    if (ov) ov.remove();
    document.removeEventListener('keydown', onKey);
  }

  // ==========================================================================
  //  الربط الكامل: بيرسم السطر في حاوية ويربط زرّه بالنافذة.
  //
  //  ⚠️ الحاوية بتتفضّى لو مافيش عمليات — سطر بيقول «آخر تعديل: لا يوجد»
  //     بياخد مساحة عشان يقول إنه مالوش لازمة.
  // ==========================================================================
  function mount(containerId, list, subtitle) {
    var box = document.getElementById(containerId);
    if (!box) return;
    box.innerHTML = lastLine(list);
    var btn = box.querySelector('[data-fhlog]');
    if (btn) btn.addEventListener('click', function () { open(list, subtitle); });
  }

  return { items: items, lastLine: lastLine, open: open, close: close, mount: mount };
})();
