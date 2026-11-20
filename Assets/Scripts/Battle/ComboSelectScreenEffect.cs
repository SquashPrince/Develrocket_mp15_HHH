using UnityEngine;
using UnityEngine.Rendering;

// 콤보 스킬 캐릭터 선택 동안 화면 효과를 켠다. 색 번짐(같은 오브젝트의 Volume)과 화면 왜곡(카메라 앞에 놓은 왜곡 Renderer)을 함께 쓴다.
// 선택이 열리면 효과 비율을 1 로, 닫히면 0 으로 서서히 바꾼다. 연계로 선택이 다시 열리면 그 자리에서 다시 1 로 간다.
// 콤보 선택 중에는 게임 시간이 느려지므로 전환은 실제 시간으로 잰다.
// 색 번짐 : Volume 의 Weight 를 비율로 바꾼다. 효과의 종류 · 세기는 Volume Profile 에서 정한다.
// 왜곡 : 왜곡 세기(_Distorsion)를 비율 × 최대 왜곡 세기로 넣는다. MaterialPropertyBlock 으로 넣어 Material 에셋은 바꾸지 않는다.
//   비율이 0 이면 Renderer 를 꺼, 효과가 없는 동안 화면을 한 번 더 그리지 않게 한다.
//   왜곡 셰이더는 화면 색을 읽으므로 URP 설정의 Opaque Texture 가 켜져 있어야 한다.
[RequireComponent(typeof(Volume))]
public class ComboSelectScreenEffect : MonoBehaviour
{
    private static readonly int DISTORTION_ID = Shader.PropertyToID("_Distorsion");

    [Tooltip("카메라 앞에 놓은 화면 왜곡 Renderer (Distorsion 프리팹)")]
    [SerializeField] private Renderer _distortionRenderer;
    [Tooltip("효과가 다 켜졌을 때의 왜곡 세기")]
    [SerializeField, Min(0f)] private float _maxDistortion = 0.14f;
    [Tooltip("효과가 켜지는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _fadeInTime = 0.2f;
    [Tooltip("효과가 사라지는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _fadeOutTime = 0.3f;

    private MaterialPropertyBlock _propertyBlock;
    private Volume _volume;
    // 효과 비율(0 = 꺼짐, 1 = 다 켜짐)과 목표
    private float _rate;
    private float _target;

    private void Awake()
    {
        CacheComponent();
        Init();
    }

    private void OnEnable() => BindFeedbackEvent();
    private void OnDisable() => UnbindFeedbackEvent();
    private void Update() => UpdateEffect();

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        if(moment == BattleFeedback.Moment.ComboSelectOpened) _target = 1f;
        else if(moment == BattleFeedback.Moment.ComboSelectClosed) _target = 0f;
    }

    private void UpdateEffect()
    {
        if(_rate == _target) return;

        float speed = 1f / (_target > _rate ? _fadeInTime : _fadeOutTime);

        _rate = Mathf.MoveTowards(_rate, _target, speed * Time.unscaledDeltaTime);
        ApplyRate();
    }

    private void ApplyRate()
    {
        _volume.weight = _rate;
        _distortionRenderer.enabled = _rate > 0f;

        if(_rate <= 0f) return;

        _distortionRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(DISTORTION_ID, _rate * _maxDistortion);
        _distortionRenderer.SetPropertyBlock(_propertyBlock);
    }

    private void CacheComponent()
    {
        _volume = GetComponent<Volume>();
    }

    // 씬에 저장된 Weight · Renderer 상태와 상관없이 효과가 꺼진 채로 시작한다.
    private void Init()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _rate = 0f;
        _target = 0f;
        ApplyRate();
    }

    private void BindFeedbackEvent()
    {
        BattleFeedback.Occurred += OnMoment;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.Occurred -= OnMoment;
    }
}
