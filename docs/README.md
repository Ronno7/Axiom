# UI showcase

A static, image-led tour of Axiom's interface, intended for GitHub Pages at
<https://ronno7.github.io/Axiom/>. No build step or JavaScript dependencies.

Open `index.html` locally, or run `python -m http.server 8765 --bind 127.0.0.1 --directory docs`
from the repository root and visit <http://127.0.0.1:8765/>. Publishing is a separate step.

## Editing

Keep headings and captions short. Put implementation detail in the expandable notes and let the
screenshots lead. Update the footer date when refreshing the page.

The page runs in seven numbered sections: Screens, In play, Feedback, Arcade, Rendering, Cockpit and
Evolution. Each section's `id`, eyebrow (`01 / Screens`) and masthead link share one name. The masthead
marks the section in view, and each section ends with a "Next" link generated from the masthead, so
adding a section means adding a `<section id>` and a matching masthead link. Subsections have their
own anchors too (`#flow`, `#feedback-channels`, `#layers`, `#glitch`, `#lcd`).

| Part | Where to edit |
| --- | --- |
| Copy, section order, links | `index.html` |
| Typography, layout, responsive rules | `showcase.css` |
| Screen flow map | Buttons in `.flow-map` with `data-img` and `data-caption`; a `node-*` class places each one on the map. Results stays a dashed placeholder until a natural results capture exists |
| Feedback gallery | Buttons with `data-img`, `data-alt`, `data-caption` and the three channel texts |
| Standard / Arcade comparisons | `data-sets` on `.compare`, with labels in `data-labels` |
| Layer and evolution steppers | One `<li data-img="...">` per step; `data-start` picks the opening step |
| Annotations | `.item` with `data-kind` and `data-rect="left,top,width,height"`, in percentages |
| Implemented / Planned | `.legend` with `.on` / `.off` markers; use the same two words everywhere |
| Cockpit LCD | Buttons with `data-top` and `data-bottom`, exactly 16 characters per row |
| Viewer, navigation and interactions | `showcase.js` |

Click a gallery image or an annotated image to enlarge it. Comparisons have a separate Expand button
so dragging the split stays unambiguous. The viewer provides previous/next, Fit image / Actual size,
and close controls; arrow keys move between gallery images and Escape closes the native dialog.
Actual-size images can be scrolled or dragged. Expanded comparisons keep their slider position when closed.

## Captures

Current game images in `img/` are 2560 × 1440 JPGs, captured from Unity's Game view in Play Mode.
The historical images remain intentionally older; the cockpit illustration is a concept render.
Replace an existing filename to update all its uses. Annotation coordinates are percentages of the
source image: divide the pixel box by the actual width and height, then multiply by 100.

Refresh the current menu, Options, glitch, render-layer sequence, flight, pause, hit/proximity feedback,
and both Arcade comparisons together when the game's presentation changes. Preserve the archive.
Capture Standard and Arcade from the same frozen camera and geometry. The two archive level shots share
a camera; the current level shot does not, and its caption says so.

On a 2560 × 1440 monitor, **F11** in Play Mode (Window → General → Fullscreen Game View) gives a
native-resolution Game view with no toolbar; press F11 again or leave Play Mode to close it.

For captures, let the Game view finish rendering after each state change. If the editor is unfocused,
frames may stop updating; a temporary Play Mode run-in-background setting can help. Restore runtime
settings and leave Play Mode afterwards. Do not save staged scenes or change saved player preferences.
The layer sequence removes interface, bloom, grade and atmosphere progressively at runtime only.

The feedback images are staged UI examples (including a real shield hit held for capture), not evidence
of an end-to-end playthrough. Add the results panel from a natural completed/failed run with meaningful
stats. Keep planned cockpit effects distinguished from working integration.

## Verification

The refresh passed 22 DOM interaction checks covering galleries, viewer navigation/zoom/focus restoration,
comparison sliders, layers, annotations and LCD states. JavaScript syntax and asset references were checked.
DOM tests simulate the dialog API; they do not verify browser rendering or native modal behavior.

Layout was then checked in headless Chrome at 320, 390, 768 and 1440 px: no horizontal page overflow (only the
feedback table scrolls inside its own box) and no console errors. Still to check in a real browser before
publishing: dialog focus and Escape, image zoom and panning, touch comparisons, reduced motion, and fonts unavailable.

The section restructure (23 Oct 2026) was checked in headless Chrome over the DevTools protocol at the same
four widths. Every masthead link kept the masthead on top, landed its heading below it, and marked the right
section; the marked link stays visible in the scrolling mobile nav. The page also passed checks for no
horizontal overflow, no section marked at the intro, Evolution marked at the page end, flow-map selection,
feedback defaults, steppers, next links, the viewer opening, unique ids, every image reference loading, and
no console errors. Scrolling by touch or trackpad was not tested.
