// ============================================================================
//  عدّ تصاعدي لأرقام البطاقات - التعريف الوحيد في النظام.
//
//  ⚠️ ليه ملف مشترك لا سطرين في كل شاشة:
//     أرقام البطاقات بتتكتب في تسع شاشات بتسع طرق - بعضها من الخادم في
//     Razor (الرئيسية، قائمة الطلاب)، وبعضها من الجافاسكربت بعد نداء API
//     (التقارير، سجل العمليات، الرفع الجماعي، سكن أعضاء هيئة التدريس).
//     لو الحركة اتكتبت في كل شاشة كانت هتبقى تسع نسخ بتسع مدد وتسع منحنيات،
//     وأول شاشة جديدة تتضاف هتفضل بلا حركة ومحدّش ياخد باله.
//
//  ⚠️ وعشان كده الملف ده **مابيطلبش** من الشاشات تناديه: هو بيراقب الصفحة
//     بـ MutationObserver، فالرقم اللي بيوصل من الـ API بعد ثانيتين بياخد
//     نفس الحركة بتاعة الرقم اللي جه مع الصفحة - من غير سطر واحد في شاشته.
//
//  ⚠️ ومابيغيّرش أي قيمة: بيعرض الطريق للرقم اللي الشاشة كتبته وبيقف عنده
//     بالظبط. لو الحركة اتعطّلت لأي سبب، اللي بيبان هو الرقم الصح فورًا -
//     أسوأ حالة إن الحركة تضيع، مش إن الرقم يغلط.
// ============================================================================
var NuhCount = (function () {
  'use strict';

  // ⚠️ القائمة دي هي كل «رقم ملخّص» في النظام: أرقام البطاقات وعدّادات
  //    التبويبات. مش .info-value ولا أرقام الجداول - دي قيم بيانات بيتقرا
  //    فيها الرقم مباشرةً، والحركة عليها بتأخّر القراءة بلا فايدة.
  //    و [data-count] للي بيتضاف بعدين: أي عنصر جديد بياخد الحركة بالخاصية
  //    دي بلا تعديل هنا.
  var SELECTOR = '.stat-num, .stat-value, .tab-count, .rp-kpi .v, [data-count]';

  // ⚠️ المدّة مش رقمًا واحدًا: الرقم الكبير (٢٤٢) محتاج وقت عشان العدّ
  //    يبان عدًّا، والرقم الصغير (٣) على نفس المدّة بيبقى تلات قفزات بطيئة
  //    شكلها متلعثم لا هادي. فالمدّة بتكبر مع حجم الرقم وبتقف عند حدّ:
  //    من ١٤٠٠ للأرقام الصغيرة لحد ٢٨٠٠ للكبيرة.
  function duration(delta) {
    var d = Math.abs(delta);
    if (d <= 5) return 1400;
    return Math.min(2800, 1200 + d * 14);
  }

  // ⚠️ الأرقام الصحيحة بس: النسب («٨٥٪») والتواريخ والنصوص بتعدّي زي ما هي.
  //    والفاصلة بتتشال قبل القراءة وبترجع بعدها، عشان «1,240» ما تبقاش «1240».
  var INT_RE = /^-?\d{1,9}$/;

  function reduced() {
    try {
      return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    } catch (e) { return false; }
  }

  function parse(el) {
    var raw = (el.textContent || '').trim();
    if (!raw) return null;
    var grouped = raw.indexOf(',') > -1;
    var clean = raw.replace(/,/g, '');
    if (!INT_RE.test(clean)) return null;
    return { value: parseInt(clean, 10), grouped: grouped };
  }

  function write(el, n, grouped) {
    el.textContent = grouped ? n.toLocaleString('en-US') : String(n);
  }

  // ⚠️ منحنى بيبدأ سريع وبيهدى بقوّة في الآخر (القوّة الخامسة لا التالتة):
  //    الفرق إن العدّ بيقطع أغلب المسافة في أول تلت الوقت، وبعدين بيزحف
  //    على آخر أرقام - فالعين بتشوفه «بيوصل» لـ٢٤٢ بدل ما يتقطع عندها.
  //    القوّة التالتة كانت بتنزل بالتساوي تقريبًا، فالوقوف كان بيبان مفاجئًا
  //    مهما طوّلنا المدّة.
  function ease(t) { return 1 - Math.pow(1 - t, 5); }

  function animate(el, from, to, grouped) {
    // ⚠️ العلامة دي هي اللي بتمنع الحلقة اللانهائية: إحنا بنكتب في العنصر،
    //    والكتابة بتوقظ المراقب، والمراقب بيمسح الصفحة تاني - ولو مافيش
    //    علامة كان هيلاقي رقمًا وسطًا ويبدأ حركة جديدة عليه، وهكذا للأبد.
    el.__nuhBusy = true;
    el.__nuhTarget = to;

    // ⚠️ أرقام بعرض ثابت أثناء العدّ: من غيرها البطاقة بتترجرج يمين وشمال
    //    لأن عرض الرقم ١ أقل من عرض الرقم ٨ في أغلب الخطوط.
    var prevNum = el.style.fontVariantNumeric;
    el.style.fontVariantNumeric = 'tabular-nums';

    var ms = duration(to - from);
    var start = 0;
    function step(ts) {
      if (!start) start = ts;
      var t = Math.min(1, (ts - start) / ms);
      write(el, Math.round(from + (to - from) * ease(t)), grouped);
      if (t < 1) { requestAnimationFrame(step); return; }
      write(el, to, grouped);
      el.style.fontVariantNumeric = prevNum;
      // ⚠️ الإفراج بعد رسم الإطار الأخير لا معاه: الكتابة الأخيرة بتوقظ
      //    المراقب، ولو العلامة اتشالت في نفس اللحظة كان ممكن يشوف العنصر
      //    فاضي البال ويعيد الحركة على نفس الرقم.
      requestAnimationFrame(function () { el.__nuhBusy = false; });
    }
    requestAnimationFrame(step);
  }

  function apply(el) {
    if (!el || el.__nuhBusy) return;
    var p = parse(el);
    if (!p) return;

    // نفس الرقم اللي واقفين عنده - مفيش حاجة تتحرّك. ده اللي بيخلّي إعادة
    // رسم الشاشة (فلتر، ترقيم صفحات) ما تعيدش الحركة بلا سبب.
    if (el.__nuhTarget === p.value) return;

    var from = typeof el.__nuhTarget === 'number' ? el.__nuhTarget : 0;
    if (reduced() || p.value === from) {
      el.__nuhTarget = p.value;
      write(el, p.value, p.grouped);
      return;
    }
    animate(el, from, p.value, p.grouped);
  }

  function scan(root) {
    var scope = root && root.querySelectorAll ? root : document;
    var list = scope.querySelectorAll(SELECTOR);
    for (var i = 0; i < list.length; i++) apply(list[i]);
  }

  // ⚠️ مسحة واحدة لكل إطار لا مسحة لكل تغيير: تحديث بطاقة بيولّد عشرات
  //    التغييرات في نفس اللحظة، ومسحة لكل واحدة معناها عشرات المسحات
  //    لنفس النتيجة.
  var queued = false;
  function schedule() {
    if (queued) return;
    queued = true;
    requestAnimationFrame(function () { queued = false; scan(document); });
  }

  function start() {
    scan(document);
    if (!window.MutationObserver) return;
    new MutationObserver(schedule).observe(document.body, {
      childList: true, subtree: true, characterData: true
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }

  // مكشوفة للشاشة اللي عايزة تشغّل الحركة على عنصر بعينه بإيدها.
  return { scan: scan, apply: apply };
})();
