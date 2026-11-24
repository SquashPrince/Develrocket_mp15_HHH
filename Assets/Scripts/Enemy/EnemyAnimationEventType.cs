// 적 공격 클립에 찍는 애니메이션 이벤트 종류. 클립의 이벤트 Function 이름과 같은 이름을 쓴다.
public enum EnemyAnimationEventType
{
    // 패링 공격의 준비 단계 클립에 넣는다. 이 순간 패링 가능 시간을 연다.
    Telegraph,
    HitStart,
    // 구간 판정 동작에서 없으면 그 동작이 끝날 때까지 판정한다.
    HitEnd,
    // 경직 저항을 이벤트까지 유지하는 공격에서만 쓴다.
    ResistEnd,
    LeapStart,
    // 없으면 LeapEnd 까지 목표 지점이 대상을 따라간다.
    LeapLock,
    // 착지 · 도착 순간. 판정은 이 이벤트와 별개로 HitStart 가 정한다.
    LeapEnd
}
