using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HM.UnityProjectInspector.Editor
{
    internal static class BuildReportExporter
    {
        private const int SCHEMA_VERSION = 2;

        private const string LOG_PREFIX =
            "[Unity Project Inspector]";

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
            AssetBuildInfoDto[] assetInfoArray =
                CreateAssetInfoArray(buildReport);

            BuildMessageDto[] messageArray =
                CreateMessageArray(buildReport);

            return new BuildReportDto
            {
                schemaVersion = SCHEMA_VERSION ,
                projectName = Application.productName ,
                unityVersion = Application.unityVersion ,
                assetSizeDefinition = ASSET_SIZE_DEFINITION ,
                build = CreateBuildInfo(buildReport , messageArray) ,
                assets = assetInfoArray ,
                messages = messageArray
            };
        }

        private static BuildInfoDto CreateBuildInfo(
            BuildReport buildReport ,
            BuildMessageDto[] messageArray)
        {
            BuildSummary buildSummary = buildReport.summary;

            long artifactSizeBytes = GetArtifactSize(
                buildSummary.outputPath ,
                out string artifactSizeSource);

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
                reportedOutputSizeBytes =
                    ConvertSize(buildSummary.totalSize) ,
                artifactSizeBytes = artifactSizeBytes ,
                artifactSizeSource = artifactSizeSource ,
                warningCount = CountMessages(messageArray , "Warning") ,
                errorCount = CountMessages(messageArray , "Error") ,
                reportedWarningCount = buildSummary.totalWarnings ,
                reportedErrorCount = buildSummary.totalErrors
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

            Type sourceAssetType = GetSourceAssetType(
                assetPath ,
                packedAssetInfo.type);

            assetInfoMap.Add(
                assetPath ,
                new AssetBuildInfoDto
                {
                    name = GetAssetName(assetPath) ,
                    path = assetPath ,
                    type = GetAssetType(
                        sourceAssetType ,
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

                    if ( IsInspectorMessage(buildMessage.content) )
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

        private static bool IsInspectorMessage(string message)
        {
            return !string.IsNullOrEmpty(message) &&
                message.StartsWith(
                    LOG_PREFIX ,
                    StringComparison.Ordinal);
        }

        private static int CountMessages(
            BuildMessageDto[] messageArray ,
            string messageType)
        {
            int count = 0;

            foreach ( BuildMessageDto message in messageArray )
            {
                if ( string.Equals(
                        message.type ,
                        messageType ,
                        StringComparison.Ordinal) )
                {
                    count++;
                }
            }

            return count;
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
            string extension = GetAssetExtension(assetPath);

            switch ( extension )
            {
                case ".unity":
                    return "Scene";

                case ".fbx":
                case ".obj":
                case ".dae":
                case ".blend":
                case ".3ds":
                    return "Mesh";

                case ".anim":
                case ".controller":
                case ".overridecontroller":
                    return "Animation";

                case ".shader":
                case ".shadergraph":
                case ".shadersubgraph":
                case ".compute":
                case ".hlsl":
                case ".cginc":
                case ".glslinc":
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
                case ".spriteatlas":
                case ".cubemap":
                    return "Texture";

                case ".ttf":
                case ".otf":
                case ".ttc":
                    return "Font";

                case ".prefab":
                    return "Prefab";

                case ".mat":
                    return "Material";

                case ".cs":
                    return "Script";

                case ".dll":
                    return "Assembly";

                case ".inputactions":
                case ".json":
                case ".xml":
                case ".txt":
                case ".csv":
                case ".bytes":
                case ".yaml":
                case ".yml":
                    return "Data";

                case ".mp4":
                case ".mov":
                case ".webm":
                    return "Video";
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

            if ( typeof(Font).IsAssignableFrom(assetType) ||
                assetType.Name.IndexOf(
                    "FontAsset" ,
                    StringComparison.OrdinalIgnoreCase) >= 0 )
            {
                return "Font";
            }

            if ( typeof(Material).IsAssignableFrom(assetType) )
            {
                return "Material";
            }

            if ( typeof(MonoScript).IsAssignableFrom(assetType) )
            {
                return "Script";
            }

            if ( typeof(ScriptableObject).IsAssignableFrom(assetType) )
            {
                return "Data";
            }

            return "Other";
        }

        private static Type GetSourceAssetType(
            string assetPath ,
            Type packedAssetType)
        {
            if ( !IsProjectAssetPath(assetPath) )
            {
                return packedAssetType;
            }

            Type sourceAssetType =
                AssetDatabase.GetMainAssetTypeAtPath(assetPath);

            return sourceAssetType ?? packedAssetType;
        }

        private static bool IsProjectAssetPath(string assetPath)
        {
            return assetPath.StartsWith(
                       "Assets/" ,
                       StringComparison.Ordinal) ||
                assetPath.StartsWith(
                    "Packages/" ,
                    StringComparison.Ordinal);
        }

        private static string GetAssetExtension(string assetPath)
        {
            string assetName = GetAssetName(assetPath);

            int extensionIndex = assetName.LastIndexOf('.');

            if ( extensionIndex <= 0 )
            {
                return string.Empty;
            }

            return assetName
                .Substring(extensionIndex)
                .ToLowerInvariant();
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

            return $"BuiltIn/{packedAssetInfo.sourceAssetGUID}";
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\' , '/');
        }

        private static string GetAssetName(string assetPath)
        {
            if ( string.IsNullOrEmpty(assetPath) )
            {
                return string.Empty;
            }

            string normalizedPath = NormalizePath(assetPath).TrimEnd('/');

            int separatorIndex = normalizedPath.LastIndexOf('/');

            if ( separatorIndex < 0 )
            {
                return normalizedPath;
            }

            return normalizedPath.Substring(separatorIndex + 1);
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

        private static long GetArtifactSize(
            string outputPath ,
            out string artifactSizeSource)
        {
            if ( string.IsNullOrWhiteSpace(outputPath) )
            {
                artifactSizeSource = "Unavailable";
                return 0;
            }

            if ( File.Exists(outputPath) )
            {
                artifactSizeSource = "File";
                return new FileInfo(outputPath).Length;
            }

            if ( Directory.Exists(outputPath) )
            {
                artifactSizeSource = "Directory";
                return GetDirectorySize(outputPath);
            }

            artifactSizeSource = "Unavailable";
            return 0;
        }

        private static long GetDirectorySize(string directoryPath)
        {
            long totalSizeBytes = 0;

            foreach ( string filePath in Directory.EnumerateFiles(
                         directoryPath ,
                         "*" ,
                         SearchOption.AllDirectories) )
            {
                totalSizeBytes = checked(
                    totalSizeBytes + new FileInfo(filePath).Length);
            }

            return totalSizeBytes;
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
