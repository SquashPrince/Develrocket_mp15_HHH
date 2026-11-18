using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름. 웨이브 → 보스전 → 결과 순서로 진행한다.
// 웨이브 : 시작하면 첫 웨이브를 만들고, 웨이브가 정리되면 대기 시간 뒤 다음 웨이브를 만든다.
//   보스 등급 적도 웨이브에 들어 있다. 제한 시간을 쓰지 않는 웨이브의 보스(중간 보스)는 일반 적처럼 웨이브 정리에만 센다.
// 보스전 : 제한 시간을 쓰는 웨이브(마지막 웨이브)가 나오는 순간부터 제한 시간을 잰다.
//   제한 시간 안에 그 웨이브의 보스 등급 적을 모두 처치하면 처치, 남은 시간이 0 이 되면 시간 초과, 파티가 모두 쓰러지면 전멸이다. 남은 일반 적은 상관없다.
// 웨이브 중 전멸 : 전멸 화면을 띄운다. 보스전 전이라 재시도로 세지 않는다.
// 끝난 뒤 : 결과 화면이 뜨면 플레이어 입력을 잠그고, 이후 처치 · 시간 초과 · 전멸 알림은 무시한다.
//   전투는 멈추지 않는다. 히트스톱이 끝나며 게임 시간을 되돌리므로, 게임 시간을 멈추는 일시정지는 Esc 메뉴 작업에서 함께 만든다.
// 다시 하기 : 재시작 · 재도전 · 타이틀 이동은 씬을 불러오는 것으로 처리한다. 이어 갈 기록은 RunRecord 에 남긴다.
//   보스전 재도전으로 다시 불러온 씬은 파티를 보스전 앞 스폰 포인트로 옮기고 보스전 웨이브부터 시작한다.
//   씬이 바뀌어도 남는 정적 값(게임 시간 배율 · 적 시간 배율 · 패링 가능 목록)은 씬을 바꾸기 전에 처음 값으로 돌린다.
// 대기 시간 · 제한 시간 · 전체 시간은 게임 시간으로 잰다. 히트스톱 · 콤보 선택 슬로모션 동안에는 느리게 흐른다.
public class GameFlow : MonoBehaviour
{
    // 보스전이 끝난 방식
    public enum BossOutcome
    {
        Cleared,
        TimeOut,
        Wiped
    }

    [SerializeField] private WaveSpawner _waveSpawner;
    [SerializeField] private PlayerParty _party;
    [Tooltip("보스전 재도전 때 파티가 시작할 위치 · 방향")]
    [SerializeField] private Transform _bossCheckpoint;
    [Tooltip("타이틀로 갈 때 불러올 씬. 빌드 씬 목록에 등록되어 있어야 한다")]
    [SerializeField] private SceneReference _titleScene;

    // 지금 진행 중인 웨이브 번호
    private int _waveIndex = -1;
    // 다음 웨이브까지 남은 시간
    private float _nextWaveTimer;
    private float _timeLimit;
    private float _timeLeft;
    private bool _isWaitingNextWave;
    private bool _isTimerRunning;
    // 결과 화면이 떠 이번 판이 끝났는지
    private bool _isEnded;
    // 보스를 처치해 끝났는지. 재도전 규칙이 실패 후와 다르다.
    private bool _isBossCleared;

    // 보스전이 시작되었는지. 보스전이 끝나도 보스전 단계로 본다.
    public bool IsBossPhase { get; private set; }
    public float TimeLimit => _timeLimit;
    public float TimeLeft => _timeLeft;

    // 보스전이 시작되어 제한 시간을 재기 시작하면 알린다. 타이머 표시를 켜는 데 쓴다.
    public event System.Action BossPhaseStarted;
    // 웨이브 중 파티가 전멸하면 알린다.
    public event System.Action WaveWiped;
    // 보스전이 끝나면 끝난 방식과 함께 알린다. 기록은 RunRecord 에 남긴 뒤 알린다.
    public event System.Action<BossOutcome> BossPhaseEnded;

    private void Start() => Init();
    private void OnEnable()
    {
        BindSpawnerEvent();
        BindPartyEvent();
    }
    private void OnDisable()
    {
        UnbindSpawnerEvent();
        UnbindPartyEvent();
    }
    private void Update()
    {
        UpdateTotalTime();
        UpdateNextWaveTimer();
        UpdateTimeLimit();
    }

    // 웨이브 전멸 화면의 재시작. 기록을 지우고 처음부터 다시 한다.
    public void RestartRun()
    {
        RunRecord.ResetRun();
        ReloadScene();
    }

    // 보스전 결과 화면의 재도전. 보스전 앞부터 다시 한다.
    // 실패 후 : 재시도 횟수를 올리고 전체 시간을 이어서 센다.
    // 처치 후 : 재시도 횟수는 그대로, 전체 시간은 이번 보스전이 시작된 순간의 값으로 되돌린다.
    public void RetryBoss()
    {
        if(_isBossCleared) RunRecord.RetryBossAfterClear();
        else RunRecord.RetryBoss();

        ReloadScene();
    }

    // 결과 화면의 타이틀. 기록을 지우고 타이틀로 간다.
    public void GoToTitle()
    {
        RunRecord.ResetRun();
        PrepareSceneChange();
        SceneManager.LoadScene(_titleScene.SceneName);
    }

