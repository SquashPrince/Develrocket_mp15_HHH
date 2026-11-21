using UnityEngine;

// 전투 구역. 맵 위의 한 영역으로, 조작 중인 캐릭터가 들어오면 게임 흐름에 알리고, 전투 동안 막는 벽을 켠다.
// 구역은 진입 감지 · 벽 · 구역 설정(웨이브 구성 · 생성 영역 · 보스 위치 · 다시 시작 지점)만 가진다. 구역 순서와 웨이브 진행은 게임 흐름이 맡는다.
// 진입 : 이 오브젝트의 트리거 Collider 에 캐릭터가 들어오면 알린다. 조작 중인 캐릭터인지 · 지금 진행할 구역인지는 게임 흐름이 확인한다.
//   트리거 Collider 의 Layer 는 캐릭터와 충돌하도록 둔다.
// 벽 : 전투가 시작되면 켜고 정리되면 끈다. 씬에 저장된 상태와 상관없이 꺼진 채로 시작한다.
// 다시 시작 지점 : 이 구역을 정리한 뒤 전멸 · 재도전하면 파티가 설 위치 · 방향이다. 다음 구역 입구 앞에 둔다.
[RequireComponent(typeof(Collider))]
public class BattleZone : MonoBehaviour
{
    [Tooltip("이 구역의 웨이브 구성. 제한 시간을 쓰는 웨이브가 있으면 보스 구역이다")]
    [SerializeField] private WaveData _waveData;
    [Tooltip("전투 동안 켜서 구역을 막는 벽")]
    [SerializeField] private GameObject[] _walls;

    [Header("생성 위치")]
    [Tooltip("생성 영역의 중심. 이 위치를 중심으로 한 수평 원 안에 일반 · 엘리트 적을 만든다")]
    [SerializeField] private Transform _spawnCenter;
    [Tooltip("생성 영역 반지름(m)")]
    [SerializeField, Min(1f)] private float _spawnRadius = 10f;
    [Tooltip("보스 등급 적이 나올 위치. 보스가 없는 구역은 비워 둔다")]
    [SerializeField] private Transform _bossSpawnPoint;

    [Header("다시 시작")]
    [Tooltip("이 구역을 정리한 뒤 전멸 · 재도전하면 파티가 설 위치 · 방향")]
    [SerializeField] private Transform _restartPoint;

    public WaveData Waves => _waveData;
    public Vector3 SpawnCenter => _spawnCenter.position;
    public float SpawnRadius => _spawnRadius;
    public Vector3 BossSpawnPosition => _bossSpawnPoint.position;
    public Transform RestartPoint => _restartPoint;

    // 캐릭터가 이 구역에 들어오면 알린다. 전달 값 : 이 구역, 들어온 캐릭터
    public event System.Action<BattleZone, PlayerController> Entered;

    private void Awake() => SetWalls(false);

    private void OnTriggerEnter(Collider other)
    {
        if(!other.TryGetComponent(out PlayerController member)) return;

        Entered?.Invoke(this, member);
    }

    public void SetWalls(bool isActive)
    {
        foreach(GameObject wall in _walls)
        {
            wall.SetActive(isActive);
        }
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
