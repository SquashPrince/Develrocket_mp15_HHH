# Hollow Horizon Hunter

<img width="1577" height="884" alt="image" src="https://github.com/user-attachments/assets/5041b656-6214-4f74-8811-af797da08a68" />

> Unity 6 · URP 기반 3D 스타일리쉬 액션 게임 · 개인 프로젝트


### 시놉시스

***어느 날 공허**(HOLLOW)* ***속에서 나타난 몬스터들에 의해 폐허가 된 도시.***

***가까스로 살아남은 그들은 과거의 영광**(HORIZON)**를 되찾기 위해***

***몬스터를 사냥하러**(HUNTER) **다니고 있다.***

---

<details>
  <summary>플레이 영상</summary>
  
  [[Youtube 링크]](https://www.youtube.com/watch?app=desktop&v=wdAf5GuQr1U&feature=youtu.be)
  
</details>


## 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 개발 기간 | 2026.11.06 ~ 2026.11.24 (19일) |
| 개발 인원 | 1인 |
| 장르 | 3D 스타일리쉬 액션 (어반 판타지) |
| 플랫폼 | PC (Windows) · 키보드/마우스, 게임패드 |
| 엔진 | Unity 6000.3.9f1 · URP |
| 레퍼런스 | 젠레스 존 제로 (주), 데빌 메이 크라이 · Hi-Fi Rush · Dead as Disco (보조) |

## 기술 스택

- **Engine** : Unity 6 (URP)
- **Input Type** : Input System
- **Used Packages** : Cinemachine · Unity Behavior · Timeline · UniTask
- **Tools** : Git / GitHub, Claude Code, Codex

## AI 활용 방식

- **Claude** : 기획 · 구조 설계 검토, Codex 작업 지시서 작성, Issue · PR · 문서 작성
- **Codex** : 지시서 기반 구현, UI 이미지 생성
- 코드 컨벤션 · 작업 흐름 · 승인 규칙을 **에이전트 규칙 문서**로 관리
- 컨벤션 내용을 고정하지 않고 프로젝트에 맞게 계속 최적화
- 작업 시간 · AI 수정 사례 · 규칙 변경 이력을 기록 지시

## 주요 구현 내용

### 플레이어
- **상태 머신 기반 행동 제어** : 이동 · 공격 · 대시 · 패링 · 교체 · 필살기 · 피격 등을 상태 단위로 분리
- **데이터 기반 액션 설계** : 공격 · 스킬 · 캐릭터 설정을 ScriptableObject(ActionData · CharacterData)로 분리해 코드 수정 없이 조정
- **애니메이션 이벤트 연동** : 클립 이벤트로 타격 구간(HitStart/HitEnd) · 다단 히트 · 연계 시점을 판정
- **3인 파티 태그 액션** : 교체 시 나가는 캐릭터가 기술을 끝까지 수행, 교체 키로 적의 예고 공격을 받아치는 패링
- **콤보 스킬 연계** : 그로기 상태의 적에게 강타 적중 시 캐릭터를 골라 연계 공격을 이어받는 구조
- **완벽 회피** : 적의 시간만 느리게 하는 개별 시간 스케일로 회피 반격 구간 구현

### 적 AI
- **Unity Behavior** 비헤이비어 트리와 커스텀 Action/Condition 노드로 추적 · 거리 유지 · 원거리 공격 패턴 구성
- 피격 · 경직 · 그로기 · 패링 반응을 **인터페이스로 분리**해 일반 적과 보스가 필요한 반응만 구현
- 공격 패턴을 ScriptableObject 로 정의 (단계별 판정 · 회전 속도 · 패링 가능 구간)

### 전투 연출 · 타격감
- 히트스톱 · 히트렉 · 슬로모션, Cinemachine 기반 카메라 임팩트 · 샷 연출 · 록온 카메라
- 시간이 멈춘 순간에도 실제 속도로 재생되는 이펙트, **오브젝트 풀링**으로 이펙트 재사용
- 대미지 숫자, 그로기 · 완벽 회피 화면 효과, 상황별 효과음 · 배경음 크로스페이드

### 게임 흐름 · 시스템
- 구역 진입 → 일반 적 웨이브 → 보스전 → 결과 등급으로 이어지는 진행 구조
- **Bridson 알고리즘**(Poisson Disk Sampling) 기반 적 스폰 위치 분산
- 점수 · 등급 계산, 최고 기록 · 설정 JSON 저장
- 씬 전환 간 유지되는 공용 UI(설정 · 볼륨 · 일시정지), 키 재설정 · 감도 설정
- UniTask 기반 비동기 처리, 이벤트 기반 시스템 간 통신
## 문서

- 기술 문서 : [`Docs/Technical/`](Docs/Technical/) (플레이어 · 적 · 전투 · UI · 사운드 · 게임 흐름)
- 기획서 : [`Docs/Planning/`](Docs/Planning/)
