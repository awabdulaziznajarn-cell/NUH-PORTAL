// ==========================================================================
//  idle-session.js — إنهاء الجلسة عند الخمول، مع تحذير وعدّاد تنازلي.
//
//  المشكلة التي يعالجها: فحص الخمول على الخادم لا يعمل إلا عند نداء الـ API.
//  فلو تُركت الصفحة مفتوحة بلا نشاط، لا يحدث نداء ولا يُنهي أحد الجلسة —
//  تبقى الشاشة مفتوحة أمام أي شخص يمر بالمكتب، ثم يفاجأ المستخدم برفض
//  مباشر عند أول ضغطة بعد عودته.
//
//  ملف مشترك بين شاشات الموظفين وبوابة الطالب — قاعدة واحدة لا نسختان.
//  المهلة نفسها تأتي من الخادم (Core/SessionPolicy.cs) فلا يفترق الرقمان.
// ==========================================================================
// ⚠️ ترجمة آمنة: الملف ده بيتحمّل في صفحات ممكن ما يكونش i18n.js فيها
//    (شاشات الموظفين اللي مابتحمّلوش الملف، وأي صفحة جديدة)، فلو t() مش
//    موجودة أو المفتاح ناقص بنرجع للنصّ المكتوب هنا.
//
//  ⚠️ والاحتياطي باللغتين لا بالعربي وحده: كان فيه قاموس عربي واحد، يعني
//     الصفحة اللي مافيهاش t() كانت بتوريّ نافذة عربية للمستخدم اللي واجهته
//     إنجليزي - ودي آخر نافذة بيشوفها قبل ما جلسته تتقفل.
var __IDLE_TX = {
  ar: {
    pt_idleTitle: 'جلستك على وشك الانتهاء',
    pt_idleText: 'لم يُسجَّل أي نشاط منذ فترة. سيتم إنهاء الجلسة تلقائيًا خلال',
    pt_idleStay: 'متابعة الجلسة',
    pt_idleLogout: 'تسجيل الخروج الآن',
    pt_idleOverTitle: 'انتهت الجلسة لعدم النشاط',
    pt_idleOverText: 'تم تسجيل خروجك تلقائيًا حفاظًا على أمان بياناتك.',
    pt_idleHome: 'العودة للبوابة',
    pt_backToPortal: 'العودة لبوابة الطلاب'
  },
  en: {
    pt_idleTitle: 'Your session is about to end',
    pt_idleText: 'No activity has been recorded for a while. The session will end automatically in',
    pt_idleStay: 'Stay signed in',
    pt_idleLogout: 'Sign out now',
    pt_idleOverTitle: 'Your session ended due to inactivity',
    pt_idleOverText: 'You were signed out automatically to keep your data secure.',
    pt_idleHome: 'Back to the portal',
    pt_backToPortal: 'Back to the student portal'
  }
};
function txLang() {
  try { return (document.documentElement.lang || 'ar').slice(0, 2) === 'en' ? 'en' : 'ar'; }
  catch (e) { return 'ar'; }
}
function tx(k) {
  if (typeof t === 'function') { var v = t(k); if (v && v !== k) return v; }
  var d = __IDLE_TX[txLang()] || __IDLE_TX.ar;
  return d[k] || __IDLE_TX.ar[k] || k;
}

