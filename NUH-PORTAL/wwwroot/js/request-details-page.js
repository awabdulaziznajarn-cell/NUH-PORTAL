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

// ⚠️ كان هنا تعريف تاني لـ escHtml **ناقص تهريب العلامة المفردة (')**، ولأنه
//    في النطاق العام كان بيدهس نسخة التخطيط القوية على الصفحة دي وحدها.
//    وشاشة تفاصيل الطلب بالذات بتحقن أسماء الطلاب والملاحظات جوّه خصائص
//    (title=... وonclick=...) - يعني هي أكتر شاشة الفرق ده بيهمّ فيها.
//    التعريف الوحيد دلوقتي في /js/esc.js المحمَّل من التخطيط.

// نص بديل لو المفتاح مش موجود في ملف الترجمة.
// ⚠️ كانت متعرّفة *جوه* renderRequest، يعني أي كود برّاها بينادي عليها كان
//    بيرمي ReferenceError. ده اللي كان بيمنع خانة «المعلومات المطلوبة» إنها
//    تظهر: selectInfo() بينادي setReasonTexts('info') واللي بتستخدم tf، فبتقع
//    قبل ما توصل للسطر اللي بيعرض الخانة. (زر الرفض كان شغال لأنه بيستخدم t.)
// ⚠️ tf() اتنقلت لـ /i18n.js - بقت مستخدمة في أكتر من شاشة.
// ============================================================================
//  زرّ «رجوع».
//
//  ⚠️ كان رابطًا ثابتًا لـ /Requests. وبعد ما بقت حالة الشاشة في الرابط
//     (js/url-state.js) بقى الرابط الثابت غلط صريح: بيرمي الموظف على قائمة
//     نضيفة كأنه داخل لأول مرة، بينما زرّ الرجوع بتاع المتصفح - جنبه على
//     بُعد سنتيمترات - بيرجّعه لتبويبه وصفحته وبحثه.
//
//  ⚠️ والفولباك مش زيادة: اللي فتح الطلب من إشعار أو من رابط متبعت مالوش
//     صفحة سابقة، و history.back() ساعتها بتطلّعه بره النظام كله - أو
//     بترجّعه للموقع اللي جه منه.
// ============================================================================
function goBack() {
  var ref = document.referrer || '';
  var fromUs = ref.indexOf(window.location.origin + '/') === 0;
  if (fromUs && window.history.length > 1) { window.history.back(); return; }
  window.location.href = '/Requests';
}
// ⚠️ تنسيق التاريخ كان بأربع صيغ مختلفة في نفس الملف. أوضحها في الشاشة:
//    «تاريخ التقديم» كان toLocaleDateString('ar-SA') فيطلع ٢٠٢٦/٨/٩، بينما
//    «آخر مزامنة» كان toLocaleString() بلا لغة فيطلع 09/08/2026, 14:26:01 —
//    تاريخان في صفحة واحدة بشكلين وأبجديتين مختلفتين. الصيغة هنا مرة واحدة.
function __lang() {
  var el = document.getElementById('html-root') || document.documentElement;
  return (el.getAttribute('lang') || 'ar') === 'en' ? 'en-US' : 'ar-SA';
}
// شكل التاريخ من NuhFmt — التعريف الوحيد في /js/date-format.js
// ⚠️ كانت toLocaleDateString('ar-SA') — وفي متصفحات دي بترجّع **هجري**
//    افتراضيًا، فالتاريخ يتقرا على إنه ميلادي وهو مش كده.
function formatDate(v) { return NuhFmt.date(v); }

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
  // ⚠️ اللي بيتصرّف وهو واقف على need_more_info هو *الطالب* لا المشرف —
  //    بيعيد تقديم بياناته. وكان مكتوب هنا 'rdp_stage_pendingSupervisor'
  //    («موافقة إدارة الإسكان»)، فسجل المراجعات كان بيقول إن الطالبة وافقت
  //    على إسكان نفسها.
  need_more_info:         { ok: 'rdp_tl_studentResubmitted',       no: 'rdp_stage_rejected'        }
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
// ⚠️ كان فيه خطوة زيادة: cyber_approved ثم ready_for_provisioning، وهما مرحلة
//    واحدة — موافقة الأمن السيبراني *هي* اللي بتخلّي الطلب جاهزًا لإنشاء الحساب.
//    وكان عنوان الخطوة نفسه بيقول الاتنين: «موافقة إدارة الأمن السيبراني -
//    جاهز لإنشاء حساب شبكة السكن»، فالمسار يبان فيه تكرار.
//    كمان أرقام الخطوات جاية من الخادم على أساس ٥ خطوات، فالمصفوفة
//    السداسية كانت بتخلّي «مكتمل» يعلّم على الخطوة الغلط.
//    المسارين دلوقتي بنفس الشكل: تقديم ← إسكان ← سيبراني ← تجهيز ← مكتمل.
const workflowSteps = [
  { status:'submitted', key:'submitted' },
  { status:'housing_approved', key:'housing' },
  { status:'cyber_review', key:'cyber_review' },
  { status:'ready_for_provisioning', key:'ready' },
  { status:'completed', key:'completed' }
];
// ⚠️ كانت هنا خريطة «الحالة → رقم الخطوة» مكتوبة بالإيد. الرقم موجود على
//    الخادم في حقل Step جوّه جدول الانتقالات، والخريطتان اتفارقتا فعلًا —
//    التفاصيل في تعليق stepOf داخل js/request-workflow.js.
//    القراءة بقت NuhWorkflow.stepOf، وحالات الرفض بتتحدّد من السجل تحت.
function stepOf(status) {
  var v = NuhWorkflow.stepOf(status);
  return v === undefined ? 0 : v;
}

