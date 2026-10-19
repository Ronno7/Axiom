# Geometric Flight project guidance

- Read `Docs/DESIGN_BRIEF.md` and `Docs/CODEX_HANDOFF.md` before implementing features.
- Target Unity 6.3 URP. Retain the installed 6.3 patch and template package versions unless an identified issue requires a change.
- Level 1 is playable end to end with menus, a keyboard and an Arduino controller. Read `Docs/FLIGHT.md`, `Docs/COMBAT.md`, `Docs/UI.md` and `Arduino/cockpit/README.md` for the design and verification boundary. Follow the user's current milestone; the design brief is context, not a request to implement all planned features at once.
- Keep changes small, readable, and easy to inspect in Unity.
- Preserve scene edits, `.meta` files, and asset GUIDs. Do not overwrite the whole project or regenerate scenes over user edits.
- Keep editor utilities in `Editor`; avoid import-time project mutations.
- Validate changed behavior in Unity when an editor/MCP connection exists. Report source-only checks separately from editor/build/playtesting.
- The game is titled **Axiom**. `GeometricFlight` remains the code namespace.
