// ==========================================================================
//  dialog.js — نوافذ التأكيد والتنبيه والإدخال، بشكل النظام.
//
//  ⚠️ تحلّ محل confirm() / alert() / prompt() الأصلية في المتصفح. مشاكلها:
//     • شكلها من المتصفح لا من النظام — تظهر ملصقة بأعلى الصفحة ومكتوب فيها
//       اسم الموقع، فتبدو كتنبيه غريب لا كجزء من الشاشة.
//     • تُجمّد الصفحة بالكامل وتوقف كل الجافاسكريبت حتى تُغلَق.
//     • لا يمكن تمييز إجراء خطير (تعطيل حساب) عن سؤال عادي.
//     • على بعض المتصفحات يظهر خيار «امنع هذه الصفحة من إظهار نوافذ» فتتعطّل
//       كل التأكيدات بعدها بلا أي أثر ظاهر — وهذا خطر حقيقي في شاشات الإجراءات.
//
//  كلها تُرجع Promise، فالاستدعاء يبقى بنفس بساطة القديم:
//     if (!await NuhDialog.confirm({ message: '...' })) return;
// ==========================================================================
var NuhDialog = (function () {

  var OVERLAY_ID = 'nuhDialogOverlay';

  function esc(v) {
    return String(v == null ? '' : v)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function ensureStyles() {
    if (document.getElementById('nuhDialogStyles')) return;
    var css = document.createElement('style');
    css.id = 'nuhDialogStyles';
    css.textContent =
      '#' + OVERLAY_ID + '{position:fixed;inset:0;background:rgba(17,28,66,.55);z-index:100000;' +
        'display:none;align-items:center;justify-content:center;padding:20px;' +
        'font-family:"IBM Plex Sans Arabic",sans-serif;animation:nuhDlgFade .16s ease}' +
      '#' + OVERLAY_ID + '.show{display:flex}' +
      '@keyframes nuhDlgFade{from{opacity:0}to{opacity:1}}' +
      '@keyframes nuhDlgRise{from{opacity:0;transform:translateY(10px) scale(.98)}to{opacity:1;transform:none}}' +
      '.nuh-dlg{background:#fff;border-radius:16px;max-width:440px;width:100%;padding:26px;' +
        'box-shadow:0 24px 64px rgba(17,28,66,.28);direction:rtl;text-align:center;' +
        'animation:nuhDlgRise .18s ease}' +
      '.nuh-dlg .ic{width:56px;height:56px;border-radius:50%;display:flex;align-items:center;' +
        'justify-content:center;margin:0 auto 14px}' +
      '.nuh-dlg .ic.ask{background:#EEF3FF;color:#1B2A5E}' +
      '.nuh-dlg .ic.danger{background:#FEF2F2;color:#991B1B}' +
      '.nuh-dlg .ic.ok{background:#E1F5EE;color:#0F6E56}' +
      '.nuh-dlg .ic.warn{background:#FFF7E6;color:#B8860B}' +
      '.nuh-dlg h3{font-size:17px;font-weight:800;color:#111C42;margin:0 0 8px}' +
      '.nuh-dlg p{font-size:13.5px;color:#4A5270;line-height:2;margin:0 0 18px;' +
        'white-space:pre-wrap;word-break:break-word}' +
      '.nuh-dlg input{width:100%;height:46px;border:1.5px solid #DDE3F0;border-radius:10px;' +
        'padding:0 14px;margin-bottom:16px;font-family:inherit;font-size:14px;outline:none;' +
        'color:#111C42;background:#F4F6FB}' +
      '.nuh-dlg input:focus{border-color:#1B2A5E;background:#fff;box-shadow:0 0 0 3px rgba(27,42,94,.08)}' +
      '.nuh-dlg .btns{display:flex;gap:10px}' +
      '.nuh-dlg button{flex:1;padding:12px;border-radius:10px;border:none;cursor:pointer;' +
        'font-family:inherit;font-size:14px;font-weight:700;transition:all .18s}' +
      '.nuh-dlg .b-primary{background:#1B2A5E;color:#fff}' +
      '.nuh-dlg .b-primary:hover{background:#2B3E7E}' +
      '.nuh-dlg .b-danger{background:#991B1B;color:#fff}' +
      '.nuh-dlg .b-danger:hover{background:#7F1616}' +
      '.nuh-dlg .b-ghost{background:#F4F6FB;color:#4A5270;border:1.5px solid #DDE3F0}' +
      '.nuh-dlg .b-ghost:hover{background:#EEF1F8}';
    document.head.appendChild(css);
  }

  function close(overlay) {
    if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
  }

  // opts: kind(ask|danger|ok|warn) title message confirmLabel cancelLabel input inputType placeholder
  function open(opts) {
    ensureStyles();
    opts = opts || {};

    return new Promise(function (resolve) {
      var overlay = document.createElement('div');
      overlay.id = OVERLAY_ID;
      overlay.className = 'show';

      var kind = opts.kind || 'ask';
      var icons = {
        ask:    '<circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
        danger: '<path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
        ok:     '<path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/>',
        warn:   '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>'
      };

      var hasCancel = opts.cancelLabel !== null;
      var confirmClass = kind === 'danger' ? 'b-danger' : 'b-primary';

      overlay.innerHTML =
        '<div class="nuh-dlg" role="dialog" aria-modal="true">' +
          '<div class="ic ' + kind + '"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" ' +
            'stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
            (icons[kind] || icons.ask) + '</svg></div>' +
          (opts.title ? '<h3>' + esc(opts.title) + '</h3>' : '') +
          (opts.message ? '<p>' + esc(opts.message) + '</p>' : '') +
          (opts.input ? '<input type="' + esc(opts.inputType || 'text') + '" id="nuhDlgInput" ' +
                        'placeholder="' + esc(opts.placeholder || '') + '">' : '') +
          '<div class="btns">' +
            '<button type="button" class="' + confirmClass + '" id="nuhDlgOk">' +
              esc(opts.confirmLabel || 'تأكيد') + '</button>' +
            (hasCancel ? '<button type="button" class="b-ghost" id="nuhDlgCancel">' +
              esc(opts.cancelLabel || 'إلغاء') + '</button>' : '') +
          '</div>' +
        '</div>';

      document.body.appendChild(overlay);

      var input = document.getElementById('nuhDlgInput');
      var okBtn = document.getElementById('nuhDlgOk');

      function done(v) {
        document.removeEventListener('keydown', onKey);
        close(overlay);
        resolve(v);
      }
      function accept() { done(opts.input ? (input ? input.value : '') : true); }
      function reject() { done(opts.input ? null : false); }

      function onKey(e) {
        if (e.key === 'Escape' && hasCancel) reject();
        else if (e.key === 'Enter' && (opts.input || !hasCancel)) accept();
      }

      okBtn.addEventListener('click', accept);
      var cancelBtn = document.getElementById('nuhDlgCancel');
      if (cancelBtn) cancelBtn.addEventListener('click', reject);

      // الضغط خارج النافذة = إلغاء. لا يُطبَّق على الإجراءات الخطرة حتى لا
      // تُغلق بالخطأ ويظن المستخدم أن الإجراء نُفِّذ.
      if (hasCancel && kind !== 'danger') {
        overlay.addEventListener('click', function (e) { if (e.target === overlay) reject(); });
      }

      document.addEventListener('keydown', onKey);
      setTimeout(function () { (input || okBtn).focus(); }, 30);
    });
  }

  return {
    open: open,

    // بديل confirm() — يرجّع true/false
    confirm: function (o) {
      o = typeof o === 'string' ? { message: o } : (o || {});
      return open({
        kind: o.danger ? 'danger' : 'ask',
        title: o.title || (o.danger ? 'تأكيد الإجراء' : 'تأكيد'),
        message: o.message,
        confirmLabel: o.confirmLabel || 'تأكيد',
        cancelLabel: o.cancelLabel || 'إلغاء'
      });
    },

    // بديل alert() — زر واحد
    alert: function (o) {
      o = typeof o === 'string' ? { message: o } : (o || {});
      return open({
        kind: o.kind || 'warn',
        title: o.title || '',
        message: o.message,
        confirmLabel: o.confirmLabel || 'حسنًا',
        cancelLabel: null
      });
    },

    success: function (msg, title) {
      return open({ kind: 'ok', title: title || 'تمت العملية بنجاح', message: msg,
                    confirmLabel: 'حسنًا', cancelLabel: null });
    },

    error: function (msg, title) {
      return open({ kind: 'danger', title: title || 'تعذّر إتمام العملية', message: msg,
                    confirmLabel: 'حسنًا', cancelLabel: null });
    },

    // بديل prompt() — يرجّع النص أو null
    prompt: function (o) {
      o = typeof o === 'string' ? { message: o } : (o || {});
      return open({
        kind: 'ask', title: o.title || '', message: o.message,
        input: true, inputType: o.inputType || 'text', placeholder: o.placeholder || '',
        confirmLabel: o.confirmLabel || 'حفظ', cancelLabel: o.cancelLabel || 'إلغاء'
      });
    }
  };
})();
