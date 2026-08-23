// ==========================================================================
//  switch-field.js — مفتاح التفعيل/التعطيل، بصياغة واحدة لكل شاشات النظام.
//
//  ⚠️ ملف مشترك عن قصد — نفس منطق name-fields.js و phone-field.js.
//     كان المفتاح مكتوبًا بالحرف داخل lookup-admin.js وحده، بينما شاشة
//     المستخدمين تفعّل وتعطّل بزرَّين في عمود الإجراءات: نفس الفكرة بمظهرين،
//     والحالة معروضة مرتين — شارة في عمود وزر في عمود آخر.
//
//  ⚠️ الكلمة بجانب المفتاح جزء من المكوّن لا زيادة عليه: المفتاح وحده يحتاج
//     تخمينًا في الواجهة العربية (أي الجهتين «تشغيل»؟)، والكلمة تحسمها. وهي
//     كذلك ما يقرأه قارئ الشاشة، إذ المفتاح بلا اسم يُقرأ «مربع اختيار» فقط.
//     ولهذا تُمرَّر النصوص من الشاشة (resx) لا تُكتب هنا — الترجمة تبقى مكانها.
//
//  الشكل في css/site.css: ‎.nsw / .nsw-track / .nsw-field / .nsw-label
// ==========================================================================
var NuhSwitch = (function () {

  // تهريب HTML — التعريف الوحيد في /js/esc.js
  function esc(v) { return escHtml(v); }

  // خانة جدول كاملة: المفتاح + الكلمة.
  //   on        الحالة الحالية
  //   onchange  نصّ الاستدعاء عند التبديل (مثال: "toggleActive(5)")
  //   onText    نصّ الحالة المفعّلة   (من resx)
  //   offText   نصّ الحالة المعطّلة  (من resx)
  //   opts.disabled  يعرض المفتاح بلا إمكان تبديل (صلاحية ناقصة مثلًا)
  function cell(on, onchange, onText, offText, opts) {
    opts = opts || {};
    var label = on ? onText : offText;
    // ⚠️ aria-label على العنصر الحقيقي لا على الغلاف: العنصر هو ما يصل إليه
    //    التنقّل بلوحة المفاتيح وما يقرأه قارئ الشاشة.
    return '<span class="nsw-field' + (on ? ' is-on' : '') + '">' +
             '<label class="nsw">' +
               '<input type="checkbox"' + (on ? ' checked' : '') +
                      (opts.disabled ? ' disabled' : '') +
                      ' aria-label="' + esc(label) + '"' +
                      (opts.disabled ? '' : ' onchange="' + onchange + '"') + '>' +
               '<span class="nsw-track"></span>' +
             '</label>' +
             '<span class="nsw-label">' + esc(label) + '</span>' +
           '</span>';
  }

  // نفس المكوّن داخل نموذج: يُقرأ ويُكتب بالمعرّف بدل الاستدعاء.
  function field(id, on, onText, offText) {
    return '<span class="nsw-field' + (on ? ' is-on' : '') + '" id="' + esc(id) + '_wrap"' +
                ' data-on="' + esc(onText) + '" data-off="' + esc(offText) + '">' +
             '<label class="nsw">' +
               '<input type="checkbox" id="' + esc(id) + '"' + (on ? ' checked' : '') +
                      ' aria-label="' + esc(on ? onText : offText) + '"' +
                      ' onchange="NuhSwitch.sync(\'' + esc(id) + '\')">' +
               '<span class="nsw-track"></span>' +
             '</label>' +
             '<span class="nsw-label">' + esc(on ? onText : offText) + '</span>' +
           '</span>';
  }

  // تحديث الكلمة واللون بعد التبديل داخل نموذج
  function sync(id) {
    var el = document.getElementById(id);
    var wrap = document.getElementById(id + '_wrap');
    if (!el || !wrap) return;
    var on = el.checked;
    var txt = on ? (wrap.getAttribute('data-on') || '') : (wrap.getAttribute('data-off') || '');
    wrap.classList.toggle('is-on', on);
    var lbl = wrap.querySelector('.nsw-label');
    if (lbl) lbl.textContent = txt;
    el.setAttribute('aria-label', txt);
  }

  function get(id) {
    var el = document.getElementById(id);
    return !!(el && el.checked);
  }

  function set(id, on) {
    var el = document.getElementById(id);
    if (!el) return;
    el.checked = !!on;
    sync(id);
  }

  return { cell: cell, field: field, sync: sync, get: get, set: set };
})();
