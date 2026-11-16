using UnityEngine;

// 버튼 HUD. 조작 캐릭터의 값으로 스킬 칸의 사용 가능 여부와 회피 칸의 쿨타임 표시를 정한다.
// 눌림 표시는 각 버튼 칸이 입력을 직접 읽어 처리하므로, 여기서는 캐릭터마다 달라지는 표시만 맡는다.
//   스킬 : 에너지가 특수스킬 에너지 비용 이상이면 원래 색, 모자라면 회색. 일반 스킬은 항상 쓸 수 있어 아이콘은 그대로 둔다.
//   회피 : 대시 횟수를 모두 써서 쓸 수 없을 때만 다음 횟수가 찰 때까지의 쿨타임을 보여준다. 남은 횟수는 보여주지 않는다.
// 에너지와 쿨타임은 시간이 지나며 계속 바뀌므로 매 프레임 조작 캐릭터에서 읽는다. 교체하면 다음 프레임부터 새 캐릭터 값을 읽는다.
public class ActionButtonHud : MonoBehaviour
{
    [SerializeField] private PlayerParty _party;
    [SerializeField] private ActionButton _skillButton;
    [SerializeField] private ActionButton _dashButton;

    private void Update() => Refresh();

    private void Refresh()
    {
        PlayerController controlled = _party.Controlled;

        _skillButton.SetAvailable(controlled.Status.CurrentEnergy >= controlled.Character.SpecialSkill.EnergyCost);
        _dashButton.SetCooldown(controlled.HasDashStack ? 0f : controlled.DashCooldownRatio);
    }
}
