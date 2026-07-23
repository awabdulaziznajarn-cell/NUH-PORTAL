// ==========================================================================
// lookups.js — ملء قوائم النماذج (تسجيل/تعديل) من /api/lookups/*
// القيمة المخزّنة تفضل الـ Code زي ما هي (متوافقة مع الباك-إند الحالي)،
// والنص المعروض هو الاسم المترجم من قاعدة البيانات (حسب ثقافة الطلب).
// المباني تترجّع حسب النوع (?gender=)، والأقسام حسب الكلية (?collegeId=).
// ==========================================================================
var Lookups = (function () {
  function headers() { var tk = localStorage.getItem('staffToken'); return tk ? { 'Authorization': 'Bearer ' + tk } : {}; }
  function escOpt(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }

  async function fetchList(url) {
    try {
      var res = await fetch(url, { headers: headers() });
      if (!res.ok) return [];
      return await res.json();
    } catch (e) { return []; }
  }

  // ملء <select> بعناصر lookup. بيحافظ على عنصر الـ placeholder (value="") لو موجود.
  // opts.keep: قيمة (code) نحاول نحافظ عليها بعد الملء.
  function fill(sel, items, opts) {
    opts = opts || {};
    if (!sel) return;
    var ph = sel.querySelector('option[value=""]');
    var html = ph ? ph.outerHTML : '';
    (items || []).forEach(function (it) {
      html += '<option value="' + escOpt(it.code) + '" data-id="' + it.id + '">' + escOpt(it.name) + '</option>';
    });
    sel.innerHTML = html;
    if (opts.keep != null && opts.keep !== '') {
      sel.value = String(opts.keep);
      if (sel.selectedIndex < 0) sel.selectedIndex = 0;
    }
  }

  // id الكلية/العنصر المرتبط بالخيار المختار (من data-id) — عشان الفلترة التابعة
  function selectedId(sel) {
    if (!sel || sel.selectedIndex < 0) return null;
    var o = sel.options[sel.selectedIndex];
    var v = o ? o.getAttribute('data-id') : null;
    return v ? parseInt(v, 10) : null;
  }

  return { fetchList: fetchList, fill: fill, selectedId: selectedId, headers: headers };
})();
