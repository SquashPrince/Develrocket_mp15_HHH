using System.Collections.Generic;
using UnityEngine;

// 적 공격. 일반 · 엘리트 · 보스가 함께 쓰고, 적 데이터의 공격 목록(공격 데이터 에셋)으로 차이를 둔다.
// 행동 그래프가 공격 시작을 요청하면, 쓸 수 있는 공격 중 하나를 골라 동작 목록의 Animator 상태를 순서대로 재생한다.
// 각 동작은 그 상태의 재생이 끝나면 다음 동작으로 넘어가고, 마지막 동작이 끝나면 공격이 끝난다.
// 판정은 지금 동작 클립의 HitStart 이벤트에서 한다. 첫 HitStart 이벤트 전까지는 공격의 회전 속도로 대상 쪽으로 돌고, 그 뒤에는 방향을 고정한다.
// 구간 판정 동작은 HitStart 이벤트부터 그 동작이 끝날 때까지 매 프레임 판정한다. 어느 쪽이든 공격 한 번에 범위 안의 캐릭터마다 한 번씩만 맞는다. (파티 전투에서 필드에 캐릭터가 여럿일 수 있다)
// 패링할 수 있는 동작은 Telegraph 이벤트부터 첫 HitStart 이벤트까지 패링 가능 시간을 연다. 예고 시점은 이벤트 프레임으로 조정한다. 예고 표시는 플레이어 쪽(파티)이 예고 위치에 띄운다.
// 공격 시작부터 그 공격의 경직 저항 레벨을 적용하고, 첫 HitStart 이벤트(또는 설정에 따라 ResistEnd 이벤트)에서 푼다.
// 경직 · 그로기 · 처치되면 진행 중인 공격을 끊는다. 끊긴 공격도 쓴 것으로 보고 쿨타임을 시작한다.
// 공격 선택 : 쿨타임 중이거나 대상이 사용 최소 거리보다 가까운 공격은 빼고, 가중치(기본 가중치 + 증가량 × 마지막으로 쓴 뒤 다른 공격이 선택된 횟수)에 비례해 무작위로 고른다.
//   사거리 밖 공격만 요청받으면(대상이 공격 사거리 밖) 사거리 밖에서 쓸 수 있는 공격 중에서만 고른다.
// 모션은 루트 모션 없이 제자리에서 재생한다. 도약 · 돌진 공격은 LeapStart · LeapLock · LeapEnd 이벤트를 도약 기능(EnemyLeap)에 넘겨 목표 지점으로 옮긴다.
[RequireComponent(typeof(EnemyState), typeof(EnemyAnimation))]
public class EnemyAttacker : MonoBehaviour, IEnemyParryable
{
    // 한 번의 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 8;

    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("패링 공격 예고 표시를 띄울 위치. 록온 표시와 겹치지 않도록 적 안에 따로 둔 자식. 표시는 플레이어 쪽이 띄운다")]
    [SerializeField] private Transform _telegraphPoint;

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    // 이번 공격에 이미 맞은 캐릭터. 공격 한 번에 캐릭터마다 한 번만 맞힌다.
    private readonly List<PlayerStatus> _landedTargets = new List<PlayerStatus>();
    // 이 적이 쓰는 공격 목록 (적 데이터)
    private EnemyAttackData[] _attacks;
    private EnemyState _state;
    private EnemyAnimation _animation;
    // 도약 공격이 있는 적만 가진다. 없으면 null 이다.
    private EnemyLeap _leap;
    private Transform _target;
    // 공격별 남은 쿨타임과, 마지막으로 쓴 뒤 다른 공격이 선택된 횟수
    private float[] _cooldownTimers;
    private int[] _skipCounts;
    private int _attackIndex = -1;
    private int _stepIndex;
    private int _stepHash;
    // 첫 HitStart 이벤트가 지났는지. 지나면 대상 쪽으로 돌지 않고 패링 가능 시간을 닫는다.
    private bool _isHitStarted;
    // 구간 판정 동작에서 지금 동작의 판정이 켜져 있는지. 동작이 바뀌면 끈다.
    private bool _isHitActive;
    private bool _isParryWindowOpen;

    public bool IsParryWindowOpen => _isParryWindowOpen;
    public Transform TelegraphPoint => _telegraphPoint;
    public bool IsAttacking => _attackIndex >= 0;
    // 마지막 공격이 경직 · 그로기 · 처치로 끊겼는지. 행동 그래프가 공격 노드의 성공 · 실패를 정할 때 쓴다.
    public bool WasLastAttackInterrupted { get; private set; }

    private EnemyAttackData CurrentAttack => _attacks[_attackIndex];
    private EnemyAttackStep CurrentStep => CurrentAttack.GetStep(_stepIndex);

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
        _stepIndex = -1;
        _isHitStarted = false;
        _landedTargets.Clear();
        WasLastAttackInterrupted = false;

        _state.SetAttackStaggerResistLevel(CurrentAttack.StaggerResistLevel);

