#if FLAX_EDITOR
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.IO;
using FlaxEditor.GUI.Input;
using FlaxEditor.GUI.Tabs;
using FlaxEditor.GUI.Tree;
using FlaxEditor.Windows;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsCodeEditorEditor
{
    /// <summary>
    /// Dockable project-code editor window.
    /// </summary>
    public class WsCodeEditorWindow : EditorWindow
    {
        private static WsCodeEditorWindow _instance;

        private readonly Dictionary<string, OpenDocument> _documentsByPath = new Dictionary<string, OpenDocument>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Tab, OpenDocument> _documentsByTab = new Dictionary<Tab, OpenDocument>();

        private SearchBox _filterBox;
        private Tree _fileTree;
        private Tabs _tabs;
        private ContainerControl _toolbar;
        private Label _pathLabel;
        private Label _statusLabel;
        private Label _diagnosticsLabel;
        private TextBox _findBox;
        private TextBox _goLineBox;
        private Button _saveButton;
        private Button _reloadButton;
        private Button _closeButton;
        private Button _goButton;
        private OpenDocument _activeDocument;

        public static WsCodeEditorWindow OpenOrFocus()
        {
            if (_instance == null || _instance.IsDisposing)
            {
                _instance = new WsCodeEditorWindow();
                _instance.Show();
            }
            else
            {
                _instance.Focus();
            }

            return _instance;
        }

        public WsCodeEditorWindow() : base(FlaxEditor.Editor.Instance, false, ScrollBars.None)
        {
            _instance = this;
            Title = "Ws Code Editor";
            BuildUi();
            RefreshFileTree();
            UpdateHeader();
        }

        public void OpenFile(string path, int line = 0)
        {
            if (!ProjectSourceScanner.IsProjectSourceFile(path))
            {
                _statusLabel.Text = "Ws Code Editor only opens scripts under the project Source folder.";
                _statusLabel.TextColor = new Color(0.95f, 0.6f, 0.25f, 1f);
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (_documentsByPath.TryGetValue(fullPath, out var existing))
            {
                SelectDocument(existing);
                if (line > 0)
                    existing.Editor.JumpToLine(line);
                return;
            }

            try
            {
                var text = File.ReadAllText(fullPath);
                var tab = _tabs.AddTab(new Tab(Path.GetFileName(fullPath)));
                var editor = new CodeEditorControl
                {
                    Parent = tab,
                    AnchorPreset = AnchorPresets.StretchAll,
                    Offsets = Margin.Zero,
                    FilePath = fullPath,
                };
                editor.LoadText(text);

                var document = new OpenDocument(fullPath, tab, editor, File.GetLastWriteTimeUtc(fullPath));

                document.SaveRequestedHandler = SaveCurrentFile;
                document.DirtyChangedHandler = () => OnEditorDirtyChanged(fullPath);
                document.FindRequestedHandler = () => _findBox?.Focus();
                document.GoToLineRequestedHandler = () => _goLineBox?.Focus();
                document.DiagnosticsChangedHandler = _ => UpdateHeader();

                editor.SaveRequested += document.SaveRequestedHandler;
                editor.DirtyChanged += document.DirtyChangedHandler;
                editor.FindRequested += document.FindRequestedHandler;
                editor.GoToLineRequested += document.GoToLineRequestedHandler;
                editor.DiagnosticsChanged += document.DiagnosticsChangedHandler;

                _documentsByPath.Add(fullPath, document);
                _documentsByTab.Add(tab, document);

                SelectDocument(document);
                if (line > 0)
                    editor.JumpToLine(line);

                _statusLabel.Text = "Editing project source inside Flax.";
                _statusLabel.TextColor = Style.Current.ForegroundGrey;
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to open file: {ex.Message}";
                _statusLabel.TextColor = new Color(0.95f, 0.35f, 0.35f, 1f);
                Debug.LogError(ex);
            }
        }

        private void BuildUi()
        {
            var split = new SplitPanel(Orientation.Horizontal, ScrollBars.None, ScrollBars.None)
            {
                Parent = this,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = Margin.Zero,
                SplitterValue = Mathf.Clamp(WsCodeEditorSettings.Current.ProjectTreeWidth, 0.15f, 0.45f),
            };

            BuildFileBrowser(split.Panel1);
            BuildEditorArea(split.Panel2);
        }

        private void BuildFileBrowser(ContainerControl parent)
        {
            var header = new ContainerControl
            {
                Parent = parent,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, 34),
                BackgroundColor = new Color(0.09f, 0.095f, 0.105f, 1f),
            };

            _filterBox = new SearchBox
            {
                Parent = header,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(6, 6, 6, 22),
                WatermarkText = "Filter project C# files...",
            };
            _filterBox.TextChanged += RefreshFileTree;

            var treePanel = new Panel(ScrollBars.Both)
            {
                Parent = parent,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, 34, 0),
                BackgroundColor = new Color(0.08f, 0.085f, 0.095f, 1f),
            };

            _fileTree = new Tree(false)
            {
                Parent = treePanel,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
            };
            _fileTree.SelectedChanged += OnFileTreeSelectionChanged;
        }

        private void BuildEditorArea(ContainerControl parent)
        {
            _toolbar = new ContainerControl
            {
                Parent = parent,
                AnchorPreset = AnchorPresets.HorizontalStretchTop,
                Offsets = new Margin(0, 0, 0, GetToolbarHeight()),
                BackgroundColor = new Color(0.095f, 0.1f, 0.112f, 1f),
            };

            _pathLabel = new Label
            {
                Parent = _toolbar,
                Text = "Open a project script from Source.",
                TextColor = Style.Current.ForegroundGrey,
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };

            _diagnosticsLabel = new Label
            {
                Parent = _toolbar,
                Text = "Diagnostics: 0",
                TextColor = Style.Current.ForegroundGrey,
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };

            _saveButton = CreateActionButton("Save", SaveCurrentFile, 52f, "Save current file (Ctrl+S)");
            _saveButton.Parent = _toolbar;

            _reloadButton = CreateActionButton("Reload", ReloadCurrentFile, 62f, "Reload file from disk");
            _reloadButton.Parent = _toolbar;

            _closeButton = CreateActionButton("Close File", CloseCurrentTab, 72f, "Close current file tab");
            _closeButton.Parent = _toolbar;

            _findBox = new TextBox(false, 0, 0, 0)
            {
                Parent = _toolbar,
                Width = 128f,
                Height = 24f,
                WatermarkText = "Find",
            };
            _findBox.TextChanged += OnFindBoxTextChanged;

            _goLineBox = new TextBox(false, 0, 0, 0)
            {
                Parent = _toolbar,
                Width = 72f,
                Height = 24f,
                WatermarkText = "Line",
            };

            _goButton = CreateActionButton("Go", GoToLine, 30f, "Jump to line");
            _goButton.Parent = _toolbar;

            _tabs = new Tabs
            {
                Parent = parent,
                AnchorPreset = AnchorPresets.StretchAll,
                Offsets = new Margin(0, 0, GetToolbarHeight(), 22),
                Orientation = Orientation.Horizontal,
                UseScroll = true,
                TabsSize = new Float2(150f, 26f),
            };
            _tabs.SelectedTabChanged += OnSelectedTabChanged;

            _statusLabel = new Label
            {
                Parent = parent,
                AnchorPreset = AnchorPresets.HorizontalStretchBottom,
                Offsets = new Margin(8, 8, 0, 20),
                Text = "Project Source only. Plugin code stays read-only/outside this editor.",
                TextColor = Style.Current.ForegroundGrey,
                HorizontalAlignment = TextAlignment.Near,
                VerticalAlignment = TextAlignment.Center,
            };

            LayoutEditorToolbar();
        }

        private static Button CreateActionButton(string text, Action clicked, float width, string tooltip)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 24f,
                TooltipText = tooltip,
                BackgroundColor = Style.Current.BackgroundNormal,
                BackgroundColorHighlighted = Style.Current.BackgroundHighlighted,
            };
            button.Clicked += clicked;
            return button;
        }

        private void RefreshFileTree()
        {
            if (_fileTree == null)
                return;

            _fileTree.DisposeChildren();

            var root = ProjectSourceScanner.ProjectSourceRoot;
            var rootNode = new TreeNode
            {
                Text = "Project Source",
                TextColor = Style.Current.ForegroundGrey,
                TooltipText = root,
                Parent = _fileTree,
            };

            var files = new List<string>(ProjectSourceScanner.GetProjectCSharpFiles(_filterBox?.Text));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
                AddFileNode(rootNode, root, file);

            if (rootNode.HasChildren)
                rootNode.Expand();

            _fileTree.PerformLayout();
        }

        private void AddFileNode(TreeNode rootNode, string rootPath, string filePath)
        {
            var relative = Path.GetRelativePath(rootPath, filePath);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = rootNode;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var folder = FindChild(parent, parts[i]);
                if (folder == null)
                {
                    folder = new TreeNode
                    {
                        Text = parts[i],
                        TextColor = Style.Current.ForegroundGrey,
                        Parent = parent,
                    };
                    folder.Expand();
                }

                parent = folder;
            }

            new CodeFileTreeNode(filePath, ProjectSourceScanner.GetProjectRelativePath(filePath))
            {
                Parent = parent,
            };
        }

        private static TreeNode FindChild(TreeNode parent, string text)
        {
            for (var i = 0; i < parent.ChildrenCount; i++)
            {
                if (parent.Children[i] is TreeNode node && string.Equals(node.Text, text, StringComparison.OrdinalIgnoreCase))
                    return node;
            }

            return null;
        }

        private void OnFileTreeSelectionChanged(List<TreeNode> before, List<TreeNode> after)
        {
            if (after == null || after.Count == 0 || after[0] is not CodeFileTreeNode fileNode)
                return;

            OpenFile(fileNode.FullPath, 0);
        }

        private void OnSelectedTabChanged(Tabs tabs)
        {
            var tab = tabs?.SelectedTab;
            if (tab != null && _documentsByTab.TryGetValue(tab, out var document))
                _activeDocument = document;
            else
                _activeDocument = null;

            UpdateHeader();
        }

        private void SelectDocument(OpenDocument document)
        {
            _activeDocument = document;
            _tabs.SelectedTab = document.Tab;
            document.Editor.Focus();
            UpdateHeader();
        }

        private void OnEditorDirtyChanged(string path)
        {
            if (_documentsByPath.TryGetValue(path, out var document))
                UpdateTabTitle(document);

            UpdateHeader();
        }

        private void UpdateTabTitle(OpenDocument document)
        {
            document.Tab.Text = document.Editor.IsDirty ? "*" + Path.GetFileName(document.Path) : Path.GetFileName(document.Path);
        }

        private void SaveCurrentFile()
        {
            if (_activeDocument == null)
                return;

            try
            {
                File.WriteAllText(_activeDocument.Path, _activeDocument.Editor.GetText());
                _activeDocument.LastWriteTimeUtc = File.GetLastWriteTimeUtc(_activeDocument.Path);
                _activeDocument.Editor.MarkClean();
                UpdateTabTitle(_activeDocument);
                _statusLabel.Text = $"Saved {Path.GetFileName(_activeDocument.Path)}";
                _statusLabel.TextColor = new Color(0.35f, 0.85f, 0.42f, 1f);
                UpdateHeader();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to save file: {ex.Message}";
                _statusLabel.TextColor = new Color(0.95f, 0.35f, 0.35f, 1f);
                Debug.LogError(ex);
            }
        }

        private void ReloadCurrentFile()
        {
            if (_activeDocument == null || !File.Exists(_activeDocument.Path))
                return;

            if (_activeDocument.Editor.IsDirty && WsCodeEditorSettings.Current.ConfirmUnsavedChanges && !ConfirmDiscardChanges("Reloading will discard unsaved changes. Continue?"))
                return;

            try
            {
                _activeDocument.Editor.LoadText(File.ReadAllText(_activeDocument.Path));
                _activeDocument.LastWriteTimeUtc = File.GetLastWriteTimeUtc(_activeDocument.Path);
                UpdateTabTitle(_activeDocument);
                _statusLabel.Text = $"Reloaded {Path.GetFileName(_activeDocument.Path)}";
                _statusLabel.TextColor = Style.Current.ForegroundGrey;
                UpdateHeader();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to reload file: {ex.Message}";
                _statusLabel.TextColor = new Color(0.95f, 0.35f, 0.35f, 1f);
                Debug.LogError(ex);
            }
        }

        private void CloseCurrentTab()
        {
            if (_activeDocument == null)
                return;

            CloseDocument(_activeDocument);
        }

        private void DisposeDocument(OpenDocument document)
        {
            if (document == null)
                return;

            document.Editor.SaveRequested -= document.SaveRequestedHandler;
            document.Editor.DirtyChanged -= document.DirtyChangedHandler;
            document.Editor.FindRequested -= document.FindRequestedHandler;
            document.Editor.GoToLineRequested -= document.GoToLineRequestedHandler;
            document.Editor.DiagnosticsChanged -= document.DiagnosticsChangedHandler;

            _documentsByPath.Remove(document.Path);
            _documentsByTab.Remove(document.Tab);
            document.Tab.Dispose();
        }

        private void CloseDocument(OpenDocument document)
        {
            if (document.Editor.IsDirty && WsCodeEditorSettings.Current.ConfirmUnsavedChanges && !ConfirmSaveBeforeClose(document))
                return;

            DisposeDocument(document);

            _activeDocument = null;
            foreach (var pair in _documentsByTab)
            {
                SelectDocument(pair.Value);
                break;
            }

            UpdateHeader();
        }

        private bool ConfirmSaveBeforeClose(OpenDocument document)
        {
            var result = MessageBox.Show($"Save changes to {Path.GetFileName(document.Path)} before closing?", "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                SelectDocument(document);
                SaveCurrentFile();
            }

            return true;
        }

        private static bool ConfirmDiscardChanges(string message)
        {
            return MessageBox.Show(message, "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        private void GoToLine()
        {
            if (_activeDocument == null)
                return;

            if (int.TryParse(_goLineBox.Text, out var line))
                _activeDocument.Editor.JumpToLine(line);
            _activeDocument.Editor.Focus();
        }

        private void OnFindBoxTextChanged()
        {
            _activeDocument?.Editor.Find(_findBox.Text);
        }

        private void UpdateHeader()
        {
            var hasFile = _activeDocument != null;
            _saveButton.Enabled = hasFile;
            _reloadButton.Enabled = hasFile;
            _closeButton.Enabled = hasFile;

            if (!hasFile)
            {
                _pathLabel.Text = "Open a project script from Source.";
                _diagnosticsLabel.Text = WsCodeEditorSettings.Current.ShowDiagnostics ? "Diagnostics: 0" : "Diagnostics hidden";
                Title = "Ws Code Editor";
                return;
            }

            var relativePath = ProjectSourceScanner.GetProjectRelativePath(_activeDocument.Path);
            _pathLabel.Text = (_activeDocument.Editor.IsDirty ? "* " : string.Empty) + relativePath;
            var count = _activeDocument.Editor.Diagnostics?.Count ?? 0;
            if (WsCodeEditorSettings.Current.ShowDiagnostics)
            {
                _diagnosticsLabel.Text = $"Diagnostics: {count}" + (_activeDocument.Editor.Diagnostics != null && count > 0 ? " (see red markers)" : string.Empty);
                _diagnosticsLabel.TextColor = count > 0 ? new Color(0.95f, 0.6f, 0.25f, 1f) : Style.Current.ForegroundGrey;
            }
            else
            {
                _diagnosticsLabel.Text = "Diagnostics hidden";
                _diagnosticsLabel.TextColor = Style.Current.ForegroundGrey;
            }
            Title = _activeDocument.Editor.IsDirty ? $"Ws Code Editor* - {Path.GetFileName(_activeDocument.Path)}" : $"Ws Code Editor - {Path.GetFileName(_activeDocument.Path)}";
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            if (_toolbar != null)
            {
                var toolbarHeight = GetToolbarHeight();
                _toolbar.Offsets = new Margin(0, 0, 0, toolbarHeight);
                _tabs.Offsets = new Margin(0, 0, toolbarHeight, 22);
            }
            LayoutEditorToolbar();
            CheckExternalReloads();
        }

        private void LayoutEditorToolbar()
        {
            if (_toolbar == null)
                return;

            var right = Math.Max(360f, _toolbar.Width - 6f);
            var top = 4f;
            var gap = 5f;

            PlaceFromRight(_closeButton, ref right, top, 72f);
            PlaceFromRight(_reloadButton, ref right, top, 62f, gap);
            PlaceFromRight(_saveButton, ref right, top, 52f, gap);

            _pathLabel.Bounds = new Rectangle(8f, 2f, Math.Max(80f, right - 14f), 22f);

            right = Math.Max(300f, _toolbar.Width - 6f);
            PlaceFromRight(_goButton, ref right, 30f, 30f);
            PlaceFromRight(_goLineBox, ref right, 30f, 66f, gap);
            PlaceFromRight(_findBox, ref right, 30f, 128f, gap);
            _diagnosticsLabel.Bounds = new Rectangle(8f, 28f, Math.Max(80f, right - 14f), 22f);
        }

        private static void PlaceFromRight(Control control, ref float right, float y, float width, float gap = 0f)
        {
            if (control == null)
                return;

            right -= width;
            control.Bounds = new Rectangle(right, y, width, 22f);
            right -= gap;
        }

        public override bool OnKeyDown(KeyboardKeys key)
        {
            if (Input.GetKey(KeyboardKeys.Control))
            {
                if (key == KeyboardKeys.F)
                {
                    _findBox?.Focus();
                    return true;
                }

                if (key == KeyboardKeys.G)
                {
                    _goLineBox?.Focus();
                    return true;
                }
            }

            return base.OnKeyDown(key);
        }

        private void CheckExternalReloads()
        {
            if (_activeDocument == null || !File.Exists(_activeDocument.Path))
                return;

            if (!WsCodeEditorSettings.Current.PromptReloadExternalChanges)
                return;

            var currentWrite = File.GetLastWriteTimeUtc(_activeDocument.Path);
            if (currentWrite <= _activeDocument.LastWriteTimeUtc)
                return;

            _activeDocument.LastWriteTimeUtc = currentWrite;
            if (_activeDocument.Editor.IsDirty)
            {
                _statusLabel.Text = "File changed on disk while this tab has unsaved edits.";
                _statusLabel.TextColor = new Color(0.95f, 0.6f, 0.25f, 1f);
                return;
            }

            if (MessageBox.Show($"{Path.GetFileName(_activeDocument.Path)} changed on disk. Reload it?", "File Changed", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                ReloadCurrentFile();
        }

        private static float GetToolbarHeight()
        {
            return Mathf.Clamp(WsCodeEditorSettings.Current.ToolbarHeight, 44f, 76f);
        }

        public override void OnDestroy()
        {
            if (_tabs != null)
                _tabs.SelectedTabChanged -= OnSelectedTabChanged;
            if (_filterBox != null)
                _filterBox.TextChanged -= RefreshFileTree;
            if (_fileTree != null)
                _fileTree.SelectedChanged -= OnFileTreeSelectionChanged;
            if (_findBox != null)
                _findBox.TextChanged -= OnFindBoxTextChanged;

            var docs = new List<OpenDocument>(_documentsByPath.Values);
            foreach (var doc in docs)
            {
                DisposeDocument(doc);
            }
            _documentsByPath.Clear();
            _documentsByTab.Clear();
            _activeDocument = null;

            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }

        private sealed class OpenDocument
        {
            public readonly string Path;
            public readonly Tab Tab;
            public readonly CodeEditorControl Editor;
            public DateTime LastWriteTimeUtc;

            public Action SaveRequestedHandler;
            public Action DirtyChangedHandler;
            public Action FindRequestedHandler;
            public Action GoToLineRequestedHandler;
            public Action<int> DiagnosticsChangedHandler;

            public OpenDocument(string path, Tab tab, CodeEditorControl editor, DateTime lastWriteTimeUtc)
            {
                Path = path;
                Tab = tab;
                Editor = editor;
                LastWriteTimeUtc = lastWriteTimeUtc;
            }
        }

        private sealed class CodeFileTreeNode : TreeNode
        {
            public string FullPath { get; }

            public CodeFileTreeNode(string fullPath, string relativePath)
            {
                FullPath = fullPath;
                Text = Path.GetFileName(fullPath);
                TooltipText = relativePath;
            }
        }
    }
}
#endif
