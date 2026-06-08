#if FLAX_EDITOR
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Text;
using FlaxEngine;
using FlaxEngine.GUI;

namespace WsCodeEditorEditor
{
    /// <summary>
    /// Lightweight code editor control with manual rendering and a line-based document model.
    /// </summary>
    public class CodeEditorControl : Control
    {
        public event Action SaveRequested;
        public event Action DirtyChanged;
        public event Action FindRequested;
        public event Action GoToLineRequested;
        public event Action<int> DiagnosticsChanged;

        private readonly List<string> _lines = new List<string> { string.Empty };
        private readonly Stack<string> _undo = new Stack<string>();
        private readonly Stack<string> _redo = new Stack<string>();
        private SyntaxAnalysisResult _syntax = SyntaxAnalysisResult.Empty;
        private string _loadedText = string.Empty;
        private string _filePath = string.Empty;
        private string _findText = string.Empty;
        private float _lineHeight = 18f;
        private float _charWidth = 8f;
        private float _blinkTime;
        private float _controlTime;
        private int _caretLine;
        private int _caretColumn;
        private int _selectionLine = -1;
        private int _selectionColumn = -1;
        private int _scrollLine;
        private int _scrollColumn;
        private int _preferredColumn = -1;
        private int _lastMouseDownLine = -1;
        private int _lastMouseDownColumn = -1;
        private double _lastMouseDownTime;
        private bool _isDirty;
        private bool _draggingSelection;

        private const float TextPadding = 8f;
        private const int MaxUndoSnapshots = 200;

        public string FilePath
        {
            get => _filePath;
            set => _filePath = value ?? string.Empty;
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value)
                    return;

