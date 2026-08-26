// ==========================================================================
//  تذييل صفحات البوابة — المصدر الوحيد لشكله ونصّه.
//
//  ⚠️ ليه الملف ده موجود:
//     التذييل كان مبنيًّا خمس مرات بخمسة أشكال مختلفة: الصفحة الرئيسية
//     (شريط أخضر)، وبوابة الطالب (شريط أخضر + شعار + روابط)، وشاشة متابعة
//     الطلب (شريط فاتح رمادي بحدّ علوي)، وصفحة الدخول (نصّ ذهبي بلا شريط)،
//     وصفحة الصيانة (نسخة مستقلّة). وخمس صفحات في البوابة كانت بلا تذييل
//     إطلاقًا. فأي تعديل في الشكل كان يصل لصفحة ويترك أربعًا.
//
//  ⚠️ النصّ من مفتاح Brand_Copyright في Resources/*.resx — نفس المفتاح الذي
//     تقرأ منه القائمة الجانبية وصفحة الدخول. لا يُكتب النصّ في أي صفحة.
//
//  ⚠️ والتنسيق مُحقَن من هنا لا من portal.css: الشكل والسلوك في ملف واحد،
//     فلا تُنقل الصفحة إلى تذييل جديد وينسى أحدهم تنسيقه. نفس نمط
//     js/dialog.js في المشروع.
//
//  ⚠️ الاستثناء الوحيد: _maintenance/app_offline.htm. صفحة الصيانة يخدمها
//     IIS والتطبيق متوقّف، فلا يُحمَّل معها أي ملف خارجي — لا CSS ولا JS ولا
//     نقطة الترجمة. نسختها مضمّنة فيها بالضرورة، وفوقها تعليق يوضّح ذلك.
//
//  الاستعمال: <script src="/js/site-footer.js"></script> قبل </body>.
//  ولإظهار صفّ الروابط: <div data-nuh-footer="links"></div> في مكان التذييل.
// ==========================================================================
(function () {
  'use strict';

  var STYLE_ID = 'nuh-foot-style';
  var KEY = 'Brand_Copyright';

  // ⚠️ نصّ احتياطي واحد لكل الصفحات — يظهر فقط لو سقطت الشبكة قبل وصول
  //    القاموس من /api/ui/i18n. لازم يطابق قيمة Brand_Copyright في
  //    Resources/SharedResource.resx حرفًا بحرف، وإلا رجعنا لنصّين.
  var FALLBACK = 'جميع الحقوق محفوظة © جامعة نجران 2026\nتطوير عمادة التحول الرقمي ومصادر المعرفة';

  // ⚠️ صفّ الروابط لبوابة الطالب وحدها. الصفحة الرئيسية لا تعرضه: هي نفسها
  //    الفهرس، فروابط التذييل كانت تُودّي الزائر إلى المكان الذي هو فيه.
  var LINKS = [
    { href: 'https://www.nu.edu.sa', key: 'Brand_Sub', external: true },
    { href: 'index.html', key: 'pt_footHousing' },
    { href: 'track-request.html', key: 'pt_footTrack' }
  ];

  var CSS = [
    // margin-top:auto لا margin ثابت: body في portal.css عمود flex بارتفاع
    // 100vh على الأقل، فالتذييل يلتصق بأسفل الشاشة في الصفحات القصيرة
    // ويمشي مع المحتوى في الطويلة - بلا position:fixed وبلا حساب ارتفاع.
    '.nuh-foot{margin-top:auto;background:var(--navy-dark);color:rgba(255,255,255,.82);padding:22px 40px;text-align:center}',
    '.nuh-foot-links{display:flex;flex-wrap:wrap;justify-content:center;gap:6px 18px;margin-bottom:12px;font-size:12.5px;font-weight:600}',
    '.nuh-foot-links a{color:rgba(255,255,255,.72);transition:color .2s}',
    '.nuh-foot-links a:hover{color:var(--gold-light)}',
    '.nuh-foot-links a:focus-visible{outline:2px solid var(--gold);outline-offset:3px;border-radius:4px}',
    '.nuh-foot-line{width:44px;height:1px;background:rgba(255,255,255,.16);margin:0 auto 12px}',
    // white-space:pre-line لأن Brand_Copyright سطران في ملف الترجمة نفسه -
    // تقسيم السطر لا يُعمل هنا وإلا صار القرار في مكانين.
    '.nuh-foot-copy{font-size:13px;font-weight:600;line-height:1.8;white-space:pre-line}',
    '@media(max-width:820px){.nuh-foot{padding:18px 20px}.nuh-foot-copy{font-size:12px}}'
  ].join('');

  function injectStyle() {
    if (document.getElementById(STYLE_ID)) return;
    var st = document.createElement('style');
    st.id = STYLE_ID;
    st.textContent = CSS;
    document.head.appendChild(st);
  }

  // t() من i18n.js تُرجّع المفتاح نفسه لو لم يصل القاموس بعد - وقتها نعرض
  // النصّ الاحتياطي بدل أن يرى الزائر «Brand_Copyright» مكتوبة أمامه.
  function tr(key, fallback) {
    var v = (typeof t === 'function') ? t(key) : key;
    return (v && v !== key) ? v : (fallback || '');
  }

  function build(withLinks) {
    var foot = document.createElement('footer');
    foot.className = 'nuh-foot';

    if (withLinks) {
      var row = document.createElement('div');
      row.className = 'nuh-foot-links';
      LINKS.forEach(function (l, i) {
        if (i) {
          var dot = document.createElement('span');
          dot.textContent = '•';
          row.appendChild(dot);
        }
        var a = document.createElement('a');
        a.href = l.href;
        if (l.external) { a.target = '_blank'; a.rel = 'noopener'; }
        a.setAttribute('data-i18n', l.key);
        a.textContent = tr(l.key, '');
        row.appendChild(a);
      });
      foot.appendChild(row);

      var line = document.createElement('div');
      line.className = 'nuh-foot-line';
      foot.appendChild(line);
    }

    var copy = document.createElement('div');
    copy.className = 'nuh-foot-copy';
    // data-i18n عشان applyLang في i18n.js تحدّثه مع تبديل اللغة زي أي عنصر آخر
    copy.setAttribute('data-i18n', KEY);
    copy.textContent = tr(KEY, FALLBACK);
    foot.appendChild(copy);

    return foot;
  }

  function refresh(foot) {
    foot.querySelectorAll('[data-i18n]').forEach(function (el) {
      var k = el.getAttribute('data-i18n');
      var v = tr(k, '');
      if (v) el.textContent = v;
    });
  }

  function mount() {
    if (document.querySelector('.nuh-foot')) return;   // مركّب بالفعل
    injectStyle();

    var slot = document.querySelector('[data-nuh-footer]');
    var foot = build(!!(slot && slot.getAttribute('data-nuh-footer') === 'links'));

    if (slot) slot.parentNode.replaceChild(foot, slot);
    else document.body.appendChild(foot);

    // ⚠️ صفحات البوابة تجيب القاموس بـ fetch، فوقت تركيب التذييل قد يكون
    //    لم يصل بعد. __i18nReady من i18n.js تتحقّق عند وصوله - وفي شاشات
    //    الموظفين تتحقّق فورًا لأن القاموس محقون من اللياوت.
    if (window.__i18nReady && typeof window.__i18nReady.then === 'function') {
      window.__i18nReady.then(function () { refresh(foot); });
    }
  }

  if (document.body) mount();
  else document.addEventListener('DOMContentLoaded', mount);

  window.NuhFooter = { mount: mount };
})();
