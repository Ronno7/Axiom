# Geometric Flight project guidance

- Read `Docs/DESIGN_BRIEF.md` and `Docs/CODEX_HANDOFF.md` before implementing features.
- Target Unity 6.3 URP. Retain the installed 6.3 patch and template package versions unless an identified issue requires a change.
- The prototype has forward flight with throttle, bounded keyboard steering, a forward-following camera, and hold-to-fire shooting at shootable targets. Read `Docs/FLIGHT.md` and `Docs/COMBAT.md` for the component design and verification boundary. Follow the user's current milestone; the design brief is context, not a request to implement all planned features at once.
- Keep changes small, readable, and easy to inspect in Unity.
- Preserve scene edits, `.meta` files, and asset GUIDs. Do not overwrite the whole project or regenerate scenes over user edits.
- Keep editor utilities in `Editor`; avoid import-time project mutations.
- Validate changed behavior in Unity when an editor/MCP connection exists. Report source-only checks separately from editor/build/playtesting.
- The game is titled **Axiom**. `GeometricFlight` remains the code namespace.
