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
//  الشكل في css/components.css تحت .ch-* و .fh-modal* و .fh-lastchg*.
// ==========================================================================
var NuhFacultyLog = (function () {
  'use strict';

  function T(k) { return (window.FH_T && window.FH_T[k]) || k; }
  function esc(v) { return escHtml(v); }
  function dt(v) { return NuhFmt.dateTime(v); }

  // معلومات المنفّذ: الوقت ثم الاسم ثم عنوان الجهاز.
  // ⚠️ عنوان الجهاز بيفضل معروض هنا عن قصد — دي شاشة داخلية للمساءلة، مش
  //    الورقة المطبوعة اللي بتتداول برّه (وثيقة التعهّد شالته لنفس السبب
  //    بالعكس). «مين عدّل» من غير «منين» ناقصة في أي مراجعة أمنية.
  function actorMeta(a) {
    return dt(a.actionAt) +
      (a.actorName ? ' &nbsp;·&nbsp; ' + esc(a.actorName) : '') +
      (a.ipAddress ? ' &nbsp;·&nbsp; <span class="ch-ip">' + esc(a.ipAddress) + '</span>' : '');
  }

  function fieldRows(a) {
    return (a.fields || []).map(function (f) {
      // ⚠️ الفرق بين «كانت فارغة فاتملت» و«ما اتغيّرتش» هو جوهر السطر، فالفارغ
      //    بيتكتب «(فارغ)» بلون باهت لا بيتساب بياض.
      var oldEmpty = (f.oldValue == null || f.oldValue === '');
      var newEmpty = (f.newValue == null || f.newValue === '');
      return '<tr><td>' + esc(NuhAudit.fieldText(f.fieldName)) + '</td>' +
             '<td><span class="chg-old' + (oldEmpty ? ' chg-empty' : '') + '">' +
               esc(oldEmpty ? T('fh_ChgEmpty') : NuhAudit.valueText(f.oldValue, f.fieldName)) + '</span></td>' +
             '<td><span class="chg-new' + (newEmpty ? ' chg-empty' : '') + '">' +
               esc(newEmpty ? T('fh_ChgEmpty') : NuhAudit.valueText(f.newValue, f.fieldName)) + '</span></td></tr>';
    }).join('');
  }

  // قائمة العمليات كاملة — الأحدث أولًا (الترتيب من الخادم).
  function items(list) {
    list = list || [];
    if (!list.length) return '<div class="fh-empty">' + esc(T('fh_NoAuditLog')) + '</div>';

    return list.map(function (a) {
      var rows = fieldRows(a);
      return '<div class="ch-item">' +
        '<div class="ch-top"><span class="ch-act">' + esc(NuhAudit.actionText(a.action)) + '</span>' +
        '<span class="ch-meta">' + actorMeta(a) + '</span></div>' +
        (rows
          ? '<table class="chg-tbl"><tr><th>' + esc(T('fh_ChgField')) + '</th><th>' +
            esc(T('fh_ChgOld')) + '</th><th>' + esc(T('fh_ChgNew')) + '</th></tr>' + rows + '</table>'
          : '') +
      '</div>';
    }).join('');
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
