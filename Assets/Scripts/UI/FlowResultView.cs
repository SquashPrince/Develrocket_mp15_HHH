using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임 흐름의 결과 화면. 웨이브 중 전멸과 보스전 결과(처치 · 시간 초과 · 전멸)를 보여주고, 버튼으로 다음 행동을 고른다.
// 웨이브 전멸 : 제목과 전체 시간만 보여준다. 재시작(처음부터, 기록 지움) · 타이틀(저장 없음)
// 보스전 결과 : 전체 시간 · 보스 시간 · 재시도 횟수 · 점수 · 등급을 보여준다. 점수 · 등급은 점수 설정 에셋으로 계산한다.
//   보스 시간은 처치면 걸린 시간, 시간 초과면 제한 시간, 전멸이면 [실패]다.
//   재도전 : 실패 후(재시도 +1, 전체 시간 이어서) · 처치 후(재시도 유지, 전체 시간을 보스전 시작 값으로 되돌림)
//   타이틀 : 이번 점수가 최고 기록보다 높으면 저장하고 타이틀로 간다.
// 시간은 분:초.1/100초 형식이다. 점수는 소수점 아래를 버린 정수로 보여준다.
// 화면이 뜨면 버튼을 누를 수 있게 커서를 풀어 보여준다. 씬을 다시 불러오면 일시정지 기능이 다시 잠근다.
// 표시 묶음은 이 컴포넌트가 붙은 오브젝트의 자식으로 둔다. 이 오브젝트를 끄면 게임 흐름의 알림을 받지 못한다.
public class FlowResultView : MonoBehaviour
{
    private const string FAILED_TEXT = "[실패]";

    [SerializeField] private GameFlow _gameFlow;
    [SerializeField] private ScoreSettings _scoreSettings;
    [Tooltip("평소에는 숨길 표시 묶음. 이 오브젝트의 자식을 연결한다")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _totalTimeText;

    [Header("보스전 결과 (웨이브 전멸 때는 숨김)")]
    [Tooltip("보스 시간 · 재시도 · 점수 · 등급을 묶은 오브젝트")]
    [SerializeField] private GameObject _bossRecordGroup;
    [SerializeField] private TMP_Text _bossTimeText;
    [SerializeField] private TMP_Text _retryText;
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _gradeText;

    [Header("버튼")]
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _retryBossButton;
    [SerializeField] private Button _titleButton;

    // 이번 보스전 결과로 만든 기록. 타이틀로 갈 때 최고 기록 저장을 시도한다. 웨이브 전멸이면 없다.
    private BestRecord _record;

    private void Start() => Init();
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();

    private void OnWaveWiped()
    {
        _record = null;
        _titleText.text = "전멸";
        _totalTimeText.text = RecordTimeText.Format(RunRecord.TotalTime);
        _bossRecordGroup.SetActive(false);
        Show(true, false);
    }

    private void OnBossPhaseEnded(GameFlow.BossOutcome outcome)
    {
        bool isWiped = outcome == GameFlow.BossOutcome.Wiped;
        ScoreResult score = ScoreCalculator.Calculate(_scoreSettings, RunRecord.TotalTime, RunRecord.RetryCount, RunRecord.BossDamageRatio, outcome);
        int totalScore = Mathf.FloorToInt(score.Total);

        _record = BestRecord.Create(totalScore, score.Grade, RunRecord.TotalTime, RunRecord.BossTime, RunRecord.RetryCount, isWiped);
        _titleText.text = outcome switch
        {
            GameFlow.BossOutcome.Cleared => "보스 처치",
            GameFlow.BossOutcome.TimeOut => "시간 초과",
            _ => "전멸"
        };
        _totalTimeText.text = RecordTimeText.Format(RunRecord.TotalTime);
        _bossTimeText.text = isWiped ? FAILED_TEXT : RecordTimeText.Format(RunRecord.BossTime);
        _retryText.text = RunRecord.RetryCount.ToString();
        _scoreText.text = totalScore.ToString();
        _gradeText.text = score.Grade;
        _bossRecordGroup.SetActive(true);

#if UNITY_EDITOR
        Debug.Log($"[점수] 전체 시간 {score.TotalTimeScore:0} · 재시도 {score.RetryScore:0} · 보스 {score.BossScore:0} = {score.Total:0} ({score.Grade})");
#endif

        Show(false, true);
    }

    // 보스전 결과면 최고 기록 저장을 시도한 뒤 타이틀로 간다. 웨이브 전멸이면 저장하지 않는다.
    private void OnTitleClicked()
    {
        if(_record != null) BestRecordStore.TrySave(_record);

        _gameFlow.GoToTitle();
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
        _titleButton.onClick.AddListener(OnTitleClicked);
    }

    private void UnbindEvent()
    {
        _gameFlow.WaveWiped -= OnWaveWiped;
        _gameFlow.BossPhaseEnded -= OnBossPhaseEnded;
        _restartButton.onClick.RemoveListener(_gameFlow.RestartRun);
        _retryBossButton.onClick.RemoveListener(_gameFlow.RetryBoss);
        _titleButton.onClick.RemoveListener(OnTitleClicked);
    }
}
