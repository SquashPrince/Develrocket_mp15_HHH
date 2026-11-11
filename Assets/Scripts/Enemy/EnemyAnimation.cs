using UnityEngine;

// 적의 피격 · 그로기 · 처치 모션. 적 체력 · 적 반응의 알림을 받아 Animator 상태를 이름으로 재생한다.
// 모든 적이 같은 상태 이름(Idle · Hit · Groggy · Die)을 쓰고, 적마다 Animator Override Controller 로 클립만 바꾼다.
// 경직되면 Hit, 그로기 중 맞으면 Hit 만 재생하고 그로기를 유지, 그로기가 되면 Groggy, 회복하면 Idle, 처치되면 Die 를 재생한다.
// 공격 중 맞았지만 경직 저항으로 경직되지 않았으면 공격 모션이 끊기지 않도록 Hit 을 재생하지 않는다.
// Hit 이 끝난 뒤 Groggy · Idle 중 어디로 돌아갈지는 Animator 의 IsGroggy 파라미터와 전이선이 정한다.
// 완벽 회피 슬로모션 · 필살기 연출 동안 함께 느려지거나 멈추도록 Animator 재생 속도를 적 전용 시간 배율에 맞춘다.
[RequireComponent(typeof(EnemyHealth), typeof(EnemyReaction))]
public class EnemyAnimation : MonoBehaviour
{
    private static readonly int IDLE_ANIMATION = Animator.StringToHash("Idle");
    private static readonly int HIT_ANIMATION = Animator.StringToHash("Hit");
    private static readonly int GROGGY_ANIMATION = Animator.StringToHash("Groggy");
    private static readonly int DIE_ANIMATION = Animator.StringToHash("Die");
    private static readonly int IS_GROGGY_PARAMETER = Animator.StringToHash("IsGroggy");

    private const float ANIMATION_FADE_TIME = 0.1f;

    [Tooltip("외형(Visual)의 Animator")]
    [SerializeField] private Animator _animator;

    private EnemyHealth _health;
    private EnemyReaction _reaction;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindReactionEvent();
    private void OnDisable() => UnbindReactionEvent();
    private void Update() => _animator.speed = EnemyTime.Scale;

    // 그로기 중 맞으면 그로기를 유지한 채 Hit 만 재생한다. 그로기가 아닐 때의 Hit 은 경직 알림에서 재생한다.
    private void OnDamaged(bool isImpact)
    {
        if(_reaction.IsGroggy) Play(HIT_ANIMATION);
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
        if(!_health.IsAlive && stateHash != DIE_ANIMATION) return;

        _animator.CrossFadeInFixedTime(stateHash, ANIMATION_FADE_TIME);
    }

    private void CacheComponent()
    {
        _health = GetComponent<EnemyHealth>();
        _reaction = GetComponent<EnemyReaction>();
    }

    private void BindReactionEvent()
    {
        _health.Damaged += OnDamaged;
        _health.Died += OnDied;
        _reaction.Staggered += OnStaggered;
        _reaction.GroggyEntered += OnGroggyEntered;
        _reaction.Recovered += OnRecovered;
    }

    private void UnbindReactionEvent()
    {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
        _reaction.Staggered -= OnStaggered;
        _reaction.GroggyEntered -= OnGroggyEntered;
        _reaction.Recovered -= OnRecovered;
    }
}
