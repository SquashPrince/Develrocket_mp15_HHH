using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬라이더의 현재 값에 배율을 곱해 형식대로 쓴다. 예 : 볼륨은 배율 100 · 형식 "0'%'" → 80%
// 설정 화면은 알림 없이 값을 맞춘 뒤 켜므로, 켜질 때도 한 번 쓴다.
[RequireComponent(typeof(TMP_Text))]
public class SliderValueText : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [Tooltip("값에 곱할 배율. 0 ~ 1 볼륨을 퍼센트로 보이려면 100")]
    [SerializeField] private float _multiplier = 1f;
    [Tooltip("숫자 형식. 예 : \"0.00\" (소수 둘째 자리), \"0'%'\" (정수 + %)")]
    [SerializeField] private string _format = "0.00";

    private TMP_Text _text;

    private void Awake() => CacheComponent();
    private void OnEnable()
    {
        BindSliderEvent();
        Refresh(_slider.value);
    }
    private void OnDisable() => UnbindSliderEvent();

    private void Refresh(float value)
    {
        _text.text = (value * _multiplier).ToString(_format);
    }

    private void CacheComponent()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void BindSliderEvent()
    {
        _slider.onValueChanged.AddListener(Refresh);
    }

    private void UnbindSliderEvent()
    {
        _slider.onValueChanged.RemoveListener(Refresh);
    }
}
