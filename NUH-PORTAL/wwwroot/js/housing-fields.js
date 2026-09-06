/* ============================================================================
   خانات السكن المترابطة - منطق مشترك بين كل شاشات إدخال بيانات السكن.
   ----------------------------------------------------------------------------
   سلسلة الاختيار:   الجنس ← المبنى ← الدور ← الشقة ← الغرفة

   ⚠️ المبنى بقى **جزءًا من السلسلة** لا خانة سابقة لها: أسلوب ترقيم الشقق
      والغرف بيختلف من مبنى لمبنى، فاختيار المبنى بيحدّد الأرقام اللي هتظهر
      في الخانتين اللي بعده.

        سكن الطلاب  (Continuous): الترقيم متّصل عبر المبنى.
            الأرضي شقق ١-٤ وغرفها ١-١٦، الدور ١ شقق ٥-٨ وغرفها ١٧-٣٢ ...
        سكن الطالبات (PerFloor)  : الترقيم بيبدأ من أول كل دور.
            كل دور شقق ١-٤، وكل شقة غرفها ١-٤.

      قبل كده كانت الغرفة ١-٤ دايمًا في كل الشاشات - وده غلط في سكن الطلاب:
      شقة ٥ غرفها ١٧-٢٠، والخانة كانت بتعرض ١-٤ فالمشرف بيسجّل رقم غرفة
      مالوش وجود في المبنى.

   ⚠️ الأرقام مش مكتوبة هنا: بتتقرا من NuhHousingStructure (/js/nuh-housing.js)
      المتولّد من Core/HousingStructure.cs، وأسلوب كل مبنى من
      /api/lookups/buildings. يعني الخادم والمتصفح بيقروا من نفس المصدر.

   ملف مشترك عن قصد: نفس القواعد لازم تسري على تسجيل الطالب لنفسه، وتسجيل
   المشرف الفردي، وتعديل بيانات الطالب، ونقل السكن.
   ============================================================================ */
