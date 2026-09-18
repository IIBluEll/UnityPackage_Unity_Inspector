# Unity Project Inspector

Unity 2022.3 이상을 최소 버전으로 선언한 BuildReport JSON 내보내기 Editor 전용 패키지입니다. 실제 빌드 검증은 Unity 6000.3.20f1에서 수행했습니다.

## 출력 위치

```text
<UnityProject>/.unityprojectinspector/build_report.json
```

## Schema Version 2

- `reportedOutputSizeBytes`: Unity `BuildSummary.totalSize` 값
- `artifactSizeBytes`: `outputPath`가 가리키는 파일 또는 디렉터리 크기. Windows Standalone에서 경로가 EXE면 함께 생성된 Data 폴더는 포함하지 않음
- `artifactSizeSource`: `File`, `Directory`, `Unavailable` 중 하나
- `reportedWarningCount`, `reportedErrorCount`: Unity BuildSummary 원본 개수
- `warningCount`, `errorCount`: Inspector 자체 로그를 제외한 표시 대상 개수

Asset Type은 Texture, Audio, Mesh, Animation, Shader, Scene, Font,
Prefab, Material, Script, Assembly, Data, Video, Other로 분류합니다.
`Assets/`와 `Packages/` 경로는 원본 Main Asset Type을 우선 사용합니다.

## 설치 및 사용

Unity Package Manager의 **Add package from disk**에서 이 패키지의 `package.json`을 선택합니다. Unity 프로젝트를 빌드하면 Editor 전용 `BuildPostProcessor`가 위 경로에 보고서를 내보냅니다. 빌드가 끝난 뒤 Unity 메뉴 **Tools > Unity Project Inspector > Export Latest Build Report**로 마지막 BuildReport를 수동 내보낼 수도 있습니다. 생성된 `.unityprojectinspector/` 폴더는 필요에 따라 Unity 프로젝트의 `.gitignore`에 추가하세요.

## 검증 결과 (2026-09-18, v0.2.2)

Unity 6000.3.20f1 Windows 빌드에서 수정 전에는 `OnPostprocessBuild` 자동 JSON에 `result = Unknown`, 빌드 시간과 Unity 보고 출력 크기 `0`이 기록됐습니다. v0.2.2는 빌드 결과가 확정된 뒤 자동으로 내보냅니다. Editor를 열어둔 경우와 배치 모드 종료 시 모두 자동 JSON의 `result = Succeeded`, 정상 빌드 시간·출력 크기를 확인했습니다. `DateTime.Kind = Unspecified`인 빌드 시작·종료 시각도 이번 테스트 환경의 UTC 값에 맞게 기록됐습니다.

빌드 전 단계에서 실패하면 콜백이 실행되지 않아 이전 JSON이 그대로 남는 사례를 확인했습니다. 빌드 취소와 Unity 2022.3에서는 아직 실제 검증하지 않았습니다. 자세한 검증 기록은 WPF 앱 저장소의 `docs/VERIFICATION_2026-09-18.md`에 있습니다.
