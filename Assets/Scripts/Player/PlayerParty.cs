using UnityEngine;

// 파티 전투의 캐릭터 목록과 공통 설정을 들고, 조작 중인 캐릭터를 정한다.
// 공통 설정 에셋은 파티 하나가 들고, 각 캐릭터의 기능은 컨트롤러를 통해 이 파티의 공통 설정을 꺼내 쓴다.
//   캐릭터 Prefab 마다 같은 에셋을 연결하지 않아도 되고, 바꿀 곳이 이 파티 하나다.
// 캐릭터 하나만 있는 씬도 목록에 한 명을 넣어 같은 방식으로 쓴다.
// 조작 중인 캐릭터만 입력에 반응하고, 나머지 캐릭터의 입력은 끈다.
// 슬로모션처럼 전투 전체에 거는 효과는 캐릭터가 아닌 파티 오브젝트에 두고, 캐릭터는 파티를 통해 꺼내 쓴다.
[RequireComponent(typeof(BattleSlowMotion))]
public class PlayerParty : MonoBehaviour
{
    [Tooltip("파티 캐릭터 목록. 씬에 놓인 캐릭터를 넣는다. 첫 번째 캐릭터를 조작하며 시작한다")]
    [SerializeField] private PlayerController[] _members;
    [Tooltip("모든 캐릭터가 함께 쓰는 규칙 값 · 입력")]
    [SerializeField] private CommonCharacterSettings _common;

    private int _controlledIndex;

    public CommonCharacterSettings Common => _common;
    public PlayerController Controlled => _members[_controlledIndex];
    public BattleSlowMotion SlowMotion { get; private set; }

    // 캐릭터 기능은 Start 부터 공통 설정을 읽는다. 그래서 캐릭터의 Start 보다 먼저 도는 Awake 에서 파티를 알려 준다.
    private void Awake()
    {
        CacheComponent();
        JoinMembers();
    }
    // 입력 기능은 각 캐릭터의 Awake 에서 준비되므로, 모든 Awake 가 끝난 뒤인 Start 에서 조작 캐릭터를 정한다.
    private void Start() => Init();

    private void JoinMembers()
    {
        for(int i = 0; i < _members.Length; i++)
        {
            _members[i].JoinParty(this);
        }
    }

    // index 의 캐릭터만 입력에 반응하게 한다.
    private void SetControlled(int index)
    {
        _controlledIndex = index;

        for(int i = 0; i < _members.Length; i++)
        {
            _members[i].Input.IsControlled = i == index;
        }
    }

    private void CacheComponent()
    {
        SlowMotion = GetComponent<BattleSlowMotion>();
    }

    private void Init()
    {
        SetControlled(0);
    }
}
