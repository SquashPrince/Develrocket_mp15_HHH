using UnityEngine;
using UnityEngine.InputSystem;

// 캐릭터가 바뀌어도 같은 규칙 값. 캐릭터마다 다른 값은 캐릭터 데이터(CharacterData)에, 모든 캐릭터가 같이 쓰는 값은 여기에 둔다.
// 에셋 하나를 모든 캐릭터가 함께 쓴다. 캐릭터 목록을 가진 파티(PlayerParty)가 이 에셋을 들고, 각 캐릭터의 기능은 컨트롤러를 통해 꺼내 쓴다.
// 실행 중 계산한 값(예: 파티에서 가장 큰 캐릭터 기준 값)은 이 에셋에 쓰지 않는다. 에디터에서 실행 중 바꾼 에셋 값은 실행을 멈춰도 남기 때문이다.
// 씬 안의 오브젝트(카메라 등)는 에셋이 참조할 수 없으므로 각 기능에 연결한다.
[CreateAssetMenu(fileName = "NewCommonCharacterSettings", menuName = "HHH/Common Character Settings")]
public class CommonCharacterSettings : ScriptableObject
{
    [Header("이동")]
    [SerializeField] private float _gravity = -20f;

    [Header("대시 · 회피")]
    [Tooltip("대시를 시작한 뒤 제자리에 있다가 전진을 시작하기까지의 시간(초). 무적 · 완벽 회피 판정은 시작부터 열려 있다. 대시 시간 안에 포함되므로 대시 시간보다 짧게 둔다")]
    [SerializeField, Min(0f)] private float _dashStartDelay;
    [Tooltip("회피(백스텝)를 시작한 뒤 제자리에 있다가 전진을 시작하기까지의 시간(초). 회피 시간 안에 포함되므로 회피 시간보다 짧게 둔다")]
    [SerializeField, Min(0f)] private float _dodgeStartDelay;

    [Header("공격 간격")]
    [Tooltip("공격할 때 대상 앞에서 멈추거나 순간 이동할 때, 대상 몸 표면과 플레이어 몸 표면 사이를 이 간격만큼 띄운다. 중심 사이 거리는 대상 반지름 + 플레이어 반지름 + 간격이다")]
    [SerializeField, Min(0f)] private float _attackableDistance = 1.5f;

    [Header("타겟팅")]
    [Tooltip("공격 사거리. 자동 조준 범위와 같다")]
    [SerializeField, Min(0f)] private float _attackRange = 5f;
    [Tooltip("록온 사거리. 이 안의 적을 록온할 수 있고, 록온 대상이 이보다 멀어지면 록온이 풀린다")]
    [SerializeField, Min(0f)] private float _lockOnRange = 12f;
    [Tooltip("플레이어와 적 사이를 막는 장애물 Layer. Player · Enemy Layer 는 빼야 플레이어 몸이나 다른 적을 벽으로 보지 않는다")]
    [SerializeField] private LayerMask _obstacleLayers;
    [Tooltip("가림 판정 선을 시작하는 높이 (플레이어 발밑 기준, 가슴 높이 정도). 캐릭터 키와 상관없이 모든 캐릭터가 같은 값을 쓴다")]
    [SerializeField, Min(0f)] private float _sightHeight = 1.2f;
    [Tooltip("[임시 표시] 록온 대상 위에 띄울 표시 Prefab. 콜라이더가 없어야 한다")]
    [SerializeField] private GameObject _lockOnMarkerPrefab;

