using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

// 웨이브 적 생성. 웨이브 데이터의 웨이브 하나를 생성 영역(수평 원) 안에 겹치지 않게 만들고, 모두 처치되면 알린다.
// 생성 위치 : Bridson 푸아송 디스크로 영역 전체를 최소 간격으로 채운 뒤, 섞어서 앞에서부터 조건에 맞는 자리를 고른다.
//   영역을 다 채운 뒤 고르는 이유 : Bridson 은 첫 점에서 번지며 채우므로, N 개만 놓고 멈추면 첫 점 근처에 몰리기 때문이다.
//   조건 : NavMesh 위로 맞출 수 있고, 맞춘 위치가 이미 고른 자리와 최소 간격 이상, 조작 중인 캐릭터와 최소 거리 이상 떨어져 있어야 한다.
//   NavMesh 위로 맞추면 위치가 조금 움직이므로 간격은 맞춘 위치로 다시 확인한다.
//   자리가 모자라면 놓을 수 있는 만큼만 만들고 경고를 남긴다. 생성 영역 반지름이나 간격을 조정해야 한다.
// 생성한 적은 조작 중인 캐릭터를 바라보게 하고, 파티에 등록해 공격 대상을 받는다.
// 처치 확인 : 생성한 적의 처치 알림으로 남은 수를 세고, 0 이 되면 웨이브 정리를 알린다.
public class WaveSpawner : MonoBehaviour
{
    // 뽑은 자리에서 NavMesh 를 찾는 최대 거리(m)
    private const float NAVMESH_SEARCH_DISTANCE = 2f;

    [SerializeField] private WaveData _waveData;
    [SerializeField] private PlayerParty _party;
    [Tooltip("생성 영역의 중심. 이 위치를 중심으로 한 수평 원 안에 적을 만든다")]
    [SerializeField] private Transform _spawnCenter;
    [Tooltip("생성 영역 반지름(m)")]
    [SerializeField, Min(1f)] private float _spawnRadius = 10f;

    // 이번 웨이브에서 생성해 처치 알림을 구독한 적
    private readonly List<EnemyState> _waveEnemies = new List<EnemyState>();
    private int _remainingCount;

    public WaveData Data => _waveData;

    // 이번 웨이브의 적이 모두 처치되면 알린다.
    public event System.Action WaveCleared;

    private void OnDisable() => UnbindEnemyEvent();

    // index 번째 웨이브의 적을 만든다.
    public void SpawnWave(int index)
    {
        WaveData.Wave wave = _waveData.GetWave(index);
        int totalCount = CountEnemies(wave);
        List<Vector3> positions = FindSpawnPositions(totalCount);
        int positionIndex = 0;

        foreach(WaveData.SpawnEntry entry in wave.Entries)
        {
            for(int i = 0; i < entry.Count && positionIndex < positions.Count; i++)
            {
                Spawn(entry.Prefab, positions[positionIndex]);
                positionIndex++;
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[웨이브 {index + 1}] {positions.Count}마리 생성");
#endif
    }

    private void Spawn(EnemyController prefab, Vector3 position)
    {
        Vector3 toPlayer = _party.Controlled.transform.position - position;
        toPlayer.y = 0f;

        Quaternion rotation = toPlayer == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(toPlayer);
        EnemyController enemy = Instantiate(prefab, position, rotation);

        _party.RegisterEnemy(enemy.GetComponent<BehaviorGraphAgent>());

        enemy.State.Died += OnEnemyDied;
        _waveEnemies.Add(enemy.State);
        _remainingCount++;
    }

    private void OnEnemyDied()
    {
        _remainingCount--;

        if(_remainingCount > 0) return;

        UnbindEnemyEvent();
        WaveCleared?.Invoke();
    }

    // count 개의 생성 위치를 고른다. 자리가 모자라면 고를 수 있는 만큼만 돌려준다.
    private List<Vector3> FindSpawnPositions(int count)
    {
        List<Vector2> candidates = PoissonDiskSampler.SampleCircle(_spawnRadius, _waveData.MinSpacing);
        List<Vector3> positions = new List<Vector3>();

        Shuffle(candidates);

        foreach(Vector2 candidate in candidates)
        {
            if(positions.Count >= count) break;

            Vector3 point = _spawnCenter.position + new Vector3(candidate.x, 0f, candidate.y);

            if(!NavMesh.SamplePosition(point, out NavMeshHit hit, NAVMESH_SEARCH_DISTANCE, NavMesh.AllAreas)) continue;
            if(!IsFarEnough(hit.position, positions)) continue;

            positions.Add(hit.position);
        }

#if UNITY_EDITOR
        if(positions.Count < count) Debug.LogWarning($"[웨이브 생성] 자리가 모자라 {count}마리 중 {positions.Count}마리만 생성합니다. 생성 영역 반지름 · 간격을 조정하세요.");
#endif

        return positions;
    }

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

    private static int CountEnemies(WaveData.Wave wave)
    {
        int count = 0;

        foreach(WaveData.SpawnEntry entry in wave.Entries)
        {
            count += entry.Count;
        }

        return count;
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

        _waveEnemies.Clear();
        _remainingCount = 0;
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
