// ============================================================================
//  قراءة جدول انتقالات مسار الطلب في الواجهة.
//
//  ⚠️ الجدول نفسه ليس هنا. تعريفه الوحيد في Core/RequestWorkflow.cs على الخادم،
//     ويُحقن في كل صفحة باسم window.__WF من _Layout.cshtml. هذا الملف يقرأ منه
//     فقط، فلا تكتب حالة أو انتقالًا هنا مهما كان صغيرًا.
//
//     السبب: الجدول كان مكتوبًا ثلاث مرات (الخادم، شاشة القائمة، شاشة التفاصيل
//     مرتين) وتفارق فعلًا — فاختفى زر الاعتماد على الطلبات في حالة «مقدَّم»
//     من شاشة التفاصيل وطُبع مكانه «undefined»، بينما نفس الطلب يُعتمد من
//     شاشة القائمة. أي حالة تُضاف مستقبلًا تعمل في الشاشتين بلا تعديل هنا.
// ============================================================================
var NuhWorkflow = (function () {
  'use strict';

  // ⚠️ __WF بقى كائن { transitions, rejected } مش مصفوفة. مفيش احتياطي
  //    للشكل القديم عن قصد: الاحتياطي بيخلّي نصف الصفحات تشتغل بالقديم
  //    ونصفها بالجديد، وده أصعب في الاكتشاف من عطل صريح.
  function wf() { return window.__WF || {}; }
  function table() { var t = wf().transitions; return Array.isArray(t) ? t : []; }

  // ==========================================================================
  //  رقم خطوة المرحلة في مؤشّر «سير العمل».
  //
  //  ⚠️ الرقم ده موجود على الخادم من الأول في حقل Step جوّه نفس جدول
  //     الانتقالات — ومع ذلك كانت شاشة التفاصيل كاتبة خريطة تانية بالإيد
  //     (stageToWorkflowStep). والاتنين اتفارقوا فعلًا:
  //
  //        الحالة              الخادم   الواجهة
  //        submitted             1        0      ← اختلاف حقيقي
  //        housing_approved      2        1      ← اختلاف حقيقي
  //        pending_supervisor    1        1
  //        cyber_review          2        2
  //
  //     يعني طلب مقدَّم كان المؤشّر بيوقف على «تم تقديم الطلب» بينما الخادم
  //     شايفه في مرحلة «بانتظار مراجعة الإسكان» — والشارة فوقه بتقول كده.
  //
  //  ⚠️ المراحل النهائية (مكتمل/معتمد) والحالة «بيانات ناقصة» مالهاش انتقال في
  //     الجدول لأنها مش نقطة قرار، فأرقامها هنا — ودي كل ما تبقّى من الخريطة.
  var TERMINAL_STEP = { completed: 4, approved: 4, need_more_info: 1 };

  function stepOf(status) {
    var s = String(status || '').toLowerCase();
    var row = forStatus(s);
    if (row && typeof row.step === 'number') return row.step;
    return TERMINAL_STEP[s];      // undefined لو الحالة مش معروفة — والنداء بيقرر
  }

  // «هل الحالة دي مرفوضة؟» — القائمة من الخادم لا مكتوبة هنا.
  //    ⚠️ شاشة التفاصيل كانت بتسألها بـ indexOf('rejected') — فحص نصّي بيمسك
  //       أي حالة فيها الكلمة، ويسكت لو اتغيّر الاسم.
  function isRejected(status) {
    var s = String(status || '').toLowerCase();
    var list = wf().rejected;
    if (!Array.isArray(list)) return false;
    for (var i = 0; i < list.length; i++) {
      if (String(list[i]).toLowerCase() === s) return true;
    }
    return false;
  }

  // بيانات الانتقال من حالة معيّنة، أو null إن كانت مرحلة مقفولة (مكتمل/مرفوض)
  function forStatus(status) {
    var s = String(status || '').toLowerCase();
    var rows = table();
    for (var i = 0; i < rows.length; i++) {
      if (String(rows[i].status || '').toLowerCase() === s) return rows[i];
    }
    return null;
  }

  // ==========================================================================
  //  الاسم الواحد للمرحلة الواحدة — نفس RequestWorkflow.Canonical على الخادم.
  //
  //  ⚠️ الشاشات كانت بتبني مفتاح الترجمة وصنف الـ CSS من الحالة الخام
  //     ('req_badge_' + status)، فطلبان واقفان في نفس المرحلة بالظبط ظهروا في
  //     نفس الجدول بنصّين ولونين (pending_cyber مقابل cyber_review) — والموظف
  //     بيقراهما حالتين مختلفتين. التسمية بقت تمرّ من هنا.
  //
  //  ⚠️ والمسمّيات نفسها مش مكتوبة هنا: القراءة من حقل sameStageAs في الجدول
  //     المحقون من الخادم، فما فيش قائمة في الواجهة تقدر تفارق الجدول.
  // ==========================================================================
  function canonical(status) {
    var s = String(status || '').toLowerCase();
    var row = forStatus(s);
    var same = row && row.sameStageAs;
    return same ? String(same).toLowerCase() : s;
  }

  function badgeKey(status) { return 'req_badge_' + canonical(status); }
  function stageKey(status) { return 'req_stage_' + canonical(status); }

  // هل يملك المستخدم صلاحية التصرّف في هذه المرحلة؟
  // hasPerm دالة يمرّرها كل شاشة لأن مصدر الصلاحيات يختلف بينها.
  function canAct(status, hasPerm) {
    var t = forStatus(status);
    if (!t || typeof hasPerm !== 'function') return false;
    var perms = t.permissions || [];
    for (var i = 0; i < perms.length; i++) {
      if (hasPerm(perms[i])) return true;
    }
    return false;
  }

  // ==========================================================================
  //  نقطة الإرسال — لماذا هي هنا لا في كل شاشة.
  //
  //  ⚠️ للطلب مساران وواجهتان مختلفتان لإرسال القرار: مسار تسجيل الطالب يُرسل
  //     POST /api/Workflow/{id}/{approve|reject|request-info} والمرحلة هي التي
  //     تحدّد الإجراء، ومسار طلب الموظف يُرسل PUT /api/Requests/{id}/review
  //     ومعه الحالة الهدف. الشاشتان كانتا تعرفان هذا التفريع كلٌّ بنسختها،
  //     وشاشة القائمة لم تكن تعرف المسار الأول أصلًا — فطلب الطالب يظهر لها
  //     بلا زر. المسار الآن حقلٌ في الجدول (api) والإرسال من هنا وحده.
  // ==========================================================================
  function endpointFor(id, status, decision, notes, fields) {
    var wf = forStatus(status);
    if (!wf) return null;

    if (wf.api === 'workflow') {
      var body = { notes: notes || '' };
      if (decision === 'request-info' && fields) body.fields = fields;
      return { url: '/api/Workflow/' + id + '/' + decision, method: 'POST', body: body };
    }

    var target = decision === 'approve' ? wf.approveTo : (decision === 'reject' ? wf.rejectTo : '');
    if (!target) return null;
    // الهوية من الجلسة على السيرفر — الواجهة ما بتبعتش مين اللي راجع.
    return { url: '/api/Requests/' + id + '/review', method: 'PUT',
             body: { status: target, notes: notes || '' } };
  }

  // نص زر القرار ومفتاح ترجمته — من الجدول، فلا تُكتب أسماء الأزرار في الشاشات.
  function labelKey(status, decision) {
    var wf = forStatus(status);
    if (!wf) return '';
    return (decision === 'approve' ? wf.approveKey : wf.rejectKey) || '';
  }

  return {
    forStatus: forStatus,
    isRejected: isRejected,
    stepOf: stepOf,
    canonical: canonical,
    badgeKey: badgeKey,
    stageKey: stageKey,
    canAct: canAct,
    endpointFor: endpointFor,
    labelKey: labelKey
  };
})();
