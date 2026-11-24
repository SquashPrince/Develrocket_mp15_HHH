using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임 흐름의 결과 화면. 웨이브 중 전멸과 보스전 결과(처치 · 시간 초과 · 전멸)를 보여주고, 버튼으로 다음 행동을 고른다.
// 구역 전투 전멸 : 제목과 전체 시간만 보여준다. 재시작(마지막으로 정리한 구역 뒤에서, 재시도 +1) · 타이틀(저장 없음)
// 보스전 결과 : 전체 시간 · 보스 시간 · 재시도 횟수 · 점수 · 등급을 보여준다. 점수 · 등급은 점수 설정 에셋으로 계산한다.
//   보스 시간은 처치면 걸린 시간, 시간 초과면 제한 시간, 전멸이면 [실패]다.
//   재도전 : 실패 후(재시도 +1, 전체 시간 이어서) · 처치 후(재시도 유지, 전체 시간을 보스전 시작 값으로 되돌림)
//   타이틀 : 이번 점수가 최고 기록보다 높으면 저장하고 타이틀로 간다.
// 시간은 분:초:1/100초 형식이다. 점수는 소수점 아래를 버린 정수로 보여준다.
// 각 값은 Inspector 의 문구 형식({0} 자리에 값)으로 한 줄을 만들어 보여준다. (예 : "총 클리어 시간 : {0}")
// 화면이 뜨면 버튼을 누를 수 있게 커서를 풀어 보여준다. 씬을 다시 불러오면 일시정지 기능이 다시 잠근다.
// 표시 묶음은 이 컴포넌트가 붙은 오브젝트의 자식으로 둔다. 이 오브젝트를 끄면 게임 흐름의 알림을 받지 못한다.
// 보여주기 연출 : 열림 애니메이션을 기다린 뒤 값 글자를 전체 시간 → 보스 시간 → 재시도 → 점수 순서로 한 글자씩 드러낸다.
//   등급은 잠깐 뜸을 들인 뒤 한 번에 보여주고, 그 뒤 정한 시간이 지나면 버튼 묶음을 보여준다. 문구까지 한 줄 전체를 타이핑한다.
//   실제 시간으로 재서, 결과 화면이 뜰 때 게임 시간이 멈추거나 느려져 있어도 같은 속도로 나온다. 씬이 바뀌어 사라지면 멈춘다.
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
    [Tooltip("버튼들을 묶은 오브젝트. 값을 모두 보여준 뒤에 보인다")]
    [SerializeField] private GameObject _buttonGroup;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _retryBossButton;
    [SerializeField] private Button _titleButton;

    [Header("문구 형식 ({0} 자리에 값)")]
    [SerializeField] private string _totalTimeFormat = "총 클리어 시간 : {0}";
    [SerializeField] private string _bossTimeFormat = "보스 처치 시간 : {0}";
    [SerializeField] private string _retryFormat = "재도전 횟수 : {0} 회";
    [SerializeField] private string _scoreFormat = "총 점수 : {0}";

    [Header("보여주기 연출 (실제 시간)")]
    [Tooltip("화면이 켜진 뒤 값을 보여주기 시작할 때까지 기다리는 시간(초). 열림 애니메이션 길이에 맞춘다")]
    [SerializeField, Min(0f)] private float _revealDelay = 1f;
    [Tooltip("값 글자 하나가 나오는 간격(초)")]
    [SerializeField, Min(0f)] private float _charInterval = 0.04f;
    [Tooltip("값 하나를 다 보여준 뒤 다음 값까지 쉬는 시간(초)")]
    [SerializeField, Min(0f)] private float _itemInterval = 0.2f;
    [Tooltip("점수를 보여준 뒤 등급을 보여주기까지 뜸 들이는 시간(초)")]
    [SerializeField, Min(0f)] private float _gradeDelay = 0.8f;
    [Tooltip("마지막 값을 보여준 뒤 버튼이 나올 때까지의 시간(초)")]
    [SerializeField, Min(0f)] private float _buttonDelay = 2f;

    // 이번 보스전 결과로 만든 기록. 타이틀로 갈 때 최고 기록 저장을 시도한다. 웨이브 전멸이면 없다.
    private BestRecord _record;

    private void Start() => Init();
    private void OnEnable() => BindEvent();
    private void OnDisable() => UnbindEvent();

    private void OnWaveWiped()
    {
        _record = null;
        _titleText.text = "전멸";
        _totalTimeText.text = string.Format(_totalTimeFormat, RecordTimeText.Format(RunRecord.TotalTime));
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
        _totalTimeText.text = string.Format(_totalTimeFormat, RecordTimeText.Format(RunRecord.TotalTime));
        _bossTimeText.text = string.Format(_bossTimeFormat, isWiped ? FAILED_TEXT : RecordTimeText.Format(RunRecord.BossTime));
        _retryText.text = string.Format(_retryFormat, RunRecord.RetryCount);
        _scoreText.text = string.Format(_scoreFormat, totalScore);
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
        RevealAsync(destroyCancellationToken).Forget();
    }

    // 값을 모두 숨긴 뒤 순서대로 드러내고, 마지막에 버튼을 보여준다. 보스 기록 묶음이 숨겨져 있으면 전체 시간만 보여준다.
    private async UniTaskVoid RevealAsync(CancellationToken token)
    {
        bool hasBossRecord = _bossRecordGroup.activeSelf;

        _buttonGroup.SetActive(false);
        HideText(_totalTimeText);
        HideText(_bossTimeText);
        HideText(_retryText);
        HideText(_scoreText);
        HideText(_gradeText);

        await WaitAsync(_revealDelay, token);
        await TypeAsync(_totalTimeText, token);

        if(hasBossRecord)
        {
            await WaitAsync(_itemInterval, token);
            await TypeAsync(_bossTimeText, token);
            await WaitAsync(_itemInterval, token);
            await TypeAsync(_retryText, token);
            await WaitAsync(_itemInterval, token);
            await TypeAsync(_scoreText, token);
            await WaitAsync(_gradeDelay, token);
            _gradeText.maxVisibleCharacters = int.MaxValue;
        }

        await WaitAsync(_buttonDelay, token);
        _buttonGroup.SetActive(true);
    }

    private static void HideText(TMP_Text text) => text.maxVisibleCharacters = 0;

    // 글자를 한 개씩 드러낸다. 글자 위치는 처음부터 정해져 있어 줄이 흔들리지 않는다.
    private async UniTask TypeAsync(TMP_Text text, CancellationToken token)
    {
        text.ForceMeshUpdate();

        int count = text.textInfo.characterCount;

        for(int i = 1; i <= count; i++)
        {
            text.maxVisibleCharacters = i;
            await WaitAsync(_charInterval, token);
        }
    }

    private static UniTask WaitAsync(float seconds, CancellationToken token) =>
        UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: token);

    private void Init()
    {
        _root.SetActive(false);
    }

    private void BindEvent()
    {
        _gameFlow.WaveWiped += OnWaveWiped;
        _gameFlow.BossPhaseEnded += OnBossPhaseEnded;
        _restartButton.onClick.AddListener(_gameFlow.RetryZone);
        _retryBossButton.onClick.AddListener(_gameFlow.RetryBoss);
        _titleButton.onClick.AddListener(OnTitleClicked);
    }

    private void UnbindEvent()
    {
        _gameFlow.WaveWiped -= OnWaveWiped;
        _gameFlow.BossPhaseEnded -= OnBossPhaseEnded;
        _restartButton.onClick.RemoveListener(_gameFlow.RetryZone);
        _retryBossButton.onClick.RemoveListener(_gameFlow.RetryBoss);
        _titleButton.onClick.RemoveListener(OnTitleClicked);
    }
}
