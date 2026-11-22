using UnityEngine;

// 플레이어 모델의 Animator 제어. 상태 재생, 이동 속도 파라미터, 하체 달리기 레이어를 맡는다.
// 어떤 모션을 재생할지는 상태 클래스가 정하고, 이 기능은 Animator 를 다루는 방법만 맡는다.
// 부위 덮기 : 기본 레이어에서 모션을 재생하는 동안, 고른 부위는 그 부위만 덮는 마스크 레이어에서 같은 이름의 상태를 함께 재생한다.
//   마스크 레이어의 상태에는 기본 레이어와 다른 클립을 넣어 두고 캐릭터별 Override 로 바꿔 끼운다.
//   여러 캐릭터가 함께 쓰는 공용 모션에서, 무기를 든 자세처럼 캐릭터에 맞지 않는 부위만 캐릭터 클립으로 덮기 위해서다.
public class PlayerAnimation : MonoBehaviour
{
    // 캐릭터 클립으로 덮을 몸 부위. 없음이면 기본 레이어의 모션만 재생한다.
    public enum BodyPart
    {
        None,
        LowerBody,
        UpperBody,
        FullBody
    }

    private static readonly int SPEED_PARAMETER = Animator.StringToHash("Speed");
    private static readonly int LEG_RUN_ANIMATION = Animator.StringToHash("LegRun");

    // Animator Controller 의 하체 달리기 레이어 이름과 같아야 한다. 하체만 덮는 Avatar Mask 를 씌운 레이어다.
    private const string LEG_RUN_LAYER_NAME = "LegRun";
    // Animator Controller 의 부위 덮기 레이어 이름과 같아야 한다. 각각 하체 · 상체만 덮는 Avatar Mask 를 씌운 레이어다.
    private const string LOWER_BODY_LAYER_NAME = "LowerBody";
    private const string UPPER_BODY_LAYER_NAME = "UpperBody";

    // 애니메이션을 바꿀 때 이전 모션과 섞는 시간(초)
    public const float DEFAULT_FADE_TIME = 0.1f;
    // 이동 속도 파라미터가 목표값까지 부드럽게 따라가는 시간(초)
    private const float SPEED_DAMP_TIME = 0.1f;

    [Tooltip("캐릭터 모델의 Animator (Visual 아래 캐릭터 모델)")]
    [SerializeField] private Animator _animator;

    private PlayerController _controller;
    private float _legRunTargetWeight;
    private int _legRunLayer;
    private int _lowerBodyLayer;
    private int _upperBodyLayer;

    private void Awake() => CacheComponent();
    private void Update()
    {
        ApplyTimeScale();
        UpdateLegRunWeight();
    }

    // 기본 레이어에서 stateHash 모션을 재생하고, coverPart 에 해당하는 마스크 레이어를 켜 같은 상태를 함께 재생한다.
    // 하체 · 상체는 그 레이어 하나, 전신은 두 레이어를 모두 켠다. 없음이면 기본 레이어만 재생한다.
    // 부위 덮기를 쓰는 상태는 나갈 때 EndBodyPart 를 불러 레이어를 끈다.
    public void PlayOnBodyPart(int stateHash, BodyPart coverPart)
    {
        EndBodyPart();
        Play(stateHash);

        if(coverPart == BodyPart.LowerBody || coverPart == BodyPart.FullBody) CoverLayer(_lowerBodyLayer, stateHash);
        if(coverPart == BodyPart.UpperBody || coverPart == BodyPart.FullBody) CoverLayer(_upperBodyLayer, stateHash);
    }

    // 부위 덮기 레이어를 모두 끈다.
    public void EndBodyPart()
    {
        _animator.SetLayerWeight(_lowerBodyLayer, 0f);
        _animator.SetLayerWeight(_upperBodyLayer, 0f);
    }

    private void CoverLayer(int layer, int stateHash)
    {
        _animator.CrossFadeInFixedTime(stateHash, DEFAULT_FADE_TIME, layer);
        _animator.SetLayerWeight(layer, 1f);
    }

    // Animator 의 상태를 이름(해시)으로 직접 재생한다. 전이선 대신 코드가 어떤 모션을 재생할지 정한다.
    // fadeTime : 이전 모션과 섞는 시간(초). 공격은 공격 설정의 값을 넘기고, 그 외에는 기본값을 쓴다.
    public void Play(int stateHash, float fadeTime = DEFAULT_FADE_TIME)
    {
        _animator.CrossFadeInFixedTime(stateHash, fadeTime);
    }

    // 기본 레이어에서 지금 재생 중인 상태가 stateHash 이고 끝까지 재생되었으면 true 다. 다른 상태와 섞이는 중이면 false 다.
    public bool IsStateFinished(int stateHash) => _animator.IsStateFinished(stateHash);

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
        _lowerBodyLayer = _animator.GetLayerIndex(LOWER_BODY_LAYER_NAME);
        _upperBodyLayer = _animator.GetLayerIndex(UPPER_BODY_LAYER_NAME);
    }
}
