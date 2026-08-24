using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DarkFlare.Tests
{
    public sealed class InfrastructurePolicyTests
    {
        const string RuntimeRoot = "Assets/Scripts/Runtime";
        const string TestsRoot = "Assets/Scripts/Tests";
        const string ManifestPath = "Assets/Scripts/Tests/EditMode/InfrastructurePolicyExceptions.json";

        static readonly PolicyRule[] Rules =
        {
            new PolicyRule(
                "game-architecture-interface",
                @"\bGameArchitecture\s*\.\s*Interface\b",
                new[] { RuntimeRoot, TestsRoot },
                new[] { "Assets/Scripts/Runtime/Infrastructure/Lifecycle/GameArchitectureProvider.cs" }),
            new PolicyRule(
                "runtime-initialize-hook",
                @"\bRuntimeInitializeOnLoadMethod(?:Attribute)?\b",
                new[] { RuntimeRoot },
                new[] { "Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationBootstrap.cs" }),
            new PolicyRule(
                "dont-destroy-on-load",
                @"\bDontDestroyOnLoad\s*\(",
                new[] { RuntimeRoot },
                new[] { "Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs" }),
            new PolicyRule(
                "player-prefs",
                @"\bPlayerPrefs\s*\.",
                new[] { RuntimeRoot },
                Array.Empty<string>()),
            new PolicyRule(
                "business-file-io",
                @"\b(?:(?:(?:System\s*\.\s*)?IO\s*\.\s*)?(?:File|Directory)\s*\."
                    + @"|new\s+(?:(?:System\s*\.\s*)?IO\s*\.\s*)?(?:FileStream|FileInfo)\s*\()",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Persistence/LocalSaveStorage.cs",
                }),
            new PolicyRule(
                "persistent-data-path",
                @"\bApplication\s*\.\s*persistentDataPath\b",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Persistence/SavePathProvider.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Settings/SettingsPathProvider.cs",
                }),
            new PolicyRule(
                "business-scene-loading",
                @"\bSceneManager\s*\.\s*(?:Load\w*|Unload\w*|SetActiveScene)\s*\(",
                new[] { RuntimeRoot, TestsRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Flow/UnitySceneLoader.cs",
                }),
            new PolicyRule(
                "runtime-time-scale-owner",
                @"\bTime\s*\.\s*timeScale\s*=",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Time/GameTimeService.cs",
                }),
            new PolicyRule(
                "unitask-void",
                @"\bUniTaskVoid\b",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Audio/AudioService.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Input/ApplicationInputService.cs",
                }),
            new PolicyRule(
                "naked-forget",
                @"\.\s*Forget\s*\(",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Audio/AudioService.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Input/ApplicationInputService.cs",
                }),
            new PolicyRule(
                "cancellation-token-source-owner",
                @"\bCancellationTokenSource\b",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Lifecycle/LifecycleScope.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Lifecycle/LifecycleTaskGroup.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Flow/SceneFlowService.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Audio/AudioService.cs",
                    "Assets/Scripts/Runtime/Infrastructure/UI/ApplicationFrontEndController.cs",
                    "Assets/Scripts/Runtime/Infrastructure/UI/ApplicationSettingsController.cs",
                }),
            new PolicyRule(
                "input-device-singleton-owner",
                @"\b(?:Keyboard|Mouse|Gamepad)\s*\.\s*current\b",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Input/ApplicationInputService.cs",
                }),
            new PolicyRule(
                "input-binding-override-owner",
                @"\.\s*(?:ApplyBindingOverride|RemoveBindingOverride|RemoveAllBindingOverrides)\s*\(",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Input/ApplicationInputService.cs",
                }),
            new PolicyRule(
                "runtime-audio-owner",
                @"\b(?:AudioSource|AudioListener|AudioMixer(?:Group)?)\b",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Audio/AudioService.cs",
                    "Assets/Scripts/Runtime/Infrastructure/Audio/AudioServiceConfiguration.cs",
                }),
            new PolicyRule(
                "platform-callback-owner",
                @"\bOnApplication(?:Focus|Pause)\s*\(",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Lifecycle/ApplicationHost.cs",
                }),
            new PolicyRule(
                "runtime-guid-generation-owner",
                @"\b(?:(?:System\s*\.\s*)?Guid)\s*\.\s*NewGuid\s*\(",
                new[] { RuntimeRoot },
                new[]
                {
                    "Assets/Scripts/Runtime/Infrastructure/Identity/StableInstanceIds.cs",
                    "Assets/Scripts/Runtime/Gameplay/Combat/GameplayRandomSystem.cs",
                })
        };

        [TestCase("game-architecture-interface")]
        [TestCase("runtime-initialize-hook")]
        [TestCase("dont-destroy-on-load")]
        [TestCase("player-prefs")]
        [TestCase("business-file-io")]
        [TestCase("persistent-data-path")]
        [TestCase("business-scene-loading")]
        [TestCase("runtime-time-scale-owner")]
        [TestCase("unitask-void")]
        [TestCase("naked-forget")]
        [TestCase("cancellation-token-source-owner")]
        [TestCase("input-device-singleton-owner")]
        [TestCase("input-binding-override-owner")]
        [TestCase("runtime-audio-owner")]
        [TestCase("platform-callback-owner")]
        [TestCase("runtime-guid-generation-owner")]
        public void SourcePolicy_HasNoUnregisteredViolations(string ruleId)
        {
            PolicyContext context = CreateContext();
            AssertManifestIsValid(context);
            List<PolicyViolation> violations = Scan(context, ruleId, null);

            Assert.That(violations, Is.Empty, FormatViolations(violations));
        }

        [Test]
        public void ExceptionManifest_HasFourRequiredFieldsExactPathsAndNoStaleEntries()
        {
            PolicyContext context = CreateContext();
            AssertManifestIsValid(context);
            HashSet<string> usedExceptions = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < Rules.Length; i++)
            {
                Scan(context, Rules[i].Id, usedExceptions);
            }

            List<string> staleEntries = new List<string>();

            for (int i = 0; i < context.Manifest.Exceptions.Count; i++)
            {
                PolicyExceptionEntry entry = context.Manifest.Exceptions[i];
                string key = GetExceptionKey(entry.RuleId, entry.Path);

                if (!usedExceptions.Contains(key))
                {
                    staleEntries.Add($"{entry.RuleId} | {entry.Path}");
                }
            }

            Assert.That(
                staleEntries,
                Is.Empty,
                "例外已不再命中任何源码，请删除：\n" + string.Join("\n", staleEntries));
        }

        [Test]
        public void CSharpSanitizer_IgnoresCommentsAndLiteralsButKeepsExecutableAccess()
        {
            string sample = "// GameArchitecture.Interface\n"
                + "string normal = \"GameArchitecture.Interface\";\n"
                + "string verbatim = @\"GameArchitecture.Interface\";\n"
                + "/* GameArchitecture.Interface */\n"
                + "return GameArchitecture.Interface;\n";
            string sanitized = SanitizeCSharp(sample);
            PolicyRule rule = FindRule("game-architecture-interface");

            Assert.AreEqual(1, rule.Pattern.Matches(sanitized).Count);
        }

        [TestCase(
            "game-architecture-interface",
            "return GameArchitecture . Interface;",
            "GameArchitecture.Interface")]
        [TestCase(
            "runtime-initialize-hook",
            "[RuntimeInitializeOnLoadMethod] static void Boot() { }",
            "RuntimeInitializeOnLoadMethod")]
        [TestCase(
            "dont-destroy-on-load",
            "DontDestroyOnLoad(gameObject);",
            "DontDestroyOnLoad(")]
        [TestCase(
            "player-prefs",
            "PlayerPrefs.SetInt(key, 1);",
            "PlayerPrefs.SetInt")]
        [TestCase(
            "business-file-io",
            "System.IO.File.WriteAllText(path, value);",
            "File.WriteAllText")]
        [TestCase(
            "persistent-data-path",
            "return Application.persistentDataPath;",
            "Application.persistentDataPath")]
        [TestCase(
            "business-scene-loading",
            "SceneManager.LoadScene(sceneName);",
            "SceneManager.LoadScene")]
        [TestCase(
            "business-scene-loading",
            "SceneManager.SetActiveScene(scene);",
            "SceneManager.SetActiveScene")]
        [TestCase(
            "runtime-time-scale-owner",
            "Time.timeScale = 0f;",
            "Time.timeScale =")]
        [TestCase(
            "unitask-void",
            "UniTaskVoid RunAsync() { }",
            "UniTaskVoid")]
        [TestCase(
            "naked-forget",
            "RunAsync().Forget();",
            ".Forget(")]
        [TestCase(
            "cancellation-token-source-owner",
            "var cancellation = new CancellationTokenSource();",
            "CancellationTokenSource")]
        [TestCase(
            "input-device-singleton-owner",
            "return Gamepad.current;",
            "Gamepad.current")]
        [TestCase(
            "input-binding-override-owner",
            "action.ApplyBindingOverride(0, path);",
            "ApplyBindingOverride")]
        [TestCase(
            "runtime-audio-owner",
            "return gameObject.GetComponent<AudioListener>();",
            "AudioListener")]
        [TestCase(
            "platform-callback-owner",
            "void OnApplicationPause(bool paused) { }",
            "OnApplicationPause")]
        [TestCase(
            "runtime-guid-generation-owner",
            "var id = Guid.NewGuid();",
            "Guid.NewGuid")]
        public void PolicyRule_DetectsExecutableProbeAndIgnoresText(
            string ruleId,
            string executableProbe,
            string textProbe)
        {
            PolicyRule rule = FindRule(ruleId);
            string ignoredSource = $"// {textProbe}\nstring ignored = \"{textProbe}\";\n";
            string executableSource = ignoredSource + executableProbe;

            Assert.AreEqual(
                0,
                rule.Pattern.Matches(SanitizeCSharp(ignoredSource)).Count,
                $"规则 {ruleId} 错误命中了注释或字符串");
            Assert.AreEqual(
                1,
                rule.Pattern.Matches(SanitizeCSharp(executableSource)).Count,
                $"规则 {ruleId} 未命中可执行探针");
        }

        static PolicyContext CreateContext()
        {
            string projectRoot = FindProjectRoot();
            string manifestAbsolutePath = GetAbsolutePath(projectRoot, ManifestPath);
            Assert.IsTrue(File.Exists(manifestAbsolutePath), ManifestPath);
            string json = File.ReadAllText(manifestAbsolutePath, Encoding.UTF8);
            PolicyExceptionManifest manifest = JsonUtility.FromJson<PolicyExceptionManifest>(json);

            Assert.IsNotNull(manifest, $"无法解析 {ManifestPath}");
            return new PolicyContext(projectRoot, manifest, LoadSources(projectRoot));
        }

        static IReadOnlyList<SourceFile> LoadSources(string projectRoot)
        {
            List<SourceFile> sources = new List<SourceFile>();
            LoadSourcesUnder(projectRoot, RuntimeRoot, sources);
            LoadSourcesUnder(projectRoot, TestsRoot, sources);
            sources.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
            return sources;
        }

        static void LoadSourcesUnder(string projectRoot, string relativeRoot, List<SourceFile> sources)
        {
            string absoluteRoot = GetAbsolutePath(projectRoot, relativeRoot);
            Assert.IsTrue(Directory.Exists(absoluteRoot), relativeRoot);
            string[] paths = Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories);

            for (int i = 0; i < paths.Length; i++)
            {
                string relativePath = GetProjectRelativePath(projectRoot, paths[i]);
                string source = File.ReadAllText(paths[i], Encoding.UTF8);
                sources.Add(new SourceFile(relativePath, source, SanitizeCSharp(source)));
            }
        }

        static List<PolicyViolation> Scan(
            PolicyContext context,
            string ruleId,
            HashSet<string> usedExceptions)
        {
            PolicyRule rule = FindRule(ruleId);
            List<PolicyViolation> violations = new List<PolicyViolation>();

            for (int sourceIndex = 0; sourceIndex < context.Sources.Count; sourceIndex++)
            {
                SourceFile source = context.Sources[sourceIndex];

                if (!rule.AppliesTo(source.Path))
                {
                    continue;
                }

                MatchCollection matches = rule.Pattern.Matches(source.SanitizedSource);

                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    Match match = matches[matchIndex];

                    if (rule.IsAllowedPath(source.Path))
                    {
                        continue;
                    }

                    PolicyExceptionEntry exception = FindException(context.Manifest, rule.Id, source.Path);

                    if (exception != null)
                    {
                        usedExceptions?.Add(GetExceptionKey(exception.RuleId, exception.Path));
                        continue;
                    }

                    violations.Add(new PolicyViolation(
                        rule.Id,
                        source.Path,
                        GetLineNumber(source.Source, match.Index),
                        GetLine(source.Source, match.Index)));
                }
            }

            return violations;
        }

        static void AssertManifestIsValid(PolicyContext context)
        {
            List<string> issues = ValidateManifest(context);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        static List<string> ValidateManifest(PolicyContext context)
        {
            List<string> issues = new List<string>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            if (context.Manifest.SchemaVersion != 1)
            {
                issues.Add($"schemaVersion 必须为 1，当前为 {context.Manifest.SchemaVersion}");
            }

            for (int i = 0; i < context.Manifest.Exceptions.Count; i++)
            {
                PolicyExceptionEntry entry = context.Manifest.Exceptions[i];
                string label = $"exceptions[{i}]";

                if (entry == null)
                {
                    issues.Add($"{label} 不能为空");
                    continue;
                }

                ValidateRequiredField(entry.RuleId, label, "ruleId", issues);
                ValidateRequiredField(entry.Path, label, "path", issues);
                ValidateRequiredField(entry.Reason, label, "reason", issues);
                ValidateRequiredField(entry.RemoveByStage, label, "removeByStage", issues);

                if (!IsKnownRule(entry.RuleId))
                {
                    issues.Add($"{label}.ruleId 未登记：{entry.RuleId}");
                }

                string normalizedPath = NormalizeProjectPath(entry.Path);

                if (entry.Path != normalizedPath
                    || Path.IsPathRooted(entry.Path)
                    || entry.Path.IndexOf('*') >= 0
                    || entry.Path.IndexOf('?') >= 0
                    || entry.Path.StartsWith("../", StringComparison.Ordinal)
                    || entry.Path.Contains("/../"))
                {
                    issues.Add($"{label}.path 必须是无通配符的项目相对精确路径：{entry.Path}");
                }
                else if (!File.Exists(GetAbsolutePath(context.ProjectRoot, entry.Path)))
                {
                    issues.Add($"{label}.path 不存在：{entry.Path}");
                }

                string key = GetExceptionKey(entry.RuleId, entry.Path);

                if (!keys.Add(key))
                {
                    issues.Add($"{label} 重复：{entry.RuleId} | {entry.Path}");
                }
            }

            return issues;
        }

        static void ValidateRequiredField(
            string value,
            string entryLabel,
            string fieldName,
            List<string> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add($"{entryLabel}.{fieldName} 不能为空");
            }
        }

        static PolicyExceptionEntry FindException(
            PolicyExceptionManifest manifest,
            string ruleId,
            string path)
        {
            for (int i = 0; i < manifest.Exceptions.Count; i++)
            {
                PolicyExceptionEntry entry = manifest.Exceptions[i];

                if (entry != null && entry.RuleId == ruleId && entry.Path == path)
                {
                    return entry;
                }
            }

            return null;
        }

        static PolicyRule FindRule(string ruleId)
        {
            for (int i = 0; i < Rules.Length; i++)
            {
                if (Rules[i].Id == ruleId)
                {
                    return Rules[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(ruleId), ruleId, "未登记的基础设施规则");
        }

        static bool IsKnownRule(string ruleId)
        {
            for (int i = 0; i < Rules.Length; i++)
            {
                if (Rules[i].Id == ruleId)
                {
                    return true;
                }
            }

            return false;
        }

        static string FindProjectRoot()
        {
            List<string> candidates = new List<string>
            {
                Application.dataPath,
                Directory.GetCurrentDirectory(),
                TestContext.CurrentContext.WorkDirectory,
                TestContext.CurrentContext.TestDirectory,
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                string projectRoot = FindProjectRootFrom(candidates[i]);

                if (!string.IsNullOrEmpty(projectRoot))
                {
                    return projectRoot;
                }
            }

            Assert.Fail(
                "无法定位 Unity 项目根目录。已检查 Application.dataPath、当前目录、NUnit WorkDirectory 和 TestDirectory。");
            return string.Empty;
        }

        static string FindProjectRootFrom(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return string.Empty;
            }

            DirectoryInfo directory;

            try
            {
                directory = new DirectoryInfo(Path.GetFullPath(startPath));
            }
            catch (Exception)
            {
                return string.Empty;
            }

            while (directory != null)
            {
                bool hasRuntime = Directory.Exists(Path.Combine(
                    directory.FullName,
                    "Assets",
                    "Scripts",
                    "Runtime"));
                bool hasProjectVersion = File.Exists(Path.Combine(
                    directory.FullName,
                    "ProjectSettings",
                    "ProjectVersion.txt"));

                if (hasRuntime && hasProjectVersion)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }

        static string GetAbsolutePath(string projectRoot, string relativePath)
        {
            string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, platformPath));
        }

        static string GetProjectRelativePath(string projectRoot, string absolutePath)
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(absolutePath);
            string prefix = root + Path.DirectorySeparatorChar;

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"源码路径不在项目目录内：{path}");
            }

            return NormalizeProjectPath(path.Substring(prefix.Length));
        }

        static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        static string GetExceptionKey(string ruleId, string path)
        {
            return ruleId + "\n" + path;
        }

        static int GetLineNumber(string source, int index)
        {
            int line = 1;

            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        static string GetLine(string source, int index)
        {
            int start = source.LastIndexOf('\n', Math.Max(0, index - 1));
            start = start < 0 ? 0 : start + 1;
            int end = source.IndexOf('\n', index);
            end = end < 0 ? source.Length : end;
            return source.Substring(start, end - start).Trim();
        }

        static string FormatViolations(IReadOnlyList<PolicyViolation> violations)
        {
            if (violations.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder("发现未登记的基础设施策略违规：\n");

            for (int i = 0; i < violations.Count; i++)
            {
                PolicyViolation violation = violations[i];
                builder.Append('[')
                    .Append(violation.RuleId)
                    .Append("] ")
                    .Append(violation.Path)
                    .Append(':')
                    .Append(violation.Line)
                    .Append(" | ")
                    .AppendLine(violation.Evidence);
            }

            return builder.ToString();
        }

        static string SanitizeCSharp(string source)
        {
            char[] sanitized = source.ToCharArray();
            int index = 0;

            while (index < source.Length)
            {
                if (StartsWith(source, index, "//"))
                {
                    index = BlankLineComment(source, sanitized, index);
                }
                else if (StartsWith(source, index, "/*"))
                {
                    index = BlankBlockComment(source, sanitized, index);
                }
                else if (source[index] == '\'')
                {
                    index = BlankCharacterLiteral(source, sanitized, index);
                }
                else if (source[index] == '"')
                {
                    index = BlankStringLiteral(source, sanitized, index);
                }
                else
                {
                    index++;
                }
            }

            return new string(sanitized);
        }

        static int BlankLineComment(string source, char[] sanitized, int index)
        {
            while (index < source.Length && source[index] != '\n')
            {
                Blank(sanitized, index);
                index++;
            }

            return index;
        }

        static int BlankBlockComment(string source, char[] sanitized, int index)
        {
            Blank(sanitized, index++);
            Blank(sanitized, index++);

            while (index < source.Length)
            {
                if (StartsWith(source, index, "*/"))
                {
                    Blank(sanitized, index++);
                    Blank(sanitized, index++);
                    break;
                }

                Blank(sanitized, index++);
            }

            return index;
        }

        static int BlankCharacterLiteral(string source, char[] sanitized, int index)
        {
            Blank(sanitized, index++);

            while (index < source.Length)
            {
                char character = source[index];
                Blank(sanitized, index++);

                if (character == '\\' && index < source.Length)
                {
                    Blank(sanitized, index++);
                    continue;
                }

                if (character == '\'')
                {
                    break;
                }
            }

            return index;
        }

        static int BlankStringLiteral(string source, char[] sanitized, int quoteIndex)
        {
            int quoteCount = CountRun(source, quoteIndex, '"');

            if (quoteCount >= 3)
            {
                return BlankRawStringLiteral(source, sanitized, quoteIndex, quoteCount);
            }

            bool verbatim = quoteIndex > 0 && source[quoteIndex - 1] == '@'
                || quoteIndex > 1 && source[quoteIndex - 2] == '@' && source[quoteIndex - 1] == '$';
            int index = quoteIndex;
            Blank(sanitized, index++);

            while (index < source.Length)
            {
                char character = source[index];
                Blank(sanitized, index++);

                if (!verbatim && character == '\\' && index < source.Length)
                {
                    Blank(sanitized, index++);
                    continue;
                }

                if (verbatim && character == '"' && index < source.Length && source[index] == '"')
                {
                    Blank(sanitized, index++);
                    continue;
                }

                if (character == '"')
                {
                    break;
                }
            }

            return index;
        }

        static int BlankRawStringLiteral(
            string source,
            char[] sanitized,
            int quoteIndex,
            int quoteCount)
        {
            int index = quoteIndex;

            for (int i = 0; i < quoteCount; i++)
            {
                Blank(sanitized, index++);
            }

            while (index < source.Length)
            {
                if (CountRun(source, index, '"') >= quoteCount)
                {
                    for (int i = 0; i < quoteCount; i++)
                    {
                        Blank(sanitized, index++);
                    }

                    break;
                }

                Blank(sanitized, index++);
            }

            return index;
        }

        static int CountRun(string source, int index, char character)
        {
            int count = 0;

            while (index + count < source.Length && source[index + count] == character)
            {
                count++;
            }

            return count;
        }

        static bool StartsWith(string source, int index, string value)
        {
            if (index + value.Length > source.Length)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (source[index + i] != value[i])
                {
                    return false;
                }
            }

            return true;
        }

        static void Blank(char[] source, int index)
        {
            if (source[index] != '\r' && source[index] != '\n')
            {
                source[index] = ' ';
            }
        }

        sealed class PolicyContext
        {
            public string ProjectRoot { get; }

            public PolicyExceptionManifest Manifest { get; }

            public IReadOnlyList<SourceFile> Sources { get; }

            public PolicyContext(
                string projectRoot,
                PolicyExceptionManifest manifest,
                IReadOnlyList<SourceFile> sources)
            {
                ProjectRoot = projectRoot;
                Manifest = manifest;
                Sources = sources;
            }
        }

        sealed class PolicyRule
        {
            readonly HashSet<string> _allowedPaths;
            readonly string[] _roots;

            public string Id { get; }

            public Regex Pattern { get; }

            public PolicyRule(
                string id,
                string pattern,
                string[] roots,
                string[] allowedPaths)
            {
                Id = id;
                Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
                _roots = roots;
                _allowedPaths = new HashSet<string>(allowedPaths, StringComparer.Ordinal);
            }

            public bool AppliesTo(string path)
            {
                for (int i = 0; i < _roots.Length; i++)
                {
                    if (path.StartsWith(_roots[i] + "/", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool IsAllowedPath(string path)
            {
                return _allowedPaths.Contains(path);
            }
        }

        sealed class SourceFile
        {
            public string Path { get; }

            public string Source { get; }

            public string SanitizedSource { get; }

            public SourceFile(string path, string source, string sanitizedSource)
            {
                Path = path;
                Source = source;
                SanitizedSource = sanitizedSource;
            }
        }

        sealed class PolicyViolation
        {
            public string RuleId { get; }

            public string Path { get; }

            public int Line { get; }

            public string Evidence { get; }

            public PolicyViolation(string ruleId, string path, int line, string evidence)
            {
                RuleId = ruleId;
                Path = path;
                Line = line;
                Evidence = evidence;
            }
        }

        [Serializable]
        sealed class PolicyExceptionManifest
        {
            // JSON wire fields retain the manifest's lowerCamelCase names.
            [SerializeField]
            int schemaVersion;

            [SerializeField]
            List<PolicyExceptionEntry> exceptions = new List<PolicyExceptionEntry>();

            public int SchemaVersion => schemaVersion;

            public IReadOnlyList<PolicyExceptionEntry> Exceptions => exceptions;
        }

        [Serializable]
        sealed class PolicyExceptionEntry
        {
            // JSON wire fields retain the manifest's lowerCamelCase names.
            [SerializeField]
            string ruleId = string.Empty;

            [SerializeField]
            string path = string.Empty;

            [SerializeField]
            string reason = string.Empty;

            [SerializeField]
            string removeByStage = string.Empty;

            public string RuleId => ruleId;

            public string Path => path;

            public string Reason => reason;

            public string RemoveByStage => removeByStage;
        }
    }
}
