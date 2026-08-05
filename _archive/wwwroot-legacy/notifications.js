function getToken() { return localStorage.getItem('staffToken') || localStorage.getItem('studentToken'); }
function getUser() { try { return JSON.parse(localStorage.getItem('staffUser') || localStorage.getItem('studentUser') || '{}'); } catch(e) { return {}; } }
function getLang() { return (document.getElementById('html-root')||document.documentElement).getAttribute('lang')||'ar'; }

async function fetchNotifs(token,role) {
  try {
    var res = await fetch('/api/notifications?role='+encodeURIComponent(role), {headers:{'Authorization':'Bearer '+token}});
    if (res.status===401) { localStorage.removeItem('staffToken'); localStorage.removeItem('studentToken'); localStorage.removeItem('staffUser'); localStorage.removeItem('studentUser'); localStorage.removeItem('token'); localStorage.removeItem('user'); window.location.replace('login.html'); return null; }
    return await res.json();
  } catch(e) { return null; }
}

async function loadNotifDropdown() {
  var token = getToken();
  var user = getUser();
  var role = user.role || 'admin';
  var data = await fetchNotifs(token, role);
  var list = document.getElementById('notif-dropdown-list');
  if (!data) { list.innerHTML='<div class="notif-empty">'+(getLang()==='ar'?'تعذر الاتصال':'Connection error')+'</div>'; return; }
  list.innerHTML = data.length ? data.map(function(n){
    return '<div class="notif-item" onclick="notifClick(this)" data-id="'+n.id+'" data-request-id="'+(n.request_id||'')+'">'+
      '<div class="notif-item-dot" style="background:'+(n.status==='pending'?'#1B2A5E':'#CBD5E1')+';"></div>'+
      '<div><div class="notif-item-text">'+escHtml(n.message)+'</div>'+
      '<div class="notif-item-time">'+new Date(n.sent_at||Date.now()).toLocaleDateString(getLang()==='ar'?'ar-SA':'en-US')+'</div></div></div>';
  }).join('') : '<div class="notif-empty">'+(getLang()==='ar'?'لا توجد إشعارات':'No notifications')+'</div>';
}

async function loadUnreadCount() {
  try {
    var token = getToken();
    var user = getUser();
    var role = user.role || 'admin';
    var res = await fetch('/api/notifications/unread-count?role='+encodeURIComponent(role), {headers:{'Authorization':'Bearer '+token}});
    if (res.status===401) { localStorage.removeItem('token'); localStorage.removeItem('user'); window.location.replace('login.html'); return; }
    var d = await res.json();
    var badge = document.getElementById('notif-badge');
    var dot = document.querySelector('.notif-dot');
    if (d&&d.count>0) { badge.textContent=d.count; badge.classList.add('show'); if(dot)dot.classList.remove('hidden'); }
    else { badge.classList.remove('show'); if(dot)dot.classList.add('hidden'); }
  } catch(e) {}
}

function toggleNotifDropdown(e) {
  e.stopPropagation();
  var dd = document.getElementById('notif-dropdown');
  var isOpen = dd.classList.contains('open');
  document.querySelectorAll('.notif-dropdown.open').forEach(function(d) {
    d.classList.remove('open'); d.style.top=''; d.style.left='';
  });
  if (!isOpen) {
    dd.classList.add('open');
    if (!dd.dataset.loaded) {
      loadNotifDropdown();
      dd.dataset.loaded = '1';
    }
    var btn = e.currentTarget;
    var rect = btn.getBoundingClientRect();
    var ddWidth = 400;
    var top = rect.bottom + 8;
    var left = rect.right - ddWidth;
    if (left < 8) left = Math.max(8, rect.left);
    if (left + ddWidth > window.innerWidth - 8) left = window.innerWidth - ddWidth - 8;
    if (left < 8) left = 8;
    var spaceBelow = window.innerHeight - top;
    if (spaceBelow < 80) {
      var spaceAbove = rect.top - 8;
      if (spaceAbove > 80) top = rect.top - 8 - Math.min(480, spaceAbove);
    }
    dd.style.top = top + 'px';
    dd.style.left = left + 'px';
  }
}

async function markNotifRead(id,el) {
  try {
    var token = getToken();
    await fetch('/api/notifications/read',{method:'PATCH',headers:{'Authorization':'Bearer '+token,'Content-Type':'application/json'},body:JSON.stringify([id])});
    var dot = el.querySelector('.notif-item-dot');
    if (dot) dot.style.background='#CBD5E1';
    loadUnreadCount();
  } catch(e) {}
}

function notifClick(el) {
  var nid = el.dataset.id;
  var rid = el.dataset.requestId;
  markNotifRead(nid, el);
  if (rid) { window.location.href = 'request-details.html?id=' + rid; }
  else { var lang = getLang(); alert(lang === 'ar' ? 'الطلب لم يعد متاحاً' : 'Request is no longer available'); }
}

async function markAllNotifRead() {
  var items = document.querySelectorAll('#notif-dropdown-list .notif-item[data-id]'), ids = [];
  items.forEach(function(el){ ids.push(parseInt(el.dataset.id)); });
  if (!ids.length) return;
  try {
    var token = getToken();
    await fetch('/api/notifications/read',{method:'PATCH',headers:{'Authorization':'Bearer '+token,'Content-Type':'application/json'},body:JSON.stringify(ids)});
    document.querySelectorAll('#notif-dropdown-list .notif-item-dot').forEach(function(d){ d.style.background='#CBD5E1'; });
    loadUnreadCount();
  } catch(e) {}
}

document.addEventListener('click', function(e) {
  if (!e.target.closest('.notif-wrapper') && !e.target.closest('.notif-dropdown')) {
    document.querySelectorAll('.notif-dropdown.open').forEach(function(d) {
      d.classList.remove('open'); d.style.top=''; d.style.left='';
    });
  }
});
