# WsCodeEditor

WsCodeEditor is a Flax Engine 1.12+ editor plugin that embeds a project-only C# code editor directly inside the Flax Editor.

The goal is practical in-editor scripting: open project scripts from the Content Browser, edit them in a dockable Flax window, and keep the normal Flax source-code editor workflow available if you want to use another editor instead.

## Requirements

- Flax Engine 1.12 or newer
- Windows x64 editor
- Project C# scripts under `Source/`

## Features

- Dockable in-editor code editor window
- Project-only source tree rooted at `Source/`
- Content Browser right-click action for project C# scripts
- Optional Flax source-code editor integration through the normal source editor selector
- Save, reload, close, search, and go-to-line actions
- Multiple open files in tabs
- Unsaved-change prompts
- Simple code-editor settings exposed in Flax Editor Settings

## Editor Settings

WsCodeEditor adds a custom settings section in the Flax Editor Settings window.

Useful options include:

- Font face and font size
- Character width scale and line padding
- Tab size
- Line number gutter width
- Project tree width
- Toolbar height
- Auto-pairing for braces, brackets, parentheses, and quotes
- Unsaved-change and external-file reload prompts
- Line numbers, current-line highlight, and diagnostics display
- Editor color theme

## Installation

### Clone Through Flax

1. Open `Tools > Plugins`.
2. Use `Clone Project`.
3. Enter the plugin repository URL.
4. Restart the editor.

### Manual

1. Close Flax Editor.
2. Clone or copy this repository into `<your-game-project>/Plugins/WsCodeEditor/`.
3. Add the plugin reference to your game `.flaxproj`:

```json
"References": [
  {
    "Name": "$(EnginePath)/Flax.flaxproj"
  },
  {
    "Name": "$(ProjectPath)/Plugins/WsCodeEditor/WsCodeEditor.flaxproj"
  }
]
```

4. Restart Flax Editor.

## Limitations

- The editor focuses on project scripts under `Source/` and intentionally ignores plugin source folders.
- This pass is IDE-lite, not a full Visual Studio or Rider replacement.
- Roslyn-backed semantic intelligence is not bundled in this revision.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
