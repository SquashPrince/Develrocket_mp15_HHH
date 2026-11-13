using UnityEngine;
using UnityEngine.InputSystem;

// 캐릭터가 바뀌어도 같은 규칙 값. 캐릭터마다 다른 값은 캐릭터 데이터(CharacterData)에, 모든 캐릭터가 같이 쓰는 값은 여기에 둔다.
// 에셋 하나를 모든 캐릭터가 함께 쓴다. 지금(1인)은 컨트롤러가 들고 있고, 다른 기능은 컨트롤러에서 꺼내 쓴다.
// 파티 전투에서는 캐릭터 목록을 가진 파티 기능이 이 에셋을 들고 각 캐릭터에 넘겨준다. 다른 기능은 그대로 컨트롤러에서 꺼내 쓴다.
// 실행 중 계산한 값(예: 파티에서 가장 큰 캐릭터 기준 값)은 이 에셋에 쓰지 않는다. 에디터에서 실행 중 바꾼 에셋 값은 실행을 멈춰도 남기 때문이다.
// 씬 안의 오브젝트(카메라 등)는 에셋이 참조할 수 없으므로 각 기능에 연결한다.
[CreateAssetMenu(fileName = "NewCommonCharacterSettings", menuName = "HHH/Common Character Settings")]
public class CommonCharacterSettings : ScriptableObject
{
    [Header("이동")]
    [SerializeField] private float _gravity = -20f;

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
    [Tooltip("순간 이동할 때 대상 몸 표면과 플레이어 몸 표면 사이를 이 간격만큼 띄운다")]
    [SerializeField, Min(0f)] private float _teleportDistance = 1.5f;
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

    [Header("필살기")]
    [Tooltip("발동할 때 이 거리 안의 대상(록온 대상 우선, 없으면 자동 조준 대상)을 찾는다. 필살기 액션에 순간 이동 옵션이 있으면 그 대상 앞으로 순간 이동한다")]
    [SerializeField, Min(0f)] private float _ultimateTargetSearchRange = 10f;

    [Header("입력 : 이동 · 행동")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _dashAction;
    [SerializeField] private InputActionReference _attackAction;
    [SerializeField] private InputActionReference _skillAction;
    [SerializeField] private InputActionReference _ultimateAction;

    [Header("입력 : 패링 · 교체")]
    [SerializeField] private InputActionReference _parryPreviousAction;
    [SerializeField] private InputActionReference _parryNextAction;

    [Header("입력 : 타겟팅")]
    [SerializeField] private InputActionReference _lockOnAction;

    [Header("입력 : 테스트")]
    [Tooltip("[코어 전투 테스트] 그로기 테스트 입력. 그로기 게이지를 만들면 지운다")]
    [SerializeField] private InputActionReference _groggyDebugAction;

    public float Gravity => _gravity;
    public float AttackRange => _attackRange;
    public float LockOnRange => _lockOnRange;
    public LayerMask ObstacleLayers => _obstacleLayers;
    public float SightHeight => _sightHeight;
    public GameObject LockOnMarkerPrefab => _lockOnMarkerPrefab;
    public float TeleportDistance => _teleportDistance;
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
    public float UltimateTargetSearchRange => _ultimateTargetSearchRange;
    public InputActionReference MoveAction => _moveAction;
    public InputActionReference DashAction => _dashAction;
    public InputActionReference AttackAction => _attackAction;
    public InputActionReference SkillAction => _skillAction;
    public InputActionReference UltimateAction => _ultimateAction;
    public InputActionReference ParryPreviousAction => _parryPreviousAction;
    public InputActionReference ParryNextAction => _parryNextAction;
    public InputActionReference LockOnAction => _lockOnAction;
    public InputActionReference GroggyDebugAction => _groggyDebugAction;
}
