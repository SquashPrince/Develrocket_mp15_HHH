// 그로기가 되고, 그로기 중 콤보 스킬을 받을 수 있는 적.
// 플레이어 공격 적중 · 패링 성공이 그로기 게이지를 쌓고, 플레이어 강타가 콤보 스킬 기회를 확인할 때 쓴다.
public interface IEnemyGroggyable
{
    // 이 적에게 콤보 스킬이 이어지는 최대 연계 수. 적마다 다르다. (일반 1, 엘리트 2)
    int MaxComboChain { get; }

    // 그로기 게이지를 쌓는다. 게이지가 최대치에 이르면 그로기가 된다.
    void AddGroggy(float amount);

    // 그로기 중이고 이번 그로기에서 콤보 스킬을 아직 발동하지 않았으면 기회를 소모하고 true 를 돌려준다.
    bool TryConsumeComboSkillChance();
}
