document.addEventListener('DOMContentLoaded', function () {
  updateSidebar();
  applyRolePermissions();
  updateUserInfo();
});

function updateSidebar() {
  var currentPath = window.location.pathname.split('/').pop() || 'dashboard.html';
  var navItems = document.querySelectorAll('.sidebar-nav .nav-item');
  var matched = false;
  navItems.forEach(function (item) {
    var href = item.getAttribute('href');
    if (href) {
      var page = href.split('/').pop();
      if (page === currentPath || (currentPath === 'request-details.html' && page === 'requests.html')) {
        item.classList.add('active');
        matched = true;
      } else {
        item.classList.remove('active');
      }
    }
  });
}

function updateUserInfo() {
  var userData = JSON.parse(localStorage.getItem('staffUser') || localStorage.getItem('studentUser') || '{}');
  var nameEl = document.getElementById('sidebar-user-name');
  var roleEl = document.getElementById('sidebar-user-role');
  if (nameEl) nameEl.textContent = userData.full_name || userData.username || 'مستخدم';
  if (roleEl) roleEl.textContent = (userData.role || '').toUpperCase();
}

function applyRolePermissions() {
  var userData = JSON.parse(localStorage.getItem('staffUser') || localStorage.getItem('studentUser') || '{}');
  var role = (userData.role || '').toLowerCase();
  var tabsToRemove = [];
  if (role === 'user') {
    tabsToRemove = ['workflowQueueTab'];
  } else if (role === 'supervisor') {
    tabsToRemove = ['auditTab', 'registerPhoneTab', 'myRequestsTab', 'housingManagementTab'];
  } else if (role === 'cyber') {
    tabsToRemove = ['registerStudentTab', 'registerBulkTab', 'bulkRegistrationTab', 'housingManagementTab', 'registerPhoneTab', 'myRequestsTab'];
  }
  tabsToRemove.forEach(function(id) {
    var el = document.getElementById(id);
    if (el) el.remove();
  });
}
