using UnityEngine;

// 콤보 스킬 상태. 그로기 적 앞으로 순간 이동한 뒤 연계 단계(ComboSkill 1, ComboSkill 2 ...)를 이어 쓰고 이동으로 돌아간다.
// 각 단계는 모션 끝 이벤트(AttackMotionEnd)를 받으면 다음 단계로 넘어가고, 마지막 단계가 끝나면 이전 상태가 아닌 이동 상태로 돌아간다.
// 같은 상태를 연속으로 다시 재생하면 처음부터 재생되지 않거나 이전 회차의 이벤트와 구분할 수 없어, 같은 클립이라도 단계마다 상태를 나눈다.
// 단계마다 일반 공격처럼 대상 앞 정지 거리까지 다가간다.
// 발동부터 끝까지 무적이고, 어떤 입력으로도 끊기지 않도록 입력을 읽지 않는다. 적 시간은 멈추지 않는다.
public class PlayerComboSkillState : PlayerState
{
    // Animator Controller 의 상태 이름과 같아야 한다. "ComboSkill 1", "ComboSkill 2" ... 순서다.
    private const string ANIMATION_PREFIX = "ComboSkill ";

    private readonly PlayerComboSkill _comboSkill;
    private IEnemyTargetable _target;
    private Vector3 _advanceVelocity;
    private float _advanceTimer;
    private float _advanceDuration;
    private int _step;
    private int _animationHash;

    public PlayerComboSkillState(PlayerController controller, PlayerComboSkill comboSkill) : base(controller)
    {
        _comboSkill = comboSkill;
    }

    public bool IsActive { get; private set; }

    // target : 콤보 스킬을 발동시킨 그로기 적. 순간 이동과 단계별 접근의 대상이 된다.
    public void Setup(IEnemyTargetable target) => _target = target;

    public override void Enter()
    {
        IsActive = true;
        _step = -1;

        // 남아 있을 수 있는 대시 무적 · 완벽 회피 판정을 지운 뒤, 콤보 스킬이 끝날 때까지 무적으로 둔다.
        // 공격 무적 중에는 패링 입력도 무시되므로 패링 · 교체 키로도 끊기지 않는다.
        Controller.Health.EndInvincible();
        Controller.Health.SetAttackInvincible(true);

        _comboSkill.TeleportToTarget(_target);
        StartNextStep();
    }

    public override void Exit()
    {
        IsActive = false;
        Controller.Health.SetAttackInvincible(false);
    }

    public override void Tick() => Controller.HorizontalVelocity = UpdateAdvance();

    public override void OnAnimationEvent(PlayerAnimationEventType type, int stateHash)
    {
        // 이전 단계 클립과 섞이는 동안 들어오는 이전 클립의 이벤트는 무시한다.
        if(stateHash != _animationHash) return;

        switch(type)
        {
            case PlayerAnimationEventType.Hit:
            case PlayerAnimationEventType.Impact:
                Controller.PerformHit(PlayerAttackState.AttackType.ComboSkill, _comboSkill.GetChainStep(_step), isImpact: type == PlayerAnimationEventType.Impact);
                break;
            case PlayerAnimationEventType.AttackMotionEnd:
                StartNextStep();
                break;
        }
    }

    private void StartNextStep()
    {
        _step++;

        if(_step >= _comboSkill.ChainStepCount)
        {
            Controller.EnterLocomotion(false);
            return;
        }

        PlayerAttackSetting setting = _comboSkill.GetChainStep(_step);

        // 발동시킨 그로기 적 쪽으로 돌아 대상 앞 정지 거리까지, 단계의 전진 시간 동안 다가간다. 도중에 록온이 바뀌어도 대상은 그대로다.
        float advanceDistance = Controller.AimFixedTarget(setting, _target);

        _advanceDuration = setting.AdvanceDuration;
        _advanceTimer = 0f;
        _advanceVelocity = Controller.transform.forward * (advanceDistance / _advanceDuration);

        _animationHash = Animator.StringToHash(ANIMATION_PREFIX + (_step + 1));
        Controller.PlayAnimation(_animationHash, setting.FadeTime);
    }

    // 전진 시간 동안만 앞으로 나아가고, 그 뒤에는 제자리에 멈춘다.
    private Vector3 UpdateAdvance()
    {
        _advanceTimer += Time.deltaTime;

        return _advanceTimer < _advanceDuration ? _advanceVelocity : Vector3.zero;
    }
}
