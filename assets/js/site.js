/* ============================================================
   缘启 · 官网页面交互
   无依赖：视差、横向画廊、入场动画、主题换色、移动菜单
   ============================================================ */
(function () {
  "use strict";

  var root = document.documentElement;
  root.classList.add("js");

  var reduceMQ = window.matchMedia("(prefers-reduced-motion: reduce)");
  var reduced = reduceMQ.matches;
  var isStatic = /(\?|&)static(=|&|$)/.test(location.search);

  var clamp = function (v, a, b) { return v < a ? a : v > b ? b : v; };
  var lerp = function (a, b, t) { return a + (b - a) * t; };
  var $ = function (s, c) { return (c || document).querySelector(s); };
  var $$ = function (s, c) { return Array.prototype.slice.call((c || document).querySelectorAll(s)); };

  /* ---------------- 入场动画 ---------------- */
  var revealEls = $$(".reveal");

  if (isStatic || reduced || !("IntersectionObserver" in window)) {
    revealEls.forEach(function (el) { el.classList.add("is-in"); });
  } else {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) {
          e.target.classList.add("is-in");
          io.unobserve(e.target);
        }
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.06 });
    revealEls.forEach(function (el) { io.observe(el); });
  }

  /* ---------------- 滚动进度 / 顶栏 / 回到顶部 ---------------- */
  var bar = $("#progress span");
  var topbar = $("#topbar");
  var toTop = $("#toTop");
  var lastY = 0, scrollQueued = false;

  function onScrollFrame() {
    scrollQueued = false;
    var y = window.scrollY || window.pageYOffset;
    lastY = y;

    var docH = document.documentElement.scrollHeight - window.innerHeight;
    if (bar) bar.style.width = (docH > 0 ? (y / docH) * 100 : 0) + "%";

    if (topbar) topbar.classList.toggle("is-stuck", y > 12);
    if (toTop) toTop.classList.toggle("is-on", y > 640);

    updateSeedMap();
  }
  function requestScroll() {
    if (!scrollQueued) { scrollQueued = true; requestAnimationFrame(onScrollFrame); }
  }


  /* ---------------- 工具箱：左侧切换 ---------------- */
  var bench = $(".bench");
  if (bench) {
    var benchItems = $$(".bench__item", bench);
    var benchPanels = $$(".panel", bench);
    function benchShow(i) {
      benchItems.forEach(function (b, k) {
        b.classList.toggle("is-on", k === i);
        b.setAttribute("aria-selected", k === i ? "true" : "false");
      });
      benchPanels.forEach(function (p, k) { p.classList.toggle("is-on", k === i); });
    }
    benchItems.forEach(function (b, k) {
      b.addEventListener("click", function () { benchShow(k); });
    });
  }
  /* ---------------- 界面画廊：大图 + 缩略图 ---------------- */
  var gStage = $("#galleryStage");
  var gThumbs = $("#galleryThumbs");
  var gCount = $("#galleryCount");
  var gPrev = $("#galleryPrev");
  var gNext = $("#galleryNext");
  var gShots = [], gTabs = [], gIndex = 0;

  function pad2(n) { return (n < 10 ? "0" : "") + n; }

  function gShow(i) {
    if (!gShots.length) return;
    gIndex = (i + gShots.length) % gShots.length;
    gShots.forEach(function (s, k) { s.classList.toggle("is-active", k === gIndex); });
    gTabs.forEach(function (b, k) {
      b.classList.toggle("is-active", k === gIndex);
      b.setAttribute("aria-selected", k === gIndex ? "true" : "false");
    });
    if (gCount) gCount.textContent = pad2(gIndex + 1) + " / " + pad2(gShots.length);
  }

  if (gStage) {
    gShots = $$(".shot", gStage);
    gTabs = gThumbs ? $$(".thumb", gThumbs) : [];
    gTabs.forEach(function (b, k) {
      b.addEventListener("click", function () { gShow(k); });
    });
    if (gPrev) gPrev.addEventListener("click", function () { gShow(gIndex - 1); });
    if (gNext) gNext.addEventListener("click", function () { gShow(gIndex + 1); });
    gStage.addEventListener("keydown", function (e) {
      if (e.key === "ArrowRight") { e.preventDefault(); gShow(gIndex + 1); }
      if (e.key === "ArrowLeft") { e.preventDefault(); gShow(gIndex - 1); }
    });
    gShow(0);

    /* 标题居中开场 → 滚动时滑到左侧并浮出截图 */
    var gPin = $("#galleryPin");
    var gInner = $(".gallery__inner", gallery);
    var gH2 = $(".gallery__aside h2", gallery);
    var gPinOn = false;

    var gPinTarget = 0, gPinCur = 0, gPinRaf = 0;

    function smooth(x) { return x * x * (3 - 2 * x); }

    function applyGalleryVars() {
      var p = gPinCur;
      var t = smooth(clamp(p / 0.55, 0, 1));
      var c = smooth(clamp((p - 0.35) / 0.65, 0, 1));
      gallery.style.setProperty("--p", p.toFixed(4));
      gallery.style.setProperty("--t", t.toFixed(4));
      gallery.style.setProperty("--c", c.toFixed(4));
    }

    function gPinTick() {
      var d = gPinTarget - gPinCur;
      if (Math.abs(d) < 0.0012) {
        gPinCur = gPinTarget;
        applyGalleryVars();
        gPinRaf = 0;
        return;
      }
      gPinCur += d * 0.14;
      applyGalleryVars();
      gPinRaf = requestAnimationFrame(gPinTick);
    }

    function updateGalleryPin() {
      if (!gPinOn || !gPin) return;
      var hold = window.innerHeight * 0.30;
      var len = gPin.offsetHeight - window.innerHeight - hold;
      if (len <= 0) return;
      gPinTarget = clamp(-gPin.getBoundingClientRect().top / len, 0, 1);
      if (!gPinRaf) gPinRaf = requestAnimationFrame(gPinTick);
    }

    function layoutGalleryPin() {
      if (!gPin || !gInner || !gH2) return;
      gPinOn = window.innerWidth > 960 && !reduced && !isStatic;
      gallery.classList.toggle("is-pin", gPinOn);
      if (!gPinOn) {
        gPinCur = 1; gPinTarget = 1;
        gallery.style.setProperty("--p", "1");
        gallery.style.setProperty("--t", "1");
        gallery.style.setProperty("--c", "1");
        gallery.style.removeProperty("--shift");
        return;
      }
      var prev = gallery.style.getPropertyValue("--p") || "1";
      gallery.style.setProperty("--p", "1");
      var ri = gInner.getBoundingClientRect();
      var rh = gH2.getBoundingClientRect();
      gallery.style.setProperty("--shift", ((ri.left + ri.width / 2) - (rh.left + rh.width / 2)).toFixed(1) + "px");
      gallery.style.setProperty("--p", prev);
      updateGalleryPin();
    }

    var gPinQueued = false;
    window.addEventListener("scroll", function () {
      if (!gPinOn || gPinQueued) return;
      gPinQueued = true;
      requestAnimationFrame(function () { gPinQueued = false; updateGalleryPin(); });
    }, { passive: true });
    window.addEventListener("resize", layoutGalleryPin);
    layoutGalleryPin();
  }
  /* ---------------- 种子地图视差 ---------------- */
  var seedmap = $("#seedmap");
  var seedmapImg = $("#seedmapImg");
  function updateSeedMap() {
    if (!seedmap || !seedmapImg || reduced) return;
    var r = seedmap.getBoundingClientRect();
    if (r.bottom < -200 || r.top > window.innerHeight + 200) return;
    var p = clamp((window.innerHeight - r.top) / (window.innerHeight + r.height), 0, 1);
    seedmapImg.style.transform = "translate3d(0," + ((p - 0.5) * -46).toFixed(1) + "px,0) scale(1.05)";
  }

  /* ---------------- 首屏视差 ---------------- */
  var hero = $("#heroStage");
  var floatBlocks = $$(".float-block");
  var parallaxEls = $$("[data-depth]");
  var targetX = 0, targetY = 0, curX = 0, curY = 0, parallaxOn = false;

  if (hero && !reduced && window.matchMedia("(hover: hover) and (pointer: fine)").matches) {
    hero.addEventListener("mousemove", function (e) {
      var r = hero.getBoundingClientRect();
      targetX = (e.clientX - r.left) / r.width - 0.5;
      targetY = (e.clientY - r.top) / r.height - 0.5;
      if (!parallaxOn) { parallaxOn = true; requestAnimationFrame(parallaxLoop); }
    });
    hero.addEventListener("mouseleave", function () {
      targetX = 0; targetY = 0;
      if (!parallaxOn) { parallaxOn = true; requestAnimationFrame(parallaxLoop); }
    });
  }

  function parallaxLoop() {
    curX = lerp(curX, targetX, 0.08);
    curY = lerp(curY, targetY, 0.08);

    parallaxEls.forEach(function (el) {
      var d = parseFloat(el.getAttribute("data-depth") || "0");
      var x = curX * d, y = curY * d;
      if (el.classList.contains("float-block")) {
        el.style.transform = "translate3d(" + x.toFixed(2) + "px," + y.toFixed(2) + "px,0)";
      } else {
        el.style.translate = x.toFixed(2) + "px " + y.toFixed(2) + "px";
      }
    });

    var settled = Math.abs(curX - targetX) < 0.0008 && Math.abs(curY - targetY) < 0.0008;
    if (settled) { parallaxOn = false; return; }
    requestAnimationFrame(parallaxLoop);
  }

  /* ---------------- 数字滚动 ---------------- */
  var counters = $$("[data-count]");
  function runCounter(el) {
    if (el.dataset.plain) return;
    var end = parseFloat(el.dataset.count);
    if (isNaN(end)) return;
    var start = performance.now(), dur = 1100;
    function step(now) {
      var t = clamp((now - start) / dur, 0, 1);
      var eased = 1 - Math.pow(1 - t, 3);
      el.textContent = Math.round(end * eased);
      if (t < 1) requestAnimationFrame(step);
      else el.textContent = end;
    }
    requestAnimationFrame(step);
  }
  if (counters.length) {
    if (reduced || !("IntersectionObserver" in window)) {
      /* 保持静态值 */
    } else {
      var cio = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
          if (e.isIntersecting) { runCounter(e.target); cio.unobserve(e.target); }
        });
      }, { threshold: 0.6 });
      counters.forEach(function (el) { cio.observe(el); });
    }
  }

  /* ---------------- 导航高亮 ---------------- */
  var navLinks = $$(".nav a");
  var sections = navLinks.map(function (a) { return $(a.getAttribute("href")); }).filter(Boolean);
  if ("IntersectionObserver" in window && sections.length) {
    var nio = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) return;
        var id = "#" + e.target.id;
        navLinks.forEach(function (a) { a.classList.toggle("is-active", a.getAttribute("href") === id); });
      });
    }, { rootMargin: "-45% 0px -50% 0px", threshold: 0 });
    sections.forEach(function (s) { nio.observe(s); });
  }

  /* ---------------- 主题换色 ---------------- */
  var swatchWrap = $("#swatches");
  var THEMES = ["grass","bamboo","mint","deep","purple","rose","sakura","vermil","dusk","caramel","amber","gold","wheat","ink"];
  var stored = null;
  try { stored = localStorage.getItem("yql-site-theme"); } catch (err) {}

  function applyTheme(name) {
    if (THEMES.indexOf(name) < 0) name = "grass";
    root.setAttribute("data-theme", name);
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      var c = getComputedStyle(root).getPropertyValue("--accent").trim();
      if (c) meta.setAttribute("content", c);
    }
    if (swatchWrap) {
      $$("button", swatchWrap).forEach(function (b) {
        b.setAttribute("aria-pressed", b.dataset.theme === name ? "true" : "false");
      });
    }
  }

  if (stored) applyTheme(stored);
  if (swatchWrap) {
    swatchWrap.addEventListener("click", function (e) {
      var btn = e.target.closest("button[data-theme]");
      if (!btn) return;
      var name = btn.dataset.theme;
      applyTheme(name);
      try { localStorage.setItem("yql-site-theme", name); } catch (err) {}
    });
  }

  /* ---------------- 移动菜单 ---------------- */
  var menuBtn = $("#menuBtn");
  var menu = $("#mobileMenu");
  var menuOpen = false;

  function setMenu(open) {
    if (!menu || !menuBtn) return;
    menuOpen = open;
    menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
    menuBtn.setAttribute("aria-label", open ? "关闭导航" : "打开导航");
    if (open) {
      menu.hidden = false;
      requestAnimationFrame(function () { menu.classList.add("is-open"); });
      root.classList.add("no-scroll");
    } else {
      menu.classList.remove("is-open");
      root.classList.remove("no-scroll");
      window.setTimeout(function () { if (!menuOpen) menu.hidden = true; }, 340);
    }
  }

  if (menuBtn) {
    menuBtn.addEventListener("click", function () { setMenu(!menuOpen); });
  }
  if (menu) {
    $$("a", menu).forEach(function (a) { a.addEventListener("click", function () { setMenu(false); }); });
  }
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && menuOpen) setMenu(false);
  });

  /* ---------------- 回到顶部 ---------------- */
  if (toTop) {
    toTop.addEventListener("click", function () {
      window.scrollTo({ top: 0, behavior: reduced ? "auto" : "smooth" });
    });
  }

  /* ---------------- 事件绑定 ---------------- */
  window.addEventListener("scroll", requestScroll, { passive: true });
  window.addEventListener("resize", function () {

    requestScroll();
  });

  if (reduceMQ.addEventListener) {
    reduceMQ.addEventListener("change", function (e) {
      reduced = e.matches;

      requestScroll();
    });
  }

  /* 字体加载完成后重新量一次画廊宽度 */
  if (document.fonts && document.fonts.ready) {
  }

  window.addEventListener("load", function () {

    requestScroll();
  });

  requestScroll();
})();

