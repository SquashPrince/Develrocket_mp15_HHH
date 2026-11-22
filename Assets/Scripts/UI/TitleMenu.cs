using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 타이틀 메뉴. 시작 · 게임 종료 버튼을 처리하고, 키보드 · 패드로 바로 고를 수 있게 시작 버튼을 선택한 채로 연다.
// 설정 버튼은 공용 UI 의 설정 화면을 여는 버튼(SettingsOpenButton)이 따로 맡는다.
// 시작 : 로딩 화면을 거쳐 게임 씬을 불러온다. 한 판 기록은 결과 화면 · Esc 메뉴에서 타이틀로 올 때 이미 지워져 있어 처음부터 시작한다.
// 게임 종료 : 빌드에서는 게임을 끄고, 에디터에서는 플레이를 멈춘다.
// 설정 화면을 닫으면 선택이 풀리므로, 선택된 버튼이 없으면 다시 시작 버튼을 고른다. 설정 화면이 열려 있는 동안은 고르지 않는다.
public class TitleMenu : MonoBehaviour
{
    [Tooltip("시작하면 불러올 씬. 빌드 씬 목록에 등록되어 있어야 한다")]
    [SerializeField] private SceneReference _gameScene;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _quitButton;

    private void Start() => SelectStartButton();
    private void OnEnable() => BindButtonEvent();
    private void OnDisable() => UnbindButtonEvent();
    private void Update() => KeepSelection();

    private void OnStartClicked()
    {
        if(!_gameScene.IsValid)
        {
            Debug.LogWarning("[타이틀] 시작할 씬이 연결되지 않았습니다.", this);
            return;
        }

        CommonUI.Instance.SceneLoader.Load(_gameScene.SceneName);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 마우스로 빈 곳을 누르거나 설정 화면을 닫아 선택이 풀리면, 키보드 · 패드로 다시 고를 수 있게 시작 버튼을 고른다.
    // 설정 화면이 열려 있는 동안은 고르지 않는다. 화면 뒤의 시작 버튼이 눌리지 않게 하기 위해서다.
    private void KeepSelection()
    {
        if(EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return;
        if(CommonUI.Instance != null && CommonUI.Instance.Settings.IsOpen) return;

        SelectStartButton();
    }

    private void SelectStartButton()
    {
        if(EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(_startButton.gameObject);
    }

    private void BindButtonEvent()
    {
        _startButton.onClick.AddListener(OnStartClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void UnbindButtonEvent()
    {
        _startButton.onClick.RemoveListener(OnStartClicked);
        _quitButton.onClick.RemoveListener(OnQuitClicked);
    }
}
