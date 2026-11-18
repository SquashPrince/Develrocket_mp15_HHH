using UnityEngine;
using UnityEngine.UI;

// 설정 화면. 마우스 가로 · 세로 감도와 전체 · 배경음 · 효과음 볼륨을 슬라이더로 바꾼다. 공용 UI 에 하나만 있고, 타이틀의 설정 버튼과 Esc 메뉴가 함께 쓴다.
// 열 때 저장된 값으로 슬라이더를 맞추고, 슬라이더를 움직이면 바로 반영한다. 닫을 때 파일에 저장한다.
// 슬라이더 범위는 각 Slider 의 최소 · 최대값으로 정한다. (감도는 배율, 볼륨은 0 ~ 1)
// 표시 묶음은 이 컴포넌트가 붙은 오브젝트의 자식으로 둔다. 이 오브젝트를 끄면 슬라이더 알림을 받지 못한다.
public class SettingsView : MonoBehaviour
{
    [Tooltip("평소에는 숨길 표시 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Slider _mouseXSlider;
    [SerializeField] private Slider _mouseYSlider;
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button _closeButton;

    public bool IsOpen => _root.activeSelf;

    // 설정 화면을 닫으면 알린다. Esc 메뉴가 메뉴로 돌아가는 데 쓴다.
    public event System.Action Closed;

    private void Start() => Init();
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();

    // 저장된 값으로 슬라이더를 맞추고 연다. 맞추는 동안에는 값 변경 알림을 보내지 않는다.
    public void Open()
    {
        SettingsData settings = SettingsStore.Current;

        _mouseXSlider.SetValueWithoutNotify(settings.MouseSensitivity.x);
        _mouseYSlider.SetValueWithoutNotify(settings.MouseSensitivity.y);
        _masterSlider.SetValueWithoutNotify(settings.MasterVolume);
        _bgmSlider.SetValueWithoutNotify(settings.BgmVolume);
        _sfxSlider.SetValueWithoutNotify(settings.SfxVolume);
        _root.SetActive(true);
    }

    public void Close()
    {
        SettingsStore.Save();
        _root.SetActive(false);
        Closed?.Invoke();
    }

    private void OnMouseSensitivityChanged(float value)
    {
        SettingsStore.SetMouseSensitivity(new Vector2(_mouseXSlider.value, _mouseYSlider.value));
    }

    private void Init()
    {
        _root.SetActive(false);
    }

    private void BindEvent()
    {
        _mouseXSlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        _mouseYSlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        _masterSlider.onValueChanged.AddListener(SettingsStore.SetMasterVolume);
        _bgmSlider.onValueChanged.AddListener(SettingsStore.SetBgmVolume);
        _sfxSlider.onValueChanged.AddListener(SettingsStore.SetSfxVolume);
        _closeButton.onClick.AddListener(Close);
    }

    private void UnbindEvent()
    {
        _mouseXSlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
        _mouseYSlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
        _masterSlider.onValueChanged.RemoveListener(SettingsStore.SetMasterVolume);
        _bgmSlider.onValueChanged.RemoveListener(SettingsStore.SetBgmVolume);
        _sfxSlider.onValueChanged.RemoveListener(SettingsStore.SetSfxVolume);
        _closeButton.onClick.RemoveListener(Close);
    }
}
