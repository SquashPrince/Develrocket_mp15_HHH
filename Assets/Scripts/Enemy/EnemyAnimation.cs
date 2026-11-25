using UnityEngine;

// 적 모션 재생. EnemyState 의 알림을 받아 반응 모션을 재생하고, 재생 속도를 이 적의 시간 배율에 맞춘다.
// 모든 적이 같은 상태 이름을 쓰고, 적마다 Animator Override Controller 로 클립만 바꾼다.
// Hit 뒤 Groggy · Idle 중 어디로 돌아갈지는 Animator 의 IsGroggy 파라미터와 전이선이 정한다.
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

    // isDirectional : 대상을 바라본 채 움직이면(거리 유지) 방향 값으로 섞는 Move, 아니면 Run 을 재생한다.
    // 경직 · 그로기 · 처치 중에는 반응 모션을 덮지 않는다.
    public void SetMoving(bool isMoving, bool isDirectional = false)
    {
        if(_state.IsRestricted) return;

        if(!isMoving) Play(IDLE_ANIMATION);
        else Play(isDirectional ? MOVE_ANIMATION : RUN_ANIMATION);
    }

    // 적이 바라보는 방향 기준. x 는 오른쪽(+), z 는 앞(+)
    public void SetMoveDirection(Vector3 localDirection)
    {
        _animator.SetFloat(MOVE_X_PARAMETER, localDirection.x, MOVE_DIRECTION_DAMP_TIME, _state.Clock.DeltaTime);
        _animator.SetFloat(MOVE_Z_PARAMETER, localDirection.z, MOVE_DIRECTION_DAMP_TIME, _state.Clock.DeltaTime);
    }

    public void PlayState(int stateHash) => Play(stateHash);

    // BackStep 상태가 없는데 재생하면 끝나지 않는 모션을 기다리게 되므로 false 를 돌려 좌 · 우 이동으로 대신하게 한다.
    public bool TryPlayBackStep()
    {
        if(_state.IsRestricted || !_animator.HasState(0, BACK_STEP_ANIMATION)) return false;

        Play(BACK_STEP_ANIMATION);
        return true;
    }

    public bool IsBackStepFinished() => IsStateFinished(BACK_STEP_ANIMATION);

    public void PlayIdle() => SetMoving(false);

    public bool IsStateFinished(int stateHash) => _animator.IsStateFinished(stateHash);

    // 그 상태로 섞여 들어가는 중이면 들어가는 쪽의 위치다.
    public bool TryGetStateNormalizedTime(int stateHash, out float normalizedTime)
    {
        AnimatorStateInfo stateInfo = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
        normalizedTime = stateInfo.normalizedTime;

        return stateInfo.shortNameHash == stateHash;
    }

    // 그로기가 아닐 때의 Hit 은 경직 알림에서 재생한다. 경직 저항으로 버틴 공격 모션은 끊지 않는다.
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
