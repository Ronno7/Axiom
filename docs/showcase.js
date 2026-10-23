/* Progressive enhancement: static screenshots remain ordinary image links. */
(() => {
  "use strict";
  const all = (s, root = document) => [...root.querySelectorAll(s)];
  const make = (tag, cls, text) => {
    const node = document.createElement(tag);
    if (cls) node.className = cls;
    if (text != null) node.textContent = text;
    return node;
  };
  const button = (text, cls, action) => {
    const b = make("button", cls, text);
    b.type = "button";
    if (action) b.addEventListener("click", action);
    return b;
  };
  const pressed = (buttons, active) =>
    buttons.forEach((b) =>
      b.setAttribute("aria-pressed", String(b === active)),
    );
  const image = (src, alt) => {
    const img = make("img");
    img.src = src;
    img.alt = alt || "";
    img.decoding = "async";
    img.loading = "lazy";
    return img;
  };
  function comparison(entry, expandable) {
    const root = make("div", "compare");
    const labels = make("div", "compare-labels");
    labels.append(
      make("span", null, entry.labels[0]),
      make("span", null, entry.labels[1]),
    );
    const frame = make("div", "frame");
    const a = image(entry.a, entry.title + ": " + entry.labels[0]);
    const b = image(entry.b, entry.title + ": " + entry.labels[1]);
    b.className = "b";
    const seam = make("div", "seam");
    seam.setAttribute("aria-hidden", "true");
    const range = make("input");
    Object.assign(range, {
      type: "range",
      min: 0,
      max: 100,
      value: entry.cut ?? 50,
    });
    range.setAttribute("aria-label", "Compare " + entry.labels.join(" and "));
    const update = () => {
      frame.style.setProperty("--cut", range.value + "%");
      range.setAttribute(
        "aria-valuetext",
        range.value + "% " + entry.labels[0],
      );
      entry.cut = Number(range.value);
      if (!expandable) entry.syncInline?.(entry.cut);
    };
    if (expandable) {
      entry.syncInline = (cut) => {
        range.value = cut;
        update();
      };
    }
    range.addEventListener("input", update);
    update();
    frame.append(a, b, seam, range);
    if (expandable)
      frame.append(
        button("Expand ↗", "expand", (e) =>
          openViewer([entry], 0, e.currentTarget),
        ),
      );
    root.append(labels, frame);
    return root;
  }

  // A single native modal provides focus containment, Escape and an inert background.
  const dialog = make("dialog", "viewer");
  dialog.setAttribute("aria-labelledby", "viewer-title");
  const head = make("div", "viewer-head"),
    title = make("h2", null, "Image");
  title.id = "viewer-title";
  const actions = make("div", "viewer-actions");
  const previous = button("←", null, () => navigate(-1));
  previous.setAttribute("aria-label", "Previous image");
  const next = button("→", null, () => navigate(1));
  next.setAttribute("aria-label", "Next image");
  const zoom = button("Actual size", null, () => setZoom(!zoomed));
  zoom.setAttribute("aria-pressed", "false");
  const close = button("Close ×", null, () => dialog.close());
  actions.append(previous, next, zoom, close);
  head.append(title, actions);
  const stage = make("div", "viewer-stage");
  stage.tabIndex = 0;
  stage.setAttribute(
    "aria-label",
    "Image viewer; scroll to pan at actual size",
  );
  const foot = make("div", "viewer-foot"),
    caption = make("p"),
    count = make("span", "viewer-count");
  caption.setAttribute("aria-live", "polite");
  foot.append(caption, count);
  dialog.append(head, stage, foot);
  document.body.append(dialog);
  let entries = [],
    current = 0,
    opener = null,
    zoomed = false,
    drag = null,
    moved = false;
  function setZoom(value) {
    zoomed = value;
    stage.classList.toggle("zoomed", value);
    zoom.textContent = value ? "Fit image" : "Actual size";
    zoom.setAttribute("aria-pressed", String(value));
    stage.scrollTop = stage.scrollLeft = 0;
  }
  function fitComparison() {
    const comp = stage.querySelector(".compare");
    if (!comp) return;
    // Account for labels and padding so a 16:9 comparison also fits a short viewport.
    const height = Math.max(100, stage.clientHeight - 76);
    comp.style.maxWidth = Math.floor((height * 16) / 9) + "px";
  }
  function renderViewer() {
    const entry = entries[current];
    setZoom(false);
    title.textContent = entry.title || entry.alt || "Axiom";
    caption.textContent = entry.caption || "";
    count.textContent = current + 1 + " / " + entries.length;
    previous.disabled = current === 0;
    next.disabled = current === entries.length - 1;
    previous.hidden = next.hidden = entries.length < 2;
    zoom.hidden = !!entry.a;
    stage.replaceChildren(
      entry.a
        ? comparison(entry, false)
        : image(entry.src, entry.alt || entry.title),
    );
    requestAnimationFrame(fitComparison);
  }
  function navigate(direction) {
    const target = current + direction;
    if (target < 0 || target >= entries.length) return;
    current = target;
    renderViewer();
  }
  function openViewer(items, index, trigger) {
    if (typeof dialog.showModal !== "function") {
      window.open(items[index].src || items[index].b, "_blank", "noopener");
      return;
    }
    entries = items;
    current = index;
    opener = trigger;
    renderViewer();
    document.body.classList.add("viewer-open");
    dialog.showModal();
    close.focus();
  }
  dialog.addEventListener("close", () => {
    document.body.classList.remove("viewer-open");
    stage.replaceChildren();
    if (opener?.isConnected) opener.focus();
  });
  dialog.addEventListener("keydown", (e) => {
    if (e.target.matches('input[type="range"]')) return;
    if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
      e.preventDefault();
      navigate(e.key === "ArrowLeft" ? -1 : 1);
    }
  });
  stage.addEventListener("click", (e) => {
    if (e.target.tagName === "IMG" && !entries[current].a && !moved)
      setZoom(!zoomed);
    else if (e.target === stage && !zoomed) dialog.close();
  });
  stage.addEventListener("pointerdown", (e) => {
    moved = false;
    if (!zoomed || e.pointerType === "touch" || e.button !== 0) return;
    drag = {
      x: e.clientX,
      y: e.clientY,
      left: stage.scrollLeft,
      top: stage.scrollTop,
    };
    stage.setPointerCapture(e.pointerId);
    e.preventDefault();
  });
  stage.addEventListener("pointermove", (e) => {
    if (!drag) return;
    const dx = e.clientX - drag.x,
      dy = e.clientY - drag.y;
    if (Math.abs(dx) + Math.abs(dy) > 4) moved = true;
    stage.scrollLeft = drag.left - dx;
    stage.scrollTop = drag.top - dy;
  });
  const endDrag = () => {
    drag = null;
  };
  stage.addEventListener("pointerup", endDrag);
  stage.addEventListener("pointercancel", endDrag);
  stage.addEventListener("dragstart", (e) => e.preventDefault());
  window.addEventListener("resize", fitComparison);

  function wireImageLink(link, items, getIndex = () => 0) {
    link.addEventListener("click", (e) => {
      if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
      e.preventDefault();
      openViewer(items, getIndex(), link);
    });
  }
  all("a[data-image]").forEach((link) => {
    const img = link.querySelector("img");
    wireImageLink(link, [
      {
        src: link.getAttribute("href"),
        alt: img.alt,
        title: link.dataset.title || img.alt,
        caption:
          link.closest("figure")?.querySelector("figcaption")?.textContent ||
          "",
      },
    ]);
  });

  // Screen and feedback galleries share real buttons, not incomplete ARIA tabs.
  all(".gallery").forEach((root) => {
    const controls = all(".controls button", root),
      link = root.querySelector(".image-link"),
      img = link.querySelector("img");
    const captionNode = root.querySelector(".caption"),
      counter = root.querySelector(".counter");
    const items = controls.map((b) => ({
      src: b.dataset.img,
      title: b.textContent.trim(),
      alt: b.dataset.alt || b.textContent.trim(),
      caption: b.dataset.caption || "",
    }));
    let selected = 0;
    function select(i) {
      selected = i;
      const item = items[i];
      img.src = item.src;
      img.alt = item.alt;
      link.href = item.src;
      link.setAttribute("aria-label", "Enlarge " + item.title);
      pressed(controls, controls[i]);
      if (captionNode) captionNode.textContent = item.caption;
      if (counter)
        counter.textContent =
          String(i + 1).padStart(2, "0") +
          " / " +
          String(items.length).padStart(2, "0");
      all("[data-feedback-channel]", root).forEach((node) => {
        node.textContent =
          controls[i].dataset[node.dataset.feedbackChannel] || "";
      });
    }
    controls.forEach((b, i) => b.addEventListener("click", () => select(i)));
    wireImageLink(link, items, () => selected);
    select(0);
  });

  all(".compare[data-sets]").forEach((root) => {
    const sets = JSON.parse(root.dataset.sets),
      labels = JSON.parse(root.dataset.labels);
    const items = sets.map((s) => ({
      title: s[0],
      a: s[1],
      b: s[2],
      labels,
      caption: root.dataset.caption || "",
    }));
    const controls = make("div", "controls");
    controls.setAttribute("role", "group");
    controls.setAttribute("aria-label", "Comparison scene");
    const slot = make("div");
    const buttons = items.map((entry, i) =>
      button(entry.title, null, () => select(i)),
    );
    function select(i) {
      pressed(buttons, buttons[i]);
      slot.replaceChildren(comparison(items[i], true));
    }
    if (items.length > 1) {
      controls.append(...buttons);
      root.append(controls);
    }
    root.append(slot);
    select(0);
  });

  all(".stepper").forEach((root) => {
    const list = root.querySelector(".steps"),
      rows = all("li", list);
    const items = rows.map((li) => ({
      src: li.dataset.img,
      title: li.querySelector("b").textContent.trim(),
      caption: li.querySelector("span").textContent,
    }));
    const link = make("a", "frame image-link"),
      img = image(items[0].src, items[0].title);
    img.loading = "lazy";
    link.append(img);
    const description = make("p", "step-description");
    description.setAttribute("aria-live", "polite");
    root.prepend(link);
    root.append(description);
    let selected = 0;
    const buttons = rows.map((li, i) => {
      const b = button(items[i].title, null, () => select(i));
      li.replaceChildren(b);
      return b;
    });
    function select(i) {
      selected = i;
      const item = items[i];
      img.src = item.src;
      img.alt = item.title;
      link.href = item.src;
      link.setAttribute("aria-label", "Enlarge " + item.title);
      description.textContent = item.caption;
      buttons.forEach((b, j) => {
        if (i === j) b.setAttribute("aria-current", "step");
        else b.removeAttribute("aria-current");
      });
    }
    list.addEventListener("keydown", (e) => {
      const i = buttons.indexOf(document.activeElement);
      if (i < 0) return;
      let n =
        e.key === "ArrowRight" || e.key === "ArrowDown"
          ? i + 1
          : e.key === "ArrowLeft" || e.key === "ArrowUp"
            ? i - 1
            : e.key === "Home"
              ? 0
              : e.key === "End"
                ? buttons.length - 1
                : -1;
      if (n < 0 || n >= buttons.length) return;
      e.preventDefault();
      buttons[n].focus();
      select(n);
    });
    wireImageLink(link, items, () => selected);
    select(Number(root.dataset.start || items.length - 1));
  });

  all(".hotspots").forEach((root) => {
    const kinds = JSON.parse(root.dataset.kinds),
      rows = all(".item", root);
    const stageNode = make("div", "stage"),
      frame = make("div", "frame"),
      img = image(root.dataset.img, root.dataset.alt);
    img.loading = "lazy";
    frame.style.aspectRatio = root.dataset.ratio || "16/9";
    frame.append(img);
    const detail = make(
      "p",
      "detail",
      "Choose a category, then select a marker or label.",
    );
    detail.setAttribute("aria-live", "polite");
    stageNode.append(frame);
    root.prepend(stageNode);
    root.append(detail);
    const overview = button("Overview", "chip", () => filter(null));
    const chips = make("div", "chip-row");
    chips.setAttribute("role", "group");
    chips.setAttribute("aria-label", "Annotation categories");
    const filters = [
      [null, overview],
      ...Object.entries(kinds).map(([kind, label]) => [
        kind,
        button(label, "chip", () => filter(kind)),
      ]),
    ];
    chips.append(...filters.map((f) => f[1]));
    root.prepend(chips);
    let kind = null;
    const pairs = rows.map((row, i) => {
      const name = row.querySelector("b").textContent.trim(),
        description = row.querySelector("span").textContent;
      const b = button(null, null, () => select(i));
      const number = make(
        "span",
        "item-number",
        String(i + 1).padStart(2, "0"),
      );
      b.append(number, document.createTextNode(name));
      b.setAttribute("aria-pressed", "false");
      row.replaceChildren(b);
      let box = null,
        pin = null;
      if (row.dataset.rect) {
        box = make("div", "hotspot-box");
        box.dataset.kind = row.dataset.kind;
        box.hidden = true;
        if (row.dataset.rect === "edge") box.classList.add("edge");
        else {
          const [x, y, w, h] = row.dataset.rect.split(",").map(Number);
          Object.assign(box.style, {
            left: x + "%",
            top: y + "%",
            width: w + "%",
            height: h + "%",
          });
        }
        pin = button(String(i + 1), "pin", () => select(i));
        pin.setAttribute("aria-label", name);
        pin.setAttribute("aria-pressed", "false");
        box.append(pin);
        frame.append(box);
      }
      return { row, b, box, pin, name, description };
    });
    function select(i) {
      pairs.forEach((p, j) => {
        p.b.setAttribute("aria-pressed", String(i === j));
        if (p.box) {
          p.box.hidden = i !== j && (!kind || p.row.dataset.kind !== kind);
          p.box.classList.toggle("selected", i === j);
          p.pin.setAttribute("aria-pressed", String(i === j));
        }
      });
      detail.replaceChildren(
        make("b", null, pairs[i].name),
        document.createTextNode(pairs[i].description),
      );
    }
    function filter(value) {
      kind = value;
      filters.forEach(([k, b]) =>
        b.setAttribute("aria-pressed", String(k === kind)),
      );
      pairs.forEach((p) => {
        p.row.hidden = !!kind && p.row.dataset.kind !== kind;
        p.b.setAttribute("aria-pressed", "false");
        if (p.box) {
          p.box.hidden = !kind || p.row.hidden;
          p.box.classList.remove("selected");
          p.pin.setAttribute("aria-pressed", "false");
        }
      });
      detail.textContent = kind
        ? "Select a marker or label to explore " +
          kinds[kind].toLowerCase() +
          "."
        : "Choose a category, then select a marker or label.";
    }
    frame.append(
      button("Expand ↗", "expand", (e) =>
        openViewer(
          [
            {
              src: root.dataset.img,
              title: root.dataset.title || "Interface details",
              alt: img.alt,
              caption: detail.textContent,
            },
          ],
          0,
          e.currentTarget,
        ),
      ),
    );
    img.style.cursor = "zoom-in";
    img.addEventListener("click", () => frame.querySelector(".expand").click());
    filter(null);
  });

  all(".lcd-demo").forEach((root) => {
    const lcd = make("div", "lcd");
    lcd.setAttribute("role", "img");
    const rows = [make("div", "row"), make("div", "row")];
    rows.forEach((r) => r.setAttribute("aria-hidden", "true"));
    lcd.append(...rows);
    root.prepend(lcd);
    const status = make("span", "sr-only");
    status.setAttribute("aria-live", "polite");
    root.append(status);
    const buttons = all("button", root);
    function show(b) {
      pressed(buttons, b);
      const texts = [b.dataset.top, b.dataset.bottom];
      rows.forEach((row, i) =>
        row.replaceChildren(
          ...Array.from(texts[i].padEnd(16))
            .slice(0, 16)
            .map((c) => make("span", "cell", c === " " ? "\u00a0" : c)),
        ),
      );
      const description =
        b.textContent + ": " + texts.map((t) => t.trim()).join(". ");
      lcd.setAttribute("aria-label", description);
      status.textContent = description;
    }
    buttons.forEach((b) => b.addEventListener("click", () => show(b)));
    show(buttons[1] || buttons[0]);
  });

  // The masthead marks the section in view, and each section ends with a link onward.
  const nav = document.querySelector(".masthead nav");
  const sections = all("a", nav)
    .map((link) => ({
      link,
      section: document.querySelector(link.getAttribute("href")),
    }))
    .filter((s) => s.section);
  sections.forEach(({ section }, i) => {
    const next = sections[i + 1];
    if (!next) return;
    const link = make("a", "next-section");
    link.href = next.link.getAttribute("href");
    link.append(
      make("span", "eyebrow", "Next · " + next.link.firstChild.textContent),
      make("b", null, next.link.lastChild.textContent),
    );
    section.append(link);
  });
  const smooth = !matchMedia("(prefers-reduced-motion: reduce)").matches;
  let activeLink = null,
    queued = false;
  function markCurrent() {
    queued = false;
    const line = innerHeight * 0.35,
      atEnd = innerHeight + scrollY >= document.documentElement.scrollHeight - 2;
    let active = null;
    sections.forEach((s) => {
      if (s.section.getBoundingClientRect().top <= line) active = s.link;
    });
    // A short final section can never reach the line, so the page end selects it.
    if (atEnd) active = sections[sections.length - 1].link;
    if (active === activeLink) return;
    activeLink?.removeAttribute("aria-current");
    activeLink = active;
    if (!active) return;
    active.setAttribute("aria-current", "location");
    // Centre the link in a scrolling nav without moving the page.
    const a = active.getBoundingClientRect(),
      n = nav.getBoundingClientRect();
    nav.scrollBy({
      left: a.left - n.left - (n.width - a.width) / 2,
      behavior: smooth ? "smooth" : "auto",
    });
  }
  if (sections.length) {
    addEventListener(
      "scroll",
      () => {
        if (!queued) requestAnimationFrame(markCurrent);
        queued = true;
      },
      { passive: true },
    );
    addEventListener("resize", markCurrent);
    markCurrent();
  }
})();