                _isDirty = value;
                DirtyChanged?.Invoke();
            }
        }

        public string Text
        {
            get => GetText();
            set => LoadText(value);
        }

        public IReadOnlyList<CodeDiagnostic> Diagnostics => _syntax.Diagnostics;

        public CodeEditorControl()
        {
            BackgroundColor = WsCodeEditorSettings.Current.EditorBackground;
        }

        public void LoadText(string text)
        {
            _loadedText = NormalizeNewLines(text ?? string.Empty);
            SetTextInternal(_loadedText);
            _undo.Clear();
            _redo.Clear();
            _caretLine = 0;
            _caretColumn = 0;
            ClearSelection();
            _scrollLine = 0;
            _scrollColumn = 0;
            IsDirty = false;
            AnalyzeNow();
        }

        public string GetText()
        {
            return string.Join(Environment.NewLine, _lines);
        }

        public void MarkClean()
        {
            _loadedText = NormalizeNewLines(GetText());
            IsDirty = false;
        }

        public void JumpToLine(int line)
        {
            if (line <= 0)
                line = 1;

            _caretLine = Mathf.Clamp(line - 1, 0, _lines.Count - 1);
            _caretColumn = 0;
            ClearSelection();
            EnsureCaretVisible();
        }

        public void Find(string text)
        {
            _findText = text ?? string.Empty;
            if (string.IsNullOrEmpty(_findText))
                return;

            var startLine = _caretLine;
            var startColumn = Mathf.Min(_caretColumn + 1, _lines[_caretLine].Length);
            for (var pass = 0; pass < 2; pass++)
            {
                var line = pass == 0 ? startLine : 0;
                for (; line < _lines.Count; line++)
                {
                    var column = line == startLine && pass == 0 ? startColumn : 0;
                    var index = _lines[line].IndexOf(_findText, column, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        _caretLine = line;
                        _caretColumn = index + _findText.Length;
                        _selectionLine = line;
                        _selectionColumn = index;
                        EnsureCaretVisible();
                        return;
                    }
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            _blinkTime += deltaTime;
            _controlTime += deltaTime;

            if (IsMouseOver)
            {
                var delta = Input.MouseScrollDelta;
                if (Math.Abs(delta) > 0.01f)
                {
                    if (Input.GetKey(KeyboardKeys.Shift))
                        _scrollColumn = Math.Max(0, _scrollColumn - (int)Math.Sign(delta) * 6);
                    else
                        _scrollLine = Mathf.Clamp(_scrollLine - (int)Math.Sign(delta) * 3, 0, Math.Max(0, _lines.Count - 1));
                }
            }
        }

        public override bool OnCharInput(char c)
        {
            if (char.IsControl(c))
                return base.OnCharInput(c);

            InsertCharacter(c);
            return true;
        }

        public override bool OnKeyDown(KeyboardKeys key)
        {
            var ctrl = Input.GetKey(KeyboardKeys.Control);
            var shift = Input.GetKey(KeyboardKeys.Shift);

            if (ctrl)
            {
                if (key == KeyboardKeys.S)
                {
                    SaveRequested?.Invoke();
                    return true;
                }

                if (key == KeyboardKeys.A)
                {
                    SelectAll();
                    return true;
                }

                if (key == KeyboardKeys.C)
                {
                    CopySelection();
                    return true;
                }

                if (key == KeyboardKeys.X)
                {
                    CutSelection();
                    return true;
                }

                if (key == KeyboardKeys.V)
                {
                    PasteClipboard();
                    return true;
                }

                if (key == KeyboardKeys.Z)
                {
                    Undo();
                    return true;
                }

                if (key == KeyboardKeys.Y)
                {
                    Redo();
                    return true;
                }

                if (key == KeyboardKeys.F)
                {
                    FindRequested?.Invoke();
                    return true;
                }

                if (key == KeyboardKeys.G)
                {
                    GoToLineRequested?.Invoke();
                    return true;
                }
            }

            switch (key)
            {
                case KeyboardKeys.Backspace:
                    Backspace();
                    return true;
                case KeyboardKeys.Delete:
                    Delete();
                    return true;
                case KeyboardKeys.Return:
                    InsertNewLine();
                    return true;
                case KeyboardKeys.Tab:
                    if (shift)
                        OutdentSelection();
                    else
                        IndentSelection();
                    return true;
                case KeyboardKeys.ArrowLeft:
                    MoveHorizontal(-1, shift);
                    return true;
                case KeyboardKeys.ArrowRight:
                    MoveHorizontal(1, shift);
                    return true;
                case KeyboardKeys.ArrowUp:
                    MoveVertical(-1, shift);
                    return true;
                case KeyboardKeys.ArrowDown:
                    MoveVertical(1, shift);
                    return true;
                case KeyboardKeys.Home:
                    MoveHome(shift);
                    return true;
                case KeyboardKeys.End:
                    MoveEnd(shift);
                    return true;
                case KeyboardKeys.PageUp:
                    MoveVertical(-GetVisibleLineCount(), shift);
                    return true;
                case KeyboardKeys.PageDown:
                    MoveVertical(GetVisibleLineCount(), shift);
                    return true;
            }

            return base.OnKeyDown(key);
        }

        public override bool OnMouseDown(Float2 location, MouseButton button)
        {
            if (button != MouseButton.Left)
                return base.OnMouseDown(location, button);

            Focus();
            var position = ScreenToTextPosition(location);
            var now = _controlTime;
            if (_lastMouseDownLine == position.Line && _lastMouseDownColumn == position.Column && now - _lastMouseDownTime < 0.35)
            {
                SelectWord(position.Line, position.Column);
            }
            else
            {
                _caretLine = position.Line;
                _caretColumn = position.Column;
                _selectionLine = _caretLine;
                _selectionColumn = _caretColumn;
                _draggingSelection = true;
            }

            _lastMouseDownLine = position.Line;
            _lastMouseDownColumn = position.Column;
            _lastMouseDownTime = now;
            EnsureCaretVisible();
            return true;
        }

        public override void OnMouseMove(Float2 location)
        {
            base.OnMouseMove(location);
            if (!_draggingSelection || !Input.GetMouseButton(MouseButton.Left))
                return;

            var position = ScreenToTextPosition(location);
            _caretLine = position.Line;
            _caretColumn = position.Column;
            EnsureCaretVisible();
        }

        public override bool OnMouseUp(Float2 location, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                _draggingSelection = false;
                if (_selectionLine == _caretLine && _selectionColumn == _caretColumn)
                    ClearSelection();
                return true;
            }

            return base.OnMouseUp(location, button);
        }

        public override void Draw()
        {
            base.Draw();
            UpdateMetrics();

            var settings = WsCodeEditorSettings.Current;
            var gutterWidth = GetGutterWidth();
            Render2D.FillRectangle(new Rectangle(0, 0, Width, Height), settings.EditorBackground);
            Render2D.FillRectangle(new Rectangle(0, 0, gutterWidth, Height), settings.GutterBackground);
            Render2D.DrawLine(new Float2(gutterWidth, 0), new Float2(gutterWidth, Height), new Color(0.18f, 0.2f, 0.23f, 1f));

            var font = GetEditorFont();
            var visible = GetVisibleLineCount() + 1;
            var first = Mathf.Clamp(_scrollLine, 0, Math.Max(0, _lines.Count - 1));
            var last = Math.Min(_lines.Count - 1, first + visible);

            for (var line = first; line <= last; line++)
            {
                var y = (line - first) * _lineHeight;
                if (settings.HighlightCurrentLine && line == _caretLine)
                    Render2D.FillRectangle(new Rectangle(gutterWidth, y, Width - gutterWidth, _lineHeight), settings.CurrentLineBackground);

                if (settings.ShowLineNumbers)
                {
                    var lineNumber = (line + 1).ToString();
                    Render2D.DrawText(font, lineNumber, new Rectangle(0, y, gutterWidth - 8f, _lineHeight), new Color(0.48f, 0.52f, 0.58f, 1f), TextAlignment.Far, TextAlignment.Center);
                }
            }

            Render2D.PushClip(new Rectangle(gutterWidth, 0, Math.Max(0, Width - gutterWidth), Height));
            for (var line = first; line <= last; line++)
            {
                var y = (line - first) * _lineHeight;
                DrawSelectionForLine(line, y);
                DrawLineText(line, y);
                DrawDiagnosticsForLine(line, y);
            }

            if (IsFocused && ((_blinkTime % 1.0f) < 0.55f))
            {
                var caretX = GetTextX(_lines[_caretLine], _caretColumn);
                var caretY = (_caretLine - first) * _lineHeight + 2f;
                Render2D.FillRectangle(new Rectangle(caretX, caretY, 1.5f, _lineHeight - 4f), new Color(0.9f, 0.95f, 1f, 1f));
            }
            Render2D.PopClip();

            Render2D.DrawRectangle(new Rectangle(0, 0, Width, Height), IsFocused ? Style.Current.BorderSelected : Style.Current.BorderNormal, 1f);
        }

        private void DrawLineText(int lineIndex, float y)
        {
            if (lineIndex < 0 || lineIndex >= _lines.Count)
                return;

            var line = _lines[lineIndex];
            if (line.Length == 0)
                return;

            var spans = lineIndex < _syntax.Lines.Length ? _syntax.Lines[lineIndex] : null;
            var cursor = 0;
            if (spans != null)
            {
                foreach (var span in spans)
                {
                    if (span.Start > cursor)
                        DrawTextSegment(line, cursor, span.Start - cursor, CodeTokenKind.Plain, y);
                    DrawTextSegment(line, span.Start, span.Length, span.Kind, y);
                    cursor = Math.Max(cursor, span.Start + span.Length);
                }
            }

            if (cursor < line.Length)
                DrawTextSegment(line, cursor, line.Length - cursor, CodeTokenKind.Plain, y);
        }

        private void DrawTextSegment(string line, int start, int length, CodeTokenKind kind, float y)
        {
            if (length <= 0)
                return;

            var visibleStart = Math.Max(start, _scrollColumn);
            var visibleEnd = Math.Min(start + length, _scrollColumn + GetVisibleColumnCount() + 4);
            if (visibleEnd <= visibleStart)
                return;

            var text = line.Substring(visibleStart, visibleEnd - visibleStart).Replace("\t", new string(' ', GetTabSize()));
            var x = GetTextX(line, visibleStart);
            Render2D.DrawText(GetEditorFont(), text, GetColor(kind), new Float2(x, y + 2f), null);
        }

        private void DrawSelectionForLine(int line, float y)
        {
            if (!HasSelection())
                return;

            GetSelection(out var startLine, out var startColumn, out var endLine, out var endColumn);
            if (line < startLine || line > endLine)
                return;

            var from = line == startLine ? startColumn : 0;
            var to = line == endLine ? endColumn : _lines[line].Length;
            if (from == to && line != endLine)
                to++;

            from = Math.Max(from, _scrollColumn);
            to = Math.Max(from, to);
            var x = GetTextX(_lines[line], from);
            var width = Math.Max(_charWidth, GetTextWidth(_lines[line], from, to));
            Render2D.FillRectangle(new Rectangle(x, y + 1f, width, _lineHeight - 2f), new Color(0.23f, 0.43f, 0.72f, 0.65f));
        }

        private void DrawDiagnosticsForLine(int line, float y)
        {
            if (!WsCodeEditorSettings.Current.ShowDiagnostics || _syntax.Diagnostics == null || _syntax.Diagnostics.Length == 0)
                return;

            for (var i = 0; i < _syntax.Diagnostics.Length; i++)
            {
                var diagnostic = _syntax.Diagnostics[i];
                if (diagnostic.Line != line)
                    continue;

                var gutterWidth = GetGutterWidth();
                var x = GetTextX(_lines[line], diagnostic.Column);
                if (x < gutterWidth)
                    x = gutterWidth + 2f;

                Render2D.DrawLine(new Float2(x, y + _lineHeight - 3f), new Float2(x + _charWidth * 4f, y + _lineHeight - 3f), new Color(0.95f, 0.35f, 0.35f, 1f), 1.5f);
                Render2D.FillRectangle(new Rectangle(4f, y + 5f, 5f, 5f), new Color(0.95f, 0.35f, 0.35f, 1f));
            }
        }

        private Color GetColor(CodeTokenKind kind)
        {
            return kind switch
            {
                CodeTokenKind.Keyword => new Color(0.34f, 0.62f, 0.9f, 1f),
                CodeTokenKind.Comment => new Color(0.43f, 0.62f, 0.36f, 1f),
                CodeTokenKind.String => new Color(0.86f, 0.56f, 0.42f, 1f),
                CodeTokenKind.Number => new Color(0.72f, 0.82f, 0.57f, 1f),
                CodeTokenKind.Preprocessor => new Color(0.77f, 0.52f, 0.88f, 1f),
                CodeTokenKind.Type => new Color(0.36f, 0.82f, 0.76f, 1f),
                CodeTokenKind.Member => new Color(0.86f, 0.86f, 0.55f, 1f),
                CodeTokenKind.Error => new Color(0.95f, 0.35f, 0.35f, 1f),
                _ => new Color(0.82f, 0.85f, 0.9f, 1f),
            };
        }

        private void InsertCharacter(char c)
        {
            var pair = WsCodeEditorSettings.Current.AutoPairBrackets ? GetPair(c) : '\0';
            if (pair != '\0')
            {
                ReplaceSelectionOrInsert(c.ToString() + pair);
                MoveHorizontal(-1, false);
            }
            else
            {
                ReplaceSelectionOrInsert(c.ToString());
            }
        }

        private static char GetPair(char c)
        {
            return c switch
            {
                '(' => ')',
                '[' => ']',
                '{' => '}',
                '"' => '"',
                '\'' => '\'',
                _ => '\0',
            };
        }

        private void InsertNewLine()
        {
            PushUndo();
            DeleteSelectionIfAny(false);

            var line = _lines[_caretLine];
            var before = line.Substring(0, _caretColumn);
            var after = line.Substring(_caretColumn);
            var indent = GetIndent(before);
            if (before.TrimEnd().EndsWith("{", StringComparison.Ordinal))
                indent += new string(' ', GetTabSize());

            _lines[_caretLine] = before;
            _lines.Insert(_caretLine + 1, indent + after);
            _caretLine++;
            _caretColumn = indent.Length;
            AfterEdit();
        }

        private void ReplaceSelectionOrInsert(string text)
        {
            PushUndo();
            DeleteSelectionIfAny(false);
            InsertTextAtCaret(text);
            AfterEdit();
        }

        private void InsertTextAtCaret(string text)
        {
            text = NormalizeNewLines(text);
            var parts = text.Split('\n');
            var current = _lines[_caretLine];
            var before = current.Substring(0, _caretColumn);
            var after = current.Substring(_caretColumn);

            if (parts.Length == 1)
            {
                _lines[_caretLine] = before + parts[0] + after;
                _caretColumn += parts[0].Length;
                return;
            }

            _lines[_caretLine] = before + parts[0];
            for (var i = 1; i < parts.Length; i++)
                _lines.Insert(_caretLine + i, parts[i]);

            _caretLine += parts.Length - 1;
            _caretColumn = parts[parts.Length - 1].Length;
            _lines[_caretLine] += after;
        }

        private void Backspace()
        {
            if (DeleteSelectionIfAny(true))
                return;

            if (_caretColumn == 0 && _caretLine == 0)
                return;

            PushUndo();
            if (_caretColumn > 0)
            {
                var line = _lines[_caretLine];
                _lines[_caretLine] = line.Remove(_caretColumn - 1, 1);
                _caretColumn--;
            }
            else
            {
                var previousLength = _lines[_caretLine - 1].Length;
                _lines[_caretLine - 1] += _lines[_caretLine];
                _lines.RemoveAt(_caretLine);
                _caretLine--;
                _caretColumn = previousLength;
            }

            AfterEdit();
        }

        private void Delete()
        {
            if (DeleteSelectionIfAny(true))
                return;

            if (_caretColumn >= _lines[_caretLine].Length && _caretLine >= _lines.Count - 1)
                return;

            PushUndo();
            if (_caretColumn < _lines[_caretLine].Length)
            {
                _lines[_caretLine] = _lines[_caretLine].Remove(_caretColumn, 1);
            }
            else
            {
                _lines[_caretLine] += _lines[_caretLine + 1];
                _lines.RemoveAt(_caretLine + 1);
            }

            AfterEdit();
        }

        private bool DeleteSelectionIfAny(bool pushUndo)
        {
            if (!HasSelection())
                return false;

            if (pushUndo)
                PushUndo();

            GetSelection(out var startLine, out var startColumn, out var endLine, out var endColumn);
            if (startLine == endLine)
            {
                _lines[startLine] = _lines[startLine].Remove(startColumn, endColumn - startColumn);
            }
            else
            {
                var before = _lines[startLine].Substring(0, startColumn);
                var after = _lines[endLine].Substring(endColumn);
                _lines[startLine] = before + after;
                _lines.RemoveRange(startLine + 1, endLine - startLine);
            }

            _caretLine = startLine;
            _caretColumn = startColumn;
            ClearSelection();
            AfterEdit();
            return true;
        }

        private void IndentSelection()
        {
            PushUndo();
            if (HasSelection())
            {
                GetSelection(out var startLine, out _, out var endLine, out var endColumn);
                if (endColumn == 0 && endLine > startLine)
                    endLine--;
                for (var i = startLine; i <= endLine; i++)
                    _lines[i] = new string(' ', GetTabSize()) + _lines[i];
                _caretColumn += GetTabSize();
            }
            else
            {
                InsertTextAtCaret(new string(' ', GetTabSize()));
            }

            AfterEdit();
        }

        private void OutdentSelection()
        {
            PushUndo();
            var startLine = _caretLine;
            var endLine = _caretLine;
            if (HasSelection())
                GetSelection(out startLine, out _, out endLine, out _);

            for (var line = startLine; line <= endLine; line++)
            {
                var remove = 0;
                while (remove < GetTabSize() && remove < _lines[line].Length && _lines[line][remove] == ' ')
                    remove++;
                if (remove > 0)
                {
                    _lines[line] = _lines[line].Remove(0, remove);
                    if (line == _caretLine)
                        _caretColumn = Math.Max(0, _caretColumn - remove);
                }
            }

            AfterEdit();
        }

        private void CopySelection()
        {
            if (HasSelection())
                Clipboard.Text = GetSelectedText();
        }

        private void CutSelection()
        {
            if (!HasSelection())
                return;

            Clipboard.Text = GetSelectedText();
            DeleteSelectionIfAny(true);
        }

        private void PasteClipboard()
        {
            var text = Clipboard.Text;
            if (!string.IsNullOrEmpty(text))
                ReplaceSelectionOrInsert(text);
        }

        private void Undo()
        {
            if (_undo.Count == 0)
                return;

            _redo.Push(GetText());
            SetTextInternal(_undo.Pop());
            ClampCaret();
            AfterTextRestored();
        }

        private void Redo()
        {
            if (_redo.Count == 0)
                return;

            _undo.Push(GetText());
            SetTextInternal(_redo.Pop());
            ClampCaret();
            AfterTextRestored();
        }

        private void SelectAll()
        {
            _selectionLine = 0;
            _selectionColumn = 0;
            _caretLine = _lines.Count - 1;
            _caretColumn = _lines[_caretLine].Length;
            EnsureCaretVisible();
        }

        private void MoveHorizontal(int delta, bool selecting)
        {
            BeginSelectionIfNeeded(selecting);
            if (delta < 0)
            {
                if (!selecting && HasSelection())
                {
                    GetSelection(out _caretLine, out _caretColumn, out _, out _);
                    ClearSelection();
                }
                else if (_caretColumn > 0)
                    _caretColumn--;
                else if (_caretLine > 0)
                {
                    _caretLine--;
                    _caretColumn = _lines[_caretLine].Length;
                }
            }
            else
            {
                if (!selecting && HasSelection())
                {
                    GetSelection(out _, out _, out _caretLine, out _caretColumn);
                    ClearSelection();
                }
                else if (_caretColumn < _lines[_caretLine].Length)
                    _caretColumn++;
                else if (_caretLine < _lines.Count - 1)
                {
                    _caretLine++;
                    _caretColumn = 0;
                }
            }

            EndSelectionIfNeeded(selecting);
            _preferredColumn = -1;
            EnsureCaretVisible();
        }

        private void MoveVertical(int delta, bool selecting)
        {
            BeginSelectionIfNeeded(selecting);
            if (_preferredColumn < 0)
                _preferredColumn = _caretColumn;
            _caretLine = Mathf.Clamp(_caretLine + delta, 0, _lines.Count - 1);
            _caretColumn = Mathf.Clamp(_preferredColumn, 0, _lines[_caretLine].Length);
            EndSelectionIfNeeded(selecting);
            EnsureCaretVisible();
        }

        private void MoveHome(bool selecting)
        {
            BeginSelectionIfNeeded(selecting);
            var line = _lines[_caretLine];
            var firstNonWhitespace = 0;
            while (firstNonWhitespace < line.Length && char.IsWhiteSpace(line[firstNonWhitespace]))
                firstNonWhitespace++;
            _caretColumn = _caretColumn == firstNonWhitespace ? 0 : firstNonWhitespace;
            EndSelectionIfNeeded(selecting);
            EnsureCaretVisible();
        }

        private void MoveEnd(bool selecting)
        {
            BeginSelectionIfNeeded(selecting);
            _caretColumn = _lines[_caretLine].Length;
            EndSelectionIfNeeded(selecting);
            EnsureCaretVisible();
        }

        private void BeginSelectionIfNeeded(bool selecting)
        {
            if (selecting && !HasSelection())
            {
                _selectionLine = _caretLine;
                _selectionColumn = _caretColumn;
            }
        }

        private void EndSelectionIfNeeded(bool selecting)
        {
            if (!selecting)
                ClearSelection();
            else if (_selectionLine == _caretLine && _selectionColumn == _caretColumn)
                ClearSelection();
        }

        private void SelectWord(int lineIndex, int column)
        {
            var line = _lines[lineIndex];
            if (line.Length == 0)
                return;

            column = Mathf.Clamp(column, 0, Math.Max(0, line.Length - 1));
            if (!IsWordChar(line[column]) && column > 0)
                column--;

            var start = column;
            var end = column;
            while (start > 0 && IsWordChar(line[start - 1]))
                start--;
            while (end < line.Length && IsWordChar(line[end]))
                end++;

            _selectionLine = lineIndex;
            _selectionColumn = start;
            _caretLine = lineIndex;
            _caretColumn = end;
        }

        private Position ScreenToTextPosition(Float2 location)
        {
            var line = Mathf.Clamp(_scrollLine + (int)Math.Floor(location.Y / _lineHeight), 0, _lines.Count - 1);
            var column = GetColumnFromX(_lines[line], location.X);
            return new Position(line, column);
        }

        private void PushUndo()
        {
            _undo.Push(GetText());
            _redo.Clear();
            while (_undo.Count > MaxUndoSnapshots)
            {
                var copy = _undo.ToArray();
                _undo.Clear();
                for (var i = copy.Length - 2; i >= 0; i--)
                    _undo.Push(copy[i]);
            }
        }

        private void AfterEdit()
        {
            ClampCaret();
            EnsureCaretVisible();
            AnalyzeNow();
            IsDirty = !string.Equals(NormalizeNewLines(GetText()), _loadedText, StringComparison.Ordinal);
        }

        private void AfterTextRestored()
        {
            ClearSelection();
            EnsureCaretVisible();
            AnalyzeNow();
            IsDirty = !string.Equals(NormalizeNewLines(GetText()), _loadedText, StringComparison.Ordinal);
        }

        private void AnalyzeNow()
        {
            _syntax = CSharpSyntaxService.Analyze(GetText());
            DiagnosticsChanged?.Invoke(_syntax.Diagnostics?.Length ?? 0);
        }

        private void SetTextInternal(string text)
        {
            _lines.Clear();
            var normalized = NormalizeNewLines(text);
            _lines.AddRange(normalized.Split('\n'));
            if (_lines.Count == 0)
                _lines.Add(string.Empty);
        }

        private string GetSelectedText()
        {
            if (!HasSelection())
                return string.Empty;

            GetSelection(out var startLine, out var startColumn, out var endLine, out var endColumn);
            if (startLine == endLine)
                return _lines[startLine].Substring(startColumn, endColumn - startColumn);

            var builder = new StringBuilder();
            builder.AppendLine(_lines[startLine].Substring(startColumn));
            for (var line = startLine + 1; line < endLine; line++)
                builder.AppendLine(_lines[line]);
            builder.Append(_lines[endLine].Substring(0, endColumn));
            return builder.ToString();
        }

        private bool HasSelection()
        {
            return _selectionLine >= 0 && (_selectionLine != _caretLine || _selectionColumn != _caretColumn);
        }

        private void ClearSelection()
        {
            _selectionLine = -1;
            _selectionColumn = -1;
        }

        private void GetSelection(out int startLine, out int startColumn, out int endLine, out int endColumn)
        {
            if (_selectionLine < _caretLine || (_selectionLine == _caretLine && _selectionColumn <= _caretColumn))
            {
                startLine = _selectionLine;
                startColumn = _selectionColumn;
                endLine = _caretLine;
                endColumn = _caretColumn;
            }
            else
            {
                startLine = _caretLine;
                startColumn = _caretColumn;
                endLine = _selectionLine;
                endColumn = _selectionColumn;
            }
        }

        private void EnsureCaretVisible()
        {
            var visibleLines = GetVisibleLineCount();
            if (_caretLine < _scrollLine)
                _scrollLine = _caretLine;
            else if (_caretLine >= _scrollLine + visibleLines)
                _scrollLine = Math.Max(0, _caretLine - visibleLines + 1);

            var visibleColumns = GetVisibleColumnCount();
            if (_caretColumn < _scrollColumn)
                _scrollColumn = _caretColumn;
            else if (_caretColumn >= _scrollColumn + visibleColumns)
                _scrollColumn = Math.Max(0, _caretColumn - visibleColumns + 1);
        }

        private int GetVisibleLineCount()
        {
            return Math.Max(1, (int)Math.Floor(Height / Math.Max(1f, _lineHeight)));
        }

        private int GetVisibleColumnCount()
        {
            return Math.Max(1, (int)Math.Floor((Width - GetGutterWidth() - TextPadding) / Math.Max(1f, _charWidth)));
        }

        private void ClampCaret()
        {
            _caretLine = Mathf.Clamp(_caretLine, 0, Math.Max(0, _lines.Count - 1));
            _caretColumn = Mathf.Clamp(_caretColumn, 0, _lines[_caretLine].Length);
        }

        private void UpdateMetrics()
        {
            var font = GetEditorFont();
            var settings = WsCodeEditorSettings.Current;
            var cell = font.MeasureText("M");
            _charWidth = Math.Max(6f, cell.X * Mathf.Clamp(settings.CharacterWidthScale, 0.75f, 1.5f));
            _lineHeight = Math.Max(16f, cell.Y + Mathf.Clamp(settings.LinePadding, 0f, 14f));
        }

        private static Font GetEditorFont()
        {
            var settings = WsCodeEditorSettings.Current;
            switch (settings.FontFace)
            {
                case WsCodeEditorFontFace.EditorSmall:
                    return Style.Current.FontSmall;
                default:
                    return Style.Current.FontMedium;
            }
        }

        private static string NormalizeNewLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string GetIndent(string text)
        {
            var count = 0;
            while (count < text.Length && (text[count] == ' ' || text[count] == '\t'))
                count++;
            return text.Substring(0, count).Replace("\t", new string(' ', GetTabSize()));
        }

        private float GetTextX(string line, int column)
        {
            return GetGutterWidth() + TextPadding + GetTextWidth(line, _scrollColumn, column);
        }

        private static float GetTextWidth(string line, int startColumn, int endColumn)
        {
            if (string.IsNullOrEmpty(line) || endColumn <= startColumn)
                return 0f;

            startColumn = Mathf.Clamp(startColumn, 0, line.Length);
            endColumn = Mathf.Clamp(endColumn, startColumn, line.Length);
            if (endColumn <= startColumn)
                return 0f;

            var text = line.Substring(startColumn, endColumn - startColumn).Replace("\t", new string(' ', GetTabSize()));
            return GetEditorFont().MeasureText(text).X;
        }

        private int GetColumnFromX(string line, float x)
        {
            var localX = Math.Max(0f, x - GetGutterWidth() - TextPadding);
            var bestColumn = Mathf.Clamp(_scrollColumn, 0, line.Length);
            var bestDistance = Math.Abs(localX);

            for (var column = bestColumn; column <= line.Length; column++)
            {
                var width = GetTextWidth(line, _scrollColumn, column);
                var distance = Math.Abs(localX - width);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestColumn = column;
                }

                if (width > localX && distance > bestDistance)
                    break;
            }

            return bestColumn;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static int GetTabSize()
        {
            return Mathf.Clamp(WsCodeEditorSettings.Current.TabSize, 2, 8);
        }

        private static float GetGutterWidth()
        {
            return Mathf.Clamp(WsCodeEditorSettings.Current.GutterWidth, 44f, 96f);
        }

        private readonly struct Position
        {
            public readonly int Line;
            public readonly int Column;

            public Position(int line, int column)
            {
                Line = line;
                Column = column;
            }
        }
    }
}
#endif
