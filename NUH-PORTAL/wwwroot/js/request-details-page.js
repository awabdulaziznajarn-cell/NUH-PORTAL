const token = localStorage.getItem('staffToken');
if (!token) window.location.replace('/Account/Login');
const currentUser = JSON.parse(localStorage.getItem('staffUser') || '{}');
const _userRole = (currentUser.role || '').toLowerCase();
const urlParams = new URLSearchParams(window.location.search);
var __pathIdMatch = window.location.pathname.match(/\/Requests\/Details\/(\d+)/i);
const requestId = (urlParams.get('id') || (__pathIdMatch ? __pathIdMatch[1] : null));

function escHtml(str) { return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
function goBack() { window.location.href = '/Requests'; }
function formatDate(d) { if (!d) return '-'; return new Date(d).toLocaleString(); }

window.onLanguageChange = function(l) {
  if (requestId) loadRequest();
};

const stageNames = {
  submitted: { ar:'مقدم', en:'Submitted' },
  housing_approved: { ar:'موافقة إدارة الإسكان', en:'Housing Approved' },
  housing_rejected: { ar:'رفض إسكان', en:'Housing Rejected' },
  cyber_review: { ar:'مراجعة إدارة الأمن السيبراني', en:'Cyber Review' },
  cyber_approved: { ar:'موافقة إدارة الأمن السيبراني', en:'Cyber Approved' },
  cyber_rejected: { ar:'رفض إدارة الأمن السيبراني', en:'Cyber Rejected' },
  ready_for_provisioning: { ar:'جاهز لإنشاء حساب شبكة السكن', en:'Ready For Housing Network Account Creation' },
  completed: { ar:'مكتمل', en:'Completed' },
  pending_supervisor: { ar:'موافقة إدارة الإسكان', en:'Housing Approval' },
  pending_cyber: { ar:'مراجعة إدارة الأمن السيبراني', en:'Cyber Review' },
  need_more_info: { ar:'بحاجة معلومات إضافية', en:'Need More Info' },
  approved: { ar:'مكتمل', en:'Completed' },
  rejected: { ar:'مرفوض', en:'Rejected' }
};
const statusMap = {
  submitted: 'معلق', housing_approved: 'موافقة إدارة الإسكان', housing_rejected: 'مرفوض',
  cyber_review: 'مراجعة إدارة الأمن السيبراني', cyber_approved: 'معتمد', cyber_rejected: 'مرفوض',
  ready_for_provisioning: 'جاهز لإنشاء حساب شبكة السكن', completed: 'مكتمل',
  pending_supervisor: 'موافقة إدارة الإسكان', pending_cyber: 'مراجعة إدارة الأمن السيبراني',
  need_more_info: 'بحاجة معلومات إضافية',
  approved: 'مكتمل', rejected: 'مرفوض'
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
const genderMap = { male: { ar:'ذكر', en:'Male' }, female: { ar:'أنثى', en:'Female' } };

async function loadRequest() {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  try {
    var res = await fetch('/api/requests/' + requestId, { headers:{'Authorization':'Bearer '+token} });
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    if (res.status === 404) { document.getElementById('loading-state').style.display='none'; document.getElementById('error-state').style.display='block'; document.getElementById('error-message').textContent=lang==='ar'?'الطلب لم يعد متاحاً':'Request is no longer available'; return; }
    if (!res.ok) { throw new Error('HTTP '+res.status); }
    var r = await res.json();
    if (r.requestType === 'bulk_req' && r.bulkRequestId) {
      try {
        var bulkRes = await fetch('/api/bulkregistration/' + r.bulkRequestId, { headers:{'Authorization':'Bearer '+token} });
        if (bulkRes.ok) r.bulkDetails = await bulkRes.json();
      } catch(e) { console.error('Failed to load bulk details:', e); }
    }
    if (r.requestType === 'self_registration') {
      try {
        var regRes = await fetch('/api/Registration/my-requests/' + requestId, { headers:{'Authorization':'Bearer '+token} });
        if (regRes.ok) { var regData = await regRes.json(); r._regHistory = regData.history || []; }
      } catch(e) { r._regHistory = []; }
    }
    renderRequest(r);
  } catch(e) {
    document.getElementById('loading-state').style.display = 'none';
    document.getElementById('error-state').style.display = 'block';
    document.getElementById('error-message').textContent = lang === 'ar' ? 'حدث خطأ أثناء تحميل بيانات الطلب.' : 'Error loading request details.';
  }
}

function renderRequest(r) {
  var lang = document.getElementById('html-root').getAttribute('lang') || 'ar';
  document.getElementById('loading-state').style.display = 'none';
  document.getElementById('detail-content').style.display = 'block';
  var reqNum = r.requestNumber || new Date().getFullYear()+'-'+String(r.id).padStart(6,'0');
  var __pt = document.getElementById('pageTitle'); if (__pt) __pt.textContent = (lang==='ar'?'تفاصيل الطلب':'Request Details')+' - '+reqNum;

  var s = r.student || {};
  var st = (r.status||'').toLowerCase();
  var stageLabel = (stageNames[st]&&stageNames[st][lang])||st;
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
    var label = step.key === 'submitted' ? (lang==='ar'?'تقديم الطلب':'Submitted')
      : step.key === 'housing' ? (lang==='ar'?'موافقة إدارة الإسكان':'Housing App.')
      : step.key === 'cyber_review' ? (lang==='ar'?'مراجعة إدارة الأمن السيبراني':'Cyber Review')
      : step.key === 'cyber_approved' ? (lang==='ar'?'موافقة إدارة الأمن السيبراني - جاهز لإنشاء حساب شبكة السكن':'Cyber App. - Ready Housing')
      : step.key === 'ready' ? (lang==='ar'?'جاهز لإنشاء حساب شبكة السكن':'Ready Housing')
      : (lang==='ar'?'مكتمل':'Completed');
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
      var hlbl = idx === 0 ? { ar:'تقديم الطلب', en:'Submitted' } : (stageNames[h.toStage] || { ar:h.toStage, en:h.toStage });
      historyEntries.push({ label: hlbl, cls: h.toStage === 'rejected' ? 'rejected' : 'approved', time: h.actionDate, notes: h.notes || null, username: h.actorName || '' });
    });
  } else {
    if (r.submittedAt) {
      historyEntries.push({ label: { ar:'تقديم الطلب', en:'Request Created' }, cls:'approved', time:r.submittedAt, notes:null, username: r.submittedByName });
    }
    if (r.housingReviewedAt) {
      var housingLabel = st === 'housing_rejected'
        ? { ar:'رفض الإسكان', en:'Housing Rejected' }
        : { ar:'إحالة للمراجعة الإلكترونية', en:'Assigned To Cyber' };
      historyEntries.push({ label: housingLabel, cls: st==='housing_rejected'?'rejected':'approved', time:r.housingReviewedAt, notes: st==='housing_rejected'?r.housingNotes:null, username: r.housingReviewedByName });
    }
    if (r.cyberReviewedAt) {
      var cyberLabel = st === 'cyber_rejected'
        ? { ar:'رفض إدارة الأمن السيبراني', en:'Cyber Rejected' }
        : { ar:'موافقة إدارة الأمن السيبراني', en:'Cyber Approved' };
      historyEntries.push({ label: cyberLabel, cls: st==='cyber_rejected'?'rejected':'approved', time:r.cyberReviewedAt, notes: st==='cyber_rejected'?r.cyberNotes:null, username: r.cyberReviewedByName });
    }
    if (r.readyForProvisioningAt) {
      historyEntries.push({ label: { ar:'جاهز لإنشاء حساب شبكة السكن', en:'Ready For Housing Network Account Creation' }, cls:'approved', time:r.readyForProvisioningAt, notes:null, username: r.readyForProvisioningByName });
    }
    if (r.completedAt) {
      historyEntries.push({ label: { ar:'مكتمل', en:'Completed' }, cls:'approved', time:r.completedAt, notes:null, username: r.completedByName });
    }
  }

  function fmtDate(t) { return new Date(t).toLocaleDateString(lang==='ar'?'ar-SA':'en-US', { year:'numeric', month:'short', day:'numeric' }); }
  function fmtTime(t) { return new Date(t).toLocaleTimeString(lang==='ar'?'ar-SA':'en-US', { hour:'2-digit', minute:'2-digit' }); }

  var historyHtml = historyEntries.map(function(e) {
    var title = e.label[lang];
    var dotCls = e.cls;
    var dateStr = fmtDate(e.time);
    var timeStr = fmtTime(e.time);
    var userHtml = e.username ? '<div class="tl-row"><span class="tl-label">'+(lang==='ar'?'المستخدم:':'User:')+'</span><span class="tl-value username">'+escHtml(e.username)+'</span></div>' : '';
    var notesHtml = '<div class="tl-notes">'+(lang==='ar'?'الملاحظات:':'Notes:')+' '+(e.notes && e.cls==='rejected'?escHtml(e.notes):(lang==='ar'?'لا توجد ملاحظات':'No notes'))+'</div>';
    return '<div class="tl-item"><div class="tl-dot '+dotCls+'"></div><div class="tl-content"><div class="tl-title">'+title+'</div>'+userHtml+'<div class="tl-row"><span class="tl-label">'+(lang==='ar'?'التاريخ:':'Date:')+'</span><span class="tl-value">'+dateStr+'</span></div><div class="tl-row"><span class="tl-label">'+(lang==='ar'?'الوقت:':'Time:')+'</span><span class="tl-value">'+timeStr+'</span></div>'+notesHtml+'</div></div>';
  }).join('');
  if (!historyHtml) {
    historyHtml = '<div class="history-empty">'+(lang==='ar'?'لا توجد مراجعات بعد':'No reviews yet')+'</div>';
  }

  /* --- Rejection info banner --- */
  var rejectionNotes = st === 'cyber_rejected' ? r.cyberNotes : (st === 'housing_rejected' ? r.housingNotes : (st === 'rejected' ? r.notes : null));
  var rejectionHtml = rejectionNotes
    ? '<div class="rejection-info">'+
      '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#991B1B" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>'+
      '<div><div class="rejection-info-label">'+
      (lang==='ar'?'سبب الرفض':'Rejection Reason')+'</div><div class="rejection-info-text">'+
      escHtml(rejectionNotes)+'</div></div></div>'
    : '';

  /* --- Review actions --- */
  var reviewHtml = '';
  if ((st === 'housing_approved' && _userRole === 'admin') || (st === 'cyber_review' && (_userRole === 'cyber' || _userRole === 'admin')) || (st === 'cyber_approved' && _userRole === 'admin') || (st === 'ready_for_provisioning' && _userRole === 'admin') || (st === 'pending_supervisor' && _userRole === 'supervisor') || (st === 'pending_cyber' && _userRole === 'cyber')) {
    var isSelfRegAction = st === 'pending_supervisor' || st === 'pending_cyber' || st === 'ready_for_provisioning';
    var actionLabel, actionDesc, nextApproved, showRequestInfo;
    if (isSelfRegAction) {
      if (st === 'pending_supervisor') {
        actionLabel = lang==='ar'?'اعتماد الطلب':'Approve Request';
        actionDesc = lang==='ar'?'الموافقة على الطلب وإرساله لمراجعة الأمن السيبراني':'Approve and send to cyber review';
        nextApproved = 'pending_cyber';
        showRequestInfo = true;
      } else if (st === 'pending_cyber') {
        actionLabel = lang==='ar'?'اعتماد الطلب':'Approve Request';
        actionDesc = lang==='ar'?'الموافقة على الطلب وإرساله لإنشاء حساب الشبكة':'Approve and create housing network account';
        nextApproved = 'ready_for_provisioning';
      } else {
        actionLabel = lang==='ar'?'إكمال الطلب':'Complete Request';
        actionDesc = lang==='ar'?'الموافقة على الطلب وإكماله مع إنشاء حساب الشبكة':'Approve and complete request with AD account creation';
        nextApproved = 'completed';
      }
    } else if (st === 'housing_approved') {
      actionLabel = lang==='ar'?'إرسال للمراجعة الإلكترونية':'Send to Cyber Review';
      actionDesc = lang==='ar'?'إرسال الطلب لمراجعة الأمن السيبراني':'Submit to cyber security review';
      nextApproved = 'cyber_review';
    } else if (st === 'cyber_review') {
      actionLabel = _userRole === 'cyber'
        ? (lang==='ar'?'الموافقة على إنشاء حساب شبكة السكن':'Approve Housing Account')
        : (lang==='ar'?'الموافقة على الطلب':'Approve Request');
      actionDesc = _userRole === 'cyber'
        ? (lang==='ar'?'الموافقة على الطلب وإرساله للمرحلة التالية':'Approve and move to next stage')
        : (lang==='ar'?'الموافقة وإرساله للمرحلة التالية':'Approve and move to next stage');
      nextApproved = 'cyber_approved';
    } else if (st === 'cyber_approved') {
      actionLabel = lang==='ar'?'تأكيد الجاهزية لإنشاء حساب شبكة السكن':'Mark Ready for Housing Network Account Creation';
      actionDesc = lang==='ar'?'تحديد الطلب كجاهز لإنشاء حساب شبكة السكن':'Mark request as ready for housing network account creation';
      nextApproved = 'ready_for_provisioning';
    } else if (st === 'ready_for_provisioning') {
      actionLabel = lang==='ar'?'إكمال الطلب':'Complete Request';
      actionDesc = lang==='ar'?'إنهاء الطلب وإنشاء حساب الشبكة':'Complete request and create network account';
      nextApproved = 'completed';
    }
    var rejectNewStatus = isSelfRegAction ? 'rejected' : (st === 'housing_approved' ? 'housing_rejected' : (st === 'cyber_review' ? 'cyber_rejected' : ''));
    reviewHtml = '<div class="card" id="reviewSection"><div class="card-header">'+
      (lang==='ar'?'إجراءات المراجعة':'Review Actions')+'</div><div class="card-body">'+
      '<div class="review-actions">'+
        '<label class="review-option" id="optApprove" onclick="selectApprove()">'+
          '<input type="radio" name="reviewDecision" value="approve" onchange="selectApprove()">'+
          '<div><div class="review-option-text">'+actionLabel+'</div>'+
          '<div class="review-option-desc">'+actionDesc+'</div></div>'+
        '</label>'+
        (showRequestInfo ? '<label class="review-option" id="optInfo" onclick="selectInfo()">'+
          '<input type="radio" name="reviewDecision" value="request-info" onchange="selectInfo()">'+
          '<div><div class="review-option-text">'+(lang==='ar'?'طلب معلومات إضافية':'Need More Info')+'</div>'+
          '<div class="review-option-desc">'+(lang==='ar'?'طلب معلومات إضافية من الطالب':'Request additional info from student')+'</div></div>'+
        '</label>' : '')+
        (rejectNewStatus ? '<label class="review-option" id="optReject" onclick="selectReject()">'+
          '<input type="radio" name="reviewDecision" value="reject" onchange="selectReject()">'+
          '<div><div class="review-option-text">'+(lang==='ar'?'رفض الطلب':'Reject Request')+'</div>'+
          '<div class="review-option-desc">'+(lang==='ar'?'رفض الطلب مع ذكر السبب':'Reject with reason')+'</div></div>'+
        '</label>' : '')+
        '<div class="reject-reason-field" id="rejectReasonField">'+
          '<label style="font-size:13px;font-weight:600;color:var(--navy-dark);margin-bottom:6px;display:block">'+
            (lang==='ar'?'سبب الرفض *':'Rejection Reason *')+'</label>'+
          '<textarea id="rejectReason" placeholder="'+(lang==='ar'?'الرجاء إدخال سبب الرفض':'Please enter rejection reason')+'"></textarea>'+
          '<div class="error-msg" id="rejectReasonError">'+(lang==='ar'?'الرجاء إدخال سبب الرفض':'Rejection reason is required')+'</div>'+
        '</div>'+
        '<button class="submit-review-btn" id="submitReviewBtn" onclick="submitReview(\''+st+'\')">'+
          (lang==='ar'?'تأكيد المراجعة':'Submit Review')+'</button>'+
      '</div></div></div>';
  }

  /* --- Housing assignment info (for completed/approved requests) --- */
  var housingHtml = '';
  if (s.housing_building || s.room_number || s.apartment_number) {
    var housingParts = [escHtml(s.housing_building||'')];
    if (s.room_number) housingParts.push(escHtml(s.room_number));
    if (s.apartment_number) housingParts.push((lang==='ar'?'شقة ':'Apt ')+escHtml(s.apartment_number));
    housingHtml = '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الإسكان':'Housing')+'</span><span class="info-value">'+
      housingParts.join(' - ')+'</span></div>';
  }

  document.getElementById('detail-content').innerHTML =
    /* 1 - Request Info header card */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>'+
      (lang==='ar'?'معلومات الطلب':'Request Information')+'</div><div class="card-body"><div class="info-grid">'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'رقم الطلب':'Request #')+'</span><span class="info-value">'+reqNum+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'نوع الطلب':'Type')+'</span><span class="info-value">'+(r.requestType==='bulk_req'?(lang==='ar'?'تسجيل جماعي':'Bulk Registration'):escHtml(r.requestType||''))+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الحالة':'Status')+'</span><span class="info-value"><span class="badge badge-'+st+'">'+(statusMap[st]||r.status)+'</span></span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'مقدم من':'Submitted By')+'</span><span class="info-value">'+escHtml(r.submittedByName||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'تاريخ التقديم':'Date')+'</span><span class="info-value">'+(r.submittedAt?new Date(r.submittedAt).toLocaleDateString(lang==='ar'?'ar-SA':'en-US'):'')+'</span></div>'+
    '</div></div></div>'+

    /* 1b - Bulk Request Details (only for bulk_req) */
    (r.requestType === 'bulk_req' && r.bulkDetails ? function(){
      var bd = r.bulkDetails;
      var bStudents = bd.students || [];
      var total = bStudents.length;
      var maleCnt = bStudents.filter(function(x){ return (x.gender||'').toLowerCase() === 'male'; }).length;
      var femaleCnt = bStudents.filter(function(x){ return (x.gender||'').toLowerCase() === 'female'; }).length;
      var bulkStatusMap = { pending: lang==='ar'?'قيد الانتظار':'Pending', processing: lang==='ar'?'قيد المعالجة':'Processing', completed: lang==='ar'?'مكتمل':'Completed' };
      var previewHtml = bStudents.slice(0, 50).map(function(bs, i){
        return '<tr><td>'+(i+1)+'</td><td>'+escHtml(bs.studentID||'')+'</td><td>'+escHtml(bs.fullNameArabic||'')+'</td><td>'+escHtml(bs.fullNameEnglish||'')+'</td><td>'+escHtml(bs.college||'')+'</td><td>'+escHtml(bs.academicLevel||'')+'</td></tr>';
      }).join('');

      var bulkCards =
        /* Bulk Details card */
        '<div class="card"><div class="card-header">'+
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>'+
          (lang==='ar'?'تفاصيل التسجيل الجماعي':'Bulk Registration Details')+'</div><div class="card-body"><div class="info-grid">'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'رقم الطلب الجماعي':'BRQ Number')+'</span><span class="info-value">'+escHtml(bd.requestNumber||reqNum)+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'اسم الملف':'File Name')+'</span><span class="info-value">'+escHtml(bd.fileName||'')+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'إجمالي الطلاب':'Total Students')+'</span><span class="info-value">'+total+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'عدد الطلاب الصحيح':'Valid Count')+'</span><span class="info-value">'+(bd.validCount||0)+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الأخطاء':'Errors')+'</span><span class="info-value">'+(bd.errorCount||0)+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الحالة':'Status')+'</span><span class="info-value">'+(bulkStatusMap[bd.status]||bd.status||'')+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'تاريخ الإنشاء':'Created Date')+'</span><span class="info-value">'+(bd.createdDate?new Date(bd.createdDate).toLocaleDateString(lang==='ar'?'ar-SA':'en-US'):'')+'</span></div>'+
        '</div></div></div>'+

        /* Summary card */
        '<div class="card"><div class="card-header">'+
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg>'+
          (lang==='ar'?'ملخص الطلاب':'Student Summary')+'</div><div class="card-body"><div class="info-grid">'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'إجمالي الطلاب':'Total Students')+'</span><span class="info-value">'+total+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الطلاب (ذكر)':'Male Students')+'</span><span class="info-value">'+maleCnt+'</span></div>'+
          '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الطلاب (أنثى)':'Female Students')+'</span><span class="info-value">'+femaleCnt+'</span></div>'+
        '</div></div></div>'+

        /* Students preview table */
        (bStudents.length > 0 ? '<div class="card"><div class="card-header">'+
          '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg>'+
          (lang==='ar'?'قائمة الطلاب (أول 50)':'Student List (First 50)')+'</div><div style="overflow-x:auto"><table><thead><tr>'+
          '<th>'+(lang==='ar'?'#':'#')+'</th>'+
          '<th>'+(lang==='ar'?'الرقم الجامعي':'Student ID')+'</th>'+
          '<th>'+(lang==='ar'?'الاسم (عربي)':'Name (Ar)')+'</th>'+
          '<th>'+(lang==='ar'?'الاسم (إنجليزي)':'Name (En)')+'</th>'+
          '<th>'+(lang==='ar'?'الكلية':'College')+'</th>'+
          '<th>'+(lang==='ar'?'المستوى':'Level')+'</th>'+
          '</tr></thead><tbody>'+previewHtml+'</tbody></table></div></div>' : '');
      return bulkCards;
    }() : '')+

    /* 2 - Student Info (personal) */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>'+
      (lang==='ar'?'معلومات الطالب':'Student Information')+'</div><div class="card-body"><div class="info-grid">'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الاسم (عربي)':'Name (Ar)')+'</span><span class="info-value">'+escHtml(s.full_name||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الاسم (إنجليزي)':'Name (En)')+'</span><span class="info-value">'+escHtml(s.full_name_english||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الرقم الجامعي':'Student ID')+'</span><span class="info-value">'+escHtml(s.student_id||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'رقم الهوية':'National ID')+'</span><span class="info-value">'+escHtml(s.national_id||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الجوال':'Mobile')+'</span><span class="info-value">'+escHtml(s.phone||'')+'</span></div>'+
      housingHtml+
    '</div></div></div>'+

    /* 3 - Academic Info */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>'+
      (lang==='ar'?'المعلومات الأكاديمية':'Academic Information')+'</div><div class="card-body"><div class="info-grid">'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الكلية':'College')+'</span><span class="info-value">'+escHtml(s.college||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'القسم':'Department')+'</span><span class="info-value">'+escHtml(s.department||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'المستوى':'Level')+'</span><span class="info-value">'+escHtml(s.academic_level||'')+'</span></div>'+
      '<div class="info-field"><span class="info-label">'+(lang==='ar'?'الجنس':'Gender')+'</span><span class="info-value">'+escHtml((genderMap[s.gender]&&genderMap[s.gender][lang])||s.gender||'')+'</span></div>'+
    '</div></div></div>'+

    /* 3b - Housing Account Card */
    '<div class="detail-card" id="housingAccountCard" style="margin-top:16px;display:none">'+
      '<div class="detail-card-header">'+
        '<h3 data-i18n="housingAccounts">حساب السكن</h3>'+
      '</div>'+
      '<div class="detail-card-content">'+
        '<div id="housingAccountContent">'+
          '<p style="color:var(--gray-500);text-align:center;padding:16px" data-i18n="loading">جاري التحميل...</p>'+
        '</div>'+
      '</div>'+
    '</div>'+

    /* 4 - Workflow Timeline (horizontal bar) */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>'+
      (lang==='ar'?'سير العمل':'Workflow')+'</div><div class="card-body">'+
      '<div style="padding:8px 0"><div class="workflow-bar">'+wfHtml+'</div></div>'+
      rejectionHtml+
    '</div></div>'+

    /* 4b - Completion success message (only for completed/approved) */
    (st === 'completed' || st === 'approved' ? '<div class="card" style="border:2px solid var(--green);background:var(--green-light)"><div class="card-body" style="text-align:center;padding:24px">'+
      '<svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#0F6E56" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-bottom:12px"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>'+
      '<h3 style="font-size:18px;color:var(--green);margin-bottom:8px">'+(lang==='ar'?'تم إنشاء حساب شبكة السكن بنجاح':'Housing Network Account Created Successfully')+'</h3>'+
      '<p style="font-size:14px;color:var(--navy-dark)">'+(lang==='ar'?'اسم المستخدم':'Username')+': <strong dir="ltr" style="display:inline-block;background:var(--white);padding:4px 12px;border-radius:6px;border:1px solid var(--gray-200)">'+(s.ad_username || '-')+'</strong></p>'+
      '<p style="font-size:12px;color:var(--gray-500);margin-top:8px">'+(lang==='ar'?'سيتم إرسال بيانات الاعتماد عبر الرسائل النصية لاحقاً':'Credentials will be sent via SMS later')+'</p>'+
    '</div></div>' : '')+

    /* 5 - Review History */
    '<div class="card"><div class="card-header">'+
      '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>'+
      (lang==='ar'?'سجل المراجعات':'Review History')+'</div><div class="card-body"><div class="review-timeline">'+
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
  if (_userRole !== 'admin') { card.style.display = 'none'; return; }
  try {
    var res = await fetch('/api/HousingAccountManagement/' + studentId, { headers: { 'Authorization': 'Bearer ' + token } });
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
    content.innerHTML = '<div class="housing-info-grid">' +
      '<div class="housing-info-item"><label>' + t('adUsername') + '</label><span dir="ltr" style="display:inline-block">' + (s.ad_username || '-') + '</span></div>' +
      '<div class="housing-info-item"><label>' + t('adAccountStatus') + '</label><span>' + statusBadge + '</span></div>' +
      '<div class="housing-info-item"><label>' + t('adLastSync') + '</label><span>' + (s.ad_last_sync_at ? formatDate(s.ad_last_sync_at) : '-') + '</span></div>' +
      '<div class="housing-info-item"><label>' + t('college') + '</label><span>' + (s.college || '-') + '</span></div>' +
    '</div>' +
    '<div style="margin-top:12px;display:flex;gap:8px;flex-wrap:wrap">' +
      (_userRole === 'admin' ? (enabled ? '<button class="btn btn-danger btn-sm" onclick="housingAction(' + studentId + ',\'disable\')">' + t('disableAccount') + '</button>'
               : '<button class="btn btn-success btn-sm" onclick="housingAction(' + studentId + ',\'enable\')">' + t('enableAccount') + '</button>') : '') +
      (_userRole === 'admin' ? '<button class="btn btn-warning btn-sm" onclick="housingResetPassword(' + studentId + ')">' + t('resetPassword') + '</button>' : '') +
      '<button class="btn btn-outline btn-sm" onclick="showHousingLifecycle(' + (s.id || studentId) + ',\'' + (s.full_name_english || '') + '\')">📋 ' + t('lifecycleLog') + '</button>' +
    '</div>';
  } catch(e) {
    card.style.display = 'none';
  }
}

