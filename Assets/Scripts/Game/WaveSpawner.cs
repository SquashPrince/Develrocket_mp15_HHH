using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 웨이브 적 생성. 구역의 웨이브 하나를 만들고, 적 · 보스가 모두 처치되면 알린다.
// 보스는 보스 생성 위치에 만들고(한 웨이브에 한 마리로 가정), 일반 · 엘리트는 생성 영역 안에 겹치지 않게 만든다.
// 적은 프리팹마다 모아 두고 처치되어 꺼진 적을 다시 꺼내 쓴다. 모아 둔 적은 이 오브젝트 아래에 둔다.
public class WaveSpawner : MonoBehaviour
{
    private const float NAVMESH_SEARCH_DISTANCE = 2f;

    [SerializeField] private PlayerParty _party;
    [SerializeField] private BattleManager _battleManager;
    [Tooltip("보스 바를 띄울 화면 Canvas")]
    [SerializeField] private Transform _bossHudRoot;

    private BattleZone _zone;

    // 프리팹마다 만든 적. 꺼진 적이 다시 꺼낼 수 있는 적이다.
    private readonly Dictionary<EnemyController, List<EnemyController>> _pools = new Dictionary<EnemyController, List<EnemyController>>();
    // 로딩 중 미리 만들어 켜 둔 적. 미리 그리기가 끝나면 끈다.
    private readonly List<EnemyController> _prewarmed = new List<EnemyController>();
    private readonly List<EnemyState> _waveEnemies = new List<EnemyState>();
    private readonly List<EnemyState> _waveBosses = new List<EnemyState>();
    private int _remainingCount;
    private int _remainingBossCount;

    // 웨이브의 적이 모두 처치되면 목록을 비우므로, 보스전이 끝나는 순간에 읽는다.
    public Transform FirstBoss => _waveBosses.Count > 0 ? _waveBosses[0].transform : null;

    public event System.Action WaveCleared;
    // 보스가 없는 웨이브에서는 알리지 않는다.
    public event System.Action BossesCleared;

    private void OnDisable() => UnbindEnemyEvent();

