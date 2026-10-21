// Axiom UI showcase: small components driven by data attributes in index.html.
// Edit the HTML to change content; this file only adds the behaviour.

const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));
const el = (tag, cls, html) => {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (html != null) e.innerHTML = html;
  return e;
};

// ---------- Compare slider ----------
// <div class="compare" data-labels='["Before","After"]' data-sets='[["Name","a.jpg","b.jpg"], ...]'>
for (const root of $$(".compare")) {
  const sets = JSON.parse(root.dataset.sets);
  const [labelA, labelB] = JSON.parse(root.dataset.labels || '["Before","After"]');
  const frame = el("div", "frame");
  const a = el("img", "a"), b = el("img", "b");
  a.alt = labelA; b.alt = labelB;
  const range = el("input");
  Object.assign(range, { type: "range", min: 0, max: 100, value: 50 });
  range.setAttribute("aria-label", `Slide between ${labelA} and ${labelB}`);
  frame.append(a, b, el("div", "seam"), range);
  const labels = el("div", "compare-labels");
  labels.append(el("span", null, labelA), el("span", null, labelB));
  const show = i => {
    a.src = sets[i][1]; b.src = sets[i][2];
    a.alt = `${sets[i][0]}: ${labelA}`; b.alt = `${sets[i][0]}: ${labelB}`;
  };
  range.addEventListener("input", () => frame.style.setProperty("--cut", range.value + "%"));
  if (sets.length > 1) {
    const tabs = el("div", "tabs");
    tabs.setAttribute("role", "tablist");
    sets.forEach((s, i) => {
      const t = el("button", null, s[0]);
      t.setAttribute("role", "tab");
      t.setAttribute("aria-selected", i === 0);
      t.addEventListener("click", () => {
        $$("button", tabs).forEach(x => x.setAttribute("aria-selected", x === t));
        show(i);
      });
      tabs.append(t);
    });
    root.append(tabs);
  }
  root.append(labels, frame);
  show(0);
}

// ---------- Stepper ----------
// <div class="stepper"><ol class="steps"><li data-img="x.jpg"><b>Title</b><span>Text</span></li>...</ol></div>
for (const root of $$(".stepper")) {
  const list = root.querySelector(".steps");
  const frame = el("div", "frame");
  root.prepend(frame);
  const buttons = $$("li", list).map((li, i) => {
    const img = el("img");
    img.src = li.dataset.img;
    img.alt = li.querySelector("b").textContent;
    img.loading = i < 2 ? "eager" : "lazy";
    frame.append(img);
    const button = el("button");
    button.append(...li.childNodes);
    li.append(button);
    button.addEventListener("click", () => select(i));
    return { button, img };
  });
  const select = i => buttons.forEach((b, j) => {
    b.img.classList.toggle("on", i === j);
    if (i === j) b.button.setAttribute("aria-current", "step"); else b.button.removeAttribute("aria-current");
  });
  list.addEventListener("keydown", e => {
    const i = buttons.findIndex(b => b.button === document.activeElement);
    if (i < 0) return;
    const next = e.key === "ArrowDown" || e.key === "ArrowRight" ? i + 1 : e.key === "ArrowUp" || e.key === "ArrowLeft" ? i - 1 : -1;
    if (next < 0 || next >= buttons.length) return;
    e.preventDefault();
    buttons[next].button.focus();
    select(next);
  });
  select(Number(root.dataset.start || 0));
}

