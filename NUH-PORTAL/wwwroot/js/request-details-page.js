/* التوكن هنا *اختياري* عن قصد.
   الجلسة الحقيقية لصفحات MVC هي كوكي NUH.Auth، والمتصفح بيبعتها تلقائيًا مع كل
   نداء لنفس الدومين. الـ staffToken في localStorage بقايا من الواجهة القديمة.

   قبل كده كان: لو مفيش توكن -> تحويل على /Account/Login. وصفحة الدخول لما بتلاقي
   الكوكي صالح بتحوّل على /Home. النتيجة: المستخدم يضغط على طلب فيلاقي نفسه في
   لوحة التحكم من غير أي رسالة.

   وده كان بيحصل فعلًا: register-phone.html بيمسح staffToken لما طالب يتحقق بالـ OTP
   (عشان ماتختلطش جلسة الموظف بجلسة الطالب)، فأي موظف يجرّب شاشة تسجيل الطالب في
   نفس المتصفح كان بيفقد قدرته يفتح تفاصيل الطلبات — والكوكي بتاعته سليمة طول الوقت. */
const token = localStorage.getItem('staffToken');

// بنضيف الترويسة لو التوكن موجود (توافق مع الواجهة القديمة)، وغير كده الكوكي بتتكفّل.
function authHeaders(extra) {
  var h = {};
  for (var k in (extra || {})) h[k] = extra[k];
  if (token) h['Authorization'] = 'Bearer ' + token;
  return h;
}
const currentUser = JSON.parse(localStorage.getItem('staffUser') || '{}');
const _userRole = (currentUser.role || '').toLowerCase();

// الصلاحيات بتتحقن من السيرفر مع الصفحة (Views/Requests/Details.cshtml).
// ⚠️ localStorage.staffUser بقايا من الواجهة القديمة ومش مصدر موثوق للدور —
//    الفحص الحقيقي بيحصل على السيرفر في كل الحالات، وده إخفاء واجهة بس.
const _perms = Array.isArray(window.NUH_PERMS) ? window.NUH_PERMS : [];
function can(p) { return _perms.indexOf(p) !== -1; }
const urlParams = new URLSearchParams(window.location.search);
var __pathIdMatch = window.location.pathname.match(/\/Requests\/Details\/(\d+)/i);
const requestId = (urlParams.get('id') || (__pathIdMatch ? __pathIdMatch[1] : null));

