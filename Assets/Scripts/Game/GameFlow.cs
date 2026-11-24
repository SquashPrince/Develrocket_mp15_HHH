using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름. 전투 구역(웨이브)들을 차례로 정리한 뒤 보스 구역의 보스전 결과까지 진행한다.
// 다시 하기는 씬을 다시 불러오는 것으로 처리하고, 이어 갈 기록(정리한 구역 수 · 시간 · 재시도)은 RunRecord 에 남긴다.
// 대기 · 제한 · 전체 시간은 게임 시간으로 재 히트스톱 · 콤보 선택 동안 느리게 흐른다.
// 자세한 흐름 : Docs/Technical/Game/GameFlow.md
public class GameFlow : MonoBehaviour
{
    public enum BossOutcome
    {
        Cleared,
        TimeOut,
        Wiped
    }

    [SerializeField] private WaveSpawner _waveSpawner;
    [SerializeField] private PlayerParty _party;
    [Tooltip("웨이브 마지막 적 · 보스 처치 연출")]
    [SerializeField] private BattleFinisher _finisher;
    [Tooltip("전투 구역 목록. 위에서부터 차례로 진행하고, 마지막 구역이 보스 구역이다")]
    [SerializeField] private BattleZone[] _zones;
    [Tooltip("타이틀로 갈 때 불러올 씬. 빌드 씬 목록에 등록되어 있어야 한다")]
    [SerializeField] private SceneReference _titleScene;

    // 다음에 진행할(전투 중이면 지금) 구역
    private int _zoneIndex;
    private int _waveIndex = -1;
    private bool _isZoneActive;
    private float _nextWaveTimer;
    private float _timeLimit;
    private float _timeLeft;
    private bool _isWaitingNextWave;
    private bool _isTimerRunning;
    // 끝난 뒤 오는 처치 · 시간 초과 · 전멸 알림은 무시한다.
    private bool _isEnded;
    // 처치 후 재도전 규칙이 실패 후와 다르다.
    private bool _isBossCleared;

    // 보스전이 끝나도 보스전 단계로 본다.
    public bool IsBossPhase { get; private set; }
    public float TimeLimit => _timeLimit;
    public float TimeLeft => _timeLeft;
    public bool IsEnded => _isEnded;

    // 전투 중에만 읽는다.
    private BattleZone CurrentZone => _zones[_zoneIndex];

    public event System.Action BossPhaseStarted;
    // 보스전 전 구역 전투 중 전멸
    public event System.Action WaveWiped;
    // 기록은 RunRecord 에 남긴 뒤 알린다.
    public event System.Action<BossOutcome> BossPhaseEnded;
    // 결과 화면보다 먼저 온다. 카메라 입력을 막는 데 쓴다.
    public event System.Action RunEnded;

    private void Start() => Init();
    private void OnEnable()
    {
        BindSpawnerEvent();
        BindPartyEvent();
        BindFinisherEvent();
        BindZoneEvent();
    }
    private void OnDisable()
    {
        UnbindSpawnerEvent();
        UnbindPartyEvent();
        UnbindFinisherEvent();
        UnbindZoneEvent();
    }
    private void Update()
    {
        UpdateTotalTime();
        UpdateNextWaveTimer();
        UpdateTimeLimit();
    }

    public void RestartRun()
    {
        RunRecord.ResetRun();
        ReloadScene();
    }

    // 마지막으로 정리한 구역 뒤에서 다시 한다.
    public void RetryZone()
    {
        RunRecord.Retry();
        ReloadScene();
    }

    // 처치 후 재도전은 재시도로 세지 않고, 전체 시간을 보스전 시작 값으로 되돌린다.
    public void RetryBoss()
    {
        if(_isBossCleared) RunRecord.RetryBossAfterClear();
        else RunRecord.Retry();

        ReloadScene();
    }

    public void GoToTitle()
    {
        RunRecord.ResetRun();
        PrepareSceneChange();
        CommonUI.Instance.SceneLoader.Load(_titleScene.SceneName);
    }

    // 이미 정리한 구역이나 순서가 아닌 구역에 들어오는 것은 무시한다.
    private void OnZoneEntered(BattleZone zone, PlayerController member)
    {
        if(_isEnded || _isZoneActive || member != _party.Controlled) return;
        if(_zoneIndex >= _zones.Length || zone != _zones[_zoneIndex]) return;

        StartZone();
    }

