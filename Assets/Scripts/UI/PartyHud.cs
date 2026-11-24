using UnityEngine;

// 파티 HUD. 조작 캐릭터와 대기 캐릭터를 칸에 보여주고, 조작 캐릭터가 바뀔 때만 다시 채운다.
// 대기 칸은 교체 키 방향(이전 · 다음)과 맞춘다.
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

        // 2인 파티는 이전 · 다음 캐릭터가 같으므로 이전 칸을 숨긴다.
        RefreshStandbyGauge(_nextGauge, controlledIndex, NEXT, count >= 2);
        RefreshStandbyGauge(_previousGauge, controlledIndex, PREVIOUS, count >= 3);
    }

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
