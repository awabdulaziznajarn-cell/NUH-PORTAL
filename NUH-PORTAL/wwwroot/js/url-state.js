// ==========================================================================
//  url-state.js — حالة الشاشة في الرابط. التعريف الوحيد في النظام.
//
//  ⚠️ ليه الملف ده موجود:
//     الموظف بيفتح تبويب «مكتمل»، ويكتب في البحث، ويروح صفحة ٣، وبعدين
//     بيدوس على طلب عشان يشوف تفاصيله. أول ما يرجع بيلاقي نفسه في «يحتاج
//     إجراءك» على صفحة ١ بلا بحث - وكل اللي عمله راح، فيعيده من الأول.
//
//     السبب إن الحالة دي كانت عايشة في متغيّر في الذاكرة بس. والانتقال
//     لصفحة التفاصيل انتقال حقيقي، فالمتغيّر بيموت مع الصفحة، والرجوع
//     بيحمّل الشاشة من الصفر بقيمها الافتراضية.
//
//  ⚠️ ليه الرابط لا localStorage:
//     • الرجوع بيرجّع الرابط بحالته، فالشاشة بتتبني من نفس اللي كانت عليه
//       بلا أي كود إضافي.
//     • الرابط يتبعت: «بص على دول» بيبقى لينك، مش شرح شفهي لخطوات الفلترة.
//     • التخزين المحلي بيتسرّب: تبويب تاني في نفس المتصفح بياخد فلتر
//       التبويب الأول من غير ما حد يطلب، والرابط اللي تبعته يفتح عند غيرك
//       بفلاتره هو لا بفلاترك.
//
//  ⚠️ replaceState لا pushState:
//     pushState بيحطّ مدخل في تاريخ المتصفح مع **كل** ضغطة فلتر، فالرجوع
//     يفضل يتنقّل بين الفلاتر بدل ما يخرج من الشاشة - والموظف بيدوس رجوع
//     خمس مرات وهو لسه في نفس الصفحة. replaceState بيحدّث الرابط في مكانه.
//
//  ⚠️ والقيم الافتراضية بتتشال من الرابط لا تتكتب فيه: /Requests أنضف من
//     /Requests?tab=all&page=1&q=، والاتنين بيفتحوا نفس الشاشة بالظبط.
//
//  الاستعمال:
//      var tab  = NuhUrl.one('tab', ['all','mine','completed'], 'all');
//      var page = NuhUrl.int('page', 1);
//      NuhUrl.sync({ tab: state.status, page: state.page, q: state.filter },
//                  { tab: 'all', page: 1, q: '' });
// ==========================================================================
var NuhUrl = (function () {
  'use strict';

  function params() {
    try { return new URLSearchParams(window.location.search); }
    catch (e) { return new URLSearchParams(''); }
  }

  // نصّ من الرابط، أو الافتراضي لو ناقص أو فاضي
  function get(key, dflt) {
    var v = params().get(key);
    return (v === null || v === '') ? (dflt === undefined ? '' : dflt) : v;
  }

  // ⚠️ رقم بحدّ أدنى: page=0 أو page=-5 أو page=abc في الرابط كانت هتبعت
  //    للـ API قيمة بايظة. الرابط مدخل مستخدم زيّه زي أي مدخل تاني.
  function int(key, dflt, min) {
    var n = parseInt(get(key, ''), 10);
    if (isNaN(n)) return dflt;
    if (min !== undefined && n < min) return dflt;
    return n;
  }

  function bool(key, dflt) {
    var v = get(key, '');
    if (v === '') return !!dflt;
    return v === '1' || v === 'true';
  }

  // ⚠️ قيمة من قائمة معروفة لا أي نصّ: من غير كده ?tab=<script> بيتكتب في
  //    الصفحة، وأي كلمة غلط بتخلّي الشاشة تفتح على تبويب مش موجود فتبان
  //    فاضية. أي حاجة بره القائمة بترجع للافتراضي في صمت.
  function one(key, allowed, dflt) {
    var v = get(key, '');
    return (allowed && allowed.indexOf(v) > -1) ? v : dflt;
  }

  // تاريخ بصيغة yyyy-MM-dd فقط - نفس اللي بتقبله حقول <input type=date>
  var DATE_RX = /^\d{4}-\d{2}-\d{2}$/;
  function date(key) {
    var v = get(key, '');
    return DATE_RX.test(v) ? v : '';
  }

  // ⚠️ الكتابة بتشيل الافتراضي بدل ما تكتبه، فالرابط بيفضل قصير ومقروء.
  //    والمقارنة بالنصّ عشان 1 و'1' يبقوا واحد.
  function sync(state, defaults) {
    if (!state) return;
    var u;
    try { u = new URL(window.location.href); } catch (e) { return; }
    var d = defaults || {};
    Object.keys(state).forEach(function (k) {
      var v = state[k];
      var isEmpty = (v === null || v === undefined || v === '' || v === false);
      var isDefault = (d[k] !== undefined) && (String(v) === String(d[k]));
      if (isEmpty || isDefault) u.searchParams['delete'](k);
      else u.searchParams.set(k, String(v));
    });
    try { window.history.replaceState(null, '', u.toString()); } catch (e) { }
  }

  return { get: get, int: int, bool: bool, one: one, date: date, sync: sync, params: params };
})();