    private void OnWaveCleared()
    {
        if(_isEnded) return;

        if(_waveIndex + 1 >= _waveSpawner.Data.WaveCount)
        {
#if UNITY_EDITOR
            Debug.Log("[게임 흐름] 모든 웨이브 정리");
#endif
            return;
        }

        _nextWaveTimer = _waveSpawner.Data.WaveInterval;
        _isWaitingNextWave = true;
    }

    // 보스전의 보스를 모두 처치하면 처치로 끝난다. 제한 시간이 없는 웨이브의 보스(중간 보스)는 무시한다.
    private void OnBossesCleared()
    {
        if(_isEnded || !_isTimerRunning) return;

        EndBossPhase(BossOutcome.Cleared);
    }

    private void OnPartyWiped()
    {
        if(_isEnded) return;

        if(IsBossPhase)
        {
            EndBossPhase(BossOutcome.Wiped);
            return;
        }

        EndRun();

#if UNITY_EDITOR
        Debug.Log("[게임 흐름] 웨이브 중 전멸");
#endif

        WaveWiped?.Invoke();
    }

    private void UpdateTotalTime()
    {
        if(_isEnded) return;

        RunRecord.AddTime(Time.deltaTime);
    }

    private void UpdateNextWaveTimer()
    {
        if(!_isWaitingNextWave) return;

        _nextWaveTimer -= Time.deltaTime;

        if(_nextWaveTimer > 0f) return;

        _isWaitingNextWave = false;
        SpawnNextWave();
    }

    private void SpawnNextWave()
    {
        _waveIndex++;
        _waveSpawner.SpawnWave(_waveIndex);

        WaveData.Wave wave = _waveSpawner.Data.GetWave(_waveIndex);

        if(wave.UseTimeLimit) StartBossPhase(wave.TimeLimit);
    }

    private void StartBossPhase(float timeLimit)
    {
        _timeLimit = timeLimit;
        _timeLeft = timeLimit;
        _isTimerRunning = true;
        IsBossPhase = true;
        RunRecord.BeginBossPhase();

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 보스전 시작 · 제한 시간 {timeLimit}초 · 재시도 {RunRecord.RetryCount}회");
#endif

        BossPhaseStarted?.Invoke();
    }

    private void UpdateTimeLimit()
    {
        if(!_isTimerRunning) return;

        _timeLeft = Mathf.Max(0f, _timeLeft - Time.deltaTime);

        if(_timeLeft > 0f) return;

        EndBossPhase(BossOutcome.TimeOut);
    }

    // 보스 시간 · 깎은 체력 비율을 기록하고 끝난 방식을 알린다. 시간 초과면 보스 시간은 제한 시간 그대로다.
    private void EndBossPhase(BossOutcome outcome)
    {
        EndRun();
        _isBossCleared = outcome == BossOutcome.Cleared;
        RunRecord.EndBossPhase(_timeLimit - _timeLeft, _waveSpawner.GetBossDamageRatio());

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 보스전 종료 : {outcome} · 보스 시간 {RunRecord.BossTime:0.0}초 · 깎은 체력 {RunRecord.BossDamageRatio:P0}");
#endif

        BossPhaseEnded?.Invoke(outcome);
    }

    // 이번 판을 끝낸다. 타이머 · 전체 시간을 멈추고 플레이어 입력을 잠근다.
    private void EndRun()
    {
        _isEnded = true;
        _isTimerRunning = false;
        _isWaitingNextWave = false;
        _party.LockInput();
    }

    private void ReloadScene()
    {
        PrepareSceneChange();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // 씬이 바뀌어도 남는 정적 값을 처음 값으로 돌린다. 히트스톱 · 콤보 선택 · 슬로모션 중에 씬을 바꿀 수 있기 때문이다.
    private static void PrepareSceneChange()
    {
        Time.timeScale = 1f;
        EnemyTime.Scale = 1f;
        ParryWindow.ClearAttackers();
    }

    // 보스전 웨이브(제한 시간을 쓰는 웨이브) 번호. 없으면 첫 웨이브다.
    private int FindBossWaveIndex()
    {
        for(int i = 0; i < _waveSpawner.Data.WaveCount; i++)
        {
            if(_waveSpawner.Data.GetWave(i).UseTimeLimit) return i;
        }

        return 0;
    }

    // 보스전 재도전이면 파티를 보스전 앞 스폰 포인트로 옮기고 보스전 웨이브부터, 아니면 첫 웨이브부터 시작한다.
    private void Init()
    {
        if(RunRecord.StartFrom == RunRecord.StartPoint.BossPhase)
        {
            _party.PlaceAt(_bossCheckpoint);
            _waveIndex = FindBossWaveIndex() - 1;
        }

        SpawnNextWave();
    }

    private void BindSpawnerEvent()
    {
        _waveSpawner.WaveCleared += OnWaveCleared;
        _waveSpawner.BossesCleared += OnBossesCleared;
    }

    private void UnbindSpawnerEvent()
    {
        _waveSpawner.WaveCleared -= OnWaveCleared;
        _waveSpawner.BossesCleared -= OnBossesCleared;
    }

    private void BindPartyEvent()
    {
        _party.Wiped += OnPartyWiped;
    }

    private void UnbindPartyEvent()
    {
        _party.Wiped -= OnPartyWiped;
    }
}