function escHtml(str) { return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

// نص بديل لو المفتاح مش موجود في ملف الترجمة.
// ⚠️ كانت متعرّفة *جوه* renderRequest، يعني أي كود برّاها بينادي عليها كان
//    بيرمي ReferenceError. ده اللي كان بيمنع خانة «المعلومات المطلوبة» إنها
//    تظهر: selectInfo() بينادي setReasonTexts('info') واللي بتستخدم tf، فبتقع
//    قبل ما توصل للسطر اللي بيعرض الخانة. (زر الرفض كان شغال لأنه بيستخدم t.)
function tf(key, arText, enText) {
  var v = t(key);
  if (v !== key) return v;
  var root = document.getElementById('html-root');
  var lng = root ? (root.getAttribute('lang') || 'ar') : 'ar';
  return lng === 'en' ? enText : arText;
}
function goBack() { window.location.href = '/Requests'; }
function formatDate(d) { if (!d) return '-'; return new Date(d).toLocaleString(); }

window.onLanguageChange = function(l) {
  if (requestId) loadRequest();
};

const stageNames = {
  submitted: 'rdp_stage_submitted',
  housing_approved: 'rdp_stage_housingApproved',
  housing_rejected: 'rdp_stage_housingRejected',
  cyber_review: 'rdp_stage_cyberReview',
  cyber_approved: 'rdp_stage_cyberApproved',
  cyber_rejected: 'rdp_stage_cyberRejected',
  ready_for_provisioning: 'rdp_stage_readyForProvisioning',
  completed: 'rdp_stage_completed',
  pending_supervisor: 'rdp_stage_pendingSupervisor',
  pending_cyber: 'rdp_stage_cyberReview',
  need_more_info: 'rdp_stage_needMoreInfo',
  approved: 'rdp_stage_completed',
  rejected: 'rdp_stage_rejected'
};
// اسم الإجراء بيتحدد بالمرحلة اللي اتعمل فيها (fromStage) مش اللي انتقل ليها (toStage).
// المشرف بيوافق وهو واقف على pending_supervisor فالطلب بينتقل لـ pending_cyber —
// التسمية بالـ toStage كانت بتكتب "مراجعة الأمن السيبراني" على إجراء إدارة الإسكان،
// يعني بتنسب الخطوة للجهة اللي لسه ماعملتش حاجة.
const actionNames = {
  pending_supervisor:     { ok: 'rdp_stage_housingApproved',       no: 'rdp_stage_housingRejected' },
  pending_cyber:          { ok: 'rdp_stage_cyberApproved',         no: 'rdp_stage_cyberRejected'   },
  pending_admin:          { ok: 'rdp_stage_readyForProvisioning',  no: 'rdp_stage_rejected'        },
  ready_for_provisioning: { ok: 'rdp_stage_completed',             no: 'rdp_stage_rejected'        },
  need_more_info:         { ok: 'rdp_stage_pendingSupervisor',     no: 'rdp_stage_rejected'        }
};
// أسماء أنواع الطلبات موجودة في الترجمة (req_type_*) لكن الصفحة كانت بتعرض
// الكود الخام self_registration زي ما هو للمستخدم النهائي.
function requestTypeName(code) {
  var raw = String(code == null ? '' : code);
  if (!raw) return '';
  var key = 'req_type_' + raw;
  var v = t(key);
  return v === key ? raw : v;   // لو النوع جديد ومالوش ترجمة نعرض الكود بدل ما نخفيه
}

const statusMap = {
  submitted: 'rdp_status_pending', housing_approved: 'rdp_stage_housingApproved', housing_rejected: 'rdp_stage_rejected',
  cyber_review: 'rdp_stage_cyberReview', cyber_approved: 'rdp_status_approved', cyber_rejected: 'rdp_stage_rejected',
  ready_for_provisioning: 'rdp_stage_readyForProvisioning', completed: 'rdp_stage_completed',
  pending_supervisor: 'rdp_stage_housingApproved', pending_cyber: 'rdp_stage_cyberReview',
  need_more_info: 'rdp_stage_needMoreInfo',
  approved: 'rdp_stage_completed', rejected: 'rdp_stage_rejected'
};
const workflowSteps = [
  { status:'submitted', key:'submitted' },
  { status:'housing_approved', key:'housing' },
  { status:'cyber_review', key:'cyber_review' },
  { status:'cyber_approved', key:'cyber_approved' },
  { status:'ready_for_provisioning', key:'ready' },
  { status:'completed', key:'completed' }
];
const stageToWorkflowStep = {
  submitted:0, housing_approved:1, housing_rejected:1,
  cyber_review:2, cyber_approved:3, cyber_rejected:2,
  ready_for_provisioning:3, completed:4,
  pending_supervisor:1, pending_cyber:2,
  approved:4,
  need_more_info:1, rejected:0
};

const selfRegWorkflowSteps = [
  { status: null, key:'submitted' },
  { status:'pending_supervisor', key:'housing' },
  { status:'pending_cyber', key:'cyber_review' },
  { status:'ready_for_provisioning', key:'ready' },
  { status:'completed', key:'completed' }
];
const isRejectedStatus = { housing_rejected:true, cyber_rejected:true, rejected:true };
const genderMap = { male: 'rdp_gender_male', female: 'rdp_gender_female' };

async function loadRequest() {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  try {
    var res = await fetch('/api/requests/' + requestId, { headers: authHeaders() });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    if (res.status === 404) { document.getElementById('loading-state').style.display='none'; document.getElementById('error-state').style.display='block'; document.getElementById('error-message').textContent=t('rdp_msg_requestNotAvailable'); return; }
    if (!res.ok) { throw new Error('HTTP '+res.status); }
    var r = await res.json();
    if (r.requestType === 'bulk_req' && r.bulkRequestId) {
      try {
        var bulkRes = await fetch('/api/bulkregistration/' + r.bulkRequestId, { headers: authHeaders() });
        if (bulkRes.ok) r.bulkDetails = await bulkRes.json();
      } catch(e) { console.error('Failed to load bulk details:', e); }
    }
    if (r.requestType === 'self_registration') {
      try {
        var regRes = await fetch('/api/Registration/my-requests/' + requestId, { headers: authHeaders() });
        if (regRes.ok) { var regData = await regRes.json(); r._regHistory = regData.history || []; }
      } catch(e) { r._regHistory = []; }
    }
    renderRequest(r);
  } catch(e) {
    document.getElementById('loading-state').style.display = 'none';
    document.getElementById('error-state').style.display = 'block';
    document.getElementById('error-message').textContent = t('rdp_msg_loadError');
  }
}

// ============================================================================
//  تعليم الحقول التي عدّلها الطالب بعد «بحاجة معلومات إضافية».
//  المصدر r.studentEdits القادم من WorkflowHistory.changes_json — بيانات منظّمة
//  مش نص، فالتعليم بيشتغل مع أي حقل يتضاف في TrackedFields من غير تعديل هنا.
// ============================================================================
function injectEditStyles() {
  if (document.getElementById('rdpEditStyles')) return;
  var st = document.createElement('style');
  st.id = 'rdpEditStyles';
  st.textContent =
    '.info-field.edited{background:#FFFBF0;border-radius:8px;padding:8px 12px;margin:-8px -4px}' +
    '[dir="rtl"] .info-field.edited{border-right:3px solid #C9A84C}' +
    '[dir="ltr"] .info-field.edited{border-left:3px solid #C9A84C}' +
    '.info-field.edited .info-value{font-weight:700}' +
    '.edited-tag{display:inline-block;margin-inline-start:6px;padding:1px 7px;border-radius:20px;' +
      'background:#C9A84C;color:#3A2E08;font-size:10px;font-weight:700;vertical-align:middle}' +
    '.edited-old{display:block;margin-top:3px;font-size:11.5px;color:#8891A8}' +
    '.edited-old del{color:#991B1B;text-decoration-thickness:1px}' +
    '.edits-banner{display:flex;align-items:flex-start;gap:10px;margin:0 0 14px;padding:11px 14px;' +
      'border-radius:10px;background:#FFFBF0;border:1px solid #EBDCA8;color:#7A5C0B;' +
      'font-size:13px;font-weight:600;line-height:1.7}' +
    '.edits-banner svg{flex-shrink:0;margin-top:2px}';
  document.head.appendChild(st);
}

// خريطة field -> التغيير. mobile و phone نفس الحقل في سجل الطالب.
var STUDENT_EDITS = {};
function buildEditMap(list) {
  STUDENT_EDITS = {};
  (list || []).forEach(function (c) {
    if (!c || !c.field) return;
    STUDENT_EDITS[c.field] = c;
    if (c.field === 'mobile') STUDENT_EDITS.phone = c;
    if (c.field === 'phone') STUDENT_EDITS.mobile = c;
  });
}

// بديل موحّد لكتابة صف البيانات — بيعلّم الصف تلقائيًا لو الحقل اتعدّل
function infoField(fieldKeys, label, valueHtml) {
  var keys = [].concat(fieldKeys || []);
  var hits = keys.map(function (k) { return STUDENT_EDITS[k]; }).filter(Boolean);

  if (!hits.length)
    return '<div class="info-field"><span class="info-label">' + label +
           '</span><span class="info-value">' + valueHtml + '</span></div>';

  var olds = hits.map(function (c) {
    var prev = (c.old === null || c.old === undefined || c.old === '')
      ? tf('rdp_editEmpty', 'فارغ', 'empty') : c.old;
    return (hits.length > 1 ? escHtml(c.label) + ': ' : '') + '<del>' + escHtml(prev) + '</del>';
  }).join(' · ');

  return '<div class="info-field edited"><span class="info-label">' + label +
         '<span class="edited-tag">' + tf('rdp_editedTag', 'مُعدَّل', 'Edited') + '</span></span>' +
         '<span class="info-value">' + valueHtml + '</span>' +
         '<span class="edited-old">' + tf('rdp_editPrev', 'قبل التعديل', 'Before') + ': ' + olds + '</span></div>';
}

function renderRequest(r) {
  injectEditStyles();
  buildEditMap(r.studentEdits);
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('loading-state').style.display = 'none';
  document.getElementById('detail-content').style.display = 'block';
  // ماتخترعش رقم طلب — الرقم المصنوع هنا مكانش متخزّن، والطالب كان بيكتبه في
  // صفحة التتبع فمايتلاقاش. الرقم بقى بيتولّد ويتخزّن وقت إنشاء الطلب.
  var reqNum = r.requestNumber || '—';
  var __pt = document.getElementById('pageTitle'); if (__pt) __pt.textContent = t('rdp_pageTitle')+' - '+reqNum;

  var s = r.student || {};
  var st = (r.status||'').toLowerCase();
  var stageLabel = (stageNames[st]&&t(stageNames[st]))||st;
  var isRejected = isRejectedStatus[st];
  var workflowIdx = stageToWorkflowStep[st] !== undefined ? stageToWorkflowStep[st] : 0;

  /* --- Workflow bar --- */
  var isSelfReg = r.requestType === 'self_registration';
  var wfSteps = isSelfReg ? selfRegWorkflowSteps : workflowSteps;
  var wfHtml = wfSteps.map(function(step,i) {
    var cls = '';
    if (isRejected && st.indexOf('rejected') > -1) {
      cls = (i === workflowIdx) ? ' rejected' : '';
    } else if (i < workflowIdx) {
      cls = ' completed';
    } else if (i === workflowIdx) {
      cls = ' active';
    }
    var label = step.key === 'submitted' ? t('rdp_step_submitted')
      : step.key === 'housing' ? t('rdp_wf_housingApp')
      : step.key === 'cyber_review' ? t('rdp_stage_cyberReview')
      : step.key === 'cyber_approved' ? t('rdp_wf_cyberApprovedReady')
      : step.key === 'ready' ? t('rdp_wf_readyHousing')
      : t('rdp_stage_completed');
    var iconSvg = cls==='completed'
      ? '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="8" fill="#0F6E56"/><path d="M4.5 8L7 10.5L11.5 6" stroke="white" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>'
      : cls==='rejected'
        ? '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="8" fill="#DC2626"/><path d="M5.5 5.5L10.5 10.5M10.5 5.5L5.5 10.5" stroke="white" stroke-width="1.5" stroke-linecap="round"/></svg>'
        : cls==='active'
          ? '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="7" fill="#1B2A5E" stroke="#1B2A5E" stroke-width="2"/><circle cx="8" cy="8" r="3" fill="white"/></svg>'
          : '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="7" stroke="#CBD5E1" stroke-width="1.5" fill="none"/></svg>';
    return '<div class="workflow-step'+cls+'"><div class="workflow-circle">'+iconSvg+'</div><div class="workflow-step-label">'+label+'</div></div>';
  }).join('');

  /* --- Review history entries (enterprise timeline) --- */
  var historyEntries = [];
  if (r.requestType === 'self_registration' && r._regHistory) {
    r._regHistory.forEach(function(h, idx) {
      var wasRejected = String(h.toStage || '').indexOf('rejected') > -1;
      // «طلب معلومات إضافية» مش موافقة ولا رفض — كانت بتتسمّى بالخطأ
      // «موافقة إدارة الإسكان» لأن الاسم كان بيتحدد من fromStage بفرضية إن
      // أي خطوة مش رفض تبقى موافقة.
      var isInfo = h.toStage === 'need_more_info';
      var hlbl;
      if (idx === 0) {
        hlbl = t('rdp_step_submitted');
      } else if (isInfo) {
        hlbl = t('rdp_stage_needMoreInfo');
      } else {
        var act = actionNames[h.fromStage];
        // احتياطي للسجلات القديمة اللي مالهاش fromStage محفوظ
        var key = act ? (wasRejected ? act.no : act.ok) : stageNames[h.toStage];
        hlbl = key ? t(key) : (h.toStage || '');
      }
      // أول سطر = تقديم الطلب، والاسم فيه لازم يكون اسم الطالب مش اسم حساب الدخول.
      // h.actorName بيرجّع صاحب الحساب اللي قدّم (ممكن يكون موظف سجّل نيابة عنه،
      // أو حساب OTP اسمه "طالب")، فالسجل كان بيعرض اسم غير صاحب الطلب.
      var actor = (idx === 0 ? (s.full_name || s.fullNameArabic || h.actorName) : h.actorName) || '';
      historyEntries.push({ label: hlbl, cls: isInfo ? 'info' : (wasRejected ? 'rejected' : 'approved'), time: h.actionDate, notes: h.notes || null, username: actor, isSubmitter: idx === 0 });
    });
  } else {
    if (r.submittedAt) {
      // مقدّم الطلب = اسم الطالب نفسه. r.submittedByName هو اسم حساب الدخول، واللي في
      // التسجيل الذاتي بيتخلق من مسار الـ OTP باسم "طالب" لو الطالب لسه مش مسجّل في
      // جدول الطلاب — فالسجل كان بيعرض "طالب" بدل اسم صاحب الطلب.
      historyEntries.push({ label: t('rdp_tl_requestCreated'), cls:'approved', time:r.submittedAt, notes:null, username: (s.fullNameArabic || s.full_name || r.submittedByName), isSubmitter:true });
    }
    if (r.housingReviewedAt) {
      var housingLabel = st === 'housing_rejected'
        ? t('rdp_tl_housingRejected')
        : t('rdp_tl_assignedToCyber');
      historyEntries.push({ label: housingLabel, cls: st==='housing_rejected'?'rejected':'approved', time:r.housingReviewedAt, notes: st==='housing_rejected'?r.housingNotes:null, username: r.housingReviewedByName });
    }
    if (r.cyberReviewedAt) {
      var cyberLabel = st === 'cyber_rejected'
        ? t('rdp_stage_cyberRejected')
        : t('rdp_stage_cyberApproved');
      historyEntries.push({ label: cyberLabel, cls: st==='cyber_rejected'?'rejected':'approved', time:r.cyberReviewedAt, notes: st==='cyber_rejected'?r.cyberNotes:null, username: r.cyberReviewedByName });
    }
    if (r.readyForProvisioningAt) {
      historyEntries.push({ label: t('rdp_stage_readyForProvisioning'), cls:'approved', time:r.readyForProvisioningAt, notes:null, username: r.readyForProvisioningByName });
    }
    if (r.completedAt) {
      historyEntries.push({ label: t('rdp_stage_completed'), cls:'approved', time:r.completedAt, notes:null, username: r.completedByName });
    }
  }

  function fmtDate(t) { return new Date(t).toLocaleDateString(lang==='ar'?'ar-SA':'en-US', { year:'numeric', month:'short', day:'numeric' }); }
  function fmtTime(t) { return new Date(t).toLocaleTimeString(lang==='ar'?'ar-SA':'en-US', { hour:'2-digit', minute:'2-digit' }); }

  var historyHtml = historyEntries.map(function(e) {
    var title = e.label;
    var dotCls = e.cls;
    var dateStr = fmtDate(e.time);
    var timeStr = fmtTime(e.time);
    // "مقدّم الطلب" لخطوة التقديم، و"بواسطة" لباقي المراحل — دي إجراءات موظفين مش تقديم.
    // tf بترجع نص احتياطي لو المفتاح لسه مش موجود في الـ .resx: ملفات الـ resx بتتجمّع
    // جوه الـ DLL فمابتوصلش غير مع النشر، لكن ملف الـ JS ده بيتحدّث فورًا — من غير
    // الاحتياطي ده كان هيظهر اسم المفتاح نفسه في الواجهة لحد أول نشر.
    var userLabel = e.isSubmitter
      ? tf('rdp_lbl_submitter', 'مقدّم الطلب:', 'Submitted by:')
      : tf('rdp_lbl_actionBy',  'بواسطة:',      'By:');
    var userHtml = e.username ? '<div class="tl-row"><span class="tl-label">'+userLabel+'</span><span class="tl-value username">'+escHtml(e.username)+'</span></div>' : '';
    // الملاحظات بتظهر في خطوات الرفض *وطلب المعلومات* — دول الخطوتين اللي
    // الملاحظة فيهم هي المحتوى نفسه. ملاحظات الموافقة داخلية.
    var showNotes = e.cls === 'rejected' || e.cls === 'info';
    var notesHtml = '<div class="tl-notes">'+t('rdp_lbl_notes')+' '+(e.notes && showNotes?escHtml(e.notes):t('rdp_msg_noNotes'))+'</div>';
    // اللون البرتقالي للنقطة — مافيش كلاس ليه في site.css فبيتحط هنا مباشرة
    var dotStyle = e.cls === 'info' ? ' style="border-color:#E65100;background:#E65100"' : '';
    return '<div class="tl-item"><div class="tl-dot '+dotCls+'"'+dotStyle+'></div><div class="tl-content"><div class="tl-title">'+title+'</div>'+userHtml+'<div class="tl-row"><span class="tl-label">'+t('rdp_lbl_date')+'</span><span class="tl-value">'+dateStr+' - '+timeStr+'</span></div>'+notesHtml+'</div></div>';
  }).join('');
  if (!historyHtml) {
    historyHtml = '<div class="history-empty">'+t('rdp_msg_noReviews')+'</div>';
  }

  /* --- Rejection info banner --- */
  var rejectionNotes = st === 'cyber_rejected' ? r.cyberNotes : (st === 'housing_rejected' ? r.housingNotes : (st === 'rejected' ? r.notes : null));
  var rejectionHtml = rejectionNotes
    ? '<div class="rejection-info">'+
      '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#991B1B" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>'+
      '<div><div class="rejection-info-label">'+
      t('rdp_lbl_rejectionReason')+'</div><div class="rejection-info-text">'+
      escHtml(rejectionNotes)+'</div></div></div>'
    : '';

  // "مقدّم من" في التسجيل الذاتي = الطالب نفسه، مش حساب الدخول اللي اتسجّل عليه
  // الإجراء. أما لو موظف سجّل نيابة عن الطالب (تسجيل فردي/جماعي) فاسم الموظف هو
  // المعلومة المفيدة فعلاً هنا.
  var submittedByDisplay = (r.requestType === 'self_registration')
    ? (s.full_name || s.fullNameArabic || r.submittedByName || '')
    : (r.submittedByName || '');

  /* --- Review actions --- */
  var reviewHtml = '';
  // مين يقدر يتصرّف = مرحلة الطلب + صلاحية المستخدم، مش دوره. نفس الجدول
  // بالظبط مطبّق على السيرفر في WorkflowActionService و RequestService.ReviewAsync.
  var canActOnStage =
    ((st === 'pending_supervisor' || st === 'submitted') && can('requests.reviewHousing')) ||
    ((st === 'pending_cyber' || st === 'cyber_review') && can('requests.reviewCyber')) ||
    (st === 'housing_approved' && can('requests.complete')) ||
    (st === 'cyber_approved' && (can('requests.reviewCyber') || can('requests.complete'))) ||
    (st === 'ready_for_provisioning' && can('requests.complete'));

  if (canActOnStage) {
    var isSelfRegAction = st === 'pending_supervisor' || st === 'pending_cyber' || st === 'ready_for_provisioning';
    var actionLabel, actionDesc, nextApproved, showRequestInfo;
    if (isSelfRegAction) {
      if (st === 'pending_supervisor') {
        actionLabel = t('rdp_action_approveRequest');
        actionDesc = t('rdp_actionDesc_approveToCyber');
        nextApproved = 'pending_cyber';
        showRequestInfo = true;
      } else if (st === 'pending_cyber') {
        actionLabel = t('rdp_action_approveRequest');
        actionDesc = t('rdp_actionDesc_approveToNetwork');
        nextApproved = 'ready_for_provisioning';
      } else {
        actionLabel = t('rdp_action_completeRequest');
        actionDesc = t('rdp_actionDesc_approveComplete');
        nextApproved = 'completed';
      }
    } else if (st === 'housing_approved') {
      actionLabel = t('rdp_action_sendToCyber');
      actionDesc = t('rdp_actionDesc_submitToCyber');
      nextApproved = 'cyber_review';
    } else if (st === 'cyber_review') {
      // مراجع الأمن السيبراني بيشوف صيغة أوضح لدوره؛ الأدمن (اللي عنده إكمال
      // الطلب كمان) بيشوف الصيغة العامة.
      var _cyberVoice = can('requests.reviewCyber') && !can('requests.complete');
      actionLabel = _cyberVoice
        ? t('rdp_action_approveHousingAccount')
        : t('rdp_action_agreeRequest');
      actionDesc = _cyberVoice
        ? t('rdp_actionDesc_approveNextStage')
        : t('rdp_actionDesc_approveNextStageShort');
      nextApproved = 'cyber_approved';
    } else if (st === 'cyber_approved') {
      actionLabel = t('rdp_action_markReady');
      actionDesc = t('rdp_actionDesc_markReady');
      nextApproved = 'ready_for_provisioning';
    } else if (st === 'ready_for_provisioning') {
      actionLabel = t('rdp_action_completeRequest');
      actionDesc = t('rdp_actionDesc_completeNetwork');
      nextApproved = 'completed';
    }
    var rejectNewStatus = isSelfRegAction ? 'rejected' : (st === 'housing_approved' ? 'housing_rejected' : (st === 'cyber_review' ? 'cyber_rejected' : ''));
    reviewHtml = '<div class="card" id="reviewSection"><div class="card-header">'+
      t('rdp_card_reviewActions')+'</div><div class="card-body">'+
      '<div class="review-actions">'+
        '<label class="review-option" id="optApprove" onclick="selectApprove()">'+
          '<input type="radio" name="reviewDecision" value="approve" onchange="selectApprove()">'+
          '<div><div class="review-option-text">'+actionLabel+'</div>'+
          '<div class="review-option-desc">'+actionDesc+'</div></div>'+
        '</label>'+
        // الترتيب: اعتماد ← رفض ← طلب معلومات إضافية.
        // الرفض قرار نهائي فمكانه جنب الاعتماد؛ وطلب المعلومات تعليق مؤقت
        // فآخر حاجة، عشان ما يتاخدش بالغلط بدل الرفض.
        (rejectNewStatus ? '<label class="review-option" id="optReject" onclick="selectReject()">'+
          '<input type="radio" name="reviewDecision" value="reject" onchange="selectReject()">'+
          '<div><div class="review-option-text">'+t('rdp_option_rejectRequest')+'</div>'+
          '<div class="review-option-desc">'+t('rdp_optionDesc_rejectReason')+'</div></div>'+
        '</label>' : '')+
        (showRequestInfo ? '<label class="review-option" id="optInfo" onclick="selectInfo()">'+
          '<input type="radio" name="reviewDecision" value="request-info" onchange="selectInfo()">'+
          '<div><div class="review-option-text">'+t('rdp_option_needMoreInfo')+'</div>'+
          '<div class="review-option-desc">'+t('rdp_optionDesc_needMoreInfo')+'</div></div>'+
        '</label>' : '')+
        '<div class="reject-reason-field" id="rejectReasonField">'+
          '<label id="reasonLabel" style="font-size:13px;font-weight:600;color:var(--navy-dark);margin-bottom:6px;display:block">'+
            t('rdp_lbl_rejectionReasonRequired')+'</label>'+
          '<textarea id="rejectReason" placeholder="'+t('rdp_ph_rejectionReason')+'" oninput="updateReviewSubmitState()"></textarea>'+
          '<div class="error-msg" id="rejectReasonError">'+t('rdp_err_rejectionReasonRequired')+'</div>'+
        '</div>'+
        '<button class="submit-review-btn" id="submitReviewBtn" disabled onclick="submitReview(\''+st+'\')">'+
          t('rdp_btn_submitReview')+'</button>'+
      '</div></div></div>';
  }

  /* --- لافتة تنبيه أعلى بيانات الطالب لما يكون فيه تعديل من الطالب --- */
  var editsBanner = '';
  if (r.studentEdits && r.studentEdits.length) {
    var __n = r.studentEdits.length;
    var __when = r.studentEditedAt ? new Date(r.studentEditedAt).toLocaleString(lang === 'en' ? 'en-GB' : 'ar-SA') : '';
    editsBanner =
      '<div class="edits-banner" style="grid-column:1/-1">' +
        '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
        '<path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>' +
        '<span>' + tf('rdp_editsBanner1', 'عدّل الطالب ', 'The student edited ') + __n +
        tf('rdp_editsBanner2', ' حقلًا بعد طلب المعلومات الإضافية. الحقول المعلّمة بـ «مُعدَّل» هي التي تغيّرت.',
                               ' field(s) after the request for more information. Fields tagged “Edited” are the ones that changed.') +
        (__when ? '<br><span style="font-weight:500;opacity:.85">' + escHtml(__when) + '</span>' : '') +
        '</span></div>';
  }

  /* --- Housing assignment info (for completed/approved requests) --- */
  var housingHtml = '';
  if (s.housing_building || s.floor_number || s.room_number || s.apartment_number) {
    // كل جزء بيتكتب بليبله — «مبنى 65 · الدور 1 · شقة 36 · غرفة 12».
    // الصيغة القديمة (65 - 12 - شقة 36) كانت بترتّب غرفة قبل شقة وبتسيب المبنى والدور
    // بدون ليبل، فمحدش يعرف الرقم ده بتاع إيه.
    var housingParts = [];
    if (s.housing_building) housingParts.push(tf('loc_building','مبنى','Building')+' '+escHtml(s.housing_building));
    // الدور متخزّن كود ("0" = الأرضي) عشان الترتيب يفضل رقمي — بيتترجم هنا بس
    if (s.floor_number !== null && s.floor_number !== undefined && s.floor_number !== '') {
      var __fl = String(s.floor_number) === '0' ? tf('reg_optFloorGround', 'الأرضي', 'Ground') : escHtml(s.floor_number);
      housingParts.push(tf('loc_floor', 'الدور', 'Floor') + ' ' + __fl);
    }
    if (s.apartment_number) housingParts.push(tf('loc_apartment','شقة','Apt')+' '+escHtml(s.apartment_number));
    if (s.room_number) housingParts.push(tf('loc_room','غرفة','Room')+' '+escHtml(s.room_number));
    housingHtml = infoField(['housing_building','floor_number','apartment_number','room_number'],
      t('rdp_field_housing'), housingParts.join(' · '));
  }

  document.getElementById('detail-content').innerHTML =
    /* 1 - Request Info header card */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>'+
      t('rdp_card_requestInfo')+'</div><div class="card-body"><div class="info-grid">'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_requestNumber')+'</span><span class="info-value">'+reqNum+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_requestType')+'</span><span class="info-value">'+escHtml(requestTypeName(r.requestType))+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_status')+'</span><span class="info-value"><span class="badge badge-'+st+'">'+(statusMap[st]?t(statusMap[st]):r.status)+'</span></span></div>'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_submittedBy')+'</span><span class="info-value">'+escHtml(submittedByDisplay)+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_submittedDate')+'</span><span class="info-value">'+(r.submittedAt?new Date(r.submittedAt).toLocaleDateString(lang==='ar'?'ar-SA':'en-US'):'')+'</span></div>'+
    '</div></div></div>'+

    /* 1b - Bulk Request Details (only for bulk_req) */
    (r.requestType === 'bulk_req' && r.bulkDetails ? function(){
      var bd = r.bulkDetails;
      var bStudents = bd.students || [];
      var total = bStudents.length;
      var maleCnt = bStudents.filter(function(x){ return (x.gender||'').toLowerCase() === 'male'; }).length;
      var femaleCnt = bStudents.filter(function(x){ return (x.gender||'').toLowerCase() === 'female'; }).length;
      var bulkStatusMap = { pending: t('rdp_bulkStatus_pending'), processing: t('rdp_bulkStatus_processing'), completed: t('rdp_stage_completed') };
      var previewHtml = bStudents.slice(0, 50).map(function(bs, i){
        return '<tr><td>'+(i+1)+'</td><td>'+escHtml(bs.studentID||'')+'</td><td>'+escHtml(bs.fullNameArabic||'')+'</td><td>'+escHtml(bs.fullNameEnglish||'')+'</td><td>'+escHtml(collegeName(bs.college))+'</td><td>'+escHtml(bs.academicLevel||'')+'</td></tr>';
      }).join('');

      var bulkCards =
        /* Bulk Details card */
        '<div class="card"><div class="card-header">'+
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>'+
          t('rdp_card_bulkDetails')+'</div><div class="card-body"><div class="info-grid">'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_brqNumber')+'</span><span class="info-value">'+escHtml(bd.requestNumber||reqNum)+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_fileName')+'</span><span class="info-value">'+escHtml(bd.fileName||'')+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_totalStudents')+'</span><span class="info-value">'+total+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_validCount')+'</span><span class="info-value">'+(bd.validCount||0)+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_errors')+'</span><span class="info-value">'+(bd.errorCount||0)+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_status')+'</span><span class="info-value">'+(bulkStatusMap[bd.status]||bd.status||'')+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_createdDate')+'</span><span class="info-value">'+(bd.createdDate?new Date(bd.createdDate).toLocaleDateString(lang==='ar'?'ar-SA':'en-US'):'')+'</span></div>'+
        '</div></div></div>'+

        /* Summary card */
        '<div class="card"><div class="card-header">'+
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg>'+
          t('rdp_card_studentSummary')+'</div><div class="card-body"><div class="info-grid">'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_totalStudents')+'</span><span class="info-value">'+total+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_maleStudents')+'</span><span class="info-value">'+maleCnt+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+t('rdp_field_femaleStudents')+'</span><span class="info-value">'+femaleCnt+'</span></div>'+
        '</div></div></div>'+

        /* Students preview table */
        (bStudents.length > 0 ? '<div class="card"><div class="card-header">'+
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg>'+
          t('rdp_card_studentList')+'</div><div style="overflow-x:auto"><table><thead><tr>'+
          '<th>'+(lang==='ar'?'#':'#')+'</th>'+
          '<th>'+t('rdp_field_studentId')+'</th>'+
          '<th>'+t('rdp_field_nameAr')+'</th>'+
          '<th>'+t('rdp_field_nameEn')+'</th>'+
          '<th>'+t('rdp_field_college')+'</th>'+
          '<th>'+t('rdp_field_level')+'</th>'+
          '</tr></thead><tbody>'+previewHtml+'</tbody></table></div></div>' : '');
      return bulkCards;
    }() : '')+

    /* 2 - Student Info (personal) */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>'+
      t('rdp_card_studentInfo')+'</div><div class="card-body"><div class="info-grid">'+
      editsBanner+
      infoField('full_name',         t('rdp_field_nameAr'),     escHtml(s.full_name||''))+
      infoField('full_name_english', t('rdp_field_nameEn'),     escHtml(s.full_name_english||''))+
      infoField(null,                t('rdp_field_studentId'),  escHtml(s.student_id||''))+
      infoField('national_id',       t('rdp_field_nationalId'), escHtml(s.national_id||''))+
      infoField('phone',             t('rdp_field_mobile'),     escHtml(s.phone||''))+
      housingHtml+
    '</div></div></div>'+

    /* 3 - Academic Info */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>'+
      t('rdp_card_academicInfo')+'</div><div class="card-body"><div class="info-grid">'+
      infoField('college',        t('rdp_field_college'),    escHtml(collegeName(s.college)))+
      infoField('department',     t('rdp_field_department'), escHtml(deptName(s.department)))+
      infoField('academic_level', t('rdp_field_level'),      escHtml(s.academic_level||''))+
      infoField('gender',         t('rdp_field_gender'),     escHtml((genderMap[s.gender]&&t(genderMap[s.gender]))||s.gender||''))+
    '</div></div></div>'+

    /* 3b - Housing Account Card */
    '<div class="detail-card" id="housingAccountCard" style="margin-top:16px;display:none">'+
      '<div class="detail-card-header">'+
        '<h3 data-i18n="housingAccounts">'+t('rdp_housingAccounts')+'</h3>'+
      '</div>'+
      '<div class="detail-card-content">'+
        '<div id="housingAccountContent">'+
          '<p style="color:var(--gray-500);text-align:center;padding:16px" data-i18n="loading">'+t('rdp_loading')+'</p>'+
        '</div>'+
      '</div>'+
    '</div>'+

    /* 4 - Workflow Timeline (horizontal bar) */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>'+
      t('rdp_card_workflow')+'</div><div class="card-body">'+
      '<div style="padding:8px 0"><div class="workflow-bar">'+wfHtml+'</div></div>'+
      rejectionHtml+
    '</div></div>'+

    /* 4b - Completion success message (only for completed/approved) */
    (st === 'completed' || st === 'approved' ? '<div class="card" style="border:2px solid var(--green);background:var(--green-light)"><div class="card-body" style="text-align:center;padding:24px">'+
      '<svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#0F6E56" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-bottom:12px"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>'+
      '<h3 style="font-size:18px;color:var(--green);margin-bottom:8px">'+t('rdp_msg_accountCreated')+'</h3>'+
      '<p style="font-size:14px;color:var(--navy-dark)">'+t('rdp_lbl_username')+': <strong dir="ltr" style="display:inline-block;background:var(--white);padding:4px 12px;border-radius:6px;border:1px solid var(--gray-200)">'+(s.ad_username || '-')+'</strong></p>'+
      '<p style="font-size:12px;color:var(--gray-500);margin-top:8px">'+t('rdp_msg_credentialsSms')+'</p>'+
    '</div></div>' : '')+

    /* 5 - Review History */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>'+
      t('rdp_card_reviewHistory')+'</div><div class="card-body"><div class="review-timeline">'+
      historyHtml+
    '</div></div></div>'+

    /* 6 - Review Actions */
    reviewHtml;

  loadHousingAccount(s.id);

}

