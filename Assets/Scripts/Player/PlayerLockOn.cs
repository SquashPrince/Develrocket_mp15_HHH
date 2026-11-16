using UnityEngine;

// 플레이어의 록온 상태. 록온은 캐릭터가 아니라 플레이어가 고른 대상이라 파티에 하나만 둔다.
//   교체해도 같은 대상을 계속 록온하고, 록온 표시 · 록온 카메라도 하나만 본다.
// 록온 입력마다 록온과 해제를 번갈아 하고, 대상이 처치되거나 록온 사거리 밖으로 나가면 자동으로 해제한다.
// 록온할 대상 찾기 · 거리 · 가림 확인은 조작 중인 캐릭터를 기준으로 한다. 대상 찾기 규칙은 캐릭터의 타겟팅 기능을 그대로 쓴다.
// 록온 사거리 · 록온 표시 Prefab 과 록온 입력은 파티의 공통 설정에서 읽는다. 록온 표시는 시작할 때 Prefab 으로 만든다.
public class PlayerLockOn : MonoBehaviour
{
    [Header("록온 표시")]
    [Tooltip("대상에 록온 표시 위치가 없을 때, 대상 위치에서 이만큼 위에 띄운다")]
    [SerializeField] private float _markerHeight = 2.5f;

    private PlayerParty _party;
    private Transform _lockOnMarker;
    private Collider _lockTargetCollider;
    private IEnemyTargetable _lockTarget;
    private bool _isLockTargetBlocked;

    public IEnemyTargetable LockTarget => _lockTarget;
    // 록온 대상이 벽에 가려져 있는지. 가려져도 록온은 유지하며, 록온 카메라가 구도를 바꾸는 데 쓴다.
    public bool IsLockTargetBlocked => _isLockTargetBlocked;
    private CommonCharacterSettings Common => _party.Common;
    private PlayerTargeting Targeting => _party.Controlled.Targeting;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateLockOn();

    // 록온 입력마다 록온과 해제를 번갈아 하고, 록온 중이면 거리 확인 · 가림 확인 · 표시 위치 갱신을 한다.
    // 록온 사거리 안에 적이 없으면 록온 입력은 아무 일도 하지 않는다.
    private void UpdateLockOn()
    {
        if(Common.LockOnAction.action.WasPressedThisFrame())
        {
            if(_lockTarget != null) SetLockTarget(null);
            else SetLockTarget(Targeting.FindTarget(Common.LockOnRange, true));
        }

        if(_lockTarget == null) return;

        // 록온 대상이 처치되었거나 조작 중인 캐릭터에서 록온 사거리 밖으로 나가면 록온을 푼다.
        Vector3 toTarget = _lockTarget.transform.position - _party.Controlled.transform.position;

        if(!_lockTarget.IsAlive || toTarget.sqrMagnitude > Common.LockOnRange * Common.LockOnRange)
        {
            SetLockTarget(null);
            return;
        }

        // 가림 판정은 대상 찾기와 같게 콜라이더 중심을 쓴다. 대상의 위치(발밑일 수 있음)를 쓰면 바닥에 막혀 항상 가려진 것으로 판정될 수 있다.
        _isLockTargetBlocked = Targeting.IsBlocked(_lockTargetCollider.bounds.center);

        // 대상이 알려주는 록온 표시 위치(머리 · 가슴 등)가 있으면 그 위치를 따라가고, 없으면 기본 높이에 띄운다.
        Transform lockOnPoint = _lockTarget.LockOnPoint;
        _lockOnMarker.position = lockOnPoint != null ? lockOnPoint.position : _lockTarget.transform.position + Vector3.up * _markerHeight;
    }

    private void SetLockTarget(IEnemyTargetable target)
    {
        _lockTarget = target;
        _lockTargetCollider = target != null ? target.transform.GetComponentInChildren<Collider>() : null;
        _isLockTargetBlocked = false;
        _lockOnMarker.gameObject.SetActive(target != null);
    }

    private void CacheComponent()
    {
        _party = GetComponent<PlayerParty>();
    }

    // 록온 표시를 공통 설정의 Prefab 으로 만들고 숨겨 둔다.
    private void Init()
    {
        _lockOnMarker = Instantiate(Common.LockOnMarkerPrefab).transform;
        SetLockTarget(null);
    }
}
