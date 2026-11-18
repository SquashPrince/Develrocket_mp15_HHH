using UnityEngine;
using UnityEngine.UI;

// Esc 메뉴. 일시정지되면 열고, 이어가면 닫는다.
// 버튼 : 계속하기 · 설정 · 처음부터 재시작 · 보스전부터 다시 · 타이틀로
//   보스전부터 다시는 보스전 중에만 보인다. 재시도 횟수가 1 오른다. (게임 흐름의 재도전과 같다)
//   처음부터 재시작은 기록을 지우고 처음부터 한다. 타이틀로는 기록을 남기지 않고 간다.
// 설정 : 같은 설정 화면을 열고, 그동안 메뉴 버튼은 숨긴다. 설정을 닫으면 메뉴로 돌아온다.
//   설정을 연 채 Esc 로 이어가면 설정을 닫고(저장) 메뉴도 닫는다.
// 표시 묶음은 이 컴포넌트가 붙은 오브젝트의 자식으로 둔다. 이 오브젝트를 끄면 일시정지 알림을 받지 못한다.
public class PauseMenuView : MonoBehaviour
{
    [SerializeField] private GamePause _gamePause;
    [SerializeField] private GameFlow _gameFlow;
    [SerializeField] private SettingsView _settingsView;
    [Tooltip("평소에는 숨길 메뉴 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _retryBossButton;
    [SerializeField] private Button _titleButton;

    private void Start() => Init();
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();

    private void OnPausedChanged(bool isPaused)
    {
        if(isPaused)
        {
            _retryBossButton.gameObject.SetActive(_gameFlow.IsBossPhase);
            _root.SetActive(true);
            return;
        }

        _root.SetActive(false);

        if(_settingsView.IsOpen) _settingsView.Close();
    }

    private void OnSettingsClicked()
    {
        _root.SetActive(false);
        _settingsView.Open();
    }

    // 설정을 닫았을 때 아직 멈춰 있으면 메뉴로 돌아온다. Esc 로 이어가며 닫힌 경우는 메뉴를 다시 열지 않는다.
    private void OnSettingsClosed()
    {
        if(GamePause.IsPaused) _root.SetActive(true);
    }

    private void Init()
    {
        _root.SetActive(false);
    }

    private void BindEvent()
    {
        _gamePause.PausedChanged += OnPausedChanged;
        _settingsView.Closed += OnSettingsClosed;
        _resumeButton.onClick.AddListener(_gamePause.Resume);
        _settingsButton.onClick.AddListener(OnSettingsClicked);
        _restartButton.onClick.AddListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.AddListener(_gameFlow.RetryBoss);
        _titleButton.onClick.AddListener(_gameFlow.GoToTitle);
    }

    private void UnbindEvent()
    {
        _gamePause.PausedChanged -= OnPausedChanged;
        _settingsView.Closed -= OnSettingsClosed;
        _resumeButton.onClick.RemoveListener(_gamePause.Resume);
        _settingsButton.onClick.RemoveListener(OnSettingsClicked);
        _restartButton.onClick.RemoveListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.RemoveListener(_gameFlow.RetryBoss);
        _titleButton.onClick.RemoveListener(_gameFlow.GoToTitle);
    }
}