var NuhIdle = (function () {

  var ACTIVITY_KEY = 'nuhLastActivity';   // مشترك بين التبويبات
  var cfg = null, timer = null, tickTimer = null, warning = false, deadline = 0;

  function now() { return Date.now(); }

  function readLast() {
    try {
      var v = parseInt(localStorage.getItem(ACTIVITY_KEY) || '0', 10);
      return isNaN(v) ? 0 : v;
    } catch (e) { return 0; }
  }

  function writeLast(t) { try { localStorage.setItem(ACTIVITY_KEY, String(t)); } catch (e) { } }

  // ---------------------------------------------------------------- الواجهة
  function ensureModal() {
    if (document.getElementById('nuhIdleOverlay')) return;

    var css = document.createElement('style');
    css.textContent =
      '#nuhIdleOverlay{position:fixed;inset:0;background:rgba(16,70,49,.55);z-index:99999;' +
        'display:none;align-items:center;justify-content:center;padding:20px;' +
        'font-family:"IBM Plex Sans Arabic",sans-serif}' +
      '#nuhIdleOverlay.show{display:flex}' +
      /* ⚠️ مافيش direction:rtl مثبّت هنا. كان مكتوب بالحرف، فالنافذة كانت
         بتطلع من اليمين لليسار على صفحة إنجليزية - الزرّ الأساسي («متابعة
         الجلسة») بيقع على اليمين وباقي الصفحة أزرارها الأساسية على الشمال.
         بلا القاعدة دي الصندوق بيورّث اتجاه <html> فيمشي مع الصفحة في
         اللغتين، وبيتغيّر معاها لحظة تبديل اللغة من غير إعادة بناء. */
      '#nuhIdleBox{background:#fff;border-radius:16px;max-width:420px;width:100%;padding:28px 26px;' +
        'text-align:center;box-shadow:0 20px 60px rgba(16,70,49,.3)}' +
      '#nuhIdleBox .ic{width:56px;height:56px;border-radius:50%;background:#fffaeb;color:var(--gold-dark);' +
        'display:flex;align-items:center;justify-content:center;margin:0 auto 14px}' +
      '#nuhIdleBox h3{font-size:17px;font-weight:800;color:#104631;margin-bottom:8px}' +
      '#nuhIdleBox p{font-size:13.5px;color:#333741;line-height:2;margin-bottom:6px}' +
      '#nuhIdleCount{display:block;font-size:30px;font-weight:800;color:#b42318;' +
        'direction:ltr;letter-spacing:2px;margin:10px 0 18px;font-variant-numeric:tabular-nums}' +
      '#nuhIdleBox .btns{display:flex;gap:10px}' +
      '#nuhIdleBox button{flex:1;padding:12px;border-radius:10px;border:none;cursor:pointer;' +
        'font-family:inherit;font-size:14px;font-weight:700;transition:all .2s}' +
      '#nuhIdleStay{background:#166a45;color:#fff}' +
      '#nuhIdleStay:hover{background:#25935f}' +
      '#nuhIdleOut{background:#f5f5f6;color:#333741;border:1.5px solid #dcdfe4!important}' +
      '#nuhIdleOut:hover{background:#eceded}';
    document.head.appendChild(css);

    var o = document.createElement('div');
    o.id = 'nuhIdleOverlay';
    o.innerHTML =
      '<div id="nuhIdleBox" role="alertdialog" aria-live="assertive">' +
        '<div class="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
          'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
          '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg></div>' +
        // ⚠️ النص من قاموس الـ resx — الشاشة دي بتظهر في بوابة الطالب كمان
        //    وهي بلغتين، فما ينفعش يكون مكتوب عربي هنا.
        // ⚠️ data-i18n مع النصّ لا بدله: النصّ بيتكتب دلوقتي عشان النافذة
        //    تبان صح فورًا، والسمة بتخلّي تبديل اللغة وهي مفتوحة يوصلها -
        //    قبل كده كانت بتفضل باللغة اللي اتبنت بيها.
        '<h3 data-i18n="pt_idleTitle">' + tx('pt_idleTitle') + '</h3>' +
        '<p data-i18n="pt_idleText">' + tx('pt_idleText') + '</p>' +
        '<span id="nuhIdleCount">2:00</span>' +
        '<div class="btns">' +
          '<button id="nuhIdleStay" type="button" data-i18n="pt_idleStay">' + tx('pt_idleStay') + '</button>' +
          '<button id="nuhIdleOut" type="button" data-i18n="pt_idleLogout">' + tx('pt_idleLogout') + '</button>' +
        '</div>' +
      '</div>';
    document.body.appendChild(o);

    document.getElementById('nuhIdleStay').addEventListener('click', stay);
    document.getElementById('nuhIdleOut').addEventListener('click', function () { logout(false); });
  }

  function paint(secs) {
    var el = document.getElementById('nuhIdleCount');
    if (!el) return;
    var m = Math.floor(secs / 60), s = secs % 60;
    el.textContent = m + ':' + (s < 10 ? '0' : '') + s;
  }

  // ---------------------------------------------------------------- السلوك
  function bump() {
    // ⚠️ بعد ظهور التحذير لا يُلغى بحركة عابرة — لا بد من ضغطة صريحة.
    //    لو ألغته حركة ماوس، اختفى التحذير دون أن ينتبه المستخدم فيظن
    //    جلسته سليمة، وهذا أسوأ من ظهوره.
    if (warning) return;
    writeLast(now());
  }

  function stay() {
    warning = false;
    document.getElementById('nuhIdleOverlay').classList.remove('show');
    writeLast(now());

    // ⚠️ تصفير عدّاد المتصفح وحده لا يكفي: الخادم ما زال يرى آخر نشاط قديمًا
    //    وسينهي الجلسة رغم أن المستخدم اختار المتابعة.
    try {
      fetch('/api/Auth/Ping', {
        method: 'POST',
        credentials: 'same-origin',
        headers: (typeof cfg.pingHeaders === 'function' ? cfg.pingHeaders() : (cfg.pingHeaders || {}))
      }).catch(function () { });
    } catch (e) { }
  }

  // ⚠️ homeLabel القديمة لسه مقبولة عشان مانكسرش أي صفحة فاتت، بس بنتجاهلها
  //    لو طلعت مفتاح خام (اللي كان بيحصل فعلًا) بدل ما نطبعه زي ما هو.
  function homeKey() {
    var k = cfg && cfg.homeLabelKey;
    if (k) return k;
    var lbl = cfg && cfg.homeLabel;
    if (lbl && !/^[a-z]{2,4}_[A-Za-z]/.test(lbl)) return lbl;   // نصّ حقيقي
    return 'pt_idleHome';
  }

  // ⚠️ التحويل الصامت يترك المستخدم أمام صفحة جديدة بلا تفسير — يعود بعد ساعة
  //    فيجد نفسه في مكان آخر ولا يعرف لماذا. الرسالة تبقى على الشاشة حتى يقرأها.
  function showExpired() {
    ensureModal();
    var box = document.getElementById('nuhIdleBox');
    box.innerHTML =
      '<div class="ic" style="background:#fef3f2;color:#b42318">' +
        '<svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ' +
        'stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/>' +
        '<line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg></div>' +
      '<h3 data-i18n="pt_idleOverTitle">' + tx('pt_idleOverTitle') + '</h3>' +
      '<p style="margin-bottom:18px" data-i18n="pt_idleOverText">' + tx('pt_idleOverText') + '</p>' +
      // ⚠️ اسم الزرّ بيتترجم **دلوقتي** لا وقت ما الصفحة نادت start().
      //    الصفحات الثابتة في البوابة بتنادي start() في <head> والقاموس
      //    بيوصل بـ fetch بعدها، فـ t() وقتها بترجّع المفتاح نفسه. النتيجة
      //    اللي كانت بتحصل فعلًا: الزرّ مكتوب عليه «pt_backToPortal» -
      //    مفتاح خام في وش الطالب على آخر شاشة قبل ما جلسته تنتهي.
      //    الحلّ نفس قاعدة باقي المكوّنات: بتاخد **مفتاح** وبتترجمه هي.
      '<div class="btns"><button id="nuhIdleHome" type="button" data-i18n="' + homeKey() + '" ' +
        'style="background:#166a45;color:#fff">' + tx(homeKey()) + '</button></div>';
    document.getElementById('nuhIdleOverlay').classList.add('show');
    document.getElementById('nuhIdleHome').addEventListener('click', function () {
      window.location.replace(cfg.homeUrl || '/');
    });
  }

  function logout(expired) {
    stopTimers();
    if (typeof cfg.onSessionEnd === 'function') cfg.onSessionEnd();   // تنظيف محلي

    // انتهاء بالخمول: نعرض السبب ونترك المستخدم يضغط. أما الخروج اليدوي
    // فيمضي مباشرة — هو يعرف أنه ضغط «تسجيل الخروج».
    if (expired && cfg.showExpiredPanel) { showExpired(); return; }
    if (typeof cfg.onLogout === 'function') { cfg.onLogout(expired); return; }

    // ⚠️ نموذج الخروج للخروج **اليدوي** وحده. الكوكي وقتها لسه صالح، فالطلب
    //    بيمرّ وبيتنفّذ الخروج فعلًا.
    //
    //    أما الانتهاء بالخمول فالكوكي بيكون انتهى قبلها بلحظة، فإرسال النموذج
    //    بيترفض ويتحوّل على:
    //        /Account/Login?ReturnUrl=%2FAccount%2FLogout
    //    والمستخدم بيدخل ببياناته، وبعد نجاح الدخول صفحة الجسر بتنفّذ الرابط
    //    ده كـ GET فيرد 405 - أو يخرّجه بمجرد ما دخل. وde كان بيحصل فعلًا في
    //    الإنتاج، وشكله عند المستخدم: «دخلت وطلعني، وتاني مرة دخل عادي».
    //
    //    ⚠️ الحماية مقفولة من الناحيتين: هنا مابنبعتش النموذج أصلًا، وفي
    //       AccountController.SafeReturnUrl مابنقبلش /Account/Logout كوجهة
    //       رجوع مهما وصلت منين. القفلة الواحدة كانت هتسيب الباب مفتوح لأي
    //       مسار تاني يوصّل نفس الرابط.
    if (!expired) {
      var f = document.querySelector('form[action="/Account/Logout"]');
      if (f) { f.submit(); return; }
    }
    window.location.href = '/Account/Login';
  }

  function stopTimers() {
    if (timer) { clearInterval(timer); timer = null; }
    if (tickTimer) { clearInterval(tickTimer); tickTimer = null; }
  }

  function check() {
    var idleMs = now() - readLast();
    var totalMs = cfg.minutes * 60 * 1000;
    var warnMs = cfg.warnSeconds * 1000;

    if (idleMs >= totalMs) { logout(true); return; }

    if (!warning && idleMs >= (totalMs - warnMs)) {
      warning = true;
      deadline = readLast() + totalMs;
      ensureModal();
      paint(Math.max(0, Math.round((deadline - now()) / 1000)));
      document.getElementById('nuhIdleOverlay').classList.add('show');
    }

    if (warning) {
      var left = Math.max(0, Math.round((deadline - now()) / 1000));
      paint(left);
      if (left <= 0) logout(true);
    }
  }

  // opts: minutes, warnSeconds, pingHeaders, onLogout
  function start(opts) {
    cfg = opts || {};
    cfg.minutes = cfg.minutes || 10;
    cfg.warnSeconds = cfg.warnSeconds || 120;

    writeLast(now());

    ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart', 'click'].forEach(function (ev) {
      document.addEventListener(ev, bump, { passive: true });
    });

    // نشاط في تبويب آخر يصفّر هذا التبويب — فلا يظهر تحذير وأنت تعمل بجواره
    window.addEventListener('storage', function (e) {
      if (e.key !== ACTIVITY_KEY || warning) return;
      // القيمة الجديدة تُقرأ في check تلقائيًا؛ هنا نغلق أي تحذير معلّق فقط
    });

    stopTimers();
    timer = setInterval(check, 1000);
  }

  return { start: start, stay: stay, ACTIVITY_KEY: ACTIVITY_KEY };
})();
