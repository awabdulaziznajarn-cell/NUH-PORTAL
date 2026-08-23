// ==========================================================================
// محرك الترجمة (خفيف) — مصدر النصوص الوحيد هو window.__I18N
// window.__I18N بيتصدّر من اللياوت من ملفات .resx (SharedResource) حسب ثقافة السيرفر الحالية.
// يعني القاموس كله عايش في Resources/*.resx زي ما مشروع الـ permit عايش في ar.json/en.json،
// والـ JS بيقرأ منه بس (مفيش نصوص متيّبة هنا).
// التبديل الفعلي للغة بيحصل على السيرفر (كوكي .AspNetCore.Culture + reload) — الدوال دي بتطبّق
// النصوص على العناصر الديناميكية اللي لسه معتمدة على data-i18n / data-ar-data-en.
// ==========================================================================

var currentLang = (function () {
  var root = document.getElementById('html-root') || document.documentElement;
  var l = root ? root.getAttribute('lang') : null;
  return (l === 'en') ? 'en' : 'ar';
})();

// جلب ترجمة مفتاح من القاموس المُصدّر من الـ .resx (fallback للمفتاح نفسه لو مش موجود)
function t(key) {
  var d = (typeof window !== 'undefined' && window.__I18N) ? window.__I18N : {};
  return (d[key] != null) ? d[key] : key;
}

// ⚠️ نصّ احتياطي مكتوب في مكان النداء لو المفتاح لسه ماتضافش للـ resx.
//    اتنقلت هنا من request-details-page.js لأن أكتر من شاشة بقت تستخدمها
//    (وثيقة التعهّد بتتعرض في شاشتين) - ونسختين من نفس الدالة بيفترقوا.
//    القاعدة: المفتاح هو الأصل، والنصّ هنا شبكة أمان لحد ما يتضاف.
function tf(key, arText, enText) {
  var v = t(key);
  if (v !== key) return v;
  var root = document.getElementById('html-root');
  var lng = root ? (root.getAttribute('lang') || 'ar') : 'ar';
  return lng === 'en' ? enText : arText;
}

// أكواد الكليات/الأقسام (زي ما بيتخزّنوا من فورم التسجيل) → مفاتيح reg_*.
// لو القيمة كود معروف بنترجمه، وأي حاجة تانية (نص عربي جاهز) بترجع زي ما هي.
var COLLEGE_KEYS = { engineering: 'reg_collegeEngineering', medicine: 'reg_collegeMedicine', cs: 'reg_collegeCS', science: 'reg_collegeScience', business: 'reg_collegeBusiness', arts: 'reg_collegeArts', education: 'reg_collegeEducation', pharmacy: 'reg_collegePharmacy' };
var DEPT_KEYS = { cs: 'reg_deptCS', computer: 'reg_deptComputer', electrical: 'reg_deptElectrical', mechanical: 'reg_deptMechanical', civil: 'reg_deptCivil', math: 'reg_deptMath', physics: 'reg_deptPhysics', chemistry: 'reg_deptChemistry', biology: 'reg_deptBiology', business: 'reg_deptBusiness', accounting: 'reg_deptAccounting', islamic: 'reg_deptIslamic', arabic: 'reg_deptArabic', english: 'reg_deptEnglish' };
// خريطة القوائم من قاعدة البيانات — بتتصدّر من اللياوت في window.__LKMAP حسب لغة الطلب.
// الأولوية ليها؛ لو مش موجودة (لسه ماتهاجرتش) بنرجع لمفاتيح الـ .resx زي الأول.
function lkName(cat, code) {
  try {
    var m = (typeof window !== 'undefined' && window.__LKMAP && window.__LKMAP[cat]) ? window.__LKMAP[cat] : null;
    if (m) { var key = (code == null ? '' : String(code)).trim().toLowerCase(); if (m[key] != null && m[key] !== '') return m[key]; }
  } catch (e) { }
  return null;
}
function collegeName(c) { var raw = (c == null ? '' : String(c)); var db = lkName('college', raw); if (db) return db; var k = COLLEGE_KEYS[raw.trim().toLowerCase()]; if (!k) return raw; var v = t(k); return v === k ? raw : v; }
function deptName(d) { var raw = (d == null ? '' : String(d)); var db = lkName('department', raw); if (db) return db; var k = DEPT_KEYS[raw.trim().toLowerCase()]; if (!k) return raw; var v = t(k); return v === k ? raw : v; }
// المباني والمستويات: الاسم من قاعدة البيانات لو موجود، وإلا الكود الخام زي ما هو
function buildingName(b) { var raw = (b == null ? '' : String(b)); var db = lkName('building', raw); return db ? db : raw; }
function levelName(l) { var raw = (l == null ? '' : String(l)); var db = lkName('level', raw); return db ? db : raw; }