    private void StartZone()
    {
        _isZoneActive = true;
        _waveIndex = -1;
        CurrentZone.SetWalls(true);

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 구역 {_zoneIndex + 1} '{CurrentZone.name}' 전투 시작");
#endif

        SpawnNextWave();
    }

    private void OnWaveCleared()
    {
        if(_isEnded) return;

        _finisher.PlayWaveFinish();

        if(_waveIndex + 1 >= CurrentZone.Waves.WaveCount)
        {
            ClearZone();
            return;
        }

        _nextWaveTimer = CurrentZone.Waves.WaveInterval;
        _isWaitingNextWave = true;
    }

    // 정리한 구역 수를 기록해 이후 다시 하기는 이 구역 뒤에서 시작하게 한다.
    private void ClearZone()
    {
        CurrentZone.SetWalls(false);
        _isZoneActive = false;
        _zoneIndex++;
        RunRecord.ClearZone(_zoneIndex);

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 구역 {_zoneIndex} 정리 · 남은 구역 {_zones.Length - _zoneIndex}개");
#endif
    }

    // 제한 시간이 없는 웨이브의 보스(중간 보스)는 웨이브 정리에만 센다. 남은 일반 적은 상관없다.
    private void OnBossesCleared()
    {
        if(_isEnded || !_isTimerRunning) return;

        EndBossPhase(BossOutcome.Cleared);
    }

    private void OnPartyWiped()
    {
        if(_isEnded) return;

        BattleFeedback.Raise(BattleFeedback.Moment.RunFailed);

        if(IsBossPhase)
        {
            EndBossPhase(BossOutcome.Wiped);
            return;
        }

        EndRun();

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 구역 {_zoneIndex + 1} 전투 중 전멸");
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
        _waveSpawner.SpawnWave(CurrentZone, _waveIndex);

        WaveData.Wave wave = CurrentZone.Waves.GetWave(_waveIndex);

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

        BattleFeedback.Raise(BattleFeedback.Moment.RunFailed);
        EndBossPhase(BossOutcome.TimeOut);
    }

    // 처치면 기록은 처치 순간에 하고, 마지막 일격 연출이 끝난 뒤에 알린다.
    private void EndBossPhase(BossOutcome outcome)
    {
        EndRun();
        _isBossCleared = outcome == BossOutcome.Cleared;
        RunRecord.EndBossPhase(_timeLimit - _timeLeft, _waveSpawner.GetBossDamageRatio());

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 보스전 종료 : {outcome} · 보스 시간 {RunRecord.BossTime:0.0}초 · 깎은 체력 {RunRecord.BossDamageRatio:P0}");
#endif

        if(outcome == BossOutcome.Cleared)
        {
            _finisher.PlayBossFinish(_waveSpawner.FirstBoss);
            return;
        }

        BossPhaseEnded?.Invoke(outcome);
    }

    private void OnBossFinishEnded() => BossPhaseEnded?.Invoke(BossOutcome.Cleared);

    private void EndRun()
    {
        _isEnded = true;
        _isTimerRunning = false;
        _isWaitingNextWave = false;
        _party.EndBattle();
        RunEnded?.Invoke();
    }

    private void ReloadScene()
    {
        PrepareSceneChange();
        CommonUI.Instance.SceneLoader.Load(SceneManager.GetActiveScene().name);
    }

    // 히트스톱 · 콤보 선택 · 마지막 일격 중에도 씬을 바꿀 수 있어, 씬이 바뀌어도 남는 정적 값을 처음 값으로 돌린다.
    private static void PrepareSceneChange()
    {
        Time.timeScale = 1f;
        EnemyTime.Scale = 1f;
        ParryWindow.ClearAttackers();
    }

    // 정리한 구역이 있으면 파티를 마지막으로 정리한 구역의 다시 시작 지점에 놓는다.
    private void Init()
    {
        _zoneIndex = Mathf.Min(RunRecord.ClearedZoneCount, _zones.Length);

        if(_zoneIndex > 0) _party.PlaceAt(_zones[_zoneIndex - 1].RestartPoint);
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

    private void BindFinisherEvent()
    {
        _finisher.BossFinishEnded += OnBossFinishEnded;
    }

    private void UnbindFinisherEvent()
    {
        _finisher.BossFinishEnded -= OnBossFinishEnded;
    }

    private void BindZoneEvent()
    {
        foreach(BattleZone zone in _zones)
        {
            zone.Entered += OnZoneEntered;
        }
    }

    private void UnbindZoneEvent()
    {
        foreach(BattleZone zone in _zones)
        {
            zone.Entered -= OnZoneEntered;
        }
    }
}
