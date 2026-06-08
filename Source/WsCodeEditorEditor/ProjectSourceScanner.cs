#if FLAX_EDITOR
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.IO;
using FlaxEngine;

namespace WsCodeEditorEditor
{
    /// <summary>
    /// Finds editable project C# files. Plugin, cache, generated, and build folders are intentionally excluded.
    /// </summary>
    public static class ProjectSourceScanner
    {
        private static readonly HashSet<string> ExcludedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".vs",
            "bin",
            "obj",
            "Cache",
            "Binaries",
            "Intermediate",
            "Generated",
        };

        public static string ProjectSourceRoot => Path.GetFullPath(Path.Combine(Globals.ProjectFolder, "Source"));

        public static bool IsProjectSourceFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!IsUnderDirectory(fullPath, ProjectSourceRoot))
                    return false;

                var relative = Path.GetRelativePath(ProjectSourceRoot, fullPath);
                foreach (var part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    if (ExcludedFolders.Contains(part) || part.Equals("Plugins", StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        public static IEnumerable<string> GetProjectCSharpFiles(string filter = null)
        {
            var root = ProjectSourceRoot;
            if (!Directory.Exists(root))
                yield break;

            foreach (var file in EnumerateFiles(root))
            {
                var relative = GetProjectRelativePath(file);
                if (!string.IsNullOrWhiteSpace(filter) && relative.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                yield return file;
            }
        }

        public static string GetProjectRelativePath(string path)
        {
            try
            {
                return Path.GetRelativePath(Globals.ProjectFolder, Path.GetFullPath(path)).Replace('\\', '/');
            }
            catch
            {
                return path ?? string.Empty;
            }
        }

        private static IEnumerable<string> EnumerateFiles(string directory)
        {
            IEnumerable<string> children;
            try
            {
                children = Directory.GetDirectories(directory);
            }
            catch
            {
                yield break;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (ExcludedFolders.Contains(name) || name.Equals("Plugins", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var nested in EnumerateFiles(child))
                    yield return nested;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.GetFiles(directory, "*.cs");
            }
            catch
            {
                yield break;
            }

            foreach (var file in files)
            {
                if (IsProjectSourceFile(file))
                    yield return Path.GetFullPath(file);
            }
        }

        private static bool IsUnderDirectory(string path, string directory)
        {
            var normalizedPath = EnsureTrailingSeparator(Path.GetFullPath(path));
            var normalizedDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
            return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()) || path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                return path;

            return path + Path.DirectorySeparatorChar;
        }
    }
}
#endif
