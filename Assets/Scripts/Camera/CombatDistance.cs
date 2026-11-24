using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// 주변 적 수 · 엘리트 · 보스에 따라 조작 카메라의 거리를 늘려 많은 적이나 큰 적을 화면에 담는다. 여럿이면 가장 먼 쪽을 쓴다.
// Orbital Follow 의 반지름에 더하므로 마우스 휠 배율(Radial Axis)과 함께 적용된다. 실제 시간으로 서서히 바뀐다.
public class CombatDistance : MonoBehaviour
{
    private const int MAX_COLLIDERS = 64;

    [Tooltip("조작 중인 캐릭터를 읽을 파티")]
    [SerializeField] private PlayerParty _party;
    [Tooltip("거리를 늘릴 카메라의 Orbital Follow (일반 · 록온)")]
    [SerializeField] private CinemachineOrbitalFollow[] _orbitalFollows;
    [Tooltip("적을 찾을 Layer")]
    [SerializeField] private LayerMask _enemyLayers;
    [Tooltip("조작 캐릭터 주변에서 적을 세는 범위(m)")]
    [SerializeField, Min(0f)] private float _checkRange = 10f;

    [Header("적이 많을 때")]
    [Tooltip("범위 안에 살아 있는 적이 이 수 이상이면 거리를 늘린다")]
    [SerializeField, Min(1)] private int _crowdCount = 4;
    [Tooltip("적이 많을 때 더할 거리(m)")]
    [SerializeField, Min(0f)] private float _crowdDistance = 1f;

    [Header("엘리트")]
    [Tooltip("범위 안에 살아 있는 엘리트가 있을 때 더할 거리(m)")]
    [SerializeField, Min(0f)] private float _eliteDistance = 1f;

    [Header("보스")]
    [Tooltip("범위 안에 살아 있는 보스가 있을 때 더할 거리(m)")]
    [SerializeField, Min(0f)] private float _bossDistance = 1.5f;

    [Header("전환")]
    [Tooltip("추가 거리가 바뀌는 속도(m / 실제 시간 초)")]
    [SerializeField, Min(0.01f)] private float _changeSpeed = 1.5f;

    private readonly HashSet<EnemyController> _countedEnemies = new HashSet<EnemyController>();
    private readonly Collider[] _colliders = new Collider[MAX_COLLIDERS];
    private float[] _baseRadii;
    private float _currentDistance;

    private void Start() => Init();
    private void Update() => UpdateDistance();
    private void OnDisable() => ApplyDistance(0f);

    private void UpdateDistance()
    {
        _currentDistance = Mathf.MoveTowards(_currentDistance, GetTargetDistance(), _changeSpeed * Time.unscaledDeltaTime);
        ApplyDistance(_currentDistance);
    }

    // 범위 안의 살아 있는 적 수와 엘리트 · 보스 여부로 추가 거리를 정한다. 콜라이더가 여러 개인 적도 한 번만 센다.
    private float GetTargetDistance()
    {
        int count = Physics.OverlapSphereNonAlloc(_party.Controlled.transform.position, _checkRange, _colliders, _enemyLayers, QueryTriggerInteraction.Ignore);
        bool hasElite = false;
        bool hasBoss = false;

        _countedEnemies.Clear();

        for(int i = 0; i < count; i++)
        {
            EnemyController enemy = _colliders[i].GetComponentInParent<EnemyController>();

            if(enemy == null || !enemy.IsAlive || !_countedEnemies.Add(enemy)) continue;

            if(enemy.Data.Grade == EnemyGrade.Elite) hasElite = true;
            else if(enemy.Data.Grade == EnemyGrade.Boss) hasBoss = true;
        }

        float crowd = _countedEnemies.Count >= _crowdCount ? _crowdDistance : 0f;
        float elite = hasElite ? _eliteDistance : 0f;
        float boss = hasBoss ? _bossDistance : 0f;

        return Mathf.Max(crowd, Mathf.Max(elite, boss));
    }

    private void ApplyDistance(float distance)
    {
        if(_baseRadii == null) return;

        for(int i = 0; i < _orbitalFollows.Length; i++)
        {
            _orbitalFollows[i].Radius = _baseRadii[i] + distance;
        }
    }

    // 카메라에 설정한 반지름을 원래 값으로 기억해 둔다.
    private void Init()
    {
        _baseRadii = new float[_orbitalFollows.Length];

        for(int i = 0; i < _orbitalFollows.Length; i++)
        {
            _baseRadii[i] = _orbitalFollows[i].Radius;
        }
    }
}
