using UnityEngine;

// 플레이어 모델의 Animator 제어. 무엇을 재생할지는 상태 클래스가 정하고, 이 기능은 재생 방법만 맡는다.
// 부위 덮기 : 공용 모션에서 무기를 든 자세처럼 캐릭터에 맞지 않는 부위만, 마스크 레이어에서 캐릭터 클립(Override)으로 덮는다.
public class PlayerAnimation : MonoBehaviour
{
    public enum BodyPart
    {
        None,
        LowerBody,
        UpperBody,
        FullBody
    }

    private static readonly int SPEED_PARAMETER = Animator.StringToHash("Speed");
    private static readonly int LEG_RUN_ANIMATION = Animator.StringToHash("LegRun");

    // Animator Controller 의 레이어 이름과 같아야 한다.
    private const string LEG_RUN_LAYER_NAME = "LegRun";
    private const string LOWER_BODY_LAYER_NAME = "LowerBody";
    private const string UPPER_BODY_LAYER_NAME = "UpperBody";

    public const float DEFAULT_FADE_TIME = 0.1f;
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

    // 부위 덮기를 쓰는 상태는 나갈 때 EndBodyPart 를 불러 레이어를 끈다.
    public void PlayOnBodyPart(int stateHash, BodyPart coverPart)
    {
        EndBodyPart();
        Play(stateHash);

        if(coverPart == BodyPart.LowerBody || coverPart == BodyPart.FullBody) CoverLayer(_lowerBodyLayer, stateHash);
        if(coverPart == BodyPart.UpperBody || coverPart == BodyPart.FullBody) CoverLayer(_upperBodyLayer, stateHash);
    }

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

    // 전이선 대신 코드가 어떤 모션을 재생할지 정한다.
    public void Play(int stateHash, float fadeTime = DEFAULT_FADE_TIME)
    {
        _animator.CrossFadeInFixedTime(stateHash, fadeTime);
    }

    public bool IsStateFinished(int stateHash) => _animator.IsStateFinished(stateHash);

    public void SetMoveSpeed(float speed)
    {
        _animator.SetFloat(SPEED_PARAMETER, speed, SPEED_DAMP_TIME, _controller.DeltaTime);
    }

    // 공격 대상에게 멀리 다가가는 동안 하체에만 달리기 모션을 덮는다. 비중은 섞는 시간에 걸쳐 바꿔 모션이 튀지 않게 한다.
    public void SetLegRun(bool isRunning)
    {
        // 콤보가 이어지며 다시 켜질 때 발이 끊기지 않게, 꺼져 있다가 켜질 때만 처음부터 재생한다.
        if(isRunning && _legRunTargetWeight == 0f) _animator.Play(LEG_RUN_ANIMATION, _legRunLayer, 0f);

        _legRunTargetWeight = isRunning ? 1f : 0f;
    }

    // 히트렉으로 배율이 0 이면 모션과 애니메이션 이벤트가 함께 멈춘다.
    private void ApplyTimeScale() => _animator.speed = _controller.Status.TimeScale;

    private void UpdateLegRunWeight()
    {
        float weight = Mathf.MoveTowards(_animator.GetLayerWeight(_legRunLayer), _legRunTargetWeight, _controller.DeltaTime / DEFAULT_FADE_TIME);

        _animator.SetLayerWeight(_legRunLayer, weight);
    }

    // 다른 기능이 Start 에서 모션을 재생하기 전에 레이어 번호가 필요해 Awake 에서 찾는다.
    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
        _legRunLayer = _animator.GetLayerIndex(LEG_RUN_LAYER_NAME);
        _lowerBodyLayer = _animator.GetLayerIndex(LOWER_BODY_LAYER_NAME);
        _upperBodyLayer = _animator.GetLayerIndex(UPPER_BODY_LAYER_NAME);
    }
}
