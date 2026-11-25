using UnityEngine;
using UnityEngine.UI;

// 체력 막대가 줄어든 만큼을 잠깐 남겨 보여주는 잔상 막대. 머물렀다가 일정한 속도로 내려와 값을 따라간다.
// 화면 연출이라 느려진 게임 시간에 영향받지 않게 실제 시간으로 흐른다.
[RequireComponent(typeof(Slider))]
public class GaugeTrail : MonoBehaviour
{
    [Tooltip("값이 줄어든 뒤 잔상이 그 자리에 머무는 시간(초, 실제 시간). 값이 다시 줄면 처음부터 센다")]
    [SerializeField, Min(0f)] private float _holdTime = 0.5f;
    [Tooltip("잔상이 내려오는 속도(초당 막대 비율). 1 이면 가득 찬 막대가 1 초 만에 빈다")]
    [SerializeField, Min(0.01f)] private float _dropSpeed = 1f;

    private Slider _slider;
    private float _targetValue;
    private float _holdTimer;

    private void Awake() => CacheComponent();
    private void Update() => UpdateTrail();

    // 줄 때마다 머무는 시간을 처음부터 세어, 연타 중에는 쌓였다가 끝나면 내려온다. 늘면 바로 따라간다.
    public void SetValue(float value)
    {
        _targetValue = value;

        if(value < _slider.value) _holdTimer = _holdTime;
        else _slider.value = value;
    }

    // 보여줄 대상이 바뀌거나 처음 그릴 때 잔상 없이 바로 맞춘다.
    public void Snap(float value)
    {
        _targetValue = value;
        _holdTimer = 0f;
        _slider.value = value;
    }

    private void UpdateTrail()
    {
        if(_slider.value <= _targetValue) return;

        if(_holdTimer > 0f)
        {
            _holdTimer -= Time.unscaledDeltaTime;
            return;
        }

        _slider.value = Mathf.MoveTowards(_slider.value, _targetValue, _dropSpeed * Time.unscaledDeltaTime);
    }

    private void CacheComponent()
    {
        _slider = GetComponent<Slider>();
    }
}
