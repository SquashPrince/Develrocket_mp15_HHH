// 그로기가 되고, 그로기 중 콤보 스킬을 받을 수 있는 적.
// 그로기 테스트 입력이 그로기를 걸고, 플레이어 강타가 콤보 스킬 기회를 확인할 때 쓴다.
public interface IEnemyGroggyable
{
    // 이 적에게 콤보 스킬이 이어지는 최대 연계 수. 적마다 다르다. (일반 1, 엘리트 2)
    int MaxComboChain { get; }

    void EnterGroggy();

    // 그로기 중이고 이번 그로기에서 콤보 스킬을 아직 발동하지 않았으면 기회를 소모하고 true 를 돌려준다.
    bool TryConsumeComboSkillChance();
}
