using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Caro.EditorTools
{
    /// <summary>Runs the EditMode tests from tooling and keeps a one-line summary in <see cref="Result"/>.</summary>
    public static class CaroTestHook
    {
        public static string Result = "idle";
        public static string Failures = "";

        [MenuItem("Caro/Run EditMode tests")]
        public static void Run()
        {
            Result = "running";
            Failures = "";
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Caro.Tests" } }));
        }

        private class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor r) =>
                Result = $"pass={r.PassCount} fail={r.FailCount} skip={r.SkipCount} inconclusive={r.InconclusiveCount}";
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.TestStatus == TestStatus.Failed && !r.HasChildren) Failures += $"{r.FullName}: {r.Message}\n";
            }
        }
    }
}
