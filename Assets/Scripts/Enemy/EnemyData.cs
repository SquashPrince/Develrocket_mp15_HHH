using UnityEngine;

// 적 한 종류의 조정값. 오브젝트 참조(록온 표시 위치 · 예고 위치 · Animator 등)와 실행 중 현재값은 Prefab 에 둔다.
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HHH/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Tooltip("보스는 등장할 때 보스 바를 띄운다. 엘리트는 지금은 일반 적과 같게 동작한다")]
    [SerializeField] private EnemyGrade _grade;

    [Header("표시")]
    [Tooltip("이름. 이름 글자를 연결한 막대(보스 바)에 보인다. 정체를 숨길 보스는 ??? 처럼 적는다. 비워 두면 에셋 이름을 쓴다")]
    [SerializeField] private string _displayName;
    [Tooltip("[선택] 초상화 Image 를 연결한 막대(보스 바)에 띄울 초상화. 비워 두면 빈 Image 로 보인다")]
    [SerializeField] private Sprite _portrait;

    [Header("스테이터스")]
    [SerializeField, Min(1f)] private float _maxHealth = 100f;
    [Tooltip("끄면 체력이 0 이어도 강타에 처치되지 않는다 (시험용 허수아비 등)")]
    [SerializeField] private bool _canDie = true;
    [Tooltip("처치된 뒤 시체가 남아 있다가 사라지기까지의 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _corpseDuration = 3f;
    [Tooltip("평소 경직 저항 레벨. 경직을 거는 쪽의 경직 레벨이 이 값 이상이어야 경직된다")]
    [SerializeField, Min(0)] private int _baseStaggerResistLevel;
    [Tooltip("그로기 게이지 최대치. 게이지가 이 값에 이르면 그로기가 된다")]
    [SerializeField, Min(1f)] private float _maxGroggy = 100f;
    [Tooltip("그로기가 유지되는 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _groggyTime = 12f;
    [Tooltip("그로기 중에 받는 대미지 배율. 1.5 면 그로기 동안 1.5 배 대미지를 받는다")]
    [SerializeField, Min(0f)] private float _groggyDamageMultiplier = 1.5f;
    [Tooltip("이 적에게 콤보 스킬이 이어지는 최대 연계 수. 일반 1, 엘리트 2")]
    [SerializeField, Min(1)] private int _maxComboChain = 1;

    [Header("처치 연출")]
    [Tooltip("시체가 사라질 때 녹아 없어지게 바꿔 끼울 Material (DissolveShader). 적 Prefab 의 EnemyDissolve 에 연결한 Renderer 에 쓴다. 비워 두면 녹지 않고 그냥 사라진다")]
    [SerializeField] private Material _dissolveMaterial;
    [Tooltip("녹아 없어지는 데 걸리는 시간(초, 적 전용 시간). 시체 시간이 끝나기 이 시간만큼 전에 시작한다. 시체 시간보다 길면 처치되자마자 시작해 시체 시간 안에 끝낸다")]
    [SerializeField, Min(0.01f)] private float _dissolveDuration = 1f;
    [Tooltip("녹기 시작할 때 적 위치(발밑 기준)에 적이 바라보는 방향으로 띄우는 이펙트 (날리는 흔적 등). 적에 붙이지 않아 적이 사라진 뒤에도 끝날 때까지 남는다. 비워 두면 띄우지 않는다")]
    [SerializeField] private GameObject _deathEffectPrefab;

    [Header("이동")]
    [Tooltip("추적할 때의 이동 속도(초당 거리)")]
    [SerializeField, Min(0f)] private float _moveSpeed = 3f;
    [Tooltip("초당 회전 각도")]
    [SerializeField, Min(0f)] private float _turnSpeed = 540f;
    [Tooltip("목적지가 이 거리보다 적게 바뀌면 경로를 다시 계산하지 않는다")]
    [SerializeField, Min(0f)] private float _repathDistance = 0.3f;

    [Header("행동 : 추적")]
    [Tooltip("추적 · 공격 접근(Run)할 때의 이동 속도. 추적 이동 속도에 곱하는 배율이다 (1 = 같은 속도). 거리 유지 · 백스텝 속도에는 적용하지 않는다")]
    [SerializeField, Min(0f)] private float _runSpeedRate = 1f;

    [Header("행동 : 거리 유지")]
    [Tooltip("대상과 유지할 거리. 추적은 이 거리까지 다가가면 끝나고, 거리 유지는 대상 둘레의 이 거리 위를 돈다")]
    [SerializeField, Min(0f)] private float _keepDistance = 4f;
    [Tooltip("거리 유지 중 이 값보다 더 벌어지면 추적 속도로 다가간다")]
    [SerializeField, Min(0f)] private float _keepDistanceTolerance = 1f;
    [Tooltip("거리 유지 중 대상 둘레를 도는 속도(초당 거리)")]
    [SerializeField, Min(0f)] private float _strafeSpeed = 1.5f;
    [Tooltip("거리 유지를 하는 최소 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _keepDistanceTimeMin = 2f;
    [Tooltip("거리 유지를 하는 최대 시간(초, 적 전용 시간). 최소 ~ 최대 사이에서 매번 무작위로 정한다")]
    [SerializeField, Min(0f)] private float _keepDistanceTimeMax = 4f;
    [Tooltip("거리 유지를 시작할 때 좌 · 우로 도는 대신 백스텝할 확률")]
    [SerializeField, Range(0f, 1f)] private float _backStepChance = 0.3f;
    [Tooltip("거리 유지를 시작할 때 좌 · 우로 도는 대신 제자리에서 대상만 바라보며 기다릴 확률. 시간은 거리 유지 시간을 쓴다. 백스텝 확률과 합이 1 을 넘으면 남은 만큼만 적용된다")]
    [SerializeField, Range(0f, 1f)] private float _idleChance = 0f;
    [Tooltip("백스텝 중 뒤로 가는 속도. 추적 이동 속도에 곱하는 배율이다 (1 = 같은 속도). 물러나는 거리 = 속도 × 백스텝 모션 길이")]
    [SerializeField, Min(0f)] private float _backStepSpeedRate = 1f;

    [Header("행동 : 공격")]
    [Tooltip("이 거리 안에 들어오면 공격을 시작한다. 밖이면 이 거리까지 다가간다")]
    [SerializeField, Min(0f)] private float _attackRange = 2f;
    [Tooltip("공격을 마친 뒤 다음 행동까지 기다리는 시간(초, 적 전용 시간)")]
    [SerializeField, Min(0f)] private float _attackInterval = 2f;
    [Tooltip("이 적이 쓰는 공격 목록 (적 공격 데이터 에셋)")]
    [SerializeField] private EnemyAttackData[] _attacks;

    public EnemyGrade Grade => _grade;
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public Sprite Portrait => _portrait;
    public float MaxHealth => _maxHealth;
    public bool CanDie => _canDie;
    public float CorpseDuration => _corpseDuration;
    public Material DissolveMaterial => _dissolveMaterial;
    public float DissolveDuration => _dissolveDuration;
    public GameObject DeathEffectPrefab => _deathEffectPrefab;
    public int BaseStaggerResistLevel => _baseStaggerResistLevel;
    public float MaxGroggy => _maxGroggy;
    public float GroggyTime => _groggyTime;
    public float GroggyDamageMultiplier => _groggyDamageMultiplier;
    public int MaxComboChain => _maxComboChain;
    public float MoveSpeed => _moveSpeed;
    public float TurnSpeed => _turnSpeed;
    public float RepathDistance => _repathDistance;
    public float RunSpeedRate => _runSpeedRate;
    public float KeepDistance => _keepDistance;
    public float KeepDistanceTolerance => _keepDistanceTolerance;
    public float StrafeSpeed => _strafeSpeed;
    public float BackStepChance => _backStepChance;
    public float IdleChance => _idleChance;
    public float BackStepSpeedRate => _backStepSpeedRate;
    public float AttackRange => _attackRange;
    public float AttackInterval => _attackInterval;
    public EnemyAttackData[] Attacks => _attacks;

    // 이번 거리 유지 시간을 최소 ~ 최대 사이에서 무작위로 정한다. 최대가 최소보다 작으면 최소를 쓴다.
    public float PickKeepDistanceTime() => Random.Range(_keepDistanceTimeMin, Mathf.Max(_keepDistanceTimeMin, _keepDistanceTimeMax));
}
