using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 흐름. 맵 이동 → 전투 구역(웨이브) → … → 보스 구역(보스전) → 결과 순서로 진행한다.
// 전투 구역 : 구역 목록의 순서대로 진행한다. 다음 구역에 조작 중인 캐릭터가 들어오면 구역의 벽을 켜고 첫 웨이브를 만든다.
//   웨이브가 정리되면 대기 시간 뒤 다음 웨이브를 만들고, 구역의 마지막 웨이브까지 정리하면 벽을 끄고 정리한 구역 수를 기록한다.
//   이미 정리한 구역이나 순서가 아닌 구역에 들어오는 것은 무시한다. 구역 밖에서는 전투가 없다.
//   보스 등급 적도 웨이브에 들어 있다. 제한 시간을 쓰지 않는 웨이브의 보스(중간 보스)는 일반 적처럼 웨이브 정리에만 센다.
// 보스전 : 제한 시간을 쓰는 웨이브(보스 구역의 마지막 웨이브)가 나오는 순간부터 제한 시간을 잰다.
//   제한 시간 안에 그 웨이브의 보스 등급 적을 모두 처치하면 처치, 남은 시간이 0 이 되면 시간 초과, 파티가 모두 쓰러지면 전멸이다. 남은 일반 적은 상관없다.
// 마지막 일격 : 웨이브가 정리되면(보스까지 모두 처치) 마지막 일격 기능이 게임 전체를 잠깐 느리게 한다.
//   보스전 처치는 기록과 입력 잠금을 처치 순간에 하고, 마지막 일격 기능이 게임 전체를 멈춘 뒤 끝났다고 알리면 그때 처치 결과를 알린다.
//   결과 화면이 떠도 게임은 멈춘 채로 두고, 남은 적도 그대로 둔다. 결과 화면의 버튼은 씬을 바꾸기 전에 게임 시간을 되돌린다.
//   보스 처치와 웨이브 정리가 함께 오면 보스 처치가 먼저 와 판이 끝나므로, 웨이브 슬로우는 걸지 않는다.
// 판 실패(전멸 · 시간 초과) : 판 실패 순간을 알려 카메라가 조금 줌 인한 채 유지한다. 마지막 캐릭터는 쓰러진 채 남고, 시간 초과는 쓰러뜨리지 않는다.
// 판이 끝나면 파티의 입력을 막고 적 공격에 맞지 않게 하며, 판 종료를 알려 카메라 입력도 막게 한다.
// 구역 전투 중 전멸 : 전멸 화면을 띄운다. 재시작하면 보스전 재도전과 같이 재시도로 센다.
// 끝난 뒤 : 결과 화면이 뜨면 플레이어 입력을 잠그고, 이후 처치 · 시간 초과 · 전멸 알림은 무시한다.
//   전투는 멈추지 않는다. 히트스톱이 끝나며 게임 시간을 되돌리므로, 게임 시간을 멈추는 일시정지는 Esc 메뉴 작업에서 함께 만든다.
// 다시 하기 : 재시작 · 재도전 · 타이틀 이동은 씬을 불러오는 것으로 처리한다. 이어 갈 기록은 RunRecord 에 남긴다.
//   다시 불러온 씬은 기록된 정리한 구역 수만큼의 구역을 열어 두고, 파티를 마지막으로 정리한 구역의 다시 시작 지점에 놓는다.
//   구역 전투 전멸 · 보스전 실패 뒤 다시 하기는 이 방식으로 실패한 구역 앞에서 시작한다. 처음부터 재시작은 기록을 지우고 처음 시작 지점에서 시작한다.
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
    [Tooltip("웨이브 마지막 적 · 보스 처치 연출")]
    [SerializeField] private BattleFinisher _finisher;
    [Tooltip("전투 구역 목록. 위에서부터 차례로 진행하고, 마지막 구역이 보스 구역이다")]
    [SerializeField] private BattleZone[] _zones;
    [Tooltip("타이틀로 갈 때 불러올 씬. 빌드 씬 목록에 등록되어 있어야 한다")]
    [SerializeField] private SceneReference _titleScene;

    // 다음에 진행할(전투 중이면 지금) 구역 번호와 그 구역에서 진행 중인 웨이브 번호
    private int _zoneIndex;
    private int _waveIndex = -1;
    private bool _isZoneActive;
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
    // 결과 화면이 떠 이번 판이 끝났는지. 끝난 뒤에는 일시정지할 수 없다.
    public bool IsEnded => _isEnded;

    // 지금 전투 중인 구역. 전투 중에만 읽는다.
    private BattleZone CurrentZone => _zones[_zoneIndex];

    // 보스전이 시작되어 제한 시간을 재기 시작하면 알린다. 타이머 표시를 켜는 데 쓴다.
    public event System.Action BossPhaseStarted;
    // 구역 전투 중(보스전 전) 파티가 전멸하면 알린다.
    public event System.Action WaveWiped;
    // 보스전이 끝나면 끝난 방식과 함께 알린다. 기록은 RunRecord 에 남긴 뒤 알린다.
    public event System.Action<BossOutcome> BossPhaseEnded;
    // 판이 끝나는 순간(보스 처치 · 시간 초과 · 전멸) 알린다. 결과 화면보다 먼저 온다. 카메라 입력을 막는 데 쓴다.
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

    // 처음부터 재시작(Esc 메뉴). 기록을 지우고 처음 시작 지점부터 다시 한다.
    public void RestartRun()
    {
        RunRecord.ResetRun();
        ReloadScene();
    }

    // 구역 전투 전멸 화면의 재시작. 마지막으로 정리한 구역 뒤에서 다시 한다. 재시도 횟수를 올리고 전체 시간을 이어서 센다.
    public void RetryZone()
    {
        RunRecord.Retry();
        ReloadScene();
    }

    // 보스전 결과 화면(Esc 메뉴)의 재도전. 보스 구역 앞(직전 구역 뒤)부터 다시 한다.
    // 실패 후 : 재시도 횟수를 올리고 전체 시간을 이어서 센다.
    // 처치 후 : 재시도 횟수는 그대로, 전체 시간은 이번 보스전이 시작된 순간의 값으로 되돌린다.
    public void RetryBoss()
    {
        if(_isBossCleared) RunRecord.RetryBossAfterClear();
        else RunRecord.Retry();

        ReloadScene();
    }

    // 결과 화면의 타이틀. 기록을 지우고 타이틀로 간다.
    public void GoToTitle()
    {
        RunRecord.ResetRun();
        PrepareSceneChange();
        CommonUI.Instance.SceneLoader.Load(_titleScene.SceneName, false);
    }

    // 진행할 순서의 구역에 조작 중인 캐릭터가 들어오면 그 구역의 전투를 시작한다.
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

    // 구역의 웨이브를 모두 정리했다. 벽을 끄고, 정리한 구역 수를 기록해 이후 다시 하기는 이 구역 뒤에서 시작하게 한다.
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

    // 보스전의 보스를 모두 처치하면 처치로 끝난다. 제한 시간이 없는 웨이브의 보스(중간 보스)는 무시한다.
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

    // 보스 시간 · 깎은 체력 비율을 기록하고 끝난 방식을 알린다. 시간 초과면 보스 시간은 제한 시간 그대로다.
    // 처치면 기록은 처치 순간에 하고, 마지막 일격 정지가 끝난 뒤에 알린다.
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

    // 이번 판을 끝낸다. 타이머 · 전체 시간을 멈추고 플레이어 입력을 잠근다.
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
        CommonUI.Instance.SceneLoader.Load(SceneManager.GetActiveScene().name, true);
    }

    // 씬이 바뀌어도 남는 정적 값을 처음 값으로 돌린다. 히트스톱 · 콤보 선택 · 슬로모션 중에 씬을 바꿀 수 있기 때문이다.
    private static void PrepareSceneChange()
    {
        Time.timeScale = 1f;
        EnemyTime.Scale = 1f;
        ParryWindow.ClearAttackers();
    }

    // 기록된 정리한 구역 수만큼 구역을 열어 두고, 정리한 구역이 있으면 파티를 마지막으로 정리한 구역의 다시 시작 지점에 놓는다.
    // 정리한 구역이 없으면 파티는 씬에 놓인 처음 시작 지점에 그대로 있다. 전투는 다음 구역에 들어갈 때 시작한다.
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
