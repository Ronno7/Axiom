// Turns each [data-name] frame of index.html into a standalone SVG that Figma imports as
// editable layers (rects, vectors, text). Loaded only when the page is opened as index.html#svg.
// Element boxes, borders and text lines are read from the live layout, so the SVG matches the HTML.

(function () {
  const NS = 'http://www.w3.org/2000/svg';
  // Inter metrics: the text range box starts `ascent` above the baseline.
  const ASCENT = 0.96875;

  // CSS gradients used by the page, as SVG gradient defs (Figma reads these as gradient fills).
  const GRADIENTS = {
    'bg-level': `<linearGradient id="g-level" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#2b3440"/><stop offset="0.46" stop-color="#46505c"/>
      <stop offset="0.47" stop-color="#1a1d22"/><stop offset="1" stop-color="#0b0c0e"/></linearGradient>`,
    'bg-menu': `<radialGradient id="g-menu" cx="0.72" cy="0.5" r="0.75">
      <stop offset="0" stop-color="#3a3f48"/><stop offset="0.55" stop-color="#121417"/>
      <stop offset="1" stop-color="#000000"/></radialGradient>`,
    'scrim': `<linearGradient id="g-scrim" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#000" stop-opacity="0.92"/><stop offset="0.45" stop-color="#000" stop-opacity="0.75"/>
      <stop offset="1" stop-color="#000" stop-opacity="0"/></linearGradient>`,
  };
  const GRADIENT_IDS = { 'bg-level': 'g-level', 'bg-menu': 'g-menu', 'scrim': 'g-scrim' };

  const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  const n = v => Math.round(v * 100) / 100;

  // "rgba(r, g, b, a)" -> { hex, a }
  function color(css) {
    const m = css && css.match(/rgba?\(([^)]+)\)/);
    if (!m) return null;
    const [r, g, b, a = 1] = m[1].split(',').map(parseFloat);
    if (a === 0) return null;
    const hex = '#' + [r, g, b].map(c => Math.round(c).toString(16).padStart(2, '0')).join('');
    return { hex, a };
  }
  // Inline vectors carry CSS colour strings; rewrite them as hex plus a separate opacity.
  const hexColors = markup => markup.replace(/(fill|stroke)="(rgba?\([^)]+\))"/g, (m, attr, css) => {
    const c = color(css);
    if (!c) return `${attr}="none"`;
    return `${attr}="${c.hex}"` + (c.a < 1 ? ` ${attr}-opacity="${n(c.a)}"` : '');
  });
  const fillAttrs = (c, opacity) => `fill="${c.hex}"` + (c.a * opacity < 1 ? ` fill-opacity="${n(c.a * opacity)}"` : '');

  function layerName(el) {
    if (el.dataset.name) return el.dataset.name;
    const cls = [...el.classList].filter(c => !c.startsWith('is-') && c !== 've' && c !== 'label' && c !== 'slant');
    const text = el.children.length === 0 || el.classList.contains('slant') ? el.textContent.trim() : '';
    return (cls[0] || el.tagName.toLowerCase()) + (text ? ` · ${text.slice(0, 24)}` : '');
  }

  // Each wrapped line of a text node: [{ text, left, top }], using word positions.
  function textLines(node) {
    const range = document.createRange();
    const words = [];
    const re = /\S+/g;
    let m;
    while ((m = re.exec(node.data))) {
      range.setStart(node, m.index);
      range.setEnd(node, m.index + m[0].length);
      const r = range.getClientRects()[0];
      if (r) words.push({ start: m.index, end: m.index + m[0].length, left: r.left, top: r.top });
    }
    const lines = [];
    for (const w of words) {
      const line = lines[lines.length - 1];
      if (line && Math.abs(line.top - w.top) < 2) line.end = w.end;
      else lines.push({ start: w.start, end: w.end, left: w.left, top: w.top });
    }
    return lines.map(l => ({ text: node.data.slice(l.start, l.end), left: l.left, top: l.top }));
  }

  function exportFrame(frame) {
    const origin = frame.getBoundingClientRect();
    const W = Math.round(origin.width), H = Math.round(origin.height);
    const defs = new Set();
    let useHalo = false;

    function walk(el, opacity, depth) {
      const cs = getComputedStyle(el);
      if (cs.display === 'none' || cs.visibility === 'hidden') return '';
      opacity *= parseFloat(cs.opacity);
      const r = el.getBoundingClientRect();
      const x = n(r.left - origin.left), y = n(r.top - origin.top), w = n(r.width), h = n(r.height);
      const pad = '  '.repeat(depth);
      let out = '';

      // Background: gradient, then flat colour.
      const grad = Object.keys(GRADIENT_IDS).find(c => el.classList.contains(c));
      if (grad) {
        defs.add(GRADIENTS[grad]);
        out += `${pad}  <rect x="${x}" y="${y}" width="${w}" height="${h}" fill="url(#${GRADIENT_IDS[grad]})"${opacity < 1 ? ` opacity="${n(opacity)}"` : ''}/>\n`;
      }
      const bg = color(cs.backgroundColor);
      const radius = parseFloat(cs.borderTopLeftRadius) || 0;
      if (bg) out += `${pad}  <rect x="${x}" y="${y}" width="${w}" height="${h}"${radius ? ` rx="${radius}"` : ''} ${fillAttrs(bg, opacity)}/>\n`;

      // Borders: one stroked rect when uniform, otherwise a filled strip per side.
      const sides = ['Top', 'Right', 'Bottom', 'Left'].map(s => ({ s, bw: parseFloat(cs[`border${s}Width`]), c: color(cs[`border${s}Color`]) }));
      const drawn = sides.filter(b => b.bw > 0 && b.c);
      const uniform = drawn.length === 4 && drawn.every(b => b.bw === drawn[0].bw && b.c.hex === drawn[0].c.hex && b.c.a === drawn[0].c.a);
      if (uniform) {
        const bw = drawn[0].bw, c = drawn[0].c;
        out += `${pad}  <rect x="${n(x + bw / 2)}" y="${n(y + bw / 2)}" width="${n(w - bw)}" height="${n(h - bw)}"${radius ? ` rx="${radius}"` : ''} fill="none" stroke="${c.hex}" stroke-width="${bw}"${cs.borderTopStyle === 'dashed' ? ` stroke-dasharray="${bw * 3} ${bw * 3}"` : ''}${c.a * opacity < 1 ? ` stroke-opacity="${n(c.a * opacity)}"` : ''}/>\n`;
      } else {
        for (const { s, bw, c } of drawn) {
          const box = { Top: [x, y, w, bw], Bottom: [x, y + h - bw, w, bw], Left: [x, y, bw, h], Right: [x + w - bw, y, bw, h] }[s];
          out += `${pad}  <rect x="${n(box[0])}" y="${n(box[1])}" width="${n(box[2])}" height="${n(box[3])}" ${fillAttrs(c, opacity)}/>\n`;
        }
      }

      for (const child of el.childNodes) {
        if (child.nodeType === Node.TEXT_NODE && child.data.trim()) {
          const c = color(cs.color);
          if (!c) continue;
          const size = parseFloat(cs.fontSize);
          const spacing = parseFloat(cs.letterSpacing) || 0;
          const halo = cs.textShadow !== 'none' && el.closest('.hud') && !el.matches('.hud-controller--warning, .hud-proximity--pulse');
          if (halo) useHalo = true;
          for (const line of textLines(child)) {
            out += `${pad}  <text x="${n(line.left - origin.left)}" y="${n(line.top - origin.top + size * ASCENT)}" font-family="Inter" font-size="${size}" font-weight="${cs.fontWeight}"`
              + (spacing ? ` letter-spacing="${spacing}"` : '') + ` ${fillAttrs(c, opacity)}`
              + (halo ? ' filter="url(#halo)"' : '') + ` xml:space="preserve">${esc(line.text)}</text>\n`;
          }
        } else if (child.nodeType === Node.ELEMENT_NODE) {
          if (child.tagName.toLowerCase() === 'svg') {
            // SlantButton outline / checkmark tick: copy the vector, placed at its box.
            const sr = child.getBoundingClientRect();
            out += `${pad}  <g transform="translate(${n(sr.left - origin.left)} ${n(sr.top - origin.top)})"${opacity < 1 ? ` opacity="${n(opacity)}"` : ''}>${hexColors(child.innerHTML)}</g>\n`;
          } else {
            out += walk(child, opacity, depth + 1);
          }
        }
      }
      if (!out) return '';
      return `${pad}<g id="${esc(layerName(el))}">\n${out}${pad}</g>\n`;
    }

    // The frame's own bounds clip everything, like the 1920x1080 game view.
    const body = walk(frame, 1, 1);
    if (useHalo) defs.add('<filter id="halo" x="-20%" y="-50%" width="140%" height="200%"><feDropShadow dx="0" dy="0" stdDeviation="4" flood-color="#000" flood-opacity="0.95"/></filter>');
    defs.add(`<clipPath id="frame-clip"><rect width="${W}" height="${H}"/></clipPath>`);
    return `<svg xmlns="${NS}" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">\n<defs>\n${[...defs].join('\n')}\n</defs>\n<g clip-path="url(#frame-clip)">\n${body}</g>\n</svg>\n`;
  }

  function fileName(frame, i) {
    return String(i + 1).padStart(2, '0') + '-' + frame.dataset.name.toLowerCase().replace(/[^a-z0-9]+/g, '-') + '.svg';
  }

  // Exports every frame. Called by the download bar, and by tools that read the results from the DOM.
  window.exportAxiomSvgs = function () {
    return [...document.querySelectorAll('[data-name]')].map((f, i) => ({ name: fileName(f, i), svg: exportFrame(f) }));
  };

  document.fonts.ready.then(() => requestAnimationFrame(() => {
    const files = window.exportAxiomSvgs();
    const bar = document.createElement('div');
    bar.id = 'svg-export-bar';
    bar.style.cssText = 'position:fixed;top:16px;right:16px;z-index:10;background:#111;border:1px solid #555;padding:14px 18px;font:14px Inter,sans-serif;color:#ebebeb;display:flex;flex-direction:column;gap:6px;';
    bar.innerHTML = '<b>Download SVGs for Figma</b>';
    for (const f of files) {
      const a = document.createElement('a');
      a.href = URL.createObjectURL(new Blob([f.svg], { type: 'image/svg+xml' }));
      a.download = f.name;
      a.textContent = f.name;
      a.style.color = 'rgb(44,160,255)';
      bar.appendChild(a);
      // Raw copies for headless export (--dump-dom).
      const raw = document.createElement('script');
      raw.type = 'text/plain';
      raw.className = 'svg-export';
      raw.dataset.file = f.name;
      raw.textContent = f.svg;
      document.body.appendChild(raw);
    }
    document.body.appendChild(bar);
  }));
})();
