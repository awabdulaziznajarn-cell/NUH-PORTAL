// ==========================================================================
//  date-field.js — حقل تاريخ بشكل النظام بدل تقويم نظام التشغيل.
//
//  ⚠️ ليه الملف ده موجود:
//     <input type="date"> بيرسمه المتصفح ونظام التشغيل مش الصفحة. النتيجة في
//     نظام عربي كامل:
//       • التقويم بينزل **بالإنجليزي دايمًا**: August 2026 وSu Mo Tu،
//         مهما كانت لغة الصفحة.
//       • النصّ الإرشادي mm/dd/yyyy أو dd/mm/yyyy حسب إعدادات ويندوز نفسه،
//         يعني نفس الشاشة شكلها مختلف من جهاز للتاني في نفس المكتب.
//       • خطّه وحوافه وألوانه من ويندوز، فبيبان دخيلًا وسط شاشة كل حقولها
//         بشكل النظام (نفس سبب select-field.js بالظبط).
//       • ومفيش هجري — وده مطلوب في جهة سعودية.
//
//     ١٣ حقل تاريخ في ٦ شاشات كانوا كلهم كده.
//
//  ⚠️ نفس أسلوب select-field.js عن قصد: العنصر الأصلي <input type="date">
//     بيفضل مكانه (مخفي بصريًا) وبناخد منه ونحطّ فيه القيمة بصيغة ISO
//     (yyyy-MM-dd) زي ما هي. يعني:
//       • كل الكود القديم يفضل شغّال بلا تعديل: el.value و addEventListener
//         ('change') وبناء الروابط — كله على العنصر الأصلي.
//       • الخادم بيستقبل نفس الصيغة بالظبط، فمفيش تغيير في أي API.
//       • لو الجافاسكريبت اتعطّل، الحقل الأصلي يفضل شغّال.
//
//  ⚠️ الهجري عرض فقط: الاختيار بيتحوّل ميلادي قبل ما يتكتب في العنصر. التخزين
//     والفلترة والتقارير كلها ميلادي — وده مقصود، لأن أي تحويل عند التخزين
//     بيخلّي نفس الصفّ يتقرا بتاريخين حسب مين فتحه.
//
//  الاستخدام: تلقائي على كل <input type="date">.
//     للاستثناء: <input type="date" data-nuh-date="off">
// ==========================================================================
var NuhDate = (function () {

  var OPEN = null;                   // اللوحة المفتوحة حاليًا (واحدة بحد أقصى)

  // ⚠️ اختيار التقويم ودالة التحويل الهجري مالكهم NuhFmt (js/date-format.js)
  //    مش هنا. كانوا هنا، فكان التبديل بيغيّر شكل الحقل وحده والجدول اللي
  //    تحته يفضل بالتقويم القديم — المستخدم يبدّل وما يعرفش إذا التبديل حصل
  //    فعلًا ولا لأ. المالك واحد دلوقتي، والحقل والجدول بيتقلبوا مع بعض.

  // ⚠️ الأرقام لاتينية في العربي كمان: الرقم الجامعي والهوية والجوال ورقم الطلب
  //    (2026-000019) كلهم لاتينيين في كل النظام. لو التاريخ وحده بقى ٢٠٢٦ كان
  //    الجدول الواحد فيه عمودين بأرقام مختلفة الشكل.
  var AR_MONTHS_G = ['يناير','فبراير','مارس','أبريل','مايو','يونيو',
                     'يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'];
  var EN_MONTHS_G = ['January','February','March','April','May','June',
                     'July','August','September','October','November','December'];
  var AR_MONTHS_H = ['محرم','صفر','ربيع الأول','ربيع الآخر','جمادى الأولى','جمادى الآخرة',
                     'رجب','شعبان','رمضان','شوال','ذو القعدة','ذو الحجة'];
  var EN_MONTHS_H = ['Muharram','Safar','Rabi I','Rabi II','Jumada I','Jumada II',
                     'Rajab','Shaban','Ramadan','Shawwal','Dhul-Qadah','Dhul-Hijjah'];
  // الأسبوع بيبدأ الأحد — زي التقويم السعودي والرسمي
  var AR_DOW = ['أحد','إثنين','ثلاثاء','أربعاء','خميس','جمعة','سبت'];
  var EN_DOW = ['Su','Mo','Tu','We','Th','Fr','Sa'];

  function lang() {
    var r = document.getElementById('html-root') || document.documentElement;
    return (r.getAttribute('lang') || 'ar').indexOf('en') === 0 ? 'en' : 'ar';
  }
  function tr(key, fallbackAr, fallbackEn) {
    if (typeof t === 'function') { var v = t(key); if (v && v !== key) return v; }
    return lang() === 'en' ? fallbackEn : fallbackAr;
  }
  function pad(n) { return String(n).padStart(2, '0'); }

  // ---------------- التحويل بين ISO و Date ----------------
  // ⚠️ التقسيم اليدوي لا new Date(str): new Date('2026-08-18') بيتقري UTC،
  //    فالمستخدم شرق جرينتش ممكن يشوف اليوم اللي قبله.
  function parseIso(v) {
    var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec((v || '').trim());
    return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null;
  }
  function toIso(d) {
    return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
  }

  // ---------------- الهجري ----------------
  // ⚠️ التحويل من Intl لا بجدول محسوب بالإيد: تقويم أم القرى مضبوط في المتصفح
  //    ومحدَّث معاه، وأي جدول مكتوب هنا كان هيفرق يوم أو يومين في شهور معيّنة.
  var HIJRI_OK = NuhFmt.hijriSupported;
  var hijriParts = NuhFmt.hijriParts;

  // ⚠️ العكس (هجري → ميلادي) مفيش له API. بنقدّر اليوم الميلادي تقريبًا من
  //    متوسط طول الشهر القمري، وبعدين نمشي يوم يوم لحد ما نضبط. المدى ±٤٠ يوم
  //    كفاية لأي خطأ في التقدير، والحلقة بتقف أول ما تلاقي.
  function hijriToGreg(hy, hm, hd) {
    if (!HIJRI_OK) return null;
    var approx = Math.floor((hy - 1) * 354.367 + (hm - 1) * 29.53 + hd);
    var guess = new Date(622, 6, 16);                 // ١ محرم ١ هـ تقريبًا
    guess.setDate(guess.getDate() + approx);
    for (var i = -40; i <= 40; i++) {
      var c = new Date(guess.getFullYear(), guess.getMonth(), guess.getDate() + i);
      var p = hijriParts(c);
      if (p && p.y === hy && p.m === hm && p.d === hd) return c;
    }
    return null;
  }

  // عدد أيام شهر هجري — بالبحث عن أول يوم في الشهر اللي بعده
  function hijriMonthLength(hy, hm) {
    var ny = hm === 12 ? hy + 1 : hy, nm = hm === 12 ? 1 : hm + 1;
    var a = hijriToGreg(hy, hm, 1), b = hijriToGreg(ny, nm, 1);
    if (!a || !b) return 30;
    return Math.round((b - a) / 86400000);
  }

  var mode = NuhFmt.mode;
  var setMode = NuhFmt.setMode;

  // ---------------- نصّ الحقل ----------------
  // ⚠️ الصيغة واحدة في اللغتين: dd/mm/yyyy. كانت بتتغيّر مع إعدادات ويندوز،
  //    فموظف بيقرا 08/09 على إنه ٨ سبتمبر وزميله بيقراها ٩ أغسطس — والاتنين
  //    شايفين نفس الشاشة.
  // ⚠️ نصّ الحقل من NuhFmt.date نفسها اللي بترسم الجداول — مش صيغة تانية.
  //    وإلا الحقل يكتب 05/03/1448 والعمود اللي تحته يكتب حاجة تانية.
  function display(d) { return d ? NuhFmt.date(d) : ''; }

  // ---------------- البناء ----------------
  function build(input) {
    if (input.__nuhDate) return input.__nuhDate;
    if (input.getAttribute('data-nuh-date') === 'off') return null;

    var wrap = document.createElement('div');
    wrap.className = 'ndate';

    // ⚠️ ستايل العنصر الأصلي لازم ينتقل للغلاف — نفس علّة select-field.js:
    //    العنصر المخفي كان بياخد العرض والزرّ الظاهر يطلع بعرض تاني.
    var inline = input.getAttribute('style');
    if (inline) { wrap.setAttribute('style', inline); input.removeAttribute('style'); }

    var btn = document.createElement('button');
    btn.type = 'button';                       // ⚠️ من غيره الزرّ بيبعت النموذج
    btn.className = 'ndate-btn';
    btn.innerHTML =
      '<svg class="ndate-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" ' +
      'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
      '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/></svg>' +
      '<span class="ndate-txt"></span>' +
      '<span class="ndate-clear" role="button" tabindex="-1" aria-hidden="true">&times;</span>';

    input.classList.add('ndate-native');
    input.parentNode.insertBefore(wrap, input);
    wrap.appendChild(btn);
    wrap.appendChild(input);

    var panel = null;
    var view = null;                            // الشهر المعروض
    // ⚠️ مستوى العرض: يوم / شهر / سنة. الوصول لشهر في ٢٠٢٣ كان بيتطلب أكتر
    //    من ثلاثين ضغطة على السهم - شهرًا شهرًا. بثلاث مستويات بقى ثلاث
    //    ضغطات: العنوان يفتح السنين، السنة تفتح الشهور، الشهر يرجّع الأيام.
    //    ⚠️ وبيرجع لـ 'd' مع كل فتحة للتقويم: المستخدم اللي قفل وهو في
    //       شبكة السنين مش عايز يلاقيها لما يفتح تاني.
    var level = 'd';

    function label() {
      var d = parseIso(input.value);
      var txt = btn.querySelector('.ndate-txt');
      var ph = input.getAttribute('placeholder') || input.getAttribute('title') ||
               tr('date_placeholder', 'اختر التاريخ', 'Pick a date');
      txt.textContent = d ? display(d) : ph;
      txt.classList.toggle('is-ph', !d);
      wrap.classList.toggle('has-value', !!d);
    }

    function commit(d) {
      input.value = d ? toIso(d) : '';
      label();
      // ⚠️ الحدث لازم يتبعت من العنصر الأصلي: كل الشاشات مربوطة بـ
      //    input.addEventListener('change') وما تعرفش حاجة عن المكوّن ده.
      input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function close() {
      if (panel) { panel.remove(); panel = null; }
      wrap.classList.remove('is-open');
      if (OPEN && OPEN.wrap === wrap) OPEN = null;
    }

    function place() {
      if (!panel) return;
      var r = btn.getBoundingClientRect();
      var h = panel.offsetHeight || 320;
      var below = window.innerHeight - r.bottom;
      panel.style.top = (below < h + 8 && r.top > h + 8 ? r.top - h - 6 : r.bottom + 6) + 'px';
      var left = Math.min(r.left, window.innerWidth - panel.offsetWidth - 8);
      panel.style.left = Math.max(8, left) + 'px';
    }

    // نطاق السنين المعروض: اثنتا عشرة سنة في الصفحة، والصفحة الحالية محسوبة
    // من سنة العرض فالسنة اللي انت فيها بتبان دايمًا.
    function yearPage(y) { return Math.floor(y / 12) * 12; }

    // السنة والشهر الجاريان في وضع العرض الحالي (ميلادي أو هجري).
    function curYM() {
      if (mode() === 'hijri') {
        var hv = hijriParts(view) || hijriParts(new Date());
        return { y: view.__hy || hv.y, m: view.__hm || hv.m };
      }
      return { y: view.getFullYear(), m: view.getMonth() + 1 };
    }

    // ---------------- رسم شبكة السنين ----------------
    function drawYears() {
      var L = lang(), c = curYM(), start = yearPage(c.y), out = '';
      for (var y = start; y < start + 12; y++) {
        out += '<button type="button" class="ndate-my' + (y === c.y ? ' is-sel' : '') +
               '" data-year="' + y + '">' + y + '</button>';
      }
      panel.innerHTML =
        '<div class="ndate-nav">' +
          '<button type="button" class="ndate-arrow" data-page="-12" aria-label="prev">' +
            '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round"><path d="M6 3l5 5-5 5"/></svg></button>' +
          '<span class="ndate-title">' + start + ' - ' + (start + 11) + '</span>' +
          '<button type="button" class="ndate-arrow" data-page="12" aria-label="next">' +
            '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round"><path d="M10 3L5 8l5 5"/></svg></button>' +
        '</div>' +
        '<div class="ndate-mygrid">' + out + '</div>' +
        '<div class="ndate-foot">' +
          '<button type="button" class="ndate-link" data-lvl="d">' +
            tr('date_back', 'رجوع', 'Back') + '</button>' +
          '<button type="button" class="ndate-link is-strong" data-act="today">' +
            tr('date_today', 'اليوم', 'Today') + '</button>' +
        '</div>';
    }

    // ---------------- رسم شبكة الشهور ----------------
    function drawMonths() {
      var L = lang(), hijri = mode() === 'hijri', c = curYM();
      var names = hijri ? (L === 'en' ? EN_MONTHS_H : AR_MONTHS_H)
                        : (L === 'en' ? EN_MONTHS_G : AR_MONTHS_G);
      var out = '';
      for (var m = 1; m <= 12; m++) {
        out += '<button type="button" class="ndate-my' + (m === c.m ? ' is-sel' : '') +
               '" data-month="' + m + '">' + escHtml(names[m - 1]) + '</button>';
      }
      panel.innerHTML =
        '<div class="ndate-nav">' +
          '<button type="button" class="ndate-arrow" data-year-step="-1" aria-label="prev">' +
            '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round"><path d="M6 3l5 5-5 5"/></svg></button>' +
          '<button type="button" class="ndate-title is-btn" data-lvl="y">' + c.y + '</button>' +
          '<button type="button" class="ndate-arrow" data-year-step="1" aria-label="next">' +
            '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round"><path d="M10 3L5 8l5 5"/></svg></button>' +
        '</div>' +
        '<div class="ndate-mygrid">' + out + '</div>' +
        '<div class="ndate-foot">' +
          '<button type="button" class="ndate-link" data-lvl="d">' +
            tr('date_back', 'رجوع', 'Back') + '</button>' +
          '<button type="button" class="ndate-link is-strong" data-act="today">' +
            tr('date_today', 'اليوم', 'Today') + '</button>' +
        '</div>';
    }

    // ينقل العرض إلى سنة/شهر محدَّدين في الوضع الحالي.
    function goto(y, m) {
      if (mode() === 'hijri') {
        var g = hijriToGreg(y, m, 1);
        if (g) { view = g; view.__hy = y; view.__hm = m; }
      } else {
        view = new Date(y, m - 1, 1);
      }
    }

    // ---------------- رسم الشبكة ----------------
    function draw() {
      if (level === 'y') { drawYears(); return; }
      if (level === 'm') { drawMonths(); return; }
      var L = lang();
      var hijri = mode() === 'hijri';
      var dow = L === 'en' ? EN_DOW : AR_DOW;
      var today = new Date(); today.setHours(0, 0, 0, 0);
      var sel = parseIso(input.value);

      var head, cells = [];

      if (hijri) {
        var hv = hijriParts(view) || hijriParts(today);
        var hy = view.__hy || hv.y, hm = view.__hm || hv.m;
        head = (L === 'en' ? EN_MONTHS_H : AR_MONTHS_H)[hm - 1] + ' ' + hy;
        var first = hijriToGreg(hy, hm, 1);
        var len = hijriMonthLength(hy, hm);
        for (var i = 0; i < first.getDay(); i++) cells.push(null);
        for (var d = 1; d <= len; d++) {
          var g = new Date(first.getFullYear(), first.getMonth(), first.getDate() + (d - 1));
          cells.push({ n: d, g: g });
        }
      } else {
        head = (L === 'en' ? EN_MONTHS_G : AR_MONTHS_G)[view.getMonth()] + ' ' + view.getFullYear();
        var f = new Date(view.getFullYear(), view.getMonth(), 1);
        var last = new Date(view.getFullYear(), view.getMonth() + 1, 0).getDate();
        for (var j = 0; j < f.getDay(); j++) cells.push(null);
        for (var k = 1; k <= last; k++) cells.push({ n: k, g: new Date(view.getFullYear(), view.getMonth(), k) });
      }

      var same = function (a, b) { return a && b && a.getTime() === b.getTime(); };

      panel.innerHTML =
        (HIJRI_OK ? (
          // ⚠️ مفتاح واحد وشبكة واحدة: الشبكتين جنب بعض بتخلّي المستخدم يقارن
          //    بدل ما يختار، والاختيار هو المطلوب هنا.
          '<div class="ndate-modes">' +
            '<button type="button" class="ndate-mode' + (hijri ? '' : ' on') + '" data-mode="greg">' +
              tr('date_gregorian', 'ميلادي', 'Gregorian') + '</button>' +
            '<button type="button" class="ndate-mode' + (hijri ? ' on' : '') + '" data-mode="hijri">' +
              tr('date_hijri', 'هجري', 'Hijri') + '</button>' +
          '</div>') : '') +
        '<div class="ndate-nav">' +
          '<button type="button" class="ndate-arrow" data-nav="-1" aria-label="prev">' +
            '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round"><path d="M6 3l5 5-5 5"/></svg></button>' +
          '<button type="button" class="ndate-title is-btn" data-lvl="m">' + escHtml(head) + '</button>' +
          '<button type="button" class="ndate-arrow" data-nav="1" aria-label="next">' +
            '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round"><path d="M10 3L5 8l5 5"/></svg></button>' +
        '</div>' +
        '<div class="ndate-dow">' + dow.map(function (x) { return '<span>' + escHtml(x) + '</span>'; }).join('') + '</div>' +
        '<div class="ndate-grid">' + cells.map(function (c) {
          if (!c) return '<span class="ndate-cell is-blank"></span>';
          var cls = 'ndate-cell';
          if (same(c.g, today)) cls += ' is-today';
          if (same(c.g, sel)) cls += ' is-sel';
          return '<button type="button" class="' + cls + '" data-iso="' + toIso(c.g) + '">' + c.n + '</button>';
        }).join('') + '</div>' +
        '<div class="ndate-foot">' +
          '<button type="button" class="ndate-link" data-act="clear">' + tr('date_clear', 'مسح', 'Clear') + '</button>' +
          '<button type="button" class="ndate-link is-strong" data-act="today">' + tr('date_today', 'اليوم', 'Today') + '</button>' +
        '</div>';

      if (hijri) { view.__hy = hy; view.__hm = hm; }
    }

    function nav(step) {
      if (mode() === 'hijri') {
        var hy = view.__hy, hm = view.__hm + step;
        if (hm < 1) { hm = 12; hy--; } else if (hm > 12) { hm = 1; hy++; }
        var g = hijriToGreg(hy, hm, 1);
        if (g) { view = g; view.__hy = hy; view.__hm = hm; }
      } else {
        view = new Date(view.getFullYear(), view.getMonth() + step, 1);
      }
      draw();
    }

    function open() {
      if (OPEN) OPEN.close();

      // ⚠️ التقويم بيفتح دايمًا على شهر **النهاردة**، مش على شهر القيمة المختارة.
      //    كان بيفتح على المختار (وده السلوك الشائع)، لكنه غلط في النظام ده
      //    تحديدًا: كل حقول التاريخ عندنا فلاتر مدى (من/إلى) وأزرار المدد
      //    الجاهزة بتحطّ فيها قيم قديمة — «الشهر الماضي» بتحطّ ١٤/٠٧. فالمستخدم
      //    بيفتح التقويم عشان يختار من جديد ويلاقي نفسه في يوليو، ولازم يرجّع
      //    شهر بشهر لحد أغسطس. النقطة المرجعية اللي في دماغه هي «النهاردة».
      //    واليوم المختار بيفضل مميّز (is-sel) فما بيضيعش لما يتنقّل.
      level = 'd';
      var todayD = new Date();
      view = new Date(todayD.getFullYear(), todayD.getMonth(), 1);
      if (mode() === 'hijri') {
        // نفس القاعدة في الهجري: أول الشهر الهجري اللي النهاردة فيه
        var h = hijriParts(todayD);
        if (h) { var g = hijriToGreg(h.y, h.m, 1); if (g) { view = g; view.__hy = h.y; view.__hm = h.m; } }
      }

      panel = document.createElement('div');
      panel.className = 'ndate-panel';
      document.body.appendChild(panel);
      draw();
      place();
      wrap.classList.add('is-open');
      OPEN = { wrap: wrap, panel: panel, btn: btn, close: close, place: place };

      panel.addEventListener('click', function (e) {
        var m = e.target.closest('[data-mode]');
        if (m) {
          setMode(m.getAttribute('data-mode'));
          // ⚠️ إعادة التثبيت على شهر النهاردة في التقويم الجديد لا ترجمة الشهر
          //    المعروض: أول أغسطس الميلادي بيقع في شهر هجري، و«شهر النهاردة»
          //    الهجري ممكن يكون غيره — فالمستخدم كان يبدّل الوضع ويلاقي نفسه في
          //    شهر ما اختارهوش ومش فيه النهاردة. نفس قاعدة الفتح بالظبط.
          var td = new Date();
          view = new Date(td.getFullYear(), td.getMonth(), 1);
          if (mode() === 'hijri') {
            var hh = hijriParts(td);
            if (hh) { var gg = hijriToGreg(hh.y, hh.m, 1); if (gg) { view = gg; view.__hy = hh.y; view.__hm = hh.m; } }
          }
          label(); draw(); place();
          return;
        }

        var nv = e.target.closest('[data-nav]');
        if (nv) { nav(parseInt(nv.getAttribute('data-nav'), 10)); place(); return; }

        // تنقّل بين المستويات: العنوان يطلع لفوق، و«رجوع» ينزل للأيام.
        var lv = e.target.closest('[data-lvl]');
        if (lv) { level = lv.getAttribute('data-lvl'); draw(); place(); return; }

        var pg = e.target.closest('[data-page]');
        if (pg) {
          var cy = curYM();
          goto(cy.y + parseInt(pg.getAttribute('data-page'), 10), cy.m);
          draw(); place(); return;
        }

        var ys = e.target.closest('[data-year-step]');
        if (ys) {
          var cy2 = curYM();
          goto(cy2.y + parseInt(ys.getAttribute('data-year-step'), 10), cy2.m);
          draw(); place(); return;
        }

        var yr = e.target.closest('[data-year]');
        if (yr) {
          goto(parseInt(yr.getAttribute('data-year'), 10), curYM().m);
          level = 'm'; draw(); place(); return;
        }

        var mo = e.target.closest('[data-month]');
        if (mo) {
          goto(curYM().y, parseInt(mo.getAttribute('data-month'), 10));
          level = 'd'; draw(); place(); return;
        }

        var act = e.target.closest('[data-act]');
        if (act) {
          if (act.getAttribute('data-act') === 'clear') commit(null);
          else { var td = new Date(); commit(new Date(td.getFullYear(), td.getMonth(), td.getDate())); }
          close();
          return;
        }

        var cell = e.target.closest('[data-iso]');
        if (cell) { commit(parseIso(cell.getAttribute('data-iso'))); close(); }
      });
    }

    btn.addEventListener('click', function (e) {
      // زرّ المسح جوّه الزرّ نفسه — ما يفتحش اللوحة
      if (e.target.closest('.ndate-clear')) { e.stopPropagation(); commit(null); close(); return; }
      if (panel) close(); else open();
    });

    btn.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && panel) { close(); }
      else if ((e.key === 'Enter' || e.key === ' ') && !panel) { e.preventDefault(); open(); }
    });

    // ⚠️ الكود اللي بيغيّر القيمة برمجيًا (زي أزرار «آخر ٧ أيام») بيطلق change،
    //    فالنصّ لازم يتحدّث معاه وإلا الزرّ يفضل مكتوب فيه القديم.
    input.addEventListener('change', label);

    label();
    input.__nuhDate = { refresh: label, close: close };
    return input.__nuhDate;
  }

  function enhance(root) {
    var scope = root || document;
    var list = scope.querySelectorAll ? scope.querySelectorAll('input[type=date]') : [];
    for (var i = 0; i < list.length; i++) {
      try { build(list[i]); } catch (e) { /* حقل واحد ما يكسرش الصفحة */ }
    }
  }

  document.addEventListener('mousedown', function (e) {
    if (!OPEN) return;
    if (OPEN.panel.contains(e.target) || OPEN.btn.contains(e.target)) return;
    OPEN.close();
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && OPEN) OPEN.close();
  });

  // ⚠️ تبديل التقويم لازم يعيد كتابة نصّ كل حقول التاريخ المرسومة خلاص، مش
  //    الحقل اللي اتبدّل منه بس. من غير ده الشاشة تطلع بحقلين: واحد هجري
  //    وواحد ميلادي — وهي أوضح صورة على إن التبديل نصّ شغّال.
  window.addEventListener('nuh:calendar-mode', function () {
    document.querySelectorAll('input[type=date]').forEach(function (el) {
      if (el.__nuhDate) el.__nuhDate.refresh();
    });
  });
  window.addEventListener('resize', function () { if (OPEN) OPEN.place(); });
  window.addEventListener('scroll', function (e) {
    if (!OPEN) return;
    if (OPEN.panel.contains(e.target)) return;
    OPEN.place();
  }, true);

  if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', function () { enhance(document); });
  else enhance(document);

  return { enhance: enhance, refresh: function (el) { var s = el && el.__nuhDate; if (s) s.refresh(); } };
})();
