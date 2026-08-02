/* ============================================================================
   خانات السكن المترابطة — منطق مشترك بين كل شاشات إدخال بيانات السكن.
   ----------------------------------------------------------------------------
   سلسلة الاختيار:   الجنس ← المبنى   |   الدور ← الشقة   |   الغرفة 1-4 دائمًا

   توزيع الشقق (نفسه في كل المباني — مؤكَّد مع الجهة):
       الأرضي (0) → 1-4      الدور 1 → 5-8      الدور 2 → 9-12
       الدور 3   → 13-16     الدور 4 → 17-20

   بتتحسب بالمعادلة مش بجدول مخزّن، عشان لو الأدوار أو عدد الشقق اتغيّر
   يبقى التعديل في ثابت واحد هنا بدل ما يتعدّل في أربع شاشات.

   ملف مشترك عن قصد: نفس القواعد لازم تسري على تسجيل الطالب لنفسه، وتسجيل
   المشرف الفردي، وتعديل بيانات الطالب، ونقل السكن. أي نسخة منفصلة من المنطق
   ده هتفرق عن التانية مع أول تعديل.
   ============================================================================ */
(function (global) {
  'use strict';

  var APARTMENTS_PER_FLOOR = 4;   // عدد الشقق في الدور الواحد
  var ROOMS_PER_APARTMENT = 4;    // عدد الغرف في الشقة الواحدة
  var FLOOR_CODES = ['0', '1', '2', '3', '4'];   // "0" = الأرضي

  // الدور → أرقام الشقق. الدور 3 مثلًا: 3*4+1 = 13 لحد 16.
  function apartmentsFor(floor) {
    var f = parseInt(floor, 10);
    if (isNaN(f) || f < 0) return [];
    var out = [];
    var start = f * APARTMENTS_PER_FLOOR + 1;
    for (var i = 0; i < APARTMENTS_PER_FLOOR; i++) out.push(String(start + i));
    return out;
  }

  function rooms() {
    var out = [];
    for (var i = 1; i <= ROOMS_PER_APARTMENT; i++) out.push(String(i));
    return out;
  }

  function el(id) { return id ? document.getElementById(id) : null; }

  // بيملأ الـ select ويحافظ على القيمة الحالية *فقط* لو لسه ضمن الخيارات الجديدة.
  // القيم القديمة الخارجة عن الترقيم (زي 101 و201) بتتشال عمدًا — المستخدم لازم
  // يختار من جديد بدل ما يتحفظ رقم مش موجود في المبنى.
  function fillSelect(sel, values, placeholder, disabled) {
    if (!sel) return;
    var previous = sel.value;
    sel.innerHTML = '';

    var ph = document.createElement('option');
    ph.value = '';
    ph.textContent = placeholder || '';
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
       floor, apartment, room   : ids الخانات (أي واحدة ممكن تتساب)
       labels: { floorFirst, selectApartment, selectRoom }
     }
     بيرجّع دالة refresh() تنفع تتنادى بعد ما تتحط قيم برمجيًا (شاشات التعديل). */
  function attach(opts) {
    opts = opts || {};
    var labels = opts.labels || {};
    var fEl = el(opts.floor), aEl = el(opts.apartment), rEl = el(opts.room);

    function syncApartments() {
      if (!aEl) return;
      var floor = fEl ? fEl.value : '';
      if (!floor) {
        // من غير دور مفيش شقق — القائمة تتفضّى وتتقفل بدل ما تعرض كل الأرقام
        fillSelect(aEl, [], labels.floorFirst || '', true);
        return;
      }
      fillSelect(aEl, apartmentsFor(floor), labels.selectApartment || '', false);
    }

    if (rEl) fillSelect(rEl, rooms(), labels.selectRoom || '', false);
    syncApartments();

    if (fEl) fEl.addEventListener('change', syncApartments);

    return function refresh() {
      if (rEl) fillSelect(rEl, rooms(), labels.selectRoom || '', false);
      syncApartments();
    };
  }

  global.NuhHousing = {
    apartmentsFor: apartmentsFor,
    rooms: rooms,
    floors: function () { return FLOOR_CODES.slice(); },
    fillSelect: fillSelect,
    attach: attach,
    APARTMENTS_PER_FLOOR: APARTMENTS_PER_FLOOR,
    ROOMS_PER_APARTMENT: ROOMS_PER_APARTMENT
  };
})(window);
