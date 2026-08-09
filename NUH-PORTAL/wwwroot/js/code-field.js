// ==========================================================================
//  code-field.js — قفل الزر حتى تكتمل بيانات الخانة.
//
//  ⚠️ ليه الملف ده موجود:
//     خانة رقم الجوال عندها القاعدة دي من زمان عبر NuhPhone.attach: الزر
//     معطّل ورماديّ حتى يصحّ الرقم. لكن الخانات الرقمية التانية مكانتش كده،
//     والنتيجة تناقض داخل نفس الشاشة:
//
//       • «تحقق» في شاشة رمز التحقق كان أزرق من أول ثانية، والطالب يضغطه قبل
//         ما يكمل الرمز فيتلقّى رسالة خطأ كان ممكن ما تحصلش أصلًا.
//       • في شاشة التتبع، تبويب «رقم الجوال» زرّه مقفول لحد ما الرقم يصحّ
//         (NuhPhone)، وتبويب «رقم الطلب» زرّه مفتوح دائمًا — نفس الشاشة
//         وسلوكان مختلفان.
//
//     القاعدة واحدة: الزر لا يُفتح إلا لما اللي قبله يكتمل. مكانها هنا مرة
//     واحدة، مش مكرّرة في كل شاشة فيها خانة.
//
//  الاستخدام:
//     NuhCode.gate({ button: 'verifyOtpBtn',
//                    fields: [{ input: 'otpInput', length: 6, digits: true }] });
//
//     NuhCode.gate({ button: 'searchBtnNum', fields: [
//        { input: 'requestNumberInput', test: function (v) { return v.length > 0; } },
//        { input: 'last4Input', length: 4, digits: true }
//     ]});
// ==========================================================================
var NuhCode = (function () {

  function el(x) { return typeof x === 'string' ? document.getElementById(x) : x; }

  function fieldOk(f, node) {
    var v = (node.value || '').trim();
    if (f.digits) v = v.replace(/\D/g, '');
    if (typeof f.test === 'function') return !!f.test(v);
    if (f.length) return v.length === f.length;
    return v.length > 0;
  }

  // opts: button, fields[{input,length,digits,test}], onReady(bool)
  function gate(opts) {
    opts = opts || {};
    var btn = el(opts.button);
    var fields = (opts.fields || []).map(function (f) {
      return { cfg: f, node: el(f.input) };
    }).filter(function (f) { return f.node; });

    if (!btn || !fields.length) return null;

    function apply() {
      var ready = fields.every(function (f) { return fieldOk(f.cfg, f.node); });
      // ⚠️ الزر ممكن يكون معطّلًا لسبب تاني وقت التنفيذ («جاري التحقق...»).
      //    بنعلّم على السبب بتاعنا عشان ما نفتحوش ونحن في نص عملية.
      if (btn.dataset.busy === '1') return ready;
      btn.disabled = !ready;
      if (typeof opts.onReady === 'function') opts.onReady(ready);
      return ready;
    }

    fields.forEach(function (f) {
      // أرقام فقط: نصفّي أثناء الكتابة بدل ما نرفض بعد الضغط
      if (f.cfg.digits) {
        f.node.addEventListener('input', function () {
          var v = f.node.value.replace(/\D/g, '');
          if (f.node.value !== v) f.node.value = v;
        });
      }
      ['input', 'change', 'blur', 'paste'].forEach(function (ev) {
        f.node.addEventListener(ev, function () { setTimeout(apply, 0); });
      });
    });

    apply();
    return { refresh: apply };
  }

  // تُستدعى قبل/بعد العملية حتى لا تعيد البوابة فتح الزر أثناء التنفيذ
  function busy(button, on) {
    var b = el(button);
    if (!b) return;
    b.dataset.busy = on ? '1' : '0';
    b.disabled = !!on;
  }

  return { gate: gate, busy: busy };
})();