function closeHousingLifecycle() {
  document.getElementById('housingLifecycleModal').classList.remove('open');
}

async function loadHousingAccount(studentId) {
  var card = document.getElementById('housingAccountCard');
  var content = document.getElementById('housingAccountContent');
  if (!studentId) { card.style.display = 'none'; return; }
  if (!can('housing.view')) { card.style.display = 'none'; return; }
  try {
    var res = await fetch('/api/HousingAccountManagement/' + studentId, { headers: authHeaders() });
    if (!res.ok) { card.style.display = 'none'; return; }
    var data = await res.json();
    var s = data.student || {};
    if (!s.ad_username) { card.style.display = 'none'; return; }
    card.style.display = 'block';
    var ad = data.adDetails || {};
    var enabled = s.ad_status === 'enabled';
    var statusBadge = enabled ? '<span style="background:#E1F5EE;color:#0F6E56;padding:2px 8px;border-radius:999px;font-size:11px;font-weight:600">' + t('adEnabled') + '</span>'
      : (s.ad_status === 'disabled' ? '<span style="background:#FEF2F2;color:#991B1B;padding:2px 8px;border-radius:999px;font-size:11px;font-weight:600">' + t('adDisabled') + '</span>'
      : '<span style="background:#F4F6FB;color:#8891A8;padding:2px 8px;border-radius:999px;font-size:11px;font-weight:600">' + (s.ad_status || t('adUnknown')) + '</span>');
    // ⚠️ الكلاسات housing-info-grid / housing-info-item مالهاش أي CSS في المشروع،
    //    فالعنوان كان بيلزق في القيمة: "اسم المستخدم في ADh456969999".
    //    بنستخدم info-grid / info-field اللي بتستخدمها باقي بطاقات الصفحة —
    //    عمودين مرتبين بخط فاصل، نفس شكل «معلومات الطالب» بالظبط.
    function adField(label, valueHtml, extra) {
      return '<div class="info-field">' +
               '<span class="info-label">' + label + '</span>' +
               '<span class="info-value"' + (extra || '') + '>' + valueHtml + '</span>' +
             '</div>';
    }

    content.innerHTML = '<div class="info-grid">' +
      adField(t('adUsername'), escHtml(s.ad_username || '-'), ' dir="ltr" style="text-align:start"') +
      adField(t('adAccountStatus'), statusBadge) +
      adField(t('adLastSync'), s.ad_last_sync_at ? formatDate(s.ad_last_sync_at) : '-') +
      adField(t('college'), escHtml(collegeName(s.college) || '-')) +
    '</div>' +
    '<div style="margin-top:12px;display:flex;gap:8px;flex-wrap:wrap">' +
      (can('housing.manageAccounts') ? (enabled ? '<button class="btn btn-danger btn-sm" onclick="housingAction(' + studentId + ',\'disable\')">' + t('disableAccount') + '</button>'
               : '<button class="btn btn-success btn-sm" onclick="housingAction(' + studentId + ',\'enable\')">' + t('enableAccount') + '</button>') : '') +
      (can('housing.manageAccounts') ? '<button class="btn btn-warning btn-sm" onclick="housingResetPassword(' + studentId + ')">' + t('resetPassword') + '</button>' : '') +
      // سجل دورة حياة الحساب بيتقرا من /api/students/{id}/lifecycle، فمحتاج صلاحية عرض الطلاب
      (can('students.view') ? '<button class="btn btn-outline btn-sm" onclick="showHousingLifecycle(' + (s.id || studentId) + ',\'' + (s.full_name_english || '') + '\')">📋 ' + t('lifecycleLog') + '</button>' : '') +
    '</div>';
  } catch(e) {
    card.style.display = 'none';
  }
}

