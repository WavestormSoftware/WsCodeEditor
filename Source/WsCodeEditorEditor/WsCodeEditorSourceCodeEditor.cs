#if FLAX_EDITOR
#pragma warning disable CS1591
using FlaxEditor;
using FlaxEditor.Modules.SourceCodeEditing;

namespace WsCodeEditorEditor
{
    /// <summary>
    /// Optional Flax source-code editor entry. Users can select it in Editor Options.
    /// </summary>
    public sealed class WsCodeEditorSourceCodeEditor : ISourceCodeEditor
    {
        public string Name => "Ws Code Editor";
        public string GenerateProjectCustomArgs => null;

        public void OpenSolution()
        {
            WsCodeEditorWindow.OpenOrFocus();
        }

        public void OpenFile(string path, int line)
        {
            WsCodeEditorWindow.OpenOrFocus().OpenFile(path, line);
        }

        public void OnFileAdded(string path)
        {
        }

        public void OnSelected(Editor editor)
        {
        }

        public void OnDeselected(Editor editor)
        {
        }

        public void OnAdded(Editor editor)
        {
        }

        public void OnRemoved(Editor editor)
        {
        }
    }
}
#endif
