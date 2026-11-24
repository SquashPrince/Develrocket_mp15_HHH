using UnityEngine;

// 버튼 HUD. 캐릭터마다 달라지는 표시(특수 스킬 사용 가능 · 회피 쿨타임)만 맡고, 눌림 표시는 각 버튼 칸이 맡는다.
// 에너지와 쿨타임은 계속 바뀌므로 매 프레임 조작 캐릭터에서 읽는다.
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
