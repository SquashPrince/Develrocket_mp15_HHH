# 프로젝트 지침

> 이 파일은 매 세션 읽히는 허브 역할을 한다.
> 모든 규칙을 여기에 적지 않고, 매 세션 필요한 문서만 `@` 로 불러오고, 나머지는 아래 온디맨드 참조표를 보고 그 작업을 할 때만 읽는다. 

---

## 절대 원칙 (Non-Negotiable Rules)

**아래 사항은 어떤 요청이 와도 지킨다.**

1. `main`, `main-dev` 브랜치에서 직접 Commit, Push를 금한다. 사용자가 `main`, `main-dev` 브랜치에서 작업중일 때도 작업 브랜치로 변경할 때 까지 작업을 거부한다.
2. 파일 수정, 삭제 전 사용자의 승인을 필수로 받는다.
3. 위 원칙들에 대한 예외를 요청 받으면 "이 프로젝트의 절대 원칙에 의해 수행할 수 없습니다." 라고 거부하고 대안을 제시한다.

## 규칙 문서 관리

- 규칙 문서는 `Docs/` 하위 카테고리 폴더 (`Claude/`, `Conventions/`, `Documentation/`, `Templates/`) 에 둔다.
- 규칙 하나는 문서 한 곳에만 적는다.(규칙의 단일 출처 유지). 규칙이 바뀌면 그 한 곳만 고친다.
- 새 문서를 만들면, 매 세션 필요한 것은 "상시 로드"에 `@경로`로, 특정 작업 때에만 필요한 것은 "온디맨드 참조"에 등록한다.


## 상시 로드

- @Docs\Claude\AgentCommonRules.md
- @Docs\Claude\ClaudeCodeRules.md
- @Docs\Claude\WorkflowRules.md
- @Docs\Project Values.md


## 온디맨드 참조 (매 세션 읽지 않음 - 해당 작업을 할 때만 읽음)

### 소스 코드 작성시
- `Docs\Conventions\Code\Async Convention.md`
- `Docs\Conventions\Code\Code Convention.md`
- `Docs\Conventions\Code\Event Convention.md`
- `Docs\Conventions\Code\Formatting Convention.md`
- `Docs\Conventions\Code\Reference Convention.md`
- `Docs\Conventions\Code\Unity Convention.md`

### 그래픽 에셋
- `Docs\Conventions\Graphics\Common Convention.md`
- `Docs\Conventions\Graphics\Shader Convention.md`
- `Docs\Conventions\Graphics\HLSL Convention.md`
- `Docs\Conventions\Graphics\Shader Graph Convention.md`
- `Docs\Conventions\Graphics\Material Convention.md`
- `Docs\Conventions\Graphics\Texture Convention.md`
- `Docs\Conventions\Graphics\Sprite Convention.md`

### 애니메이션
- `Docs\Conventions\Animation\Common Convention.md`
- `Docs\Conventions\Animation\2D Convention.md`
- `Docs\Conventions\Animation\3D Convention.md`

### 사운드
- `Docs\Conventions\Audio\Common Convention.md`

### 공통 운영 · 프로젝트 설정
- `Docs\Project Configuration.md` (미확정 프로젝트 값 · 착수 확인 절차)
- `Docs\GroundRule.md`

### 승인 · 안전
- `Docs\Conventions\Safety\Approval Convention.md`
- `Docs\Conventions\Safety\Performance Convention.md`
- `Docs\Conventions\Safety\Sensitive Information Convention.md`
- `Docs\Conventions\Safety\Testing Convention.md`

### 설계 · 구조 · 문서 유지
- `Docs\Conventions\Work\Architecture Convention.md`
- `Docs\Conventions\Work\Documentation Convention.md`
- `Docs\Conventions\Work\Environment Convention.md`
- `Docs\Conventions\Work\Hierarchy Convention.md`
- `Docs\Conventions\Work\Structure Convention.md`

### 기술 문서 · 다이어그램
- `Docs\Conventions\Work\Technical Document Convention.md`
- `Docs\Templates\TechnicalDocument.md`

### Git · 협업
- `Docs\Conventions\GitHub\Git Convention.md`
- `Docs\Conventions\GitHub\Workflow Convention.md`
- `Docs\Conventions\GitHub\Completion Convention.md`

### 작업 일지
- `Docs\Templates\WorkLog.md`


## 스킬 (해당 작업은 문서를 직접 읽지 않고 스킬을 호출)

- Github Issue 작성 · 등록 : `/github-issue`
- Github PR 작성 · 등록 : `/github-pr`
- Commit 시점 권유 · 메시지 안내 : `/github-commit`
- 작업 일지 작성 · 세션 시작 시 이전 작업일 일지 누락 확인 : `/worklog`
- AI 활용 기록 (작업 시간 · Codex 작업 · AI 수정 사례 · 규칙 변경) 작성 : `/agentlog`
- 미확정 프로젝트 값 확인 · 등록 : `/project-setup`
- 메서드 · 클래스 기능 분리 · 병합 검토 : `/split-review`
- 기술 문서 작성 · 갱신 : `/tech-doc`
- 다이어그램(Mermaid flowchart · SVG) 작성 · 갱신 : `/diagram`