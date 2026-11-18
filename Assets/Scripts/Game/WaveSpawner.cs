using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

// 웨이브 적 생성. 웨이브 데이터의 웨이브 하나를 만들고, 웨이브의 적 · 보스가 모두 처치되면 알린다.
// 보스 : 적 데이터의 등급이 보스인 적은 보스 생성 위치에 만들고, 보스 바를 띄울 화면 Canvas 를 넘겨준다.
//   한 웨이브에 보스는 한 마리로 가정한다. 여러 마리면 같은 위치에 겹친다.
// 일반 · 엘리트 : 생성 영역(수평 원) 안에 겹치지 않게 만든다.
//   Bridson 푸아송 디스크로 영역 전체를 최소 간격으로 채운 뒤, 섞어서 앞에서부터 조건에 맞는 자리를 고른다.
//   영역을 다 채운 뒤 고르는 이유 : Bridson 은 첫 점에서 번지며 채우므로, N 개만 놓고 멈추면 첫 점 근처에 몰리기 때문이다.
//   조건 : NavMesh 위로 맞출 수 있고, 맞춘 위치가 이미 놓은 적 · 이 웨이브의 보스 위치와 최소 간격 이상, 조작 중인 캐릭터와 최소 거리 이상 떨어져 있어야 한다.
//   NavMesh 위로 맞추면 위치가 조금 움직이므로 간격은 맞춘 위치로 다시 확인한다.
//   자리가 모자라면 놓을 수 있는 만큼만 만들고 경고를 남긴다. 생성 영역 반지름이나 간격을 조정해야 한다.
// 생성한 적은 조작 중인 캐릭터를 바라보게 하고, 파티에 등록해 공격 대상을 받는다.
// 처치 확인 : 생성한 적의 처치 알림으로 남은 수를 센다. 보스 등급 적이 모두 처치되면 보스 정리를, 모든 적이 처치되면 웨이브 정리를 알린다.
public class WaveSpawner : MonoBehaviour
{
    // 뽑은 자리에서 NavMesh 를 찾는 최대 거리(m)
    private const float NAVMESH_SEARCH_DISTANCE = 2f;

    [SerializeField] private WaveData _waveData;
    [SerializeField] private PlayerParty _party;
    [Tooltip("생성 영역의 중심. 이 위치를 중심으로 한 수평 원 안에 일반 · 엘리트 적을 만든다")]
    [SerializeField] private Transform _spawnCenter;
    [Tooltip("생성 영역 반지름(m)")]
    [SerializeField, Min(1f)] private float _spawnRadius = 10f;

    [Header("보스")]
    [Tooltip("보스 등급 적이 나올 위치")]
    [SerializeField] private Transform _bossSpawnPoint;
    [Tooltip("보스 바를 띄울 화면 Canvas")]
    [SerializeField] private Transform _bossHudRoot;

    // 이번 웨이브에서 생성해 처치 알림을 구독한 적 · 보스
    private readonly List<EnemyState> _waveEnemies = new List<EnemyState>();
    private readonly List<EnemyState> _waveBosses = new List<EnemyState>();
    private int _remainingCount;
    private int _remainingBossCount;

    public WaveData Data => _waveData;

    // 이번 웨이브의 적이 모두 처치되면 알린다.
    public event System.Action WaveCleared;
    // 이번 웨이브의 보스 등급 적이 모두 처치되면 알린다. 보스가 없는 웨이브에서는 알리지 않는다.
    public event System.Action BossesCleared;

    private void OnDisable() => UnbindEnemyEvent();

