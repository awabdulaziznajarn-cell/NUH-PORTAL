/* ============================================================================
   خانات الاسم الثلاثية — منطق مشترك بين كل شاشات إدخال الطالب.
   ----------------------------------------------------------------------------
   الاسم بيتخزّن نص واحد في full_name / full_name_english زي ما هو، لكنه بيتدخّل
   مقسّم لتلات خانات. السبب مش شكلي:

     ADProvisioningService.SplitEnglishName بياخد
        givenName = parts[0]        اسم واحد
        initials  = parts[1][0]     أول حرف من اسم الأب
        sn        = parts[^1]       اسم واحد

   يعني أي اسم مكتوب بترتيب مختلف بيروح Active Directory غلط ومحدش بياخد باله.
   الخانات المقسّمة بتمنع عكس الترتيب من الأصل، والخانة الوسطى بتستوعب الاسم
   الخماسي والسداسي من غير ما تكسر التفكيك.

   اصطلاح الـ id:   {prefix}ar_first  {prefix}ar_middle  {prefix}ar_family
                    {prefix}en_first  {prefix}en_middle  {prefix}en_family
   رسائل الخطأ:     {prefix}err-name_ar   {prefix}err-name_en   {prefix}err-name_match
   ============================================================================ */
(function (global) {
  'use strict';

  // ⚠️ العربي والإنجليزي ليسا سواءً هنا، والفرق ليس تفضيلًا:
  //
  //    الاسم العربي (full_name) يذهب إلى الدليل النشط في description **كنصّ
  //    واحد بلا تفكيك** - تحقَّق منه في ADProvisioningService سطر 65 و279.
  //    فالمسافة فيه لا تكسر شيئًا.
  //
  //    الاسم الإنجليزي (full_name_english) يُفكَّك في SplitEnglishName إلى
  //    givenName و initials و sn، و sn = آخر كلمة. فلو سُمحت المسافة في خانة
  //    العائلة الإنجليزية صار "AL SAIARI" ← sn = "SAIARI" وضاعت «AL» في
  //    الدليل بصمت. تبقى ممنوعة حتى يُصلَح التفكيك نفسه.
  //
  //  ⚠️ وكانت المسافة ممنوعة في العربي أيضًا للسبب الإنجليزي نفسه - فتعذّرت
  //     كتابة «آل سوادي» و«آل سالم» و«عبد الله»، وهي أسماء عائلات حقيقية،
  //     فيُجبَر المُدخِل على لصقها «السوادي» فيُخزَّن اسم غير اسم صاحبه.
  //
  //  الخانة الوسطى: مسافة عادية بس (مش \s) — \s بيسمح بـ Tab و NBSP، والـ C#
  //  كان بيقسّم على المسافة العادية، فاللصق من Word كان بيلزّق اسمين في اسم.
  //  النطاق العربي بيستثني الأرقام العربية (\u0660-\u0669) عمدًا.
  var RE = {
    ar_one:  /[^\u0621-\u0652\u0670-\u06D3 ]/g,
    ar_many: /[^\u0621-\u0652\u0670-\u06D3 ]/g,
    en_one:  /[^a-zA-Z]/g,
    en_many: /[^a-zA-Z ]/g
  };

  var FIELDS = [
    { part: 'ar_first',  re: RE.ar_one,  lang: 'ar' },
    { part: 'ar_middle', re: RE.ar_many, lang: 'ar' },
    { part: 'ar_family', re: RE.ar_one,  lang: 'ar' },
    { part: 'en_first',  re: RE.en_one,  lang: 'en', upper: true },
    { part: 'en_middle', re: RE.en_many, lang: 'en', upper: true },
    { part: 'en_family', re: RE.en_one,  lang: 'en', upper: true }
  ];

  function get(prefix, part) { return document.getElementById((prefix || '') + part); }
  function err(prefix, name) { return document.getElementById((prefix || '') + 'err-' + name); }
  function toggle(el, on) { if (el) el.classList[on ? 'add' : 'remove']('show'); }
  function mark(el, on) { if (el) el.classList[on ? 'add' : 'remove']('error'); }

  function collapse(v) { return String(v == null ? '' : v).replace(/ +/g, ' ').trim(); }
  function words(v) { var c = collapse(v); return c ? c.split(' ') : []; }

  // بيشيل الحروف الممنوعة فور كتابتها أو لصقها، وبيحافظ على مكان المؤشر —
  // من غير كده المؤشر بيقفز لآخر الخانة مع كل تصحيح.
  function strip(el, re, upper) {
    var before = el.value;
    var after = before.replace(re, '');
    if (upper) after = after.toUpperCase();
    if (after === before) return;
    var pos = el.selectionStart;
    el.value = after;
    var p = Math.max(0, pos - (before.length - after.length));
    try { el.setSelectionRange(p, p); } catch (e) { /* بعض المتصفحات ماتدعمش */ }
  }

  // عدد أسماء الأب والجد لازم يتطابق بين اللغتين — بيمسك نسيان اسم في لغة واحدة،
  // ومهم لأن description بياخد العربي و displayName بياخد الإنجليزي لنفس الشخص.
  function checkMatch(prefix) {
    var ar = get(prefix, 'ar_middle');
    var en = get(prefix, 'en_middle');
    if (!ar || !en) return true;
    var bad = words(ar.value).length > 0 && words(en.value).length > 0 &&
              words(ar.value).length !== words(en.value).length;
    toggle(err(prefix, 'name_match'), bad);
    mark(ar, bad); mark(en, bad);
    return !bad;
  }

  function attach(prefix) {
    FIELDS.forEach(function (f) {
      var el = get(prefix, f.part);
      if (!el) return;
      el.addEventListener('input', function () {
        strip(this, f.re, f.upper);
        toggle(err(prefix, 'name_' + f.lang), false);
        checkMatch(prefix);
      });
      // دمج المسافات المتكررة عند الخروج — من غيره "وليد  محمد" بيتخزّن
      // بمسافتين في description و displayName جوه AD
      el.addEventListener('blur', function () {
        this.value = collapse(this.value);
        checkMatch(prefix);
      });
    });
  }

  // بيلزّق التلات خانات في نص واحد — ده اللي بيتبعت للسيرفر، فشكل البيانات
  // المخزّنة ما اتغيّرش: العمود فاضل نص واحد زي الأول.
  function join(prefix, lang) {
    return [lang + '_first', lang + '_middle', lang + '_family']
      .map(function (part) { var el = get(prefix, part); return el ? collapse(el.value) : ''; })
      .filter(function (v) { return v.length; })
      .join(' ');
  }

  // بيوزّع اسم مخزّن كنص واحد على التلات خانات (شاشات التعديل والبيانات القديمة)
  function fill(prefix, lang, full) {
    var p = words(full);
    var first = '', middle = '', family = '';
    if (p.length === 1) { first = p[0]; }
    else if (p.length === 2) { first = p[0]; family = p[1]; }
    else if (p.length > 2) { first = p[0]; family = p[p.length - 1]; middle = p.slice(1, -1).join(' '); }
    var a = get(prefix, lang + '_first');   if (a) a.value = first;
    var b = get(prefix, lang + '_middle');  if (b) b.value = middle;
    var c = get(prefix, lang + '_family');  if (c) c.value = family;
  }

  // كل الخانات مطلوبة — ورسالة واحدة لكل لغة بدل ست رسائل
  function validate(prefix) {
    var ok = true, missing = { ar: false, en: false };
    FIELDS.forEach(function (f) {
      var el = get(prefix, f.part);
      if (!el) return;
      var empty = !collapse(el.value);
      mark(el, empty);
      if (empty) { missing[f.lang] = true; ok = false; }
    });
    toggle(err(prefix, 'name_ar'), missing.ar);
    toggle(err(prefix, 'name_en'), missing.en);
    if (!checkMatch(prefix)) ok = false;
    return ok;
  }

  function clear(prefix) {
    FIELDS.forEach(function (f) {
      var el = get(prefix, f.part);
      if (el) { el.value = ''; mark(el, false); }
    });
    toggle(err(prefix, 'name_ar'), false);
    toggle(err(prefix, 'name_en'), false);
    toggle(err(prefix, 'name_match'), false);
  }

  global.NuhName = {
    attach: attach, join: join, fill: fill,
    validate: validate, clear: clear,
    words: words, collapse: collapse
  };
})(window);
