using UnityEngine;
using UnityEngine.AI;

// 적 도약 · 돌진 공격의 이동. 공격 클립의 LeapStart 이벤트부터 같은 클립의 LeapEnd 이벤트(착지 · 도착)까지 출발 위치에서 목표 지점으로 옮긴다.
// 진행도는 흐른 시간이 아니라 클립 재생 위치로 정한다. 슬로모션 · 속도 곡선으로 재생 속도가 바뀌어도 LeapEnd 이벤트 순간에 목표 지점에 닿는다.
//   그래서 이동 속도는 LeapStart ~ LeapEnd 사이 클립 구간 길이(와 그 구간의 재생 속도)로 조절한다.
// 판정은 이동과 별개로 Hit 이벤트가 정한다. 도약은 LeapEnd 와 같거나 조금 뒤에, 돌진은 이동 시작 쪽에 Hit 을 두고 지속 판정을 쓴다.
// 목표 지점은 LeapLock 이벤트 전까지 대상을 따라가고, 그 뒤에는 고정된다. LeapLock 이벤트가 없으면 LeapEnd 까지 따라간다.
// 목표 지점은 대상 위치에서 출발 위치 반대쪽으로 지나치는 거리만큼 더 간 곳이다. NavMesh 밖이면 출발 위치에서 가는 선 위의 NavMesh 경계 지점으로 바꾼다.
// 높이가 0 이면 땅을 따라 직선으로 돌진한다. 0 보다 크면 외형(Visual)만 포물선으로 띄운다.
//   루트는 NavMesh 위치 맞춤을 따라야 하므로 띄우지 않는다. 공중에 있는 동안은 콜라이더를 꺼 맞지 않게 한다.
//   내려오면 콜라이더를 바로 켜 공격(회피 반격 등)에 맞게 하되, 대상과 겹쳐 있으면 겹침이 풀릴 때까지 대상과의 충돌만 무시한다.
//   겹친 채 충돌을 켜면 대상의 CharacterController 가 겹침을 풀며 바닥 아래로 밀려날 수 있기 때문이다.
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
    // 내려온 뒤 겹침이 풀릴 때까지 충돌을 무시하고 있는 대상의 콜라이더. 없으면 null 이다.
    private Collider _ignoredCollider;
    private Vector3 _startPosition;
    private Vector3 _landingPosition;
    private float _visualBaseHeight;
    private float _height;
    private float _overshootDistance;
    private int _stateHash;
    // 클립 기준 이륙 · 착지 시간(초)과 클립 길이. 재생 위치를 클립 시간으로 바꿔 진행도를 구한다.
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

    // LeapStart 이벤트에서 부른다. 같은 클립에서 이 이벤트 뒤에 LeapEnd 이벤트가 없으면 도착 시점을 알 수 없어 이동하지 않는다.
    // height : 외형이 떠오르는 최고 높이. 0 이면 돌진. overshootDistance : 대상 위치보다 더 지나칠 거리.
    public void Begin(Transform target, AnimationEvent leapStartEvent, float height, float overshootDistance)
    {
        AnimationClip clip = leapStartEvent.animatorClipInfo.clip;
        float landTime = FindEventTime(clip, nameof(EnemyAnimationEventType.LeapEnd), leapStartEvent.time);

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

    // LeapLock 이벤트에서 부른다. 이 순간의 대상 위치로 목표 지점을 고정한다.
    public void Lock()
    {
        if(!IsLeaping || _isLocked) return;

        UpdateLandingPosition();
        _isLocked = true;
    }

    // LeapEnd 이벤트에서 부른다. 목표 지점에 내려놓는다.
    public void Land()
    {
        if(!IsLeaping) return;

        MoveTo(_landingPosition);
        Stop();
    }

    // 착지하거나 공격이 끊기면 도약을 끝낸다. 끊긴 경우 그 자리에 내려온다. 외형 높이 · 콜라이더 · 표시를 되돌린다.
    public void Stop()
    {
        if(!IsLeaping) return;

        IsLeaping = false;
        SetVisualHeight(0f);
        SetMarkerActive(false);

        if(_height > 0f) EnableColliderAfterLanding();
    }

    // 콜라이더를 켜고, 대상과 겹쳐 있으면 겹침이 풀릴 때까지 대상과의 충돌만 무시한다. 공격 판정은 콜라이더로 찾으므로 그대로 맞는다.
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

    // 대상 위치에서 출발 위치 반대쪽으로 지나치는 거리만큼 더 간 곳을 목표 지점으로 삼는다.
    // 출발 위치에서 그 지점까지 가는 길에 NavMesh 경계가 있으면 경계 지점으로 바꾼다.
    // NavMesh.Raycast 는 경계에 막히면 막힌 지점을, 막히지 않으면 도착 지점을 돌려준다.
    private void UpdateLandingPosition()
    {
        if(_target == null) return;

        Vector3 toTarget = _target.position - _startPosition;
        toTarget.y = 0f;

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

    // clip 에서 afterTime 이후에 있는 functionName 이벤트 중 가장 이른 시간을 찾는다. 없으면 -1 이다.
    private static float FindEventTime(AnimationClip clip, string functionName, float afterTime)
    {
        float foundTime = -1f;

        foreach(AnimationEvent clipEvent in clip.events)
        {
            if(clipEvent.functionName != functionName || clipEvent.time <= afterTime) continue;
            if(foundTime < 0f || clipEvent.time < foundTime) foundTime = clipEvent.time;
        }

        return foundTime;
    }

    private void CacheComponent()
    {
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider>();
        _animation = GetComponent<EnemyAnimation>();
    }

    // 외형의 원래 높이를 기억해 둔다. 착지 표시는 적을 따라 움직이지 않도록 부모 없이 한 번 만들어 꺼 둔다. 프리팹은 넣지 않아도 된다.
    private void Init()
    {
        _visualBaseHeight = _visual.localPosition.y;

        if(_landingMarkerPrefab == null) return;

        _landingMarker = Instantiate(_landingMarkerPrefab);
        _landingMarker.SetActive(false);
    }
}
