#if FLAX_EDITOR
#pragma warning disable CS1591
using System;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.GUI;
using FlaxEditor.GUI.ContextMenu;
using FlaxEditor.GUI.Docking;
using FlaxEngine;

namespace WsCodeEditorEditor
{
    /// <summary>
    /// Editor integration for the embedded code editor.
    /// </summary>
    public class WsCodeEditorEditorPlugin : EditorPlugin
    {
        public const string SettingsName = "WsCodeEditor";

        private ToolStripButton _toolstripButton;
        private ContextMenuButton _menuButton;
        private WsCodeEditorSourceCodeEditor _sourceCodeEditor;

        public WsCodeEditorEditorPlugin()
        {
            _description = new PluginDescription
            {
                Name = "WsCodeEditor",
                Category = "Scripting",
                Author = "Wavestorm Software",
                Description = "Dockable in-editor C# code editor for Flax projects.",
                Version = new Version(0, 1, 0),
                IsAlpha = true,
                IsBeta = false,
            };
        }

        public override void InitializeEditor()
        {
            base.InitializeEditor();

            _toolstripButton = Editor.UI.ToolStrip.AddButton("Code Editor");
            _toolstripButton.Clicked += OnOpenWindow;

            _menuButton = Editor.UI.MenuWindow.ContextMenu.AddButton("Code Editor");
            _menuButton.Clicked += OnOpenWindow;

            _sourceCodeEditor = new WsCodeEditorSourceCodeEditor();
            Editor.CodeEditing.AddEditor(_sourceCodeEditor);
            Editor.Options.AddCustomSettings(SettingsName, () => new WsCodeEditorSettings());

            if (Editor.Windows?.ContentWin != null)
                Editor.Windows.ContentWin.ContextMenuShow += OnContentContextMenuShow;
        }

        public override void DeinitializeEditor()
        {
            if (Editor.Windows?.ContentWin != null)
                Editor.Windows.ContentWin.ContextMenuShow -= OnContentContextMenuShow;

            if (_sourceCodeEditor != null)
            {
                Editor.CodeEditing.RemoveEditor(_sourceCodeEditor);
                _sourceCodeEditor = null;
            }

            Editor.Options.RemoveCustomSettings(SettingsName);

            if (_toolstripButton != null)
            {
                _toolstripButton.Clicked -= OnOpenWindow;
                _toolstripButton.Dispose();
                _toolstripButton = null;
            }

            if (_menuButton != null)
            {
                _menuButton.Clicked -= OnOpenWindow;
                _menuButton.Dispose();
                _menuButton = null;
            }

            base.DeinitializeEditor();
        }

        private void OnOpenWindow()
        {
            WsCodeEditorWindow.OpenOrFocus();
        }

        private void OnContentContextMenuShow(ContextMenu menu, ContentItem item)
        {
            if (menu == null || item is not CSharpScriptItem || !ProjectSourceScanner.IsProjectSourceFile(item.Path))
                return;

            menu.AddSeparator();
            menu.AddButton("Open in Ws Code Editor", () => WsCodeEditorWindow.OpenOrFocus().OpenFile(item.Path, 0));
        }
    }
}
#endif
