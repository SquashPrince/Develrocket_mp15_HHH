using TMPro;
using UnityEngine;

// 대미지 숫자 하나. 맞은 위치(월드)에서 위로 떠오르며 흐려지고, 시간이 다 되면 꺼져 다음에 다시 쓰인다.
// 화면 공간 Canvas 위에 있고, 매 프레임 월드 위치를 화면 좌표로 바꿔 따라간다. 거리와 상관없이 같은 크기로 읽히게 하기 위해서다.
// 위치는 맞은 순간의 값을 쓴다. 적이 움직여도 숫자는 맞은 자리에서 떠오른다.
// 카메라 뒤쪽 위치는 화면에 그리지 않는다.
// 강타는 크기 · 색을 다르게 한다. 크리티컬은 크리티컬 색을 쓰고 크기를 더 키운다(강타 크리티컬은 두 배율을 곱한다).
// 게임 시간으로 움직여 일시정지 중에는 멈춘다.
[RequireComponent(typeof(TMP_Text))]
public class DamageText : MonoBehaviour
{
    [Tooltip("떠오르며 사라지는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _lifetime = 0.8f;
    [Tooltip("사라질 때까지 떠오르는 높이(m, 월드)")]
    [SerializeField, Min(0f)] private float _riseHeight = 1f;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _impactColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("강타 숫자의 크기 배율")]
    [SerializeField, Min(1f)] private float _impactScale = 1.4f;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.3f, 0.2f);
    [Tooltip("크리티컬 숫자의 크기 배율")]
    [SerializeField, Min(1f)] private float _criticalScale = 1.3f;

    private TMP_Text _text;
    private Camera _camera;
    private Vector3 _worldPosition;
    private Vector3 _baseScale;
    private Color _color;
    private float _timer;

    private void Awake() => CacheComponent();
    private void Update() => UpdateFloat();

    // worldPosition 에서 amount 를 띄운다. 숫자는 반올림한 정수로 보여준다.
    // 먼저 켜서, 꺼진 채 만들어져 아직 준비(Awake)되지 않은 숫자도 바로 쓸 수 있게 한다.
    public void Show(Camera camera, Vector3 worldPosition, float amount, bool isImpact, bool isCritical)
    {
        gameObject.SetActive(true);
        _camera = camera;
        _worldPosition = worldPosition;
        _timer = 0f;
        _color = isCritical ? _criticalColor : isImpact ? _impactColor : _normalColor;
        _text.text = Mathf.RoundToInt(amount).ToString();
        _text.color = _color;
        transform.localScale = _baseScale * (isImpact ? _impactScale : 1f) * (isCritical ? _criticalScale : 1f);
        UpdatePosition(0f);
    }

    private void UpdateFloat()
    {
        _timer += Time.deltaTime;

        float rate = _timer / _lifetime;

        if(rate >= 1f)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdatePosition(rate);

        // 앞쪽 절반은 그대로 보이고, 뒤쪽 절반 동안 흐려진다.
        Color color = _color;
        color.a = 1f - Mathf.Clamp01((rate - 0.5f) * 2f);
        _text.color = color;
    }

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
