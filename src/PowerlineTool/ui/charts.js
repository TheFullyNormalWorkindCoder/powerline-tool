/* Small SVG chart helpers. No dependencies. */
window.Charts = (() => {
  const NS = "http://www.w3.org/2000/svg";

  /** "Nice" top value and tick step for a 0..max axis. */
  function scale(max) {
    const step = max <= 120 ? 25 : max <= 300 ? 50 : max <= 600 ? 100 : 200;
    const top = Math.max(step * 2, Math.ceil(max * 1.1 / step) * step);
    return { step, top };
  }

  /** Smooth path through points using a monotone-ish cubic curve. */
  function smooth(pts) {
    if (pts.length < 2) return "";
    let d = `M${pts[0][0].toFixed(1)},${pts[0][1].toFixed(1)}`;
    for (let i = 1; i < pts.length; i++) {
      const [x0, y0] = pts[i - 1], [x1, y1] = pts[i];
      const cx = (x0 + x1) / 2;
      d += ` C${cx.toFixed(1)},${y0.toFixed(1)} ${cx.toFixed(1)},${y1.toFixed(1)} ${x1.toFixed(1)},${y1.toFixed(1)}`;
    }
    return d;
  }

  /** Tiny line for a link row. values: array of numbers. */
  function spark(values, w = 84, h = 30, color = "var(--a1)") {
    if (!values || values.length < 2) return `<svg class="spark" viewBox="0 0 ${w} ${h}"><line x1="0" y1="${h - 3}" x2="${w}" y2="${h - 3}" stroke="var(--track)" stroke-width="2" stroke-dasharray="3 4"/></svg>`;
    const max = Math.max(...values), min = Math.min(...values);
    const lo = Math.max(0, min - (max - min) * 0.3), hi = max + (max - min) * 0.3 + 1;
    const pts = values.map((v, i) => [3 + i * (w - 6) / (values.length - 1), h - 3 - (v - lo) / (hi - lo) * (h - 6)]);
    const line = smooth(pts);
    const area = `${line} L${pts[pts.length - 1][0]},${h} L${pts[0][0]},${h} Z`;
    const id = "sg" + Math.random().toString(36).slice(2, 8);
    const last = pts[pts.length - 1];
    return `<svg class="spark" viewBox="0 0 ${w} ${h}" aria-hidden="true">
      <defs><linearGradient id="${id}" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="${color}" stop-opacity=".35"/><stop offset="1" stop-color="${color}" stop-opacity="0"/></linearGradient></defs>
      <path d="${area}" fill="url(#${id})"/><path d="${line}" fill="none" stroke="${color}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
      <circle cx="${last[0]}" cy="${last[1]}" r="2.6" fill="${color}"/></svg>`;
  }

  /**
   * Main history chart. samples: [[tsMs, tx, rx], ...]. Returns {html, attach(container)} where attach wires up the hover tooltip.
   */
  function history(samples, opts) {
    const W = 1000, H = 360, L = 52, R = 16, T = 14, B = 30;
    const pw = W - L - R, ph = H - T - B;
    const t0 = samples[0][0], t1 = samples[samples.length - 1][0], span = Math.max(1, t1 - t0);
    const max = Math.max(...samples.map(s => Math.max(s[1], s[2])));
    const { step, top } = scale(max);
    const X = t => L + (t - t0) / span * pw;
    const Y = v => T + ph - v / top * ph;
    const tx = samples.map(s => [X(s[0]), Y(s[1])]);
    const rx = samples.map(s => [X(s[0]), Y(s[2])]);
    let grid = "";
    for (let v = 0; v <= top; v += step) {
      grid += `<line x1="${L}" x2="${W - R}" y1="${Y(v)}" y2="${Y(v)}" stroke="var(--line)" stroke-width="1"/><text x="${L - 10}" y="${Y(v) + 4}" text-anchor="end">${v}</text>`;
    }
    const fmt = ts => new Date(ts).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
    const ticks = [0, .25, .5, .75, 1].map(f => `<text x="${L + f * pw}" y="${H - 8}" text-anchor="${f === 0 ? "start" : f === 1 ? "end" : "middle"}">${fmt(t0 + f * span)}</text>`).join("");
    const txLine = smooth(tx), rxLine = smooth(rx);
    const txArea = `${txLine} L${tx[tx.length - 1][0]},${T + ph} L${tx[0][0]},${T + ph} Z`;
    const rxArea = `${rxLine} L${rx[rx.length - 1][0]},${T + ph} L${rx[0][0]},${T + ph} Z`;
    const warn = opts.warn > 0 && opts.warn < top
      ? `<line x1="${L}" x2="${W - R}" y1="${Y(opts.warn)}" y2="${Y(opts.warn)}" stroke="var(--poor)" stroke-width="1.4" stroke-dasharray="6 6" opacity=".8"/>` : "";
    const html = `
      <svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" class="chart-axis" role="img" aria-label="${opts.label}">
        <defs>
          <linearGradient id="gTx" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="var(--a1)" stop-opacity=".42"/><stop offset="1" stop-color="var(--a1)" stop-opacity="0"/></linearGradient>
          <linearGradient id="gRx" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="var(--rx)" stop-opacity=".30"/><stop offset="1" stop-color="var(--rx)" stop-opacity="0"/></linearGradient>
        </defs>
        ${grid}${ticks}${warn}
        <path d="${rxArea}" fill="url(#gRx)"/><path d="${txArea}" fill="url(#gTx)"/>
        <path d="${rxLine}" fill="none" stroke="var(--rx)" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>
        <path d="${txLine}" fill="none" stroke="var(--a1)" stroke-width="2.8" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>
        <g class="cross" style="display:none"><line y1="${T}" y2="${T + ph}" stroke="var(--muted)" stroke-width="1" stroke-dasharray="4 4"/>
          <circle class="dtx" r="5" fill="var(--a1)" stroke="var(--card)" stroke-width="2.5"/><circle class="drx" r="5" fill="var(--rx)" stroke="var(--card)" stroke-width="2.5"/></g>
        <rect class="hit" x="${L}" y="${T}" width="${pw}" height="${ph}" fill="transparent"/>
      </svg>
      <div class="chart-tip"></div>`;

    function attach(host) {
      const svg = host.querySelector("svg"), hit = svg.querySelector(".hit"), cross = svg.querySelector(".cross");
      const tip = host.querySelector(".chart-tip"), dtx = svg.querySelector(".dtx"), drx = svg.querySelector(".drx"), vline = cross.querySelector("line");
      hit.addEventListener("mousemove", ev => {
        const r = svg.getBoundingClientRect();
        const sx = (ev.clientX - r.left) / r.width * W;
        const t = t0 + Math.min(1, Math.max(0, (sx - L) / pw)) * span;
        let best = 0;
        for (let i = 1; i < samples.length; i++) if (Math.abs(samples[i][0] - t) < Math.abs(samples[best][0] - t)) best = i;
        const s = samples[best], x = X(s[0]);
        cross.style.display = "";
        vline.setAttribute("x1", x); vline.setAttribute("x2", x);
        dtx.setAttribute("cx", x); dtx.setAttribute("cy", Y(s[1])); drx.setAttribute("cx", x); drx.setAttribute("cy", Y(s[2]));
        tip.innerHTML = `<div class="t">${new Date(s[0]).toLocaleTimeString()}</div>
          <div class="r"><span><i style="background:var(--a1)"></i>TX</span><span>${s[1]} Mbps</span></div>
          <div class="r"><span><i style="background:var(--rx)"></i>RX</span><span>${s[2]} Mbps</span></div>`;
        const hr = host.getBoundingClientRect();
        let left = ev.clientX - hr.left + 16;
        if (left + 170 > hr.width) left = ev.clientX - hr.left - 176;
        tip.style.left = left + "px"; tip.style.top = Math.max(8, ev.clientY - hr.top - 40) + "px"; tip.style.opacity = 1;
      });
      hit.addEventListener("mouseleave", () => { cross.style.display = "none"; tip.style.opacity = 0; });
    }
    return { html, attach };
  }

  /** Circular gauge: fraction 0..1. */
  function gauge(fraction, centerHtml) {
    const r = 64, c = 2 * Math.PI * r, f = Math.max(0, Math.min(1, fraction));
    return `<div class="gauge"><svg viewBox="0 0 150 150">
      <defs><linearGradient id="gaugeGrad" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="var(--a1)"/><stop offset="1" stop-color="var(--a2)"/></linearGradient></defs>
      <circle class="track" cx="75" cy="75" r="${r}"/>
      <circle class="val" cx="75" cy="75" r="${r}" stroke-dasharray="${c}" stroke-dashoffset="${c}" data-target="${c * (1 - f)}"/>
    </svg><div class="center">${centerHtml}</div></div>`;
  }

  return { scale, smooth, spark, history, gauge, NS };
})();
