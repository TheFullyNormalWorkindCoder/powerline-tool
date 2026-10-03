/* Powerline Tool UI. Talks to the C# host through window.chrome.webview; falls back to demo data in a normal browser. */
(() => {
  "use strict";

  const host = window.chrome && window.chrome.webview;
  const MOCK = !host;
  const params = new URLSearchParams(location.search);

  const $ = (s, e = document) => e.querySelector(s);
  const $$ = (s, e = document) => [...e.querySelectorAll(s)];
  const esc = s => String(s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  // ---------------------------------------------------------------- state
  let S = null;                  // last state from the host
  let page = params.get("page") || "devices";
  let histLink = null, histRange = "5m";
  let drawerOpen = false;
  const logLines = [];
  const prevVals = new Map();    // "mac|peer|tx" -> number, so bars and numbers animate from the old value
  const openCards = new Set();
  let seenEventTs = null;
  let pageSig = null, pendingRender = false;
  let devSort = "default";       // default | name | speed
  let palSel = 0, palQuery = "";

  // ---------------------------------------------------------------- i18n
  function lang() {
    const l = (S && S.settings && S.settings.language) || params.get("lang") || "auto";
    if (l === "en" || l === "hr") return l;
    return (navigator.language || "en").toLowerCase().startsWith("hr") ? "hr" : "en";
  }
  function t(key, ...a) {
    let s = (I18N[lang()][key] ?? I18N.en[key] ?? key);
    a.forEach((v, i) => { s = s.split("{" + i + "}").join(v); });
    return s;
  }

  // ---------------------------------------------------------------- icons
  const P = {
    devices: '<path d="M9 2v6M15 2v6"/><path d="M6 8h12v4a6 6 0 0 1-12 0z"/><path d="M12 18v4"/>',
    map: '<circle cx="6" cy="12" r="2.6"/><circle cx="18" cy="6" r="2.6"/><circle cx="18" cy="18" r="2.6"/><path d="M8.3 10.9l7.4-3.7M8.3 13.1l7.4 3.7"/>',
    history: '<path d="M3 12h4l3-8 4 16 3-8h4"/>',
    settings: '<path d="M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1"/><circle cx="15" cy="6" r="2"/><circle cx="9" cy="12" r="2"/><circle cx="17" cy="18" r="2"/>',
    info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5h.01"/>',
    refresh: '<path d="M21 12a9 9 0 1 1-3-6.7"/><path d="M21 4v5h-5"/>',
    copy: '<rect x="9" y="9" width="11" height="11" rx="2"/><path d="M5 15V6a2 2 0 0 1 2-2h9"/>',
    pencil: '<path d="M4 20h4L19 9l-4-4L4 16z"/>',
    download: '<path d="M12 4v11M7 11l5 5 5-5M5 20h14"/>',
    list: '<path d="M4 6h16M4 12h16M4 18h10"/>',
    alert: '<path d="M12 3l10 18H2z"/><path d="M12 10v4.5M12 17.5h.01"/>',
    check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
    x: '<path d="M6 6l12 12M18 6L6 18"/>',
    arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
    chart: '<path d="M4 4v16h16"/><path d="M8 15l4-5 3 3 5-7"/>',
    sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
    moon: '<path d="M21 13.5A8.5 8.5 0 1 1 10.5 3a7 7 0 0 0 10.5 10.5z"/>',
    monitor: '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>',
    pause: '<path d="M8 5v14M16 5v14"/>',
    play: '<path d="M7 4l13 8-13 8z"/>',
    pin: '<path d="M9 3h6l-1 6 3 3H7l3-3z"/><path d="M12 12v9"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/>',
    sort: '<path d="M7 4v16M3 16l4 4 4-4M17 20V4M13 8l4-4 4 4"/>',
    down: '<path d="M6 9l6 6 6-6"/>',
    up: '<path d="M6 15l6-6 6 6"/>',
    file: '<path d="M6 3h8l4 4v14H6z"/><path d="M14 3v4h4M9 13h6M9 17h6"/>',
    trash: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>',
    keyboard: '<rect x="2" y="6" width="20" height="12" rx="2"/><path d="M6 10h.01M10 10h.01M14 10h.01M18 10h.01M7 14h10"/>',
    bell: '<path d="M6 16V11a6 6 0 0 1 12 0v5l2 2H4z"/><path d="M10 21h4"/>',
    link: '<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>',
  };
  const icon = (n, size) => `<svg viewBox="0 0 24 24" ${size ? `width="${size}" height="${size}"` : ""} aria-hidden="true">${P[n] || ""}</svg>`;

  const LOGO = `<svg class="logo" viewBox="0 0 64 64" aria-hidden="true"><defs><linearGradient id="lg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="var(--a1)"/><stop offset="1" stop-color="var(--a2)"/></linearGradient></defs>
    <rect width="64" height="64" rx="16" fill="url(#lg)"/><path d="M19 32c4-12 7-12 13 0s9 12 13 0" fill="none" stroke="#fff" stroke-width="3.6" stroke-linecap="round"/>
    <circle cx="13" cy="32" r="6.5" fill="#fff"/><circle cx="13" cy="32" r="2.8" fill="var(--a1)"/><circle cx="51" cy="32" r="6.5" fill="#fff"/><circle cx="51" cy="32" r="2.8" fill="var(--a1)"/></svg>`;

  // ---------------------------------------------------------------- helpers
  const qOf = v => v >= 200 ? "excellent" : v >= 100 ? "good" : v >= 50 ? "fair" : "poor";
  const pct = v => Math.max(3, Math.min(100, v / 450 * 100));
  const nameOf = mac => {
    if (!S) return shortName(mac);
    const d = S.devices.find(x => x.mac === mac);
    if (d) return d.name;
    for (const x of S.devices) { const l = x.links.find(l => l.peer === mac); if (l && l.peerName) return l.peerName; }
    return shortName(mac);
  };
  const shortName = mac => { const p = mac.split(":"); return "Device_" + (p[p.length - 2] + p[p.length - 1]).toLowerCase(); };

  function ago(ts) {
    if (!ts) return "–";
    const s = Math.max(0, Math.round((Date.now() - ts) / 1000));
    if (s < 4) return t("time.now");
    if (s < 60) return t("time.s", s);
    if (s < 3600) return t("time.m", Math.floor(s / 60));
    return t("time.h", Math.floor(s / 3600));
  }

  /** Unique physical links, with the rates as the first reporting end sees them. */
  /** An adapter reports 0/0 for a partner it lists but cannot reach: that is "no link", not "slow". */
  const isDown = l => l.tx === 0 && l.rx === 0;

  function linkPairs(devices) {
    const map = new Map();
    for (const d of devices) for (const l of d.links) {
      const key = d.mac < l.peer ? d.mac + "|" + l.peer : l.peer + "|" + d.mac;
      const rec = { a: d.mac, b: l.peer, tx: l.tx, rx: l.rx, min: Math.min(l.tx, l.rx), avg: (l.tx + l.rx) / 2, down: isDown(l) };
      const have = map.get(key);
      if (!have || (have.down && !rec.down)) map.set(key, rec);   // prefer a live reading over a dead one
    }
    return [...map.values()];
  }

  /** Real adapters plus placeholders for partners the local adapter lists but that never answered. */
  function viewDevices() {
    const known = new Set(S.devices.map(d => d.mac)), seen = new Set(), ghosts = [];
    for (const d of S.devices) for (const l of d.links) {
      if (known.has(l.peer) || seen.has(l.peer)) continue;
      seen.add(l.peer);
      ghosts.push({ mac: l.peer, name: l.peerName || shortName(l.peer), local: false, role: "", firmware: "", nid: "", tei: 0, nic: "", stale: false, ghost: true, missed: 0, linksStale: false, lastSeen: 0, links: [] });
    }
    return S.devices.concat(ghosts);
  }

  function summary() {
    const devices = viewDevices(), pairs = linkPairs(S.devices), up = pairs.filter(p => !p.down), down = pairs.length - up.length;
    const base = { devices: devices.length, links: pairs.length, down };
    if (!pairs.length) return { ...base, avg: 0, weakest: null, fastest: null, health: devices.length ? "alone" : "none" };
    if (!up.length) return { ...base, avg: 0, weakest: null, fastest: null, health: "down" };
    const weakest = up.reduce((m, p) => p.min < m.min ? p : m);
    const fastest = up.reduce((m, p) => p.min > m.min ? p : m);
    const avg = up.reduce((s, p) => s + p.avg, 0) / up.length;
    const health = down ? "down" : weakest.min >= 100 ? "good" : weakest.min >= 50 ? "fair" : "poor";
    return { ...base, avg, weakest, fastest, health };
  }

  function send(cmd, payload) {
    if (host) host.postMessage(Object.assign({ cmd }, payload || {}));
    else Mock.handle(cmd, payload || {});
  }

  // ---------------------------------------------------------------- toasts / feed
  function toast(kind, title, text) {
    const el = document.createElement("div");
    el.className = "toast " + kind;
    el.innerHTML = `${icon(kind === "good" ? "check" : kind === "bad" ? "alert" : kind === "warn" ? "alert" : "info")}<div><b>${esc(title)}</b>${text ? `<span>${esc(text)}</span>` : ""}</div>`;
    $("#toasts").appendChild(el);
    setTimeout(() => { el.classList.add("out"); setTimeout(() => el.remove(), 320); }, 5200);
  }

  function eventText(e) {
    const n = m => nameOf(m);
    switch (e.kind) {
      case "DeviceAppeared": return t("ev.appeared", n(e.mac));
      case "DeviceLost": return t("ev.lost", n(e.mac));
      case "LinkLow": return t("ev.low", n(e.mac), n(e.peer), e.mbps);
      default: return t("ev.recovered", n(e.mac), n(e.peer), e.mbps);
    }
  }
  const eventTone = e => (e.kind === "DeviceLost" || e.kind === "LinkLow") ? "bad" : "good";

  // ---------------------------------------------------------------- theme
  function applyLook() {
    const s = S ? S.settings : {};
    const th = params.get("theme") || s.theme || "system";
    const dark = th === "dark" || (th === "system" && matchMedia("(prefers-color-scheme: dark)").matches);
    const root = document.documentElement;
    root.dataset.theme = dark ? "dark" : "light";
    root.dataset.accent = params.get("accent") || s.accent || "teal";
    root.lang = lang();
    document.body.classList.toggle("reduce-motion", !!s.reduceMotion);
  }
  matchMedia("(prefers-color-scheme: dark)").addEventListener("change", applyLook);

  // ---------------------------------------------------------------- shell
  const NAV = [["devices", "nav.devices"], ["map", "nav.map"], ["history", "nav.history"], ["settings", "nav.settings"]];
  const themeIcon = () => { const th = S && S.settings.theme; return th === "light" ? "sun" : th === "dark" ? "moon" : "monitor"; };
  const nextTheme = () => ({ system: "light", light: "dark", dark: "system" }[(S && S.settings.theme) || "system"]);

  function renderRail() {
    const warn = S && S.warnings && S.warnings.some(w => w === "npcap_missing");
    $("#rail").innerHTML = LOGO + NAV.map(([id, k]) =>
      `<button class="nav ${page === id ? "active" : ""}" data-act="page" data-page="${id}" aria-label="${esc(t(k))}" ${page === id ? 'aria-current="page"' : ""}>${icon(id)}<span>${esc(t(k))}</span>${id === "devices" && warn ? '<i class="dot"></i>' : ""}</button>`
    ).join("") + `<div class="spacer"></div>` +
      `<button class="nav small ${S && S.settings.alwaysOnTop ? "active" : ""}" data-act="pin" aria-label="${esc(t("btn.pin"))}" title="${esc(t("btn.pin"))}">${icon("pin")}<span>${esc(t("btn.pin"))}</span></button>` +
      `<button class="nav small" data-act="theme" aria-label="${esc(t("btn.theme"))}" title="${esc(t("btn.theme"))}">${icon(themeIcon())}<span>${esc(t("btn.theme"))}</span></button>` +
      `<button class="nav ${drawerOpen ? "active" : ""}" data-act="drawer" aria-label="${esc(t("btn.logs"))}">${icon("list")}<span>${esc(t("btn.logs"))}</span></button>` +
      `<button class="nav ${page === "about" ? "active" : ""}" data-act="page" data-page="about" aria-label="${esc(t("nav.about"))}">${icon("info")}<span>${esc(t("nav.about"))}</span></button>`;
  }

  function chipState() {
    if (S.scanning) return ["busy", t("status.busy")];
    if (!S.devices.length) return ["off", t("status.off")];
    return ["live", t("status.live")];
  }

  function renderTop() {
    const [cls, label] = chipState();
    const st = S.settings;
    const ring = S.nextScanAt && st.autoRefresh ? `<svg class="countdown" viewBox="0 0 26 26" aria-hidden="true"><circle class="bg" cx="13" cy="13" r="10"/><circle class="fg" cx="13" cy="13" r="10" stroke-dasharray="62.8" stroke-dashoffset="0"/></svg>` : "";
    const showScan = page !== "about" && page !== "settings";
    $("#top").innerHTML = `
      <div><h1>${esc(t("top." + page))}</h1><div class="sub">${esc(t("sub." + page))}</div></div>
      <div class="actions">
        ${MOCK ? `<span class="chip">${esc(t("mock"))}</span>` : ""}
        <span class="chip ${cls}" id="chip"><i class="pulse"></i><span id="chipText">${esc(label)}</span>${ring}</span>
        ${showScan ? `<button class="btn" data-act="auto" title="${esc(st.autoRefresh ? t("btn.pause") : t("btn.resume"))}" aria-label="${esc(st.autoRefresh ? t("btn.pause") : t("btn.resume"))}">${icon(st.autoRefresh ? "pause" : "play")}<span>${esc(st.autoRefresh ? t("btn.pause") : t("btn.resume"))}</span></button>` : ""}
        <button class="icon-btn" data-act="palette" title="${esc(t("btn.palette"))} (Ctrl+K)" aria-label="${esc(t("btn.palette"))}">${icon("search")}</button>
        ${showScan ? `<button class="btn primary" data-act="scan" id="scanBtn" ${S.scanning ? "disabled" : ""}><span class="${S.scanning ? "spin" : ""}" style="display:inline-flex">${icon("refresh")}</span><span>${esc(S.scanning ? t("btn.scanning") : t("btn.scan"))}</span></button>` : ""}
      </div>`;
    tickClock();
  }

  /** Cheap once-a-second update of the status chip text and countdown ring. */
  function tickClock() {
    if (!S) return;
    const txt = $("#chipText");
    if (txt) {
      const [, label] = chipState();
      let extra = "", tip = "";
      if (!S.scanning && S.lastScan) extra = " · " + t("status.ago", ago(S.lastScan));
      if (!S.scanning && S.settings.autoRefresh && S.nextScanAt) {
        const left = Math.max(0, Math.ceil((S.nextScanAt - Date.now()) / 1000));
        tip = t("status.next", left);   // the ring shows the same thing; the words go in the tooltip to keep the header short
      }
      txt.textContent = label + extra;
      const chip = $("#chip"); if (chip) chip.title = tip;
    }
    const fg = $("#chip .fg");
    if (fg && S.nextScanAt && S.settings.autoRefresh) {
      const total = S.settings.intervalSec * 1000, left = Math.max(0, S.nextScanAt - Date.now());
      fg.style.strokeDashoffset = String(62.8 * (1 - Math.min(1, left / total)));
    }
    $$("[data-ago]").forEach(el => { el.textContent = t("dev.lastSeen", ago(+el.dataset.ago)); });
  }
  setInterval(tickClock, 1000);

  function renderBanners() {
    const w = S.warnings || [], out = [];
    const b = (cls, ic, title, text, extra = "") => out.push(`<div class="banner ${cls}">${icon(ic)}<div class="grow"><b>${esc(title)}</b><span>${esc(text)}</span></div>${extra}</div>`);
    if (w.includes("npcap_missing")) b("err", "alert", t("warn.npcap.t"), t("warn.npcap.d"));
    if (w.includes("tpplc_running")) b("warn", "alert", t("warn.tpplc.t"), t("warn.tpplc.d"));
    if (!S.devices.length && w.includes("no_nic")) b("warn", "alert", t("warn.no_nic.t"), t("warn.no_nic.d"));
    if (!S.devices.length && w.includes("no_reply")) b("warn", "alert", t("warn.no_reply.t"), t("warn.no_reply.d", (S.skipped || []).join(", ") || "–"));
    if (S.devices.some(d => d.stale)) b("info", "info", t("warn.stale.t"), t("warn.stale.d"));
    $("#banners").innerHTML = out.join("");
  }

  // ---------------------------------------------------------------- pages
  function render() {
    if (!S) return;
    applyLook();
    renderRail(); renderTop(); renderBanners(); renderDrawer();
    const active = document.activeElement;
    if (active && active.closest && active.closest("#page") && /INPUT|SELECT/.test(active.tagName) && active.type !== "range" && active.type !== "checkbox") { pendingRender = true; return; }
    pendingRender = false;
    const sig = page;
    const first = pageSig !== sig;
    pageSig = sig;
    const el = $("#page");
    el.classList.toggle("still", !first);
    const scroll = $(".main").scrollTop;
    el.innerHTML = ({ devices: pageDevices, map: pageMap, history: pageHistory, settings: pageSettings, about: pageAbout }[page] || pageDevices)();
    afterPage();
    if (!first) $(".main").scrollTop = scroll;
  }

  // ----- devices
  function pageDevices() {
    if (!S.lastScan && S.scanning) return `<div class="skeleton"></div><div class="skeleton" style="height:140px"></div>`;
    if (!S.devices.length) return pageEmpty();
    const sm = summary();
    const healthTitle = t("hero.title." + sm.health);
    const sub = sm.health === "good" ? t("hero.sub.good", 100) : t("hero.sub." + sm.health);
    const upCount = sm.links - sm.down;
    const gauge = Charts.gauge(sm.avg / 300, `<div class="num">${upCount > 0 ? Math.round(sm.avg) : "–"}</div><div class="unit">${esc(t("gauge.avg"))}</div>`);
    const w = sm.weakest, f = sm.fastest;
    const stat = (k, v, unit = "") => `<div class="stat"><div class="k">${esc(k)}</div><div class="v">${v}${unit ? `<small>${unit}</small>` : ""}</div></div>`;
    const hero = `<div class="card hero">${gauge}<div>
        <h2>${esc(healthTitle)} <span class="health ${sm.health}">${esc(t("health." + sm.health))}</span></h2><p>${esc(sub)}</p>
        <div class="stats">${stat(t("stat.devices"), sm.devices)}${stat(t("stat.links"), sm.down ? `${upCount}<small>/ ${sm.links}</small>` : sm.links)}
          ${stat(t("stat.weakest"), w ? w.min : "–", w ? "Mbps" : "")}${stat(t("stat.fastest"), f ? f.min : "–", f ? "Mbps" : "")}</div></div></div>`;
    const skipped = S.nics && S.nics.length ? `<p class="muted" style="margin:14px 4px 0;font-size:12.5px">${esc(t("warn.skipped", S.nics.join(", ")))}</p>` : "";
    const sortBtn = (id, key) => `<button class="${devSort === id ? "on" : ""}" data-act="sort" data-sort="${id}">${esc(t(key))}</button>`;
    const tools = `<div class="tools"><span class="lbl">${icon("sort")}${esc(t("tools.sort"))}</span>
      <div class="seg" role="group">${sortBtn("default", "tools.sort.default")}${sortBtn("name", "tools.sort.name")}${sortBtn("speed", "tools.sort.speed")}</div><span class="grow"></span>
      <button class="btn sm" data-act="expand-all" title="${esc(t("tools.expand"))}" aria-label="${esc(t("tools.expand"))}">${icon("down")}</button>
      <button class="btn sm" data-act="collapse-all" title="${esc(t("tools.collapse"))}" aria-label="${esc(t("tools.collapse"))}">${icon("up")}</button>
      <button class="btn sm" data-act="copy-summary">${icon("copy")}<span>${esc(t("tools.copySummary"))}</span></button>
      <div class="seg" role="group" aria-label="${esc(t("btn.export"))}" title="${esc(t("btn.export"))}"><button data-act="export" data-format="csv">${icon("download")}CSV</button><button data-act="export" data-format="json">JSON</button></div></div>`;
    return hero + tools + `<div class="grid">${sortedDevices().map(deviceCard).join("")}</div>` + skipped;
  }

  function sortedDevices() {
    const d = viewDevices();
    const weakest = x => x.links.length ? Math.min(...x.links.map(l => Math.min(l.tx, l.rx))) : (x.ghost ? -1 : Infinity);
    if (devSort === "name") d.sort((a, b) => a.name.localeCompare(b.name));
    else if (devSort === "speed") d.sort((a, b) => weakest(a) - weakest(b));
    return d;
  }

  /** Plain-text summary for pasting into a chat or a note. */
  function summaryText() {
    const sm = summary();
    const lines = [`Powerline Tool ${S.app.version} · ${new Date().toLocaleString()}`,
      t("stat.devices") + ": " + sm.devices + " · " + t("stat.links") + ": " + sm.links + (sm.links ? " · " + t("stat.avg") + ": " + Math.round(sm.avg) + " Mbps" : "")];
    for (const d of S.devices) {
      lines.push(`${d.name} (${d.mac})${d.local ? " [" + t("dev.local") + "]" : ""}${d.stale ? " [" + t("dev.stale") + "]" : ""}`);
      for (const l of d.links) lines.push(`  → ${nameOf(l.peer)}: TX ${l.tx} / RX ${l.rx} Mbps (${t("q." + qOf(Math.min(l.tx, l.rx)))})`);
    }
    return lines.join("\n");
  }

  /** What to paste into a GitHub issue. MAC addresses keep only the manufacturer part. */
  function diagnosticsText() {
    const mask = m => m.split(":").slice(0, 3).join(":") + ":xx:xx:xx";
    const L = [`Powerline Tool ${S.app.version}${S.app.demo ? " (demo)" : ""}`, `UI language: ${lang()} · browser: ${navigator.language}`,
      `Network cards used: ${(S.nics || []).join(", ") || "-"}`, `Skipped: ${(S.skipped || []).join("; ") || "-"}`, `Warnings: ${(S.warnings || []).join(", ") || "-"}`,
      `Last scan: ${S.lastScan ? new Date(S.lastScan).toISOString() : "-"} · auto-refresh: ${S.settings.autoRefresh ? S.settings.intervalSec + " s" : "off"}`, `Adapters: ${S.devices.length}`];
    for (const d of S.devices) {
      L.push(`- ${mask(d.mac)}${d.local ? " local" : ""}${d.stale ? " stale" : ""} fw=${d.firmware || "-"} tei=${d.tei} role=${d.role || "-"}`);
      for (const l of d.links) L.push(`    -> ${mask(l.peer)} TX ${l.tx} RX ${l.rx}`);
    }
    L.push("", "Log (last lines):", ...logLines.slice(-25).map(x => x.replace(/([0-9A-Fa-f]{2}[:-]){3}([0-9A-Fa-f]{2}[:-]){2}[0-9A-Fa-f]{2}/g, m => m.slice(0, 8) + ":xx:xx:xx")));
    return L.join("\n");
  }

  function deviceCard(d, i) {
    const badges = [
      `<span class="badge ${d.local ? "local" : ""}">${esc(d.local ? t("dev.local") : t("dev.remote"))}</span>`,
      d.ghost ? `<span class="badge stale">${esc(t("dev.offline"))}</span>` : "",
      d.stale ? `<span class="badge stale">${esc(t("dev.stale"))}</span>` : "",
      d.firmware ? `<span class="badge fw" title="${esc(t("dev.firmware"))}">${esc(d.firmware)}</span>` : "",
    ].join("");
    const links = d.links.length ? d.links.map(l => linkRow(d, l)).join("") : `<div class="nolinks">${esc(d.ghost ? t("dev.ghost") : t("dev.nolinks"))}</div>`;
    const meta = [[t("dev.nid"), d.nid || "–"], [t("dev.tei"), d.tei], [t("dev.role"), d.role || "–"], [t("dev.via"), d.nic || "–"]]
      .map(([k, v]) => `<span>${esc(k)}: <b>${esc(v)}</b></span>`).join("");
    return `<article class="card dev ${d.stale || d.ghost ? "is-stale" : ""} ${openCards.has(d.mac) ? "open" : ""}" data-mac="${esc(d.mac)}" style="animation-delay:${i * 70}ms">
      <div class="dev-head">
        <div class="avatar ${d.local ? "" : "remote"}">${icon("devices")}</div>
        <div class="dev-id">
          <div class="dev-name"><span class="nm">${esc(d.name)}</span><button class="icon-btn" data-act="rename" data-mac="${esc(d.mac)}" title="${esc(t("dev.rename"))}" aria-label="${esc(t("dev.rename"))}">${icon("pencil")}</button></div>
          <div class="dev-mac mono">${esc(d.mac)}<button class="icon-btn" data-act="copy" data-text="${esc(d.mac)}" title="${esc(t("dev.copyMac"))}" aria-label="${esc(t("dev.copyMac"))}">${icon("copy")}</button></div>
        </div>
        <div class="dev-badges">${badges}</div>
      </div>
      <div class="dev-meta">${meta}</div>
      ${links}
      <div class="dev-foot"><span data-ago="${d.lastSeen || 0}">${esc(t("dev.lastSeen", ago(d.lastSeen)))}${d.linksStale ? " · " + esc(t("dev.linksStale")) : ""}</span>
        <span><button class="btn sm" data-act="history-for" data-mac="${esc(d.mac)}">${icon("chart")}<span>${esc(t("dev.history"))}</span></button>
        <button class="btn sm" data-act="toggle" data-mac="${esc(d.mac)}"><span>${esc(t("btn.details"))}</span></button></span></div>
    </article>`;
  }

  function linkRow(d, l) {
    if (isDown(l)) {
      return `<div class="link down"><div class="peer">${icon("arrow")}<span title="${esc(t("link.to", nameOf(l.peer)))}">${esc(nameOf(l.peer))}</span></div>
        <div class="down-msg">${esc(t("link.down"))}</div><span class="pill down">${esc(t("q.down"))}</span></div>`;
    }
    const q = qOf(Math.min(l.tx, l.rx));
    const series = ((S.history || {})[d.mac < l.peer ? d.mac + "|" + l.peer : l.peer + "|" + d.mac] || []).slice(-30).map(s => Math.min(s[1], s[2]));
    const meter = (label, v, dir) => {
      const key = `${d.mac}|${l.peer}|${dir}`;
      const from = prevVals.has(key) ? prevVals.get(key) : 0;
      return `<div class="meter q-${qOf(v)}"><div class="row"><span>${label}</span><b data-count="${v}" data-from="${from}" data-key="${esc(key)}">${from}<small>Mbps</small></b></div>
        <div class="bar"><i data-w="${pct(v)}" style="width:${from ? pct(from) : 0}%"></i></div></div>`;
    };
    return `<div class="link">
      <div class="peer">${icon("arrow")}<span title="${esc(t("link.to", nameOf(l.peer)))}">${esc(nameOf(l.peer))}</span></div>
      ${meter(t("link.tx"), l.tx, "tx")}${meter(t("link.rx"), l.rx, "rx")}
      ${Charts.spark(series, 84, 30, `var(--${q === "poor" ? "poor" : q === "fair" ? "fair" : "a1"})`)}
      <span class="pill ${q}">${esc(t("q." + q))}</span></div>`;
  }

  function pageEmpty() {
    const art = `<svg class="art" viewBox="0 0 220 130" aria-hidden="true"><defs><linearGradient id="ea" x1="0" x2="1"><stop offset="0" stop-color="var(--a1)"/><stop offset="1" stop-color="var(--a2)"/></linearGradient></defs>
      <path d="M52 66c14-40 28-40 42 0s28 40 42 0 14-26 32-8" fill="none" stroke="url(#ea)" stroke-width="5" stroke-linecap="round" stroke-dasharray="2 12" opacity=".8"/>
      <circle cx="34" cy="66" r="22" fill="var(--card2)" stroke="var(--line)" stroke-width="2"/><path d="M26 56v-6M42 56v-6M23 60h22v6a11 11 0 0 1-22 0z" fill="none" stroke="var(--muted)" stroke-width="2.4" stroke-linecap="round"/>
      <circle cx="186" cy="66" r="22" fill="var(--card2)" stroke="var(--line)" stroke-width="2" stroke-dasharray="4 5"/><text x="186" y="74" text-anchor="middle" font-size="24" fill="var(--faint)" font-weight="700">?</text></svg>`;
    return `<div class="card empty">${art}<h2>${esc(t("empty.title"))}</h2><p>${esc(t("empty.text"))}</p>
      <div class="checks">${[1, 2, 3, 4].map(n => `<div><span class="n">${n}</span><span>${t("empty.c" + n)}</span></div>`).join("")}</div>
      <button class="btn primary" data-act="scan">${icon("refresh")}<span>${esc(t("btn.retry"))}</span></button></div>`;
  }

  // ----- map
  function pageMap() {
    const devs = viewDevices();
    const card = inner => `<div class="card map-card">${inner}</div>`;
    if (!devs.length) return card(`<div class="empty" style="padding-top:140px"><h2>${esc(t("map.empty"))}</h2></div>`);
    const W = 1000, H = 560, cx = W / 2, cy = H / 2 - 10;
    const pos = {};
    const list = [...devs].sort((a, b) => (b.local - a.local) || a.mac.localeCompare(b.mac));
    if (list.length === 1) pos[list[0].mac] = [cx, cy];
    else if (list.length === 2) { pos[list[0].mac] = [cx - 270, cy]; pos[list[1].mac] = [cx + 270, cy]; }
    else list.forEach((d, i) => { const a = -Math.PI / 2 + i * 2 * Math.PI / list.length; pos[d.mac] = [cx + Math.cos(a) * 330, cy + Math.sin(a) * 190]; });

    let edges = "", labels = "";
    const drawn = new Set();
    for (const d of devs) for (const l of d.links) {
      if (!pos[l.peer]) continue;
      const key = d.mac < l.peer ? d.mac + l.peer : l.peer + d.mac;
      if (drawn.has(key)) continue; drawn.add(key);
      const [x1, y1] = pos[d.mac], [x2, y2] = pos[l.peer];
      const dn = isDown(l);
      const q = qOf(Math.min(l.tx, l.rx)), col = dn ? "var(--faint)" : q === "poor" ? "var(--poor)" : q === "fair" ? "var(--fair)" : q === "excellent" ? "var(--a1)" : "var(--good)";
      const wdt = dn ? 3 : Math.min(11, 3 + Math.min(l.tx, l.rx) / 40);
      const dur = Math.max(0.45, 1.7 - Math.min(l.tx, l.rx) / 220);
      edges += dn
        ? `<g><line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${col}" stroke-width="${wdt}" stroke-linecap="round" stroke-dasharray="2 12" opacity=".8"/></g>`
        : `<g><line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${col}" stroke-opacity=".22" stroke-width="${wdt + 6}" stroke-linecap="round"/>
        <line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${col}" stroke-width="${wdt}" stroke-linecap="round" opacity=".55"/>
        <line class="flow" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="#fff" stroke-opacity=".85" stroke-width="${Math.max(2, wdt / 3)}" style="animation-duration:${dur}s"/></g>`;
      const f = list.length === 2 ? 0.5 : 0.58, mx = x1 + (x2 - x1) * f, my = y1 + (y2 - y1) * f;
      const l1 = `${nameOf(d.mac)} → ${nameOf(l.peer)}`, l2 = dn ? t("link.down") : `TX ${l.tx} · RX ${l.rx} Mbps`;
      const tw = Math.max(l1.length * 6.3, l2.length * 7.4) + 28;
      labels += `<g class="edge-label" transform="translate(${mx},${my})"><title>${esc(l1)}: ${esc(l2)}</title>
        <rect x="${-tw / 2}" y="-25" width="${tw}" height="50" rx="14" stroke="${col}"/>
        <text y="-6" text-anchor="middle" style="font-size:10.5px;font-weight:600;fill:var(--muted)">${esc(l1)}</text><text y="14" text-anchor="middle">${esc(l2)}</text></g>`;
    }
    const nodes = list.map(d => {
      const [x, y] = pos[d.mac];
      const grad = d.local ? "url(#nodeLocal)" : "url(#nodeRemote)";
      return `<g class="node" transform="translate(${x},${y})" opacity="${d.stale || d.ghost ? .5 : 1}"><circle class="halo" r="62" fill="${d.local ? "var(--a1)" : "#64748b"}"/>
        <circle r="40" fill="${grad}"/><g transform="translate(-17,-17) scale(1.42)" fill="none" stroke="#fff" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">${P.devices}</g>
        <text class="node-label" y="68" text-anchor="middle">${esc(d.name)}</text>
        <text class="node-sub" y="86" text-anchor="middle">${esc(d.mac)}${d.local ? " · " + esc(t("map.local")) : ""}</text></g>`;
    }).join("");
    const legend = `<div class="map-legend"><span><i style="background:var(--a1)"></i>${esc(t("map.legend.fast"))}</span><span><i style="background:var(--good)"></i>${esc(t("map.legend.good"))}</span><span><i style="background:var(--fair)"></i>${esc(t("map.legend.fair"))}</span><span><i style="background:var(--poor)"></i>${esc(t("map.legend.poor"))}</span></div>`;
    return card(`<svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="xMidYMid meet" role="img" aria-label="${esc(t("top.map"))}">
      <defs><radialGradient id="nodeLocal"><stop offset="0" stop-color="var(--a2)"/><stop offset="1" stop-color="var(--a1)"/></radialGradient>
      <radialGradient id="nodeRemote"><stop offset="0" stop-color="#94a3b8"/><stop offset="1" stop-color="#475569"/></radialGradient></defs>${edges}${nodes}${labels}</svg>${legend}`);
  }

  // ----- history
  function histPairs() {
    return Object.keys(S.history || {}).filter(k => (S.history[k] || []).length).sort();
  }

  function pageHistory() {
    const keys = histPairs();
    if (!keys.length) return `<div class="card empty"><h2>${esc(t("hist.none"))}</h2></div>`;
    if (!histLink || !keys.includes(histLink)) histLink = keys[0];
    const [a, b] = histLink.split("|");
    const opts = keys.map(k => { const [x, y] = k.split("|"); return `<option value="${esc(k)}" ${k === histLink ? "selected" : ""}>${esc(nameOf(x))}  ↔  ${esc(nameOf(y))}</option>`; }).join("");
    const rng = [["1m", 60e3], ["5m", 300e3], ["30m", 1800e3], ["all", Infinity]];
    const seg = rng.map(([id]) => `<button class="${histRange === id ? "on" : ""}" data-act="range" data-range="${id}">${esc(t("hist." + id))}</button>`).join("");
    const lim = rng.find(r => r[0] === histRange)[1];
    const all = S.history[histLink];
    const cutoff = (all[all.length - 1][0]) - lim;
    const samples = lim === Infinity ? all : all.filter(s => s[0] >= cutoff);
    let body;
    if (samples.length < 2) body = `<div class="empty" style="padding:90px 0"><p>${esc(t("hist.empty"))}</p></div>`;
    else body = `<div class="legend"><span><i style="background:var(--a1)"></i>TX  ${esc(nameOf(a))} → ${esc(nameOf(b))}</span><span><i style="background:var(--rx)"></i>RX</span>
      ${S.settings.warnBelowMbps ? `<span style="color:var(--poor)">- - ${esc(t("hist.warn", S.settings.warnBelowMbps))}</span>` : ""}<span style="margin-left:auto">${esc(t("hist.samples", samples.length))}</span></div>
      <div class="chart-host" style="position:relative">${Charts.history(samples, { warn: S.settings.warnBelowMbps, label: t("top.history") }).html}</div>`;
    const col = i => samples.map(s => s[i]);
    const stats = samples.length >= 2 ? (() => {
      const f = (arr, fn) => Math.round(fn(arr)), mn = x => Math.min(...x), mx = x => Math.max(...x), av = x => x.reduce((s, v) => s + v, 0) / x.length;
      const cell = (k, fn) => `<div class="stat"><div class="k">${esc(t(k))}</div><div class="v" style="font-size:19px">${f(col(1), fn)} <small>/</small> ${f(col(2), fn)}<small>TX / RX</small></div></div>`;
      const last = samples[samples.length - 1];
      return `<div class="statrow"><div class="stat"><div class="k">${esc(t("hist.now"))}</div><div class="v" style="font-size:19px">${last[1]} <small>/</small> ${last[2]}<small>TX / RX</small></div></div>${cell("hist.min", mn)}${cell("hist.avg", av)}${cell("hist.max", mx)}</div>`;
    })() : "";
    return `<div class="toolbar"><select id="histSel" aria-label="${esc(t("hist.link"))}">${opts}</select><div class="seg" role="group" aria-label="${esc(t("hist.range"))}">${seg}</div>
      <span style="margin-left:auto;display:flex;gap:8px"><button class="btn sm" data-act="history-csv">${icon("download")}<span>${esc(t("hist.csv"))}</span></button>
      <button class="btn sm" data-act="clear-history">${icon("trash")}<span>${esc(t("btn.clear"))}</span></button></span></div>
      <div class="card chart-card">${body}${stats}</div>`;
  }

  // ----- settings
  function pageSettings() {
    const s = S.settings;
    const sw = (key, checked) => `<label class="switch"><input type="checkbox" data-set="${key}" ${checked ? "checked" : ""}><i></i></label>`;
    const row = (title, desc, ctl) => `<div class="setting"><div class="l"><b>${esc(title)}</b>${desc ? `<span>${esc(desc)}</span>` : ""}</div>${ctl}</div>`;
    const seg = (key, items, cur) => `<div class="seg">${items.map(([v, label]) => `<button class="${cur === v ? "on" : ""}" data-act="set" data-key="${key}" data-val="${v}">${esc(label)}</button>`).join("")}</div>`;
    const accents = ["teal", "blue", "violet", "green", "orange", "pink"].map(a => {
      const c = { teal: "#14b8c7", blue: "#3b82f6", violet: "#8b5cf6", green: "#22c55e", orange: "#f97316", pink: "#ec4899" }[a];
      return `<button class="${s.accent === a ? "on" : ""}" style="background:${c}" data-act="set" data-key="accent" data-val="${a}" aria-label="${a}"></button>`;
    }).join("");
    const range = (key, min, max, val, unit) => `<div class="range"><input type="range" data-set="${key}" min="${min}" max="${max}" value="${val}"><output>${val} ${unit}</output></div>`;
    const kbd = k => `<span class="kbd">${k}</span>`;
    return `<div class="sections">
      <div class="card section"><h3>${esc(t("set.appearance"))}</h3><p>${esc(t("set.appearance.d"))}</p>
        ${row(t("set.theme"), "", seg("theme", [["system", t("set.theme.system")], ["light", t("set.theme.light")], ["dark", t("set.theme.dark")]], s.theme))}
        ${row(t("set.accent"), "", `<div class="swatches">${accents}</div>`)}
        ${row(t("set.language"), t("set.language.note"), seg("language", [["auto", "Auto"], ["en", "English"], ["hr", "Hrvatski"]], s.language))}
        ${row(t("set.motion"), t("set.motion.d"), sw("reduceMotion", s.reduceMotion))}</div>
      <div class="card section"><h3>${esc(t("set.scanning"))}</h3><p>${esc(t("set.scanning.d"))}</p>
        ${row(t("set.auto"), t("set.auto.d"), sw("autoRefresh", s.autoRefresh))}
        ${row(t("set.interval"), "", range("intervalSec", 3, 120, s.intervalSec, "s"))}
        ${row(t("set.warn"), t("set.warn.d"), range("warnBelowMbps", 10, 300, s.warnBelowMbps, "Mbps"))}
        ${row(t("set.notify"), t("set.notify.d"), sw("notify", s.notify))}</div>
      <div class="card section"><h3>${esc(t("set.system"))}</h3><p>${esc(t("set.system.d"))}</p>
        ${row(t("set.tray"), t("set.tray.d"), sw("minimizeToTray", s.minimizeToTray))}
        ${row(t("set.startup"), t("set.startup.d"), sw("startWithWindows", s.startWithWindows))}
        ${row(t("set.top"), t("set.top.d"), sw("alwaysOnTop", s.alwaysOnTop))}
        ${row(t("set.log"), t("set.log.d"), sw("logHistory", s.logHistory))}
        ${row(t("btn.openFolder"), "", `<button class="btn sm" data-act="open-folder">${esc(t("btn.openFolder"))}</button>`)}</div>
      <div class="card section"><h3>${esc(t("set.tools"))}</h3><p>${esc(t("set.tools.d"))}</p>
        ${row(t("set.sniff"), t("set.sniff.d"), sw("sniff", S.sniffing))}
        ${row(t("set.ui"), t("set.ui.d"), sw("classicUi", s.ui === "classic"))}
        ${row(t("set.testNotify"), t("set.testNotify.d"), `<button class="btn sm" data-act="test-notify">${icon("bell")}<span>${esc(t("set.testNotify"))}</span></button>`)}
        ${row(t("set.diag"), t("set.diag.d"), `<span style="display:flex;gap:8px"><button class="btn sm" data-act="copy-diag">${icon("copy")}<span>${esc(t("btn.copy"))}</span></button><button class="btn sm" data-act="save-diag">${icon("file")}<span>${esc(t("btn.save"))}</span></button></span>`)}
        ${row(t("set.reset"), t("set.reset.d"), `<button class="btn sm" data-act="reset-settings">${icon("trash")}<span>${esc(t("set.reset"))}</span></button>`)}</div>
      <div class="card section"><h3>${esc(t("set.shortcuts"))}</h3><p></p>
        ${[[`${kbd("F5")} ${kbd("R")}`, t("sc.scan")], [`${kbd("1")}–${kbd("4")}`, t("sc.pages")], [kbd("L"), t("sc.log")], [kbd("?"), t("sc.help")]].map(([k, v]) => row(v, "", `<span>${k}</span>`)).join("")}</div>
    </div>`;
  }

  // ----- about
  function pageAbout() {
    const lic = [["SharpPcap 6.3.0", "MIT"], ["PacketDotNet 1.4.7", "MPL-2.0 (unmodified)"], ["Microsoft.Web.WebView2", "BSD-style (Microsoft)"], [".NET 8 runtime", "MIT"], ["Npcap", "not bundled, install it yourself"]];
    return `<div class="card about-hero">${LOGO}<div><h2>Powerline Tool</h2><div class="muted">${esc(t("about.tag"))} · ${esc(t("about.version", (S.app && S.app.version) || "–"))}</div>
        <div style="margin-top:14px;display:flex;gap:10px;flex-wrap:wrap"><button class="btn primary" data-act="check-updates">${icon("refresh")}<span>${esc(t("btn.checkUpdates"))}</span></button>
        <button class="btn" data-act="open" data-url="https://github.com/TheFullyNormalWorkindCoder/powerline-tool">${icon("link")}<span>${esc(t("about.github"))}</span></button>
        <button class="btn" data-act="copy-diag" title="${esc(t("set.diag.d"))}">${icon("copy")}<span>${esc(t("set.diag"))}</span></button>
        <button class="btn" data-act="open" data-url="https://github.com/TheFullyNormalWorkindCoder/powerline-tool/issues/new/choose">${esc(t("about.issues"))}</button></div></div></div>
      <div class="sections"><div class="card section"><h3>${esc(t("about.unofficial"))}</h3><p>${esc(t("about.readonly"))}</p><p>${esc(t("about.privacy"))}</p></div>
      <div class="card section"><h3>${esc(t("about.licenses"))}</h3><table class="lic">${lic.map(([n, l]) => `<tr><td>${esc(n)}</td><td class="muted">${esc(l)}</td></tr>`).join("")}</table></div></div>`;
  }

  // ---------------------------------------------------------------- after render: animations + wiring
  function afterPage() {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      $$("#page .bar > i[data-w]").forEach(i => { i.style.width = i.dataset.w + "%"; });
      $$("#page .gauge .val").forEach(c => { c.style.strokeDashoffset = c.dataset.target; });
    }));
    const dur = (S.settings.reduceMotion ? 0 : 900);
    $$("#page [data-count]").forEach(el => {
      const to = +el.dataset.count, from = +el.dataset.from || 0, key = el.dataset.key;
      const small = el.querySelector("small");
      const set = v => { el.firstChild.nodeValue = Math.round(v); };
      prevVals.set(key, to);
      if (!dur || from === to) { set(to); return; }
      const t0 = performance.now();
      const step = now => { const k = Math.min(1, (now - t0) / dur), e = 1 - Math.pow(1 - k, 3); set(from + (to - from) * e); if (k < 1) requestAnimationFrame(step); };
      requestAnimationFrame(step);
      void small;
    });
    const hs = $("#histSel");
    if (hs) hs.addEventListener("change", () => { histLink = hs.value; render(); });
    const chartHost = $("#page .chart-host");
    if (chartHost && page === "history") {
      const keys = histPairs();
      if (keys.length) {
        const all = S.history[histLink] || [];
        const lim = { "1m": 60e3, "5m": 300e3, "30m": 1800e3, all: Infinity }[histRange];
        const cutoff = all.length ? all[all.length - 1][0] - lim : 0;
        const samples = lim === Infinity ? all : all.filter(s => s[0] >= cutoff);
        if (samples.length >= 2) Charts.history(samples, { warn: S.settings.warnBelowMbps, label: "" }).attach(chartHost);
      }
    }
    $$("#page input[type=range]").forEach(r => r.addEventListener("input", () => {
      const o = r.parentElement.querySelector("output");
      o.textContent = r.value + " " + (r.dataset.set === "intervalSec" ? "s" : "Mbps");
    }));
  }

  // ---------------------------------------------------------------- drawer
  function renderDrawer() {
    const d = $("#drawer");
    d.classList.toggle("open", drawerOpen);
    const events = (S.events || []).slice().reverse();
    const feed = events.length ? events.map(e => `<div class="feed-item ${eventTone(e)}"><time>${new Date(e.ts).toLocaleTimeString()}</time><span>${esc(eventText(e))}</span></div>`).join("")
      : `<div class="muted" style="padding:6px 0">${esc(t("drawer.noevents"))}</div>`;
    d.innerHTML = `<div class="col"><h4>${esc(t("drawer.events"))}<button class="icon-btn" data-act="drawer" aria-label="${esc(t("btn.close"))}">${icon("x")}</button></h4>${feed}</div>
      <div class="col"><h4>${esc(t("drawer.log"))}</h4><pre class="log">${esc(logLines.slice(-200).join("\n"))}</pre></div>`;
    const pre = d.querySelector("pre"); if (pre) pre.parentElement.scrollTop = pre.parentElement.scrollHeight;
  }

  function showHelp() {
    const ov = $("#overlay");
    const rows = [["F5 / R", t("sc.scan")], ["1 – 4", t("sc.pages")], ["Ctrl+K  /  /", t("btn.palette")], ["L", t("sc.log")], ["?", t("sc.help")], ["Esc", t("btn.close")]];
    ov.innerHTML = `<div class="modal" role="dialog" aria-label="${esc(t("set.shortcuts"))}"><h3>${esc(t("set.shortcuts"))}</h3>${rows.map(([k, v]) => `<div class="krow"><span>${esc(v)}</span><span class="kbd">${esc(k)}</span></div>`).join("")}</div>`;
    ov.hidden = false;
  }

  // ---------------------------------------------------------------- events
  function setSetting(key, val) {
    S.settings[key] = val;
    send("settings", { settings: { [key]: val } });
    render();
  }

  document.addEventListener("click", ev => {
    const el = ev.target.closest("[data-act]");
    if (!el) { if (ev.target.id === "overlay") $("#overlay").hidden = true; return; }
    const act = el.dataset.act;
    switch (act) {
      case "page": page = el.dataset.page; render(); $("#page").focus({ preventScroll: true }); $(".main").scrollTop = 0; break;
      case "scan": send("scan"); break;
      case "drawer": drawerOpen = !drawerOpen; renderRail(); renderDrawer(); break;
      case "copy": send("copy", { text: el.dataset.text }); toast("good", t("toast.copied"), el.dataset.text); break;
      case "toggle": { const m = el.dataset.mac; openCards.has(m) ? openCards.delete(m) : openCards.add(m); el.closest(".dev").classList.toggle("open"); break; }
      case "history-for": { const k = histPairs().find(k => k.split("|").includes(el.dataset.mac)); if (k) histLink = k; page = "history"; render(); break; }
      case "rename": startRename(el.dataset.mac, el.closest(".dev-name")); break;
      case "range": histRange = el.dataset.range; render(); break;
      case "clear-history": send("clearHistory"); break;
      case "set": { const k = el.dataset.key; setSetting(k, el.dataset.val); break; }
      case "open": send("open", { url: el.dataset.url }); break;
      case "open-folder": send("openFolder"); break;
      case "check-updates": send("checkUpdates"); break;
      case "export": send("export", { format: el.dataset.format }); break;
      case "sort": devSort = el.dataset.sort; render(); break;
      case "expand-all": S.devices.forEach(d => openCards.add(d.mac)); $$(".dev").forEach(c => c.classList.add("open")); break;
      case "collapse-all": openCards.clear(); $$(".dev").forEach(c => c.classList.remove("open")); break;
      case "copy-summary": send("copy", { text: summaryText() }); toast("good", t("toast.copied"), t("tools.copySummary")); break;
      case "copy-diag": send("copy", { text: diagnosticsText() }); toast("good", t("toast.copied"), t("set.diag")); break;
      case "save-diag": send("saveText", { filename: "powerline-diagnostics.txt", filter: "Text|*.txt", content: diagnosticsText() }); break;
      case "history-csv": saveHistoryCsv(); break;
      case "auto": setSetting("autoRefresh", !S.settings.autoRefresh); break;
      case "theme": setSetting("theme", nextTheme()); break;
      case "pin": setSetting("alwaysOnTop", !S.settings.alwaysOnTop); break;
      case "palette": openPalette(); break;
      case "test-notify": send("testNotification"); break;
      case "reset-settings": send("resetSettings"); break;
    }
  });

  function saveHistoryCsv() {
    if (!histLink || !(S.history || {})[histLink]) return;
    const [a, b] = histLink.split("|");
    const rows = ["time,from,to,tx_mbps,rx_mbps", ...S.history[histLink].map(s => `${new Date(s[0]).toISOString()},${a},${b},${s[1]},${s[2]}`)];
    send("saveText", { filename: "powerline-history.csv", filter: "CSV|*.csv", content: rows.join("\r\n") + "\r\n" });
  }

  // ---------------------------------------------------------------- command palette (Ctrl+K)
  function commands() {
    const go = p => () => { page = p; render(); };
    return [
      ["refresh", t("btn.scan"), "F5", () => send("scan")],
      ["devices", t("nav.devices"), "1", go("devices")], ["map", t("nav.map"), "2", go("map")], ["history", t("nav.history"), "3", go("history")],
      ["settings", t("nav.settings"), "4", go("settings")], ["info", t("nav.about"), "", go("about")],
      [themeIcon(), t("cmd.theme"), "", () => setSetting("theme", nextTheme())],
      [S.settings.autoRefresh ? "pause" : "play", S.settings.autoRefresh ? t("btn.pause") : t("btn.resume"), "", () => setSetting("autoRefresh", !S.settings.autoRefresh)],
      ["pin", t("btn.pin"), "", () => setSetting("alwaysOnTop", !S.settings.alwaysOnTop)],
      ["copy", t("tools.copySummary"), "", () => { send("copy", { text: summaryText() }); toast("good", t("toast.copied")); }],
      ["copy", t("set.diag"), "", () => { send("copy", { text: diagnosticsText() }); toast("good", t("toast.copied")); }],
      ["download", t("cmd.exportCsv"), "", () => send("export", { format: "csv" })], ["download", t("cmd.exportJson"), "", () => send("export", { format: "json" })],
      ["list", t("btn.logs"), "L", () => { drawerOpen = !drawerOpen; renderRail(); renderDrawer(); }],
      ["file", t("btn.openFolder"), "", () => send("openFolder")], ["refresh", t("btn.checkUpdates"), "", () => send("checkUpdates")],
      ["keyboard", t("set.shortcuts"), "?", () => showHelp()],
    ];
  }

  function openPalette() {
    palQuery = ""; palSel = 0;
    const ov = $("#overlay");
    ov.innerHTML = `<div class="palette" role="dialog" aria-label="${esc(t("btn.palette"))}"><input id="palIn" placeholder="${esc(t("cmd.placeholder"))}" autocomplete="off" spellcheck="false"><ul id="palList"></ul></div>`;
    ov.hidden = false;
    const input = $("#palIn");
    input.focus();
    input.addEventListener("input", () => { palQuery = input.value; palSel = 0; fillPalette(); });
    input.addEventListener("keydown", e => {
      const n = filteredCommands().length;
      if (e.key === "ArrowDown") { palSel = (palSel + 1) % Math.max(1, n); fillPalette(); e.preventDefault(); }
      else if (e.key === "ArrowUp") { palSel = (palSel - 1 + n) % Math.max(1, n); fillPalette(); e.preventDefault(); }
      else if (e.key === "Enter") { runCommand(palSel); e.preventDefault(); }
      else if (e.key === "Escape") { closeOverlay(); e.preventDefault(); }
    });
    fillPalette();
  }
  const filteredCommands = () => commands().filter(c => c[1].toLowerCase().includes(palQuery.trim().toLowerCase()));
  function fillPalette() {
    const list = filteredCommands();
    $("#palList").innerHTML = list.length ? list.map((c, i) => `<li class="${i === palSel ? "sel" : ""}" data-i="${i}">${icon(c[0])}<span>${esc(c[1])}</span>${c[2] ? `<small>${esc(c[2])}</small>` : ""}</li>`).join("") : `<div class="none">${esc(t("cmd.none"))}</div>`;
    $$("#palList li").forEach(li => li.addEventListener("click", () => runCommand(+li.dataset.i)));
    const sel = $("#palList li.sel"); if (sel) sel.scrollIntoView({ block: "nearest" });
  }
  function runCommand(i) { const c = filteredCommands()[i]; closeOverlay(); if (c) c[3](); }
  function closeOverlay() { const ov = $("#overlay"); ov.hidden = true; ov.innerHTML = ""; }

  document.addEventListener("change", ev => {
    const el = ev.target.closest("[data-set]");
    if (!el) return;
    const key = el.dataset.set;
    if (key === "sniff") { send("sniff", { on: el.checked }); return; }
    if (key === "classicUi") { setSetting("ui", el.checked ? "classic" : "web"); return; }
    const val = el.type === "checkbox" ? el.checked : +el.value;
    setSetting(key, val);
  });

  function startRename(mac, box) {
    const d = S.devices.find(x => x.mac === mac); if (!d) return;
    const nm = box.querySelector(".nm");
    const input = document.createElement("input");
    input.value = d.name; input.maxLength = 40; input.setAttribute("aria-label", t("dev.rename"));
    nm.replaceWith(input); input.focus(); input.select();
    let done = false;
    const finish = save => {
      if (done) return; done = true;
      if (save && input.value.trim() && input.value.trim() !== d.name) send("rename", { mac, name: input.value.trim() });
      render();
    };
    input.addEventListener("keydown", e => { if (e.key === "Enter") finish(true); else if (e.key === "Escape") finish(false); });
    input.addEventListener("blur", () => finish(true));
  }

  document.addEventListener("keydown", ev => {
    if ((ev.ctrlKey || ev.metaKey) && (ev.key === "k" || ev.key === "K")) { ev.preventDefault(); openPalette(); return; }
    if (/INPUT|TEXTAREA|SELECT/.test((ev.target.tagName || ""))) return;
    if (ev.ctrlKey || ev.metaKey || ev.altKey) return;
    const k = ev.key;
    if (k === "/") { ev.preventDefault(); openPalette(); return; }
    if (k === "F5" || k === "r" || k === "R") { ev.preventDefault(); send("scan"); }
    else if (k >= "1" && k <= "4") { page = ["devices", "map", "history", "settings"][+k - 1]; render(); }
    else if (k === "l" || k === "L") { drawerOpen = !drawerOpen; renderRail(); renderDrawer(); }
    else if (k === "?") { showHelp(); }
    else if (k === "Escape") { closeOverlay(); if (drawerOpen) { drawerOpen = false; renderRail(); renderDrawer(); } }
  });
  $("#overlay").addEventListener("click", e => { if (e.target.id === "overlay") closeOverlay(); });

  // ---------------------------------------------------------------- messages from the host
  function onState(st) {
    const firstState = S === null;
    S = st;
    if (seenEventTs === null) seenEventTs = Math.max(0, ...(st.events || []).map(e => e.ts));
    else (st.events || []).filter(e => e.ts > seenEventTs).forEach(e => {
      seenEventTs = Math.max(seenEventTs, e.ts);
      const bad = eventTone(e) === "bad";
      toast(bad ? "bad" : "good", eventText(e));
    });
    if (firstState || !pendingRender || true) render();
  }

  function handle(m) {
    switch (m.type) {
      case "state": onState(m.state); break;
      case "page": if (["devices", "map", "history", "settings", "about"].includes(m.page)) { page = m.page; if (S) render(); } break;
      case "log": logLines.push(m.line); if (logLines.length > 600) logLines.splice(0, 200); if (drawerOpen) renderDrawer(); break;
      case "toast": toast(m.kind || "info", m.title, m.text); break;
      case "update":
        if (m.error) toast("warn", t("toast.updateFail"), m.error);
        else if (m.newer) toast("info", t("toast.update", m.tag));
        else toast("good", t("toast.upToDate"));
        break;
      case "saved": toast("good", t("toast.saved"), m.path); break;
      case "sniff": if (S) { S.sniffing = m.on; } toast("info", m.on ? t("toast.sniffOn") : t("toast.sniffOff")); if (page === "settings") render(); break;
    }
  }

  if (host) { host.addEventListener("message", e => handle(e.data)); }

  // ---------------------------------------------------------------- demo mode (plain browser)
  const Mock = (() => {
    const A = "AA:BB:CC:00:00:01", B = "AA:BB:CC:00:00:02", C = "AA:BB:CC:00:00:03";
    const wob = (b, tick, ph) => Math.max(5, Math.round(b + Math.sin(tick * .45 + ph) * b * .10 + Math.sin(tick * 1.7 + ph * 3) * b * .04));
    const names = {};
    let tick = 0, timer = null;
    const saved = (() => { try { return JSON.parse(localStorage.getItem("plt-mock") || "{}"); } catch { return {}; } })();
    const st = {
      app: { version: "1.2.0", demo: true }, scanning: false, lastScan: Date.now(), nextScanAt: null, sniffing: false, nics: ["Ethernet"], skipped: [],
      warnings: params.has("warn") ? ["tpplc_running"] : [], devices: [], history: {}, events: [],
      settings: Object.assign({ autoRefresh: true, intervalSec: 10, theme: "system", language: "auto", accent: "teal", warnBelowMbps: 50, notify: true, minimizeToTray: false, startWithWindows: false, logHistory: false, reduceMotion: false, alwaysOnTop: false, ui: "web" }, saved),
    };
    if (params.get("lang")) st.settings.language = params.get("lang");
    const dev = (mac, local, links, extra = {}) => Object.assign({ mac, name: names[mac] || shortName(mac), local, role: local ? "CCo" : "", firmware: "tpver_demo_1.0", nid: "00112233445566", tei: local ? 1 : 2, nic: "Ethernet", stale: false, missed: 0, linksStale: false, lastSeen: Date.now(), links }, extra);
    function devices(k) {
      const ab = wob(223, k, 0), ba = wob(145, k, 1), ac = wob(48, k, 2), ca = wob(61, k, 3);
      const L = (peer, tx, rx) => ({ peer, peerName: names[peer] || shortName(peer), tx, rx, quality: qOf(Math.min(tx, rx)) });
      // ?down simulates a partner that the local adapter lists but that does not answer (what happened on the real network)
      if (params.has("down")) return [dev(A, true, [L(B, ab, ba), L(C, 0, 0)]), dev(B, false, [L(A, ba, ab)])];
      return [dev(A, true, [L(B, ab, ba), L(C, ac, ca)]), dev(B, false, [L(A, ba, ab)]), dev(C, false, [L(A, ca, ac)])];
    }
    function push(k, time) {
      const [x, y, z] = [A, B, C];
      const d = devices(k);
      const ab = d[0].links[0], ac = d[0].links[1];
      (st.history[x + "|" + y] ||= []).push([time, ab.tx, ab.rx]);
      (st.history[x + "|" + z] ||= []).push([time, ac.tx, ac.rx]);
    }
    for (let i = 90; i >= 1; i--) push(-i, Date.now() - i * 10000);
    st.devices = params.has("empty") ? [] : devices(0);
    if (params.has("empty")) { st.warnings = ["no_reply"]; st.history = {}; }
    st.events = [{ ts: Date.now() - 240000, kind: "DeviceAppeared", mac: C, peer: "", mbps: 0 }, { ts: Date.now() - 90000, kind: "LinkLow", mac: A, peer: C, mbps: 48 }];

    function emit() { setTimeout(() => handle({ type: "state", state: JSON.parse(JSON.stringify(st)) }), 0); }
    function schedule() {
      clearTimeout(timer);
      st.nextScanAt = st.settings.autoRefresh ? Date.now() + st.settings.intervalSec * 1000 : null;
      if (st.settings.autoRefresh) timer = setTimeout(scan, st.settings.intervalSec * 1000);
    }
    function scan() {
      if (st.scanning) return;
      st.scanning = true; emit();
      handle({ type: "log", line: new Date().toLocaleTimeString() + "  scan started" });
      setTimeout(() => {
        tick++; if (!params.has("empty")) { st.devices = devices(tick); push(tick, Date.now()); }
        st.scanning = false; st.lastScan = Date.now(); schedule(); emit();
        handle({ type: "log", line: new Date().toLocaleTimeString() + `  found ${st.devices.length} device(s)` });
      }, 900);
    }
    function save() { try { localStorage.setItem("plt-mock", JSON.stringify(st.settings)); } catch { } }
    function handleCmd(cmd, p) {
      switch (cmd) {
        case "ready": schedule(); emit(); break;
        case "scan": scan(); break;
        case "settings": Object.assign(st.settings, p.settings); save(); schedule(); emit(); break;
        case "rename": names[p.mac] = p.name; st.devices.forEach(d => { if (d.mac === p.mac) d.name = p.name; d.links.forEach(l => { if (l.peer === p.mac) l.peerName = p.name; }); }); emit(); break;
        case "clearHistory": st.history = {}; emit(); break;
        case "copy": try { navigator.clipboard.writeText(p.text).catch(() => { }); } catch { } break;
        case "checkUpdates": handle({ type: "update", newer: false }); break;
        case "export": handle({ type: "saved", path: "powerline." + p.format + " (demo)" }); break;
        case "saveText": handle({ type: "saved", path: p.filename + " (demo)" }); break;
        case "testNotification": handle({ type: "toast", kind: "info", title: "Powerline Tool", text: "Test notification" }); break;
        case "resetSettings": st.settings = Object.assign(st.settings, { theme: "system", language: "auto", accent: "teal", autoRefresh: true, intervalSec: 10, warnBelowMbps: 50, notify: true, minimizeToTray: false, alwaysOnTop: false, reduceMotion: false }); save(); schedule(); emit(); break;
        case "sniff": st.sniffing = p.on; handle({ type: "sniff", on: p.on }); break;
        case "open": window.open(p.url, "_blank"); break;
      }
    }
    return { handle: handleCmd };
  })();

  // ---------------------------------------------------------------- go
  send("ready");
  window.__ui = { get state() { return S; }, render, handle };
})();