// ترقية الخلايا المرندرة من السيرفر (data-lk="college:CODE") لاسم القائمة من قاعدة البيانات لو معروف.
// بتخلّي الكليات اللي الأدمن ضافها تظهر باسمها المترجم بدل الكود، من غير ما نلمس رندر السيرفر.
function upgradeLookupCells(root) {
  try {
    (root || document).querySelectorAll('[data-lk]').forEach(function (el) {
      var spec = el.getAttribute('data-lk'); if (!spec) return;
      var i = spec.indexOf(':'); if (i < 0) return;
      var cat = spec.slice(0, i), code = spec.slice(i + 1);
      var name = cat === 'college' ? collegeName(code)
               : cat === 'department' ? deptName(code)
               : cat === 'building' ? buildingName(code)
               : cat === 'level' ? levelName(code)
               : null;
      if (name != null && name !== '') el.textContent = name;
    });
  } catch (e) { }
}
if (typeof document !== 'undefined') {
  if (document.readyState !== 'loading') upgradeLookupCells();
  else document.addEventListener('DOMContentLoaded', function () { upgradeLookupCells(); });
}

// تطبيق النصوص على عناصر الصفحة (للصفحات اللي لسه بتستخدم data-* — الصفحات المحوّلة بتترندر من السيرفر)
function __baseSetLang(l) {
  currentLang = (l === 'en') ? 'en' : 'ar';
  var root = document.getElementById('html-root') || document.documentElement;
  if (root) {
    root.setAttribute('lang', currentLang);
    root.setAttribute('dir', currentLang === 'ar' ? 'rtl' : 'ltr');
  }
  var btnAr = document.getElementById('btn-ar');
  var btnEn = document.getElementById('btn-en');
  if (btnAr) btnAr.classList.toggle('active', currentLang === 'ar');
  if (btnEn) btnEn.classList.toggle('active', currentLang === 'en');

  document.querySelectorAll('[data-ar]').forEach(function (el) {
    var newText = currentLang === 'ar' ? el.dataset.ar : el.dataset.en;
    if (el.children.length === 0) {
      el.textContent = newText;
    } else {
      var child = el.firstChild;
      if (child && child.nodeType === Node.TEXT_NODE) child.textContent = newText;
    }
  });
  document.querySelectorAll('[data-i18n]').forEach(function (el) { el.textContent = t(el.dataset.i18n); });
  document.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) { el.placeholder = t(el.dataset.i18nPlaceholder); });
  document.querySelectorAll('[data-i18n-value]').forEach(function (el) { el.value = t(el.dataset.i18nValue); });
  document.querySelectorAll('[data-i18n-title]').forEach(function (el) { el.title = t(el.dataset.i18nTitle); });
  document.querySelectorAll('[data-i18n-alt]').forEach(function (el) { el.alt = t(el.dataset.i18nAlt); });
  // ⚠️ نص فيه وسوم (زي <strong>) — النص في الـ resx نفسه ومصدره موثوق (ملفاتنا)،
  //    مش مدخلات مستخدم، فـ innerHTML هنا مافيهاش خطر حقن.
  document.querySelectorAll('[data-i18n-html]').forEach(function (el) { el.innerHTML = t(el.dataset.i18nHtml); });
  document.querySelectorAll('[data-i18n-aria]').forEach(function (el) { el.setAttribute('aria-label', t(el.dataset.i18nAria)); });

  var d = document.getElementById('dateNow');
  if (d) {
    var now = new Date();
    d.textContent = currentLang === 'ar'
      // ⚠️ تالت نسخة من ترويسة التاريخ (التخطيط + reports-page + هنا).
      //    الفحص على NuhFmt لأن الملف ده بيتحمّل في صفحات بوابة الطالب كمان
      //    واللي منها ما بيحمّلش date-format.js.
      ? (typeof NuhFmt !== 'undefined' ? NuhFmt.dateFull(now)
          : now.toLocaleDateString('ar-EG', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }))
      : (typeof NuhFmt !== 'undefined' ? NuhFmt.dateFull(now)
          : now.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }));
  }
  var sidebarName = document.getElementById('sidebar-user-name');
  var sidebarRole = document.getElementById('sidebar-user-role');
  if (sidebarName) {
    try {
      var u = JSON.parse(localStorage.getItem('staffUser') || localStorage.getItem('studentUser') || '{}');
      sidebarName.textContent = u.full_name || u.username || '';
      if (sidebarRole) sidebarRole.textContent = u.role || '';
    } catch (e) { /* بيانات تخزين تالفة — تجاهل */ }
  }
}

