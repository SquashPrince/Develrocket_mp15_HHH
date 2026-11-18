using UnityEngine;

// 게임 흐름. 웨이브 → 보스 → 결과 순서로 진행한다.
// 웨이브 : 시작하면 첫 웨이브를 만들고, 웨이브가 정리되면 대기 시간 뒤 다음 웨이브를 만든다.
// 대기 시간은 게임 시간으로 잰다. 일시정지하면 함께 멈춘다.
public class GameFlow : MonoBehaviour
{
    [SerializeField] private WaveSpawner _waveSpawner;

    // 지금 진행 중인 웨이브 번호
    private int _waveIndex = -1;
    // 다음 웨이브까지 남은 시간. 기다리는 중이 아니면 0 이다.
    private float _nextWaveTimer;
    private bool _isWaitingNextWave;

    private void Start() => Init();
    private void OnEnable() => BindSpawnerEvent();
    private void OnDisable() => UnbindSpawnerEvent();
    private void Update() => UpdateNextWaveTimer();

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
    }

    private void Init()
    {
        SpawnNextWave();
    }

    private void BindSpawnerEvent()
    {
        _waveSpawner.WaveCleared += OnWaveCleared;
    }

    private void UnbindSpawnerEvent()
    {
        _waveSpawner.WaveCleared -= OnWaveCleared;
    }
}
