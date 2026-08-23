// ==========================================================================
//  date-format.js — شكل التاريخ في الواجهة. التعريف الوحيد في النظام.
//
//  ⚠️ ليه الملف ده موجود:
//     عرض التاريخ كان مكتوب **بعشر طرق مختلفة** في عشر شاشات، وكل واحدة
//     بتطلع شكل تاني لنفس اللحظة:
//
//        سجل العمليات   18/08/2026 22:44     يدوي، أرقام لاتينية
//        سجل الدخول     18/08/2026 22:44     يدوي، نسخة تانية من نفس الكود
//        سجل الأخطاء    18/08/2026 22:44     نسخة تالتة
//        التقارير       18/08/2026 22:44     نسخة رابعة
//        المستخدمون     ٢٠٢٦/٨/١٨            ar-EG - أرقام عربية-هندية
//        الطلبات        ٢٠٢٦/٨/١٨            ar-EG
//        الرئيسية       ٢٠٢٦/٨/١٨            ar-EG
//        حالة الطالب    ١٨ أغسطس ٢٠٢٦        ar-EG بأسماء شهور
//        تفاصيل الطلب   ٢٠٢٦/٨/١٨            ar-SA
//        إسكان الهيئة   2026/08/18           يدوي بترتيب مقلوب
//
//     يعني الموظف بيشوف نفس اليوم بأربع صيغ وأبجديتين حسب الشاشة اللي فاتحها.
//     وأخطر واحدة ar-SA: في متصفحات بترجّع **هجري** افتراضيًا، فالتاريخ يتقرا
//     على إنه ميلادي وهو مش كده.
//
//  ⚠️ القرار: أرقام لاتينية وصيغة dd/MM/yyyy في اللغتين.
//     السبب مش تفضيل جمالي: الرقم الجامعي والهوية والجوال ورقم الطلب
//     (2026-000019) كلهم لاتينيين في كل النظام أصلًا. لو التاريخ وحده بقى
//     ٢٠٢٦ كان الجدول الواحد فيه عمودين بأبجديتين.
//     والصيغة واحدة في اللغتين عشان mm/dd الأمريكية ما تخلّيش 08/09 تتقرا
//     ٨ سبتمبر عند واحد و٩ أغسطس عند التاني على نفس الشاشة.
//
//  ⚠️ ومكانه ملف لوحده مش جوّه date-field.js: ده حساب نصّي خالص بلا DOM،
//     وبيتحمّل بدري في التخطيط عشان الشاشات تقدر تناديه وقت التحميل. حقل
//     التاريخ نفسه بيقرا منه، فالمكتوب في الحقل والمكتوب في الجدول متطابقين
//     بحكم البناء لا بالمصادفة.
// ==========================================================================
var NuhFmt = (function () {

  var AR_MONTHS = ['يناير','فبراير','مارس','أبريل','مايو','يونيو',
                   'يوليو','أغسطس','سبتمبر','أكتوبر','نوفمبر','ديسمبر'];
  var EN_MONTHS = ['January','February','March','April','May','June',
                   'July','August','September','October','November','December'];
  var AR_MONTHS_H = ['محرم','صفر','ربيع الأول','ربيع الآخر','جمادى الأولى','جمادى الآخرة',
                     'رجب','شعبان','رمضان','شوال','ذو القعدة','ذو الحجة'];
  var EN_MONTHS_H = ['Muharram','Safar','Rabi I','Rabi II','Jumada I','Jumada II',
                     'Rajab','Shaban','Ramadan','Shawwal','Dhul-Qadah','Dhul-Hijjah'];
  var AR_DAYS = ['الأحد','الإثنين','الثلاثاء','الأربعاء','الخميس','الجمعة','السبت'];
  var EN_DAYS = ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];

  function lang() {
    var el = document.getElementById('html-root') || document.documentElement;
    return (el.getAttribute('lang') || 'ar').indexOf('en') === 0 ? 'en' : 'ar';
  }

  function pad(n) { return (n < 10 ? '0' : '') + n; }

  // ======================================================================
  //  التقويم المعروض: ميلادي أو هجري — والمالك هنا لا في حقل التاريخ.
  //
  //  ⚠️ المفتاح كان في date-field.js، فكان بيحكم شكل الحقل وحده: المستخدم
  //     يبدّل لهجري ويلاقي الحقل بقى هجري والجدول اللي تحته لسه ميلادي، فما
  //     يعرفش إذا كان التبديل حصل فعلًا ولا لأ.
  //     المالك بقى هنا لأن العرض هو الأصل وحقل التاريخ حالة خاصة منه.
  //
  //  ⚠️ التخزين ميلادي دايمًا مهما كان الاختيار. أي تحويل عند التخزين معناه
  //     إن نفس الصفّ يتقرا بتاريخين حسب مين فاتحه، وإن كل مقارنة وفلترة
  //     وتقرير يبقى فيه احتمال غلط. ده عرض بحت.
  // ======================================================================
  var MODE_KEY = 'nuhCalendarMode';

  // ⚠️ التحويل من Intl لا بجدول محسوب: تقويم أم القرى مضبوط ومحدَّث في
  //    المتصفح، وأي جدول مكتوب هنا كان هيفرق يوم في شهور معيّنة.
  var HIJRI_OK = (function () {
    try {
      return new Intl.DateTimeFormat('en-u-ca-islamic-umalqura',
        { day: 'numeric' }).format(new Date()).length > 0;
    } catch (e) { return false; }
  })();

  function mode() {
    if (!HIJRI_OK) return 'greg';
    try { return localStorage.getItem(MODE_KEY) === 'hijri' ? 'hijri' : 'greg'; }
    catch (e) { return 'greg'; }
  }

  // ⚠️ التبديل بيطلق حدثًا: الجداول مرسومة خلاص، ولو ما اتعادش رسمها هيفضل
  //    نصّها بالتقويم القديم جنب حقل بالتقويم الجديد — وده أسوأ من عدم
  //    التبديل أصلًا. الشاشات مربوطة بـ onLanguageChange وبتعيد الرسم بيها،
  //    فبنعيد استعمالها بدل ما كل شاشة تربط حدثًا جديدًا.
  function setMode(m) {
    try { localStorage.setItem(MODE_KEY, m === 'hijri' ? 'hijri' : 'greg'); } catch (e) {}
    window.dispatchEvent(new Event('nuh:calendar-mode'));
    if (typeof window.onLanguageChange === 'function') {
      try { window.onLanguageChange(); } catch (e) {}
    }
  }

  var _hFmt = null;
  function hijriParts(d) {
    if (!HIJRI_OK) return null;
    if (!_hFmt) {
      _hFmt = new Intl.DateTimeFormat('en-u-ca-islamic-umalqura-nu-latn',
        { day: 'numeric', month: 'numeric', year: 'numeric' });
    }
    var o = { y: 0, m: 0, d: 0 };
    _hFmt.formatToParts(d).forEach(function (p) {
      if (p.type === 'year')  o.y = parseInt(p.value, 10);
      if (p.type === 'month') o.m = parseInt(p.value, 10);
      if (p.type === 'day')   o.d = parseInt(p.value, 10);
    });
    return (o.y && o.m && o.d) ? o : null;
  }

  // ⚠️ بترجّع null لأي مدخل مش تاريخ — والدوال تحت بتحوّل ده لشرطة. الشرطة
  //    أوضح من "Invalid Date" أو من فراغ يخلّي الخانة تبان كأنها بيان ناقص.
  function parse(v) {
    if (v == null || v === '') return null;
    if (v instanceof Date) return isNaN(v.getTime()) ? null : v;
    var d = new Date(v);
    return isNaN(d.getTime()) ? null : d;
  }

  var DASH = '-';

  // 18/08/2026 — أو 05/03/1448 هـ لو الوضع هجري
  function date(v) {
    var d = parse(v);
    if (!d) return DASH;
    if (mode() === 'hijri') {
      var h = hijriParts(d);
      if (h) return pad(h.d) + '/' + pad(h.m) + '/' + h.y + ' هـ';
    }
    return pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + '/' + d.getFullYear();
  }

  // 22:44 — نظام ٢٤ ساعة عن قصد: صيغة واحدة لا تختلف بين العربي والإنجليزي،
  // وما فيهاش لبس ص/م في سجلّ بيتقرا للمراجعة.
  function time(v) {
    var d = parse(v);
    return d ? pad(d.getHours()) + ':' + pad(d.getMinutes()) : DASH;
  }

  // 18/08/2026 22:44
  function dateTime(v) {
    var d = parse(v);
    return d ? date(d) + ' ' + time(d) : DASH;
  }

  // 18 أغسطس 2026 — للأماكن اللي الاسم فيها أوضح من الأرقام (الخط الزمني)
  function dateLong(v) {
    var d = parse(v);
    if (!d) return DASH;
    if (mode() === 'hijri') {
      var h = hijriParts(d);
      if (h) return h.d + ' ' + (lang() === 'en' ? EN_MONTHS_H : AR_MONTHS_H)[h.m - 1] + ' ' + h.y + ' هـ';
    }
    var m = (lang() === 'en' ? EN_MONTHS : AR_MONTHS)[d.getMonth()];
    return d.getDate() + ' ' + m + ' ' + d.getFullYear();
  }

  // الثلاثاء 18 أغسطس 2026 — لترويسة الصفحة
  function dateFull(v) {
    var d = parse(v);
    if (!d) return DASH;
    var day = (lang() === 'en' ? EN_DAYS : AR_DAYS)[d.getDay()];
    return day + ' ' + dateLong(d);
  }

  return {
    date: date, time: time, dateTime: dateTime,
    dateLong: dateLong, dateFull: dateFull, parse: parse,
    mode: mode, setMode: setMode, hijriSupported: HIJRI_OK, hijriParts: hijriParts
  };
})();