async function housingAction(studentId, action) {
  var msgs = { enable: t('confirmEnable'), disable: t('confirmDisable') };
  if (!confirm(msgs[action] || t('confirm'))) return;
  if (!can('housing.manageAccounts')) { alert(t('apiError')); return; }
  try {
    var res = await fetch('/api/HousingAccountManagement/' + studentId + '/' + action, {
      method: 'POST', headers: authHeaders({ 'Content-Type': 'application/json' })
    });
    if (res.ok) {
      alert(t('success'));
      loadHousingAccount(studentId);
    } else {
      var d = await res.json().catch(function(){return{};});
      alert(d.message || t('errorOccurred'));
    }
  } catch(e) { alert(t('apiError')); }
}

function housingResetPassword(studentId) {
  if (!can('housing.manageAccounts')) { alert(t('apiError')); return; }
  var pwd = prompt(t('newPassword'));
  if (!pwd || pwd.length < 8) { alert(t('passwordMinLength') || 'Password must be at least 8 characters'); return; }
  (async function() {
    try {
      var res = await fetch('/api/HousingAccountManagement/' + studentId + '/reset-password', {
        method: 'POST', headers: authHeaders({ 'Content-Type': 'application/json' }),
        body: JSON.stringify({ newPassword: pwd })
      });
      if (res.ok) { alert(t('success')); } else { var d = await res.json(); alert(d.message || t('errorOccurred')); }
    } catch(e) { alert(t('apiError')); }
  })();
}

