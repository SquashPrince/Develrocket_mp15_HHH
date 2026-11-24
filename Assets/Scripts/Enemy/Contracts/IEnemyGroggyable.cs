// 그로기가 되고, 그로기 중 콤보 스킬을 받을 수 있는 적.
// 그로기 시간을 언제 멈출지는 플레이어 쪽(콤보 스킬 선택)이 정하고, 적은 시키는 대로 멈춘다.
public interface IEnemyGroggyable
{
    bool IsGroggy { get; }

    bool HasComboSkillChance { get; }

    // 연계 수는 적마다 다르다.
    bool HasRemainingComboChain { get; }

    // 이번에 그로기가 되었으면 true
    bool AddGroggy(float amount);

    bool TryConsumeComboSkillChance();

    void UseComboChain();

    // 콤보 스킬을 이어받을 캐릭터가 없을 때 부른다.
    void ClearComboChain();

    // 멈춰 있던 정지 시간은 버리고 duration 부터 다시 센다. 남은 그로기 시간은 그대로다.
    void HoldGroggy(float duration);

    // 콤보 스킬 캐릭터 선택 동안 켠다.
    void SetGroggyPaused(bool isPaused);
}
