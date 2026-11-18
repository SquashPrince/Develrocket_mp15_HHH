using UnityEngine;
using UnityEngine.UI;

// 누르면 공용 UI 의 설정 화면을 연다. 타이틀의 설정 버튼에 붙인다.
// 설정 화면은 씬이 바뀌어도 유지되는 공용 UI 에 있어 Inspector 의 클릭 이벤트로 연결할 수 없으므로, 누를 때 공용 UI 에서 찾는다.
[RequireComponent(typeof(Button))]
public class SettingsOpenButton : MonoBehaviour
{
    private Button _button;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindButtonEvent();
    private void OnDisable() => UnbindButtonEvent();

    private void OnClicked() => CommonUI.Instance.Settings.Open();

    private void CacheComponent()
    {
        _button = GetComponent<Button>();
    }

    private void BindButtonEvent()
    {
        _button.onClick.AddListener(OnClicked);
    }

    private void UnbindButtonEvent()
    {
        _button.onClick.RemoveListener(OnClicked);
    }
}
