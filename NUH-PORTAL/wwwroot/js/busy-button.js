// ==========================================================================
//  NuhBusy - قفل الزر أثناء تنفيذ نداء له أثر.
//
//  ⚠️ المشكلة: الزر الذي يبدأ عملية على الخادم يبقى قابلًا للضغط حتى يرجع
//     الرد. ونداءات الأكتف دايركتوري - إنشاء حساب، تعطيله، إعادة ضبطه -
//     تستغرق ثوانيَ لا أجزاء من الثانية، فالضغطة الثانية طبيعية جدًّا من
//     مستخدم ظنّ أن شيئًا لم يحدث. والنتيجة عمليتان على الحساب نفسه.
//
//  ⚠️ الخادم محمي أصلًا ببوابة الطلب (Core/RequestGate) وبفحوص التكرار في
//     الخدمات. وهذا خط الدفاع الأول: يمنع الضغطة قبل أن تغادر المتصفح -
//     فلا يرى المستخدم رسالة خطأ على شيء لم يخطئ فيه.
//
//  ⚠️ وهو هنا في ملف واحد لا في كل شاشة: كان الزر يُقفل بسطرين مكتوبين
//     بالإيد في شاشة مراجعة الطلب، وبلا شيء إطلاقًا في شاشة حسابات السكن
//     وشاشة المستخدمين. فالسلوك يختلف من زر لآخر أمام المستخدم نفسه.
//
//  الاستعمال:
//      onclick="performAction(this, 5, 'disable')"   ثم داخل الدالة:
//  أو من داخل الدالة نفسها:
//      return NuhBusy.run(btn, async function () { ... });
//
//  والقيمة المرجَعة وعدٌ يتحقق بعد انتهاء الدالة وفكّ القفل.
// ==========================================================================
var NuhBusy = (function () {
  'use strict';

  var STYLE_ID = 'nuh-busy-style';
  var CSS =
    // ⚠️ لا إخفاء للنصّ ولا استبداله بمؤشّر دوران: الزر يحمل أيقونة SVG
    //    ونصًّا معًا في أكثر شاشات النظام، واستبدال المحتوى يهدم الأيقونة
    //    ويغيّر عرض الزر فتقفز البطاقة كلها. الخفوت و cursor:progress
    //    يوصّلان الرسالة بلا لمس التخطيط.
    '.nuh-busy{opacity:.55;cursor:progress !important;pointer-events:none}';

  function injectStyle() {
    if (document.getElementById(STYLE_ID)) return;
    var st = document.createElement('style');
    st.id = STYLE_ID;
    st.textContent = CSS;
    document.head.appendChild(st);
  }

  // يقبل: العنصر نفسه، أو معرّفه، أو حدث الضغط (فنأخذ الزر منه).
  function resolve(x) {
    if (!x) return null;
    if (typeof x === 'string') return document.getElementById(x);
    if (x.currentTarget) return x.currentTarget;
    if (x.nodeType === 1) return x;
    return null;
  }

  function lock(btn) {
    injectStyle();
    btn.disabled = true;
    btn.setAttribute('aria-busy', 'true');
    btn.classList.add('nuh-busy');
  }

  function unlock(btn) {
    // ⚠️ الفحص على isConnected: كثير من هذه الأزرار داخل نوافذ تُغلق عند
    //    النجاح، فالزر يكون قد أُزيل من الصفحة. لمس عنصر مُزال لا يرمي،
    //    لكن الفحص يوضّح أن الحالة متوقّعة لا سهو.
    if (!btn || !btn.isConnected) return;
    btn.disabled = false;
    btn.removeAttribute('aria-busy');
    btn.classList.remove('nuh-busy');
  }

  // ⚠️ الضغطة الثانية تُهمَل بصمت لا برسالة: المستخدم ضغط مرتين على الشيء
  //    نفسه، وهو يريد نتيجة واحدة. رسالة «انتظر» هنا ضجيج.
  function run(el, fn) {
    var btn = resolve(el);
    if (!btn) return Promise.resolve().then(fn);   // بلا زر - ننفّذ بلا قفل
    if (btn.disabled) return Promise.resolve();    // نداء جارٍ على هذا الزر

    lock(btn);
    return Promise.resolve()
      .then(fn)
      .then(
        function (v) { unlock(btn); return v; },
        function (e) { unlock(btn); throw e; }
      );
  }

  return { run: run, lock: lock, unlock: unlock };
})();