// أسماء إجراءات سجل دورة حياة حساب الشبكة — tf بترجع نص ملف الترجمة لو موجود
// وإلا النص المكتوب هنا حسب اللغة.
function lifecycleActionLabel(action) {
  switch (action) {
    case 'provisioned':            return tf('actionProvisioned', 'تم إنشاء الحساب', 'Account created');
    case 'reprovisioned':          return tf('actionReprovisioned', 'إعادة إنشاء الحساب', 'Account re-provisioned');
    case 'enabled':                return tf('actionEnabled', 'تم التفعيل', 'Enabled');
    case 'disabled':               return tf('actionDisabled', 'تم التعطيل', 'Disabled');
    case 'disable_failed':         return tf('actionDisableFailed', 'فشل تعطيل الحساب', 'Disable failed');
    case 'password_reset':         return tf('actionPasswordReset', 'إعادة تعيين كلمة المرور', 'Password reset');
    case 'extension_attrs_synced': return tf('actionExtensionAttrsSynced', 'مزامنة الخصائص الإضافية', 'Extension attributes synced');
    case 'housing_transfer':       return tf('actionHousingTransfer', 'نقل سكن', 'Housing transfer');
    case 'left_housing':           return tf('actionLeftHousing', 'ترك الإسكان', 'Left housing');
    default:                       return action || '';
  }
}

