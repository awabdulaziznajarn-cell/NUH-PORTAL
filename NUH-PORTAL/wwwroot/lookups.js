// ==========================================================================
// lookups.js — ملء قوائم النماذج (تسجيل/تعديل) من /api/lookups/*
// القيمة المخزّنة تفضل الـ Code زي ما هي (متوافقة مع الباك-إند الحالي)،
// والنص المعروض هو الاسم المترجم من قاعدة البيانات (حسب ثقافة الطلب).
// المباني تترجّع حسب النوع (?gender=)، والأقسام حسب الكلية (?collegeId=).
// ==========================================================================
var Lookups = (function () {
  // ⚠️ كانت بتقرأ staffToken من localStorage. محدش بيكتب المفتاح ده من يوم ما
  //    الدخول اتوحّد على الكوكي، واللي فاضل منه في متصفح قديم توكن منتهي —
  //    والترويسة بتسبق الكوكي فيترفض النداء ٤٠١. الكوكي بيتبعت لوحده.
  function headers() { return {}; }
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

  // ====================================================================
  //  قائمة تابعة لقائمة تانية (الأقسام تتبع الكلية، والمباني تتبع النوع…)
  //
  //  ⚠️ العيب اللي بتعالجه:
  //     قائمة «القسم» كانت بتتحمّل بكل الأقسام من كل الكليات قبل ما المستخدم
  //     يختار كلية أصلًا. فبتفتح على عشرات الخيارات المختلطة، وممكن يختار
  //     قسمًا لا يتبع الكلية اللي هيختارها بعد كده — بيانات متضاربة تعدّي
  //     من غير ما حد يلاحظ.
  //
  //     نفس القاعدة كانت متطبّقة صح على المباني (لا تفتح قبل اختيار النوع)،
  //     لكنها كانت مكتوبة جوّه شاشة الموظف، فالأقسام ما أخدتش منها ولا
  //     بوابة الطالب أخدت منها. القاعدة هنا مرة واحدة، والشاشتين بتاخدوا منها.
  //
  //  السلوك:
  //     • الأب فاضي  → الابن مقفول، ونصّه «اختر الكلية أولًا».
  //     • الأب اتغيّر → الابن يتصفّى ويتحمّل من جديد ويتفتح.
  //     • لو الفلترة رجّعت فاضي → نرجع للقائمة الكاملة بدل ما نقفل الطريق.
  // ====================================================================
  function dependent(opts) {
    var parent = opts.parent, child = opts.child;
    if (!parent || !child) return { refresh: function () {} };

    function setOnly(text, disabled) {
      child.innerHTML = '';
      var o = document.createElement('option');
      o.value = ''; o.textContent = text;
      child.appendChild(o);
      child.value = '';
      child.disabled = !!disabled;
    }

    async function apply() {
      var id = selectedId(parent);
      var raw = parent.value;
      if (!id && !raw) { setOnly(opts.waitText, true); return; }

      setOnly(opts.loadingText || opts.chooseText, true);

      var items = await fetchList(opts.url(id, raw));
      if (!items.length && opts.fallbackUrl) items = await fetchList(opts.fallbackUrl);

      setOnly(opts.chooseText, false);
      fill(child, items, { keep: opts.keep || '' });
      opts.keep = '';                 // القيمة المحفوظة تُستعمل مرة واحدة بس
      child.disabled = false;
    }

    parent.addEventListener('change', function () { apply(); });
    apply();
    return { refresh: apply };
  }

  return { fetchList: fetchList, fill: fill, selectedId: selectedId, headers: headers, dependent: dependent };
})();
