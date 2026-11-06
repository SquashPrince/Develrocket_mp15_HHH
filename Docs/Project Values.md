# 프로젝트 값

> 프로젝트 값 템플릿이다. 새 프로젝트는 모든 항목을 `미정`으로 두고 시작한다. 매 세션 상시 로드된다.
>
> 값이 확정되면 `미정`을 실제 값으로 바꾸고 출처·확인 날짜를 적는다. 코드를 작성할 때는 확정된 값을 기준으로 하며, `미정` 값은 추측해서 쓰지 않는다.
>
> 확인 시점과 확정 절차는 [프로젝트 설정](<Project Configuration.md>)을 따른다. 매 세션 확인하지 않고, 값이 바뀌었거나 작업에 필요할 때만 확인한다.

| 항목 | 값 | 출처·확인 날짜 |
|---|---|---|
| 프로젝트 이름·경로 | 정식 이름 `Hollow Horizon Hunter` (`productName`과 같음), 약칭 `HHH`. 경로 `Project-HHH/Hollow Horizon Hunter` | `ProjectSettings.asset` · 사용자 답변 · 2026-11-06 |
| 장르 | 3D 어반 판타지 액션 | 사용자 답변 · 2026-11-06 |
| 팀 구성 | 1인 | 기획서 1장 · 사용자 답변 · 2026-11-06 |
| Unity Editor 전체 버전 | 6000.3.9f1 LTS | `ProjectSettings/ProjectVersion.txt` · 사용자 답변 · 2026-11-06 |
| 렌더 파이프라인 | URP (사용 에셋 `Assets/Settings/PC_RPAsset`) | `Packages/manifest.json` · `GraphicsSettings.asset` · 2026-11-06 |
| 대상 플랫폼 | PC (Windows) | 기획서 1-1 · 사용자 답변 · 2026-11-06 |
| 입력 시스템 | Input System (New), Active Input Handling: Input System Package (New) | `ProjectSettings.asset` · 사용자 답변 · 2026-11-06 |
| 브랜치 구성 | `main` / `main-dev` / 작업 브랜치 (`종류/이름/작업명`, `main-dev`에서 생성) | Git 컨벤션 · 원격 저장소 · 사용자 답변 · 2026-11-06 |
| C# 언어·API 호환 수준 | API 호환 수준 .NET Standard 2.1, 언어 버전은 Unity 기본값 | `ProjectSettings.asset` (`apiCompatibilityLevel: 6`) · 2026-11-06 |
| 프로젝트 루트·namespace·어셈블리 경계 | 스크립트 루트 `Assets/Scripts/`, namespace 사용 안 함 (Unity · 패키지와 같은 클래스 이름 금지), Assembly Definition 분리 안 함 | 사용자 답변 · 2026-11-06 |
| 기존 서식 설정·줄바꿈·인코딩 | `.editorconfig` 없음. 새 파일은 UTF-8, 줄바꿈 CRLF | 사용자 답변 · 2026-11-06 |
| 필수 패키지·버전 | URP 17.3.0, Input System 1.20.0, Cinemachine 3.1.7, Behavior 1.0.17, Timeline 1.8.10, UniTask 2.5.11 (git `a77a1b4`), 조이콘 `com.slenkerlab.switchcontrollerinput` (git `ba8fa4b`, schlenger 포크) | `Packages/manifest.json` · `packages-lock.json` · 2026-11-06 |
| GitHub 저장소 URL·연결 상태 | https://github.com/SquashPrince/Develrocket_mp15_HHH (`origin` 연결됨) | `git remote` · 2026-11-06 |
