using System.Collections.Generic;
using UnityEngine;

// 적 공격. 일반 · 엘리트 · 보스가 함께 쓰고, 적 데이터의 공격 목록(공격 데이터 에셋)으로 차이를 둔다.
// 행동 그래프가 공격 시작을 요청하면, 쓸 수 있는 공격 중 하나를 골라 동작 목록의 Animator 상태를 순서대로 재생한다.
// 각 동작은 그 상태의 재생이 끝나면 다음 동작으로 넘어가고, 마지막 동작이 끝나면 공격이 끝난다.
// 판정은 지금 동작 클립의 HitStart 이벤트에서 시작한다. 첫 HitStart 이벤트 전까지는 공격의 회전 속도로 대상 쪽으로 돌고, 그 뒤에는 방향을 고정한다.
//   대상 바라보기를 켠 동작은 그 동작 동안 계속 돈다.
// 구간 판정 동작은 HitStart ~ HitEnd 사이의 타격 시점마다 판정한다. 시점은 적 전용 시간으로 잰다.
//   간격 방식은 간격마다 타격하고 HitEnd 순간에 막타를 한 번 더 한다. 횟수 방식은 시작 · 끝을 포함해 나누고 끝 시점 타격이 막타다.
//   HitEnd 이벤트가 없으면 간격 방식은 동작이 끝날 때까지 간격마다 타격하고(막타 없음), 횟수 방식은 시작 시점에 한 번만 타격한다.
//   막타 반응을 켠 동작은 막타만 그 반응을 쓴다.
// 타격마다 대미지 · 경직 · 완벽 회피 · 패링을 따로 판정한다. 한 타격에서는 범위 안의 캐릭터마다 한 번씩만 맞는다. (파티 전투에서 필드에 캐릭터가 여럿일 수 있다)
//   그래서 연타 공격 하나 안에서 완벽 회피 → 회피 반격 → 다시 완벽 회피를 반복할 수 있다.
// 대상 따라가기를 켠 동작은 그 동작 동안 대상 쪽으로 이동하고, 서로의 몸 반지름이 닿으면 멈춘다. 이동 모션은 재생하지 않는다.
// 패링할 수 있는 동작은 Telegraph 이벤트부터 그 동작의 첫 HitStart 이벤트까지 패링 가능 시간을 연다. 예고 시점은 이벤트 프레임으로 조정한다. 예고 표시는 플레이어 쪽(파티)이 예고 위치에 띄운다.
// 패링당하면 플레이어는 알림만 보내고, 지금 동작이 이어가기면 경직하지 않고 아니면 경직한다. (연속 패링 패턴)
//   이어가기 동작은 공격을 끊지 않고, 막힌 동작은 더 판정하지 않은 채 모션을 끝까지 재생한 뒤 다음 동작으로 넘어간다.
//   다음 동작도 자기 클립의 Telegraph 이벤트로 패링 가능 시간을 다시 열고, 플레이어는 다시 패링해야 막을 수 있다.
// 공격 시작부터 그 공격의 경직 저항 레벨을 적용하고, 첫 HitStart 이벤트(또는 설정에 따라 ResistEnd 이벤트)에서 푼다.
// 경직 · 그로기 · 처치되면 진행 중인 공격을 끊는다. 끊긴 공격도 쓴 것으로 보고 쿨타임을 시작한다.
// 공격 선택 : 쿨타임 중이거나 대상이 사용 최소 거리보다 가까운 공격, 다른 적이 연속 패링 패턴 중일 때의 연속 패링 패턴은 빼고, 가중치(기본 가중치 + 증가량 × 마지막으로 쓴 뒤 다른 공격이 선택된 횟수)에 비례해 무작위로 고른다.
//   사거리 밖 공격만 요청받으면(대상이 공격 사거리 밖) 사거리 밖에서 쓸 수 있는 공격 중에서만 고른다.
// 모션은 루트 모션 없이 제자리에서 재생한다. 도약 · 돌진 공격은 LeapStart · LeapLock · LeapEnd 이벤트를 도약 기능(EnemyLeap)에 넘겨 목표 지점으로 옮긴다.
[RequireComponent(typeof(EnemyState), typeof(EnemyAnimation), typeof(EnemyMovement))]
public class EnemyAttacker : MonoBehaviour, IEnemyParryable
{
    // 한 번의 판정에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_HIT_COLLIDERS = 8;
    // 타격 시점과 구간 끝을 비교할 때 허용하는 오차(초). 구간 끝 시점의 타격이 계산 오차로 빠지지 않게 한다.
    private const float TIME_EPSILON = 0.0001f;