var onLanguageChange = null;

// ==========================================================================
//  بوابة الطالب: القاموس بيتجاب من نقطة نهاية بدل ما يتحقن من اللياوت.
//
//  ⚠️ صفحات البوابة (wwwroot/register/*.html) ملفات HTML ثابتة — مبتعدّيش على
//     _Layout.cshtml، فـ window.__I18N ما كانش موجود عندها أصلًا. فـ t() كانت
//     بترجّع المفتاح نفسه، وأي عنصر [data-i18n] ما كانش بيتترجم. النتيجة اللي
//     كان الطالب شايفها: زرار «English» بيقلب اتجاه الصفحة وخلاص.
//
//     دلوقتي بنجيب نفس القاموس من نفس ملفات الـ resx عبر /api/ui/i18n —
//     فالنص مكتوب مرة واحدة ويخدم شاشة الموظف وصفحة الطالب.
// ==========================================================================
var __i18nRemote = false;      // الصفحة دي محتاجة تجيب القاموس؟
var __i18nDictLang = null;     // لغة القاموس المحمّل حاليًا

// ==========================================================================
//  __i18nReady — وعد بيتحقّق لما القاموس وجدول المسار يبقوا جاهزين.
//
//  ⚠️ صفحات البوابة بتجيب القاموس بـ fetch، يعني وقت ما سكربت الصفحة بيشتغل
//     ممكن يكون لسه مجاش. الصفحة اللي بترسم صفوف فيها نصوص مترجمة لازم
//     تستنّاه، وإلا بترسم بالمفاتيح الخام (req_badge_submitted) لأن t()
//     بترجّع المفتاح لما ما يلاقيهوش.
//  ⚠️ وفي شاشات الموظفين بيتحقّق فورًا: القاموس محقون من اللياوت قبل أي
//     سكربت، فمفيش انتظار أصلًا.
// ==========================================================================
var __i18nReadyResolve = null;
var __i18nReady = new Promise(function (res) { __i18nReadyResolve = res; });
if (typeof window !== 'undefined') window.__i18nReady = __i18nReady;

function __i18nFetch(lang) {
  return fetch('/api/ui/i18n?lang=' + encodeURIComponent(lang), { credentials: 'same-origin' })
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (d) {
      if (!d) return;
      window.__I18N = d.i18n || {};
      window.__LKMAP = d.lookups || {};
      // ⚠️ جدول مسار الطلب - نفس المصدر اللي شاشات الموظفين بتقرا منه
      //    (Core/RequestWorkflow.cs). من غيره كانت صفحات البوابة بتكتب
      //    أسماء المراحل بنفسها وتفارق الخادم. js/request-workflow.js
      //    بيقرا من window.__WF بالظبط زي شاشات الموظفين.
      if (d.workflow) window.__WF = d.workflow;
      __i18nDictLang = lang;
    })
    .catch(function () {
      // الشبكة وقعت: الصفحة تفضل بنصها العربي المكتوب في الـ HTML بدل ما تفضى
    });
}

function __applyLang(l) {
  try { __baseSetLang(l); } catch (e) { console.warn('__baseSetLang error:', e); }
  try { upgradeLookupCells(); } catch (e) { }
  // ⚠️ عنوان التبويب كان بيتحدّث في applyLang وحدها - وهي مربوطة بزرّ تبديل
  //    اللغة بس. يعني الطالب اللي لغته إنجليزي محفوظة بيفتح الصفحة فيلاقي
  //    كل حاجة إنجليزي وعنوان التبويب لوحده عربي.
  try { __applyTitle(); } catch (e) { }
  if (typeof onLanguageChange === 'function') { try { onLanguageChange(currentLang); } catch (e) { console.warn('onLanguageChange error:', e); } }
}

