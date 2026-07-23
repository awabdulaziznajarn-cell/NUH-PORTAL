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

// أكواد الكليات/الأقسام (زي ما بيتخزّنوا من فورم التسجيل) → مفاتيح reg_*.
// لو القيمة كود معروف بنترجمه، وأي حاجة تانية (نص عربي جاهز) بترجع زي ما هي.
var COLLEGE_KEYS = { engineering: 'reg_collegeEngineering', medicine: 'reg_collegeMedicine', cs: 'reg_collegeCS', science: 'reg_collegeScience', business: 'reg_collegeBusiness', arts: 'reg_collegeArts', education: 'reg_collegeEducation', pharmacy: 'reg_collegePharmacy' };
var DEPT_KEYS = { cs: 'reg_deptCS', computer: 'reg_deptComputer', electrical: 'reg_deptElectrical', mechanical: 'reg_deptMechanical', civil: 'reg_deptCivil', math: 'reg_deptMath', physics: 'reg_deptPhysics', chemistry: 'reg_deptChemistry', biology: 'reg_deptBiology', business: 'reg_deptBusiness', accounting: 'reg_deptAccounting', islamic: 'reg_deptIslamic', arabic: 'reg_deptArabic', english: 'reg_deptEnglish' };
function collegeName(c) { var raw = (c == null ? '' : String(c)); var k = COLLEGE_KEYS[raw.trim().toLowerCase()]; if (!k) return raw; var v = t(k); return v === k ? raw : v; }
function deptName(d) { var raw = (d == null ? '' : String(d)); var k = DEPT_KEYS[raw.trim().toLowerCase()]; if (!k) return raw; var v = t(k); return v === k ? raw : v; }

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

  var d = document.getElementById('dateNow');
  if (d) {
    var now = new Date();
    d.textContent = currentLang === 'ar'
      ? now.toLocaleDateString('ar-EG', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })
      : now.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
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

function setLang(l) {
  try { __baseSetLang(l); } catch (e) { console.warn('__baseSetLang error:', e); }
  if (typeof onLanguageChange === 'function') { try { onLanguageChange(currentLang); } catch (e) { console.warn('onLanguageChange error:', e); } }
}

// لصفحات الدخول القديمة
function applyLang(l) {
  setLang(l);
  document.title = t('appTitle');
}