// ---------- Hotspots ----------
// <div class="hotspots" data-img="x.jpg" data-ratio="16/9" data-kinds='{"kind":"Label"}'>
//   <ul class="items"><li class="item" data-kind="kind" data-rect="x,y,w,h"><b>Name</b><span>Text</span></li></ul>
// data-rect is in percent of the image (left, top, width, height); "edge" outlines the whole frame.
// Items without data-rect are listed but have no box (parts you can't see in the picture).
for (const root of $$(".hotspots")) {
  const side = root.querySelector(".side");
  const items = $$(".item", root);
  const stage = el("div", "stage");
  const frame = el("div", "frame");
  if (root.dataset.ratio) frame.style.aspectRatio = root.dataset.ratio;
  const img = el("img");
  img.src = root.dataset.img;
  img.alt = root.dataset.alt || "";
  frame.append(img);
  const hint = root.dataset.hint || "Hover, tap or tab through the list to see each part.";
  const detail = el("p", "detail", hint);
  detail.setAttribute("aria-live", "polite");
  stage.append(frame, detail);
  root.prepend(stage);

  const pairs = items.map(item => {
    item.tabIndex = 0;
    let box = null;
    if (item.dataset.rect) {
      box = el("div", "box");
      box.dataset.kind = item.dataset.kind;
      if (item.dataset.rect === "edge") box.classList.add("edge");
      else {
        const [x, y, w, h] = item.dataset.rect.split(",").map(Number);
        Object.assign(box.style, { left: x + "%", top: y + "%", width: w + "%", height: h + "%" });
      }
      frame.append(box);
    }
    return { item, box };
  });

  // The selected item stays selected until another is picked, so its caption can be read.
  let filter = null;
  const highlight = hot => {
    pairs.forEach(p => {
      const visible = !filter || p.item.dataset.kind === filter;
      p.item.hidden = !visible;
      p.item.classList.toggle("hot", p === hot);
      if (!p.box) return;
      p.box.style.display = visible ? "" : "none";
      p.box.classList.toggle("hot", p === hot);
      p.box.classList.toggle("dim", !!hot && p !== hot);
    });
    root.classList.toggle("filtered", !!filter);
    if (hot) {
      detail.replaceChildren(el("b", null, hot.item.querySelector("b").textContent),
        document.createTextNode(hot.item.querySelector("span")?.textContent || ""));
    } else detail.textContent = hint;
  };
  for (const p of pairs) {
    p.item.addEventListener("mouseenter", () => highlight(p));
    p.item.addEventListener("focus", () => highlight(p));
    p.item.addEventListener("click", () => highlight(p));
  }

  // Filters: either a 2x2 quadrant (.quadrant with chips) or a simple chip row built from data-kinds.
  let chips = $$(".quadrant .chip", root);
  if (!chips.length && root.dataset.kinds) {
    const row = el("div", "chip-row");
    for (const [kind, label] of Object.entries(JSON.parse(root.dataset.kinds))) {
      const chip = el("button", "chip", label);
      chip.dataset.kind = kind;
      row.append(chip);
    }
    side.prepend(row);
    chips = $$(".chip", row);
  }
  for (const chip of chips) {
    chip.setAttribute("aria-pressed", "false");
    chip.addEventListener("click", () => {
      filter = filter === chip.dataset.kind ? null : chip.dataset.kind;
      chips.forEach(c => c.setAttribute("aria-pressed", c.dataset.kind === filter));
      highlight(null);
    });
  }
}

// ---------- Cockpit LCD ----------
// <div class="lcd-demo"><div class="tabs"><button data-top="..." data-bottom="...">State</button>...</div></div>
for (const root of $$(".lcd-demo")) {
  const lcd = el("div", "lcd");
  lcd.setAttribute("aria-live", "polite");
  const rows = [el("div", "row"), el("div", "row")];
  lcd.append(...rows);
  root.prepend(lcd);
  const write = (row, text) => {
    const chars = Array.from(text.padEnd(16).slice(0, 16));
    row.replaceChildren(...chars.map(c => el("span", "cell", c === " " ? "&nbsp;" : c)));
    row.setAttribute("aria-label", text.trim());
  };
  const buttons = $$("button", root);
  const show = b => {
    buttons.forEach(x => x.setAttribute("aria-selected", x === b));
    write(rows[0], b.dataset.top);
    write(rows[1], b.dataset.bottom);
  };
  buttons.forEach(b => b.addEventListener("click", () => show(b)));
  show(buttons[0]);
}

// ---------- Glitch timeline chart ----------
// <svg class="glitch" data-frames='[[tear, echo, scanlines], ...]'>
for (const svg of $$("svg.glitch")) {
  const frames = JSON.parse(svg.dataset.frames);
  const W = 720, H = 160, pad = 24, slot = (W - pad) / frames.length, bar = slot / 4;
  const colours = ["#e9e9ea", "#d61f29", "#6b6f77"];
  let out = `<line x1="${pad}" y1="${H - 20}" x2="${W}" y2="${H - 20}" stroke="#23252b"/>`;
  frames.forEach((f, i) => {
    const x = pad + i * slot + slot * 0.15;
    f.forEach((v, k) => {
      const h = v * (H - 36);
      out += `<rect x="${x + k * bar}" y="${H - 20 - h}" width="${bar - 1}" height="${h}" fill="${colours[k]}"/>`;
    });
    if (i % 3 === 0) out += `<text x="${x}" y="${H - 4}" fill="#5f636b" font-size="11" font-family="IBM Plex Mono, monospace">${i}</text>`;
  });
  svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
  svg.innerHTML = out;
}
