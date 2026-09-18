using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HM.UnityProjectInspector.Editor
{
    public sealed class BuildPostProcessor : IPostprocessBuildWithReport
    {
        private const double EXPORT_TIMEOUT_SECONDS = 30.0;

        private static BuildReport s_pendingReport;
        private static string s_pendingBuildGuid;
        private static double s_pendingSince;

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport buildReport)
        {
            if ( buildReport == null )
            {
                Debug.LogError("[Unity Project Inspector] BuildReport가 없습니다.");
                return;
            }

            s_pendingReport = buildReport;
            s_pendingBuildGuid = buildReport.summary.guid.ToString();
            s_pendingSince = EditorApplication.timeSinceStartup;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnEditorUpdate()
        {
            if ( s_pendingReport == null || BuildPipeline.isBuildingPlayer )
            {
                return;
            }

            if ( TryExportPendingReport() )
            {
                return;
            }

            if ( EditorApplication.timeSinceStartup - s_pendingSince < EXPORT_TIMEOUT_SECONDS )
            {
                return;
            }

            Debug.LogError("[Unity Project Inspector] 빌드 결과가 확정되지 않아 BuildReport를 내보내지 못했습니다.");
            ClearPendingReport();
        }

        private static void OnEditorQuitting()
        {
            if ( s_pendingReport == null )
            {
                return;
            }

            if ( BuildPipeline.isBuildingPlayer || !TryExportPendingReport() )
            {
                Debug.LogError("[Unity Project Inspector] Editor 종료 전에 빌드 결과가 확정되지 않아 BuildReport를 내보내지 못했습니다.");
                ClearPendingReport();
            }
        }

        private static bool TryExportPendingReport()
        {
            BuildReport latestReport = BuildReport.GetLatestReport();
            BuildReport reportToExport = latestReport != null &&
                string.Equals(latestReport.summary.guid.ToString(), s_pendingBuildGuid, StringComparison.OrdinalIgnoreCase)
                    ? latestReport
                    : s_pendingReport;

            if ( reportToExport.summary.result == BuildResult.Unknown )
            {
                return false;
            }

            try
            {
                string reportPath = BuildReportExporter.Export(reportToExport);
                Debug.Log($"[Unity Project Inspector] BuildReport를 생성했습니다.\n{reportPath}");
            }
            catch ( Exception exception )
            {
                Debug.LogError($"[Unity Project Inspector] BuildReport 생성에 실패했습니다.\n{exception}");
            }
            finally
            {
                ClearPendingReport();
            }

            return true;
        }

        private static void ClearPendingReport()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.quitting -= OnEditorQuitting;
            s_pendingReport = null;
            s_pendingBuildGuid = null;
        }
    }
}
