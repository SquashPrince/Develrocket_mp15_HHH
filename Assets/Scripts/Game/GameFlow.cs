using UnityEngine;

// 게임 흐름. 웨이브 → 보스전 → 결과 순서로 진행한다.
// 웨이브 : 시작하면 첫 웨이브를 만들고, 웨이브가 정리되면 대기 시간 뒤 다음 웨이브를 만든다.
//   보스 등급 적도 웨이브에 들어 있다. 제한 시간을 쓰지 않는 웨이브의 보스(중간 보스)는 일반 적처럼 웨이브 정리에만 센다.
// 보스전 : 제한 시간을 쓰는 웨이브(마지막 웨이브)가 나오는 순간부터 제한 시간을 잰다.
//   제한 시간 안에 그 웨이브의 보스 등급 적을 모두 처치하면 성공, 남은 시간이 0 이 되면 실패(타임아웃)다. 남은 일반 적은 상관없다.
// 대기 시간 · 제한 시간은 게임 시간으로 잰다. 일시정지하면 함께 멈추고, 히트스톱 · 콤보 선택 슬로모션 동안에는 느리게 흐른다.
public class GameFlow : MonoBehaviour
{
    [SerializeField] private WaveSpawner _waveSpawner;

    // 지금 진행 중인 웨이브 번호
    private int _waveIndex = -1;
    // 다음 웨이브까지 남은 시간
    private float _nextWaveTimer;
    private float _timeLimit;
    private float _timeLeft;
    private bool _isWaitingNextWave;
    private bool _isTimerRunning;

    // 보스전이 시작되었는지. 보스를 처치하거나 시간이 다 되어도 보스전 단계로 본다.
    public bool IsBossPhase { get; private set; }
    public float TimeLimit => _timeLimit;
    public float TimeLeft => _timeLeft;

    // 보스전이 시작되어 제한 시간을 재기 시작하면 알린다. 타이머 표시를 켜는 데 쓴다.
    public event System.Action BossPhaseStarted;

    private void Start() => Init();
    private void OnEnable() => BindSpawnerEvent();
    private void OnDisable() => UnbindSpawnerEvent();
    private void Update()
    {
        UpdateNextWaveTimer();
        UpdateTimeLimit();
    }

    private void OnWaveCleared()
    {
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

    // 보스전의 보스를 모두 처치하면 성공이다. 제한 시간이 없는 웨이브의 보스(중간 보스)는 무시한다.
    private void OnBossesCleared()
    {
        if(!_isTimerRunning) return;

        _isTimerRunning = false;

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 보스전 성공 · 남은 시간 {_timeLeft:0.0}초");
#endif
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

#if UNITY_EDITOR
        Debug.Log($"[게임 흐름] 보스전 시작 · 제한 시간 {timeLimit}초");
#endif

        BossPhaseStarted?.Invoke();
    }

    private void UpdateTimeLimit()
    {
        if(!_isTimerRunning) return;

        _timeLeft = Mathf.Max(0f, _timeLeft - Time.deltaTime);

        if(_timeLeft > 0f) return;

        _isTimerRunning = false;

#if UNITY_EDITOR
        Debug.Log("[게임 흐름] 보스전 실패 : 시간 초과");
#endif
    }

    private void Init()
    {
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
}