function setLang(l) {
  var want = (l === 'en') ? 'en' : 'ar';

  // شاشات الموظفين: القاموس محقون من اللياوت وتبديل اللغة بيتم على السيرفر.
  // ما نلمسش تخزين المتصفح هنا عشان ما نخالفش ثقافة الطلب.
  if (!__i18nRemote) { __applyLang(want); return; }

  try { localStorage.setItem('uiLanguage', want); } catch (e) { }
  if (__i18nDictLang === want) { __applyLang(want); return; }
  __i18nFetch(want).then(function () { __applyLang(want); });
}

// تشغيل البوابة: بيحصل مرة واحدة أول ما الملف يتحمّل (وهو في <head>)
(function () {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;
  if (window.__I18N) {                                            // شاشة موظف
    __i18nDictLang = currentLang;
    if (__i18nReadyResolve) __i18nReadyResolve();
    return;
  }
  __i18nRemote = true;

  var root = document.documentElement;
  var want = 'ar';
  try { want = (localStorage.getItem('uiLanguage') === 'en') ? 'en' : 'ar'; } catch (e) { }

  // ⚠️ اللغة والاتجاه بيتظبطوا فورًا، من غير انتظار القاموس. السبب: أي كود
  //    بيقرا currentLang وقت التحميل — زي lookups.js لما بيبعت lang= في
  //    الرابط — كان بيشوفها 'ar' دايمًا لأن ملف الـ HTML ثابت ومكتوب فيه
  //    lang="ar". فالنصوص كانت بتتترجم إنجليزي وأسماء الكليات ترجع عربي.
  currentLang = want;
  root.setAttribute('lang', want);
  root.setAttribute('dir', want === 'ar' ? 'rtl' : 'ltr');

  // ⚠️ النص العربي مكتوب في الـ HTML نفسه، فالعربي مافيهوش وميض ولا انتظار.
  //    الإنجليزي بس هو اللي محتاج القاموس — من غير الإخفاء ده الطالب هيشوف
  //    الصفحة عربي وبعدين تتبدّل قدامه.
  if (want === 'en') {
    root.classList.add('i18n-wait');
    // شبكة بطيئة ماينفعش تسيب الصفحة بيضا — بعد ثانيتين تظهر زي ما هي
    setTimeout(function () { root.classList.remove('i18n-wait'); }, 2000);
  }

  __i18nFetch(want).then(function () {
    if (__i18nReadyResolve) __i18nReadyResolve();
    function go() {
      try { __applyLang(want); } catch (e) { }
      root.classList.remove('i18n-wait');
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', go);
    else go();
  });
})();

// ⚠️ اسم الصفحة يُلتقط مرة واحدة عند التحميل قبل أي تبديل للغة. الدالة تحته
//    كانت تكتب اسم البوابة فوق العنوان دائمًا، فصفحات بوابة الطالب الثماني
//    كانت تظهر في المتصفح بعنوان واحد لا يُفرَّق بينها: «طلباتي» و«نموذج
//    التسجيل» و«تتبع الطلب» كلها تبويبات متطابقة.
var __pageTitle = (function () {
  var first = (document.title || '').split(' - ')[0].trim();
  return first;
})();

// توحيد عنوان التبويب في بوابة الطالب مع بقية النظام: «اسم الصفحة - اسم البوابة».
// ⚠️ اسم البوابة من Brand_Title وحده — نفس المفتاح الذي تقرأ منه القائمة
//    الجانبية وصفحة الدخول وعنوان التبويب في شاشات الموظفين. كان لكل موضع
//    مفتاحه الخاص بالقيمة نفسها، فتغيير الاسم كان يستلزم تعديل ستة مفاتيح.
// ⚠️ اسم الصفحة من مفتاح ترجمة لا من النصّ المكتوب في <title>: النصّ ده ثابت
//    عربي في ملف الـ HTML، فبعد التبديل للإنجليزي كان عنوان التبويب بيفضل
//    «طلباتي - University Housing Portal» - نصّين بلغتين في سطر واحد.
//    المفتاح بيتكتب على <html data-title-key="...">.
function __applyTitle() {
  var brand = t('Brand_Title');
  var root = document.getElementById('html-root') || document.documentElement;
  var key = root ? root.getAttribute('data-title-key') : null;
  var name = __pageTitle;
  if (key) { var v = t(key); if (v !== key) name = v; }   // المفتاح ناقص؟ نرجع للنصّ
  document.title = (name && name !== brand) ? name + ' - ' + brand : brand;
}

function applyLang(l) {
  setLang(l);
  __applyTitle();
}
