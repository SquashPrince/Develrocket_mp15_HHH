using UnityEngine;

// 플레이어 모델의 Animator 제어. 상태 재생, 이동 속도 파라미터, 하체 달리기 레이어를 맡는다.
// 어떤 모션을 재생할지는 상태 클래스가 정하고, 이 기능은 Animator 를 다루는 방법만 맡는다.
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int SPEED_PARAMETER = Animator.StringToHash("Speed");
    private static readonly int LEG_RUN_ANIMATION = Animator.StringToHash("LegRun");

    // Animator Controller 의 하체 달리기 레이어 이름과 같아야 한다. 하체만 덮는 Avatar Mask 를 씌운 레이어다.
    private const string LEG_RUN_LAYER_NAME = "LegRun";

    // 애니메이션을 바꿀 때 이전 모션과 섞는 시간(초)
    public const float DEFAULT_FADE_TIME = 0.1f;
    // 이동 속도 파라미터가 목표값까지 부드럽게 따라가는 시간(초)
    private const float SPEED_DAMP_TIME = 0.1f;

    [Tooltip("캐릭터 모델의 Animator (Visual 아래 캐릭터 모델)")]
    [SerializeField] private Animator _animator;

    private PlayerController _controller;
    private float _legRunTargetWeight;
    private int _legRunLayer;

    private void Awake() => CacheComponent();
    private void Update()
    {
        ApplyTimeScale();
        UpdateLegRunWeight();
    }

    // Animator 의 상태를 이름(해시)으로 직접 재생한다. 전이선 대신 코드가 어떤 모션을 재생할지 정한다.
    // fadeTime : 이전 모션과 섞는 시간(초). 공격은 공격 설정의 값을 넘기고, 그 외에는 기본값을 쓴다.
    public void Play(int stateHash, float fadeTime = DEFAULT_FADE_TIME)
    {
        _animator.CrossFadeInFixedTime(stateHash, fadeTime);
    }

    // 실제 수평 이동 속도를 넘겨 Blend Tree 가 걷기 · 달리기를 섞게 한다.
    public void SetMoveSpeed(float speed)
    {
        _animator.SetFloat(SPEED_PARAMETER, speed, SPEED_DAMP_TIME, _controller.DeltaTime);
    }

    // 공격 대상에게 멀리 다가가는 동안 하체에 달리기 모션을 덮는다. 상체는 공격 모션을 그대로 재생한다.
    // 레이어 비중은 즉시 바꾸지 않고 섞는 시간에 걸쳐 따라가게 해 모션이 튀지 않게 한다.
    public void SetLegRun(bool isRunning)
    {
        // 꺼져 있다가 켜질 때만 달리기를 처음부터 재생한다. 콤보가 이어지며 다시 켜질 때 발이 끊기지 않게 하기 위해서다.
        if(isRunning && _legRunTargetWeight == 0f) _animator.Play(LEG_RUN_ANIMATION, _legRunLayer, 0f);

        _legRunTargetWeight = isRunning ? 1f : 0f;
    }

    // 모델의 재생 속도를 캐릭터 시간 배율에 맞춘다. 히트렉으로 배율이 0 이면 모션이 멈추고, 애니메이션 이벤트도 함께 멈춘다.
    private void ApplyTimeScale() => _animator.speed = _controller.Status.TimeScale;

    private void UpdateLegRunWeight()
    {
        float weight = Mathf.MoveTowards(_animator.GetLayerWeight(_legRunLayer), _legRunTargetWeight, _controller.DeltaTime / DEFAULT_FADE_TIME);

        _animator.SetLayerWeight(_legRunLayer, weight);
    }

    // 하체 달리기 레이어 번호는 다른 기능이 Start 에서 모션을 재생하기 전에 필요하므로 Awake 에서 찾아 둔다.
    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _legRunLayer = _animator.GetLayerIndex(LEG_RUN_LAYER_NAME);
    }
}
