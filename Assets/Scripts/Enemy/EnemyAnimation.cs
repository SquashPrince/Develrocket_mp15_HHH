using UnityEngine;

// 적의 피격 · 그로기 · 처치 모션. 적 상태(EnemyState)의 알림을 받아 Animator 상태를 이름으로 재생한다.
// 모든 적이 같은 상태 이름(Idle · Run · Move · BackStep · Hit · Groggy · Die)을 쓰고, 적마다 Animator Override Controller 로 클립만 바꾼다.
// 경직되면 Hit, 그로기 중 맞으면 Hit 만 재생하고 그로기를 유지, 그로기가 되면 Groggy, 회복하면 Idle, 처치되면 Die 를 재생한다.
// 공격 중 맞았지만 경직 저항으로 경직되지 않았으면 공격 모션이 끊기지 않도록 Hit 을 재생하지 않는다.
// Hit 이 끝난 뒤 Groggy · Idle 중 어디로 돌아갈지는 Animator 의 IsGroggy 파라미터와 전이선이 정한다.
// 완벽 회피 슬로모션 · 필살기 연출 · 맞은 적 히트스톱 동안 함께 느려지거나 멈추도록 Animator 재생 속도를 이 적의 시간 배율에 맞춘다.
[RequireComponent(typeof(EnemyState))]
public class EnemyAnimation : MonoBehaviour
{
    private static readonly int IDLE_ANIMATION = Animator.StringToHash("Idle");
    private static readonly int RUN_ANIMATION = Animator.StringToHash("Run");
    private static readonly int MOVE_ANIMATION = Animator.StringToHash("Move");
    private static readonly int MOVE_X_PARAMETER = Animator.StringToHash("MoveX");
    private static readonly int MOVE_Z_PARAMETER = Animator.StringToHash("MoveZ");
    private static readonly int BACK_STEP_ANIMATION = Animator.StringToHash("BackStep");
    private static readonly int HIT_ANIMATION = Animator.StringToHash("Hit");
    private static readonly int GROGGY_ANIMATION = Animator.StringToHash("Groggy");
    private static readonly int DIE_ANIMATION = Animator.StringToHash("Die");
    private static readonly int IS_GROGGY_PARAMETER = Animator.StringToHash("IsGroggy");

    private const float ANIMATION_FADE_TIME = 0.1f;
    // 방향 값이 바뀔 때 이 시간에 걸쳐 따라가게 해, 방향이 바뀌는 순간 모션이 튀지 않게 한다.
    private const float MOVE_DIRECTION_DAMP_TIME = 0.1f;

    [Tooltip("외형(Visual)의 Animator")]
    [SerializeField] private Animator _animator;

    private EnemyState _state;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindReactionEvent();
    private void OnDisable() => UnbindReactionEvent();
    private void Update() => _animator.speed = _state.Clock.Scale;

    // 이동 기능이 이동을 시작하면 Run 또는 Move, 멈추면 Idle 을 재생한다.
    // isDirectional : 대상을 바라본 채 움직이는 이동(거리 유지)이면 true. 방향 값(MoveX · MoveZ)으로 앞 · 뒤 · 좌 · 우 · 제자리 모션을 섞는 Move 를 재생한다.
    //   false 면 이동 방향을 바라보고 달리는 이동(추적 · 공격 접근)이므로 Run 을 재생한다.
    // 경직 · 그로기 · 처치 중에는 반응 모션을 덮지 않는다. 회복하면 Idle 로 돌아가고, 다시 이동하면 이동 모션을 재생한다.
    public void SetMoving(bool isMoving, bool isDirectional = false)
    {
        if(_state.IsRestricted) return;

        if(!isMoving) Play(IDLE_ANIMATION);
        else Play(isDirectional ? MOVE_ANIMATION : RUN_ANIMATION);
    }

