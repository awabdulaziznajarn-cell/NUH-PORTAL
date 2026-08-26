// ==========================================================================
//  phone-field.js — خانة رقم الجوال السعودي، بقاعدة واحدة لكل شاشات النظام.
//
//  القاعدة: مفتاح +966 ثابت في الواجهة، والمستخدم يكتب ٩ أرقام تبدأ بـ 5.
//           لا صفر في البداية، ولا 966 مكتوبة باليد.
//
//  ⚠️ ملف مشترك عن قصد — نفس منطق housing-fields.js و name-fields.js.
//     كانت كل شاشة تحمل نسخة من التحقق، فاختلفت الصيغ: شاشة تطلب 12 رقمًا
//     تبدأ بـ 9665، وأخرى 10 أرقام تبدأ بـ 05، وثالثة تقبل الاثنين. أي تعديل
//     على القاعدة كان يُنسى في شاشة أو اثنتين. القاعدة الآن في مكان واحد.
//
//  سلوك مقصود ومهم: الصفر في البداية لا يُحذف بصمت أثناء الكتابة.
//     الحذف الصامت كان يعني أن المستخدم يضغط 0 فلا يظهر شيء ولا يفهم السبب.
//     الآن يبقى ظاهرًا ويظهر التنبيه، فيتعلّم الصيغة. أما اللصق (رقم كامل
//     بصيغة 05… أو +966… أو 00966…) فيُصحَّح تلقائيًا لأنه عملية واحدة
//     واضحة النية — والمستخدم يرى النتيجة أمامه.
// ==========================================================================
var NuhPhone = (function () {

  var VALID = /^5\d{8}$/;          // ٩ أرقام تبدأ بـ 5
  var FULL_LOCAL = /^5\d{8}$/;     // شكل الرقم بعد إزالة البادئات

  // أي صيغة → ٩ أرقام. البادئات (966 / 00966) تُزال دائمًا لأنها لا لبس فيها.
  // الصفر يُزال فقط إذا كان الباقي رقمًا كاملًا صحيحًا — أي حالة لصق.
  function normalize(raw) {
    var d = String(raw == null ? '' : raw).replace(/\D/g, '');

    if (d.indexOf('00966') === 0) d = d.slice(5);
    else if (d.indexOf('966') === 0) d = d.slice(3);

    var noZeros = d.replace(/^0+/, '');
    if (d !== noZeros && FULL_LOCAL.test(noZeros)) d = noZeros;

    return d.slice(0, 9);
  }

  function isValid(digits) { return VALID.test(digits || ''); }

  // الصيغة المخزّنة في قاعدة البيانات
  function toStorage(raw) {
    var d = normalize(raw);
    return isValid(d) ? '966' + d : '';
  }

  function last4(raw) { return normalize(raw).slice(-4); }

  // سبب الرفض — النص يأتي من الشاشة (resx أو مكتوب) عشان الترجمة تفضل مكانها
  function errorKind(digits) {
    if (!digits) return null;                       // فاضي: لا تنبيه
    if (digits.charAt(0) !== '5') return 'start';   // لازم يبدأ بـ 5
    if (digits.length < 9) return null;             // لسه بيكتب
    return isValid(digits) ? null : 'length';
  }

  // ربط الخانة. opts:
  //   input   (إلزامي) عنصر <input>
  //   wrap    الحاوية اللي بتتلوّن (تاخد class ok / bad)
  //   hint    عنصر نص التنبيه
  //   button  زر يتقفل لحد ما الرقم يصح
  //   messages { start, length } — نصّ أو **دالة تُرجع نصًّا**.
  //
  //   ⚠️ الدالة ليست ترفًا: صفحات بوابة الطالب تجيب قاموس الترجمة بـ fetch
  //      بعد تحميل السكربت، فأي t('...') يُنادى وقت بناء الكائن يرجع المفتاح
  //      الخام ويتجمّد فيه. تمرير دالة يؤجّل الترجمة إلى لحظة عرض التنبيه -
  //      وهي دائمًا بعد وصول القاموس لأنها تتبع كتابة المستخدم.
  //   onChange(digits, valid)
  function attach(opts) {
    opts = opts || {};
    var el = typeof opts.input === 'string' ? document.getElementById(opts.input) : opts.input;
    if (!el) return null;

    var wrap = typeof opts.wrap === 'string' ? document.getElementById(opts.wrap) : opts.wrap;
    var hint = typeof opts.hint === 'string' ? document.getElementById(opts.hint) : opts.hint;
    var btn = typeof opts.button === 'string' ? document.getElementById(opts.button) : opts.button;
    var msg = opts.messages || {};

    function apply() {
      var d = normalize(el.value);
      if (el.value !== d) el.value = d;             // يصحّح اللصق فورًا

      var ok = isValid(d);
      var kind = errorKind(d);

      if (wrap) {
        wrap.classList.toggle('ok', ok);
        wrap.classList.toggle('bad', !!kind);
      }
      if (hint) {
        var m = kind ? (msg[kind] || msg.start || '') : '';
        hint.textContent = typeof m === 'function' ? (m() || '') : m;
        hint.classList.toggle('show', !!kind);
      }
      if (btn) {
        btn.disabled = !ok;
        btn.style.opacity = ok ? '' : '.55';
        btn.style.cursor = ok ? '' : 'not-allowed';
      }
      if (typeof opts.onChange === 'function') opts.onChange(d, ok);
      return ok;
    }

    el.addEventListener('input', apply);
    el.addEventListener('blur', apply);
    if (el.value) apply();                          // قيمة محمَّلة مسبقًا

    return { validate: apply, value: function () { return normalize(el.value); } };
  }

  return {
    normalize: normalize,
    isValid: isValid,
    toStorage: toStorage,
    last4: last4,
    errorKind: errorKind,
    attach: attach,
    VALID: VALID
  };
})();
