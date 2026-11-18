using System;
using UnityEngine;

// 웨이브 구성. 웨이브마다 생성할 적 종류(Prefab)와 마리 수를 들고, 목록 순서대로 진행한다.
// 적 데이터가 아닌 Prefab 을 넣는 이유 : Prefab 이 적 데이터를 들고 있어, 데이터만으로는 무엇을 생성할지 알 수 없기 때문이다.
// 웨이브 사이 대기 시간과 생성 위치 간격도 여기서 정한다. 실행 중 바꾼 값이 플레이를 끝내도 남아 조정하기 쉽다.
[CreateAssetMenu(fileName = "NewWaveData", menuName = "HHH/Wave Data")]
public class WaveData : ScriptableObject
{
    // 한 웨이브에 생성할 적 한 종류
    [Serializable]
    public class SpawnEntry
    {
        [SerializeField] private EnemyController _prefab;
        [SerializeField, Min(1)] private int _count = 1;

        public EnemyController Prefab => _prefab;
        public int Count => _count;
    }

    // 웨이브 하나. 제한 시간을 쓰는 웨이브가 보스전이다. 스테이지의 마지막 웨이브 하나에만 켠다.
    //   보스전은 웨이브가 나오는 순간 제한 시간을 재기 시작하고, 그 안에 보스 등급 적을 모두 처치해야 한다.
    //   중간 보스처럼 제한 시간 없이 보스만 나오는 웨이브는 끈 채로 둔다.
    [Serializable]
    public class Wave
    {
        [Tooltip("이 웨이브에 생성할 적 종류와 마리 수. 보스 등급 적은 보스 생성 위치에 나온다")]
        [SerializeField] private SpawnEntry[] _entries;
        [Tooltip("보스전 웨이브인지. 켜면 이 웨이브가 나오는 순간부터 제한 시간을 잰다. 마지막 웨이브 하나에만 켠다")]
        [SerializeField] private bool _useTimeLimit;
        [Tooltip("보스 등급 적을 모두 처치해야 하는 제한 시간(초)")]
        [SerializeField, Min(1f)] private float _timeLimit = 180f;

        public SpawnEntry[] Entries => _entries;
        public bool UseTimeLimit => _useTimeLimit;
        public float TimeLimit => _timeLimit;
    }

    [Tooltip("웨이브 목록. 위에서부터 차례로 진행한다")]
    [SerializeField] private Wave[] _waves;

    [Header("진행")]
    [Tooltip("웨이브를 정리한 뒤 다음 웨이브가 나오기까지 기다리는 시간(초)")]
    [SerializeField, Min(0f)] private float _waveInterval = 2f;

    [Header("생성 위치")]
    [Tooltip("생성한 적끼리 떨어질 최소 거리(m)")]
    [SerializeField, Min(0.5f)] private float _minSpacing = 3f;
    [Tooltip("조작 중인 캐릭터와 떨어질 최소 거리(m)")]
    [SerializeField, Min(0f)] private float _minPlayerDistance = 5f;

    public int WaveCount => _waves.Length;
    public float WaveInterval => _waveInterval;
    public float MinSpacing => _minSpacing;
    public float MinPlayerDistance => _minPlayerDistance;

    public Wave GetWave(int index) => _waves[index];
}