const selfRegWorkflowSteps = [
  { status: null, key:'submitted' },
  { status:'pending_supervisor', key:'housing' },
  { status:'pending_cyber', key:'cyber_review' },
  { status:'ready_for_provisioning', key:'ready' },
  { status:'completed', key:'completed' }
];
// ⚠️ اسم الخطوة في شريط «سير العمل» كان ثابتًا مهما كانت حالتها، فالمرحلة اللي
//    لسه شغّالة كانت بتتسمّى باسم *نتيجتها*: طلب واقف على «مراجعة إدارة الأمن
//    السيبراني» كان المسار بيوريه «موافقة إدارة الأمن السيبراني» — يعني وافقوا
//    وهُم لسه ما فتحوش الطلب. ونفس العلة في خطوة الإسكان: مرفوض الإسكان كان
//    بيبان بعلامة ✗ حمرا وتحتها «موافقة إدارة الإسكان».
//    الاسم دلوقتي بيتبع الحالة الفعلية للخطوة اللي الطلب واقف عليها.
//
//    ⚠️ الجدول ده بالحالة مش بالخطوة عن قصد: أكتر من حالة بتقع على نفس الخطوة
//    ومعناها مختلف — housing_approved معناها الإسكان خلص، و pending_supervisor
//    معناها لسه بيراجع، والاتنين على خطوة الإسكان. الخطوات اللي مش حالية
//    بتفضل بأسماء المراحل زي ما هي (التوازي بين «موافقة إدارة الإسكان» و
//    «موافقة إدارة الأمن السيبراني» هو اللي بيخلّي المسار يتقرا كوحدة واحدة).
const currentStepLabel = {
  submitted:              'rdp_step_submitted',
  pending_supervisor:     'rdp_wf_housingPending',
  need_more_info:         'rdp_stage_needMoreInfo',
  housing_approved:       'rdp_wf_housingApp',
  housing_rejected:       'rdp_stage_housingRejected',
  cyber_review:           'rdp_stage_cyberReview',
  pending_cyber:          'rdp_stage_cyberReview',
  cyber_rejected:         'rdp_stage_cyberRejected',
  cyber_approved:         'rdp_wf_readyHousing',
  ready_for_provisioning: 'rdp_wf_readyHousing',
  completed:              'rdp_stage_completed',
  approved:               'rdp_stage_completed',
  rejected:               'rdp_stage_rejected'
};
// ⚠️ كانت هنا قائمة تالتة للحالات المرفوضة مكتوبة بالإيد. القائمة الوحيدة
//    في Core/RequestWorkflow.cs، وبتوصل الواجهة في __WF.rejected.
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
// ⚠️ الستايل كان بيتحقن من هنا: <style> بيتبني كنصّ في الجافاسكريبت بألوان
//    مكتوبة بالإيد (#fffaeb و#dba102 و#b42318). يعني مكوّن تصميم مخبّي جوّه
//    ملف سلوك — اللي بيقرا الـ CSS مش شايفه، واللي بيغيّر لون في الهوية
//    مش هيلاقيه. اتنقل كله لـ css/components.css تحت اسم .chg، والألوان
//    بقت توكنز.

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

// «كانت كذا وبقت كذا» — الشكل كله من .chg في css/components.css.
// ⚠️ نفس المكوّن بيستخدمه سجل العمليات وسجل وحدات أعضاء هيئة التدريس.
function chgHtml(oldVal, newHtml) {
  var empty = (oldVal === null || oldVal === undefined || oldVal === '');
  return '<span class="chg">' +
           '<span class="chg-old' + (empty ? ' chg-empty' : '') + '">' +
             (empty ? tf('rdp_editEmpty', 'فارغ', 'empty') : escHtml(oldVal)) +
           '</span>' +
           '<span class="chg-arrow" aria-hidden="true"></span>' +
           '<span class="chg-new">' + newHtml + '</span>' +
         '</span>';
}

