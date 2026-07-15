using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace DarkFlare.Editor
{
    /// <summary>
    /// 编译分析器：每次脚本编译时，把重新编译的程序集与耗时打印到 Console，
    /// 用于验证程序集拆分带来的增量编译收益。通过菜单开关，默认关闭，关闭时零开销。
    /// 注意：这里统计的是「编译」耗时，不含其后的域重载（domain reload）。
    /// </summary>
    [InitializeOnLoad]
    static class CompilationProfiler
    {
        const string MenuPath = "DarkFlare/编译分析器";
        const string EnabledKey = "DarkFlare.CompilationProfiler.Enabled";
        const string StartTicksKey = "DarkFlare.CompilationProfiler.StartTicks";
        const string LastMsKey = "DarkFlare.CompilationProfiler.LastMs";

        static CompilationProfiler()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Debug.Log($"[编译分析器] {(Enabled ? "已开启，编辑脚本后查看 Console" : "已关闭")}");
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void OnCompilationStarted(object context)
        {
            if (!Enabled)
            {
                return;
            }

            SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());
            SessionState.SetInt(LastMsKey, 0);
            Debug.Log("[编译分析器] ===== 编译开始 =====");
        }

        static void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!Enabled)
            {
                return;
            }

            int errorCount = 0;
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    errorCount++;
                }
            }

            int totalMs = ElapsedMs();
            int lastMs = SessionState.GetInt(LastMsKey, 0);
            SessionState.SetInt(LastMsKey, totalMs);

            string assemblyName = Path.GetFileName(assemblyPath);
            Debug.Log($"[编译分析器] 重新编译 {assemblyName}  约 {totalMs - lastMs} ms（累计 {totalMs} ms，错误 {errorCount}）");
        }

        static void OnCompilationFinished(object context)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.Log($"[编译分析器] ===== 编译结束，总耗时 {ElapsedMs()} ms（不含域重载）=====");
        }

        static int ElapsedMs()
        {
            string raw = SessionState.GetString(StartTicksKey, string.Empty);
            if (long.TryParse(raw, out long ticks))
            {
                return (int)(DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalMilliseconds;
            }

            return 0;
        }
    }
}
