(function() {
  var IDLE_TIMEOUT = 15 * 60 * 1000;
  var WARNING_TIME = 13 * 60 * 1000;
  var PING_DEBOUNCE = 30000;
  var timer = null;
  var warningTimer = null;
  var warningShown = false;
  var lastPing = 0;

  var style = document.createElement('style');
  style.textContent = '.session-overlay{position:fixed;inset:0;background:rgba(0,0,0,0.6);z-index:99999;display:none;align-items:center;justify-content:center;}.session-overlay.show{display:flex}.session-modal{background:#fff;border-radius:20px;padding:36px;max-width:400px;width:90%;text-align:center;box-shadow:0 25px 60px rgba(0,0,0,0.3)}.session-icon{width:56px;height:56px;margin:0 auto 16px;background:#FFF7ED;border-radius:50%;display:flex;align-items:center;justify-content:center}.session-icon svg{color:#92400E}.session-title{font-size:18px;font-weight:800;color:var(--navy-dark);margin-bottom:12px}.session-message{font-size:14px;color:var(--gray-700);margin-bottom:24px;line-height:1.6}.session-actions{display:flex;gap:12px;justify-content:center}';
  document.head.appendChild(style);

  var modal = document.createElement('div');
  modal.className = 'session-overlay';
  modal.id = 'session-overlay';
  modal.innerHTML = '<div class="session-modal">' +
    '<div class="session-icon"><svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg></div>' +
    '<div class="session-title" data-ar="تنبيه انتهاء الجلسة" data-en="Session Expiry Warning">تنبيه انتهاء الجلسة</div>' +
    '<div class="session-message" data-ar="ستنتهي الجلسة خلال دقيقتين بسبب عدم النشاط" data-en="Your session will expire in 2 minutes due to inactivity">ستنتهي الجلسة خلال دقيقتين بسبب عدم النشاط</div>' +
    '<div class="session-actions">' +
      '<button class="btn btn-primary" onclick="continueSession()" data-ar="متابعة العمل" data-en="Continue">متابعة العمل</button>' +
      '<button class="btn btn-cancel" onclick="forceLogout()" data-ar="تسجيل الخروج" data-en="Logout">تسجيل الخروج</button>' +
    '</div>' +
  '</div>';
  document.body.appendChild(modal);

  function getAuthToken() {
    return localStorage.getItem('staffToken') || localStorage.getItem('studentToken');
  }

  function clearAuth() {
    localStorage.removeItem('staffToken');
    localStorage.removeItem('studentToken');
    localStorage.removeItem('staffUser');
    localStorage.removeItem('studentUser');
  }

  function pingServer() {
    var now = Date.now();
    if (now - lastPing < PING_DEBOUNCE) return;
    lastPing = now;
    var token = getAuthToken();
    if (!token) return;
    fetch('/api/Auth/Ping', {
      method: 'POST',
      headers: { 'Authorization': 'Bearer ' + token }
    }).then(function(r) {
      if (r.status === 401) forceLogout();
    }).catch(function() {});
  }

  function resetTimers() {
    if (warningShown) {
      warningShown = false;
      document.getElementById('session-overlay').classList.remove('show');
    }
    clearTimeout(timer);
    clearTimeout(warningTimer);
    timer = setTimeout(forceLogout, IDLE_TIMEOUT);
    warningTimer = setTimeout(showWarning, WARNING_TIME);
    pingServer();
  }

  function showWarning() {
    if (warningShown) return;
    warningShown = true;
    document.getElementById('session-overlay').classList.add('show');
  }

  var events = ['mousemove', 'mousedown', 'click', 'keydown', 'scroll', 'touchstart'];
  events.forEach(function(evt) {
    document.addEventListener(evt, resetTimers, { passive: true });
  });

  window.continueSession = resetTimers;
  var forceLogout = function() {
    fetch('/api/Auth/Logout', {
      method: 'POST',
      headers: { 'Authorization': 'Bearer ' + getAuthToken() }
    }).catch(function() {}).then(function() {
      clearAuth();
      window.location.replace('login.html');
    });
  };
  window.forceLogout = forceLogout;

  resetTimers();
})();
