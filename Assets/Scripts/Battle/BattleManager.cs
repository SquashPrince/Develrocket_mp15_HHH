using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

// 전투에 하나뿐인 적 쪽 공용 규칙. 적 목록 · 공격 대상 지정, 근접 공격 권한, 적 공격 판정 Layer 를 맡는다.
// 공격 권한 : 다가가서 공격하는 적이 동시에 너무 많아지지 않게, 정해진 수만큼만 권한을 주고 나머지는 먼저 요청한 순서대로 기다리게 한다.
// 보스와 사거리 밖 공격(원거리 노드)은 권한을 받지 않는다.
public class BattleManager : MonoBehaviour
{
    // 적 행동 그래프 Blackboard 의 공격 대상 변수 이름
    private const string ENEMY_TARGET_VARIABLE = "Target";

    [SerializeField] private PlayerParty _party;
    [Tooltip("동시에 다가가 공격할 수 있는 적 수. 보스와 사거리 밖 공격은 세지 않는다")]
    [SerializeField, Min(1)] private int _maxMeleeAttackers = 3;
    [Tooltip("적 공격 판정에 걸릴 Layer (Player)")]
    [SerializeField] private LayerMask _playerLayers;

    // 처치되어 꺼진 적도 남겨 둔다. 시체는 꺼질 뿐 파괴되지 않고, 꺼진 그래프에 값을 넣어도 문제없다.
    private readonly List<EnemyController> _enemies = new List<EnemyController>();
    private readonly List<EnemyController> _attackers = new List<EnemyController>();
    // 먼저 요청한 적이 앞에 온다.
    private readonly List<EnemyController> _waitingAttackers = new List<EnemyController>();

    public IReadOnlyList<EnemyController> Enemies => _enemies;
    public LayerMask PlayerLayers => _playerLayers;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindPartyEvent();
    private void OnDisable() => UnbindPartyEvent();

    // 실행 중 생성한 적은 생성한 쪽이 등록한다. 다시 꺼내 쓰는 적(풀링)도 그대로 부르면 대상만 새로 넣는다.
    public void Register(EnemyController enemy)
    {
        if(!_enemies.Contains(enemy))
        {
            _enemies.Add(enemy);
            enemy.JoinBattle(this);
        }

        enemy.GetComponent<BehaviorGraphAgent>().SetVariableValue(ENEMY_TARGET_VARIABLE, GetEnemyTarget());
    }

    // 다가가서 공격하는 노드가 매 프레임 묻는다. 처음 물은 순간 줄을 서고, 자리가 나면 가장 오래 기다린 적부터 받는다.
    public bool TryAcquireAttack(EnemyController enemy)
    {
        if(enemy.Data.Grade == EnemyGrade.Boss || _attackers.Contains(enemy)) return true;

        if(!_waitingAttackers.Contains(enemy)) _waitingAttackers.Add(enemy);

        if(_attackers.Count >= _maxMeleeAttackers || _waitingAttackers[0] != enemy) return false;

        _waitingAttackers.RemoveAt(0);
        _attackers.Add(enemy);
        return true;
    }

    // 공격이 끝나거나 끊기면(경직 · 그로기 · 처치) 노드가 끝나며 부른다. 기다리던 중이면 줄에서 뺀다.
    public void ReleaseAttack(EnemyController enemy)
    {
        _attackers.Remove(enemy);
        _waitingAttackers.Remove(enemy);
    }

    // 전투가 끝나면 대상을 비워, 쓰러진 마지막 캐릭터를 쫓아와 공격하거나 밀지 않게 한다.
    private Transform GetEnemyTarget() => _party.IsBattleEnded ? null : _party.Controlled.transform;

    // 적은 공격을 시작할 때 대상을 따로 저장하므로, 진행 중인 공격은 이전 캐릭터 위치로 끝까지 시전한다.
    private void RetargetEnemies()
    {
        Transform target = GetEnemyTarget();

        for(int i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].GetComponent<BehaviorGraphAgent>().SetVariableValue(ENEMY_TARGET_VARIABLE, target);
        }
    }

    private void OnControlledChanged(PlayerController controlled) => RetargetEnemies();

    // 생성기가 실행 중 등록하는 적과 겹치지 않게, 씬에 놓인 적은 Awake 에서 찾아 목록에만 넣는다. 대상은 파티가 시작할 때 알리는 조작 캐릭터 변경에서 넣는다.
    private void CacheComponent()
    {
        foreach(EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            _enemies.Add(enemy);
            enemy.JoinBattle(this);
        }
    }

    private void BindPartyEvent()
    {
        _party.ControlledChanged += OnControlledChanged;
        _party.BattleEnded += RetargetEnemies;
    }

    private void UnbindPartyEvent()
    {
        _party.ControlledChanged -= OnControlledChanged;
        _party.BattleEnded -= RetargetEnemies;
    }
}
