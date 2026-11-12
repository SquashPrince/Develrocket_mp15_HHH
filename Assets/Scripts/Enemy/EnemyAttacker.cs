using UnityEngine;

// 적 공격. 일반 · 엘리트 · 보스가 함께 쓰고, 공격 목록 · 설정값으로 차이를 둔다.
// 행동 그래프가 공격 시작을 요청하면, 쓸 수 있는 공격 중 하나를 골라 단계별 Animator 상태를 순서대로 재생한다.
// 각 단계는 그 상태의 재생이 끝나면 다음 단계로 넘어가고, 마지막 단계가 끝나면 공격이 끝난다.
// 판정은 지금 단계 클립의 Hit 이벤트에서 한다. 첫 Hit 이벤트 전까지는 대상 쪽으로 돌고, 그 뒤에는 방향을 고정한다.
// 판정 지속 공격은 Hit 이벤트부터 그 단계가 끝날 때까지 매 프레임 판정한다. 어느 쪽이든 플레이어는 공격 한 번에 한 번만 맞는다.
// 패링 공격은 Telegraph 이벤트부터 첫 Hit 이벤트까지 패링 가능 시간을 열고 예고 표시를 켠다. 예고 시점은 이벤트 프레임으로 조정한다.
// 공격 시작부터 그 공격의 경직 저항 레벨을 적용하고, 첫 Hit 이벤트(또는 설정에 따라 ResistEnd 이벤트)에서 푼다.
// 경직 · 그로기 · 처치되면 진행 중인 공격을 끊는다. 끊긴 공격도 쓴 것으로 보고 쿨타임을 시작한다.
// 공격 선택 : 쿨타임 중이거나 대상이 사용 최소 거리보다 가까운 공격은 빼고, 가중치(기본 가중치 + 증가량 × 마지막으로 쓴 뒤 다른 공격이 선택된 횟수)에 비례해 무작위로 고른다.
//   사거리 밖 공격만 요청받으면(대상이 공격 사거리 밖) 사거리 밖에서 쓸 수 있는 공격 중에서만 고른다.
// 모션은 루트 모션 없이 제자리에서 재생한다. 도약 · 돌진 공격은 LeapStart · LeapLock · LeapEnd 이벤트를 도약 기능(EnemyLeap)에 넘겨 목표 지점으로 옮긴다.
[RequireComponent(typeof(EnemyHealth), typeof(EnemyReaction), typeof(EnemyAnimation))]
public class EnemyAttacker : MonoBehaviour, IEnemyParryable
{
    // 한 번의 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 8;

    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("공격 중 판정 전까지 대상 쪽으로 도는 초당 각도")]
    [SerializeField, Min(0f)] private float _turnSpeed = 360f;
    [Tooltip("[임시] 패링 공격 예고 표시로 쓸 프리팹. 패링 가능 시간 동안만 켜진다. 나중에 이펙트로 바꾼다")]
    [SerializeField] private GameObject _parryTelegraphPrefab;
    [Tooltip("예고 표시를 만들 위치. 록온 표시와 겹치지 않도록 적 안에 따로 둔 부모")]
    [SerializeField] private Transform _telegraphPoint;
    [Tooltip("이 적이 쓰는 공격 목록")]
    [SerializeField] private EnemyAttackSetting[] _attacks = { new EnemyAttackSetting() };

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    private EnemyHealth _health;
    private EnemyReaction _reaction;
    private EnemyAnimation _animation;
    // 도약 공격이 있는 적만 가진다. 없으면 null 이다.
    private EnemyLeap _leap;
    private GameObject _parryTelegraph;
    private Transform _target;
    // 공격별 남은 쿨타임과, 마지막으로 쓴 뒤 다른 공격이 선택된 횟수
    private float[] _cooldownTimers;
    private int[] _skipCounts;
    private int _attackIndex = -1;
    private int _phaseIndex;
    private int _phaseHash;
    // 첫 Hit 이벤트가 지났는지. 지나면 대상 쪽으로 돌지 않고 패링 가능 시간을 닫는다.
    private bool _isHitStarted;
    // 플레이어를 이미 맞혔는지. 공격 한 번에 한 번만 맞힌다.
    private bool _hasLanded;
    // 판정 지속 공격에서 지금 단계의 판정이 켜져 있는지. 단계가 바뀌면 끈다.
    private bool _isHitActive;
    private bool _isParryWindowOpen;

    public bool IsParryWindowOpen => _isParryWindowOpen;
    public bool IsAttacking => _attackIndex >= 0;
    // 마지막 공격이 경직 · 그로기 · 처치로 끊겼는지. 행동 그래프가 공격 노드의 성공 · 실패를 정할 때 쓴다.
    public bool WasLastAttackInterrupted { get; private set; }

    private EnemyAttackSetting CurrentAttack => _attacks[_attackIndex];

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void OnEnable() => BindReactionEvent();
    private void OnDisable()
    {
        CloseParryWindow();
        UnbindReactionEvent();
    }
    private void Update()
    {
        UpdateCooldowns();
        UpdateAttack();
    }
    private void OnDrawGizmosSelected() => DrawHitRanges();

    // 쿨타임이 끝난 공격이 하나라도 있으면 true 다.
    public bool CanAttack()
    {
        for(int i = 0; i < _attacks.Length; i++)
        {
            if(_cooldownTimers[i] <= 0f) return true;
        }

        return false;
    }

    // 쓸 수 있는 공격 중 하나를 골라 시작한다. 쓸 수 있는 공격이 없으면 false 를 돌려준다.
    // rangedOnly : true 면 사거리 밖에서 쓸 수 있는 공격 중에서만 고른다. 대상이 공격 사거리 밖일 때 다가가기 전에 쓴다.
    public bool TryStartAttack(Transform target, bool rangedOnly = false)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        int index = PickAttack(toTarget.magnitude, rangedOnly);

        if(index < 0) return false;

        _target = target;
        _attackIndex = index;
        _phaseIndex = -1;
        _isHitStarted = false;
        _hasLanded = false;
        WasLastAttackInterrupted = false;

        _reaction.SetAttackStaggerResistLevel(CurrentAttack.StaggerResistLevel);

