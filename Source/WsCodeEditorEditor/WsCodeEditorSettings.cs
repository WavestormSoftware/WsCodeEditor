#if FLAX_EDITOR
#pragma warning disable CS1591
using System;
using FlaxEditor;
using FlaxEngine;

namespace WsCodeEditorEditor
{
    public enum WsCodeEditorFontFace
    {
        EditorMedium,
        EditorSmall,
        Inconsolata,
    }

    public enum WsCodeEditorTheme
    {
        Dark,
        SofterDark,
        HighContrast,
    }

    public sealed class WsCodeEditorSettings
    {
        [EditorDisplay("Editor")]
        [Tooltip("Font used for code text. EditorMedium is the safest built-in option across Flax installs.")]
        public WsCodeEditorFontFace FontFace = WsCodeEditorFontFace.EditorMedium;

        [EditorDisplay("Editor")]
        [Limit(9f, 28f, 0.5f)]
        [Tooltip("Reserved for custom font support. Built-in Flax editor fonts use the editor's configured sizes.")]
        public float FontSize = 14f;

        [EditorDisplay("Editor")]
        [Limit(0.75f, 1.5f, 0.01f)]
        [Tooltip("Multiplies the estimated average character width for scrolling. Does not force visible text spacing.")]
        public float CharacterWidthScale = 1.0f;

        [EditorDisplay("Editor")]
        [Limit(0f, 14f, 0.5f)]
        [Tooltip("Extra vertical padding added to every code line.")]
        public float LinePadding = 5f;

        [EditorDisplay("Editor")]
        [Limit(2, 8)]
        [Tooltip("Number of spaces inserted when pressing Tab.")]
        public int TabSize = 4;

        [EditorDisplay("Layout")]
        [Limit(44f, 96f, 1f)]
        [Tooltip("Width of the line number gutter.")]
        public float GutterWidth = 58f;

        [EditorDisplay("Layout")]
        [Limit(0.15f, 0.45f, 0.01f)]
        [Tooltip("Initial fraction of the window used by the project file tree.")]
        public float ProjectTreeWidth = 0.26f;

        [EditorDisplay("Layout")]
        [Limit(44f, 76f, 1f)]
        [Tooltip("Height of the editor toolbar.")]
        public float ToolbarHeight = 58f;

        [EditorDisplay("Behavior")]
        [Tooltip("Automatically insert matching braces, brackets, parentheses, and quotes.")]
        public bool AutoPairBrackets = true;

        [EditorDisplay("Behavior")]
        [Tooltip("Ask before discarding unsaved changes when reloading or closing files.")]
        public bool ConfirmUnsavedChanges = true;

        [EditorDisplay("Behavior")]
        [Tooltip("Prompt to reload a file when it changes on disk and the tab is clean.")]
        public bool PromptReloadExternalChanges = true;

        [EditorDisplay("Appearance")]
        [Tooltip("Show line numbers in the gutter.")]
        public bool ShowLineNumbers = true;

        [EditorDisplay("Appearance")]
        [Tooltip("Highlight the caret's current line.")]
        public bool HighlightCurrentLine = true;

        [EditorDisplay("Appearance")]
        [Tooltip("Show diagnostics count and red line markers.")]
        public bool ShowDiagnostics = true;

        [EditorDisplay("Appearance")]
        [Tooltip("Editor color theme.")]
        public WsCodeEditorTheme Theme = WsCodeEditorTheme.Dark;

        public static WsCodeEditorSettings Current
        {
            get
            {
                try
                {
                    return Editor.Instance?.Options?.Options?.GetCustomSettings<WsCodeEditorSettings>(WsCodeEditorEditorPlugin.SettingsName) ?? new WsCodeEditorSettings();
                }
                catch
                {
                    return new WsCodeEditorSettings();
                }
            }
        }

        public Color EditorBackground => Theme switch
        {
            WsCodeEditorTheme.SofterDark => new Color(0.13f, 0.14f, 0.155f, 1f),
            WsCodeEditorTheme.HighContrast => new Color(0.035f, 0.04f, 0.05f, 1f),
            _ => new Color(0.105f, 0.11f, 0.125f, 1f),
        };

        public Color GutterBackground => Theme switch
        {
            WsCodeEditorTheme.SofterDark => new Color(0.10f, 0.108f, 0.12f, 1f),
            WsCodeEditorTheme.HighContrast => new Color(0.015f, 0.018f, 0.024f, 1f),
            _ => new Color(0.075f, 0.08f, 0.092f, 1f),
        };

        public Color CurrentLineBackground => Theme switch
        {
            WsCodeEditorTheme.SofterDark => new Color(0.18f, 0.19f, 0.215f, 1f),
            WsCodeEditorTheme.HighContrast => new Color(0.095f, 0.11f, 0.15f, 1f),
            _ => new Color(0.16f, 0.17f, 0.195f, 1f),
        };
    }
}
#endif