#if UNITY_EDITOR
        Debug.Log($"[적 공격] {name} {CurrentAttack.name}");
#endif

        StartNextStep();
        return true;
    }

    // 이전 동작 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
    public void HandleAnimationEvent(EnemyAnimationEventType type, AnimationEvent animationEvent)
    {
        if(!IsAttacking || animationEvent.animatorStateInfo.shortNameHash != _stepHash) return;

        switch(type)
        {
            case EnemyAnimationEventType.Telegraph:
                if(CurrentStep.IsParryable && !_isHitStarted) OpenParryWindow();
                break;
            case EnemyAnimationEventType.HitStart:
                PerformHit();
                break;
            case EnemyAnimationEventType.ResistEnd:
                if(CurrentAttack.ResistUntilEvent) _state.SetAttackStaggerResistLevel(0);
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

        if(_isHitActive) HitTargets();

        if(_animation.IsStateFinished(_stepHash)) StartNextStep();
    }

    private void StartNextStep()
    {
        _stepIndex++;
        _isHitActive = false;

        if(_stepIndex >= CurrentAttack.StepCount)
        {
            EndAttack(false);
            _animation.PlayIdle();
            return;
        }

        _stepHash = Animator.StringToHash(CurrentStep.AnimationState);
        _animation.PlayState(_stepHash);
    }

    // 첫 HitStart 이벤트에서 패링 가능 시간을 닫고, 경직 저항을 이벤트까지 유지하는 공격이 아니면 경직 저항도 푼다.
    // 구간 판정 동작은 HitStart 이벤트마다 그 동작의 판정을 켜고, 한 순간 판정 동작은 첫 HitStart 이벤트에서만 한 번 판정한다.
    // 판정 중 패링당해 경직하면 경직 알림에서 공격이 끊긴다.
    private void PerformHit()
    {
        bool isFirstHit = !_isHitStarted;

        if(isFirstHit)
        {
#if UNITY_EDITOR
            if(CurrentStep.IsParryable && !_isParryWindowOpen) Debug.LogWarning($"[적 공격] {name} {CurrentAttack.name} : 패링 동작인데 HitStart 전에 Telegraph 이벤트가 없어 패링할 수 없다. 준비 단계 클립에 Telegraph 이벤트를 넣는다");
#endif

            _isHitStarted = true;
            CloseParryWindow();

            if(!CurrentAttack.ResistUntilEvent) _state.SetAttackStaggerResistLevel(0);
        }

        if(CurrentStep.Mode == EnemyAttackStep.HitMode.Window) _isHitActive = true;
        else if(isFirstHit) HitTargets();
    }

    // 공격을 끝낸다. 끊겼든 끝까지 했든 쿨타임을 시작하고, 패링 가능 시간 · 경직 저항을 닫는다.
    private void EndAttack(bool isInterrupted)
    {
        if(!IsAttacking) return;

        CloseParryWindow();
        if(_leap != null) _leap.Stop();
        _state.SetAttackStaggerResistLevel(0);
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
        EnemyAttackData attack = _attacks[index];

        if(_cooldownTimers[index] > 0f || distance < attack.MinUseDistance) return false;

        return !rangedOnly || attack.CanUseOutOfRange;
    }

    private float GetWeight(int index) => _attacks[index].BaseWeight + _attacks[index].WeightGrowth * _skipCounts[index];

    // 지금 동작의 판정 구 안의 캐릭터 중 이번 공격에 아직 맞지 않은 캐릭터를 모두 맞힌다. 콜라이더가 여러 개인 캐릭터도 한 번만 맞힌다.
    // 맞은 결과(피격 · 무적 · 완벽 회피 · 패링)와 상관없이 판정을 받은 캐릭터는 이번 공격에서 다시 판정하지 않는다.
    //   지원 공격처럼 무적인 이전 캐릭터와 패링 중인 새 캐릭터가 함께 있어도, 새 캐릭터가 따로 판정을 받는다.
    // 패링당해 공격이 끊기면 남은 캐릭터는 맞히지 않는다.
    private void HitTargets()
    {
        EnemyAttackData attack = CurrentAttack;
        EnemyAttackStep step = CurrentStep;
        Vector3 center = transform.position + transform.rotation * step.HitCenter;
        int count = Physics.OverlapSphereNonAlloc(center, step.HitRadius, _hitColliders, _targetLayers, QueryTriggerInteraction.Ignore);

        for(int i = 0; i < count; i++)
        {
            PlayerStatus status = _hitColliders[i].GetComponentInParent<PlayerStatus>();

            if(status == null || _landedTargets.Contains(status)) continue;

            _landedTargets.Add(status);
            status.TakeHit(attack, step, this);

            if(!IsAttacking) return;
        }
    }

    private void TurnTowardsTarget()
    {
        if(_target == null) return;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;

        if(toTarget == Vector3.zero) return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), CurrentAttack.TurnSpeed * EnemyTime.DeltaTime);
    }

    private void StartLeap(AnimationEvent animationEvent)
    {
        if(_leap == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[적 공격] {name} {CurrentAttack.name} : LeapStart 이벤트가 있지만 EnemyLeap 컴포넌트가 없어 도약하지 않는다");
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

    }

    private void CloseParryWindow()
    {
        if(!_isParryWindowOpen) return;

        ParryWindow.Close(this);
        _isParryWindowOpen = false;

    }

    // 경직 · 그로기 · 처치되면 진행 중인 공격을 끊는다. 모션은 적 모션 기능이 반응 모션으로 바꾼다.
    private void OnInterrupted() => EndAttack(true);

    // Scene 창에서 적을 선택하면 공격 · 동작별 판정 범위를 보여준다. 패링 동작은 노란색, 나머지는 빨간색이다.
    // 플레이하지 않을 때도 그리므로 적 데이터를 직접 읽는다.
    private void DrawHitRanges()
    {
        EnemyController controller = GetComponent<EnemyController>();

        if(controller == null || controller.Data == null || controller.Data.Attacks == null) return;

        foreach(EnemyAttackData attack in controller.Data.Attacks)
        {
            if(attack == null) continue;

            for(int i = 0; i < attack.StepCount; i++)
            {
                EnemyAttackStep step = attack.GetStep(i);

                Gizmos.color = step.IsParryable ? Color.yellow : Color.red;
                Gizmos.DrawWireSphere(transform.position + transform.rotation * step.HitCenter, step.HitRadius);
            }
        }
    }

    private void CacheComponent()
    {
        _attacks = GetComponent<EnemyController>().Data.Attacks;
        _state = GetComponent<EnemyState>();
        _animation = GetComponent<EnemyAnimation>();
        _leap = GetComponent<EnemyLeap>();
    }

    private void Init()
    {
        _cooldownTimers = new float[_attacks.Length];
        _skipCounts = new int[_attacks.Length];
    }

    private void BindReactionEvent()
    {
        _state.Died += OnInterrupted;
        _state.Staggered += OnInterrupted;
        _state.GroggyEntered += OnInterrupted;
    }

    private void UnbindReactionEvent()
    {
        _state.Died -= OnInterrupted;
        _state.Staggered -= OnInterrupted;
        _state.GroggyEntered -= OnInterrupted;
    }
}