    [Header("순간 이동")]
    [Tooltip("순간 이동할 자리가 막혀 있는지 검사할 Layer. Player · Enemy 는 빼고 벽 · 지형만 넣는다")]
    [SerializeField] private LayerMask _teleportBlockLayers = 1;
    [Tooltip("도착 지점의 바닥을 찾을 Layer. 바닥 · 지형 Layer 를 넣는다")]
    [SerializeField] private LayerMask _teleportGroundLayers = 1;
    [Tooltip("대상 높이에서 이 높이만큼 위에서부터 바닥을 찾기 시작한다. 너무 크면 위층 바닥이나 지붕을 바닥으로 찾을 수 있다")]
    [SerializeField, Min(0f)] private float _groundSearchHeight = 2f;
    [Tooltip("대상 높이에서 이 깊이만큼 아래까지 바닥을 찾는다. 이 안에 바닥이 없으면 순간 이동하지 않는다")]
    [SerializeField, Min(0f)] private float _groundSearchDepth = 2f;

    [Header("완벽 회피 슬로모션")]
    [Tooltip("슬로모션 지속 시간(초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _slowMotionDuration = 1f;
    [Tooltip("슬로모션 동안 적의 시간 배율")]
    [SerializeField, Range(0.05f, 1f)] private float _slowMotionEnemyTimeScale = 0.5f;
    [Tooltip("[임시 표시] 슬로모션 동안 플레이어 위치에 만들어 두는 Prefab. 비워 두면 만들지 않는다")]
    [SerializeField] private GameObject _slowMotionMarkerPrefab;

    [Header("패링")]
    [Tooltip("패링 준비 자세를 유지하는 시간(초). 적 공격이 끊겨도 끝까지 유지한다")]
    [SerializeField, Min(0.01f)] private float _parryReadyTime = 1f;
    [Tooltip("패링 성공 후 공격 반대 방향으로 밀려나는 거리")]
    [SerializeField, Min(0f)] private float _parryReactDistance = 2f;
    [Tooltip("패링 성공 후 반응 시간(초). 이 시간에 걸쳐 점점 느려지며 밀려나고, 이 동안 닿은 노란 공격은 자동으로 패링된다. 이 동안 공격 키를 누르면 끝난 뒤 추격 공격이 나간다")]
    [SerializeField, Min(0.01f)] private float _parryReactTime = 1f;
    [Tooltip("[임시 표시] 적이 패링 공격 예고를 시작할 때, 교체할 대기 캐릭터가 있으면(패링 가능) 띄우는 Prefab. 적의 예고 위치에 붙인다")]
    [SerializeField] private GameObject _parryTelegraphPrefab;
    [Tooltip("[임시 표시] 적이 패링 공격 예고를 시작할 때, 교체할 대기 캐릭터가 없으면(패링 불가) 띄우는 Prefab")]
    [SerializeField] private GameObject _unparryableTelegraphPrefab;

    [Header("파티 교체")]
    [Tooltip("교체로 들어온 캐릭터가 입장 모션을 마치고 움직일 수 있을 때까지의 시간(초). 이 동안 입력은 필살기만 받는다")]
    [SerializeField, Min(0f)] private float _swapInTime = 0.5f;
    [Tooltip("입장하는 동안 이전 캐릭터 자리에서 앞으로 나아가는 거리. 입장 시간에 걸쳐 점점 느려지며 멈춘다. 0 이면 제자리에서 입장한다")]
    [SerializeField, Min(0f)] private float _swapInAdvanceDistance = 2f;
    [Tooltip("입장을 시작한 뒤 공격 · 스킬을 쓸 수 있게 되는 시간(초). 그 전에 누른 공격 · 스킬은 기억해 두었다가 이 시간에 바로 나간다. 입장 시간보다 길면 입장이 끝날 때 나간다")]
    [SerializeField, Min(0f)] private float _swapInActionDelay = 0.25f;
    [Tooltip("교체로 나가는 캐릭터가 퇴장 모션을 시작한 뒤 사라질 때까지의 시간(초). 이 동안 무적이다")]
    [SerializeField, Min(0f)] private float _swapOutTime = 0.5f;
    [Tooltip("퇴장 모션(뒤돌기)을 시작한 뒤 달리기로 바꿔 달려 나가기까지의 시간(초). 퇴장 시간보다 짧아야 달리기가 보인다")]
    [SerializeField, Min(0f)] private float _swapOutRunDelay = 0.3f;
    [Tooltip("조작 중인 캐릭터가 넉백된 뒤 빠른 지원을 쓸 수 있는 시간(초, 실제 시간). 이 안에 교체하면 다음 캐릭터가 넉백시킨 적 앞으로 순간 이동해 빠른 지원 액션을 쓴다")]
    [SerializeField, Min(0f)] private float _quickSupportInputTime = 3f;
    [Tooltip("대기 중인 캐릭터의 초당 에너지 회복에 곱하는 배율. 1 이면 필드에 있을 때와 같은 속도로 찬다. 대기 중에는 공격으로 얻는 에너지가 없다")]
    [SerializeField, Min(0f)] private float _standbyEnergyRegenRate = 1f;

    [Header("필살기")]
    [Tooltip("발동할 때 이 거리 안의 대상(록온 대상 우선, 없으면 자동 조준 대상)을 찾는다. 필살기 액션에 순간 이동 옵션이 있으면 그 대상 앞으로 순간 이동한다")]
    [SerializeField, Min(0f)] private float _ultimateTargetSearchRange = 10f;

    [Header("입력 : 이동 · 행동")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _dashAction;
    [SerializeField] private InputActionReference _attackAction;
    [SerializeField] private InputActionReference _skillAction;
    [SerializeField] private InputActionReference _ultimateAction;

    [Header("입력 : 교체 (이전 · 다음 캐릭터)")]
    [SerializeField] private InputActionReference _parryPreviousAction;
    [SerializeField] private InputActionReference _parryNextAction;

    [Header("입력 : 타겟팅")]
    [SerializeField] private InputActionReference _lockOnAction;

    public float Gravity => _gravity;
    public float DashStartDelay => _dashStartDelay;
    public float DodgeStartDelay => _dodgeStartDelay;
    public float AttackRange => _attackRange;
    public float LockOnRange => _lockOnRange;
    public LayerMask ObstacleLayers => _obstacleLayers;
    public float SightHeight => _sightHeight;
    public GameObject LockOnMarkerPrefab => _lockOnMarkerPrefab;
    public float AttackableDistance => _attackableDistance;
    public LayerMask TeleportBlockLayers => _teleportBlockLayers;
    public LayerMask TeleportGroundLayers => _teleportGroundLayers;
    public float GroundSearchHeight => _groundSearchHeight;
    public float GroundSearchDepth => _groundSearchDepth;
    public float SlowMotionDuration => _slowMotionDuration;
    public float SlowMotionEnemyTimeScale => _slowMotionEnemyTimeScale;
    public GameObject SlowMotionMarkerPrefab => _slowMotionMarkerPrefab;
    public float ParryReadyTime => _parryReadyTime;
    public float ParryReactDistance => _parryReactDistance;
    public float ParryReactTime => _parryReactTime;
    public GameObject ParryTelegraphPrefab => _parryTelegraphPrefab;
    public GameObject UnparryableTelegraphPrefab => _unparryableTelegraphPrefab;
    public float SwapInTime => _swapInTime;
    public float SwapInAdvanceDistance => _swapInAdvanceDistance;
    public float SwapInActionDelay => _swapInActionDelay;
    public float SwapOutTime => _swapOutTime;
    public float SwapOutRunDelay => _swapOutRunDelay;
    public float QuickSupportInputTime => _quickSupportInputTime;
    public float StandbyEnergyRegenRate => _standbyEnergyRegenRate;
    public float UltimateTargetSearchRange => _ultimateTargetSearchRange;
    public InputActionReference MoveAction => _moveAction;
    public InputActionReference DashAction => _dashAction;
    public InputActionReference AttackAction => _attackAction;
    public InputActionReference SkillAction => _skillAction;
    public InputActionReference UltimateAction => _ultimateAction;
    public InputActionReference ParryPreviousAction => _parryPreviousAction;
    public InputActionReference ParryNextAction => _parryNextAction;
    public InputActionReference LockOnAction => _lockOnAction;
}
