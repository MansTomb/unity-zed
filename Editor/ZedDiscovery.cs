using Unity.CodeEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using NiceIO;

namespace UnityZed
{
    public class ZedDiscovery
    {
        private static readonly string[] sExecutableNames = { "zed.exe", "Zed.exe", "zeditor.exe", "zed", "zeditor" };

        public CodeEditor.Installation[] GetInstallations()
        {
            var results = new List<CodeEditor.Installation>();
            var seenPaths = new HashSet<string>(PathComparer);

            foreach (var candidatePath in GetCandidatePaths())
            {
                if (!candidatePath.FileExists())
                    continue;

                var path = candidatePath.MakeAbsolute().ToString(SlashMode.Native);
                if (!seenPaths.Add(path))
                    continue;

                results.Add(CreateInstallation(path));
            }

            return results.ToArray();
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            if (IsZedExecutable(editorPath))
            {
                installation = CreateInstallation(new NPath(editorPath).MakeAbsolute().ToString(SlashMode.Native));
                return true;
            }

            installation = default;
            return false;
        }

        private static CodeEditor.Installation CreateInstallation(string path)
        {
            var name = new StringBuilder("Zed");
            if (TryGetVersion(new NPath(path), out var version))
                name.Append($" [{version}]");

            return new()
            {
                Name = name.ToString(),
                Path = path,
            };
        }

        private static IEnumerable<NPath> GetCandidatePaths()
        {
#if UNITY_EDITOR_OSX
            yield return new NPath("/Applications/Zed.app/Contents/MacOS/cli");
            yield return new NPath("/usr/local/bin/zed");
#endif

#if UNITY_EDITOR_LINUX
            yield return new NPath("/var/lib/flatpak/app/dev.zed.Zed/current/active/files/bin/zed");
            yield return new NPath("/usr/bin/zeditor");
            yield return new NPath("/run/current-system/sw/bin/zeditor");
            yield return new NPath("/etc/profiles/per-user/linx/bin/zed");
            yield return new NPath("/etc/profiles/per-user/linx/bin/zeditor");
            yield return NPath.HomeDirectory.Combine(".local/bin/zed");
#endif

#if UNITY_EDITOR_WIN
            foreach (var path in GetWindowsCandidatePaths())
                yield return path;
#endif
        }

#if UNITY_EDITOR_WIN
        private static IEnumerable<NPath> GetWindowsCandidatePaths()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return new NPath(Path.Combine(localAppData, "Programs", "Zed", "zed.exe"));
                yield return new NPath(Path.Combine(localAppData, "Programs", "Zed", "Zed.exe"));
            }

            foreach (var path in GetExecutablesFromPath())
                yield return path;
        }

        private static IEnumerable<NPath> GetExecutablesFromPath()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathVariable))
                yield break;

            foreach (var directory in pathVariable.Split(Path.PathSeparator).Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                yield return new NPath(Path.Combine(directory.Trim(), "zed.exe"));
                yield return new NPath(Path.Combine(directory.Trim(), "Zed.exe"));
                yield return new NPath(Path.Combine(directory.Trim(), "zeditor.exe"));
            }
        }
#endif

        private static bool IsZedExecutable(string editorPath)
        {
            if (string.IsNullOrWhiteSpace(editorPath))
                return false;

            var path = new NPath(editorPath);
            if (!path.FileExists())
                return false;

            var fileName = Path.GetFileName(editorPath);
            return sExecutableNames.Any(name => string.Equals(name, fileName, PathComparison));
        }

        private static bool TryGetVersion(NPath path, out string version)
        {
#if UNITY_EDITOR_OSX
            if (TryGetVersionFromPlist(path, out version))
                return true;
#endif

#if UNITY_EDITOR_WIN
            if (TryGetVersionFromFileVersionInfo(path, out version))
                return true;
#endif

            version = null;
            return false;
        }

#if UNITY_EDITOR_WIN
        private static bool TryGetVersionFromFileVersionInfo(NPath path, out string version)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path.ToString(SlashMode.Native));
            version = versionInfo.ProductVersion;
            return !string.IsNullOrWhiteSpace(version);
        }
#endif

#if UNITY_EDITOR_OSX
        private static bool TryGetVersionFromPlist(NPath path, out string version)
        {
            version = null;

            var plistPath = path.Combine("../../").Combine("Info.plist");
            if (plistPath.FileExists() == false)
                return false;

            var xPath = new XPathDocument(plistPath.ToString());
            var xNavigator = xPath.CreateNavigator().SelectSingleNode("/plist/dict/key[text()='CFBundleShortVersionString']/following-sibling::string[1]/text()");
            if (xNavigator == null)
                return false;

            version = xNavigator.Value;
            return true;
        }
#endif

        private static StringComparer PathComparer
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
                return StringComparer.OrdinalIgnoreCase;
#else
                return StringComparer.Ordinal;
#endif
            }
        }

        private static StringComparison PathComparison
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
                return StringComparison.OrdinalIgnoreCase;
#else
                return StringComparison.Ordinal;
#endif
            }
        }
    }
}