// بديل موحّد لكتابة صف البيانات — بيعلّم الصف تلقائيًا لو الحقل اتعدّل
function infoField(fieldKeys, label, valueHtml) {
  var keys = [].concat(fieldKeys || []);
  var hits = keys.map(function (k) { return STUDENT_EDITS[k]; }).filter(Boolean);

  if (!hits.length)
    return '<div class="info-field"><span class="info-label">' + label +
           '</span><span class="info-value">' + valueHtml + '</span></div>';

  var head = '<span class="info-label">' + label +
             '<span class="edited-tag">' + tf('rdp_editedTag', 'مُعدَّل', 'Edited') + '</span></span>';

  // ⚠️ حقل واحد: القديم والجديد على سطر واحد بسهم بينهم. قبل كده كانت القيمة
  //    الجديدة في سطر، وتحتها سطر رمادي صغير «قبل التعديل: ...» بشطب أحمر —
  //    فالعين ما كانتش بتربط الاتنين ببعض، والأحمر كان بيقول «غلط» والحقيقة
  //    إن ده تعديل صحيح مش خطأ.
  if (hits.length === 1)
    return '<div class="info-field edited">' + head +
           '<span class="info-value">' + chgHtml(hits[0].old, valueHtml) + '</span></div>';

  // ⚠️ أكتر من حقل (السكن: مبنى ودور وشقة وغرفة): القيمة المركّبة فوق زي ما
  //    هي، وتحتها سطر مستقل لكل حقل اتغيّر باسمه. كانت كلها بتتلزق في سطر
  //    واحد مفصول بنقط، فمكانش باين أنهي رقم بتاع أنهي حقل.
  var rows = hits.map(function (c) {
    return '<span class="chg-row">' +
             '<span class="chg-lbl">' + escHtml(c.label) + '</span>' +
             chgHtml(c.old, escHtml(c['new'] == null ? '' : c['new'])) +
           '</span>';
  }).join('');

  return '<div class="info-field edited">' + head +
         '<span class="info-value">' + valueHtml + '</span>' +
         '<span class="chg-list">' + rows + '</span></div>';
}

// ============================================================================
//  وثيقة التعهّد - الشكل والطباعة في wwwroot/js/pledge-doc.js.
//
//  ⚠️ اتنقلت من هنا لأن شاشة «ملف الطالب» بتعرض نفس الوثيقة وبتطبعها بنفس
//     الطريقة. لو فضلت مكتوبة في الشاشة دي، الشاشة التانية كانت هتاخد نسخة -
//     ونسختين لنفس الوثيقة بيفترقوا مع أول تعديل، والوثيقة دي بالذات وثيقة
//     رسمية بتتطبع وتتحطّ في ملفات.
// ============================================================================
function pledgeCardHtml(r) { return NuhPledgeDoc.card(r); }
function printPledge() { NuhPledgeDoc.print(); }