(function (global) {
  'use strict';

  // ⚠️ قيم احتياطية لو السكربت المتولّد ما حمّلش: الصفحة تفضل شغّالة بالبنية
  //    المعروفة بدل ما القوائم تطلع فاضية والمستخدم ما يقدرش يكمّل.
  var HS = global.NuhHousingStructure || {
    CONTINUOUS: 'Continuous', PER_FLOOR: 'PerFloor',
    APARTMENTS_PER_FLOOR: 4, ROOMS_PER_APARTMENT: 4,
    FLOOR_CODES: ['0', '1', '2', '3', '4'],
    apartmentsFor: function (sc, f) {
      var n = parseInt(f, 10); if (isNaN(n) || n < 0) return [];
      var base = (sc === 'PerFloor') ? 1 : n * 4 + 1, o = [];
      for (var i = 0; i < 4; i++) o.push(String(base + i));
      return o;
    },
    roomsFor: function (sc, a) {
      var n = parseInt(a, 10); if (isNaN(n) || n < 1) return [];
      var base = (sc === 'PerFloor') ? 1 : (n - 1) * 4 + 1, o = [];
      for (var i = 0; i < 4; i++) o.push(String(base + i));
      return o;
    }
  };

  var FLOOR_CODES = HS.FLOOR_CODES;   // "0" = الأرضي

  // خريطة كود المبنى ← قواعده. بتتملى مرة واحدة من القوائم المرجعية.
  // ⚠️ مافيش أي افتراض بالجنس هنا: المبنى بيقول أسلوبه بنفسه، فمبنى جديد
  //    بأسلوب مختلف بيشتغل من غير تعديل في الملف ده.
  var _rules = {};
  var _rulesReady = null;

  function norm(code) { return (code == null ? '' : String(code)).trim().toLowerCase(); }

  function setBuildings(list) {
    (list || []).forEach(function (b) {
      if (!b || !b.code) return;
      _rules[norm(b.code)] = {
        numbering: b.numbering || HS.CONTINUOUS,
        capacity: b.roomCapacity || 0,
        capacityMax: b.roomCapacityMax || 0
      };
    });
  }

  function loadRules() {
    if (_rulesReady) return _rulesReady;
    _rulesReady = fetch('/api/lookups/buildings', { credentials: 'same-origin' })
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(function (list) { setBuildings(list); })
      .catch(function () { });
    return _rulesReady;
  }

  function rulesFor(code) { return _rules[norm(code)] || null; }
  function schemeFor(code) {
    var r = rulesFor(code);
    return r ? r.numbering : HS.CONTINUOUS;
  }

  function el(id) { return id ? document.getElementById(id) : null; }

  // بيملأ الـ select ويحافظ على القيمة الحالية *فقط* لو لسه ضمن الخيارات الجديدة.
  // القيم القديمة الخارجة عن الترقيم (زي 101 و201) بتتشال عمدًا - المستخدم لازم
  // يختار من جديد بدل ما يتحفظ رقم مش موجود في المبنى.
  // ⚠️ placeholder ممكن يكون نص جاهز (شاشات الموظفين بتمرّره مترجم من الـ resx
  //    وقت الرندر) أو مفتاح resx (بوابة الطالب - القاموس بيوصل بعد التحميل).
  //    مع المفتاح بنحط data-i18n على الخيار، فتبديل اللغة بيحدّثه لوحده.
  function fillSelect(sel, values, placeholder, disabled, phKey) {
    if (!sel) return;
    var previous = sel.value;
    sel.innerHTML = '';

    var ph = document.createElement('option');
    ph.value = '';
    if (phKey) { ph.setAttribute('data-i18n', phKey); ph.textContent = (typeof t === 'function') ? t(phKey) : (placeholder || ''); }
    else { ph.textContent = placeholder || ''; }
    sel.appendChild(ph);

    values.forEach(function (v) {
      var o = document.createElement('option');
      o.value = v;
      o.textContent = v;
      sel.appendChild(o);
    });

    sel.disabled = !!disabled;
    sel.value = (previous && values.indexOf(previous) !== -1) ? previous : '';
  }

  /* ربط الخانات ببعض.
     opts = {
       building, floor, apartment, room : ids الخانات (أي واحدة ممكن تتساب)
       labels:    { floorFirst, selectApartment, selectRoom }   نصوص جاهزة، أو
       labelKeys: { floorFirst, selectApartment, selectRoom }   مفاتيح resx
     }
     بيرجّع دالة refresh() تنفع تتنادى بعد ما تتحط قيم برمجيًا (شاشات التعديل). */
  function attach(opts) {
    opts = opts || {};
    var labels = opts.labels || {};
    var keys = opts.labelKeys || {};   // بديل labels لما النص لسه ماوصلش
    var bEl = el(opts.building), fEl = el(opts.floor), aEl = el(opts.apartment), rEl = el(opts.room);

    function scheme() { return bEl ? schemeFor(bEl.value) : HS.CONTINUOUS; }

    function syncApartments() {
      if (!aEl) return;
      var floor = fEl ? fEl.value : '';
      if (!floor) {
        // من غير دور مفيش شقق - القائمة تتفضّى وتتقفل بدل ما تعرض كل الأرقام
        fillSelect(aEl, [], labels.floorFirst || '', true, keys.floorFirst);
        syncRooms();
        return;
      }
      fillSelect(aEl, HS.apartmentsFor(scheme(), floor), labels.selectApartment || '', false, keys.selectApartment);
      syncRooms();
    }

    // ⚠️ الغرف بقت تابعة للشقة لا ثابتة ١-٤: في الترقيم المتّصل رقم الغرفة
    //    بيتحدّد من رقم شقتها، فمن غير شقة مفيش غرف.
    function syncRooms() {
      if (!rEl) return;
      var apt = aEl ? aEl.value : '';
      if (!apt) {
        fillSelect(rEl, [], labels.selectApartmentFirst || labels.selectRoom || '', true,
                   keys.selectApartmentFirst || keys.selectRoom);
        return;
      }
      fillSelect(rEl, HS.roomsFor(scheme(), apt), labels.selectRoom || '', false, keys.selectRoom);
    }

    function refresh() { syncApartments(); }

    // القواعد بتوصل بعد التحميل - أول ما توصل بنعيد البناء، فالشاشة تفضل
    // مظبوطة حتى لو المستخدم اختار قبل ما الرد يرجع.
    loadRules().then(refresh);

    refresh();

    if (bEl) bEl.addEventListener('change', refresh);
    if (fEl) fEl.addEventListener('change', syncApartments);
    if (aEl) aEl.addEventListener('change', syncRooms);

    return refresh;
  }

  global.NuhHousing = {
    apartmentsFor: function (scheme, floor) { return HS.apartmentsFor(scheme, floor); },
    roomsFor: function (scheme, apt) { return HS.roomsFor(scheme, apt); },
    floors: function () { return FLOOR_CODES.slice(); },
    schemeFor: schemeFor,
    rulesFor: rulesFor,
    setBuildings: setBuildings,
    fillSelect: fillSelect,
    attach: attach,
    APARTMENTS_PER_FLOOR: HS.APARTMENTS_PER_FLOOR,
    ROOMS_PER_APARTMENT: HS.ROOMS_PER_APARTMENT
  };
})(window);
