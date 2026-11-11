// 경직되는 적. 플레이어 타격과 패링 성공이 경직을 건다.
// 경직을 거는 쪽의 경직 레벨이 적의 경직 저항 레벨 이상일 때만 경직된다.
public interface IEnemyStaggerable
{
    // duration : 경직 시간(초, 적 전용 시간)
    // staggerLevel : 경직 레벨. 적의 경직 저항 레벨 이상이어야 경직된다.
    void Stagger(float duration, int staggerLevel);
}
