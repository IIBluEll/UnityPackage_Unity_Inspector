# Unity Project Inspector

Unity 2022.3 이상에서 BuildReport를 JSON으로 내보내는 Editor 전용 패키지입니다.

## 출력 위치

```text
<UnityProject>/.unityprojectinspector/build_report.json
```

## Schema Version 2

- `reportedOutputSizeBytes`: Unity `BuildSummary.totalSize` 값
- `artifactSizeBytes`: `outputPath`에 생성된 실제 파일 또는 디렉터리 크기
- `artifactSizeSource`: `File`, `Directory`, `Unavailable` 중 하나
- `reportedWarningCount`, `reportedErrorCount`: Unity BuildSummary 원본 개수
- `warningCount`, `errorCount`: Inspector 자체 로그를 제외한 표시 대상 개수

Asset Type은 Texture, Audio, Mesh, Animation, Shader, Scene, Font,
Prefab, Material, Script, Assembly, Data, Video, Other로 분류합니다.
`Assets/`와 `Packages/` 경로는 원본 Main Asset Type을 우선 사용합니다.
