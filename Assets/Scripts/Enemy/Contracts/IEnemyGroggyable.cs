// 그로기가 되고, 그로기 중 콤보 스킬을 받을 수 있는 적.
// 플레이어 공격 적중 · 패링 성공이 그로기 게이지를 쌓고, 플레이어 강타가 콤보 스킬 기회를 확인할 때 쓴다.
// 그로기 시간을 언제 멈출지는 플레이어 쪽(콤보 스킬 선택)이 정하고, 적은 시키는 대로 멈춘다.
public interface IEnemyGroggyable
{
    // 이 적에게 콤보 스킬을 쓸 수 있는 최대 횟수(연계 수). 적마다 다르다. (일반 1, 엘리트 2)
    int MaxComboChain { get; }

    bool IsGroggy { get; }

    // 그로기 중이고 이번 그로기에서 콤보 스킬 기회가 남아 있는지
    bool HasComboSkillChance { get; }

    // 그로기 게이지를 쌓는다. 게이지가 최대치에 이르면 그로기가 되고, 이번에 그로기가 되었으면 true 를 돌려준다.
    bool AddGroggy(float amount);

    // 그로기 중이고 이번 그로기에서 콤보 스킬을 아직 발동하지 않았으면 기회를 소모하고 true 를 돌려준다.
    bool TryConsumeComboSkillChance();

    // 지금부터 duration 동안 그로기 시간이 줄지 않게 한다. 이미 멈춰 있던 남은 시간은 버리고 처음부터 센다.
    // 남은 그로기 시간은 초기화하지 않는다.
    void HoldGroggy(float duration);

    // 켜 두는 동안 그로기 시간이 줄지 않는다. 콤보 스킬 캐릭터 선택 동안 켠다.
    void SetGroggyPaused(bool isPaused);
}
