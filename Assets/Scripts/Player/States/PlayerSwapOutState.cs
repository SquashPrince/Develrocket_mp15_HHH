using UnityEngine;

// 교체로 필드에서 나가는 상태. 퇴장 모션(뒤돌기)을 재생한 뒤 달리기로 이어 나가고, 퇴장 시간이 지나면 캐릭터를 꺼 대기 상태로 만든다.
// 퇴장하는 동안은 무적이라 퇴장이 끝까지 진행된다.
// 하던 동작이 있으면 그 동작을 끝까지 마친 뒤 이 상태로 들어온다. (컨트롤러의 퇴장 예약)
// 달리기 : 퇴장 달리기 전환 시간이 지나면 퇴장 시작 때 보던 방향의 반대로 몸을 돌리고, 캐릭터의 달리기 모션 · 속도로 달려 나간다.
//   퇴장 모션은 모델만 뒤돌고 캐릭터 방향은 그대로이므로, 전환 순간 캐릭터를 반대로 돌려야 돌아선 자세에서 그대로 달려 나간다.
//   벽에 막히면 제자리에서 달리다 사라진다. 퇴장 시간이 짧아 따로 처리하지 않는다.
// 퇴장 시간 · 달리기 전환 시간은 모든 캐릭터가 같이 쓰는 공통 설정에서 읽는다.
public class PlayerSwapOutState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int SWAP_OUT_ANIMATION = Animator.StringToHash("SwapOut");
    private static readonly int MOVE_ANIMATION = Animator.StringToHash("Move");

    private float _timer;
    private bool _isRunning;

    public PlayerSwapOutState(PlayerController controller) : base(controller)
    {
    }

    public override void Enter()
    {
        _timer = 0f;
        _isRunning = false;
        Controller.HorizontalVelocity = Vector3.zero;
        Controller.Status.BeginInvincible(Controller.Common.SwapOutTime);
        Controller.Animation.PlayOnBodyPart(SWAP_OUT_ANIMATION, Controller.Character.SwapOutBodyPart);
    }

    // 꺼진 채로 다음 교체를 기다리다 다시 들어올 때 이 상태를 나가며 부위 덮기 레이어를 끈다.
    public override void Exit() => Controller.Animation.EndBodyPart();

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

        if(_timer >= Controller.Common.SwapOutTime)
        {
            Controller.Standby();
            return;
        }

        if(!_isRunning && _timer >= Controller.Common.SwapOutRunDelay) BeginRun();

        if(_isRunning) Controller.Animation.SetMoveSpeed(Controller.RunSpeed);
    }

    // 몸을 반대로 돌리고 달리기 모션으로 바꿔 그 방향으로 달려 나간다. 부위 덮기는 달리기에 남지 않도록 끈다.
    private void BeginRun()
    {
        _isRunning = true;
        Controller.FaceImmediately(-Controller.transform.forward);
        Controller.HorizontalVelocity = Controller.transform.forward * Controller.RunSpeed;
        Controller.Animation.EndBodyPart();
        Controller.Animation.Play(MOVE_ANIMATION);
    }
}
