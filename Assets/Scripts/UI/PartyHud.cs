using UnityEngine;

// 파티 HUD. 필드에 있는(조작 중인) 캐릭터와 대기 캐릭터들을 캐릭터 칸에 보여준다.
// 파티의 "조작 캐릭터 바뀜" 알림이 올 때만 칸을 다시 채운다. 시작할 때도 첫 캐릭터로 한 번 알림이 온다.
// 대기 칸은 교체 키 방향과 맞춘다. 이전 칸은 이전 교체 키(C)로, 다음 칸은 다음 교체 키(Space)로 나올 캐릭터다.
// 2인 파티는 이전 · 다음 캐릭터가 같으므로 이전 칸을 숨기고, 1인 파티는 대기 칸을 모두 숨긴다.
public class PartyHud : MonoBehaviour
{
    private const int PREVIOUS = -1;
    private const int NEXT = 1;

    [SerializeField] private PlayerParty _party;
    [SerializeField] private CharacterGauge _controlledGauge;
    [Tooltip("이전 교체 키(C)로 나올 대기 캐릭터 칸")]
    [SerializeField] private CharacterGauge _previousGauge;
    [Tooltip("다음 교체 키(Space)로 나올 대기 캐릭터 칸")]
    [SerializeField] private CharacterGauge _nextGauge;

    private void OnEnable() => BindPartyEvent();
    private void OnDisable() => UnbindPartyEvent();

    private void OnControlledChanged(PlayerController controlled)
    {
        _controlledGauge.Bind(controlled);

        int count = _party.Members.Count;
        int controlledIndex = FindIndex(controlled);

        // 다음 칸은 대기 캐릭터가 1 명 이상, 이전 칸은 2 명 이상일 때만 보인다.
        RefreshStandbyGauge(_nextGauge, controlledIndex, NEXT, count >= 2);
        RefreshStandbyGauge(_previousGauge, controlledIndex, PREVIOUS, count >= 3);
    }

    // 칸을 켜고 끈 뒤, 켜는 칸만 조작 캐릭터에서 방향만큼 떨어진 캐릭터로 채운다.
    private void RefreshStandbyGauge(CharacterGauge gauge, int controlledIndex, int direction, bool isVisible)
    {
        gauge.gameObject.SetActive(isVisible);

        if(!isVisible) return;

        int count = _party.Members.Count;
        gauge.Bind(_party.Members[(controlledIndex + direction + count) % count]);
    }

    private int FindIndex(PlayerController member)
    {
        for(int i = 0; i < _party.Members.Count; i++)
        {
            if(_party.Members[i] == member) return i;
        }

        return 0;
    }

    private void BindPartyEvent()
    {
        _party.ControlledChanged += OnControlledChanged;
    }

    private void UnbindPartyEvent()
    {
        _party.ControlledChanged -= OnControlledChanged;
    }
}
