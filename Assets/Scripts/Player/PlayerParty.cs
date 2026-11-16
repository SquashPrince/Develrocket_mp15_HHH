using UnityEngine;

// 파티 전투의 캐릭터 목록과 공통 설정을 들고, 조작 중인 캐릭터와 교체를 맡는다.
// 공통 설정 에셋은 파티 하나가 들고, 각 캐릭터의 기능은 컨트롤러를 통해 이 파티의 공통 설정을 꺼내 쓴다.
//   캐릭터 Prefab 마다 같은 에셋을 연결하지 않아도 되고, 바꿀 곳이 이 파티 하나다.
// 캐릭터 하나만 있는 씬도 목록에 한 명을 넣어 같은 방식으로 쓴다. 교체할 캐릭터가 없어 교체 키는 아무 일도 하지 않는다.
// 조작 중인 캐릭터만 입력에 반응하고, 나머지 캐릭터의 입력은 끈다.
// 슬로모션처럼 전투 전체에 거는 효과는 캐릭터가 아닌 파티 오브젝트에 두고, 캐릭터는 파티를 통해 꺼내 쓴다.
//
// 교체 : 이전 · 다음 캐릭터 키로 목록 순서대로 돌아가며 교체한다. (마지막 다음은 처음, 처음 이전은 마지막)
//        다음 순서의 캐릭터가 아직 필드에 있으면(퇴장 중) 건너뛰고 같은 방향의 다음 대기 캐릭터로 교체한다.
//        대기 캐릭터가 없으면(모두 필드에 있음) 교체하지 않는다. 쿨타임은 없다.
//        새 캐릭터는 이전 캐릭터 자리에서 입장하고, 이전 캐릭터는 하던 동작을 마친 뒤 퇴장한다.
// 대기 : 필드에 없는 캐릭터는 꺼 둔다. 꺼진 오브젝트는 적에게 맞거나 대상으로 잡히지 않는다.
[RequireComponent(typeof(BattleSlowMotion))]
public class PlayerParty : MonoBehaviour
{
    private const int PREVIOUS = -1;
    private const int NEXT = 1;

    [Tooltip("파티 캐릭터 목록. 씬에 놓인 캐릭터를 넣는다. 이 순서대로 교체하고, 첫 번째 캐릭터를 조작하며 시작한다")]
    [SerializeField] private PlayerController[] _members;
    [Tooltip("모든 캐릭터가 함께 쓰는 규칙 값 · 입력")]
    [SerializeField] private CommonCharacterSettings _common;

    private int _controlledIndex;
    // 처음 조작하지 않는 캐릭터들을 껐는지
    private bool _isStandbyReady;

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
    private void Update()
    {
        HideStandbyOnce();
        ReadSwapInput();
    }

    private void JoinMembers()
    {
        for(int i = 0; i < _members.Length; i++)
        {
            _members[i].JoinParty(this);
        }
    }

    // 처음 조작하지 않는 캐릭터를 끈다. 캐릭터는 Start 에서 상태를 만들기 때문에, Start 전에 끄면 처음 교체할 때 상태가 없다.
    // 씬의 모든 Start 는 첫 Update 전에 끝나므로 첫 Update 에서 끈다.
    private void HideStandbyOnce()
    {
        if(_isStandbyReady) return;

        _isStandbyReady = true;

        for(int i = 0; i < _members.Length; i++)
        {
            if(i != _controlledIndex) _members[i].Standby();
        }
    }

    private void ReadSwapInput()
    {
        if(_common.ParryPreviousAction.action.WasPressedThisFrame()) TrySwap(PREVIOUS);
        else if(_common.ParryNextAction.action.WasPressedThisFrame()) TrySwap(NEXT);
    }

    // direction 쪽으로 목록을 돌며 처음 만나는 대기 캐릭터로 교체한다. 대기 캐릭터가 없으면 교체하지 않는다.
    private void TrySwap(int direction)
    {
        int nextIndex = FindStandbyIndex(direction);

        if(nextIndex < 0) return;

        PlayerController previous = Controlled;
        PlayerController next = _members[nextIndex];

        // 꺼져 있는 동안 옮겨 두어야 켜지는 순간 CharacterController 가 새 위치에서 시작한다.
        next.transform.SetPositionAndRotation(previous.transform.position, previous.transform.rotation);
        next.gameObject.SetActive(true);
        next.SwapIn();

        previous.ReserveSwapOut();
        SetControlled(nextIndex);
    }

    // 조작 중인 캐릭터에서 direction 쪽으로 한 칸씩 가며 꺼져 있는(대기) 캐릭터를 찾는다. 없으면 -1 이다.
    private int FindStandbyIndex(int direction)
    {
        for(int step = 1; step < _members.Length; step++)
        {
            int index = (_controlledIndex + direction * step + _members.Length) % _members.Length;

            if(!_members[index].gameObject.activeSelf) return index;
        }

        return -1;
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
