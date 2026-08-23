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
//
//  opts.rows = [{label, value}] — صندوق بيانات المراجعة تحت النصّ.
//
//  ⚠️ نصوص الأزرار والعناوين من Resources/*.resx زي أي نصّ تاني في النظام،
//     مش مكتوبة هنا. الشاشة اللي بتفتح الحوار مابتبعتش labels إلا لو عايزة
//     كلمة مخصوصة («حذف» بدل «تأكيد») - والافتراضي بيترجم لوحده.
// ==========================================================================
var NuhDialog = (function () {

  var OVERLAY_ID = 'nuhDialogOverlay';

  // تهريب HTML — التعريف الوحيد في /js/esc.js
  function esc(v) { return escHtml(v); }

  function lang() {
    var root = document.getElementById('html-root') || document.documentElement;
    return (root && root.getAttribute('lang') === 'en') ? 'en' : 'ar';
  }

  // ⚠️ الحوار متحمَّل من اللياوت لكل الشاشات، وفي شاشة أو اتنين ممكن i18n.js
  //    ما يكونش اتحمّل. الفولباك هنا شبكة أمان مش مصدر تاني: المفتاح هو الأصل.
  function dt(key, ar, en) {
    if (typeof tf === 'function') return tf(key, ar, en);
    return lang() === 'en' ? en : ar;
  }

  function ensureStyles() {
    if (document.getElementById('nuhDialogStyles')) return;
    var css = document.createElement('style');
    css.id = 'nuhDialogStyles';
    css.textContent =
      '#' + OVERLAY_ID + '{position:fixed;inset:0;background:rgba(16,70,49,.55);z-index:100000;' +
        'display:none;align-items:center;justify-content:center;padding:20px;' +
        'font-family:"IBM Plex Sans Arabic",sans-serif;animation:nuhDlgFade .16s ease}' +
      '#' + OVERLAY_ID + '.show{display:flex}' +
      '@keyframes nuhDlgFade{from{opacity:0}to{opacity:1}}' +
      '@keyframes nuhDlgRise{from{opacity:0;transform:translateY(10px) scale(.98)}to{opacity:1;transform:none}}' +
      /* ⚠️ الاتجاه من سمة dir على العنصر لا من CSS متيّبة: النظام بيشتغل
         عربي وإنجليزي، وحوار مقلوب في الإنجليزي بيبان كأنه حاجة تانية. */
      '.nuh-dlg{background:#fff;border-radius:16px;max-width:440px;width:100%;padding:26px;' +
        'box-shadow:0 24px 64px rgba(16,70,49,.28);text-align:center;' +
        'animation:nuhDlgRise .18s ease}' +
      '.nuh-dlg .ic{width:56px;height:56px;border-radius:50%;display:flex;align-items:center;' +
        'justify-content:center;margin:0 auto 14px}' +
      '.nuh-dlg .ic.ask{background:#eff4ff;color:#166a45}' +
      '.nuh-dlg .ic.danger{background:#fef3f2;color:#b42318}' +
      '.nuh-dlg .ic.ok{background:#dff6e7;color:#067647}' +
      '.nuh-dlg .ic.warn{background:#fffaeb;color:var(--gold-dark)}' +
      '.nuh-dlg h3{font-size:17px;font-weight:800;color:#104631;margin:0 0 8px}' +
      '.nuh-dlg p{font-size:13.5px;color:#333741;line-height:2;margin:0 0 18px;' +
        'white-space:pre-wrap;word-break:break-word}' +
      '.nuh-dlg input{width:100%;height:46px;border:1.5px solid #dcdfe4;border-radius:10px;' +
        'padding:0 14px;margin-bottom:16px;font-family:inherit;font-size:14px;outline:none;' +
        'color:#104631;background:#f5f5f6}' +
      '.nuh-dlg input:focus{border-color:#166a45;background:#fff;box-shadow:0 0 0 3px rgba(16,70,49,.08)}' +
      /* ⚠️ صندوق البيانات: القيم اللي المستخدم بيراجعها قبل ما يأكّد (اسم
         المستخدم، السكن القديم والجديد…). كانت الشاشات بتكتبها سطورًا بخطوط
         فاصلة جوّه نصّ الحوار، فبتتقري كأنها جزء من الكلام. الصندوق بيفرزها
         بصريًا: النصّ بيقول «هيحصل إيه» والصندوق بيقول «على إيه». */
      '.nuh-dlg .rows{text-align:start;background:#f9fafb;border:1px solid #dcdfe4;' +
        'border-radius:10px;padding:10px 14px;margin:0 0 16px}' +
      '.nuh-dlg .rows > div{display:flex;gap:8px;font-size:13px;padding:3px 0}' +
      '.nuh-dlg .rows b{color:#85888e;font-weight:600;flex-shrink:0}' +
      '.nuh-dlg .rows span{color:#104631;font-weight:700;word-break:break-word}' +
      '.nuh-dlg .btns{display:flex;gap:10px}' +
      '.nuh-dlg button{flex:1;padding:12px;border-radius:10px;border:none;cursor:pointer;' +
        'font-family:inherit;font-size:14px;font-weight:700;transition:all .18s}' +
      '.nuh-dlg .b-primary{background:#166a45;color:#fff}' +
      '.nuh-dlg .b-primary:hover{background:#25935f}' +
      '.nuh-dlg .b-danger{background:#b42318;color:#fff}' +
      '.nuh-dlg .b-danger:hover{background:#912018}' +
      '.nuh-dlg .b-ghost{background:#f5f5f6;color:#333741;border:1.5px solid #dcdfe4}' +
      '.nuh-dlg .b-ghost:hover{background:#eceded}';
    document.head.appendChild(css);
  }

  // rows: [{ label, value }] — الصفوف اللي قيمتها فاضية بتتشال، فالشاشة
  // مابتحتاجش تخفي صفًّا بإيدها زي ما كانت بتعمل مع الماركب المبني بالإيد.
  function rowsHtml(rows) {
    if (!rows || !rows.length) return '';
    var body = rows.filter(function (r) {
      return r && r.label && r.value != null && String(r.value).trim() !== '' && String(r.value).trim() !== '--';
    }).map(function (r) {
      return '<div><b>' + esc(r.label) + '</b><span>' + esc(r.value) + '</span></div>';
    }).join('');
    return body ? '<div class="rows">' + body + '</div>' : '';
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

      // ⚠️ اسم الحوار للقارئ الصوتي من العنوان والنصّ الفعليين: role="dialog"
      //    من غير اسم بيتقري «مربع حوار» وبس.
      overlay.innerHTML =
        '<div class="nuh-dlg" role="dialog" aria-modal="true" dir="' + (lang() === 'en' ? 'ltr' : 'rtl') + '"' +
          (opts.title ? ' aria-labelledby="nuhDlgTitle"' : '') +
          (opts.message ? ' aria-describedby="nuhDlgMsg"' : '') + '>' +
          '<div class="ic ' + kind + '"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" ' +
            'stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" ' +
            'aria-hidden="true" focusable="false">' +
            (icons[kind] || icons.ask) + '</svg></div>' +
          (opts.title ? '<h3 id="nuhDlgTitle">' + esc(opts.title) + '</h3>' : '') +
          (opts.message ? '<p id="nuhDlgMsg">' + esc(opts.message) + '</p>' : '') +
          rowsHtml(opts.rows) +
          (opts.input ? '<input type="' + esc(opts.inputType || 'text') + '" id="nuhDlgInput" ' +
                        'placeholder="' + esc(opts.placeholder || '') + '">' : '') +
          '<div class="btns">' +
            '<button type="button" class="' + confirmClass + '" id="nuhDlgOk">' +
              esc(opts.confirmLabel || dt('dlg_confirm', 'تأكيد', 'Confirm')) + '</button>' +
            (hasCancel ? '<button type="button" class="b-ghost" id="nuhDlgCancel">' +
              esc(opts.cancelLabel || dt('dlg_cancel', 'إلغاء', 'Cancel')) + '</button>' : '') +
          '</div>' +
        '</div>';

      document.body.appendChild(overlay);

      var input = document.getElementById('nuhDlgInput');
      var okBtn = document.getElementById('nuhDlgOk');

      // ⚠️ العنصر اللي كان عليه التركيز قبل الفتح بيرجع له بعد الإغلاق:
      //    من غير كده التركيز بيرجع لأول الصفحة وبيضيع مكان الصف اللي كان
      //    الموظف واقف عليه في الجدول.
      var prevFocus = document.activeElement;

      function done(v) {
        document.removeEventListener('keydown', onKey);
        close(overlay);
        try { if (prevFocus && prevFocus.focus) prevFocus.focus(); } catch (e) { }
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
        title: o.title || (o.danger ? dt('dlg_titleDanger', 'تأكيد الإجراء', 'Confirm action')
                                    : dt('dlg_titleConfirm', 'تأكيد', 'Confirm')),
        message: o.message,
        rows: o.rows,
        confirmLabel: o.confirmLabel || dt('dlg_confirm', 'تأكيد', 'Confirm'),
        cancelLabel: o.cancelLabel || dt('dlg_cancel', 'إلغاء', 'Cancel')
      });
    },

    // بديل alert() — زر واحد
    alert: function (o) {
      o = typeof o === 'string' ? { message: o } : (o || {});
      return open({
        kind: o.kind || 'warn',
        title: o.title || '',
        message: o.message,
        confirmLabel: o.confirmLabel || dt('dlg_ok', 'حسنًا', 'OK'),
        cancelLabel: null
      });
    },

    success: function (msg, title) {
      return open({ kind: 'ok', title: title || dt('dlg_titleSuccess', 'تمت العملية بنجاح', 'Done'),
                    message: msg, confirmLabel: dt('dlg_ok', 'حسنًا', 'OK'), cancelLabel: null });
    },

    error: function (msg, title) {
      return open({ kind: 'danger', title: title || dt('dlg_titleError', 'تعذّر إتمام العملية', 'Action failed'),
                    message: msg, confirmLabel: dt('dlg_ok', 'حسنًا', 'OK'), cancelLabel: null });
    },

    // بديل prompt() — يرجّع النص أو null
    prompt: function (o) {
      o = typeof o === 'string' ? { message: o } : (o || {});
      return open({
        kind: 'ask', title: o.title || '', message: o.message,
        input: true, inputType: o.inputType || 'text', placeholder: o.placeholder || '',
        confirmLabel: o.confirmLabel || dt('dlg_save', 'حفظ', 'Save'),
        cancelLabel: o.cancelLabel || dt('dlg_cancel', 'إلغاء', 'Cancel')
      });
    }
  };
})();
