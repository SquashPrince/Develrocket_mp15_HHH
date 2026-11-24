using System.Collections.Generic;
using UnityEngine;

// 일반 · 엘리트 · 보스가 함께 쓰는 적 공격. 행동 그래프가 요청하면 공격 하나를 가중치로 골라 동작을 순서대로 재생한다.
// 판정 · 패링 가능 시간 · 경직 저항 · 도약 시점은 클립의 애니메이션 이벤트가 정한다.
// 자세한 흐름 : Docs/Technical/Enemy/EnemyAttack.md
[RequireComponent(typeof(EnemyState), typeof(EnemyAnimation), typeof(EnemyMovement))]
public class EnemyAttacker : MonoBehaviour, IEnemyParryable
{
    private const int MAX_HIT_COLLIDERS = 8;

    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("패링 공격 예고 표시를 띄울 위치. 록온 표시와 겹치지 않도록 적 안에 따로 둔 자식. 표시는 플레이어 쪽이 띄운다")]
    [SerializeField] private Transform _telegraphPoint;

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    // 콜라이더가 여러 개인 캐릭터도 한 타격에 한 번만 맞힌다.
    private readonly List<PlayerStatus> _landedTargets = new List<PlayerStatus>();
    private EnemyAttackData[] _attacks;
    private EnemyController _controller;
    private EnemyState _state;
    private EnemyAnimation _animation;
    private EnemyMovement _movement;
    // 도약 공격이 있는 적만 가진다. 없으면 null 이다.
    private EnemyLeap _leap;
    private Transform _target;
    private float[] _cooldownTimers;
    // 마지막으로 쓴 뒤 다른 공격이 선택된 횟수 (가중치 증가에 쓴다)
    private int[] _skipCounts;
    private int _attackIndex = -1;
    private int _stepIndex;
    private int _stepHash;
    // 이 공격의 첫 HitStart 이벤트가 지났는지. 지나면 대상 쪽으로 돌지 않는다.
    private bool _isHitStarted;
    // 동작마다 따로 세서, 패링으로 이어간 다음 동작이 자기 Telegraph 로 패링 가능 시간을 다시 열 수 있게 한다.
    private bool _isStepHitStarted;
    // 막힌 동작은 모션을 끝까지 재생하지만 더 판정하지 않는다.
    private bool _isStepParried;
    private bool _isHitActive;
    private bool _isParryWindowOpen;
    // 적 전용 시간 기준. 길이를 모르면 -1
    private float _windowLength;
    private float _windowElapsed;
    private int _nextTickIndex;

    public bool IsParryWindowOpen => _isParryWindowOpen;
    public Transform TelegraphPoint => _telegraphPoint;
    public bool IsAttacking => _attackIndex >= 0;
    // 행동 그래프가 공격 노드의 성공 · 실패를 정할 때 쓴다.
    public bool WasLastAttackInterrupted { get; private set; }

    private EnemyAttackData CurrentAttack => _attacks[_attackIndex];
    private EnemyAttackStep CurrentStep => CurrentAttack.GetStep(_stepIndex);

    private void Awake() => CacheComponent();
    private void Start() => Init();
    private void OnEnable() => BindReactionEvent();
    private void OnDisable()
    {
        CloseParryWindow();
        ParryWindow.EndChain(this);
        UnbindReactionEvent();
    }
    private void Update()
    {
        UpdateCooldowns();
        UpdateAttack();
    }
    private void OnDrawGizmosSelected() => DrawHitRanges();

    public bool CanAttack()
    {
        for(int i = 0; i < _attacks.Length; i++)
        {
            if(_cooldownTimers[i] <= 0f) return true;
        }

        return false;
    }

    // rangedOnly : 대상이 공격 사거리 밖일 때, 다가가기 전에 쓸 수 있는 공격만 고른다.
    public bool TryStartAttack(Transform target, bool rangedOnly = false)
    {
        Vector3 toTarget = HorizontalVector.Between(transform.position, target.position);

        int index = PickAttack(toTarget.magnitude, rangedOnly);

        if(index < 0) return false;

        _target = target;
        _attackIndex = index;
        _stepIndex = -1;
        _isHitStarted = false;
        WasLastAttackInterrupted = false;

        _state.SetAttackStaggerResistLevel(CurrentAttack.StaggerResistLevel);

        // 이어가기 동작이 있는 공격이면 연속 패링 패턴 시작을 알린다. 공격이 끝나거나 끊길 때 끝을 알린다.
        if(CurrentAttack.HasParryChain) ParryWindow.BeginChain(this);

#if UNITY_EDITOR
        Debug.Log($"[적 공격] {name} {CurrentAttack.name}");
#endif

        StartNextStep();
        return true;
    }

    // 이어가기 동작은 공격을 끊지 않고 판정만 멈춘다. 아니면 공격의 저항 레벨로 경직을 걸어 항상 경직된다.
    public void OnParried()
    {
        if(!IsAttacking) return;

        if(CurrentStep.ContinuesOnParry)
        {
#if UNITY_EDITOR
            Debug.Log($"[적 공격] {name} {CurrentAttack.name} : 패링당했지만 공격을 이어간다");
#endif
            _isStepParried = true;
            _isHitActive = false;
            return;
        }

        _state.Stagger(CurrentStep.ParryStaggerTime, CurrentAttack.StaggerResistLevel);
    }

    // 이전 동작 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
    public void HandleAnimationEvent(EnemyAnimationEventType type, AnimationEvent animationEvent)
    {
        if(!IsAttacking || animationEvent.animatorStateInfo.shortNameHash != _stepHash) return;

        switch(type)
        {
            case EnemyAnimationEventType.Telegraph:
                if(CurrentStep.IsParryable && !_isStepHitStarted) OpenParryWindow();
                break;
            case EnemyAnimationEventType.HitStart:
                PerformHit(FindWindowLength(animationEvent));
                break;
            case EnemyAnimationEventType.HitEnd:
                FinishWindow();
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
            if(_cooldownTimers[i] > 0f) _cooldownTimers[i] -= _state.Clock.DeltaTime;
        }
    }

    private void UpdateAttack()
    {
        if(!IsAttacking) return;

        // 따라가는 동안은 이동 기능이 몸을 돌리므로, 따라가지 않을 때만 공격 기능이 돈다.
        bool isFollowing = CurrentStep.FollowsTarget && FollowTarget();

        if(!isFollowing && (!_isHitStarted || CurrentStep.FacesTarget)) TurnTowardsTarget();

        UpdateWindow();

        if(_animation.IsStateFinished(_stepHash)) StartNextStep();
    }

    private void StartNextStep()
    {
        _stepIndex++;
        _isHitActive = false;
        _isStepHitStarted = false;
        _isStepParried = false;
        _movement.Stop(false);

        if(_stepIndex >= CurrentAttack.StepCount)
        {
            EndAttack(false);
            _animation.PlayIdle();
            return;
        }

        _stepHash = Animator.StringToHash(CurrentStep.AnimationState);
        _animation.PlayState(_stepHash);
    }

    // windowLength : 같은 클립의 다음 HitEnd 까지의 길이. 없으면 -1
    private void PerformHit(float windowLength)
    {
        if(_isStepParried) return;

        if(!_isHitStarted)
        {
#if UNITY_EDITOR
            if(CurrentStep.IsParryable && !_isParryWindowOpen) Debug.LogWarning($"[적 공격] {name} {CurrentAttack.name} : 패링 동작인데 HitStart 전에 Telegraph 이벤트가 없어 패링할 수 없다. 준비 단계 클립에 Telegraph 이벤트를 넣는다");
#endif

            _isHitStarted = true;

            if(!CurrentAttack.ResistUntilEvent) _state.SetAttackStaggerResistLevel(0);
        }

        if(!_isStepHitStarted)
        {
            _isStepHitStarted = true;
            CloseParryWindow();
        }

        if(CurrentStep.Mode == EnemyAttackStep.HitMode.Instant)
        {
            HitTargets(CurrentAttack.Reaction);
            return;
        }

#if UNITY_EDITOR
        if(CurrentStep.Timing == EnemyAttackStep.WindowTiming.Count && windowLength < 0f) Debug.LogWarning($"[적 공격] {name} {CurrentAttack.name} : 횟수 방식인데 HitStart 뒤에 HitEnd 이벤트가 없어 시작 시점에 한 번만 타격한다");
#endif

        _windowLength = windowLength;
        _windowElapsed = 0f;
        _nextTickIndex = 0;
        _isHitActive = true;

        HitDueTicks(0f);
    }

    // 횟수 방식은 간격을, 간격 방식은 막타 시점을 정하려면 구간 길이가 필요하다. 상태 재생 속도 배수는 1 로 본다.
    private static float FindWindowLength(AnimationEvent hitStart)
    {
        float endTime = AnimationClipEvents.FindNextTime(hitStart.animatorClipInfo.clip, hitStart.time, nameof(EnemyAnimationEventType.HitEnd));

        return endTime >= 0f ? endTime - hitStart.time : -1f;
    }

    private void UpdateWindow()
    {
        if(!_isHitActive) return;

        _windowElapsed += _state.Clock.DeltaTime;
        HitDueTicks(_windowElapsed);
    }

    // 이벤트가 프레임 사이에 와도 끝 시점 타격이 빠지지 않게 남은 타격을 마치고, 간격 방식은 막타를 한 번 더 한다.
    private void FinishWindow()
    {
        if(!_isHitActive) return;

        HitDueTicks(_windowLength >= 0f ? _windowLength : _windowElapsed);

        if(IsAttacking && _isHitActive && CurrentStep.Timing == EnemyAttackStep.WindowTiming.Interval) HitTargets(GetFinalReaction());

        _isHitActive = false;
    }

    // 횟수 방식의 마지막 타격은 막타다.
    private void HitDueTicks(float elapsed)
    {
        while(IsAttacking && _isHitActive && HasTick(_nextTickIndex) && GetTickTime(_nextTickIndex) <= elapsed + HitWindowTiming.TIME_EPSILON)
        {
            bool isFinal = CurrentStep.Timing == EnemyAttackStep.WindowTiming.Count && _windowLength >= 0f && _nextTickIndex == CurrentStep.HitCount - 1;

            _nextTickIndex++;
            HitTargets(isFinal ? GetFinalReaction() : CurrentAttack.Reaction);
        }
    }

    // 간격 방식은 끝 순간을 막타가 맡는다.
    private bool HasTick(int index) =>
        HitWindowTiming.HasTick(IsIntervalWindow, CurrentStep.HitInterval, CurrentStep.HitCount, _windowLength, IsIntervalWindow, index);

    private float GetTickTime(int index) =>
        HitWindowTiming.GetTickTime(IsIntervalWindow, CurrentStep.HitInterval, CurrentStep.HitCount, _windowLength, index);

    private bool IsIntervalWindow => CurrentStep.Timing == EnemyAttackStep.WindowTiming.Interval;

    private EnemyAttackData.HitReaction GetFinalReaction() => CurrentStep.UseFinalReaction ? CurrentStep.FinalReaction : CurrentAttack.Reaction;

    // 서로의 몸 반지름이 닿으면 멈춘다. 이동 모션은 재생하지 않는다.
    private bool FollowTarget()
    {
        if(_target == null) return false;

        Vector3 toTarget = HorizontalVector.Between(transform.position, _target.position);

        if(toTarget.magnitude <= _controller.Radius + BodyRadius.GetHorizontal(_target))
        {
            _movement.Stop(false);
            return false;
        }

        Transform lookTarget = CurrentStep.FacesTarget ? _target : null;
        _movement.MoveTo(_target.position, _movement.MoveSpeed * CurrentStep.FollowSpeedRate, lookTarget, false);
        return true;
    }

    // 공격을 끝낸다. 끊겼든 끝까지 했든 쿨타임을 시작하고, 패링 가능 시간 · 연속 패링 패턴 · 경직 저항을 닫는다.
    private void EndAttack(bool isInterrupted)
    {
        if(!IsAttacking) return;

        CloseParryWindow();
        ParryWindow.EndChain(this);
        if(_leap != null) _leap.Stop();
        _state.SetAttackStaggerResistLevel(0);
        _cooldownTimers[_attackIndex] = CurrentAttack.Cooldown;
        _attackIndex = -1;
        _isHitActive = false;
        _movement.Stop(false);
        WasLastAttackInterrupted = isInterrupted;
    }

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

    // 여러 적이 동시에 연속 패링 패턴을 쓰면 패링 대상 · 순간 이동 · 카메라가 적 사이를 오가므로 하나만 허용한다.
    private bool IsUsable(int index, float distance, bool rangedOnly)
    {
        EnemyAttackData attack = _attacks[index];

        if(_cooldownTimers[index] > 0f || distance < attack.MinUseDistance) return false;
        if(attack.HasParryChain && ParryWindow.IsChainActive) return false;

        return !rangedOnly || attack.CanUseOutOfRange;
    }

    private float GetWeight(int index) => _attacks[index].BaseWeight + _attacks[index].WeightGrowth * _skipCounts[index];

    // 패링할 수 있는 캐릭터를 먼저 판정한다. 콜라이더 순서대로 하면 퇴장하는 이전 캐릭터가 먼저 맞은 뒤 패링이 성립할 수 있다.
    private void HitTargets(EnemyAttackData.HitReaction reaction)
    {
        EnemyAttackData attack = CurrentAttack;
        EnemyAttackStep step = CurrentStep;
        int count = step.Range.Overlap(transform, _hitColliders, _targetLayers);
        PlayerStatus parrier = null;

        _landedTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            PlayerStatus status = _hitColliders[i].GetComponentInParent<PlayerStatus>();

            if(status == null || _landedTargets.Contains(status)) continue;

            _landedTargets.Add(status);

            if(parrier == null && status.CanParry(step)) parrier = status;
        }

        if(parrier != null)
        {
            parrier.TakeHit(attack, step, reaction, this);

            if(!IsAttacking || _isStepParried) return;
        }

        for(int i = 0; i < _landedTargets.Count; i++)
        {
            if(_landedTargets[i] == parrier) continue;

            _landedTargets[i].TakeHit(attack, step, reaction, this);

            if(!IsAttacking || _isStepParried) return;
        }
    }

    private void TurnTowardsTarget()
    {
        if(_target == null) return;

        Vector3 toTarget = HorizontalVector.Between(transform.position, _target.position);

        if(toTarget == Vector3.zero) return;

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), CurrentAttack.TurnSpeed * _state.Clock.DeltaTime);
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
        BattleFeedback.Raise(BattleFeedback.Moment.ParryCue);
    }

    private void CloseParryWindow()
    {
        if(!_isParryWindowOpen) return;

        ParryWindow.Close(this);
        _isParryWindowOpen = false;

    }

    // 경직 · 그로기 · 처치되면 진행 중인 공격을 끊는다. 모션은 적 모션 기능이 반응 모션으로 바꾼다.
    private void OnInterrupted() => EndAttack(true);

    // 플레이하지 않을 때도 그리므로 적 데이터를 직접 읽는다. 패링 동작은 노랑, 나머지는 빨강.
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
                step.Range.DrawGizmos(transform);
            }
        }
    }

    private void CacheComponent()
    {
        _controller = GetComponent<EnemyController>();
        _attacks = _controller.Data.Attacks;
        _state = GetComponent<EnemyState>();
        _animation = GetComponent<EnemyAnimation>();
        _movement = GetComponent<EnemyMovement>();
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
