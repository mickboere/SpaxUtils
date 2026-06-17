# Debuddy

A file-backed, session-scoped, filtered Unity **log capture** tool built for working
alongside an AI coding agent. It mirrors the Console into a small plain-text file the
agent can read, and exposes all of its settings as a JSON file the agent can edit — so
the whole debugging loop runs without copy-pasting logs back and forth.

> Editor-only. All scripts are wrapped in `#if UNITY_EDITOR` and live under `Editor/`,
> so nothing ships in a build.

## Quickstart (humans)

1. **Install** — drop the `Debuddy/` folder anywhere under `Assets/`. Unity compiles it; capture starts automatically.
2. **Open the window** — `Tools > Debuddy > Log Capture`. Dock it next to the Console if you like.
3. **Use it** — leave it on (`● Capturing`) and press Play. Logs stream into the list, just like the Console. The log is cleared at the start of each Play session.
4. **Toolbar at a glance:**
   - `● Capturing / ○ Paused` — master on/off.
   - `Clear` — wipe the current log + list.
   - info / warning / error icons — per-severity capture toggles.
   - `Stack` — include stack traces (off = clean, message-only lines).
   - `Reveal File` — open `Logs/Debuddy.log` in your file browser.
   - `Include` / `Exclude` — comma-separated keyword filters. `Search` filters the *displayed* list only (doesn't change what's captured).

That's the whole human workflow: **keep it open, press Play, read the list.** Everything you set is saved to `Logs/Debuddy.config.json`, which is also how an AI agent reads and drives the tool (below).

## For AI agents

Debuddy exists so you can debug *with the human without copy-paste*. Your entire interface is two files in the project-root `Logs/` folder (paths are constant; both are gitignored and outside `Assets/`):

- **Read output:** `Logs/Debuddy.log` — the captured lines, format `[<seconds>s f=<frame>] message`, times/frames relative to the start of the current Play session.
- **Configure:** `Logs/Debuddy.config.json` — edit this file to control capture. It hot-reloads within ~0.5s; no recompile, no human action needed.

**The handshake:** the human says e.g. "use Debuddy to debug X" → *you* set up the filter by editing the config → human presses Play, reproduces, and says "test complete" → *you* read `Logs/Debuddy.log`. The log is **session-scoped** (cleared on Play), so always read it *after* the human has played, and its contents are exactly that one session.

**To narrow capture**, write keywords into the config. Example — only lines mentioning the systems you care about:

```json
{
    "enabled": true,
    "includeKeywords": ["AEMOI", "Balance", "[Hit]"],
    "excludeKeywords": ["Audio"],
    "captureInfo": true,
    "captureWarnings": true,
    "captureErrors": true,
    "includeStack": false,
    "maxFileSizeKB": 5120
}
```

Rules of engagement:

- **Empty `includeKeywords` = capture everything.** Add keywords to focus; matching is case-insensitive substring. `excludeKeywords` always wins.
- Set `includeStack: true` only when you need traces (e.g. chasing an exception) — it makes the file bigger and noisier.
- Keep it **valid JSON, exact field names** (parsed by Unity `JsonUtility`; no comments).
- **Don't edit the `.cs` files to change capture behavior** — the config is the control surface. Touch the scripts only to change the tool itself.
- To get fresh instrumentation, ask the human to add `Debug.Log` lines (or add them yourself) with a unique tag, then set that tag as an include keyword.

## How it works

`DebuddyCore` hooks `Application.logMessageReceivedThreaded` and, when capture is
enabled, writes each matching line to a log file. The `DebuddyWindow` is just a
human-facing mirror — a Console-style toolbar + list + detail pane bound to the same
config. Both read/write the same files, and the core hot-reloads the config on change,
so edits apply live (even during Play).

## The two files (the agent's interface)

Both live in the project-root `Logs/` folder — **outside `Assets/`** (no reimport) and
already **gitignored** (no noise).

| File | Written by | Read by | Purpose |
|---|---|---|---|
| `Logs/Debuddy.config.json` | agent / window / hand | core + window | all settings |
| `Logs/Debuddy.log` | core | agent | filtered output |

The loop: **agent edits the JSON → human presses Play → agent reads the `.log`.**

### Output format

```
[12.34s f=740] message text
```

`time`/`frame` are relative to the start of the current Play session. Stack traces are
appended on the next lines only when `includeStack` is true.

### `Debuddy.config.json` schema

```json
{
    "enabled": true,
    "includeKeywords": [],
    "excludeKeywords": [],
    "captureInfo": true,
    "captureWarnings": true,
    "captureErrors": true,
    "includeStack": false,
    "maxFileSizeKB": 5120
}
```

| Field | Meaning |
|---|---|
| `enabled` | Master switch. `false` captures nothing. |
| `includeKeywords` | If non-empty, only messages containing one of these (case-insensitive) substrings are captured. **Empty = capture everything.** |
| `excludeKeywords` | Messages containing any of these are dropped, even if they matched an include keyword. |
| `captureInfo` / `captureWarnings` / `captureErrors` | Per-severity toggles (`Assert`/`Exception` count as errors). |
| `includeStack` | Append the stack trace beneath each line. Off = message-only. |
| `maxFileSizeKB` | Safety backstop: once the log reaches this size, capture **stops** and `[Debuddy] size cap of N KB reached` is appended — existing data is preserved. `0` = unlimited. Config-only (not in the window); resets each session. |

> The file is parsed by Unity's `JsonUtility`, so it must stay valid JSON (no comments,
> exact field names). Editing it triggers a hot-reload within ~0.5s.

## Lifecycle

- The log is **cleared on entering Play** — each session starts fresh.
- Capture runs in edit mode too (whatever is logged in the Editor is captured).
- The window polls `DebuddyCore.Version` to repaint as entries arrive.

## Window

`Tools > Debuddy > Log Capture`. Toolbar: capture toggle, Clear, per-severity icons,
Stack toggle, Reveal File. Below: Include/Exclude/Search fields, then the entry list
and a detail pane for the selected entry.