    // index 번째 웨이브의 적을 만든다.
    public void SpawnWave(int index)
    {
        WaveData.Wave wave = _waveData.GetWave(index);
        List<Vector3> occupied = new List<Vector3>();
        int normalCount = 0;

        foreach(WaveData.SpawnEntry entry in wave.Entries)
        {
            if(IsBoss(entry.Prefab))
            {
                SpawnBosses(entry);
                occupied.Add(_bossSpawnPoint.position);
            }
            else normalCount += entry.Count;
        }

        List<Vector3> positions = FindSpawnPositions(normalCount, occupied);
        int positionIndex = 0;

        foreach(WaveData.SpawnEntry entry in wave.Entries)
        {
            if(IsBoss(entry.Prefab)) continue;

            for(int i = 0; i < entry.Count && positionIndex < positions.Count; i++)
            {
                Track(Spawn(entry.Prefab, positions[positionIndex]));
                positionIndex++;
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[웨이브 {index + 1}] 일반 {positions.Count}마리 · 보스 {_remainingBossCount}마리 생성");
#endif
    }

    // 이번 웨이브의 보스 등급 적이 잃은 체력의 비율(0 ~ 1). 보스가 여러 마리면 최대 체력 합 기준이다. 보스가 없으면 0 이다.
    // 웨이브의 적이 모두 처치되면 목록을 비우므로, 보스전이 끝나는 순간에 읽는다.
    public float GetBossDamageRatio()
    {
        float maxHealth = 0f;
        float lostHealth = 0f;

        foreach(EnemyState boss in _waveBosses)
        {
            maxHealth += boss.MaxHealth;
            lostHealth += boss.MaxHealth - boss.CurrentHealth;
        }

        return maxHealth > 0f ? lostHealth / maxHealth : 0f;
    }

    private static bool IsBoss(EnemyController prefab) => prefab.Data.Grade == EnemyGrade.Boss;

    // 보스 등급 적을 보스 생성 위치에 만들고 화면 Canvas 에 보스 바를 띄운다.
    private void SpawnBosses(WaveData.SpawnEntry entry)
    {
        for(int i = 0; i < entry.Count; i++)
        {
            EnemyController boss = Spawn(entry.Prefab, _bossSpawnPoint.position);

            boss.SetHudRoot(_bossHudRoot);
            boss.State.Died += OnBossDied;
            _waveBosses.Add(boss.State);
            _remainingBossCount++;
            Track(boss);
        }
    }

    // 조작 중인 캐릭터를 바라보게 만들고 파티에 등록한다.
    private EnemyController Spawn(EnemyController prefab, Vector3 position)
    {
        Vector3 toPlayer = _party.Controlled.transform.position - position;
        toPlayer.y = 0f;

        Quaternion rotation = toPlayer == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(toPlayer);
        EnemyController enemy = Instantiate(prefab, position, rotation);

        _party.RegisterEnemy(enemy.GetComponent<BehaviorGraphAgent>());
        return enemy;
    }

    // 웨이브 정리를 세기 위해 처치 알림을 구독한다.
    private void Track(EnemyController enemy)
    {
        enemy.State.Died += OnEnemyDied;
        _waveEnemies.Add(enemy.State);
        _remainingCount++;
    }

    private void OnBossDied()
    {
        _remainingBossCount--;

        if(_remainingBossCount > 0) return;

        BossesCleared?.Invoke();
    }

    private void OnEnemyDied()
    {
        _remainingCount--;

        if(_remainingCount > 0) return;

        UnbindEnemyEvent();
        WaveCleared?.Invoke();
    }

    // count 개의 생성 위치를 고른다. occupied(보스 위치 등)와도 최소 간격을 지킨다. 자리가 모자라면 고를 수 있는 만큼만 돌려준다.
    private List<Vector3> FindSpawnPositions(int count, List<Vector3> occupied)
    {
        List<Vector3> positions = new List<Vector3>();

        if(count == 0) return positions;

        List<Vector2> candidates = PoissonDiskSampler.SampleCircle(_spawnRadius, _waveData.MinSpacing);

        Shuffle(candidates);

        foreach(Vector2 candidate in candidates)
        {
            if(positions.Count >= count) break;

            Vector3 point = _spawnCenter.position + new Vector3(candidate.x, 0f, candidate.y);

            if(!NavMesh.SamplePosition(point, out NavMeshHit hit, NAVMESH_SEARCH_DISTANCE, NavMesh.AllAreas)) continue;
            if(!IsFarEnough(hit.position, positions) || !IsFarEnough(hit.position, occupied)) continue;

            positions.Add(hit.position);
        }

#if UNITY_EDITOR
        if(positions.Count < count) Debug.LogWarning($"[웨이브 생성] 자리가 모자라 {count}마리 중 {positions.Count}마리만 생성합니다. 생성 영역 반지름 · 간격을 조정하세요.");
#endif

        return positions;
    }

    // 조작 중인 캐릭터와 최소 거리, placed 의 모든 위치와 최소 간격 이상 떨어져 있는지
    private bool IsFarEnough(Vector3 position, List<Vector3> placed)
    {
        float sqrSpacing = _waveData.MinSpacing * _waveData.MinSpacing;
        float sqrPlayerDistance = _waveData.MinPlayerDistance * _waveData.MinPlayerDistance;

        if(GetSqrHorizontalDistance(position, _party.Controlled.transform.position) < sqrPlayerDistance) return false;

        foreach(Vector3 other in placed)
        {
            if(GetSqrHorizontalDistance(position, other) < sqrSpacing) return false;
        }

        return true;
    }

    private static float GetSqrHorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;

        return offset.sqrMagnitude;
    }

    // 피셔-예이츠 섞기. 어느 자리든 고를 확률이 같다.
    private static void Shuffle(List<Vector2> list)
    {
        for(int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void UnbindEnemyEvent()
    {
        foreach(EnemyState enemy in _waveEnemies)
        {
            enemy.Died -= OnEnemyDied;
        }

        foreach(EnemyState boss in _waveBosses)
        {
            boss.Died -= OnBossDied;
        }

        _waveEnemies.Clear();
        _waveBosses.Clear();
        _remainingCount = 0;
        _remainingBossCount = 0;
    }

#if UNITY_EDITOR
    // 선택했을 때 생성 영역을 Scene 뷰에 그린다.
    private void OnDrawGizmosSelected()
    {
        if(_spawnCenter == null) return;

        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(_spawnCenter.position, Vector3.up, _spawnRadius);
    }
#endif
}
