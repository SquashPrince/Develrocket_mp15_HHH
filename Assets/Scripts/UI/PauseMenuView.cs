using UnityEngine;
using UnityEngine.UI;

// Esc 메뉴. 일시정지되면 열고, 이어가면 닫는다. 보스전부터 다시는 보스전 중에만 보인다.
// 이 오브젝트를 끄면 일시정지 알림을 받지 못하므로 표시 묶음은 자식으로 둔다.
public class PauseMenuView : MonoBehaviour
{
    [SerializeField] private GamePause _gamePause;
    [SerializeField] private GameFlow _gameFlow;
    [Tooltip("평소에는 숨길 메뉴 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _retryBossButton;
    [SerializeField] private Button _titleButton;

    // 설정 화면은 씬이 바뀌어도 유지되는 공용 UI 에 있어 Inspector 로 연결하지 않고 찾는다.
    private SettingsView Settings => CommonUI.Instance.Settings;

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

        // 설정을 연 채 이어가면 설정도 닫는다(저장).
        if(Settings.IsOpen) Settings.Close();
    }

    private void OnSettingsClicked()
    {
        _root.SetActive(false);
        Settings.Open();
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
        Settings.Closed += OnSettingsClosed;
        _resumeButton.onClick.AddListener(_gamePause.Resume);
        _settingsButton.onClick.AddListener(OnSettingsClicked);
        _restartButton.onClick.AddListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.AddListener(_gameFlow.RetryBoss);
        _titleButton.onClick.AddListener(_gameFlow.GoToTitle);
    }

    private void UnbindEvent()
    {
        _gamePause.PausedChanged -= OnPausedChanged;

        // 게임을 끌 때는 공용 UI 가 먼저 사라질 수 있다.
        if(CommonUI.Instance != null) Settings.Closed -= OnSettingsClosed;

        _resumeButton.onClick.RemoveListener(_gamePause.Resume);
        _settingsButton.onClick.RemoveListener(OnSettingsClicked);
        _restartButton.onClick.RemoveListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.RemoveListener(_gameFlow.RetryBoss);
        _titleButton.onClick.RemoveListener(_gameFlow.GoToTitle);
    }
}
