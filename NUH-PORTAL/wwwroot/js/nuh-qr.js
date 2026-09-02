// ============================================================================
//  nuh-qr.js - توليد رمز QR للطباعة، بلا أي مكتبة خارجية.
//
//  ⚠️ ليه مكتوب هنا بدل مكتبة جاهزة: الوثيقة دي بتتطبع من شاشة داخل الشبكة،
//     وربط ورقة رسمية بسكربت من CDN معناه إن الورقة تطلع بلا رمز يوم ما
//     الشبكة تقطع عن الخارج - أو تطلع برمز من مصدر مش تحت سيطرتنا.
//
//  المدى المدعوم: الإصدارات ١-٦ بمستوى تصحيح M ونمط البايت (UTF-8).
//  يعني لحد ١٠٦ بايت - ورابط التحقّق عندنا ٥٢ بايت، فالهامش واسع.
//  أطول من كده بترجّع null، والمنادي بيطبع الرمز نصًّا بلا صورة.
//
//  ⚠️ الناتج اتقورن مصفوفةً بمصفوفة مع مكتبة qrcode القياسية على نفس المدخلات
//     ولكل الأقنعة الثمانية قبل ما يتحطّ في الوثيقة.
// ============================================================================
var NuhQr = (function () {
  'use strict';

  // ---------------------------------------------------------------- GF(256)
  // ⚠️ الجدول متولّد وقت التحميل لا مكتوب بالإيد: ٥١٢ رقم منسوخين يدويًّا
  //    بيخبّوا أي غلطة رقم واحد جوّه، وبتظهر كرمز مايتقراش على ورقة مطبوعة.
  var EXP = new Array(512), LOG = new Array(256);
  (function () {
    var x = 1;
    for (var i = 0; i < 255; i++) {
      EXP[i] = x;
      LOG[x] = i;
      x <<= 1;
      if (x & 0x100) x ^= 0x11D;          // كثير الحدود البدائي للمعيار
    }
    for (var j = 255; j < 512; j++) EXP[j] = EXP[j - 255];
  })();

  function gfMul(a, b) {
    if (a === 0 || b === 0) return 0;
    return EXP[LOG[a] + LOG[b]];
  }

  // كثير حدود المولّد لعدد معيّن من خانات التصحيح
  function rsPoly(degree) {
    var poly = [1];
    for (var i = 0; i < degree; i++) {
      var next = new Array(poly.length + 1);
      for (var j = 0; j < next.length; j++) next[j] = 0;
      for (var k = 0; k < poly.length; k++) {
        next[k] ^= poly[k];
        next[k + 1] ^= gfMul(poly[k], EXP[i]);
      }
      poly = next;
    }
    return poly;
  }

  function rsEncode(data, ecLen) {
    var gen = rsPoly(ecLen);
    var res = new Array(ecLen);
    for (var i = 0; i < ecLen; i++) res[i] = 0;

    for (var d = 0; d < data.length; d++) {
      var factor = data[d] ^ res[0];
      res.shift();
      res.push(0);
      if (factor !== 0) {
        for (var g = 1; g < gen.length; g++)
          res[g - 1] ^= gfMul(gen[g], factor);
      }
    }
    return res;
  }

  // ------------------------------------------------------- جداول الإصدارات
  // [عدد الكتل, إجمالي خانات الكتلة, خانات البيانات في الكتلة] - مستوى M.
  var BLOCKS = {
    1: [[1, 26, 16]],
    2: [[1, 44, 28]],
    3: [[1, 70, 44]],
    4: [[2, 50, 32]],
    5: [[2, 67, 43]],
    6: [[4, 43, 27]]
  };

  // مراكز أنماط المحاذاة (بخلاف الإصدار ١ اللي مالوش)
  var ALIGN = { 1: [], 2: [6, 18], 3: [6, 22], 4: [6, 26], 5: [6, 30], 6: [6, 34] };

  var MAX_VERSION = 6;

  function dataCapacityBytes(version) {
    var total = 0;
    BLOCKS[version].forEach(function (b) { total += b[0] * b[2]; });
    // ٤ بت للنمط + ٨ بت لعدد البايتات (الإصدارات ١-٩)
    return Math.floor((total * 8 - 12) / 8);
  }

  function sizeOf(version) { return version * 4 + 17; }

  // -------------------------------------------------------------- المصفوفة
  function reserve(m, size, version) {
    function block(r, c, w, h) {
      for (var i = 0; i < h; i++)
        for (var j = 0; j < w; j++) {
          var rr = r + i, cc = c + j;
          if (rr >= 0 && rr < size && cc >= 0 && cc < size) m[rr][cc] = false;
        }
    }
    function finder(r, c) {
      for (var i = -1; i <= 7; i++)
        for (var j = -1; j <= 7; j++) {
          var rr = r + i, cc = c + j;
          if (rr < 0 || rr >= size || cc < 0 || cc >= size) continue;
          var on = (i >= 0 && i <= 6 && (j === 0 || j === 6)) ||
                   (j >= 0 && j <= 6 && (i === 0 || i === 6)) ||
                   (i >= 2 && i <= 4 && j >= 2 && j <= 4);
          m[rr][cc] = on;
        }
    }

    finder(0, 0);
    finder(0, size - 7);
    finder(size - 7, 0);

    // ⚠️ خانات معلومات الشكل بتتحجز **قبل** نمط التوقيت لا بعده: الشريطان
    //    بيتقاطعوا عند [6][8] و[8][6]، والخانتين دول توقيت لا شكل. لما كان
    //    الحجز بعدين كان بيمسحهم، والرمز بيطلع شكله سليم ومايتقراش.
    block(8, 0, 9, 1); block(0, 8, 1, 9);
    block(8, size - 8, 8, 1); block(size - 8, 8, 1, 8);

    // نمط التوقيت
    for (var i = 8; i < size - 8; i++) {
      m[6][i] = (i % 2 === 0);
      m[i][6] = (i % 2 === 0);
    }

    // أنماط المحاذاة - بتتخطّى اللي بيتعارك مع مربّعات الأركان
    var cs = ALIGN[version];
    for (var a = 0; a < cs.length; a++) {
      for (var b = 0; b < cs.length; b++) {
        var r = cs[a], c = cs[b];
        if ((r === 6 && c === 6) || (r === 6 && c === size - 7) || (r === size - 7 && c === 6)) continue;
        for (var i2 = -2; i2 <= 2; i2++)
          for (var j2 = -2; j2 <= 2; j2++)
            m[r + i2][c + j2] = (Math.max(Math.abs(i2), Math.abs(j2)) !== 1);
      }
    }

    // الخانة الغامقة الثابتة
    m[size - 8][8] = true;
  }

  function maskAt(pattern, i, j) {
    switch (pattern) {
      case 0: return (i + j) % 2 === 0;
      case 1: return i % 2 === 0;
      case 2: return j % 3 === 0;
      case 3: return (i + j) % 3 === 0;
      case 4: return (Math.floor(i / 2) + Math.floor(j / 3)) % 2 === 0;
      case 5: return ((i * j) % 2) + ((i * j) % 3) === 0;
      case 6: return (((i * j) % 2) + ((i * j) % 3)) % 2 === 0;
      default: return (((i + j) % 2) + ((i * j) % 3)) % 2 === 0;
    }
  }

  // معلومات الشكل: ١٥ بت بتصحيح BCH ثم XOR بالقناع الثابت
  function formatBits(mask) {
    var data = (0x00 << 3) | mask;         // 00 = مستوى التصحيح M
    var rem = data << 10;
    for (var i = 4; i >= 0; i--)
      if (rem & (1 << (i + 10))) rem ^= 0x537 << i;
    return ((data << 10) | rem) ^ 0x5412;
  }

  function putFormat(m, size, mask) {
    var bits = formatBits(mask);
    for (var i = 0; i < 15; i++) {
      var on = ((bits >> i) & 1) === 1;
      if (i < 6) m[i][8] = on;
      else if (i < 8) m[i + 1][8] = on;
      else m[size - 15 + i][8] = on;

      if (i < 8) m[8][size - i - 1] = on;
      else if (i < 9) m[8][15 - i] = on;
      else m[8][15 - i - 1] = on;
    }
    m[size - 8][8] = true;
  }

  function putData(m, size, bytes, mask) {
    var inc = -1, row = size - 1, bit = 7, idx = 0;
    for (var col = size - 1; col > 0; col -= 2) {
      if (col === 6) col--;
      for (;;) {
        for (var c = 0; c < 2; c++) {
          if (m[row][col - c] === null) {
            var dark = false;
            if (idx < bytes.length) dark = ((bytes[idx] >>> bit) & 1) === 1;
            if (maskAt(mask, row, col - c)) dark = !dark;
            m[row][col - c] = dark;
            bit--;
            if (bit === -1) { idx++; bit = 7; }
          }
        }
        row += inc;
        if (row < 0 || row >= size) { row -= inc; inc = -inc; break; }
      }
    }
  }

  // ---------------------------------------------------------- تقييم الأقنعة
  function penalty(m, size) {
    var score = 0, i, j, k;

    // (١) خمس خانات متتالية بنفس اللون فأكثر
    for (i = 0; i < size; i++) {
      for (j = 0; j < size; j++) {
        var sameRow = 1, sameCol = 1;
        for (k = j + 1; k < size && m[i][k] === m[i][j]; k++) sameRow++;
        if (sameRow >= 5) { score += 3 + (sameRow - 5); j += sameRow - 1; }
      }
    }
    for (j = 0; j < size; j++) {
      for (i = 0; i < size; i++) {
        var run = 1;
        for (k = i + 1; k < size && m[k][j] === m[i][j]; k++) run++;
        if (run >= 5) { score += 3 + (run - 5); i += run - 1; }
      }
    }

    // (٢) مربّعات ٢×٢ بلون واحد
    for (i = 0; i < size - 1; i++)
      for (j = 0; j < size - 1; j++)
        if (m[i][j] === m[i][j + 1] && m[i][j] === m[i + 1][j] && m[i][j] === m[i + 1][j + 1])
          score += 3;

    // (٣) نمط يشبه مربّع الركن (1:1:3:1:1 مع فراغ)
    var P1 = [true, false, true, true, true, false, true, false, false, false, false];
    var P2 = [false, false, false, false, true, false, true, true, true, false, true];
    function matches(get, at, len, pat) {
      if (at + 11 > len) return false;
      for (var t = 0; t < 11; t++) if (get(at + t) !== pat[t]) return false;
      return true;
    }
    for (i = 0; i < size; i++) {
      for (j = 0; j + 11 <= size; j++) {
        (function (r) {
          var getRow = function (x) { return m[r][x]; };
          if (matches(getRow, j, size, P1) || matches(getRow, j, size, P2)) score += 40;
        })(i);
        (function (c) {
          var getCol = function (x) { return m[x][c]; };
          if (matches(getCol, j, size, P1) || matches(getCol, j, size, P2)) score += 40;
        })(i);
      }
    }

    // (٤) نسبة الغامق للفاتح
    var dark = 0;
    for (i = 0; i < size; i++) for (j = 0; j < size; j++) if (m[i][j]) dark++;
    var ratio = Math.abs((dark * 100) / (size * size) - 50);
    score += Math.floor(ratio / 5) * 10;

    return score;
  }

  // ------------------------------------------------------------- الترميز
  function utf8(text) {
    var out = [], s = unescape(encodeURIComponent(String(text)));
    for (var i = 0; i < s.length; i++) out.push(s.charCodeAt(i) & 0xff);
    return out;
  }

  function buildCodewords(bytes, version) {
    var groups = BLOCKS[version];
    var dataTotal = 0, i, j;
    groups.forEach(function (g) { dataTotal += g[0] * g[2]; });

    // تيار البتّات: النمط (0100) ثم الطول (٨ بت) ثم البايتات
    var bits = [];
    function push(value, len) {
      for (var b = len - 1; b >= 0; b--) bits.push((value >> b) & 1);
    }
    push(4, 4);
    push(bytes.length, 8);
    for (i = 0; i < bytes.length; i++) push(bytes[i], 8);

    var capacity = dataTotal * 8;
    for (i = 0; i < 4 && bits.length < capacity; i++) bits.push(0);
    while (bits.length % 8 !== 0) bits.push(0);

    var words = [];
    for (i = 0; i < bits.length; i += 8) {
      var v = 0;
      for (j = 0; j < 8; j++) v = (v << 1) | bits[i + j];
      words.push(v);
    }
    var padA = 0xEC, padB = 0x11, turn = 0;
    while (words.length < dataTotal) words.push((turn++ % 2) === 0 ? padA : padB);

    // تقسيم لكتل + تصحيح لكل كتلة
    var dataBlocks = [], ecBlocks = [], at = 0;
    groups.forEach(function (g) {
      for (var b = 0; b < g[0]; b++) {
        var block = words.slice(at, at + g[2]);
        at += g[2];
        dataBlocks.push(block);
        ecBlocks.push(rsEncode(block, g[1] - g[2]));
      }
    });

    // التشبيك: خانة من كل كتلة بالدور، ثم خانات التصحيح بنفس الطريقة
    var out = [], maxData = 0, maxEc = 0;
    dataBlocks.forEach(function (b) { maxData = Math.max(maxData, b.length); });
    ecBlocks.forEach(function (b) { maxEc = Math.max(maxEc, b.length); });
    for (i = 0; i < maxData; i++)
      for (j = 0; j < dataBlocks.length; j++)
        if (i < dataBlocks[j].length) out.push(dataBlocks[j][i]);
    for (i = 0; i < maxEc; i++)
      for (j = 0; j < ecBlocks.length; j++)
        if (i < ecBlocks[j].length) out.push(ecBlocks[j][i]);

    return out;
  }

  // بترجّع مصفوفة منطقية [صف][عمود]، أو null لو النصّ أطول من المدى المدعوم.
  function matrix(text, forcedMask) {
    var bytes = utf8(text), version = 0;
    for (var v = 1; v <= MAX_VERSION; v++) {
      if (bytes.length <= dataCapacityBytes(v)) { version = v; break; }
    }
    if (!version) return null;

    var size = sizeOf(version);
    var words = buildCodewords(bytes, version);

    function fresh() {
      var m = new Array(size);
      for (var i = 0; i < size; i++) {
        m[i] = new Array(size);
        for (var j = 0; j < size; j++) m[i][j] = null;
      }
      reserve(m, size, version);
      return m;
    }

    var best = null, bestScore = Infinity;
    var from = (forcedMask == null ? 0 : forcedMask);
    var to = (forcedMask == null ? 7 : forcedMask);
    for (var mask = from; mask <= to; mask++) {
      var m = fresh();
      putData(m, size, words, mask);
      putFormat(m, size, mask);
      var s = penalty(m, size);
      if (s < bestScore) { bestScore = s; best = m; }
    }
    return best;
  }

  // ⚠️ الهامش الأبيض (٤ خانات) جزء من المعيار لا زخرفة: من غيره كتير من
  //    القارئات مابتشوفش الرمز أصلًا لما يكون ملزوق في إطار الوثيقة.
  var QUIET = 4;

  // بترسم على <canvas> بمقاس خانة واحدة لكل بكسل - والتكبير من الـ CSS
  // بـ image-rendering:pixelated، عشان الطباعة تطلع حوافّ حادّة.
  function toCanvas(canvas, text) {
    var m = matrix(text);
    if (!canvas || !canvas.getContext) return false;
    if (!m) return false;

    var n = m.length, full = n + QUIET * 2;
    canvas.width = full;
    canvas.height = full;

    var g = canvas.getContext('2d');
    g.fillStyle = '#ffffff';
    g.fillRect(0, 0, full, full);
    g.fillStyle = '#000000';
    for (var i = 0; i < n; i++)
      for (var j = 0; j < n; j++)
        if (m[i][j]) g.fillRect(j + QUIET, i + QUIET, 1, 1);

    return true;
  }

  return { matrix: matrix, toCanvas: toCanvas, capacity: dataCapacityBytes };
})();

if (typeof module !== 'undefined' && module.exports) module.exports = NuhQr;
