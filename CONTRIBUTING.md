# Contributing

## Setup

Unity **2022.3.62f1**, 2D + URP. Open the folder, open
`Assets/Scenes/Battle.unity`, run **Skyscraper → Build Battle Scene** if it looks
empty, press Play. First import is slow because `Library/` is built from scratch.

If you are an AI agent, read [docs/AGENTS.md](docs/AGENTS.md) first — it lists
the traps that do not produce error messages.

## Do not commit

The `.gitignore` covers these, but for the avoidance of doubt:

- `Library/`, `Temp/`, `obj/`, `Logs/`, `UserSettings/` — all regenerated, and
  `Library/` alone is 2.3 GB.
- `*.csproj`, `*.sln` — Unity regenerates them from the asmdefs; committing them
  produces churn across machines.
- `Assets/Resources/Bricks/`, `Assets/Resources/Figures/` — extracted from the
  original game, its publisher's copyright. See [docs/ART.md](docs/ART.md).
- `BattleProbe.txt` — probe output, rewritten every play session.
- Keystores, tokens, `.env`.

If you add a dependency that wants a credential, put it in an ignored file and
document the *shape* of it in the README, not the value.

## Code style

Match the file you are in. The existing style is:

- 4-space indent, Allman braces, `_camelCase` private fields.
- Namespaces are flat: everything under `Scripts/Battle/**` is
  `Skyscraper.Battle` regardless of subfolder; config types are
  `Skyscraper.Config`.
- Lines wrap around 80 columns, including comments.

### Comments carry the *why*

This is the one convention worth stating explicitly, because the codebase leans
on it heavily. Doc comments here explain reasoning, provenance and traps — not
what the next line does. Examples worth imitating:

- `BrickShape`'s catalogue comment records the sprite pixel dimensions each
  footprint was transcribed from, so the transcription is checkable.
- `HeightBonus` states outright that the data does **not** contain the ruler and
  that the bands are reconstructed.
- `Figures.HeroAnchor` documents the two approaches that were tried and are
  wrong, and why.

When you fix something subtle, leave the reasoning behind. When you discover an
approach that seems right and isn't, say so in the comment — that is the most
valuable kind of note in this project, and two of them have already prevented
the same mistake twice.

## Numbers are claims

Every size, offset and rate traces back to a measurement off the original —
that is what `RefScale.cs` is for. **Changing a constant is asserting something
about the original game**, so put the measurement in the commit message:

```
Widen the card slot to 284 px

Measured off the 1125x2436 reference capture: slot is 284 px wide with
61 px gaps, not the 270/70 previously assumed.
```

Do not tidy up constants that look arbitrary. `FromSource = 3.125` and
`CellPx = 36` look arbitrary and are load-bearing.

## Verifying a change

Unity does **not** recompile the moment you save. If you measure immediately
after editing, you measure the old assembly and your change will look like a
no-op. This is the most common way to waste an hour here.

1. Force `AssetDatabase.Refresh` + `CompilationPipeline.RequestScriptCompilation`.
2. Poll until `EditorApplication.isCompiling` is false.
3. Check the console: expect **0 errors**. Four warnings are pre-existing and
   harmless (`The referenced script (Unknown) on this Behaviour is missing!`).
4. Confirm the new code actually loaded — check a member that only exists after
   your edit. "Compiled successfully" and "the new assembly is live" are
   different claims.
5. Play-test. Prefer several samples over time to a single reading.
6. Exit play mode and **check the console again** — runtime exceptions only
   surface then.
7. Delete any temporary scripts you created.

**Look at the picture.** A numeric assertion like "the anchor is inside the
bounding box" passes for visibly wrong output; this exact check passed while
heroes were hanging off the ends of bricks. If a change affects what is on
screen, screenshot it.

## Commits

Imperative subject, wrapped body explaining *why*. Reference `file.cs:line`
where it helps. If a change rests on a measurement or a discovered constraint,
that belongs in the body — the commit log is the second place someone looks
after the doc comments.

## Reporting

If something is broken, say so plainly and include the output. If a change is
partial, say which part. Flat coloured polygons in play mode means the art
folders are absent (expected on a fresh clone), not that anything is wrong.