async function showHousingLifecycle(studentId, name) {
  var body = document.getElementById('housingLifecycleBody');
  body.innerHTML = '<p style="text-align:center;color:var(--gray-500);padding:20px">' + t('loading') + '</p>';
  document.getElementById('housingLifecycleModal').classList.add('open');
  if (!can('students.view')) { body.innerHTML = '<p style="text-align:center;color:var(--red);padding:20px">' + t('apiError') + '</p>'; return; }
  try {
    var res = await fetch('/api/students/' + studentId + '/lifecycle', { headers: authHeaders() });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    var data = await res.json();
    var logs = data.logs || [];
    if (logs.length === 0) { body.innerHTML = '<p style="text-align:center;color:var(--gray-500);padding:20px">' + t('noData') + '</p>'; return; }
    var html = '';
    if (name) html += '<div style="font-size:14px;font-weight:700;color:var(--navy);margin-bottom:12px">' + escHtml(name) + '</div>';
    html += '<div class="log-list">';
    logs.forEach(function(l) {
      var iconClass = (l.action === 'disabled' || l.action === 'disable_failed') ? 'disabled'
                    : (l.action === 'password_reset') ? 'password_reset' : 'enabled';
      // ⚠️ الأكواد اللي مالهاش اسم (housing_transfer / left_housing / disable_failed)
      //    كانت بتظهر للمستخدم زي ما هي مكتوبة في قاعدة البيانات.
      var actionLabel = lifecycleActionLabel(l.action);
      // ⚠️ كان كله في سطر واحد مفصول بـ | فالكلام بيدخل في بعضه. بقى:
      //    الإجراء، تحته التفاصيل، وتحتهم المنفّذ والتاريخ منفصلين.
      var meta = '';
      if (l.performerName) meta += '<span>' + t('by') + ' ' + escHtml(l.performerName) + '</span>';
      if (l.performedAt) meta += '<span>' + new Date(l.performedAt).toLocaleString() + '</span>';
      if (l.ipAddress) meta += '<span class="log-ip">IP: ' + escHtml(l.ipAddress) + '</span>';

      html += '<div class="log-entry">' +
          '<div class="log-icon ' + iconClass + '">' + (l.action === 'disabled' ? '\u2715' : '\u2713') + '</div>' +
          '<div class="log-details">' +
            '<div class="log-action">' + escHtml(actionLabel) + '</div>' +
            (l.details ? '<div class="log-desc">' + escHtml(l.details) + '</div>' : '') +
            '<div class="log-meta">' + meta + '</div>' +
          '</div>' +
        '</div>';
    });
    body.innerHTML = html + '</div>';
  } catch(e) {
    body.innerHTML = '<p style="color:var(--red);text-align:center;padding:20px">' + t('apiError') + '</p>';
  }
}

