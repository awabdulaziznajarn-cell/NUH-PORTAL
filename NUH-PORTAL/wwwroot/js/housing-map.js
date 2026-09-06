// ============================================================================
//  خريطة مباني الإسكان الجامعي.
//
//  ⚠️ الخريطة مبنية على مصدرين لا تالت: البنية من NuhHousingStructure
//     (المتولّد من Core/HousingStructure.cs) والإشغال من
//     /api/housing/occupancy. الشبكة الفاضية بتترسم هنا، والسيرفر بيبعت
//     الغرف المشغولة بس - فأي تغيير في الترقيم بيوصل للخريطة من غير ما
//     نلمس الملف ده.
//
//  ⚠️ ومافيش أي رقم متيّب هنا: عدد الأدوار والشقق والغرف وأسلوب ترقيمها من
//     NuhHousingStructure، والسعة من المبنى نفسه. سكن الطالبات بيرسم شقق
//     ١-٤ في كل دور وغرف ١-٤، وسكن الطلاب شقق ١-٢٠ وغرف ١-٨٠ - بنفس الكود.
// ============================================================================
(function () {
  'use strict';

  var HS = window.NuhHousingStructure;
  var current = null;      // BuildingOccupancyDto اللي معروض دلوقتي
  var buildings = [];
  var selectedId = null;

  function el(id) { return document.getElementById(id); }

  function floorLabel(code) {
    return String(code) === '0'
      ? tf('reg_optFloorGround', 'الأرضي', 'Ground')
      : tf('loc_floor', 'الدور', 'Floor') + ' ' + code;
  }

  // ⚠️ «١ من ١٦٠» لا «١ / ١٦٠»: الشرطة المائلة بين رقمين في نصّ من اليمين
  //    لليسار بتتقلب - المتصفح بيقرا الرقمين كتلة واحدة فيعرضهم معكوسين،
  //    فـ«١ من ١٦٠» كانت بتبان «١٦٠ / ١». مش خطأ تنسيق: الرقمين بيتبدّلوا
  //    في عين القارئ فيفتكر السكن مليان وهو فاضي.
  //    والكلمة بتحلّها من أصلها: الحروف ليها اتجاه، والرموز لأ.
  function ofText(part, total) {
    return part + ' ' + t('hmap_ofCapacity') + ' ' + total;
  }

  async function api(url) {
    var res = await fetch(url, { credentials: 'same-origin' });
    if (res.status === 401) { window.location.replace('/Account/Login'); return null; }
    if (!res.ok) {
      var msg = '';
      try { msg = (await res.json()).message || ''; } catch (e) { }
      throw new Error(msg || t('hmap_loadFailed'));
    }
    return await res.json();
  }

  // ── شريط المباني ─────────────────────────────────────────────────────────
  function renderBar() {
    var bar = el('hmapBar');
    if (!buildings.length) {
      bar.innerHTML = '<div class="hmap-bar-empty">' + escHtml(t('hmap_noBuildings')) + '</div>';
      return;
    }
    bar.innerHTML = buildings.map(function (b) {
      var pct = b.totalPlaces > 0 ? Math.round((b.occupiedPlaces / b.totalPlaces) * 100) : 0;
      // ⚠️ النسبة مكتوبة رقمًا جنب الشريط لا شريط وحده: الشريط بيقول «كتير
      //    ولا قليل»، والرقم بيقول قد إيه - والاتنين محتاجين لبعض هنا.
      return '<button type="button" class="hmap-chip' + (b.buildingId === selectedId ? ' is-on' : '') +
             '" role="tab" aria-selected="' + (b.buildingId === selectedId) + '" data-id="' + b.buildingId + '">' +
               '<span class="c">' + escHtml(buildingName(b.code) || b.name || b.code) + '</span>' +
               '<span class="m">' + escHtml(ofText(b.occupiedPlaces, b.totalPlaces)) + '</span>' +
               '<span class="bar"><i style="width:' + Math.min(100, pct) + '%"></i></span>' +
             '</button>';
    }).join('');
    Array.prototype.forEach.call(bar.querySelectorAll('.hmap-chip'), function (btn) {
      btn.addEventListener('click', function () { selectBuilding(parseInt(btn.getAttribute('data-id'), 10)); });
    });
  }

  // ── الإحصاءات ────────────────────────────────────────────────────────────
  // ⚠️ الأيقونة خلفية باهتة خارج التدفّق (.stat-ghost) - نفس بطاقات باقي
  //    الشاشات بالحرف، مش نسخة تانية بمقاسات مختلفة.
  var GHOSTS = {
    people: '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>',
    bed: '<path d="M2 4v16"/><path d="M2 8h18a2 2 0 0 1 2 2v10"/><path d="M2 17h20"/><path d="M6 8v9"/>',
    door: '<path d="M13 4h3a2 2 0 0 1 2 2v14"/><path d="M2 20h3"/><path d="M13 20h9"/><path d="M10 12v.01"/><path d="M13 4.8v14.4a.6.6 0 0 1-.7.6l-6-.6a.6.6 0 0 1-.5-.6V5.2a.6.6 0 0 1 .5-.6l6-.6a.6.6 0 0 1 .7.6z"/>',
    check: '<path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/>',
    alert: '<path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4"/><path d="M12 17h.01"/>'
  };

  // ⚠️ بلا stat-sub: السطر الزيادة كان بيطوّل بطاقة واحدة عن جيرانها، فالصفّ
  //    بيبان غير مستوٍ. البطاقة هي نفس بطاقة باقي الشاشات بالحرف - رقم وليبل
  //    وأيقونة خلفية، ومفيش نسخة خاصة بالشاشة دي.
  function stat(value, label, ghost, tone) {
    return '<div class="stat-card' + (tone ? ' ' + tone : '') + '">' +
             '<span class="stat-num">' + value + '</span>' +
             '<span class="stat-label">' + escHtml(label) + '</span>' +
             '<span class="stat-ghost"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
               'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' + GHOSTS[ghost] + '</svg></span>' +
           '</div>';
  }

  function renderStats(d) {
    var freePlaces = Math.max(0, d.totalPlaces - d.occupiedPlaces);
    var html =
      stat(d.occupiedPlaces, t('hmap_statOccupiedPlaces'), 'people') +
      stat(freePlaces, t('hmap_statFreePlaces'), 'bed', 'tone-ok') +
      stat(d.totalRooms - d.occupiedRooms, t('hmap_statEmptyRooms'), 'door') +
      stat(d.fullRooms, t('hmap_statFullRooms'), 'check');
    // ⚠️ العدّادان دول بيظهروا بس لما يبقى فيهم رقم فعلًا. عدّاد دايم بصفر
    //    بيعلّم القارئ يعدّي عليه، وساعتها ما يشوفهوش لما يبقى فيه رقم.
    if (d.extraRooms > 0)
      html += stat(d.extraRooms, t('hmap_statExtraRooms'), 'alert', 'tone-warn');
    if (d.overCapacityRooms > 0)
      html += stat(d.overCapacityRooms, t('hmap_statOverRooms'), 'alert', 'tone-bad');
    el('hmapStats').innerHTML = html;
  }

  // ── حالة الغرفة ──────────────────────────────────────────────────────────
  // خمس حالات لا أربعة: «فوق السعة بموافقة المشرف» إجراء مسموح، و«فوق الحدّ»
  // خلل بيانات - ولو الاتنين بلون واحد، اللون بيبطّل يقول حاجة.
  function roomState(n, d) {
    if (n === 0) return 'free';
    if (n > d.roomCapacityMax) return 'over';
    if (n > d.roomCapacity) return 'extra';
    if (n >= d.roomCapacity) return 'full';
    return 'part';
  }

  function index(d) {
    var byKey = {};
    d.rooms.forEach(function (r) { byKey[r.floor + '/' + r.apartment + '/' + r.room] = r; });
    return byKey;
  }

  // ⚠️ نقط لا كسر «٠/٢»: الكسر كان بينعكس في الاتجاه من اليمين لليسار فيتقري
  //    «٢/٠» - يعني المشغول والسعة بيتبدّلوا في عين القارئ. والنقط بتتقري
  //    أسرع كمان: العين بتعدّ التلات نقط من غير ما تقرا رقم.
  //  عدد النقط = الحدّ الأقصى للمبنى، والنقطة بعد السعة المعتمدة ليها شكل
  //  مميَّز - عشان «تالت ساكن بموافقة المشرف» يبان إنه استثناء لا حالة عادية.
  function dots(n, d) {
    var out = '';
    for (var i = 1; i <= d.roomCapacityMax; i++) {
      var cls = i > d.roomCapacity ? 'x' : '';
      out += '<i class="' + (i <= n ? ('on ' + cls) : cls) + '"></i>';
    }
    // الزيادة فوق الحدّ مالهاش نقطة - بيتكتب عددها رقمًا عشان تبان.
    if (n > d.roomCapacityMax) out += '<b>+' + (n - d.roomCapacityMax) + '</b>';
    return '<span class="d">' + out + '</span>';
  }

  function roomBtn(d, byKey, floor, apt, room) {
    var cell = byKey[floor + '/' + apt + '/' + room];
    var n = cell ? cell.occupants.length : 0;
    var st = roomState(n, d);
    // ⚠️ الحالة على الغرفة كمان (طالب متخرّج لسه شايل غرفة): علامة صغيرة على
    //    المربّع، مش لون تاني - اللون محجوز للإشغال.
    var flag = cell && cell.occupants.some(function (o) { return o.status; });
    return '<button type="button" class="hmap-room is-' + st + (flag ? ' has-flag' : '') + '"' +
           ' data-floor="' + escHtml(floor) + '" data-apt="' + apt + '" data-room="' + room + '"' +
           ' title="' + escHtml(tf('loc_room', 'غرفة', 'Room') + ' ' + room + ' \u2013 ' +
                                 n + ' ' + t('hmap_ofCapacity') + ' ' + d.roomCapacity) + '">' +
             '<span class="n">' + room + '</span>' +
             dots(n, d) +
           '</button>';
  }

  function bindRooms(host) {
    Array.prototype.forEach.call(host.querySelectorAll('.hmap-room'), function (btn) {
      btn.addEventListener('click', function () {
        openRoomPanel(btn.getAttribute('data-floor'),
                      parseInt(btn.getAttribute('data-apt'), 10),
                      parseInt(btn.getAttribute('data-room'), 10));
      });
    });
  }

  // ── الشبكة المسطّحة ──────────────────────────────────────────────────────
  function renderFlat(d) {
    var byKey = index(d), out = [];

    for (var f = 0; f < d.floorCount; f++) {
      var floor = String(f);
      var apts = HS.apartmentsFor(d.numbering, f);
      var floorTaken = 0, floorCap = apts.length * d.roomsPerApartment * d.roomCapacity;

      var aptHtml = apts.map(function (aptStr) {
        var apt = parseInt(aptStr, 10);
        var rooms = HS.roomsFor(d.numbering, apt).map(function (roomStr) {
          var room = parseInt(roomStr, 10);
          var cell = byKey[floor + '/' + apt + '/' + room];
          floorTaken += cell ? cell.occupants.length : 0;
          return roomBtn(d, byKey, floor, apt, room);
        }).join('');
        return '<div class="hmap-apt">' +
                 '<div class="hmap-apt-h">' + escHtml(tf('loc_apartment', 'شقة', 'Apt') + ' ' + apt) + '</div>' +
                 '<div class="hmap-apt-rooms">' + rooms + '</div>' +
               '</div>';
      }).join('');

      out.push(
        '<section class="hmap-floor">' +
          '<div class="hmap-floor-h">' +
            '<b>' + escHtml(floorLabel(floor)) + '</b>' +
            '<span>' + escHtml(ofText(floorTaken, floorCap) + ' ' + t('hmap_placesWord')) + '</span>' +
          '</div>' +
          '<div class="hmap-apts">' + aptHtml + '</div>' +
        '</section>');
    }

    var host = el('hmapFloors');
    host.className = '';
    host.innerHTML = out.join('');
    bindRooms(host);
  }

  // ── صفوف خارج البنية ─────────────────────────────────────────────────────
  //  ⚠️ مش تحذير تقني: دي صفوف طلاب حقيقيين مش ظاهرين في الخريطة، ولو
  //     اتخبّوا هيفضلوا مخفيين للأبد. بتظهر تحت الشبكة بالاسم والرقم عشان
  //     حد يصلّحها من شاشة الطلاب. ومابتظهرش أصلًا لو مافيش ولا صفّ -
  //     صندوق تحذير فاضي بيعلّم القارئ يعدّي عليه.
  function renderOrphans(d) {
    var host = el('hmapOrphans');
    if (!d.orphans || !d.orphans.length) { host.innerHTML = ''; return; }
    host.innerHTML =
      '<div class="hmap-orphans">' +
        '<div class="hmap-orphans-h">' +
          '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg>' +
          '<b>' + escHtml(t('hmap_orphansTitle')) + '</b>' +
          '<span>' + escHtml(t('hmap_orphansNote')) + '</span>' +
        '</div>' +
        '<ul>' + d.orphans.map(function (o) {
          var why = t('hmap_orphan_' + o.reason);
          // ⚠️ بالكلمات لا بالشرطات: «٣ / ٩٩ / ٢» تلات أرقام بتتقلب في الاتجاه،
          //    والصفّ ده أصلًا بيتعرض عشان حد يصلّحه - فلازم يتقري صح.
          var loc = [
            o.floor ? floorLabel(o.floor) : '',
            o.apartment ? tf('loc_apartment', 'شقة', 'Apt') + ' ' + o.apartment : '',
            o.room ? tf('loc_room', 'غرفة', 'Room') + ' ' + o.room : ''
          ].filter(Boolean).join(' · ');
          return '<li>' +
                   '<b>' + escHtml(o.name || '') + '</b>' +
                   '<span class="sn">' + escHtml(o.studentNumber || '') + '</span>' +
                   '<span class="lc">' + escHtml(loc) + '</span>' +
                   '<span class="wy">' + escHtml(why) + '</span>' +
                 '</li>';
        }).join('') + '</ul>' +
      '</div>';
  }

  // ── لوحة الغرفة ──────────────────────────────────────────────────────────
  function openRoomPanel(floor, apt, room) {
    if (!current) return;
    var cell = current.rooms.filter(function (r) {
      return r.floor === floor && r.apartment === apt && r.room === room;
    })[0];
    var occ = cell ? cell.occupants : [];

    el('hmapRoomTitle').textContent =
      floorLabel(floor) + ' · ' +
      tf('loc_apartment', 'شقة', 'Apt') + ' ' + apt + ' · ' +
      tf('loc_room', 'غرفة', 'Room') + ' ' + room;

    var free = current.roomCapacity - occ.length;
    var head = '<div class="hmap-rp-head">' +
      '<span class="cap">' + escHtml(t('hmap_capacity')) + ': ' + current.roomCapacity + '</span>' +
      (free > 0
        ? '<span class="free">' + escHtml(t('hmap_freePlaces')) + ': ' + free + '</span>'
        : free === 0
          ? '<span class="fullb">' + escHtml(t('hmap_lgFull')) + '</span>'
          : occ.length <= current.roomCapacityMax
            // ⚠️ فوق السعة وجوّه الحدّ = إجراء مسموح للمشرف لا خطأ. الرسالة
            //    بتقول كده صراحة بدل ما الشاشة تبان كأنها بتشتكي.
            ? '<span class="extrab">' + escHtml(t('hmap_extraBy')) + ' ' + (-free) + '</span>'
            : '<span class="overb">' + escHtml(t('hmap_overMax')) + ' ' + current.roomCapacityMax + '</span>') +
      '</div>';

    var body = occ.length
      ? '<ul class="hmap-rp-list">' + occ.map(function (o) {
          // ⚠️ الحالة بتظهر لو موجودة بس: الساكن العادي حالته فاضية، وشارة
          //    «مقيم» على كل صفّ كانت هتغرّق الشارة اللي فيها معلومة فعلًا.
          var statusBadge = o.status
            ? '<span class="st">' + escHtml(tf('ss_status_' + o.status, o.status, o.status)) + '</span>' : '';
          var adBadge = o.adStatus === 'disabled'
            ? '<span class="ad">' + escHtml(t('hmap_adDisabled')) + '</span>' : '';
          return '<li>' +
                   '<div class="nm">' + escHtml(o.name || '') + statusBadge + adBadge + '</div>' +
                   '<div class="sn">' + escHtml(o.studentNumber || '') + '</div>' +
                 '</li>';
        }).join('') + '</ul>'
      : '<div class="hmap-rp-empty">' + escHtml(t('hmap_roomEmpty')) + '</div>';

    el('hmapRoomBody').innerHTML = head + body;
    el('hmapRoomModal').classList.add('show');
  }

  window.closeRoomPanel = function () { el('hmapRoomModal').classList.remove('show'); };

  // ⚠️ الضغط على الخلفية بيقفل: المستخدم بيفتح غرف كتير ورا بعض، ولو
  //    القفل من زرار واحد بس بيبقى نقلتين لكل غرفة.
  document.addEventListener('click', function (e) {
    if (e.target === el('hmapRoomModal')) window.closeRoomPanel();
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') window.closeRoomPanel();
  });

  // ── التحميل ──────────────────────────────────────────────────────────────
  async function selectBuilding(id) {
    selectedId = id;
    renderBar();
    // ⚠️ رقم المبنى في الرابط: المشرف بيفضل على نفس المبنى طول اليوم،
    //    وتحديث الصفحة كان بيرجّعه لأول مبنى في القائمة.
    if (window.NuhUrl && NuhUrl.sync) NuhUrl.sync({ b: id });

    el('hmapCard').hidden = true;
    var ld = el('hmapLoading'); ld.hidden = false;
    try {
      var d = await api('/api/housing/occupancy/' + id);
      current = d;
      el('hmapTitle').textContent = buildingName(d.code) || d.name || d.code;
      el('hmapSub').textContent =
        t('hmap_capacity') + ': ' + d.roomCapacity +
        (d.roomCapacityMax > d.roomCapacity ? ' (' + t('hmap_upTo') + ' ' + d.roomCapacityMax + ')' : '') +
        ' · ' + d.totalRooms + ' ' + t('hmap_roomsWord');
      renderStats(d);
      renderFlat(d);
      renderOrphans(d);
      el('hmapCard').hidden = false;
    } catch (e) {
      el('hmapStats').innerHTML = '';
      el('hmapFloors').innerHTML = '';
      el('hmapOrphans').innerHTML = '<div class="hmap-error">' + escHtml(e.message || t('hmap_loadFailed')) + '</div>';
      el('hmapCard').hidden = false;
    } finally {
      ld.hidden = true;
    }
  }

  async function init() {
    try {
      buildings = (await api('/api/housing/occupancy/buildings')) || [];
    } catch (e) {
      el('hmapBar').innerHTML = '<div class="hmap-error">' + escHtml(e.message || t('hmap_loadFailed')) + '</div>';
      return;
    }
    if (!buildings.length) { renderBar(); return; }

    var fromUrl = (window.NuhUrl && NuhUrl.int) ? NuhUrl.int('b', 0, 1) : 0;
    var wanted = buildings.filter(function (b) { return b.buildingId === fromUrl; })[0];
    selectBuilding(wanted ? wanted.buildingId : buildings[0].buildingId);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
