// 경직되는 적. 필살기 타격 · 패링 성공이 경직을 건다.
public interface IEnemyStaggerable
{
    // duration : 경직 시간(초, 적 전용 시간)
    void Stagger(float duration);
}
