using TMPro;
using UnityEngine;

// 대미지 숫자 하나. 맞은 위치(월드)에서 위로 떠오르며 흐려지고, 시간이 다 되면 꺼져 다음에 다시 쓰인다.
// 화면 공간 Canvas 위에 있고, 매 프레임 월드 위치를 화면 좌표로 바꿔 따라간다. 거리와 상관없이 같은 크기로 읽히게 하기 위해서다.
// 위치는 처음 맞은 순간의 값을 쓴다. 적이 움직여도 숫자는 맞은 자리에서 떠오른다.
// 카메라 뒤쪽 위치는 화면에 그리지 않는다.
// 강타는 크기 · 색 그라데이션을 다르게 한다. 크리티컬은 크리티컬 그라데이션을 쓰고 크기를 더 키운다(강타 크리티컬은 두 배율을 곱한다).
// 합치기 : 같은 적이 짧은 사이에 다시 맞으면 새 숫자 대신 이 숫자에 더한다. 떠오르기를 처음부터 다시 하고, 색 · 크기는 마지막 타격(일반 · 강타 · 크리티컬)을 따른다.
// 갱신은 숫자 표시 기능이 떠 있는 숫자만 모아 부른다. 숫자마다 Update 를 돌리지 않기 위해서다.
// 게임 시간으로 움직여 일시정지 중에는 멈춘다.
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

    // 이 숫자가 띄워진 적. 합치기에서 같은 적인지 볼 때 쓴다.
    public Transform Target { get; private set; }
    // 마지막으로 숫자를 띄우거나 더한 뒤 지난 시간(초)
    public float Age => _timer;

    private void Awake() => CacheComponent();

    // worldPosition 에서 amount 를 띄운다. 숫자는 반올림한 정수로 보여준다.
    // 먼저 켜서, 꺼진 채 만들어져 아직 준비(Awake)되지 않은 숫자도 바로 쓸 수 있게 한다.
    public void Show(Camera camera, Transform target, Vector3 worldPosition, float amount, bool isImpact, bool isCritical)
    {
        gameObject.SetActive(true);
        _camera = camera;
        Target = target;
        _worldPosition = worldPosition;
        _amount = 0f;
        Add(amount, isImpact, isCritical);
    }

    // 떠 있는 숫자에 대미지를 더하고 처음부터 다시 떠오른다.
    // 색 · 크기는 마지막 타격을 따른다. 앞 타격이 크리티컬이어도 마지막이 일반이면 일반 모양이다.
    public void Add(float amount, bool isImpact, bool isCritical)
    {
        _amount += amount;
        _timer = 0f;
        TMP_ColorGradient gradient = isCritical ? _criticalGradient : isImpact ? _impactGradient : _normalGradient;

        // 그라데이션에 글자 색이 곱해지므로 글자 색은 흰색으로 두고, 흐려지기는 투명도만 바꾼다.
        _text.colorGradientPreset = gradient;
        _text.enableVertexGradient = gradient != null;
        // 숫자를 문자열로 바꾸지 않고 바로 넣어, 숫자가 뜰 때마다 문자열이 새로 생기지 않게 한다.
        _text.SetText("{0}", Mathf.RoundToInt(_amount));
        _text.color = Color.white;
        transform.localScale = _baseScale * (isImpact ? _impactScale : 1f) * (isCritical ? _criticalScale : 1f);
        UpdatePosition(0f);
    }

    // 시간을 진행한다. 다 떠올랐으면 끄고 true 를 돌려준다.
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
