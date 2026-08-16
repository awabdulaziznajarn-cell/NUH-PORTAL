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

  function cell(colspan, inner, pad, color) {
    return '<tr><td colspan="' + colspan + '" style="text-align:center;padding:' +
           (pad || '40px') + (color ? ';color:' + color : '') + '">' + inner + '</td></tr>';
  }

  function bind(tbodyId, colspan) {
    var loaded = false;

    function el() { return document.getElementById(tbodyId); }
    function put(html) { var n = el(); if (n) n.innerHTML = html; }

    return {
      // مؤشّر التحميل. الـ .spinner معرّف في ستايل كل شاشة (وفي site.css).
      loading: function () {
        put(cell(colspan, '<div class="spinner"></div>', '40px'));
      },

      // الصفوف، أو رسالة «لا توجد نتائج» لو مفيش. لا تُستدعى قبل done().
      rows: function (html, emptyText) {
        put(html || cell(colspan, emptyText || '—', '28px', 'var(--gray-500)'));
      },

      // فشل النداء — لون مختلف عن «لا توجد نتائج» عمدًا: الاتنين مش نفس الحالة.
      error: function (msg) {
        loaded = true;
        put(cell(colspan, msg || '', '40px', '#b42318'));
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
  //    labels: { show, to, of, page, pageOf, prev, next } نصوص مترجمة تمرّرها الشاشة.
  //    onGo(pageNumber) تُستدعى عند الضغط على السابق/التالي.
  function pager(elId, data, labels, onGo) {
    var el = typeof elId === 'string' ? document.getElementById(elId) : elId;
    if (!el) return;

    var size  = data.pageSize || 25;
    var page  = data.page || 1;
    var total = data.total || data.totalCount || 0;
    var pages = Math.max(1, Math.ceil(total / size));
    var from  = total === 0 ? 0 : (page - 1) * size + 1;
    var to    = Math.min(total, page * size);
    var L     = labels || {};

    function esc(v) {
      return String(v == null ? '' : v)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    el.innerHTML =
      '<span>' + esc(L.show) + ' <b>' + from + '</b> ' + esc(L.to) + ' <b>' + to + '</b> ' +
      esc(L.of) + ' <b>' + total + '</b></span>' +
      '<span style="display:flex;gap:8px;align-items:center">' +
        '<button class="btn btn-cancel" data-pg="prev"' + (page <= 1 ? ' disabled' : '') + '>' + esc(L.prev) + '</button>' +
        '<span>' + esc(L.page) + ' ' + page + ' ' + esc(L.pageOf) + ' ' + pages + '</span>' +
        '<button class="btn btn-cancel" data-pg="next"' + (page >= pages ? ' disabled' : '') + '>' + esc(L.next) + '</button>' +
      '</span>';

    var prev = el.querySelector('[data-pg="prev"]');
    var next = el.querySelector('[data-pg="next"]');
    if (prev) prev.addEventListener('click', function () { if (page > 1) onGo(page - 1); });
    if (next) next.addEventListener('click', function () { if (page < pages) onGo(page + 1); });
  }

  return { bind: bind, pager: pager };
})();
