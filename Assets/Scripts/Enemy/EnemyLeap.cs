using UnityEngine;
using UnityEngine.AI;

// 적 도약 · 돌진 공격의 이동. LeapStart ~ LeapEnd 이벤트 사이에 출발 위치에서 목표 지점으로 옮긴다.
// 진행도를 흐른 시간이 아닌 클립 재생 위치로 정해, 재생 속도가 바뀌어도 LeapEnd 순간에 목표 지점에 닿는다.
// 루트는 NavMesh 위치 맞춤을 따라야 하므로 도약 높이는 외형(Visual)만 띄운다.
// 자세한 흐름 : Docs/Technical/Enemy/EnemyLeap.md
[RequireComponent(typeof(NavMeshAgent), typeof(Collider), typeof(EnemyAnimation))]
public class EnemyLeap : MonoBehaviour
{
    [Tooltip("도약 중 띄울 외형(Visual)")]
    [SerializeField] private Transform _visual;
    [Tooltip("[임시] 목표 지점 바닥에 보여줄 표시 프리팹. 도약 · 돌진 중에만 켜진다. 나중에 이펙트로 바꾼다")]
    [SerializeField] private GameObject _landingMarkerPrefab;

    private NavMeshAgent _agent;
    private Collider _collider;
    private EnemyAnimation _animation;
    private GameObject _landingMarker;
    private Transform _target;
    // 내려온 뒤 겹침이 풀릴 때까지 충돌을 무시하는 대상
    private Collider _ignoredCollider;
    private Vector3 _startPosition;
    private Vector3 _landingPosition;
    private float _visualBaseHeight;
    private float _height;
    private float _overshootDistance;
    private int _stateHash;
    // 클립 시간 기준
    private float _startTime;
    private float _landTime;
    private float _clipLength;
    private bool _isLocked;

    public bool IsLeaping { get; private set; }

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void OnDisable()
    {
        Stop();
        RestoreCollision();
    }
    private void Update()
    {
        UpdateLeap();
        UpdateSeparation();
    }

    // 같은 클립에 LeapEnd 가 없으면 도착 시점을 알 수 없어 이동하지 않는다.
    // height : 0 이면 땅을 따라 돌진. 공중에 있는 동안은 콜라이더를 꺼 맞지 않게 한다.
    public void Begin(Transform target, AnimationEvent leapStartEvent, float height, float overshootDistance)
    {
        AnimationClip clip = leapStartEvent.animatorClipInfo.clip;
        float landTime = AnimationClipEvents.FindNextTime(clip, leapStartEvent.time, nameof(EnemyAnimationEventType.LeapEnd));

        if(landTime <= leapStartEvent.time)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[도약] {name} {clip.name} : LeapStart 뒤에 LeapEnd 이벤트가 없어 이동하지 않는다. 같은 클립의 착지 · 도착 프레임에 LeapEnd 이벤트를 넣는다");
#endif
            return;
        }

        _target = target;
        _height = height;
        _overshootDistance = overshootDistance;
        _stateHash = leapStartEvent.animatorStateInfo.shortNameHash;
        _startTime = leapStartEvent.time;
        _landTime = landTime;
        _clipLength = clip.length;
        _startPosition = transform.position;
        _isLocked = false;
        IsLeaping = true;

        if(_height > 0f) _collider.enabled = false;

        UpdateLandingPosition();
        SetMarkerActive(true);
    }

    // LeapLock 이 없으면 LeapEnd 까지 대상을 따라간다.
    public void Lock()
    {
        if(!IsLeaping || _isLocked) return;

        UpdateLandingPosition();
        _isLocked = true;
    }

    public void Land()
    {
        if(!IsLeaping) return;

        MoveTo(_landingPosition);
        Stop();
    }

    // 공격이 끊긴 경우 그 자리에 내려온다.
    public void Stop()
    {
        if(!IsLeaping) return;

        IsLeaping = false;
        SetVisualHeight(0f);
        SetMarkerActive(false);

        if(_height > 0f) EnableColliderAfterLanding();
    }

    // 겹친 채 충돌을 켜면 대상의 CharacterController 가 겹침을 풀며 바닥 아래로 밀려날 수 있어, 겹침이 풀릴 때까지 대상과의 충돌만 무시한다.
    // 공격 판정은 콜라이더로 찾으므로 그대로 맞는다.
    private void EnableColliderAfterLanding()
    {
        _collider.enabled = true;

        if(_target == null || !_target.TryGetComponent(out CharacterController targetCollider)) return;
        if(!IsOverlapping(targetCollider)) return;

        Physics.IgnoreCollision(_collider, targetCollider, true);
        _ignoredCollider = targetCollider;
    }

    private void UpdateSeparation()
    {
        if(_ignoredCollider != null && !IsOverlapping(_ignoredCollider)) RestoreCollision();
    }

    private void RestoreCollision()
    {
        if(_ignoredCollider == null) return;

        Physics.IgnoreCollision(_collider, _ignoredCollider, false);
        _ignoredCollider = null;
    }

    private bool IsOverlapping(Collider other)
    {
        return Physics.ComputePenetration(_collider, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out _, out _);
    }

    private void UpdateLeap()
    {
        if(!IsLeaping) return;
        if(!_animation.TryGetStateNormalizedTime(_stateHash, out float normalizedTime)) return;

        if(!_isLocked) UpdateLandingPosition();

        float clipTime = normalizedTime * _clipLength;
        float progress = Mathf.Clamp01((clipTime - _startTime) / (_landTime - _startTime));

        MoveTo(Vector3.Lerp(_startPosition, _landingPosition, progress));

        // 진행도 0 과 1 에서 0, 가운데에서 최고 높이가 되는 포물선
        SetVisualHeight(4f * _height * progress * (1f - progress));
    }

    // 대상 위치에서 지나치는 거리만큼 더 간 곳. NavMesh.Raycast 는 경계에 막히면 막힌 지점을, 아니면 도착 지점을 돌려준다.
    private void UpdateLandingPosition()
    {
        if(_target == null) return;

        Vector3 toTarget = HorizontalVector.Between(_startPosition, _target.position);

        Vector3 direction = toTarget == Vector3.zero ? transform.forward : toTarget.normalized;
        Vector3 destination = _target.position + direction * _overshootDistance;

        NavMesh.Raycast(_startPosition, destination, out NavMeshHit hit, _agent.areaMask);
        _landingPosition = hit.position;

        if(_landingMarker != null) _landingMarker.transform.position = _landingPosition;
    }

    // 이동 기능과 같은 방식으로 Agent 위치를 거쳐 NavMesh 위로 맞춘 위치를 실제 위치로 쓴다.
    private void MoveTo(Vector3 position)
    {
        _agent.nextPosition = position;
        transform.position = _agent.nextPosition;
    }

    private void SetVisualHeight(float height)
    {
        Vector3 localPosition = _visual.localPosition;
        localPosition.y = _visualBaseHeight + height;
        _visual.localPosition = localPosition;
    }

    private void SetMarkerActive(bool isActive)
    {
        if(_landingMarker != null) _landingMarker.SetActive(isActive);
    }

    private void CacheComponent()
    {
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider>();
        _animation = GetComponent<EnemyAnimation>();
    }

    // 착지 표시는 적을 따라 움직이지 않도록 부모 없이 만든다.
    private void Init()
    {
        _visualBaseHeight = _visual.localPosition.y;

        if(_landingMarkerPrefab == null) return;

        _landingMarker = Instantiate(_landingMarkerPrefab);
        _landingMarker.SetActive(false);
    }
}