    // 적이 바라보는 방향 기준의 이동 방향을 넘긴다. x 는 오른쪽(+) · 왼쪽(-), z 는 앞(+) · 뒤(-), 멈춰 있으면 (0, 0) 이다.
    public void SetMoveDirection(Vector3 localDirection)
    {
        _animator.SetFloat(MOVE_X_PARAMETER, localDirection.x, MOVE_DIRECTION_DAMP_TIME, _state.Clock.DeltaTime);
        _animator.SetFloat(MOVE_Z_PARAMETER, localDirection.z, MOVE_DIRECTION_DAMP_TIME, _state.Clock.DeltaTime);
    }

    // 공격 단계처럼 다른 기능이 정한 상태를 재생한다.
    public void PlayState(int stateHash) => Play(stateHash);

    // 거리 유지 중 백스텝 모션을 재생한다. 경직 · 그로기 · 처치 중이거나 Animator 에 BackStep 상태가 없으면 재생하지 않고 false 를 돌려준다.
    // 상태가 없을 때 재생하면 끝나지 않는 모션을 기다리게 되므로, 거리 유지가 좌 · 우 이동으로 대신하게 한다.
    public bool TryPlayBackStep()
    {
        if(_state.IsRestricted || !_animator.HasState(0, BACK_STEP_ANIMATION)) return false;

        Play(BACK_STEP_ANIMATION);
        return true;
    }

    public bool IsBackStepFinished() => IsStateFinished(BACK_STEP_ANIMATION);

    // 공격이 끝나 대기 자세로 돌아간다. 경직 · 그로기 · 처치 중에는 반응 모션을 덮지 않는다.
    public void PlayIdle() => SetMoving(false);

    // 지금 재생 중인 상태가 stateHash 이고 끝까지 재생되었으면 true 다. 다른 상태와 섞이는 중이면 false 다.
    public bool IsStateFinished(int stateHash)
    {
        if(_animator.IsInTransition(0)) return false;

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        return stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 1f;
    }

    // stateHash 상태의 재생 위치(0 = 처음, 1 = 끝)를 돌려준다. 그 상태로 섞여 들어가는 중이면 들어가는 쪽의 위치다.
    // 지금 재생 중이거나 섞여 들어가는 상태가 아니면 false 다.
    public bool TryGetStateNormalizedTime(int stateHash, out float normalizedTime)
    {
        AnimatorStateInfo stateInfo = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
        normalizedTime = stateInfo.normalizedTime;

        return stateInfo.shortNameHash == stateHash;
    }

    // 그로기 중 맞으면 그로기를 유지한 채 Hit 만 재생한다. 그로기가 아닐 때의 Hit 은 경직 알림에서 재생한다.
    private void OnDamaged(bool isImpact)
    {
        if(_state.IsGroggy) Play(HIT_ANIMATION);
    }

    private void OnDied() => Play(DIE_ANIMATION);

    private void OnStaggered() => Play(HIT_ANIMATION);

    private void OnGroggyEntered()
    {
        _animator.SetBool(IS_GROGGY_PARAMETER, true);
        Play(GROGGY_ANIMATION);
    }

    private void OnRecovered()
    {
        _animator.SetBool(IS_GROGGY_PARAMETER, false);
        Play(IDLE_ANIMATION);
    }

    // 처치된 뒤에는 그로기 · 경직이 끝나는 알림이 와도 Die 를 유지한다.
    private void Play(int stateHash)
    {
        if(!_state.IsAlive && stateHash != DIE_ANIMATION) return;

        _animator.CrossFadeInFixedTime(stateHash, ANIMATION_FADE_TIME);
    }

    private void CacheComponent()
    {
        _state = GetComponent<EnemyState>();
    }

    private void BindReactionEvent()
    {
        _state.Damaged += OnDamaged;
        _state.Died += OnDied;
        _state.Staggered += OnStaggered;
        _state.GroggyEntered += OnGroggyEntered;
        _state.Recovered += OnRecovered;
    }

    private void UnbindReactionEvent()
    {
        _state.Damaged -= OnDamaged;
        _state.Died -= OnDied;
        _state.Staggered -= OnStaggered;
        _state.GroggyEntered -= OnGroggyEntered;
        _state.Recovered -= OnRecovered;
    }
}