    [Tooltip("판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _targetLayers = ~0;
    [Tooltip("패링 공격 예고 표시를 띄울 위치. 록온 표시와 겹치지 않도록 적 안에 따로 둔 자식. 표시는 플레이어 쪽이 띄운다")]
    [SerializeField] private Transform _telegraphPoint;

    private readonly Collider[] _hitColliders = new Collider[MAX_HIT_COLLIDERS];
    // 이번 타격에 이미 맞은 캐릭터. 한 타격에서 콜라이더가 여러 개인 캐릭터도 한 번만 맞힌다.
    private readonly List<PlayerStatus> _landedTargets = new List<PlayerStatus>();
    // 이 적이 쓰는 공격 목록 (적 데이터)
    private EnemyAttackData[] _attacks;
    private EnemyController _controller;
    private EnemyState _state;
    private EnemyAnimation _animation;
    private EnemyMovement _movement;
    // 도약 공격이 있는 적만 가진다. 없으면 null 이다.
    private EnemyLeap _leap;
    private Transform _target;
    // 공격별 남은 쿨타임과, 마지막으로 쓴 뒤 다른 공격이 선택된 횟수
    private float[] _cooldownTimers;
    private int[] _skipCounts;
    private int _attackIndex = -1;
    private int _stepIndex;
    private int _stepHash;
    // 이 공격의 첫 HitStart 이벤트가 지났는지. 지나면 대상 쪽으로 돌지 않는다.
    private bool _isHitStarted;
    // 지금 동작의 첫 HitStart 이벤트가 지났는지. 지나면 이 동작의 패링 가능 시간을 닫고, Telegraph 이벤트가 와도 열지 않는다.
    // 동작마다 따로 세서, 패링으로 이어간 다음 동작이 자기 Telegraph 이벤트로 패링 가능 시간을 다시 열 수 있게 한다.
    private bool _isStepHitStarted;
    // 지금 동작이 패링당해 막혔는지(이어가기 동작). 막힌 동작은 모션을 끝까지 재생하지만 더 판정하지 않는다.
    private bool _isStepParried;
    // 구간 판정 동작에서 지금 동작의 판정이 켜져 있는지. HitEnd 이벤트에서 · 동작이 바뀌면 끈다.
    private bool _isHitActive;
    private bool _isParryWindowOpen;
    // 지금 구간의 길이(모르면 -1), 시작부터 지난 시간(적 전용 시간), 다음 타격 번호
    private float _windowLength;
    private float _windowElapsed;
    private int _nextTickIndex;

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
        ParryWindow.EndChain(this);
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

    // 플레이어가 지금 동작을 패링했다는 알림. 아니면 동작의 패링 경직 시간만큼 경직한다.
    // 이어가기 동작은 공격을 끊지 않는다. 막힌 동작은 더 판정하지 않고 모션을 끝까지 재생한 뒤, 평소처럼 다음 동작으로 넘어간다.
    // 경직은 패링당한 공격의 저항 레벨을 경직 레벨로 건다. 레벨이 같으면 경직되므로 패링 성공은 항상 경직을 건다. 경직 알림에서 공격이 끊긴다.
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
            if(_cooldownTimers[i] > 0f) _cooldownTimers[i] -= EnemyTime.DeltaTime;
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

    // 동작의 첫 HitStart 이벤트에서 그 동작의 패링 가능 시간을 닫는다.
    // 공격의 첫 HitStart 이벤트에서는 경직 저항을 이벤트까지 유지하는 공격이 아니면 경직 저항도 푼다.
    // 구간 판정 동작은 HitStart 이벤트마다 구간을 새로 열고 시작 시점의 타격을 바로 한다. 한 순간 판정 동작은 HitStart 이벤트마다 한 번 판정한다.
    // 판정 중 패링당하면 패링 알림(OnParried)에서 경직하거나, 이어가기 동작이면 그 동작의 판정을 멈춘다. 막힌 동작은 HitStart 이벤트가 더 와도 판정하지 않는다.
    // windowLength : 이 HitStart 부터 같은 클립의 다음 HitEnd 까지의 길이(초). 없으면 -1 이다.
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

    // 이 HitStart 뒤 같은 클립에서 처음 나오는 HitEnd 까지의 길이(초, 클립 시간)를 찾는다. 없으면 -1 이다.
    // 횟수 방식은 시작 순간에 타격 간격을, 간격 방식은 막타 시점을 정하려면 구간 길이가 필요하기 때문이다.
    // 적 모션은 적 전용 시간으로 재생하므로 클립 시간이 곧 적 전용 시간이다. Animator 상태의 재생 속도 배수는 1 이라고 본다.
    private static float FindWindowLength(AnimationEvent hitStart)
    {
        AnimationClip clip = hitStart.animatorClipInfo.clip;

        if(clip == null) return -1f;

        float nearestEnd = float.MaxValue;

        foreach(AnimationEvent clipEvent in clip.events)
        {
            if(clipEvent.functionName != nameof(EnemyAnimationEventType.HitEnd) || clipEvent.time <= hitStart.time) continue;

            nearestEnd = Mathf.Min(nearestEnd, clipEvent.time);
        }

        return nearestEnd < float.MaxValue ? nearestEnd - hitStart.time : -1f;
    }

    // 구간 판정이 켜져 있으면 적 전용 시간을 더하고, 지금까지 지난 타격 시점의 타격을 한다.
    private void UpdateWindow()
    {
        if(!_isHitActive) return;

        _windowElapsed += EnemyTime.DeltaTime;
        HitDueTicks(_windowElapsed);
    }

    // HitEnd 이벤트. 구간 끝까지 아직 하지 않은 타격을 마치고, 간격 방식이면 이 순간 막타를 한 번 더 한 뒤 판정을 끈다.
    // 이벤트가 프레임 사이에 와도 끝 시점의 타격이 빠지지 않게 하기 위해서다.
    private void FinishWindow()
    {
        if(!_isHitActive) return;

        HitDueTicks(_windowLength >= 0f ? _windowLength : _windowElapsed);

        if(IsAttacking && _isHitActive && CurrentStep.Timing == EnemyAttackStep.WindowTiming.Interval) HitTargets(GetFinalReaction());

        _isHitActive = false;
    }

    // 구간 시작부터 elapsed(초)까지 지난 타격 시점 중 아직 하지 않은 타격을 순서대로 한다. 횟수 방식의 마지막 타격은 막타다.
    private void HitDueTicks(float elapsed)
    {
        while(IsAttacking && _isHitActive && HasTick(_nextTickIndex) && GetTickTime(_nextTickIndex) <= elapsed + TIME_EPSILON)
        {
            bool isFinal = CurrentStep.Timing == EnemyAttackStep.WindowTiming.Count && _windowLength >= 0f && _nextTickIndex == CurrentStep.HitCount - 1;

            _nextTickIndex++;
            HitTargets(isFinal ? GetFinalReaction() : CurrentAttack.Reaction);
        }
    }

    // index 번째 타격이 이 구간에 있는지.
    //   횟수 : 길이를 모르면 시작 시점 한 번만, 알면 횟수만큼 한다.
    //   간격 : 길이를 모르면 판정이 꺼질 때까지 계속, 알면 구간 끝 직전까지만 한다. 끝 순간은 막타가 맡는다.
    private bool HasTick(int index)
    {
        if(CurrentStep.Timing == EnemyAttackStep.WindowTiming.Count) return _windowLength < 0f ? index == 0 : index < CurrentStep.HitCount;

        return _windowLength < 0f || GetTickTime(index) < _windowLength - TIME_EPSILON;
    }

    // index 번째 타격 시점(구간 시작부터 초).
    //   간격 : 간격 × 번호 / 횟수 : 시작과 끝을 포함해 구간을 (횟수 - 1) 칸으로 나눈 간격 × 번호. 1 타면 시작 시점이다
    private float GetTickTime(int index)
    {
        if(CurrentStep.Timing == EnemyAttackStep.WindowTiming.Interval) return CurrentStep.HitInterval * index;

        int gaps = CurrentStep.HitCount - 1;

        return gaps > 0 && _windowLength > 0f ? _windowLength / gaps * index : 0f;
    }

    // 막타 반응. 동작이 막타 반응을 켰으면 그 반응, 아니면 공격의 반응이다.
    private EnemyAttackData.HitReaction GetFinalReaction() => CurrentStep.UseFinalReaction ? CurrentStep.FinalReaction : CurrentAttack.Reaction;

    // 대상 쪽으로 동작의 따라가기 속도로 이동하고, 이동 중이면 true 를 돌려준다. 서로의 몸 반지름이 닿으면 멈춘다. 이동 모션은 재생하지 않는다.
    // 대상 바라보기를 켠 동작은 이동하며 대상을 보고, 끈 동작은 이동 방향을 본다.
    private bool FollowTarget()
    {
        if(_target == null) return false;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;

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
    // 연속 패링 패턴은 다른 적이 이미 연속 패링 패턴 중이면 쓰지 않는다.
    //   여러 적이 동시에 하면 패링 대상 고르기와 순간 이동 · 카메라가 적 사이를 오가며 어지럽게 바뀌기 때문이다.
    private bool IsUsable(int index, float distance, bool rangedOnly)
    {
        EnemyAttackData attack = _attacks[index];

        if(_cooldownTimers[index] > 0f || distance < attack.MinUseDistance) return false;
        if(attack.HasParryChain && ParryWindow.IsChainActive) return false;

        return !rangedOnly || attack.CanUseOutOfRange;
    }

    private float GetWeight(int index) => _attacks[index].BaseWeight + _attacks[index].WeightGrowth * _skipCounts[index];

    // 타격 한 번. 지금 동작의 판정 범위 안의 캐릭터를 모두 한 번씩 맞힌다. 콜라이더가 여러 개인 캐릭터도 이 타격에서 한 번만 맞힌다.
    // 맞은 결과(피격 · 무적 · 완벽 회피 · 패링)는 캐릭터마다 따로 판정한다.
    //   지원 공격처럼 무적인 이전 캐릭터와 패링 중인 새 캐릭터가 함께 있어도, 새 캐릭터가 따로 판정을 받는다.
    // reaction : 이 타격으로 맞은 캐릭터의 반응. 막타면 동작의 막타 반응이다.
    // 패링당해 공격이 끊기거나 이 동작이 막히면, 남은 캐릭터는 이 타격으로 맞히지 않는다.
    private void HitTargets(EnemyAttackData.HitReaction reaction)
    {
        EnemyAttackData attack = CurrentAttack;
        EnemyAttackStep step = CurrentStep;
        int count = step.Range.Overlap(transform, _hitColliders, _targetLayers);

        _landedTargets.Clear();

        for(int i = 0; i < count; i++)
        {
            PlayerStatus status = _hitColliders[i].GetComponentInParent<PlayerStatus>();

            if(status == null || _landedTargets.Contains(status)) continue;

            _landedTargets.Add(status);
            status.TakeHit(attack, step, reaction, this);

            if(!IsAttacking || _isStepParried) return;
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
