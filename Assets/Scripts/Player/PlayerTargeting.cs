using UnityEngine;

// 공격 · 록온 대상을 찾고 록온 상태를 관리한다.
// 록온 입력으로 록온 사거리 안의 대상을 고정하거나 해제하고, 대상이 록온 사거리 밖으로 나가면 자동으로 해제한다.
// 범위 안의 플레이어 앞쪽 적 중 카메라 중앙 레이와의 수직 거리가 가장 짧은 적을 대상으로 고른다.
// 같은 화면 위치라면 가까운 적일수록 레이와의 거리가 짧아 근접 공격 대상으로 우선된다.
// 사거리 · 가림 판정 값과 록온 표시 Prefab 은 모든 캐릭터가 같이 쓰는 공통 설정에서 읽는다. 록온 표시는 시작할 때 Prefab 으로 만든다.
[RequireComponent(typeof(PlayerController), typeof(PlayerInputReader))]
public class PlayerTargeting : MonoBehaviour
{
    // 한 번의 대상 찾기에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_CANDIDATE_COLLIDERS = 32;

    [Header("참조")]
    [Tooltip("화면 중앙 레이의 기준이 되는 카메라 (Main Camera)")]
    [SerializeField] private Transform _cameraTransform;

    [Header("록온 표시")]
    [Tooltip("대상에 록온 표시 위치가 없을 때, 대상 위치에서 이만큼 위에 띄운다")]
    [SerializeField] private float _markerHeight = 2.5f;

    private readonly Collider[] _candidateColliders = new Collider[MAX_CANDIDATE_COLLIDERS];
    private PlayerController _controller;
    private PlayerInputReader _input;
    private Camera _camera;
    private Transform _lockOnMarker;
    private Collider _lockTargetCollider;
    private IEnemyTargetable _lockTarget;
    private bool _isLockTargetBlocked;

    public IEnemyTargetable LockTarget => _lockTarget;
    // 록온 대상이 벽에 가려져 있는지. 가려져도 록온은 유지하며, 록온 카메라가 구도를 바꾸는 데 쓴다.
    public bool IsLockTargetBlocked => _isLockTargetBlocked;
    public float AttackRange => Common.AttackRange;
    public float LockOnRange => Common.LockOnRange;
    private CommonCharacterSettings Common => _controller.Common;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateLockOn();
    private void OnDrawGizmosSelected() => DrawRange();

    // 공격 대상을 고른다. 록온 대상 → 우선 대상 → range 안의 자동 조준 대상 순서다. 없으면 null 이다.
    // 공격 · 필살기 · 그로기 테스트처럼 "누구를 노릴지"는 모두 이 순서를 따른다.
    // priorityTarget : 록온 중이 아닐 때 자동 조준보다 먼저 노릴 대상. 패링 추격은 패링한 적을 넘긴다.
    public IEnemyTargetable FindAttackTarget(float range, IEnemyTargetable priorityTarget = null)
    {
        if(_lockTarget != null) return _lockTarget;
        if(priorityTarget != null) return priorityTarget;

        return FindTarget(range);
    }

    // 플레이어 기준 range 안의 적을 찾아 돌려준다. 없으면 null 이다.
    // 플레이어 앞쪽(카메라가 보는 방향 기준)의 적 중 카메라 중앙 레이와 가장 가까운 적을 우선한다.
    // 앞쪽에 적이 없을 때만 등 뒤의 적 중 플레이어와 가장 가까운 적을 고른다.
    // 등 뒤의 적은 카메라와 플레이어 사이에 있어 레이와의 거리가 거의 0 이 되므로, 같은 기준으로 비교하면 앞의 적보다 먼저 뽑히기 때문이다.
    // 플레이어와 적 사이가 벽으로 막힌 적은 고르지 않는다. 막힌 적에게 다가가면 벽에 박히기 때문이다.
    // onlyOnScreen : true 면 중심이 화면 안에 보이는 적만 고른다. 록온에 쓴다.
    public IEnemyTargetable FindTarget(float range, bool onlyOnScreen = false)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, range, _candidateColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Vector3 rayOrigin = _cameraTransform.position;
        Vector3 rayDirection = _cameraTransform.forward;
        IEnemyTargetable frontEnemy = null;
        IEnemyTargetable backEnemy = null;
        float frontSqrDistance = float.MaxValue;
        float backSqrDistance = float.MaxValue;

