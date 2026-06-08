#if FLAX_EDITOR
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Linq;

namespace WsCodeEditorEditor
{
    public enum CodeTokenKind
    {
        Plain,
        Keyword,
        Comment,
        String,
        Number,
        Preprocessor,
        Type,
        Member,
        Error,
    }

    public readonly struct SyntaxSpan
    {
        public readonly int Start;
        public readonly int Length;
        public readonly CodeTokenKind Kind;

        public SyntaxSpan(int start, int length, CodeTokenKind kind)
        {
            Start = start;
            Length = length;
            Kind = kind;
        }
    }

    public readonly struct CodeDiagnostic
    {
        public readonly int Line;
        public readonly int Column;
        public readonly string Message;

        public CodeDiagnostic(int line, int column, string message)
        {
            Line = line;
            Column = column;
            Message = message ?? string.Empty;
        }
    }

    public sealed class SyntaxAnalysisResult
    {
        public static readonly SyntaxAnalysisResult Empty = new SyntaxAnalysisResult(Array.Empty<List<SyntaxSpan>>(), Array.Empty<CodeDiagnostic>(), false);

        public readonly List<SyntaxSpan>[] Lines;
        public readonly CodeDiagnostic[] Diagnostics;
        public readonly bool UsedRoslyn;

        public SyntaxAnalysisResult(List<SyntaxSpan>[] lines, CodeDiagnostic[] diagnostics, bool usedRoslyn)
        {
            Lines = lines ?? Array.Empty<List<SyntaxSpan>>();
            Diagnostics = diagnostics ?? Array.Empty<CodeDiagnostic>();
            UsedRoslyn = usedRoslyn;
        }
    }

    public static class CSharpSyntaxService
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while", "record", "required", "file", "scoped", "var", "async",
            "await", "yield", "partial", "where", "select", "group", "into", "orderby", "let", "join",
            "on", "equals", "by", "descending", "when", "init", "with", "global",
        };

        public static SyntaxAnalysisResult Analyze(string text)
        {
            text ??= string.Empty;
            return AnalyzeLexically(text, Array.Empty<CodeDiagnostic>());
        }

        public static string[] GetCompletionWords()
        {
            return Keywords.Concat(new[]
            {
                "Actor", "Script", "Scene", "Transform", "Vector2", "Vector3", "Quaternion", "Color",
                "Debug", "Input", "Time", "Mathf", "Prefab", "Content", "Camera", "RigidBody",
            }).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static SyntaxAnalysisResult AnalyzeLexically(string text, CodeDiagnostic[] diagnostics)
        {
            var lines = SplitLines(text);
            var spans = new List<SyntaxSpan>[lines.Length];
            var inBlockComment = false;

            for (var i = 0; i < lines.Length; i++)
            {
                spans[i] = LexLine(lines[i], ref inBlockComment);
            }

            return new SyntaxAnalysisResult(spans, diagnostics, false);
        }

        private static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static List<SyntaxSpan> LexLine(string line, ref bool inBlockComment)
        {
            var spans = new List<SyntaxSpan>();
            var i = 0;

            if (!inBlockComment)
            {
                var first = 0;
                while (first < line.Length && char.IsWhiteSpace(line[first]))
                    first++;
                if (first < line.Length && line[first] == '#')
                {
                    spans.Add(new SyntaxSpan(first, line.Length - first, CodeTokenKind.Preprocessor));
                    return spans;
                }
            }

            while (i < line.Length)
            {
                var start = i;

                if (inBlockComment)
                {
                    var end = line.IndexOf("*/", i, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        spans.Add(new SyntaxSpan(i, line.Length - i, CodeTokenKind.Comment));
                        return spans;
                    }

                    spans.Add(new SyntaxSpan(i, end + 2 - i, CodeTokenKind.Comment));
                    i = end + 2;
                    inBlockComment = false;
                    continue;
                }

                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                {
                    spans.Add(new SyntaxSpan(i, line.Length - i, CodeTokenKind.Comment));
                    break;
                }

                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
                {
                    var end = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        spans.Add(new SyntaxSpan(i, line.Length - i, CodeTokenKind.Comment));
                        inBlockComment = true;
                        break;
                    }

                    spans.Add(new SyntaxSpan(i, end + 2 - i, CodeTokenKind.Comment));
                    i = end + 2;
                    continue;
                }

                if (line[i] == '"' || line[i] == '\'')
                {
                    i = ConsumeQuoted(line, i, line[i]);
                    spans.Add(new SyntaxSpan(start, i - start, CodeTokenKind.String));
                    continue;
                }

                if ((line[i] == '@' || line[i] == '$') && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '@' || line[i + 1] == '$'))
                {
                    i = ConsumePrefixedString(line, i);
                    spans.Add(new SyntaxSpan(start, i - start, CodeTokenKind.String));
                    continue;
                }

                if (char.IsDigit(line[i]))
                {
                    i++;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '.' || line[i] == '_' || line[i] == 'x' || line[i] == 'X'))
                        i++;
                    spans.Add(new SyntaxSpan(start, i - start, CodeTokenKind.Number));
                    continue;
                }

                if (IsIdentifierStart(line[i]))
                {
                    i++;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;

                    var value = line.Substring(start, i - start);
                    if (Keywords.Contains(value))
                        spans.Add(new SyntaxSpan(start, i - start, CodeTokenKind.Keyword));
                    else if (char.IsUpper(value[0]))
                        spans.Add(new SyntaxSpan(start, i - start, CodeTokenKind.Type));
                    continue;
                }

                i++;
            }

            return spans;
        }

        private static int ConsumeQuoted(string line, int index, char quote)
        {
            index++;
            while (index < line.Length)
            {
                if (line[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (line[index] == quote)
                    return index + 1;

                index++;
            }

            return line.Length;
        }

        private static int ConsumePrefixedString(string line, int index)
        {
            while (index < line.Length && line[index] != '"')
                index++;

            if (index >= line.Length)
                return line.Length;

            index++;
            while (index < line.Length)
            {
                if (line[index] == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                if (line[index] == '"')
                    return index + 1;

                index++;
            }

            return line.Length;
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }
    }
}
#endif