var selectedDecision = null;
// القرارات اللي محتاجة ملاحظات مكتوبة قبل ما الزرار يشتغل.
// الرفض بديهي، و"طلب معلومات إضافية" زيّه: بتقول للطالب محتاج إيه بالظبط،
// وقبل كده كان بيتبعت بملاحظات فاضية فالطالب مايعرفش يعمل إيه.
var DECISIONS_NEEDING_NOTES = ['reject', 'request-info'];

function updateReviewSubmitState() {
  var btn = document.getElementById('submitReviewBtn');
  if (!btn) return;
  var ok = false;
  if (selectedDecision === 'approve') {
    ok = true;
  } else if (DECISIONS_NEEDING_NOTES.indexOf(selectedDecision) > -1) {
    var ta = document.getElementById('rejectReason');
    ok = !!(ta && ta.value.trim());
  }
  btn.disabled = !ok;
}

// نص خانة الملاحظات بيتغيّر حسب القرار — "سبب الرفض" مش نفس "المطلوب من الطالب"
function setReasonTexts(kind) {
  var lbl = document.getElementById('reasonLabel');
  var ta  = document.getElementById('rejectReason');
  var er  = document.getElementById('rejectReasonError');
  if (kind === 'info') {
    if (lbl) lbl.textContent = tf('rdp_lbl_infoNeededRequired', 'المعلومات المطلوبة من الطالب *', 'Information required from the student *');
    if (ta)  ta.placeholder  = tf('rdp_ph_infoNeeded', 'يرجى تحديد المعلومات أو المستندات المطلوبة بدقة', 'Describe exactly what is missing');
    if (er)  er.textContent  = tf('rdp_err_infoNeededRequired', 'الرجاء كتابة المعلومات المطلوبة', 'Please describe the required information');
  } else {
    if (lbl) lbl.textContent = t('rdp_lbl_rejectionReasonRequired');
    if (ta)  ta.placeholder  = t('rdp_ph_rejectionReason');
    if (er)  er.textContent  = t('rdp_err_rejectionReasonRequired');
  }
}

