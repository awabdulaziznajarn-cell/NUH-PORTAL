// ==========================================================================
//  table-state.js — حالات جدول الشاشة: «جاري التحميل» و«لا توجد نتائج» و«خطأ».
//
//  ⚠️ ليه الملف ده موجود:
//     كل شاشة إدارية بترسم جدولها بنفسها، وبتحتاج تفرّق بين حالتين مختلفتين
//     تمامًا يظهران بنفس الشكل لو ما فرّقناش بينهما:
//
//        • «لسه ما حمّلناش»  → المفروض: مؤشّر تحميل.
//        • «حمّلنا ومفيش نتائج» → المفروض: «لا توجد ...».
//
//     الشاشات كانت بترسم الرسالة الفارغة من غير ما تسأل السؤال ده. والسبب
//     متكرّر في كلها: setLang في أول التهيئة بتنادي onLanguageChange، وهي
//     مربوطة بـ renderTable — فالجدول بيترسم من مصفوفة فاضية *قبل* ما ينطلق
//     أول نداء أصلًا. المستخدم يقرأ «لا توجد أدوار» بالخط العريض، وبعد جزء من
//     الثانية تظهر البيانات. أي بطء عادي في الشبكة بيبقى شكله عطل في النظام.
//
//     اتصلّح قبل كده في ثلاث شاشات بثلاث علامات مختلفة مكتوبة بالإيد
//     (usersLoaded، firstLoadDone مرتين) — وشاشة الأدوار فضلت من غير علاج
//     لأن العلاج مكانش في مكان واحد يشملها. فالقاعدة هنا: العلامة والرسم
//     الثلاثة بيعيشوا في ملف واحد، وأي شاشة جديدة بتاخدهم جاهزين.
//
//  الاستخدام:
//     var tbl = NuhTable.bind('rolBody', 6);       // مرة واحدة عند التهيئة
//
//     function renderTable() {
//       if (!tbl.isDone()) return tbl.loading();   // لسه بنحمّل
//       ... بناء الصفوف ...
//       tbl.rows(body, t('rol_noItems'));
//     }
//
//     async function loadRoles() {
//       try { ... } finally { tbl.done(); renderTable(); }
//     }
// ==========================================================================
var NuhTable = (function () {

  function cell(colspan, inner, pad, color, minHeight) {
    return '<tr><td colspan="' + colspan + '" style="text-align:center;padding:' +
           (pad || '40px') + (color ? ';color:' + color : '') +
           (minHeight ? ';height:' + minHeight + 'px' : '') + '">' + inner + '</td></tr>';
  }

  function bind(tbodyId, colspan) {
    var loaded = false;

    function el() { return document.getElementById(tbodyId); }
    function put(html) { var n = el(); if (n) n.innerHTML = html; }

    return {
      // مؤشّر التحميل. الـ .spinner معرّف في ستايل كل شاشة (وفي site.css).
      //
      // ⚠️ بيحجز ارتفاع الصفوف اللي كانت معروضة قبله. من غير الحجز ده الجدول
      //    بينكمش لسطر واحد لحظة التحميل، فطول الصفحة بيقلّ، والمتصفح بيقصّ
      //    موضع التمرير على الطول الجديد - يعني الصفحة **بتقفز لفوق**. ولما
      //    الصفوف بترجع بيفضل التمرير مكانه الجديد.
      //
      //    واللي كان بيشوفه الموظف: يدوس على ترويسة عمود عشان يرتّب، فيلاقي
      //    نفسه فجأة في أول الصفحة بعيد عن الجدول - وكل ترتيب أو تنقّل بين
      //    الصفحات كان بيعمل كده في كل شاشة فيها جدول.
      loading: function () {
        var n = el();
        var keep = n ? Math.round(n.getBoundingClientRect().height) : 0;
        put(cell(colspan, '<div class="spinner"></div>', '40px', null, keep > 60 ? keep : 0));
      },

      // الصفوف، أو رسالة «لا توجد نتائج» لو مفيش. لا تُستدعى قبل done().
      rows: function (html, emptyText) {
        put(html || cell(colspan, emptyText || '-', '28px', 'var(--gray-500)'));
      },

      // ======================================================================
      //  فشل النداء — لون مختلف عن «لا توجد نتائج» عمدًا: الاتنين مش نفس
      //  الحالة، والفرق بينهم هو الفرق بين «مفيش بيانات» و«مش عارفين».
      //
      //  ⚠️ وزرّ إعادة المحاولة جوّه الرسالة لا في كل شاشة على حدة: الشاشتين
      //     الكبيرتين (الطلبات والطلاب) كانوا كاتبين صفّ الخطأ بالإيد بستايل
      //     مضمّن متكرّر حرفًا بحرف، وشاشات القوائم المرجعية الخمسة ماكانش
      //     عندها لا خطأ ولا تحميل أصلًا - بطاقة فاضية وخلاص. تالت نسخة كانت
      //     هتتكتب هنا، فاتنقلت للمكان الواحد.
      //
      //  ⚠️ والربط بـ addEventListener لا onclick في النصّ: onclick بيحتاج
      //     الدالة تكون عامّة في الصفحة، وده اللي كان مجبر الشاشات تسيب
      //     دوالها في النطاق العام عشان الزرّ ده وحده.
      // ======================================================================
      error: function (msg, onRetry, retryLabel) {
        loaded = true;
        var body = '<div class="tbl-err-msg">' + escHtml(msg || '') + '</div>' +
                   (onRetry ? '<button type="button" class="tbl-retry">' +
                              escHtml(retryLabel || '') + '</button>' : '');
        put(cell(colspan, body, '34px', '#b42318'));
        if (onRetry) {
          var n = el();
          var btn = n ? n.querySelector('.tbl-retry') : null;
          if (btn) btn.addEventListener('click', onRetry);
        }
      },

      // انتهى أول نداء (نجح أو فشل) — بعدها الرسالة الفارغة صادقة.
      done: function () { loaded = true; },
      isDone: function () { return loaded; },

      // إعادة الحالة عند تبديل تبويب/فلتر يبدأ نداءً جديدًا من الصفر.
      reset: function () { loaded = false; }
    };
  }

  // ⚠️ صفّ الترقيم كان مبنيًّا يدويًّا في كل شاشة بستايل مضمّن مختلف — وشاشة
  //    «تغيير بيانات ساكن وحدة» لم يكن فيها صفّ أصلًا، فكانت تعرض أول ٢٥ وحدة
  //    فقط من ٢٤٢ بلا أي طريقة للوصول إلى الباقي. هذه هي النسخة الوحيدة.
  //
  //    ⚠️ وكانت تلات نسخ فعليًّا: دي، وواحدة في «سجل العمليات»، وواحدة في
  //       «الطلبات» — كل واحدة بشكل مختلف لنفس الزرّين. الاتنين اتحوّلوا هنا.
  //       أي شاشة جديدة فيها ترقيم تنادي الدالة دي ولا تكتب زرًّا بنفسها.
  //
  //    labels:
  //      { show, to, of }  → ملخّص «عرض ١ إلى ٢٥ من ٢٤٢»
  //      أو { summary }    → نصّ ملخّص جاهز بنته الشاشة (لو صيغتها مختلفة)
  //      { page, pageOf }  → «صفحة ٢ من ٥»، ولو ناقصين → «٢ / ٥»
  //      { prev, next }    → نصّ الزرّين
  //      { sizePrefix, sizeSuffix } → نصّا قائمة عدد الصفوف لو متفعّلة
  //    onGo(pageNumber)    تُستدعى عند الضغط على السابق/التالي.
  //    opts: { sizes: [50,100,200], onSize: fn }  قائمة عدد الصفوف (اختيارية).
  function pager(elId, data, labels, onGo, opts) {
    var el = typeof elId === 'string' ? document.getElementById(elId) : elId;
    if (!el) return;

    var o     = opts || {};
    var size  = data.pageSize || 25;
    var page  = data.page || 1;
    var total = data.total || data.totalCount || data.totalRecords || 0;
    // ⚠️ totalPages من السيرفر أولًا: الحساب المحلي بيغلط لما الـ API بيقصّ
    //    النتائج بحدّ أقصى مختلف عن pageSize المطلوب.
    var pages = Math.max(1, data.totalPages || Math.ceil(total / size));
    var from  = total === 0 ? 0 : (page - 1) * size + 1;
    var to    = Math.min(total, page * size);
    var L     = labels || {};

    // تهريب HTML — التعريف الوحيد في /js/esc.js
    function esc(v) { return escHtml(v); }

    // سهم واحد بشكلين متعاكسين، وبيتقلب مع اتجاه الصفحة من site.css
    function chev(d) {
      return '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" ' +
             'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="' + d + '"/></svg>';
    }
    var CHEV_PREV = chev('M6 3l5 5-5 5');
    var CHEV_NEXT = chev('M10 3L5 8l5 5');

    var summary = L.summary != null
      ? esc(L.summary)
      : esc(L.show) + ' <b>' + from + '</b> ' + esc(L.to) + ' <b>' + to + '</b> ' +
        esc(L.of) + ' <b>' + total + '</b>';

    var pageText = (L.page || L.pageOf)
      ? esc(L.page) + ' ' + page + ' ' + esc(L.pageOf) + ' ' + pages
      : page + ' / ' + pages;

    var sizeHtml = '';
    if (o.sizes && o.sizes.length) {
      sizeHtml =
        (L.sizePrefix ? '<span>' + esc(L.sizePrefix) + '</span>' : '') +
        '<select class="pager-select" data-pg="size">' +
        o.sizes.map(function (n) {
          return '<option value="' + n + '"' + (Number(n) === Number(size) ? ' selected' : '') + '>' + n + '</option>';
        }).join('') +
        '</select>' +
        (L.sizeSuffix ? '<span>' + esc(L.sizeSuffix) + '</span>' : '');
    }

    // classList.add لا className: الشاشة ممكن تكون حاطّة كلاس تاني على العنصر
    el.classList.add('table-pager');
    el.innerHTML =
      '<span class="pager-info">' + sizeHtml + '<span>' + summary + '</span></span>' +
      '<span class="pager-nav">' +
        '<button type="button" class="pager-btn" data-pg="prev"' + (page <= 1 ? ' disabled' : '') + '>' +
          CHEV_PREV + esc(L.prev) +
        '</button>' +
        '<span class="pager-page">' + pageText + '</span>' +
        '<button type="button" class="pager-btn" data-pg="next"' + (page >= pages ? ' disabled' : '') + '>' +
          esc(L.next) + CHEV_NEXT +
        '</button>' +
      '</span>';

    var prev = el.querySelector('[data-pg="prev"]');
    var next = el.querySelector('[data-pg="next"]');
    var sel  = el.querySelector('[data-pg="size"]');
    if (prev) prev.addEventListener('click', function () { if (page > 1) onGo(page - 1); });
    if (next) next.addEventListener('click', function () { if (page < pages) onGo(page + 1); });
    if (sel && o.onSize) sel.addEventListener('change', function () { o.onSize(parseInt(this.value, 10)); });

    // ⚠️ القائمة دي بتتولد بعد تحميل الصفحة، وNuhSelect بيشتغل مرة واحدة عند
    //    DOMContentLoaded — فلولا النداء ده كانت هتطلع بقائمة ويندوز الخام
    //    وسط شاشة كل قوائمها بشكل النظام.
    if (sel && window.NuhSelect && NuhSelect.enhance) NuhSelect.enhance(el);
  }

  // ==========================================================================
  //  ترتيب الأعمدة — النسخة الوحيدة في النظام.
  //
  //  ⚠️ كان كل جدول كاتب الترتيب بنفسه: دالة sortByColumn في «الطلاب»
  //     و«الطلبات» و«سجل العمليات»، وsortHousing + updateHousingSortIndicators
  //     في «حسابات السكن» — أربع نسخ لنفس السطرين، وكل واحدة معاها حلقة
  //     بتمشي على قائمة أعمدة **مكتوبة بالإيد** جوّه الدالة. يعني إضافة عمود
  //     قابل للفرز محتاجة تعديل في مكانين في نفس الشاشة، ولو نسيت التاني
  //     العمود بيترتّب من غير ما يظهر عليه سهم.
  //
  //  ⚠️ والمؤشّر نفسه كان <span class="sort-ind" id="sort-الحقل"> مكتوب في
  //     الـ HTML لكل عمود، والدالة بتدوّر عليه بالـ id وتكتب فيه ' ▲' نصًّا.
  //     دلوقتي مفيش span خالص: الجافاسكريبت بيحطّ aria-sort على الـ <th>
  //     (وهي الخاصية الصح لقارئ الشاشة أصلًا) والـ CSS هو اللي بيرسم السهم.
  //
  //  الاستخدام:
  //     <th data-sort="full_name">الاسم</th>          ← ده كل المطلوب في الـ HTML
  //
  //     var srt = NuhTable.sort('stuTable', function () { state.page = 1; load(); });
  //     ... url += srt.qs();
  //
  //  ⚠️ الاستماع على الجدول نفسه لا على كل <th>: الترويسة ممكن تتبني من
  //     الجافاسكريبت بعد كده، والمستمع الواحد بيغطّي أي <th> يتضاف بعدين.
  // ==========================================================================
  // ==========================================================================
  //  مقارنة قيمتين للترتيب في المتصفح.
  //
  //  ⚠️ ودي للجداول اللي بتحمّل **كل** صفوفها في نداء واحد بلا ترقيم
  //     (المستخدمون، الأدوار). الجدول المقسّم صفحات لازم يترتّب على الخادم:
  //     الترتيب في المتصفح بيرتّب الصفحة اللي قدامك بس، فأول اسم أبجديًّا في
  //     صفحة ٢ ممكن يسبق آخر اسم في صفحة ١ والمستخدم فاكر إنه شايف ترتيبًا
  //     صحيحًا.
  // ==========================================================================
  var ISO_DATE = /^\d{4}-\d{2}-\d{2}([T ]|$)/;

  function cmpValues(x, y) {
    // ⚠️ الفاضي في الآخر دايمًا، مهما كان اتجاه الترتيب: اللي بيدوس عشان
    //    يرتّب عايز يشوف بيانات، مش عشرين سطر فاضي في وشّه.
    var ex = (x === null || x === undefined || x === '');
    var ey = (y === null || y === undefined || y === '');
    if (ex && ey) return 0;
    if (ex) return 1;
    if (ey) return -1;

    if (typeof x === 'boolean' || typeof y === 'boolean') return (x ? 1 : 0) - (y ? 1 : 0);
    if (typeof x === 'number' && typeof y === 'number') return x - y;

    var sx = String(x), sy = String(y);

    // ⚠️ التاريخ يتقارن كوقت لا كنصّ — بس بعد ما نتأكد إنه فعلًا تاريخ بصيغة
    //    معروفة. Date.parse على أي نصّ بتفسّر حاجات زي "10" على إنها تاريخ
    //    في بعض المتصفحات، فالفحص بالنمط مش بالمحاولة.
    if (ISO_DATE.test(sx) && ISO_DATE.test(sy)) {
      var tx = Date.parse(sx), ty = Date.parse(sy);
      if (!isNaN(tx) && !isNaN(ty)) return tx - ty;
    }

    // numeric:true عشان «غرفة 10» تيجي بعد «غرفة 9» لا قبلها،
    // و sensitivity:'base' عشان الهمزات والتشكيل ما يفرّقوش الترتيب.
    return sx.localeCompare(sy, undefined, { numeric: true, sensitivity: 'base' });
  }

  function sort(tableId, onChange, initial) {
    var by  = (initial && initial.by) || '';
    var asc = !!(initial && initial.asc);

    var root = typeof tableId === 'string' ? document.getElementById(tableId) : tableId;

    // ⚠️ لو الشاشة نادت الدالة قبل ما الجدول يبقى في الصفحة، بنعيد المحاولة
    //    مرة واحدة بعد التحميل. من غير ده الترتيب كان هيفضل ميت في سكوت:
    //    مفيش خطأ في الكونسول، بس الضغط على الترويسة مابيعملش حاجة.
    if (!root && typeof tableId === 'string' && document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            root = document.getElementById(tableId);
            if (root) { attach(); paint(); }
        });
    }

    function paint() {
      if (!root) return;
      var ths = root.querySelectorAll('th[data-sort]');
      for (var i = 0; i < ths.length; i++) {
        if (ths[i].getAttribute('data-sort') === by)
          ths[i].setAttribute('aria-sort', asc ? 'ascending' : 'descending');
        else
          ths[i].removeAttribute('aria-sort');
      }
    }

    function attach() {
      root.addEventListener('click', function (e) {
        var th = e.target && e.target.closest ? e.target.closest('th[data-sort]') : null;
        if (!th || !root.contains(th)) return;
        var f = th.getAttribute('data-sort');
        // نفس العمود = اقلب الاتجاه. عمود جديد = ابدأ تصاعدي.
        if (by === f) asc = !asc; else { by = f; asc = true; }
        paint();
        onChange(by, asc);
      });
    }

    if (root) { attach(); paint(); }

    return {
      by:  function () { return by; },
      asc: function () { return asc; },
      // ترتيب مصفوفة في المتصفح (للجداول بلا ترقيم). get اختيارية لما تكون
      // القيمة المعروضة غير الخاصية الخام (اسم الدور بدل رقمه مثلًا).
      sortRows: function (rows, get) {
        if (!by || !rows) return rows;
        var pick = get || function (r, k) { return r[k]; };
        return rows.slice().sort(function (a, b) {
          var r = cmpValues(pick(a, by), pick(b, by));
          return asc ? r : -r;
        });
      },
      // ⚠️ جزء الاستعلام كامل من هنا: كل شاشة كانت بتركّبه بنفسها، وواحدة
      //    كانت ناسية encodeURIComponent.
      qs:  function () { return by ? '&sortBy=' + encodeURIComponent(by) + '&sortAsc=' + asc : ''; },
      set: function (f, a) { by = f || ''; asc = !!a; paint(); },
      refresh: paint
    };
  }

  // ==========================================================================
  //  خليّة بسطرين: قيمة أساسية بارزة وثانوية رمادية تحتها.
  //
  //  ⚠️ ليه هنا وليه مشتركة:
  //     الشكل ده هو قاعدة جداول النظام كلها (.tbl-2line)، وكان كل شاشة بتكتبه
  //     بإيدها فبدأت تفترق فعلًا: قائمة الطلاب بتستعمل .cell-p للسطر الأول،
  //     وقائمة أعضاء هيئة التدريس كانت بتستعمل .cell-p2 ومعاه صنف زيادة -
  //     فنفس الخليّة بوزنين مختلفين في شاشتين بيتقارنوا ببعض كل يوم.
  //     التعريف هنا، والشاشتين بينادوه.
  //
  //  ⚠️ والسطر الفارغ مابيتكتبش أصلًا لا بيتساب فاضي: عنصر فاضي في الشبكة
  //     بياخد ارتفاع سطر، فالصفوف اللي ناقصها القيمة التانية بتبقى أطول من
  //     غير سبب ظاهر.
  //
  //  الاستعمال:
  //     NuhTable.two(s.full_name, s.full_name_english, { en: true })
  //     NuhTable.two(s.student_id, s.phone, { num: true })
  //
  //  الخيارات:
  //     num    أرقام بخطّ جدولي (.cell-num) - الخانات بتتراصّ تحت بعضها
  //     en     السطر التاني لاتيني (.cell-en) - عزل اتجاهه جوّه صفحة عربية
  //     nowrap الوحدة الواحدة ماتتكسرش على سطرين («مبنى 68»)
  // ==========================================================================
  function two(primary, secondary, opts) {
    opts = opts || {};
    var p = (primary == null ? '' : String(primary)).trim();
    var s = (secondary == null ? '' : String(secondary)).trim();
    if (!p && !s) return '';

    var extra = (opts.num ? ' cell-num' : '') + (opts.nowrap ? ' cell-nowrap' : '');
    var out = '';
    if (p) out += '<div class="cell-p' + extra + '">' + escHtml(p) + '</div>';
    if (s) out += '<div class="cell-q' + extra + (opts.en ? ' cell-en' : '') + '">' +
                  escHtml(s) + '</div>';
    return out;
  }

  return { bind: bind, pager: pager, sort: sort, two: two };
})();
