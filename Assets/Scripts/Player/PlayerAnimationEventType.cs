// 공격 클립에 찍는 애니메이션 이벤트 종류. 클립의 이벤트 Function 이름과 같은 이름을 쓴다.
public enum PlayerAnimationEventType
{
    // 이 시점부터 누른 공격은 저장했다가 공격 동작이 끝나면 다음 단계로 이어간다.
    ComboInputOpen,
    // 칼날이 맞는 순간. 이 순간 한 번 판정한다.
    Hit,
    // 휘두르기가 끝나고 후딜(자세를 되돌리는 구간)이 시작되는 지점
    AttackMotionEnd,
    // 후딜이 끝나 대기 자세로 돌아온 지점
    RecoveryEnd
}