/* ============================================================
   最新版本号：页面里不写死，运行时自动取
   - 先问 GitHub Releases（权威），拿不到再问 jsDelivr 的标签列表
   - 页面元素用 data-version="模板 {ver}" 标记，拿到版本号后替换
   - 带 data-version-hide 的元素在拿不到版本号时整体隐藏
   - 结果缓存 1 小时，避免频繁请求
   ============================================================ */
(function () {
  "use strict";

  var REPO = "YuanzaiC/YuanQi-Launcher";
  var TITLE_BASE = "缘启 YuanQi Launcher";
  var TITLE_TAIL = " · 为 Minecraft 重做的启动器";
  var CACHE_KEY = "yql-site-version";
  var CACHE_TTL = 60 * 60 * 1000;

  var $$ = function (s) { return Array.prototype.slice.call(document.querySelectorAll(s)); };
  var clean = function (tag) {
    var v = String(tag || "").trim().replace(/^v/i, "");
    return /^\d+(\.\d+){1,3}$/.test(v) ? v : "";
  };

  function setVersion(ver) {
    $$("[data-version]").forEach(function (el) {
      var tpl = el.getAttribute("data-version") || "";
      el.textContent = tpl.replace(/\{ver\}/g, ver);
      el.removeAttribute("hidden");
    });
    $$("[data-version-hide]").forEach(function (el) { el.removeAttribute("hidden"); });
    document.title = TITLE_BASE + " " + ver + TITLE_TAIL;
    var og = document.querySelector('meta[property="og:title"]');
    if (og) og.setAttribute("content", TITLE_BASE + " " + ver);
  }

  function clearVersion() {
    $$("[data-version-hide]").forEach(function (el) { el.setAttribute("hidden", ""); });
  }

  function readCache() {
    try {
      var raw = localStorage.getItem(CACHE_KEY);
      if (!raw) return null;
      var o = JSON.parse(raw);
      if (!o || !o.v || (Date.now() - o.t) > CACHE_TTL) return null;
      return o.v;
    } catch (err) {
      return null;
    }
  }

  function writeCache(ver) {
    try { localStorage.setItem(CACHE_KEY, JSON.stringify({ v: ver, t: Date.now() })); } catch (err) {}
  }

  function fromGitHub() {
    return fetch("https://api.github.com/repos/" + REPO + "/releases/latest", {
      headers: { Accept: "application/vnd.github+json" },
      cache: "no-store"
    })
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (d) { return clean(d && d.tag_name); });
  }

  function fromJsDelivr() {
    return fetch("https://data.jsdelivr.com/v1/packages/gh/" + REPO, { cache: "no-store" })
      .then(function (r) {
        if (!r.ok) throw new Error("HTTP " + r.status);
        return r.json();
      })
      .then(function (d) {
        var list = (d && d.versions) || [];
        for (var i = 0; i < list.length; i++) {
          var v = clean(list[i] && list[i].version);
          if (v) return v;
        }
        return "";
      });
  }

  function load() {
    var cached = readCache();
    if (cached) { setVersion(cached); return; }
    if (!window.fetch) { clearVersion(); return; }

    fromGitHub()
      .catch(function () { return fromJsDelivr(); })
      .then(function (ver) {
        if (!ver) throw new Error("no version");
        writeCache(ver);
        setVersion(ver);
      })
      .catch(function () {
        /* 两个源都拿不到就不写版本号，别让页面挂着一个过期的数字 */
        clearVersion();
      });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", load);
  } else {
    load();
  }
})();
