using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HM.UnityProjectInspector.Editor
{
    public sealed class BuildPostProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport buildReport)
        {
            try
            {
                string reportPath =
                    BuildReportExporter.Export(buildReport);

                Debug.Log(
                    $"[Unity Project Inspector] BuildReport를 생성했습니다.\n{reportPath}");
            }
            catch ( Exception exception )
            {
                Debug.LogError(
                    "[Unity Project Inspector] " +
                    $"BuildReport 생성에 실패했습니다.\n{exception}");
            }
        }

    }
}