function renderRequest(r) {
  buildEditMap(r.studentEdits);
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('loading-state').style.display = 'none';
  document.getElementById('detail-content').style.display = 'block';
  // ماتخترعش رقم طلب — الرقم المصنوع هنا مكانش متخزّن، والطالب كان بيكتبه في
  // صفحة التتبع فمايتلاقاش. الرقم بقى بيتولّد ويتخزّن وقت إنشاء الطلب.
  var reqNum = r.requestNumber || '-';
  var __pt = document.getElementById('pageTitle'); if (__pt) __pt.textContent = t('rdp_pageTitle')+' - '+reqNum;

  var s = r.student || {};
  var st = (r.status||'').toLowerCase();
  var stageLabel = (stageNames[st]&&t(stageNames[st]))||st;
  var isRejected = NuhWorkflow.isRejected(st);
  var workflowIdx = stepOf(st);

  // ⚠️ الحالة «rejected» المجرّدة ما بتقولش الرفض جه من مين — والخريطة فوق
  //    بتحطّها على الخطوة ٠ (تقديم الطلب). فالمؤشّر كان بيوري الرفض على خطوة
  //    التقديم نفسها، يعني «التقديم فشل» — والتقديم نجح، واللي رفض هو الإسكان،
  //    وسجل المراجعات تحته بيقول «رفض إسكان» بالنص. نفس الشاشة بروايتين.
  //
  //    المرحلة اللي الرفض حصل فيها متسجّلة في fromStage لآخر سطر في السجل —
  //    بنقرأها منه بدل ما نخمّن.
  if (isRejected) {
    var atStage;

    // (١) المرحلة اللي الرفض حصل فيها من آخر سطر في السجل — أدقّ مصدر.
    if (Array.isArray(r._regHistory) && r._regHistory.length) {
      var lastStep = r._regHistory[r._regHistory.length - 1];
      atStage = NuhWorkflow.stepOf(lastStep.fromStage);
      // ⚠️ سجلات قديمة ممكن تكون بلا fromStage محفوظ (نفس الاحتياطي المكتوب
      //    تحت في بناء السجل). ساعتها بنجرّب toStage: housing_rejected و
      //    cyber_rejected بيقولوا المرحلة لوحدهم.
      if (atStage === undefined)
        atStage = NuhWorkflow.stepOf(lastStep.toStage);
    }

    // (٢) وإلا من ختم المراجعة: مين آخر واحد فتح الطلب فعلًا.
    //     ⚠️ السيبراني الأول: لو ختمه موجود يبقى هو آخر مرحلة اتلمست، حتى لو
    //        الإسكان ختم قبله.
    if (atStage === undefined && r.cyberReviewedAt) atStage = NuhWorkflow.stepOf('cyber_review');
    if (atStage === undefined && r.housingReviewedAt) atStage = NuhWorkflow.stepOf('pending_supervisor');

    if (atStage !== undefined) workflowIdx = atStage;
  }

  /* --- Workflow bar --- */
  var isSelfReg = r.requestType === 'self_registration';
  var wfSteps = isSelfReg ? selfRegWorkflowSteps : workflowSteps;
  var wfHtml = wfSteps.map(function(step,i) {
    var cls = '';
    // ⚠️ كان الشرط st.indexOf('rejected') > -1 — فحص نصّي بيمسك أي حالة فيها
    //    الكلمة دي، ويسكت بلا خطأ لو الاسم اتغيّر.
    if (isRejected) {
      // ⚠️ الخطوات اللي قبل نقطة الرفض بتفضل «مكتملة»: هي حصلت فعلًا. كانت
      //    بتطلع رمادية زي اللي ما حصلش، فالمؤشّر بيقول إن التقديم نفسه فشل —
      //    وبوابة الطالب في نفس اللحظة بتقول «تم تقديم الطلب ✓ ثم مرفوض ✗».
      //    نفس الطلب بروايتين، والموظف هو اللي بيرد على الطالب.
      cls = (i === workflowIdx) ? ' rejected' : (i < workflowIdx ? ' completed' : '');
    } else if (i < workflowIdx) {
      cls = ' completed';
    } else if (i === workflowIdx) {
      cls = ' active';
    }
    // الخطوة اللي الطلب واقف عليها دلوقتي (سواء جارية أو مرفوضة) بتاخد اسم
    // الحالة نفسها — نفس النص اللي في الشارة فوق، فما يحصلش تناقض بين
    // «مراجعة إدارة الأمن السيبراني» في الشارة و«موافقة…» في نفس اللحظة.
    var curKey = (cls === ' active' || cls === ' rejected') ? currentStepLabel[st] : null;
    var label = curKey ? t(curKey)
      : step.key === 'submitted' ? t('rdp_step_submitted')
      : step.key === 'housing' ? t('rdp_wf_housingApp')
      // ⚠️ مفتاح مستقل عن rdp_stage_cyberReview: ذاك اسم *حالة* («مراجعة
      //    إدارة الأمن السيبراني») ومستخدم في الشارات وسجل المراحل.
      //    أما هنا فاسم *مرحلة في المسار*، ولازم يوازي «موافقة إدارة الإسكان»
      //    اللي قبله — التوازي هو اللي بيخلّي المسار يتقرا كوحدة واحدة.
      : step.key === 'cyber_review' ? t('rdp_wf_cyberApp')
      : step.key === 'ready' ? t('rdp_wf_readyHousing')
      : t('rdp_stage_completed');
    var iconSvg = cls==='completed'
      ? '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="8" fill="#067647"/><path d="M4.5 8L7 10.5L11.5 6" stroke="white" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>'
      : cls==='rejected'
        ? '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="8" fill="#d92d20"/><path d="M5.5 5.5L10.5 10.5M10.5 5.5L5.5 10.5" stroke="white" stroke-width="1.5" stroke-linecap="round"/></svg>'
        : cls==='active'
          ? '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="7" fill="#166a45" stroke="#166a45" stroke-width="2"/><circle cx="8" cy="8" r="3" fill="white"/></svg>'
          : '<svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="7" stroke="#cecfd2" stroke-width="1.5" fill="none"/></svg>';
    return '<div class="workflow-step'+cls+'"><div class="workflow-circle">'+iconSvg+'</div><div class="workflow-step-label">'+label+'</div></div>';
  }).join('');

  /* --- Review history entries (enterprise timeline) --- */
  var historyEntries = [];
  if (r.requestType === 'self_registration' && r._regHistory) {
    r._regHistory.forEach(function(h, idx) {
      var wasRejected = NuhWorkflow.isRejected(h.toStage);
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

  // ⚠️ خطوات طلبات الموظف بتتبني من تواريخ الطلب بترتيب ثابت مكتوب في الكود،
  //    مش مرتّبة بالوقت. طول ما التواريخ ماشية بالترتيب الطبيعي الشكل سليم،
  //    وأول ما واحدة تخرج عن الترتيب (طلب اتعدّل أو اترجع لمرحلة سابقة) السجل
  //    بيتقلب ويبان إجراء يوم ٦ قبل إجراء يوم ٥.
  //    سجل طلبات الطالب مرتّب بالوقت أصلًا من قاعدة البيانات — دلوقتي الاتنين سواء.
  historyEntries.sort(function (a, b) {
    var ta = new Date(a.time).getTime(), tb = new Date(b.time).getTime();
    if (isNaN(ta) || isNaN(tb)) return 0;
    return ta - tb;
  });

  function fmtDate(t) { return NuhFmt.dateLong(t); }
  function fmtTime(t) { return NuhFmt.time(t); }

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
    // ⚠️ الصندوق بيختفي خالص لما ما يكونش فيه ملاحظة. كان بيتكتب دايمًا
    //    و«لا توجد ملاحظات» بتاخد نفس المساحة واللون بتاع الملاحظة الحقيقية —
    //    فتلات صناديق فاضية بتخنق ملاحظتين، والعين ما بتفرّقش بينهم.
    // ⚠️ والعنوان بيقول *محتوى* الصندوق: «سبب الرفض» أو «المطلوب استكماله»
    //    بدل كلمة «الملاحظات» اللي ما بتقولش حاجة عن اللي جوّه.
    var notesHtml = (showNotes && e.notes)
      ? '<div class="note-block in-tl' + (e.cls === 'rejected' ? ' red' : '') + '">' +
          '<div class="note-block-cap">' +
          (e.cls === 'rejected' ? t('rdp_lbl_rejectionReason') : t('rdp_lbl_infoRequested')) +
          '</div><div class="note-block-txt">' + escHtml(e.notes) + '</div></div>'
      : '';
    // اللون البرتقالي للنقطة — مافيش كلاس ليه في site.css فبيتحط هنا مباشرة
    var dotStyle = e.cls === 'info' ? ' style="border-color:#b54708;background:#b54708"' : '';
    return '<div class="tl-item"><div class="tl-dot '+dotCls+'"'+dotStyle+'></div><div class="tl-content"><div class="tl-title">'+title+'</div>'+userHtml+'<div class="tl-row"><span class="tl-label">'+t('rdp_lbl_date')+'</span><span class="tl-value">'+dateStr+' - '+timeStr+'</span></div>'+notesHtml+'</div></div>';
  }).join('');
  if (!historyHtml) {
    historyHtml = '<div class="history-empty">'+t('rdp_msg_noReviews')+'</div>';
  }

  /* --- Rejection info banner --- */
  var rejectionNotes = st === 'cyber_rejected' ? r.cyberNotes : (st === 'housing_rejected' ? r.housingNotes : (st === 'rejected' ? r.notes : null));
  var rejectionHtml = rejectionNotes
    // ⚠️ كان .rejection-info — تصميم تالت لنفس الصندوق، مكتوب في
    //    Views/Requests/Details.cshtml وحدها. بقى نفس المكوّن المشترك،
    //    فالبانر فوق والملاحظة في الخط الزمني وبوابة الطالب شكلهم واحد.
    ? '<div class="note-block red">'+
      '<div class="note-block-cap">'+t('rdp_lbl_rejectionReason')+'</div>'+
      '<div class="note-block-txt">'+escHtml(rejectionNotes)+'</div></div>'
    : '';

  // "مقدّم من" في التسجيل الذاتي = الطالب نفسه، مش حساب الدخول اللي اتسجّل عليه
  // الإجراء. أما لو موظف سجّل نيابة عن الطالب (تسجيل فردي/جماعي) فاسم الموظف هو
  // المعلومة المفيدة فعلاً هنا.
  var submittedByDisplay = (r.requestType === 'self_registration')
    ? (s.full_name || s.fullNameArabic || r.submittedByName || '')
    : (r.submittedByName || '');

  /* --- Review actions --- */
  var reviewHtml = '';
  // ⚠️ كل المراحل - المسارين معًا - مصدرها الوحيد window.__WF القادم من
  //    Core/RequestWorkflow.cs. لا تُكتب هنا حالة ولا انتقال ولا مسار API.
  //
  //    وكانت مكتوبة مرتين: مرة سقطت منها «submitted» فطُبع مكان اسم الزر
  //    «undefined» ولم يفعل الضغط شيئًا، ومرة بقيت فيها مراحل مسار تسجيل
  //    الطالب (pending_supervisor / pending_cyber) معروفة لهذه الشاشة وحدها
  //    ومجهولة لشاشة القائمة - فالطلب يُحسب هناك في «يحتاج إجراءك» ثم يظهر
  //    في الصف بلا زر وبلا تمييز لوني.
  //
  //    الوصف وحده يبقى هنا: صياغة تخصّ هذه الشاشة لا معنى لها في جدول الخادم.
  var wf = NuhWorkflow.forStatus(st);
  var isSelfRegAction = wf !== null && wf.api === 'workflow';
  var canActOnStage = NuhWorkflow.canAct(st, can);

  if (canActOnStage) {
    var actionLabel, actionDesc;
    var showRequestInfo = wf.allowMoreInfo === true;
    if (isSelfRegAction) {
      if (st === 'pending_supervisor') {
        actionLabel = t('rdp_action_approveRequest');
        actionDesc = t('rdp_actionDesc_approveToCyber');
      } else if (st === 'pending_cyber') {
        actionLabel = t('rdp_action_approveRequest');
        actionDesc = t('rdp_actionDesc_approveToNetwork');
      } else {
        actionLabel = t('rdp_action_completeRequest');
        actionDesc = t('rdp_actionDesc_approveComplete');
      }
    } else if (wf) {
      // الاسم من الجدول المشترك. الوصف وحده خاص بهذه الشاشة.
      actionLabel = t(wf.approveKey);
      if (st === 'submitted') {
        // ⚠️ بلا «طلب معلومات إضافية»: هذه المرحلة تمرّ على
        //    /api/requests/{id}/review وجدول الخادم يسمح لها بالاعتماد
        //    والرفض فقط. عرض خيار يرفضه الخادم إهدار لوقت المراجع.
        actionDesc = t('rdp_actionDesc_approveToCyber');
      } else if (st === 'housing_approved') {
        actionDesc = t('rdp_actionDesc_submitToCyber');
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
      } else if (st === 'cyber_approved') {
        actionDesc = t('rdp_actionDesc_markReady');
      } else {
        actionDesc = t('rdp_actionDesc_completeNetwork');
      }
    }
    var rejectNewStatus = wf.rejectTo || '';
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
        // ⚠️ الخانات المطلوب تصحيحها — بتظهر مع «طلب معلومات إضافية» بس.
        //    من غيرها الطالب بيفتح نموذج فيه ١٢ خانة كلها مفتوحة والملاحظة
        //    بتقوله «صحّح المبنى» — فبيدوّر ويغلط ويغيّر حاجات مش مطلوبة.
        '<div class="fieldpick-box" id="infoFieldsBox">'+
          '<label class="fieldpick-title">'+t('rdp_lbl_infoFields')+'</label>'+
          '<div class="fieldpick-hint">'+t('rdp_hint_infoFields')+'</div>'+
          '<div class="fieldpick-grid" id="infoFieldsGrid"></div>'+
        '</div>'+
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
    var __when = NuhFmt.dateTime(r.studentEditedAt);
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
      '<div class="info-field"><span class="info-label">'+t('rdp_field_status')+'</span><span class="info-value"><span class="badge badge-'+NuhWorkflow.canonical(st)+'">'+(statusMap[st]?t(statusMap[st]):r.status)+'</span></span></div>'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_submittedBy')+'</span><span class="info-value">'+escHtml(submittedByDisplay)+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+t('rdp_field_submittedDate')+'</span><span class="info-value">'+NuhFmt.date(r.submittedAt)+'</span></div>'+
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
          '<div class="info-field"><span class="info-label">'+t('rdp_field_createdDate')+'</span><span class="info-value">'+NuhFmt.date(bd.createdDate)+'</span></div>'+
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

    /* 3b - Housing Account Card
       ⚠️ البطاقة دي كانت بكلاسات detail-card / detail-card-header /
          detail-card-content — وهي أسماء **مالهاش أي CSS في المشروع كله**،
          ومستعملة في المكان ده وحده. فالنتيجة إنها كانت تطلع بلا إطار ولا
          ترويسة ولا حشو، مختلفة تمامًا عن باقي أقسام الصفحة.
          دلوقتي بتستخدم card / card-header / card-body زي كل البطاقات،
          ومعاها أيقونة زيّهم — فالشكل واحد ويتبع site.css تلقائيًا. */
    '<div class="card" id="housingAccountCard" style="margin-top:16px;display:none">'+
      '<div class="card-header">'+
        '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>'+
        t('rdp_housingAccounts')+
      '</div>'+
      '<div class="card-body">'+
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
      '<svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#067647" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-bottom:12px"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>'+
      '<h3 style="font-size:18px;color:var(--green);margin-bottom:8px">'+t('rdp_msg_accountCreated')+'</h3>'+
      '<p style="font-size:14px;color:var(--navy-dark)">'+t('rdp_lbl_username')+': <strong dir="ltr" style="display:inline-block;background:var(--white);padding:4px 12px;border-radius:6px;border:1px solid var(--gray-200)">'+escHtml(s.ad_username || '-')+'</strong></p>'+
      '<p style="font-size:12px;color:var(--gray-500);margin-top:8px">'+t('rdp_msg_credentialsSms')+'</p>'+
    '</div></div>' : '')+

    /* 4c - وثيقة التعهّد */
    pledgeCardHtml(r)+

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
    var statusBadge = enabled ? '<span style="background:#dff6e7;color:#067647;padding:2px 8px;border-radius:999px;font-size:11px;font-weight:600">' + t('adEnabled') + '</span>'
      : (s.ad_status === 'disabled' ? '<span style="background:#fef3f2;color:#b42318;padding:2px 8px;border-radius:999px;font-size:11px;font-weight:600">' + t('adDisabled') + '</span>'
      : '<span style="background:#f5f5f6;color:#85888e;padding:2px 8px;border-radius:999px;font-size:11px;font-weight:600">' + (s.ad_status || t('adUnknown')) + '</span>');
    // ⚠️ الكلاسات housing-info-grid / housing-info-item مالهاش أي CSS في المشروع،
    //    فالعنوان كان بيلزق في القيمة: "اسم المستخدم في ADh456969999".
    //    بنستخدم info-grid / info-field اللي بتستخدمها باقي بطاقات الصفحة —
    //    عمودين مرتبين بخط فاصل، نفس شكل «معلومات الطالب» بالظبط.
    // ⚠️ اسم المستخدم والتاريخ نصّان لاتينيان داخل صفحة عربية. كان الحل السابق
    //    dir="ltr" على الخانة نفسها، وده غيّر معنى text-align:start من «يمين»
    //    إلى «يسار»، فطارت القيمة لأقصى الشمال بعيدًا عن عنوانها.
    //    الصحيح <bdi>: يعزل اتجاه النص في داخله فقط — فالحروف والأرقام تُقرأ
    //    بترتيبها الصحيح — بينما محاذاة السطر تبقى تابعة لاتجاه الصفحة، فتقف
    //    القيمة تحت عنوانها تمامًا في العربية وفي الإنجليزية بلا استثناء لأيهما.
    // hint اختياري: بيتحط على العنوان كـ title، من غير أي تغيير في الشكل
    function adField(label, valueHtml, ltr, hint) {
      return '<div class="info-field">' +
               '<span class="info-label"' + (hint ? ' title="' + escHtml(hint) + '"' : '') + '>' + label + '</span>' +
               '<span class="info-value">' +
                 (ltr ? '<bdi>' + valueHtml + '</bdi>' : valueHtml) +
               '</span>' +
             '</div>';
    }

    // ⚠️ «آخر دخول للشبكة» غير «آخر مزامنة»: المزامنة وقت *قراءتنا* من
    //    الدليل، ودي وقت *الطالب* ما استخدم الحساب فعلًا. الاتنين جنب بعض
    //    عن قصد عشان محدش يقرا واحدة على إنها التانية.
    //    فاضية = الحساب اتعمل وما اتستخدمش ولا مرة.
    // ⚠️ NuhFmt.dateTime لا NuhFmt.date: الوقت جزء من المعلومة هنا.
    //    والتاريخين دول كانوا بيظهروا **بصيغتين مختلفتين** في شاشتين:
    //    «إدارة حسابات السكن» بتعرض «18/08/2026 11:44» وهنا كان «18/08/2026»
    //    لنفس القيمة بالظبط — فالموظف يفتكر إن الشاشتين بيقروا حاجتين مختلفتين.
    //    الاتنين بيستخدموا NuhFmt (التعريف الوحيد)، بس كانوا مختارين دالتين.
    var lastLogon = ad.lastLogonAt
      ? NuhFmt.dateTime(ad.lastLogonAt)
      : '<span style="color:var(--gray-500)">' + t('adNeverLoggedIn') + '</span>';

    content.innerHTML = '<div class="info-grid">' +
      adField(t('adUsername'), escHtml(s.ad_username || '-'), true) +
      adField(t('adAccountStatus'), statusBadge) +
      // ⚠️ الوقت هنا مقروء من lastLogonTimestamp في الدليل، والدومين بيحدّثها
      //    كل ٩-١٤ يوم لا مع كل دخول. يعني الساعة اللي ظاهرة ساعة دخول حقيقي
      //    بس مش بالضرورة **آخر** واحد. الـ hint بيقول ده للموظف بدل ما رقم
      //    دقيق للدقيقة يوحي بدقّة مش موجودة.
      adField(t('adLastLogon'), lastLogon, true, t('adLastLogonHint')) +
      adField(t('adLastSync'), s.ad_last_sync_at ? NuhFmt.dateTime(s.ad_last_sync_at) : '-', true) +
      adField(t('college'), escHtml(collegeName(s.college) || '-')) +
    '</div>';

  // ⚠️ إجراءات الحساب (تعطيل/تفعيل الحساب، إعادة تعيين كلمة المرور، سجل إجراءات
  //    الحساب) أُزيلت من هذه الشاشة عن قصد. مكانها الوحيد «إدارة حسابات السكن».
  //    السبب: الإجراء الواحد في شاشتين يعني منطقين لازم يُحدَّثا معًا وإلا افترقا،
  //    وقد كان زر السجل هنا معطّلًا أصلًا لأن نافذته غير موجودة في هذه الشاشة.
  //    هذه البطاقة للعرض فقط: تُظهر حالة حساب الشبكة ولا تعدّله.
  } catch(e) {
    card.style.display = 'none';
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

// ⚠️ القائمة بتتجاب من السيرفر (RegistrationDataMapper.EditableFields) مش
//    مكتوبة هنا. لو اتكتبت هنا، أول ما خانة تتضاف أو تتشال من النموذج
//    هيبقى عندنا قائمتين مختلفتين والمراجع يعلّم على خانة مش موجودة.
var infoFieldsLoaded = false;
async function loadInfoFields() {
  if (infoFieldsLoaded) return;
  var grid = document.getElementById('infoFieldsGrid');
  if (!grid) return;
  try {
    var r = await fetch('/api/Workflow/editable-fields', { headers: authHeaders() });
    if (!r.ok) return;
    var list = await r.json();
    grid.innerHTML = list.map(function (f) {
      return '<label class="fieldpick-item"><input type="checkbox" value="' + f.key + '">' +
             '<span>' + t('rdp_fld_' + f.key) + '</span></label>';
    }).join('');
    infoFieldsLoaded = true;
  } catch (e) { /* صامت — القائمة الفاضية معناها كل الخانات مفتوحة للطالب */ }
}

function selectedInfoFields() {
  var boxes = document.querySelectorAll('#infoFieldsGrid input:checked');
  return Array.prototype.map.call(boxes, function (b) { return b.value; });
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
  toggleInfoFields(false);
  updateReviewSubmitState();
}

function toggleInfoFields(show) {
  var box = document.getElementById('infoFieldsBox');
  if (!box) return;
  box.classList.toggle('visible', !!show);
  if (show) loadInfoFields();
}

function selectReject() {
  selectedDecision = 'reject';
  document.getElementById('optApprove').className = 'review-option';
  var el;
  if (el = document.getElementById('optInfo')) el.className = 'review-option';
  if (el = document.getElementById('optReject')) el.className = 'review-option selected-reject';
  setReasonTexts('reject');
  document.getElementById('rejectReasonField').classList.add('visible');
  toggleInfoFields(false);
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
  toggleInfoFields(true);
  updateReviewSubmitState();
}

async function submitReview(currentStatus) {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  if (!selectedDecision) return;

  // ⚠️ الحالة الهدف ومسار الإرسال من الجدول المشترك (window.__WF) لا من سلسلة
  //    شروط محلية. السلسلة القديمة هنا كانت النسخة الثالثة من الجدول، وسقطت
  //    منها «submitted» فكان الضغط على «اعتماد» لا يفعل شيئًا بصمت.
  var wfSubmit = NuhWorkflow.forStatus(currentStatus);
  if (!wfSubmit) return;
  var reason = '';

  if (selectedDecision === 'approve') {
    if (!wfSubmit.approveTo) return;
  } else if (selectedDecision === 'request-info') {
    if (wfSubmit.allowMoreInfo !== true) return;
    // الملاحظات هي جوهر الإجراء ده — من غيرها الطالب بيستلم "محتاجين معلومات" وبس
    reason = document.getElementById('rejectReason').value.trim();
    if (!reason) {
      document.getElementById('rejectReason').classList.add('error');
      document.getElementById('rejectReasonError').style.display = 'block';
      return;
    }
  } else if (selectedDecision === 'reject') {
    if (!wfSubmit.rejectTo) return;
    reason = document.getElementById('rejectReason').value.trim();
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
    // ⚠️ العنوان والطريقة وجسم الطلب من NuhWorkflow.endpointFor — نفس ما تستعمله
    //    شاشة القائمة. كان هنا تفريعٌ ثانٍ على المسار، وهو ما جعل الشاشتين
    //    تعرفان مسارين مختلفين لنفس المرحلة.
    var ep = NuhWorkflow.endpointFor(
      requestId, currentStatus, selectedDecision, reason,
      selectedDecision === 'request-info' ? selectedInfoFields() : null);
    if (!ep) return;

    var res = await fetch(ep.url, {
      method: ep.method,
      headers: authHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(ep.body)
    });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    if (!res.ok) {
      var err = await res.json();
      NuhDialog.error(err.message || t('rdp_msg_updateFailed'));
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
    NuhDialog.error(t('rdp_msg_connectionError'));
    document.getElementById('submitReviewBtn').disabled = false;
    document.getElementById('submitReviewBtn').textContent = t('rdp_btn_submitReview');
  }
}



// ⚠️ حُذفت نافذة «حول النظام» (showAbout/closeAbout ومستمع Escape). لم يكن لها
//    مستدعٍ واحد، ولا يوجد في أي صفحة عنصر aboutModal — فكانت كل ضغطة Escape
//    في هذه الشاشة تقرأ classList من عنصر غير موجود وترمي TypeError.

setLang(localStorage.getItem('uiLanguage')||'ar');
if (!requestId) { document.getElementById('loading-state').style.display='none'; document.getElementById('error-state').style.display='block'; document.getElementById('error-message').textContent='No request ID specified.'; }
