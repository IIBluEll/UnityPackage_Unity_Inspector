using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hmlee.UnityProjectInspector.Editor
{
    internal static class BuildReportExporter
    {
        private const int SCHEMA_VERSION = 1;

        private const string ASSET_SIZE_DEFINITION =
            "Unity BuildReport PackedAssetInfo.packedSize";

        private const string REPORT_DIRECTORY_NAME =
            ".unityprojectinspector";

        private const string REPORT_FILE_NAME =
            "build_report.json";

        private static readonly Encoding JSON_ENCODING =
            new UTF8Encoding(false);

        [MenuItem(
            "Tools/Unity Project Inspector/Export Latest Build Report")]
        private static void ExportLatestReport()
        {
            BuildReport buildReport = BuildReport.GetLatestReport();

            if ( buildReport == null )
            {
                Debug.LogWarning(
                    "[Unity Project Inspector] 최근 BuildReport가 없습니다.");

                return;
            }

            string reportPath = Export(buildReport);

            Debug.Log(
                $"[Unity Project Inspector] BuildReport를 생성했습니다.\n{reportPath}");
        }

        public static string Export(BuildReport buildReport)
        {
            if ( buildReport == null )
            {
                throw new ArgumentNullException(nameof(buildReport));
            }

            BuildReportDto reportDto = CreateReportDto(buildReport);
            string json = JsonUtility.ToJson(reportDto, true);
            string reportPath = GetReportPath();

            WriteAtomically(reportPath , json);

            return reportPath;
        }

        private static BuildReportDto CreateReportDto(
            BuildReport buildReport)
        {
            BuildSummary buildSummary = buildReport.summary;

            return new BuildReportDto
            {
                schemaVersion = SCHEMA_VERSION ,
                projectName = Application.productName ,
                unityVersion = Application.unityVersion ,
                assetSizeDefinition = ASSET_SIZE_DEFINITION ,
                build = CreateBuildInfo(buildSummary) ,
                assets = CreateAssetInfoArray(buildReport) ,
                messages = CreateMessageArray(buildReport)
            };
        }

        private static BuildInfoDto CreateBuildInfo(
            BuildSummary buildSummary)
        {
            return new BuildInfoDto
            {
                buildGuid = buildSummary.guid.ToString() ,
                platform = buildSummary.platform.ToString() ,
                result = buildSummary.result.ToString() ,
                outputPath = NormalizePath(buildSummary.outputPath) ,
                buildStartedAtUtc =
                    FormatUtcDateTime(buildSummary.buildStartedAt) ,
                buildEndedAtUtc =
                    FormatUtcDateTime(buildSummary.buildEndedAt) ,
                reportGeneratedAtUtc =
                    FormatUtcDateTime(DateTime.UtcNow) ,
                buildTimeSeconds =
                    buildSummary.totalTime.TotalSeconds ,
                outputSizeBytes =
                    ConvertSize(buildSummary.totalSize) ,
                warningCount = buildSummary.totalWarnings ,
                errorCount = buildSummary.totalErrors
            };
        }

        private static AssetBuildInfoDto[] CreateAssetInfoArray(
            BuildReport buildReport)
        {
            Dictionary<string, AssetBuildInfoDto> assetInfoMap =
                new Dictionary<string, AssetBuildInfoDto>(
                    StringComparer.OrdinalIgnoreCase);

            foreach ( PackedAssets packedAssets in buildReport.packedAssets )
            {
                if ( packedAssets == null )
                {
                    continue;
                }

                foreach (
                    PackedAssetInfo packedAssetInfo
                    in packedAssets.contents )
                {
                    AddPackedAsset(assetInfoMap , packedAssetInfo);
                }
            }

            List<AssetBuildInfoDto> assetInfoList =
                new List<AssetBuildInfoDto>(assetInfoMap.Values);

            assetInfoList.Sort(CompareAssetInfo);

            return assetInfoList.ToArray();
        }

        private static void AddPackedAsset(
            Dictionary<string , AssetBuildInfoDto> assetInfoMap ,
            PackedAssetInfo packedAssetInfo)
        {
            string assetPath =
                NormalizeAssetPath(packedAssetInfo);

            long packedSize =
                ConvertSize(packedAssetInfo.packedSize);

            if ( assetInfoMap.TryGetValue(
                    assetPath ,
                    out AssetBuildInfoDto existingAssetInfo) )
            {
                existingAssetInfo.packedSizeBytes = checked(
                    existingAssetInfo.packedSizeBytes + packedSize);

                return;
            }

            assetInfoMap.Add(
                assetPath ,
                new AssetBuildInfoDto
                {
                    name = GetAssetName(assetPath) ,
                    path = assetPath ,
                    type = GetAssetType(
                        packedAssetInfo.type ,
                        assetPath) ,
                    packedSizeBytes = packedSize
                });
        }

        private static BuildMessageDto[] CreateMessageArray(
            BuildReport buildReport)
        {
            List<BuildMessageDto> messageList =
                new List<BuildMessageDto>();

            foreach ( BuildStep buildStep in buildReport.steps )
            {
                foreach (
                    BuildStepMessage buildMessage
                    in buildStep.messages )
                {
                    string messageType =
                        GetMessageType(buildMessage.type);

                    if ( string.IsNullOrEmpty(messageType) )
                    {
                        continue;
                    }

                    messageList.Add(
                        new BuildMessageDto
                        {
                            type = messageType ,
                            message = buildMessage.content
                        });
                }
            }

            return messageList.ToArray();
        }

        private static string GetMessageType(LogType logType)
        {
            switch ( logType )
            {
                case LogType.Warning:
                    return "Warning";

                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return "Error";

                default:
                    return string.Empty;
            }
        }

        private static string GetAssetType(
            Type assetType ,
            string assetPath)
        {
            string extension =
                Path.GetExtension(assetPath).ToLowerInvariant();

            switch ( extension )
            {
                case ".unity":
                    return "Scene";

                case ".fbx":
                case ".obj":
                case ".dae":
                case ".blend":
                    return "Mesh";

                case ".anim":
                    return "Animation";

                case ".shader":
                case ".shadergraph":
                case ".shadersubgraph":
                    return "Shader";

                case ".wav":
                case ".mp3":
                case ".ogg":
                case ".aif":
                case ".aiff":
                    return "Audio";

                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                case ".exr":
                case ".hdr":
                    return "Texture";
            }

            if ( assetType == null )
            {
                return "Other";
            }

            if ( typeof(Texture).IsAssignableFrom(assetType) )
            {
                return "Texture";
            }

            if ( typeof(AudioClip).IsAssignableFrom(assetType) )
            {
                return "Audio";
            }

            if ( typeof(Mesh).IsAssignableFrom(assetType) )
            {
                return "Mesh";
            }

            if ( typeof(AnimationClip).IsAssignableFrom(assetType) )
            {
                return "Animation";
            }

            if ( typeof(Shader).IsAssignableFrom(assetType) )
            {
                return "Shader";
            }

            return "Other";
        }

        private static string NormalizeAssetPath(
            PackedAssetInfo packedAssetInfo)
        {
            if ( !string.IsNullOrWhiteSpace(
                    packedAssetInfo.sourceAssetPath) )
            {
                return NormalizePath(
                    packedAssetInfo.sourceAssetPath);
            }

            return
                $"<BuiltIn>/{packedAssetInfo.sourceAssetGUID}";
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\' , '/');
        }

        private static string GetAssetName(string assetPath)
        {
            string assetName = Path.GetFileName(assetPath);

            return string.IsNullOrEmpty(assetName)
                ? assetPath
                : assetName;
        }

        private static int CompareAssetInfo(
            AssetBuildInfoDto left ,
            AssetBuildInfoDto right)
        {
            int sizeComparison =
                right.packedSizeBytes.CompareTo(
                    left.packedSizeBytes);

            if ( sizeComparison != 0 )
            {
                return sizeComparison;
            }

            return string.Compare(
                left.path ,
                right.path ,
                StringComparison.OrdinalIgnoreCase);
        }

        private static long ConvertSize(ulong size)
        {
            return checked((long)size);
        }

        private static string FormatUtcDateTime(
            DateTime dateTime)
        {
            return dateTime
                .ToUniversalTime()
                .ToString("O" , CultureInfo.InvariantCulture);
        }

        private static string GetReportPath()
        {
            DirectoryInfo projectDirectory =
                Directory.GetParent(Application.dataPath);

            if ( projectDirectory == null )
            {
                throw new DirectoryNotFoundException(
                    "Unity 프로젝트 루트 경로를 찾을 수 없습니다.");
            }

            string reportDirectoryPath = Path.Combine(
                projectDirectory.FullName,
                REPORT_DIRECTORY_NAME);

            Directory.CreateDirectory(reportDirectoryPath);

            return Path.Combine(
                reportDirectoryPath ,
                REPORT_FILE_NAME);
        }

        private static void WriteAtomically(
            string reportPath ,
            string json)
        {
            string temporaryPath =
                $"{reportPath}.{Guid.NewGuid():N}.tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath ,
                    json ,
                    JSON_ENCODING);

                if ( File.Exists(reportPath) )
                {
                    File.Replace(
                        temporaryPath ,
                        reportPath ,
                        null);
                }
                else
                {
                    File.Move(
                        temporaryPath ,
                        reportPath);
                }
            }
            finally
            {
                if ( File.Exists(temporaryPath) )
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
