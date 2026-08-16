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

  function table() { return Array.isArray(window.__WF) ? window.__WF : []; }

  // بيانات الانتقال من حالة معيّنة، أو null إن كانت مرحلة مقفولة (مكتمل/مرفوض)
  function forStatus(status) {
    var s = String(status || '').toLowerCase();
    var rows = table();
    for (var i = 0; i < rows.length; i++) {
      if (String(rows[i].status || '').toLowerCase() === s) return rows[i];
    }
    return null;
  }

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
    canAct: canAct,
    endpointFor: endpointFor,
    labelKey: labelKey
  };
})();