function selectApprove() {
  selectedDecision = 'approve';
  var el;
  document.getElementById('optApprove').className = 'review-option selected-approve';
  if (el = document.getElementById('optInfo')) el.className = 'review-option';
  if (el = document.getElementById('optReject')) el.className = 'review-option';
  document.getElementById('rejectReasonField').classList.remove('visible');
  document.getElementById('rejectReason').classList.remove('error');
  document.getElementById('rejectReasonError').style.display = 'none';
  updateReviewSubmitState();
}
function selectReject() {
  selectedDecision = 'reject';
  document.getElementById('optApprove').className = 'review-option';
  var el;
  if (el = document.getElementById('optInfo')) el.className = 'review-option';
  if (el = document.getElementById('optReject')) el.className = 'review-option selected-reject';
  setReasonTexts('reject');
  document.getElementById('rejectReasonField').classList.add('visible');
  updateReviewSubmitState();
}
function selectInfo() {
  selectedDecision = 'request-info';
  document.getElementById('optApprove').className = 'review-option';
  document.getElementById('optInfo').className = 'review-option selected-approve';
  var el;
  if (el = document.getElementById('optReject')) el.className = 'review-option';
  // "طلب معلومات إضافية" بيفتح نفس خانة الملاحظات بنص مختلف — لازم يكتب المطلوب
  setReasonTexts('info');
  document.getElementById('rejectReasonField').classList.add('visible');
  document.getElementById('rejectReason').classList.remove('error');
  document.getElementById('rejectReasonError').style.display = 'none';
  updateReviewSubmitState();
}

async function submitReview(currentStatus) {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  if (!selectedDecision) return;

  var isSelfRegAction = currentStatus === 'pending_supervisor' || currentStatus === 'pending_cyber' || currentStatus === 'ready_for_provisioning';
  var newStatus, reason = '';

  if (selectedDecision === 'approve') {
    if (isSelfRegAction) {
      if (currentStatus === 'pending_supervisor') newStatus = 'pending_cyber';
      else if (currentStatus === 'pending_cyber') newStatus = 'ready_for_provisioning';
      else if (currentStatus === 'ready_for_provisioning') newStatus = 'completed';
      else return;
    } else if (currentStatus === 'housing_approved') {
      newStatus = 'cyber_review';
    } else if (currentStatus === 'cyber_review') {
      newStatus = 'cyber_approved';
    } else if (currentStatus === 'cyber_approved') {
      newStatus = 'ready_for_provisioning';
    } else if (currentStatus === 'ready_for_provisioning') {
      newStatus = 'completed';
    } else {
      return;
    }
  } else if (selectedDecision === 'request-info') {
    if (currentStatus !== 'pending_supervisor') return;
    newStatus = 'need_more_info';
    // الملاحظات هي جوهر الإجراء ده — من غيرها الطالب بيستلم "محتاجين معلومات" وبس
    reason = document.getElementById('rejectReason').value.trim();
    if (!reason) {
      document.getElementById('rejectReason').classList.add('error');
      document.getElementById('rejectReasonError').style.display = 'block';
      return;
    }
  } else if (selectedDecision === 'reject') {
    if (isSelfRegAction) {
      newStatus = 'rejected';
    } else if (currentStatus === 'housing_approved') {
      newStatus = 'housing_rejected';
    } else if (currentStatus === 'cyber_review') {
      newStatus = 'cyber_rejected';
    } else {
      return;
    }
    reason = document.getElementById('rejectReason').value.trim();
    if (!reason && isSelfRegAction) {
      document.getElementById('rejectReason').classList.add('error');
      document.getElementById('rejectReasonError').style.display = 'block';
      return;
    }
    if (!reason) {
      document.getElementById('rejectReason').classList.add('error');
      document.getElementById('rejectReasonError').style.display = 'block';
      return;
    }
  } else {
    return;
  }

  document.getElementById('submitReviewBtn').disabled = true;
  document.getElementById('submitReviewBtn').textContent = t('rdp_btn_submitting');

  try {
    var res;
    if (isSelfRegAction) {
      var action = selectedDecision === 'approve' ? 'approve' : (selectedDecision === 'reject' ? 'reject' : 'request-info');
      res = await fetch('/api/Workflow/' + requestId + '/' + action, {
        method:'POST',
        headers: authHeaders({ 'Content-Type': 'application/json' }),
        body:JSON.stringify({notes:reason})
      });
    } else {
      res = await fetch('/api/requests/' + requestId + '/review', {
        method:'PUT',
        headers: authHeaders({ 'Content-Type': 'application/json' }),
        body:JSON.stringify({status:newStatus, reviewedBy:currentUser.id||1, notes:reason})
      });
    }
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    if (!res.ok) {
      var err = await res.json();
      alert(err.message || t('rdp_msg_updateFailed'));
      document.getElementById('submitReviewBtn').disabled = false;
      document.getElementById('submitReviewBtn').textContent = t('rdp_btn_submitReview');
      return;
    }
    // نفضل على صفحة الطلب في كل الحالات ونحدّثها مكانها. قبل كده التسجيل الذاتي كان
    // بيرمي المراجع على قائمة الطلبات، فيفقد سياق اللي عمله للتو ومايشوفش المرحلة
    // الجديدة ولا سطر سجل المراجعات اللي اتضاف باسمه.
    // التأخير عشان رسالة النجاح تبان قبل التحديث.
    setTimeout(function () { loadRequest(); loadUnreadCount(); }, 1200);
  } catch(e) {
    alert(t('rdp_msg_connectionError'));
    document.getElementById('submitReviewBtn').disabled = false;
    document.getElementById('submitReviewBtn').textContent = t('rdp_btn_submitReview');
  }
}



function showAbout() {
  var lang = (document.getElementById('html-root')||document.documentElement).getAttribute('lang')||'ar';
  document.getElementById('aboutSysName').textContent = t('rdp_about_sysName');
  document.getElementById('aboutBuildDate').textContent = 'Build: 2026-07-01';
  document.getElementById('aboutModal').classList.add('open');
}
function closeAbout() { document.getElementById('aboutModal').classList.remove('open'); }
document.addEventListener('keydown',function(e){if(e.key==='Escape')closeAbout();});

setLang(localStorage.getItem('uiLanguage')||'ar');
if (!requestId) { document.getElementById('loading-state').style.display='none'; document.getElementById('error-state').style.display='block'; document.getElementById('error-message').textContent='No request ID specified.'; }
