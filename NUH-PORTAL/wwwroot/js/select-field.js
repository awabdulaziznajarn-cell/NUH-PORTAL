// ==========================================================================
//  select-field.js — قائمة اختيار بشكل النظام بدل قائمة نظام التشغيل.
//
//  ⚠️ ليه الملف ده موجود:
//     عنصر <select> بيرسمه نظام التشغيل مش الصفحة. يعني القائمة اللي بتنزل
//     لما تضغط السهم شكلها من ويندوز: خط مختلف، وحواف حادّة، وسطر مختار
//     بأزرق ويندوز — لا علاقة له بألوان النظام ولا بخطه. الفرق ده بيبان
//     فورًا في شاشة زي «تسجيل طالب جديد» فيها ست قوائم.
//
//     الحل هنا: نسيب <select> الأصلي في الصفحة زي ما هو (مخفي بصريًا)،
//     ونرسم فوقه واجهة من عندنا. وده مقصود:
//
//       • كل الكود القديم يفضل شغّال بلا تعديل: sel.value و sel.options
//         و innerHTML = '<option>...' و required و إرسال النموذج — كلها
//         بتشتغل على العنصر الأصلي زي ما هي.
//       • لو الجافاسكريبت اتعطّل لأي سبب، الصفحة تفضل تشتغل بالقائمة
//         الأصلية بدل ما تتوقف.
//
//     والمزامنة في الاتجاهين: اختيار المستخدم بيتكتب في <select> ويطلق
//     حدث change (فالكود اللي مستني الحدث بيشتغل عادي)، وأي تغيير من الكود
//     على value أو على قائمة الخيارات بيتعكس على الواجهة تلقائيًا.
//
//  الاستخدام: تلقائي. الملف مربوط في التخطيط وبيشتغل على كل <select>.
//     للاستثناء: <select data-nuh-select="off">
// ==========================================================================
var NuhSelect = (function () {

  var OPEN = null;                 // القائمة المفتوحة حاليًا (واحدة بحد أقصى)
  var SEARCH_MIN = 8;              // خانة البحث تظهر لما الخيارات تزيد عن كده

  function esc(v) {
    return String(v == null ? '' : v)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // الخيار الأول بنص زي «اختر الكلية» وقيمة فاضية = نص إرشادي لا اختيار
  function isPlaceholder(opt) { return opt.value === '' || opt.disabled; }

  function build(sel) {
    if (sel.__nuhSelect) return sel.__nuhSelect;
    if (sel.multiple || sel.size > 1) return null;
    if (sel.getAttribute('data-nuh-select') === 'off') return null;

    var wrap = document.createElement('div');
    wrap.className = 'nsel';

    var btn = document.createElement('button');
    btn.type = 'button';                       // ⚠️ بدون النوع ده الزر بيبعت النموذج
    btn.className = 'nsel-btn';
    btn.innerHTML = '<span class="nsel-txt"></span>' +
      '<svg class="nsel-chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
      'stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">' +
      '<polyline points="6 9 12 15 18 9"/></svg>';

    // ⚠️ ستايل العنصر الأصلي (زي style="max-width:220px") لازم ينتقل للغلاف،
    //    وإلا العنصر المخفي هو اللي بياخده والزر الظاهر يطلع بعرض تاني.
    var inline = sel.getAttribute('style');
    if (inline) { wrap.setAttribute('style', inline); sel.removeAttribute('style'); }

    sel.parentNode.insertBefore(wrap, sel);
    wrap.appendChild(sel);
    wrap.appendChild(btn);
    sel.classList.add('nsel-native');

    var panel = null, list = null, search = null, active = -1;

    function label() {
      var o = sel.options[sel.selectedIndex];
      var txt = btn.querySelector('.nsel-txt');
      if (!o) { txt.textContent = ''; txt.classList.add('is-ph'); return; }
      txt.textContent = o.text;
      txt.classList.toggle('is-ph', isPlaceholder(o));
    }

    function rows(filter) {
      var q = (filter || '').trim().toLowerCase();
      var html = '', shown = 0;
      for (var i = 0; i < sel.options.length; i++) {
        var o = sel.options[i];
        if (isPlaceholder(o) && sel.options.length > 1) continue;   // النص الإرشادي مش خيار
        if (q && o.text.toLowerCase().indexOf(q) === -1) continue;
        html += '<div class="nsel-opt' + (i === sel.selectedIndex ? ' is-sel' : '') +
                '" data-i="' + i + '" role="option">' +
                  '<span>' + esc(o.text) + '</span>' +
                  '<svg class="nsel-tick" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
                  'stroke-width="3" stroke-linecap="round" stroke-linejoin="round">' +
                  '<polyline points="20 6 9 17 4 12"/></svg>' +
                '</div>';
        shown++;
      }
      // ⚠️ كان النص عربي ثابت — والبوابة بتشتغل بلغتين
      if (!shown) html = '<div class="nsel-empty">' +
        (document.documentElement.lang === 'en' ? 'No results' : 'لا توجد نتائج') + '</div>';
      list.innerHTML = html;
      active = -1;
    }

    // ⚠️ اللوحة position:fixed مش absolute — عشان ما تتقصّش جوّه نافذة منبثقة
    //    أو جدول عنده overflow:hidden، وده بيحصل فعلًا في شاشات النظام.
    function place() {
      if (!panel) return;
      var r = btn.getBoundingClientRect();
      // الحقل نفسه اختفى من الشاشة — إبقاء القائمة معلّقة في الفراغ ملهوش معنى
      if (r.bottom < 0 || r.top > window.innerHeight) { close(); return; }
      var below = window.innerHeight - r.bottom;
      var h = Math.min(panel.scrollHeight, 300);
      var up = below < h + 12 && r.top > below;

      // ⚠️ اللوحة كانت تأخذ عرض الحقل بالضبط. والحقل قد يكون ضيّقًا لأن جاره
      //    في شريط الفلاتر تمدّد - فتصير القائمة شريطًا عرضه ٤٠ بكسل يتكسّر
      //    فيه «Osama Eltokhy (NUH)» إلى ثلاثة أسطر بحرف أو حرفين في السطر.
      //    الحقل يقبل القصّ بثلاث نقاط، أما القائمة فوظيفتها أن تُقرأ.
      //    حدٌّ أدنى ٢٠٠ بكسل، وحدٌّ أعلى هو عرض الشاشة ناقص هامش.
      var w = Math.min(Math.max(r.width, 200), window.innerWidth - 16);

      // ⚠️ وبعد التوسيع قد تخرج اللوحة عن حافة الشاشة - وهو ما يحدث كثيرًا في
      //    الاتجاه من اليمين لليسار لأن الحقل يقع قرب الحافة اليمنى.
      var left = Math.max(8, Math.min(r.left, window.innerWidth - w - 8));

      panel.style.width = w + 'px';
      panel.style.insetInlineStart = 'auto';
      panel.style.left = left + 'px';
      panel.style.top = up ? (r.top - h - 6) + 'px' : (r.bottom + 6) + 'px';
      panel.style.maxHeight = h + 'px';
    }

    function open() {
      if (sel.disabled) return;
      close();
      panel = document.createElement('div');
      panel.className = 'nsel-panel';
      panel.setAttribute('dir', document.documentElement.getAttribute('dir') || 'rtl');
      var withSearch = sel.options.length > SEARCH_MIN;
      panel.innerHTML =
        (withSearch ? '<div class="nsel-search"><input type="text" autocomplete="off" placeholder="بحث..."></div>' : '') +
        '<div class="nsel-list" role="listbox"></div>';
      document.body.appendChild(panel);
      list = panel.querySelector('.nsel-list');
      search = panel.querySelector('.nsel-search input');
      rows('');
      place();
      wrap.classList.add('is-open');
      OPEN = { close: close, place: place, panel: panel, btn: btn };

      if (search) { search.addEventListener('input', function () { rows(search.value); }); search.focus(); }
      else btn.focus();

      list.addEventListener('mousedown', function (e) {
        var el = e.target.closest ? e.target.closest('.nsel-opt') : null;
        if (!el) return;
        e.preventDefault();
        pick(parseInt(el.getAttribute('data-i'), 10));
      });
    }

    function close() {
      if (panel && panel.parentNode) panel.parentNode.removeChild(panel);
      panel = null; list = null; search = null; active = -1;
      wrap.classList.remove('is-open');
      if (OPEN && OPEN.btn === btn) OPEN = null;
    }

    function pick(i) {
      if (isNaN(i)) return;
      sel.selectedIndex = i;
      label();
      close();
      // ⚠️ لازم نطلق change بأيدينا — التعديل من الكود ما بيطلقهوش، وشاشات
      //    كتير مربوطة بيه (اختيار الكلية بيملأ الأقسام مثلًا).
      sel.dispatchEvent(new Event('change', { bubbles: true }));
      btn.focus();
    }

    function move(step) {
      if (!list) return;
      var opts = list.querySelectorAll('.nsel-opt');
      if (!opts.length) return;
      active = (active + step + opts.length) % opts.length;
      for (var i = 0; i < opts.length; i++) opts[i].classList.toggle('is-active', i === active);
      opts[active].scrollIntoView({ block: 'nearest' });
    }

    btn.addEventListener('click', function (e) { e.preventDefault(); panel ? close() : open(); });

    btn.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') { e.preventDefault(); if (!panel) open(); }
    });

    (panel || wrap).addEventListener('keydown', function () {});
    document.addEventListener('keydown', function (e) {
      if (!panel || OPEN.btn !== btn) return;
      if (e.key === 'Escape') { e.preventDefault(); close(); btn.focus(); }
      else if (e.key === 'ArrowDown') { e.preventDefault(); move(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
      else if (e.key === 'Enter') {
        e.preventDefault();
        var el = list.querySelector('.nsel-opt.is-active') || list.querySelector('.nsel-opt');
        if (el) pick(parseInt(el.getAttribute('data-i'), 10));
      }
    });

    // ⚠️ الكود بيغيّر الخيارات وقت التشغيل (الأقسام بتتفلتر حسب الكلية،
    //    والغرف حسب الدور). المراقب ده بيخلّي الواجهة تتبع من غير ما أي
    //    شاشة تحتاج تنادينا.
    if (window.MutationObserver) {
      new MutationObserver(function () { label(); if (panel) rows(search ? search.value : ''); })
        .observe(sel, { childList: true, subtree: true });
    }

    // تغيير القيمة من الكود مباشرة (sel.value = '...') من غير حدث
    try {
      var d = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value');
      if (d && d.get && d.set) {
        Object.defineProperty(sel, 'value', {
          configurable: true,
          get: function () { return d.get.call(this); },
          set: function (v) { d.set.call(this, v); label(); }
        });
      }
    } catch (e) { /* المتصفح رافض — المراقب و change بيغطّوا الباقي */ }

    sel.addEventListener('change', label);

    label();
    sel.__nuhSelect = { refresh: label, close: close };
    return sel.__nuhSelect;
  }

  function enhance(root) {
    var scope = root || document;
    var list = scope.querySelectorAll ? scope.querySelectorAll('select') : [];
    for (var i = 0; i < list.length; i++) {
      try { build(list[i]); } catch (e) { /* قائمة واحدة ما تكسرش الصفحة */ }
    }
  }

  document.addEventListener('mousedown', function (e) {
    if (!OPEN) return;
    if (OPEN.panel.contains(e.target) || OPEN.btn.contains(e.target)) return;
    OPEN.close();
  });
  // ⚠️ كان التمرير بيقفل القائمة — وده غلط من ناحيتين: التمرير جوّه القائمة
  //    نفسها كان بيقفلها (تحاول توصل لخيار تحت فتتقفل في وشّك)، وتمرير الصفحة
  //    كان بيلغي اختيارك بدل ما القائمة تمشي مع الحقل. دلوقتي بتتحرّك معاه،
  //    وما بتتقفل إلا بالضغط بره أو Escape أو الاختيار.
  window.addEventListener('resize', function () { if (OPEN) OPEN.place(); });
  window.addEventListener('scroll', function (e) {
    if (!OPEN) return;
    if (OPEN.panel.contains(e.target)) return;   // تمرير جوّه القائمة نفسها
    OPEN.place();
  }, true);

  if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', function () { enhance(document); });
  else enhance(document);

  // شاشات بتبني نماذج بالجافاسكريبت بعد التحميل تنادي دي
  return { enhance: enhance, refresh: function (el) { var s = el && el.__nuhSelect; if (s) s.refresh(); } };
})();
