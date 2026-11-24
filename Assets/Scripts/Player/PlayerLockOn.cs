using UnityEngine;

// 플레이어의 록온 상태. 캐릭터가 아니라 플레이어가 고른 대상이라 파티에 하나만 두어, 교체해도 같은 대상을 이어 록온한다.
// 거리 · 가림 확인과 대상 찾기는 조작 중인 캐릭터를 기준으로 한다.
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
    // 가려져도 록온은 유지하고, 록온 카메라가 구도를 바꾸는 데 쓴다.
    public bool IsLockTargetBlocked => _isLockTargetBlocked;
    private CommonCharacterSettings Common => _party.Common;
    private PlayerTargeting Targeting => _party.Controlled.Targeting;

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void Update() => UpdateLockOn();

    // 록온 입력마다 록온과 해제를 번갈아 한다.
    private void UpdateLockOn()
    {
        if(!_party.IsInputBlocked && Common.LockOnAction.action.WasPressedThisFrame())
        {
            if(_lockTarget != null) SetLockTarget(null);
            else SetLockTarget(Targeting.FindTarget(Common.LockOnRange, true));
        }

        if(_lockTarget == null) return;

        // 처치되면 다음 대상으로 옮기고, 없으면 풀린다.
        if(!_lockTarget.IsAlive)
        {
            SetLockTarget(Targeting.FindTarget(Common.LockOnRange, true));

            if(_lockTarget == null) return;
        }

        Vector3 toTarget = _lockTarget.transform.position - _party.Controlled.transform.position;

        if(toTarget.sqrMagnitude > Common.LockOnRange * Common.LockOnRange)
        {
            SetLockTarget(null);
            return;
        }

        // 대상 위치(발밑)를 쓰면 바닥에 막혀 항상 가려진 것으로 판정될 수 있어 콜라이더 중심을 쓴다.
        _isLockTargetBlocked = Targeting.IsBlocked(_lockTargetCollider.bounds.center);

        Transform lockOnPoint = _lockTarget.LockOnPoint;
        _lockOnMarker.position = lockOnPoint != null ? lockOnPoint.position : _lockTarget.transform.position + Vector3.up * _markerHeight;
    }

    // 패링을 시작할 때처럼 록온 입력 없이 정한 대상으로 록온을 옮긴다.
    public void LockOnTo(IEnemyTargetable target)
    {
        if(target == _lockTarget) return;

        SetLockTarget(target);
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

    private void Init()
    {
        _lockOnMarker = Instantiate(Common.LockOnMarkerPrefab).transform;
        SetLockTarget(null);
    }
}
