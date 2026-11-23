using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Animal_Run.Scripts.EditorTools
{
    // 에디터가 열려 있어 배치 모드를 쓸 수 없을 때, 외부(make-animal 스킬 등)에서 프리팹 빌드를 요청하는 통로.
    // Temp/RunnerPrefabRequests/<Name>.request 파일을 만들면 열린 에디터가 RunnerPrefabBuilder 로 빌드하고
    // 같은 폴더에 <Name>.result (빌드·검증 로그, 마지막 줄 RESULT OK / RESULT FAIL) 와
    // <Name>_preview.png (Unity 셰이더로 찍은 3/4·옆모습) 를 남긴다.
    [InitializeOnLoad]
    public static class RunnerPrefabRequestWatcher
    {
        private const string RequestDir = "Temp/RunnerPrefabRequests";
        private const double PollSeconds = 1.0;
        private static double nextPoll;

        static RunnerPrefabRequestWatcher()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll)
                return;
            nextPoll = EditorApplication.timeSinceStartup + PollSeconds;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
                || !Directory.Exists(RequestDir))
                return;

            foreach (string requestPath in Directory.GetFiles(RequestDir, "*.request"))
            {
                string name = Path.GetFileNameWithoutExtension(requestPath);
                File.Delete(requestPath);
                File.WriteAllLines(Path.Combine(RequestDir, name + ".result"), Run(name));
            }
        }

        private static List<string> Run(string name)
        {
            var lines = new List<string>();
            void Capture(string message, string stackTrace, LogType type)
            {
                if (message.StartsWith("[RunnerPrefabBuilder]") || message.StartsWith("[Verify]") || type == LogType.Exception || type == LogType.Error)
                    lines.Add($"{type}: {message}");
            }

            Application.logMessageReceived += Capture;
            bool ok = false;
            try
            {
                RunnerPrefabBuilder.BuildAndVerify(name);
                ok = true;
                RunnerPrefabBuilder.CapturePreview(name, Path.Combine(RequestDir, name + "_preview.png"));
            }
            catch (System.Exception e)
            {
                lines.Add($"Exception: {e}");
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }
            lines.Add(ok ? "RESULT OK" : "RESULT FAIL");
            return lines;
        }
    }
}
