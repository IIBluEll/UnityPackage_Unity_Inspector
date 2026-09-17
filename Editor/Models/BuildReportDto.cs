using System;

namespace Hmlee.UnityProjectInspector.Editor
{
    [Serializable]
    public sealed class BuildReportDto
    {
        public int schemaVersion;
        public string projectName = string.Empty;
        public string unityVersion = string.Empty;
        public string assetSizeDefinition = string.Empty;

        public BuildInfoDto build = new BuildInfoDto();
        public AssetBuildInfoDto[] assets = Array.Empty<AssetBuildInfoDto>();
        public BuildMessageDto[] messages = Array.Empty<BuildMessageDto>();
    }

    [Serializable]
    public sealed class BuildInfoDto
    {
        public string buildGuid = string.Empty;
        public string platform = string.Empty;
        public string result = string.Empty;
        public string outputPath = string.Empty;

        public string buildStartedAtUtc = string.Empty;
        public string buildEndedAtUtc = string.Empty;
        public string reportGeneratedAtUtc = string.Empty;

        public double buildTimeSeconds;
        public long outputSizeBytes;

        public int warningCount;
        public int errorCount;
    }

    [Serializable]
    public sealed class AssetBuildInfoDto
    {
        public string name = string.Empty;
        public string path = string.Empty;
        public string type = string.Empty;

        public long packedSizeBytes;
    }

    [Serializable]
    public sealed class BuildMessageDto
    {
        public string type = string.Empty;
        public string message = string.Empty;
    }
}