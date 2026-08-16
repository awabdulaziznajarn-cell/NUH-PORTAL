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
  var C = {
    navy: '#166a45', navyDark: '#104631', navyLight: '#25935f', gold: '#dba102', goldLight: '#f0c33c',
    green: '#067647', red: '#b42318', gray200: '#dcdfe4', gray500: '#85888e', gray700: '#333741'
  };

  // لوحة ألوان الشرائح — بترتيب ثابت عشان نفس التصنيف ياخد نفس اللون كل مرة
  var PALETTE = [C.navy, C.gold, C.green, '#80519f', '#175cd3', C.red, '#b54708', C.navyLight];

  function ready() { return typeof Chart !== 'undefined'; }

  function applyDefaults() {
    if (!ready() || Chart.__nuhThemed) return;
    Chart.defaults.font.family = FONT;
    Chart.defaults.font.size = 11.5;
    Chart.defaults.color = C.gray500;
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
      var total = (ds.data || []).reduce(function (a, b) { return a + (Number(b) || 0); }, 0);
      var m = chart.getDatasetMeta(0);
      if (!m || !m.data || !m.data[0]) return;
      var x = m.data[0].x, y = m.data[0].y;
      var g = chart.ctx;
      g.save();
      g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillStyle = C.navyDark || '#104631';
      g.font = '700 20px ' + FONT;
      g.fillText(String(total), x, y - 2);
      g.font = '500 10px ' + FONT;
      g.fillStyle = C.gray500;
      g.fillText(chart.options.__totalLabel || '', x, y + 15);
      g.restore();
    }
  };

  function optionsFor(type, totalLabel) {
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
      base.cutout = '68%';
      base.__totalLabel = totalLabel || '';
      base.plugins.legend = {
        display: true, position: 'bottom',
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

    base.scales = {
      y: {
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

  function datasetFor(type, data, color) {
    var c = color || C.navy;

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
    return {
      data: data,
      backgroundColor: function (x) { return gradient(x.chart.ctx, x.chart.chartArea, c, hexA(c, .55)); },
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
  function render(id, type, labels, data, color, totalLabel) {
    if (!ready()) return null;
    applyDefaults();
    var el = document.getElementById(id);
    if (!el) return null;
    if (instances[id]) instances[id].destroy();
    instances[id] = new Chart(el.getContext('2d'), {
      type: type,
      data: { labels: labels, datasets: [datasetFor(type, data, color)] },
      options: optionsFor(type, totalLabel),
      plugins: [centerTotal]
    });
    return instances[id];
  }

  function destroyAll() {
    Object.keys(instances).forEach(function (k) { instances[k].destroy(); });
    instances = {};
  }

  return { render: render, destroyAll: destroyAll, palette: PALETTE, colors: C };
})();