async function housingAction(studentId, action) {
  var msgs = { enable: t('confirmEnable'), disable: t('confirmDisable') };
  if (!confirm(msgs[action] || t('confirm'))) return;
  if (_userRole !== 'admin') { alert(t('apiError')); return; }
  try {
    var res = await fetch('/api/HousingAccountManagement/' + studentId + '/' + action, {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token }
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
  if (_userRole !== 'admin') { alert(t('apiError')); return; }
  var pwd = prompt(t('newPassword'));
  if (!pwd || pwd.length < 8) { alert(t('passwordMinLength') || 'Password must be at least 8 characters'); return; }
  (async function() {
    try {
      var res = await fetch('/api/HousingAccountManagement/' + studentId + '/reset-password', {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
        body: JSON.stringify({ newPassword: pwd })
      });
      if (res.ok) { alert(t('success')); } else { var d = await res.json(); alert(d.message || t('errorOccurred')); }
    } catch(e) { alert(t('apiError')); }
  })();
}

async function showHousingLifecycle(studentId, name) {
  var body = document.getElementById('housingLifecycleBody');
  body.innerHTML = '<p style="text-align:center;color:var(--gray-500);padding:20px">' + t('loading') + '</p>';
  document.getElementById('housingLifecycleModal').classList.add('open');
  if (_userRole !== 'admin') { body.innerHTML = '<p style="text-align:center;color:var(--red);padding:20px">' + t('apiError') + '</p>'; return; }
  try {
    var res = await fetch('/api/students/' + studentId + '/lifecycle', { headers: { 'Authorization': 'Bearer ' + token } });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    var data = await res.json();
    var logs = data.logs || [];
    if (logs.length === 0) { body.innerHTML = '<p style="text-align:center;color:var(--gray-500);padding:20px">' + t('noData') + '</p>'; return; }
    var html = '';
    if (name) html += '<div style="font-size:14px;font-weight:700;color:var(--navy);margin-bottom:12px">' + name + '</div>';
    logs.forEach(function(l) {
      var iconClass = (l.action === 'disabled') ? 'disabled' : (l.action === 'password_reset') ? 'password_reset' : '';
      var actionLabel = l.action === 'provisioned' ? t('actionProvisioned') :
                        l.action === 'enabled' ? t('actionEnabled') :
                        l.action === 'disabled' ? t('actionDisabled') :
                        l.action === 'password_reset' ? t('actionPasswordReset') : l.action;
      html += '<div class="log-entry"><div class="log-icon ' + iconClass + '">' + (l.action === 'disabled' ? '✗' : '✓') + '</div><div><div style="font-size:13px;font-weight:600">' + actionLabel + '</div><div style="font-size:11px;color:var(--gray-500)">' + (l.performerName ? t('by') + ' ' + l.performerName + ' | ' : '') + (l.performedAt ? new Date(l.performedAt).toLocaleString() : '') + (l.details ? ' | ' + l.details : '') + '</div></div></div>';
    });
    body.innerHTML = html;
  } catch(e) {
    body.innerHTML = '<p style="color:var(--red);text-align:center;padding:20px">' + t('apiError') + '</p>';
  }
}

var selectedDecision = null;
function selectApprove() {
  selectedDecision = 'approve';
  var el;
  document.getElementById('optApprove').className = 'review-option selected-approve';
  if (el = document.getElementById('optInfo')) el.className = 'review-option';
  if (el = document.getElementById('optReject')) el.className = 'review-option';
  document.getElementById('rejectReasonField').classList.remove('visible');
  document.getElementById('rejectReason').classList.remove('error');
  document.getElementById('rejectReasonError').style.display = 'none';
  document.getElementById('submitReviewBtn').disabled = false;
}
function selectReject() {
  selectedDecision = 'reject';
  document.getElementById('optApprove').className = 'review-option';
  var el;
  if (el = document.getElementById('optInfo')) el.className = 'review-option';
  if (el = document.getElementById('optReject')) el.className = 'review-option selected-reject';
  document.getElementById('rejectReasonField').classList.add('visible');
  document.getElementById('submitReviewBtn').disabled = false;
}
function selectInfo() {
  selectedDecision = 'request-info';
  document.getElementById('optApprove').className = 'review-option';
  document.getElementById('optInfo').className = 'review-option selected-approve';
  var el;
  if (el = document.getElementById('optReject')) el.className = 'review-option';
  document.getElementById('rejectReasonField').classList.remove('visible');
  document.getElementById('rejectReason').classList.remove('error');
  document.getElementById('rejectReasonError').style.display = 'none';
  document.getElementById('submitReviewBtn').disabled = false;
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
    if (currentStatus === 'pending_supervisor') { newStatus = 'need_more_info'; }
    else return;
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
  document.getElementById('submitReviewBtn').textContent = lang==='ar'?'جاري الإرسال...':'Submitting...';

  try {
    var res;
    if (isSelfRegAction) {
      var action = selectedDecision === 'approve' ? 'approve' : (selectedDecision === 'reject' ? 'reject' : 'request-info');
      res = await fetch('/api/Workflow/' + requestId + '/' + action, {
        method:'POST',
        headers:{'Content-Type':'application/json','Authorization':'Bearer '+token},
        body:JSON.stringify({notes:reason})
      });
    } else {
      res = await fetch('/api/requests/' + requestId + '/review', {
        method:'PUT',
        headers:{'Content-Type':'application/json','Authorization':'Bearer '+token},
        body:JSON.stringify({status:newStatus, reviewedBy:currentUser.id||1, notes:reason})
      });
    }
    if (res.status === 401) { localStorage.removeItem('staffToken'); localStorage.removeItem('staffUser'); window.location.replace('/Account/Login'); return; }
    if (!res.ok) {
      var err = await res.json();
      alert(err.message || (lang==='ar'?'فشل تحديث الطلب':'Failed to update request'));
      document.getElementById('submitReviewBtn').disabled = false;
      document.getElementById('submitReviewBtn').textContent = lang==='ar'?'تأكيد المراجعة':'Submit Review';
      return;
    }
    if (isSelfRegAction) {
      setTimeout(function() { window.location.href = '/Requests'; }, 1500);
    } else {
      loadRequest();
      loadUnreadCount();
    }
  } catch(e) {
    alert(lang==='ar'?'حدث خطأ في الاتصال':'Connection error');
    document.getElementById('submitReviewBtn').disabled = false;
    document.getElementById('submitReviewBtn').textContent = lang==='ar'?'تأكيد المراجعة':'Submit Review';
  }
}



function showAbout() {
  var lang = (document.getElementById('html-root')||document.documentElement).getAttribute('lang')||'ar';
  document.getElementById('aboutSysName').textContent = lang==='ar'?'نظام إسكان الطلاب والطالبات':'Students Housing System - Najran University';
  document.getElementById('aboutBuildDate').textContent = 'Build: 2026-07-01';
  document.getElementById('aboutModal').classList.add('open');
}
function closeAbout() { document.getElementById('aboutModal').classList.remove('open'); }
document.addEventListener('keydown',function(e){if(e.key==='Escape')closeAbout();});

setLang(localStorage.getItem('uiLanguage')||'ar');
if (!requestId) { document.getElementById('loading-state').style.display='none'; document.getElementById('error-state').style.display='block'; document.getElementById('error-message').textContent='No request ID specified.'; }
