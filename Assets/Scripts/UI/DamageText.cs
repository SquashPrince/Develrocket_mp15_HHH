using TMPro;
using UnityEngine;

// 대미지 숫자 하나. 처음 맞은 자리에서 떠오르며 흐려지고, 다 되면 꺼져 다시 쓰인다.
// 거리와 상관없이 같은 크기로 읽히게 화면 공간 Canvas 위에서 월드 위치를 화면 좌표로 바꿔 따라간다.
// 숫자마다 Update 를 돌리지 않도록 갱신은 DamageTextSpawner 가 모아 부른다.
[RequireComponent(typeof(TMP_Text))]
public class DamageText : MonoBehaviour
{
    [Tooltip("떠오르며 사라지는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _lifetime = 0.8f;
    [Tooltip("사라질 때까지 떠오르는 높이(m, 월드)")]
    [SerializeField, Min(0f)] private float _riseHeight = 1f;
    [Tooltip("[선택] 일반 숫자의 색 그라데이션 (Create > TextMeshPro > Color Gradient). 비우면 흰색")]
    [SerializeField] private TMP_ColorGradient _normalGradient;
    [Tooltip("[선택] 강타 숫자의 색 그라데이션. 비우면 흰색")]
    [SerializeField] private TMP_ColorGradient _impactGradient;
    [Tooltip("강타 숫자의 크기 배율")]
    [SerializeField, Min(1f)] private float _impactScale = 1.4f;
    [Tooltip("[선택] 크리티컬 숫자의 색 그라데이션. 비우면 흰색")]
    [SerializeField] private TMP_ColorGradient _criticalGradient;
    [Tooltip("크리티컬 숫자의 크기 배율")]
    [SerializeField, Min(1f)] private float _criticalScale = 1.3f;

    private TMP_Text _text;
    private Camera _camera;
    private Vector3 _worldPosition;
    private Vector3 _baseScale;
    private float _timer;
    private float _amount;

    // 합치기에서 같은 적인지 볼 때 쓴다.
    public Transform Target { get; private set; }
    // 마지막으로 띄우거나 더한 뒤 지난 시간
    public float Age => _timer;

    private void Awake() => CacheComponent();

    // 꺼진 채 만들어져 아직 Awake 되지 않은 숫자도 바로 쓸 수 있게 먼저 켠다.
    public void Show(Camera camera, Transform target, Vector3 worldPosition, float amount, bool isImpact, bool isCritical)
    {
        gameObject.SetActive(true);
        _camera = camera;
        Target = target;
        _worldPosition = worldPosition;
        _amount = 0f;
        Add(amount, isImpact, isCritical);
    }

    // 처음부터 다시 떠오르고, 색 · 크기는 마지막 타격을 따른다.
    public void Add(float amount, bool isImpact, bool isCritical)
    {
        _amount += amount;
        _timer = 0f;
        TMP_ColorGradient gradient = isCritical ? _criticalGradient : isImpact ? _impactGradient : _normalGradient;

        // 그라데이션에 글자 색이 곱해지므로 글자 색은 흰색으로 두고, 흐려지기는 투명도만 바꾼다.
        _text.colorGradientPreset = gradient;
        _text.enableVertexGradient = gradient != null;
        // SetText 는 숫자가 뜰 때마다 문자열을 만들지 않는다.
        _text.SetText("{0}", Mathf.RoundToInt(_amount));
        _text.color = Color.white;
        transform.localScale = _baseScale * (isImpact ? _impactScale : 1f) * (isCritical ? _criticalScale : 1f);
        UpdatePosition(0f);
    }

    // 게임 시간으로 받아 일시정지 중에는 멈춘다. 다 떠올랐으면 true.
    public bool Tick(float deltaTime)
    {
        _timer += deltaTime;

        float rate = _timer / _lifetime;

        if(rate >= 1f)
        {
            Hide();
            return true;
        }

        UpdatePosition(rate);

        // 앞쪽 절반은 그대로 보이고, 뒤쪽 절반 동안 흐려진다.
        _text.alpha = 1f - Mathf.Clamp01((rate - 0.5f) * 2f);
        return false;
    }

    public void Hide()
    {
        Target = null;
        gameObject.SetActive(false);
    }

    // 카메라 뒤쪽 위치는 그리지 않는다.
    private void UpdatePosition(float rate)
    {
        Vector3 screenPoint = _camera.WorldToScreenPoint(_worldPosition + Vector3.up * (_riseHeight * rate));

        _text.enabled = screenPoint.z > 0f;
        transform.position = screenPoint;
    }

    private void CacheComponent()
    {
        _text = GetComponent<TMP_Text>();
        _baseScale = transform.localScale;
    }
}
