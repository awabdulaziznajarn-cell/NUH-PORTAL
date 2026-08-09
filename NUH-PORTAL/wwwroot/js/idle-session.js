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
      '#nuhIdleOverlay{position:fixed;inset:0;background:rgba(17,28,66,.55);z-index:99999;' +
        'display:none;align-items:center;justify-content:center;padding:20px;' +
        'font-family:"IBM Plex Sans Arabic",sans-serif}' +
      '#nuhIdleOverlay.show{display:flex}' +
      '#nuhIdleBox{background:#fff;border-radius:16px;max-width:420px;width:100%;padding:28px 26px;' +
        'text-align:center;box-shadow:0 20px 60px rgba(17,28,66,.3);direction:rtl}' +
      '#nuhIdleBox .ic{width:56px;height:56px;border-radius:50%;background:#FFF7E6;color:#B8860B;' +
        'display:flex;align-items:center;justify-content:center;margin:0 auto 14px}' +
      '#nuhIdleBox h3{font-size:17px;font-weight:800;color:#111C42;margin-bottom:8px}' +
      '#nuhIdleBox p{font-size:13.5px;color:#4A5270;line-height:2;margin-bottom:6px}' +
      '#nuhIdleCount{display:block;font-size:30px;font-weight:800;color:#991B1B;' +
        'direction:ltr;letter-spacing:2px;margin:10px 0 18px;font-variant-numeric:tabular-nums}' +
      '#nuhIdleBox .btns{display:flex;gap:10px}' +
      '#nuhIdleBox button{flex:1;padding:12px;border-radius:10px;border:none;cursor:pointer;' +
        'font-family:inherit;font-size:14px;font-weight:700;transition:all .2s}' +
      '#nuhIdleStay{background:#1B2A5E;color:#fff}' +
      '#nuhIdleStay:hover{background:#2B3E7E}' +
      '#nuhIdleOut{background:#F4F6FB;color:#4A5270;border:1.5px solid #DDE3F0!important}' +
      '#nuhIdleOut:hover{background:#EEF1F8}';
    document.head.appendChild(css);

    var o = document.createElement('div');
    o.id = 'nuhIdleOverlay';
    o.innerHTML =
      '<div id="nuhIdleBox" role="alertdialog" aria-live="assertive">' +
        '<div class="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
          'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
          '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg></div>' +
        '<h3>جلستك على وشك الانتهاء</h3>' +
        '<p>لم يُسجَّل أي نشاط منذ فترة. سيتم إنهاء الجلسة تلقائيًا خلال</p>' +
        '<span id="nuhIdleCount">2:00</span>' +
        '<div class="btns">' +
          '<button id="nuhIdleStay" type="button">متابعة الجلسة</button>' +
          '<button id="nuhIdleOut" type="button">تسجيل الخروج الآن</button>' +
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

  // ⚠️ التحويل الصامت يترك المستخدم أمام صفحة جديدة بلا تفسير — يعود بعد ساعة
  //    فيجد نفسه في مكان آخر ولا يعرف لماذا. الرسالة تبقى على الشاشة حتى يقرأها.
  function showExpired() {
    ensureModal();
    var box = document.getElementById('nuhIdleBox');
    box.innerHTML =
      '<div class="ic" style="background:#FEF2F2;color:#991B1B">' +
        '<svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" ' +
        'stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/>' +
        '<line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg></div>' +
      '<h3>انتهت الجلسة لعدم النشاط</h3>' +
      '<p style="margin-bottom:18px">تم تسجيل خروجك تلقائيًا حفاظًا على أمان بياناتك.</p>' +
      '<div class="btns"><button id="nuhIdleHome" type="button" ' +
        'style="background:#1B2A5E;color:#fff">' + (cfg.homeLabel || 'العودة للبوابة') + '</button></div>';
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

    // موظف: إرسال نموذج الخروج الموجود في القائمة الجانبية (معه رمز مكافحة التزوير)
    var f = document.querySelector('form[action="/Account/Logout"]');
    if (f) { f.submit(); return; }
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
