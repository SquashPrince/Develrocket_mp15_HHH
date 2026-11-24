using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬라이더의 현재 값을 글자로 보여준다. 슬라이더 옆의 글자에 붙이고 슬라이더를 연결한다.
// 값에 배율을 곱한 뒤 형식대로 쓴다. 예 : 감도는 배율 1 · 형식 "0.00" → 1.25, 볼륨은 배율 100 · 형식 "0'%'" → 80%
// 슬라이더 값이 바뀔 때마다 다시 쓰고, 켜질 때도 한 번 쓴다.
//   설정 화면은 열 때 알림 없이 값을 맞춘 뒤 화면을 켜므로, 켜질 때 쓰면 맞춘 값이 바로 보인다.
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
