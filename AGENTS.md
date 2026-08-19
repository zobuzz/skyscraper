# AGENTS.md

The agent guide for this repository lives at **[docs/AGENTS.md](docs/AGENTS.md)**.

This file exists because `AGENTS.md` at the repository root is where coding
agents conventionally look. The short version, if you read nothing else:

1. **`Assets/Scenes/Battle.unity` is generated** by
   `Assets/Editor/BattleSceneBuilder.cs` (menu: *Skyscraper → Build Battle
   Scene*). Hand edits to the scene are discarded — change the builder.
2. **Never measure before recompilation finishes.** Unity does not recompile on
   save; reading a value straight after an edit reads the *old* assembly and
   your change will look like it did nothing.
3. **Namespaces are flat** — everything under `Scripts/Battle/**` is
   `Skyscraper.Battle`; `ConfigDB` and the `*Row` types are `Skyscraper.Config`.
   Grep, don't guess.
4. **Some members are fields, some are properties** (`BrickShape.CellsWide` is a
   field; `BattleRuntime.Base` is a property). Reflection must try both or it
   gets a silent `null`.
5. **The art folders are gitignored and optional.** Flat coloured polygons in
   play mode is expected on a fresh clone, not a bug.
6. **Do not add brick shapes** outside the sixteen in `BrickShape.ByCube`.
7. **Constants are measurements**, not preferences. See `RefScale.cs`.

Full detail, including the reflection helper, the geometry table and the
invariants that fail silently: [docs/AGENTS.md](docs/AGENTS.md).

Architecture and reasoning: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
