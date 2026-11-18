using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임 흐름의 임시 결과 화면. 웨이브 중 전멸과 보스전 결과(처치 · 시간 초과 · 전멸)를 보여주고, 버튼으로 다음 행동을 고른다.
// 점수 · 등급을 보여주는 결과 화면은 결과 화면 작업에서 이 화면을 바꾼다. 지금은 기록 값을 글자로만 보여준다.
// 웨이브 전멸 : 재시작(처음부터, 기록 지움) · 타이틀
// 보스전 실패(시간 초과 · 전멸) : 재도전(보스전 앞부터, 재시도 +1) · 타이틀
// 보스 처치 : 재도전(보스전 앞부터, 재시도 유지 · 전체 시간을 보스전 시작 값으로 되돌림) · 타이틀
// 화면이 뜨면 버튼을 누를 수 있게 커서를 풀어 보여준다. 씬을 다시 불러오면 커서 잠금 기능이 다시 잠근다.
// 표시 묶음은 이 컴포넌트가 붙은 오브젝트의 자식으로 둔다. 이 오브젝트를 끄면 게임 흐름의 알림을 받지 못한다.
public class FlowResultView : MonoBehaviour
{
    [SerializeField] private GameFlow _gameFlow;
    [Tooltip("평소에는 숨길 표시 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _titleText;
    [Tooltip("전체 시간 · 보스 시간 · 재시도 · 깎은 체력을 보여줄 글자")]
    [SerializeField] private TMP_Text _recordText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _retryBossButton;
    [SerializeField] private Button _titleButton;

    private void Start() => Init();
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();

    private void OnWaveWiped()
    {
        _titleText.text = "전멸";
        _recordText.text = $"전체 시간 {RunRecord.TotalTime:0.0}초";
        Show(true, false);
    }

    private void OnBossPhaseEnded(GameFlow.BossOutcome outcome)
    {
        string bossTime = outcome == GameFlow.BossOutcome.Wiped ? "[실패]" : $"{RunRecord.BossTime:0.0}초";

        _titleText.text = outcome switch
        {
            GameFlow.BossOutcome.Cleared => "보스 처치",
            GameFlow.BossOutcome.TimeOut => "시간 초과",
            _ => "전멸"
        };
        _recordText.text = $"전체 시간 {RunRecord.TotalTime:0.0}초\n보스 시간 {bossTime}\n재시도 {RunRecord.RetryCount}회\n깎은 체력 {RunRecord.BossDamageRatio:P0}";
        Show(false, true);
    }

    private void Show(bool canRestart, bool canRetryBoss)
    {
        _restartButton.gameObject.SetActive(canRestart);
        _retryBossButton.gameObject.SetActive(canRetryBoss);
        _root.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Init()
    {
        _root.SetActive(false);
    }

    private void BindEvent()
    {
        _gameFlow.WaveWiped += OnWaveWiped;
        _gameFlow.BossPhaseEnded += OnBossPhaseEnded;
        _restartButton.onClick.AddListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.AddListener(_gameFlow.RetryBoss);
        _titleButton.onClick.AddListener(_gameFlow.GoToTitle);
    }

    private void UnbindEvent()
    {
        _gameFlow.WaveWiped -= OnWaveWiped;
        _gameFlow.BossPhaseEnded -= OnBossPhaseEnded;
        _restartButton.onClick.RemoveListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.RemoveListener(_gameFlow.RetryBoss);
        _titleButton.onClick.RemoveListener(_gameFlow.GoToTitle);
    }
}
