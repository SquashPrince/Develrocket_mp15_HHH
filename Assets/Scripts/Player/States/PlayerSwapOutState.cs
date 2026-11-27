using UnityEngine;

// 교체로 필드에서 나가는 상태. 뒤돌기 → 달려 나가기 → 퇴장 시간이 지나면 꺼서 대기로 만든다. 끝까지 진행되게 무적이다.
// 벽에 막히면 제자리에서 달리다 사라진다. 퇴장 시간이 짧아 따로 처리하지 않는다.
public class PlayerSwapOutState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다.
    private static readonly int SWAP_OUT_ANIMATION = Animator.StringToHash("SwapOut");
    private static readonly int MOVE_ANIMATION = Animator.StringToHash("Move");

    private readonly DitherFade _ditherFade;
    private float _timer;
    private bool _isRunning;

    public PlayerSwapOutState(PlayerController controller) : base(controller)
    {
        _ditherFade = controller.GetComponent<DitherFade>();
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
    public override void Exit()
    {
        Controller.Animation.EndBodyPart();

        if(_ditherFade != null) _ditherFade.SetSwapOutVisibility(1f);
    }

    public override void Tick()
    {
        _timer += Controller.DeltaTime;

        if(_timer >= Controller.Common.SwapOutTime)
        {
            Controller.Standby();
            return;
        }

        if(!_isRunning && _timer >= Controller.Common.SwapOutRunDelay) BeginRun();

        if(!_isRunning) return;

        Controller.Animation.SetMoveSpeed(Controller.RunSpeed);

        if(_ditherFade == null) return;

        float progress = Mathf.InverseLerp(Controller.Common.SwapOutRunDelay, Controller.Common.SwapOutTime, _timer);
        _ditherFade.SetSwapOutVisibility(1f - progress);
    }

    // 퇴장 모션은 모델만 뒤돌고 캐릭터 방향은 그대로라, 캐릭터를 반대로 돌려야 돌아선 자세 그대로 달려 나간다.
    private void BeginRun()
    {
        _isRunning = true;
        Controller.FaceImmediately(-Controller.transform.forward);
        Controller.HorizontalVelocity = Controller.transform.forward * Controller.RunSpeed;
        Controller.Animation.EndBodyPart();
        Controller.Animation.Play(MOVE_ANIMATION);
    }
}