#if UNITY_EDITOR
        Debug.Log($"[적 공격] {name} {CurrentAttack.Name}");
#endif

        StartNextPhase();
        return true;
    }

    // 이전 단계 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
    public void HandleAnimationEvent(EnemyAnimationEventType type, AnimationEvent animationEvent)
    {
        if(!IsAttacking || animationEvent.animatorStateInfo.shortNameHash != _phaseHash) return;

        switch(type)
        {
            case EnemyAnimationEventType.Telegraph:
                if(CurrentAttack.IsParryable && !_isHitStarted) OpenParryWindow();
                break;
            case EnemyAnimationEventType.Hit:
                PerformHit();
                break;
            case EnemyAnimationEventType.ResistEnd:
                if(CurrentAttack.ResistUntilEvent) _reaction.SetAttackStaggerResistLevel(0);
                break;
            case EnemyAnimationEventType.LeapStart:
                StartLeap(animationEvent);
                break;
            case EnemyAnimationEventType.LeapLock:
                if(_leap != null) _leap.Lock();
                break;
            case EnemyAnimationEventType.LeapEnd:
                if(_leap != null) _leap.Land();
                break;
        }
    }

    private void UpdateCooldowns()
    {
        for(int i = 0; i < _cooldownTimers.Length; i++)
        {
            if(_cooldownTimers[i] > 0f) _cooldownTimers[i] -= EnemyTime.DeltaTime;
        }
    }

    private void UpdateAttack()
    {
        if(!IsAttacking) return;

        if(!_isHitStarted) TurnTowardsTarget();

        if(_isHitActive && !_hasLanded) _hasLanded = HitTarget(CurrentAttack);

        if(_animation.IsStateFinished(_phaseHash)) StartNextPhase();
    }

    private void StartNextPhase()
    {
        _phaseIndex++;
        _isHitActive = false;

        if(_phaseIndex >= CurrentAttack.AnimationStates.Length)
        {
            EndAttack(false);
            _animation.PlayIdle();
            return;
        }

        _phaseHash = Animator.StringToHash(CurrentAttack.AnimationStates[_phaseIndex]);
        _animation.PlayState(_phaseHash);
    }

    // 첫 Hit 이벤트에서 패링 가능 시간을 닫고, 경직 저항을 이벤트까지 유지하는 공격이 아니면 경직 저항도 푼다.
    // 판정 지속 공격은 Hit 이벤트마다 그 단계의 판정을 켜고, 아니면 첫 Hit 이벤트에서만 한 번 판정한다.
    // 판정 중 패링당해 경직하면 경직 알림에서 공격이 끊긴다.
    private void PerformHit()
    {
        bool isFirstHit = !_isHitStarted;

        if(isFirstHit)
        {
#if UNITY_EDITOR
            if(CurrentAttack.IsParryable && !_isParryWindowOpen) Debug.LogWarning($"[적 공격] {name} {CurrentAttack.Name} : 패링 공격인데 Hit 전에 Telegraph 이벤트가 없어 패링할 수 없다. 준비 단계 클립에 Telegraph 이벤트를 넣는다");
#endif

            _isHitStarted = true;
            CloseParryWindow();

            if(!CurrentAttack.ResistUntilEvent) _reaction.SetAttackStaggerResistLevel(0);
        }

        if(_hasLanded) return;

        if(CurrentAttack.IsContinuousHit) _isHitActive = true;
        else if(isFirstHit) _hasLanded = HitTarget(CurrentAttack);
    }

    // 공격을 끝낸다. 끊겼든 끝까지 했든 쿨타임을 시작하고, 패링 가능 시간 · 경직 저항을 닫는다.
    private void EndAttack(bool isInterrupted)
    {
        if(!IsAttacking) return;

        CloseParryWindow();
        if(_leap != null) _leap.Stop();
        _reaction.SetAttackStaggerResistLevel(0);
        _cooldownTimers[_attackIndex] = CurrentAttack.Cooldown;
        _attackIndex = -1;
        _isHitActive = false;
        WasLastAttackInterrupted = isInterrupted;
    }

    // 지금 쓸 수 있는 공격 중 가중치에 비례해 무작위로 고른다. 고른 공격은 선택 횟수를 0 으로, 나머지는 1 씩 올린다.
    private int PickAttack(float distance, bool rangedOnly)
    {
        float totalWeight = 0f;

        for(int i = 0; i < _attacks.Length; i++)
        {
            if(IsUsable(i, distance, rangedOnly)) totalWeight += GetWeight(i);
        }

        if(totalWeight <= 0f) return -1;

        float pick = Random.Range(0f, totalWeight);
        int pickedIndex = -1;

        for(int i = 0; i < _attacks.Length; i++)
        {
            if(!IsUsable(i, distance, rangedOnly)) continue;

            pickedIndex = i;
            pick -= GetWeight(i);

            if(pick < 0f) break;
        }

        for(int i = 0; i < _attacks.Length; i++)
        {
            _skipCounts[i] = i == pickedIndex ? 0 : _skipCounts[i] + 1;
        }

        return pickedIndex;
    }

    // 쿨타임이 끝났고, 대상이 사용 최소 거리 이상 떨어져 있으면 쓸 수 있다. 사거리 밖 공격만 고를 때는 사거리 밖에서 쓸 수 있는 공격이어야 한다.
    private bool IsUsable(int index, float distance, bool rangedOnly)
    {
        EnemyAttackSetting attack = _attacks[index];

        if(_cooldownTimers[index] > 0f || distance < attack.MinUseDistance) return false;

        return !rangedOnly || attack.CanUseOutOfRange;
    }

    private float GetWeight(int index) => _attacks[index].BaseWeight + _attacks[index].WeightGrowth * _skipCounts[index];

    // 판정 구 안에 플레이어가 있으면 한 번 맞히고 true 를 돌려준다. 콜라이더가 여러 개여도 플레이어는 하나이므로 처음 찾은 대상만 맞힌다.
    private bool HitTarget(EnemyAttackSetting attack)
    {
        Vector3 center = transform.position + transform.rotation * attack.HitCenter;
        int count = Physics.OverlapSphereNonAlloc(center, attack.HitRadius, _hitColliders, _targetLayers, QueryTriggerInteraction.Ignore);

        for(int i = 0; i < count; i++)
        {
            PlayerStatus status = _hitColliders[i].GetComponentInParent<PlayerStatus>();

            if(status == null) continue;

            status.TakeHit(attack, this);
            return true;
        }

        return false;
    }

    private void TurnTowardsTarget()
    {
        if(_target == null) return;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;

        if(toTarget == Vector3.zero) return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), _turnSpeed * EnemyTime.DeltaTime);
    }

    private void StartLeap(AnimationEvent animationEvent)
    {
        if(_leap == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[적 공격] {name} {CurrentAttack.Name} : LeapStart 이벤트가 있지만 EnemyLeap 컴포넌트가 없어 도약하지 않는다");
#endif
            return;
        }

        _leap.Begin(_target, animationEvent, CurrentAttack.LeapHeight, CurrentAttack.OvershootDistance);
    }

    private void OpenParryWindow()
    {
        if(_isParryWindowOpen) return;

        ParryWindow.Open(this);
        _isParryWindowOpen = true;

        if(_parryTelegraph != null) _parryTelegraph.SetActive(true);
    }

    private void CloseParryWindow()
    {
        if(!_isParryWindowOpen) return;

        ParryWindow.Close(this);
        _isParryWindowOpen = false;

        if(_parryTelegraph != null) _parryTelegraph.SetActive(false);
    }

    // 경직 · 그로기 · 처치되면 진행 중인 공격을 끊는다. 모션은 적 모션 기능이 반응 모션으로 바꾼다.
    private void OnInterrupted() => EndAttack(true);

    // Scene 창에서 적을 선택하면 공격별 판정 범위를 보여준다. 패링 공격은 노란색, 나머지는 빨간색이다.
    private void DrawHitRanges()
    {
        foreach(EnemyAttackSetting attack in _attacks)
        {
            Gizmos.color = attack.IsParryable ? Color.yellow : Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.rotation * attack.HitCenter, attack.HitRadius);
        }
    }

    private void CacheComponent()
    {
        _health = GetComponent<EnemyHealth>();
        _reaction = GetComponent<EnemyReaction>();
        _animation = GetComponent<EnemyAnimation>();
        _leap = GetComponent<EnemyLeap>();
    }

    // 예고 표시는 미리 한 번 만들어 꺼 두고, 패링 가능 시간마다 켜고 끈다. 예고 표시 프리팹은 넣지 않아도 된다.
    private void Init()
    {
        _cooldownTimers = new float[_attacks.Length];
        _skipCounts = new int[_attacks.Length];

        if(_parryTelegraphPrefab == null) return;

        _parryTelegraph = Instantiate(_parryTelegraphPrefab, _telegraphPoint);
        _parryTelegraph.SetActive(false);
    }

    private void BindReactionEvent()
    {
        _health.Died += OnInterrupted;
        _reaction.Staggered += OnInterrupted;
        _reaction.GroggyEntered += OnInterrupted;
    }

    private void UnbindReactionEvent()
    {
        _health.Died -= OnInterrupted;
        _reaction.Staggered -= OnInterrupted;
        _reaction.GroggyEntered -= OnInterrupted;
    }
}