        for(int i = 0; i < count; i++)
        {
            IEnemyTargetable enemy = _candidateColliders[i].GetComponentInParent<IEnemyTargetable>();

            if(enemy == null || !enemy.IsAlive) continue;

            Vector3 enemyCenter = _candidateColliders[i].bounds.center;

            if(onlyOnScreen && !IsOnScreen(enemyCenter)) continue;

            if(IsBlocked(enemyCenter)) continue;

            // 크기 비교만 하므로 제곱근 없이 거리의 제곱으로 비교한다.
            if(Vector3.Dot(rayDirection, enemyCenter - transform.position) < 0f)
            {
                float sqrDistanceToPlayer = (enemyCenter - transform.position).sqrMagnitude;

                if(sqrDistanceToPlayer >= backSqrDistance) continue;

                backSqrDistance = sqrDistanceToPlayer;
                backEnemy = enemy;
                continue;
            }

            // 레이 방향(길이 1)과의 외적 크기가 레이까지의 수직 거리다.
            float sqrDistanceToRay = Vector3.Cross(rayDirection, enemyCenter - rayOrigin).sqrMagnitude;

            if(sqrDistanceToRay >= frontSqrDistance) continue;

            frontSqrDistance = sqrDistanceToRay;
            frontEnemy = enemy;
        }

        return frontEnemy != null ? frontEnemy : backEnemy;
    }

    // 록온 입력마다 록온과 해제를 번갈아 하고, 록온 중이면 거리 확인 · 가림 확인 · 표시 위치 갱신을 한다.
    // 록온 사거리 안에 적이 없으면 록온 입력은 아무 일도 하지 않는다.
    private void UpdateLockOn()
    {
        if(_input.WasLockOnPressed)
        {
            if(_lockTarget != null) SetLockTarget(null);
            else SetLockTarget(FindTarget(LockOnRange, true));
        }

        if(_lockTarget == null) return;

        // 록온 대상이 처치되었거나 록온 사거리 밖으로 나가면 록온을 푼다.
        if(!_lockTarget.IsAlive || (_lockTarget.transform.position - transform.position).sqrMagnitude > LockOnRange * LockOnRange)
        {
            SetLockTarget(null);
            return;
        }

        // 가림 판정은 대상 찾기와 같게 콜라이더 중심을 쓴다. 대상의 위치(발밑일 수 있음)를 쓰면 바닥에 막혀 항상 가려진 것으로 판정될 수 있다.
        _isLockTargetBlocked = IsBlocked(_lockTargetCollider.bounds.center);
        // 대상이 알려주는 록온 표시 위치(머리 · 가슴 등)가 있으면 그 위치를 따라가고, 없으면 기본 높이에 띄운다.
        Transform lockOnPoint = _lockTarget.LockOnPoint;
        _lockOnMarker.position = lockOnPoint != null ? lockOnPoint.position : _lockTarget.transform.position + Vector3.up * _markerHeight;
    }

    // 화면 좌표(Viewport)는 화면 왼쪽 아래가 (0, 0), 오른쪽 위가 (1, 1) 이고, z 는 카메라 앞쪽 거리다.
    // z 가 0 이하면 카메라 뒤에 있다는 뜻이다.
    private bool IsOnScreen(Vector3 position)
    {
        Vector3 viewport = _camera.WorldToViewportPoint(position);

        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // 플레이어 가슴 높이에서 적 중심까지 선을 그어 장애물 Layer 에 닿으면 막힌 것으로 본다.
    private bool IsBlocked(Vector3 position)
    {
        Vector3 sightOrigin = transform.position + Vector3.up * Common.SightHeight;

        return Physics.Linecast(sightOrigin, position, Common.ObstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private void SetLockTarget(IEnemyTargetable target)
    {
        _lockTarget = target;
        _lockTargetCollider = target != null ? target.transform.GetComponentInChildren<Collider>() : null;
        _isLockTargetBlocked = false;
        _lockOnMarker.gameObject.SetActive(target != null);
    }

    // Scene 창에서 플레이어를 선택하면 공격 사거리(노랑)와 록온 사거리(하늘)를 보여준다.
    // 플레이 중에는 지금 공격하면 선택될 대상까지 선을 긋는다.
    // 에디터에서는 파티가 캐릭터에 자신을 알려 주기 전이므로, 실행 중이 아니면 씬의 파티를 직접 찾는다. 파티나 공통 설정이 없으면 그리지 않는다.
    private void DrawRange()
    {
        PlayerParty party = Application.isPlaying ? GetComponent<PlayerController>().Party : FindAnyObjectByType<PlayerParty>();

        if(party == null || party.Common == null) return;

        CommonCharacterSettings common = party.Common;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, common.AttackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, common.LockOnRange);

        if(!Application.isPlaying) return;

        IEnemyTargetable target = FindTarget(common.AttackRange);

        if(target == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.transform.position);
    }

    private void CacheComponent()
    {
        _camera = _cameraTransform.GetComponent<Camera>();
        _controller = GetComponent<PlayerController>();
        _input = GetComponent<PlayerInputReader>();
    }

    // 록온 표시를 공통 설정의 Prefab 으로 만들고 숨겨 둔다.
    private void Init()
    {
        _lockOnMarker = Instantiate(Common.LockOnMarkerPrefab).transform;
        SetLockTarget(null);
    }
}
