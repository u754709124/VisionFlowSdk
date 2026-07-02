using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Flow.Tests
{
    internal static class SourceTextEncodingTests
    {
        public static Task TextFilesDoNotContainCorruptedChineseMarkers()
        {
            var root = FindRepositoryRoot();
            var invalidFiles = new List<string>();
            var utf8 = new UTF8Encoding(false, true);
            var replacement = char.ConvertFromUtf32(0xFFFD);

            foreach (var path in EnumerateTextFiles(root))
            {
                string text;
                try
                {
                    text = utf8.GetString(File.ReadAllBytes(path));
                }
                catch (DecoderFallbackException)
                {
                    invalidFiles.Add(RelativePath(root, path) + " is not strict UTF-8");
                    continue;
                }

                if (text.Contains(replacement))
                {
                    invalidFiles.Add(RelativePath(root, path) + " contains U+FFFD");
                }
            }

            AssertEx.True(
                invalidFiles.Count == 0,
                "Source text contains corrupted encoding markers: " + string.Join("; ", invalidFiles.Take(12)));

            return Task.FromResult(0);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "VisionFlowSdk.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate VisionFlowSdk.sln from test output directory.");
        }

        private static IEnumerable<string> EnumerateTextFiles(string root)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs",
                ".md",
                ".ps1",
                ".xaml",
                ".flowdesign",
                ".flowruntime"
            };

            return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => extensions.Contains(Path.GetExtension(path)))
                .Where(path => !IsGeneratedOrGitPath(root, path));
        }

        private static bool IsGeneratedOrGitPath(string root, string path)
        {
            var relative = RelativePath(root, path);
            return relative.StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("artifacts" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relative.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
                || relative.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string RelativePath(string root, string path)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return path;
        }
    }
}