    // 웨이브 구성 · 생성 영역이 구역마다 달라 구역에서 받는다.
    public void SpawnWave(BattleZone zone, int index)
    {
        _zone = zone;

        WaveData.Wave wave = zone.Waves.GetWave(index);
        List<Vector3> occupied = new List<Vector3>();
        int normalCount = 0;

        foreach(WaveData.SpawnEntry entry in wave.Entries)
        {
            if(IsBoss(entry.Prefab))
            {
                SpawnBosses(entry);
                occupied.Add(_zone.BossSpawnPosition);
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
        Debug.Log($"[{zone.name} 웨이브 {index + 1}] 일반 {positions.Count}마리 · 보스 {_remainingBossCount}마리 생성");
#endif
    }

    // 로딩 화면이 덮고 있는 동안 씬의 모든 구역에 나올 적을 프리팹마다 한 웨이브의 최대 수만큼 만들어 position 에 켜 둔다.
    // 화면에 한 번 그려야 셰이더가 준비되어, 처음 나올 때 끊기거나 에디터에서 하늘색 임시 셰이더로 보이지 않는다.
    // 배틀 매니저에 등록하지 않아 공격 대상이 없으므로 움직이거나 공격하지 않는다.
    public void Prewarm(Vector3 position, Quaternion rotation)
    {
        foreach(KeyValuePair<EnemyController, int> pair in CountMaxPerWave())
        {
            List<EnemyController> pool = GetPool(pair.Key);

            while(pool.Count < pair.Value)
            {
                EnemyController enemy = Instantiate(pair.Key, position, rotation, transform);

                pool.Add(enemy);
                _prewarmed.Add(enemy);
            }
        }
    }

    // 미리 그리기가 끝나면 끈다. 꺼진 적은 웨이브에서 다시 꺼낼 때 처음 상태로 돌린다.
    public void RecallPrewarmed()
    {
        foreach(EnemyController enemy in _prewarmed)
        {
            enemy.gameObject.SetActive(false);
        }

        _prewarmed.Clear();
    }

    // 보스가 여러 마리면 최대 체력 합 기준이다. 웨이브의 적이 모두 처치되면 목록을 비우므로, 보스전이 끝나는 순간에 읽는다.
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

    // 여러 마리면 같은 위치에 겹친다.
    private void SpawnBosses(WaveData.SpawnEntry entry)
    {
        for(int i = 0; i < entry.Count; i++)
        {
            EnemyController boss = Spawn(entry.Prefab, _zone.BossSpawnPosition);

            boss.SetHudRoot(_bossHudRoot);
            boss.State.Died += OnBossDied;
            _waveBosses.Add(boss.State);
            _remainingBossCount++;
            Track(boss);
        }
    }

    // 조작 중인 캐릭터를 바라보게 만들고, 배틀 매니저에 등록해 공격 대상을 받는다.
    private EnemyController Spawn(EnemyController prefab, Vector3 position)
    {
        Vector3 toPlayer = HorizontalVector.Between(position, _party.Controlled.transform.position);

        Quaternion rotation = toPlayer == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(toPlayer);
        EnemyController enemy = TakeFromPool(prefab, position, rotation);

        _battleManager.Register(enemy);
        return enemy;
    }

    // 꺼진 적이 있으면 처음 상태로 돌려 꺼내고, 없으면 새로 만든다. 시체가 남아 있는 동안은 켜져 있어 꺼내지 않는다.
    private EnemyController TakeFromPool(EnemyController prefab, Vector3 position, Quaternion rotation)
    {
        List<EnemyController> pool = GetPool(prefab);

        foreach(EnemyController pooled in pool)
        {
            if(pooled.gameObject.activeSelf) continue;

            pooled.Respawn(position, rotation);
            return pooled;
        }

        EnemyController created = Instantiate(prefab, position, rotation, transform);

        pool.Add(created);
        return created;
    }

    private List<EnemyController> GetPool(EnemyController prefab)
    {
        if(!_pools.TryGetValue(prefab, out List<EnemyController> pool))
        {
            pool = new List<EnemyController>();
            _pools.Add(prefab, pool);
        }

        return pool;
    }

    // 웨이브는 앞 웨이브를 모두 처치해야 나오므로, 프리팹마다 한 웨이브에 나오는 가장 많은 수만큼 미리 만든다.
    // 앞 웨이브의 시체가 아직 켜져 있으면 그만큼은 웨이브 중에 새로 만든다. 구역 목록은 로딩 중 한 번만 쓰므로 씬에서 찾는다.
    private static Dictionary<EnemyController, int> CountMaxPerWave()
    {
        Dictionary<EnemyController, int> maxCounts = new Dictionary<EnemyController, int>();
        Dictionary<EnemyController, int> waveCounts = new Dictionary<EnemyController, int>();

        foreach(BattleZone zone in FindObjectsByType<BattleZone>(FindObjectsSortMode.None))
        {
            for(int i = 0; i < zone.Waves.WaveCount; i++)
            {
                waveCounts.Clear();

                foreach(WaveData.SpawnEntry entry in zone.Waves.GetWave(i).Entries)
                {
                    waveCounts.TryGetValue(entry.Prefab, out int count);
                    waveCounts[entry.Prefab] = count + entry.Count;
                }

                foreach(KeyValuePair<EnemyController, int> pair in waveCounts)
                {
                    maxCounts.TryGetValue(pair.Key, out int max);
                    maxCounts[pair.Key] = Mathf.Max(max, pair.Value);
                }
            }
        }

        return maxCounts;
    }

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

    // 영역 전체를 푸아송 디스크로 채운 뒤 섞어서 고른다. N 개만 놓고 멈추면 첫 점 근처에 몰리기 때문이다.
    // NavMesh 위로 맞추면 위치가 조금 움직이므로 간격은 맞춘 위치로 다시 확인한다. 자리가 모자라면 고를 수 있는 만큼만 돌려준다.
    private List<Vector3> FindSpawnPositions(int count, List<Vector3> occupied)
    {
        List<Vector3> positions = new List<Vector3>();

        if(count == 0) return positions;

        List<Vector2> candidates = PoissonDiskSampler.SampleCircle(_zone.SpawnRadius, _zone.Waves.MinSpacing);

        Shuffle(candidates);

        foreach(Vector2 candidate in candidates)
        {
            if(positions.Count >= count) break;

            Vector3 point = _zone.SpawnCenter + new Vector3(candidate.x, 0f, candidate.y);

            if(!NavMesh.SamplePosition(point, out NavMeshHit hit, NAVMESH_SEARCH_DISTANCE, NavMesh.AllAreas)) continue;
            if(!IsFarEnough(hit.position, positions) || !IsFarEnough(hit.position, occupied)) continue;

            positions.Add(hit.position);
        }

#if UNITY_EDITOR
        if(positions.Count < count) Debug.LogWarning($"[웨이브 생성] 자리가 모자라 {count}마리 중 {positions.Count}마리만 생성합니다. 생성 영역 반지름 · 간격을 조정하세요.");
#endif

        return positions;
    }

    private bool IsFarEnough(Vector3 position, List<Vector3> placed)
    {
        float sqrSpacing = _zone.Waves.MinSpacing * _zone.Waves.MinSpacing;
        float sqrPlayerDistance = _zone.Waves.MinPlayerDistance * _zone.Waves.MinPlayerDistance;

        if(GetSqrHorizontalDistance(position, _party.Controlled.transform.position) < sqrPlayerDistance) return false;

        foreach(Vector3 other in placed)
        {
            if(GetSqrHorizontalDistance(position, other) < sqrSpacing) return false;
        }

        return true;
    }

    private static float GetSqrHorizontalDistance(Vector3 a, Vector3 b) => HorizontalVector.Between(b, a).sqrMagnitude;

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
}
