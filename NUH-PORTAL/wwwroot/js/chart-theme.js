// ==========================================================================
//  chart-theme.js — الشكل الموحّد لكل الرسوم البيانية في النظام.
//
//  ⚠️ ليه الملف ده موجود:
//     دالة renderChart كانت مكتوبة **مرتين** — نسخة في js/reports-page.js
//     ونسخة تانية جوّه Views/Home/Index.cshtml — والنسختين متطابقتين
//     ومحدودتين: بلا وسيلة إيضاح، بلا تلميح منسّق، وبخط المتصفح الافتراضي
//     مش خط النظام.
//
//     وكان فيها عيب ظاهر للعين: options.scales اتحطّت على كل الأنواع، وde
//     يشمل الدونات — فرسمة الدونات كانت بتطلع وجنبها محور رأسي فيه 0 و1
//     بلا أي معنى (الدونات مالهاش محاور أصلًا).
//
//  ملاحظة على «ثلاثي الأبعاد»:
//     الرسوم المجسّمة بتشوّه القراءة فعليًا — الشريحة القدّام تبان أكبر من
//     شريحة ورّاها بنفس القيمة، والزاوية بتغيّر إحساس المستخدم بالنِسَب.
//     الشكل الاحترافي في لوحات المعلومات الحديثة مسطّح، والعمق بييجي من
//     التدرّج اللوني والظل والمسافات والخط — وده اللي معمول هنا.
// ==========================================================================
var NuhChart = (function () {

  var FONT = "'IBM Plex Sans Arabic', sans-serif";

  // ⚠️ الرسم بيتحطّ على <canvas>، والـ canvas **مابيفهمش** var(--navy):
  //    دي قيمة CSS بتتحلّ وقت رسم الصفحة، والكانفاس بيرسم بالبكسل. فأي لون
  //    هنا لازم يكون نصّ لون حقيقي.
  //
  //    فبدل ما نكتب الألوان بالإيد (وكانت مكتوبة كده فعلًا — نسخة تانية من
  //    هوية الجامعة جوّه ملف رسوم)، بنقراها من نفس التوكنز اللي في
  //    css/base.css وقت التحميل، والقيمة المكتوبة جنبها احتياطي لو التوكن
  //    مالقيناش. يعني لو الأخضر اتغيّر في base.css، الرسوم بتتغيّر معاه.
  function tok(name, fallback) {
    try {
      var v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      return v || fallback;
    } catch (e) { return fallback; }
  }

  var C = {
    navy:      tok('--navy',       '#166a45'),
    navyDark:  tok('--navy-dark',  '#104631'),
    navyLight: tok('--navy-light', '#25935f'),
    gold:      tok('--gold',       '#dba102'),
    goldLight: tok('--gold-light', '#f0c33c'),
    green:     tok('--green',      '#067647'),
    red:       tok('--red',        '#b42318'),
    purple:    tok('--purple',     '#80519f'),
    blue:      tok('--blue',       '#175cd3'),
    gray200:   tok('--gray-200',   '#dcdfe4'),
    gray500:   tok('--gray-500',   '#85888e'),
    gray700:   tok('--gray-700',   '#333741')
  };

  // ==========================================================================
  //  لوحة ألوان الشرائح — بترتيب ثابت عشان نفس التصنيف ياخد نفس اللون كل مرة.
  //
  //  ⚠️ الترتيب اتغيّر بعد ما اتقاس بمقياس فرق اللون (ΔE): الأحمر #b42318
  //     والبنّي #b54708 كانوا **جنب بعض** وفرقهم ٦٫١ - يعني أي رسمة فيها ستّ
  //     تصنيفات أو أكتر، السادس والسابع بيطلعوا بلون واحد عمليًا حتى لواحد
  //     شايف الألوان عادي. والترتيب الجديد أسوأ جارين فيه ٨٫٤ (عمى ألوان)
  //     و٢٣٫٩ (رؤية عادية).
  //
  //  ⚠️ ولا لون اتغيّر - الترتيب بس. الألوان هي هي من base.css.
  //
  //  ⚠️ والذهبي تباينه على الأبيض ٢٫٢٥:١، وده مقبول للشريحة **بشرط** إن
  //     اسمها ورقمها مكتوبين جنبها (وسيلة الإيضاح موجودة في كل رسمة).
  //     لو اتشالت وسيلة الإيضاح يومًا، الذهبي لازم يتغيّر.
  // ==========================================================================
  var PALETTE = [C.navy, C.gold, C.purple, C.green, C.blue, '#b54708', C.navyLight, C.red];

  function ready() { return typeof Chart !== 'undefined'; }

  function applyDefaults() {
    if (!ready() || Chart.__nuhThemed) return;
    Chart.defaults.font.family = FONT;
    Chart.defaults.font.size = 11.5;
    // ⚠️ ‏gray700 لا gray500: أسماء المحاور (مبنى 40، التواريخ) كانت رمادية
    //    فاتحة بحجم صغير على أبيض - تباينها تحت الحدّ المقروء، وبيقلّ أكتر لو
    //    ورا الرسمة خلفية باهتة. اللون ده هو نفسه لون النصّ الثانوي في باقي
    //    الواجهة، فالرسم بقى بنفس مقروئية اللي حواليه.
    Chart.defaults.color = C.gray700;
    Chart.defaults.plugins.tooltip.backgroundColor = '#104631';
    Chart.defaults.plugins.tooltip.titleFont = { family: FONT, size: 12, weight: '700' };
    Chart.defaults.plugins.tooltip.bodyFont = { family: FONT, size: 12 };
    Chart.defaults.plugins.tooltip.padding = 10;
    Chart.defaults.plugins.tooltip.cornerRadius = 8;
    Chart.defaults.plugins.tooltip.displayColors = false;
    Chart.__nuhThemed = true;
  }

  // تدرّج رأسي: العمود/المساحة تبقى غامقة من فوق وفاتحة من تحت — ده اللي
  // بيدّي إحساس العمق من غير تجسيم بيغلط في القراءة.
  function gradient(ctx, area, from, to) {
    if (!area) return from;
    var g = ctx.createLinearGradient(0, area.top, 0, area.bottom);
    g.addColorStop(0, from);
    g.addColorStop(1, to);
    return g;
  }

  function hexA(hex, a) {
    var n = parseInt(hex.slice(1), 16);
    return 'rgba(' + ((n >> 16) & 255) + ',' + ((n >> 8) & 255) + ',' + (n & 255) + ',' + a + ')';
  }

  // ⚠️ الرقم في نُص الدونات: المستخدم بيبص على الرسمة عشان يعرف الإجمالي،
  //    وبدونه بيضطر يجمع الشرائح بنفسه.
  var centerTotal = {
    id: 'nuhCenterTotal',
    afterDraw: function (chart) {
      if (chart.config.type !== 'doughnut') return;
      var ds = chart.data.datasets[0];
      if (!ds) return;
      var m = chart.getDatasetMeta(0);
      if (!m || !m.data || !m.data[0]) return;

      // ⚠️ الوسط بيعرض المجموع افتراضيًا، ويقبل قيمة مخصّصة (نسبة مثلًا).
      //    من غير ده كانت شاشة الرئيسية مضطرة تكتب إضافة خاصة بيها لرسم
      //    النسبة في الوسط - نسخة تانية من نفس الكود بفرق سطر واحد.
      var ctr = chart.options.__center;
      var value = ctr && ctr.value != null
        ? String(ctr.value)
        : String((ds.data || []).reduce(function (a, b) { return a + (Number(b) || 0); }, 0));
      var label = (ctr && ctr.label != null) ? ctr.label : (chart.options.__totalLabel || '');

      // ⚠️ الرقم بيعدّ مع امتلاء الدائرة لا بيقف مكتوبًا من أول إطار: دائرة
      //    بتتملّي جنب رقم ثابت بتبان كأن الرقم مش بتاعها.
      //    والتقدّم بيتقرا من الأقواس نفسها (مجموع circumference الحالي على
      //    دورة كاملة) لا من مؤقّت تاني - فمهما اتغيّرت مدّة الحركة أو
      //    منحناها في Chart.js، الرقم بيمشي معاها بالظبط.
      var prog = 1;
      try {
        var span = 0;
        for (var ai = 0; ai < m.data.length; ai++) span += Math.abs(m.data[ai].circumference || 0);
        prog = Math.max(0, Math.min(1, span / (Math.PI * 2)));
      } catch (e) { }
      if (prog < 1) {
        // الصيغة «77%» أو «31» - بنعدّ الجزء الرقمي وبنسيب اللاحقة زي ما هي.
        value = value.replace(/^(\d+)/, function (_, d) { return String(Math.round(Number(d) * prog)); });
      }

      var x = m.data[0].x, y = m.data[0].y;
      var g = chart.ctx;
      g.save();
      g.textAlign = 'center'; g.textBaseline = 'middle';
      // ⚠️ نفس درجة .stat-num بالظبط (--navy): الرقم ده بيقف جنب أرقام
      //    البطاقات في نفس الشاشة، فلو فضل --navy-dark هيبان أغمق منها.
      g.fillStyle = C.navy || '#166a45';
      g.font = '700 20px ' + FONT;
      g.fillText(value, x, y - 2);
      g.font = '500 10px ' + FONT;
      g.fillStyle = C.gray500;
      g.fillText(label, x, y + 15);
      g.restore();
    }
  };

  function optionsFor(type, totalLabel, opts) {
    var base = {
      responsive: true,
      maintainAspectRatio: false,
      animation: { duration: 550, easing: 'easeOutQuart' },
      // ⚠️ الاتجاه بيتقري وقت الرسم مش مرة واحدة عند التحميل — الصفحة
      //    بتتبدّل بين عربي وإنجليزي والرسوم بتتعاد رسمها معاها.
      locale: (document.documentElement.getAttribute('lang') === 'en' ? 'en' : 'ar-EG'),
      plugins: { legend: { display: false } }
    };

    if (type === 'doughnut') {
      // ⚠️ بلا scales عن قصد — الدونات مالهاش محاور، والنسخة القديمة كانت
      //    بتحط محور رأسي فيه 0 و1 جنب الرسمة.
      // ⚠️ الدونة ليها مدّتها: ٥٥٠ مللي ثانية كانت بتخلّيها تظهر شبه جاهزة،
      //    والمطلوب إنها «تتملّي» زي عدّاد البطاقات. والمنحنى هو نفسه بتاع
      //    js/count-up.js (يبدأ سريع ويهدى بقوّة في الآخر) عشان الحركتين
      //    اللي على الشاشة الواحدة يبانوا حاجة واحدة.
      //  ⚠️ و animateScale مقفولة: تكبير الدائرة مع دورانها حركتان في وقت
      //     واحد، والعين بتتوه بينهم. الدوران وحده هو اللي بيقول «بتتملّي».
      base.animation = { duration: 1800, easing: 'easeOutQuint',
                         animateRotate: true, animateScale: false };
      base.cutout = (opts && opts.cutout) || '68%';
      base.__totalLabel = totalLabel || '';
      base.__center = (opts && opts.center) || null;
      // ⚠️ في شاشات بتبني وسيلة إيضاح بالـ HTML تحت الرسمة (عشان تعرض العدد
      //    جنب الاسم، وده الكانفاس مابيعملهوش). من غير الخيار ده كانت
      //    هتطلع وسيلتان: واحدة مرسومة وواحدة مكتوبة.
      base.plugins.legend = {
        display: !(opts && opts.legend === false), position: 'bottom',
        labels: {
          usePointStyle: true, pointStyle: 'circle', boxWidth: 8, boxHeight: 8,
          padding: 12, font: { family: FONT, size: 11.5 }, color: C.gray700
        }
      };
      base.plugins.tooltip = {
        rtl: (document.documentElement.getAttribute('dir') !== 'ltr'),
        callbacks: {
          label: function (ctx) {
            var arr = ctx.dataset.data || [];
            var sum = arr.reduce(function (a, b) { return a + (Number(b) || 0); }, 0);
            var v = Number(ctx.raw) || 0;
            var pct = sum ? Math.round(v * 100 / sum) : 0;
            return ' ' + ctx.label + ': ' + v + ' (' + pct + '%)';
          }
        }
      };
      return base;
    }

    var o = opts || {};

    // ======================================================================
    //  أعمدة أفقية.
    //
    //  ⚠️ لما تكون التسميات نصًّا عربيًا (أسماء مبانٍ مثلًا) الأعمدة الرأسية
    //     بتقلب الاسم أو تقصّه تحت العمود. الأفقي بيدّي الاسم سطرًا كاملًا.
    //  ⚠️ وكان مكتوبًا بالإيد في شاشة الرئيسية بمحاور وإضافة أرقام خاصة بيها -
    //     نسخة تالتة من نفس الرسمة. بقى خيارًا في المكوّن.
    // ======================================================================
    if (o.horizontal) {
      base.indexAxis = 'y';
      base.layout = { padding: { left: 26, right: 26 } };
      base.scales = {
        x: { display: false, beginAtZero: true, grace: '16%' },
        y: { border: { display: false }, grid: { display: false },
             // ⚠️ اسم المبنى تسمية بيانات لا زخرفة: بيتقرا مع الرقم اللي جنبه،
             //    فوزنه أتقل شوية ولونه أغمق من التسميات العادية.
             ticks: { color: C.gray700, font: { family: FONT, size: 12.5, weight: '600' } } }
      };
      base.plugins.tooltip = {
        rtl: (document.documentElement.getAttribute('dir') !== 'ltr'),
        callbacks: { label: function (ctx) { return ' ' + ctx.formattedValue; } }
      };
      return base;
    }

    // مساحة فوق أعلى عمود عشان الرقم اللي فوقه ما يتقصّش عند حافة الرسمة
    if (o.valueLabels) base.layout = { padding: { top: 18 } };

    base.scales = {
      y: o.hideY ? { display: false, beginAtZero: true, grace: '18%' } : {
        beginAtZero: true,
        border: { display: false },
        // شبكة أفقية خفيفة ومتقطّعة: بتساعد على قراءة القيمة من غير ما تزاحم البيانات
        grid: { color: 'rgba(136,145,168,.18)', drawTicks: false, borderDash: [4, 4] },
        ticks: { padding: 8, precision: 0, maxTicksLimit: 6 }
      },
      x: {
        border: { display: false },
        // ⚠️ الخطوط الرأسية اتشالت: مالهاش لازمة في سلسلة زمنية وبتعمل «قفص» حوالين الرسمة
        grid: { display: false },
        ticks: { padding: 6, maxRotation: 0, autoSkip: true, maxTicksLimit: 10 }
      }
    };
    base.plugins.tooltip = {
      rtl: (document.documentElement.getAttribute('dir') !== 'ltr'),
      callbacks: { label: function (ctx) { return ' ' + ctx.formattedValue; } }
    };
    return base;
  }

  function datasetFor(type, data, color, opts) {
    var c = color || C.navy;
    var o = opts || {};

    if (type === 'doughnut') {
      return {
        data: data,
        backgroundColor: Array.isArray(color) ? color : PALETTE,
        borderColor: '#fff',
        borderWidth: 2,
        // مسافة بين الشرائح: بتفصلها بصريًا أوضح من الحدّ لوحده
        spacing: 2,
        hoverOffset: 6,
        borderRadius: 4
      };
    }

    if (type === 'line') {
      return {
        data: data,
        borderColor: c,
        borderWidth: 2.5,
        tension: 0.38,
        fill: true,
        backgroundColor: function (x) { return gradient(x.chart.ctx, x.chart.chartArea, hexA(c, .28), hexA(c, 0)); },
        pointRadius: 0,
        pointHoverRadius: 5,
        pointBackgroundColor: '#fff',
        pointHoverBorderColor: c,
        pointHoverBorderWidth: 2.5,
        // ⚠️ النقط مخفية لحد ما تقف عليها: ٣٠ نقطة ظاهرة كانت بتغطّي الخط نفسه
        pointHitRadius: 12
      };
    }

    // bar
    // ⚠️ التدرّج للأعمدة الرأسية بس. في الأفقي التدرّج بيمشي من فوق لتحت
    //    **عبر الرسمة كلها**، فالعمود الأول بيطلع غامق والأخير فاتح - يعني
    //    الدرجة بتتغيّر حسب **ترتيب الصفّ** وتتقري كأنها بتعني حاجة، وهي
    //    مابتعنيش. اللون الصلب هو الصحيح هنا.
    return {
      data: data,
      backgroundColor: o.horizontal
        ? c
        : function (x) { return gradient(x.chart.ctx, x.chart.chartArea, c, hexA(c, .55)); },
      hoverBackgroundColor: c,
      borderRadius: 6,
      borderSkipped: false,
      maxBarThickness: 34,
      categoryPercentage: 0.72,
      barPercentage: 0.86
    };
  }

  var instances = {};

  /* render(id, type, labels, data, color)
     color: نص واحد للأعمدة والخطوط، أو مصفوفة ألوان للدونات (اختياري).
     totalLabel: نص صغير تحت الإجمالي في نُص الدونات (اختياري). */
  // ==========================================================================
  //  الأرقام فوق الأعمدة.
  //  ⚠️ كان مكتوب جوّه Views/Home/Index.cshtml — ومعاه رسمة Chart.js كاملة
  //     بألوانها ومحاورها وخطّها، مع إن نفس الملف مكتوب في تعليقه إن دالة
  //     الرسم اتوحّدت هنا. فكانت رسمة لوحة التحكم الوحيدة اللي بتتعدّى على
  //     NuhChart، ولونها كان أخضر فاتح تاني مالوش علاقة بباقي رسوم النظام.
  // ==========================================================================
  var valueLabels = {
    id: 'nuhValueLabels',
    afterDatasetsDraw: function (chart) {
      // ⚠️ العلامة بتتقرا من options مش من خاصية على الكائن: Chart.js بيرسم
      //    أول رسمة **جوّه** المُنشئ، يعني قبل ما أي سطر بعد new Chart(...)
      //    يشتغل. فلو العلامة اتحطّت بعده، أول رسم بيعدّي بلا أرقام وما
      //    بتظهرش غير مع أول تغيير حجم.
      var on = chart.options && chart.options.plugins && chart.options.plugins.nuhValueLabels;
      if (chart.config.type !== 'bar' || !on) return;
      var g = chart.ctx, m = chart.getDatasetMeta(0);
      // ⚠️ الأفقي بيكتب الرقم **جنب** طرف العمود لا فوقه - فوقه كان هيقع
      //    على العمود اللي تحته.
      var horiz = chart.options && chart.options.indexAxis === 'y';
      g.save();
      g.textAlign = horiz ? 'left' : 'center';
      g.textBaseline = horiz ? 'middle' : 'bottom';
      g.font = "700 12px " + FONT;
      g.fillStyle = C.gray700;
      (m.data || []).forEach(function (bar, i) {
        var v = chart.data.datasets[0].data[i];
        if (v == null) return;
        if (horiz) g.fillText(String(v), bar.x + 8, bar.y);
        else g.fillText(String(v), bar.x, bar.y - 6);
      });
      g.restore();
    }
  };

  // opts (اختيارية):
  //   valueLabels → الرقم مكتوب فوق كل عمود (أو جنبه في الأفقي)
  //   hideY       → إخفاء المحور الرأسي (مع الأرقام فوق الأعمدة المحور بيكرّر
  //                 نفس المعلومة مرتين)
  //   horizontal  → أعمدة أفقية (للتسميات النصّية الطويلة)
  //   legend:false→ إطفاء وسيلة إيضاح الدونات المرسومة (لما الشاشة بتبنيها HTML)
  //   center      → { value, label } نصّ وسط الدونات بدل المجموع
  //   cutout      → سُمك حلقة الدونات
  function render(id, type, labels, data, color, totalLabel, opts) {
    if (!ready()) return null;
    applyDefaults();
    var el = document.getElementById(id);
    if (!el) return null;
    if (instances[id]) instances[id].destroy();
    var o = opts || {};
    var options = optionsFor(type, totalLabel, o);
    options.plugins = options.plugins || {};
    options.plugins.nuhValueLabels = !!o.valueLabels;

    var chart = new Chart(el.getContext('2d'), {
      type: type,
      data: { labels: labels, datasets: [datasetFor(type, data, color, o)] },
      options: options,
      plugins: [centerTotal, valueLabels]
    });
    instances[id] = chart;
    return chart;
  }

  function destroyAll() {
    Object.keys(instances).forEach(function (k) { instances[k].destroy(); });
    instances = {};
  }

  return { render: render, destroyAll: destroyAll, palette: PALETTE, colors: C };
})();
