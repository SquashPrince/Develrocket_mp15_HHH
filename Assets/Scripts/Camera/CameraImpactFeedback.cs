using System;
using Unity.Cinemachine;
using UnityEngine;
using Random = UnityEngine.Random;

// 전투 연출 알림을 받아 카메라를 흔들고 FOV 를 바꾼다. 게임 시간 감속과 상관없이 실제 시간으로 흐른다.
// 흔들림 모양 · 세기는 각 Impulse Source 가 정하고 여기서는 방향만 정한다. Listener 의 Use Camera Space 가 켜져 있어야 화면 기준이 된다.
// FOV 는 순간 줌 + 유지 줌을 만들고, 각 카메라의 CameraFovOffset 이 렌즈에 더한다.
public class CameraImpactFeedback : MonoBehaviour
{
    // 순간 하나의 FOV 변화. 곡선 값(시간 0 ~ 1) × 변화량만큼 FOV 를 바꾼다.
    [Serializable]
    private class FovPulse
    {
        [Tooltip("곡선 값이 1 일 때 FOV 변화량(도). 음수면 줌 인(화면이 당겨짐)")]
        [SerializeField] private float _amount;
        [Tooltip("효과 길이(실제 시간, 초)")]
        [SerializeField, Min(0.01f)] private float _duration;
        [Tooltip("시간(0 ~ 1)에 따른 변화 비율. 0 에서 시작해 0 으로 끝나야 원래 FOV 로 돌아온다")]
        [SerializeField] private AnimationCurve _curve;

        public float Duration => _duration;

        public FovPulse(float amount, float duration, AnimationCurve curve)
        {
            _amount = amount;
            _duration = duration;
            _curve = curve;
        }

        public float Evaluate(float time) => _amount * _curve.Evaluate(time / _duration);
    }

    [Tooltip("화면을 비추는 메인 카메라. 충격 방향을 화면 기준으로 바꾸고, 흔들림을 카메라 위치에서 낸다")]
    [SerializeField] private Camera _mainCamera;
    [Tooltip("완벽 회피 슬로모션. 진행 중인 동안 유지 줌을 건다")]
    [SerializeField] private BattleSlowMotion _slowMotion;

    [Header("순간별 흔들림 (비우면 흔들지 않음)")]
    [SerializeField] private CinemachineImpulseSource _impactHitShake;
    [SerializeField] private CinemachineImpulseSource _parrySuccessShake;
    [SerializeField] private CinemachineImpulseSource _knockbackShake;

    [Header("순간별 FOV")]
    [Tooltip("패링 시작 : 적 앞으로 들어가는 순간 짧게 줌 인했다가 돌아온다")]
    [SerializeField] private FovPulse _parryStartFov = new FovPulse(-4f, 0.25f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));
    [Tooltip("패링 성공 : 짧게 줌 인했다가 돌아온다")]
    [SerializeField] private FovPulse _parrySuccessFov = new FovPulse(-6f, 0.3f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));
    [Tooltip("웨이브 마지막 적 처치 : 느려지는 동안 줌 인했다가 돌아온다. 길이는 마지막 일격 기능의 슬로우 시간에 맞춘다")]
    [SerializeField] private FovPulse _waveFinishFov = new FovPulse(-8f, 1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
    [Tooltip("보스 처치 : 멈춘 동안 줌 인했다가 돌아온다. 길이는 마지막 일격 기능의 결과 화면까지의 시간에 맞춘다")]
    [SerializeField] private FovPulse _bossFinishFov = new FovPulse(-12f, 5f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f)));

    [Header("상황 유지 줌")]
    [Tooltip("콤보 선택 화면이 열려 있는 동안의 FOV 변화량(도). 음수면 줌 인")]
    [SerializeField] private float _comboSelectFov = -6f;
    [Tooltip("완벽 회피 슬로모션 동안의 FOV 변화량(도). 음수면 줌 인")]
    [SerializeField] private float _slowMotionFov = -6f;
    [Tooltip("판 실패(전멸 · 시간 초과) 뒤 유지할 FOV 변화량(도). 음수면 줌 인. 씬이 바뀔 때까지 유지한다")]
    [SerializeField] private float _runFailedFov = -6f;
    [Tooltip("유지 줌이 들어가고 돌아오는 속도(도 / 실제 시간 초)")]
    [SerializeField, Min(0.01f)] private float _holdFovSpeed = 30f;

    private FovPulse _currentFov;
    private float _fovTimer;
    private float _pulseOffset;
    private float _holdOffset;
    private bool _isComboSelectOpen;
    private bool _isRunFailed;

    // 지금 더할 FOV 변화량(도). 순간 줌과 유지 줌을 더한 값이며, 효과가 없으면 0 이다.
    public float FovOffset => _pulseOffset + _holdOffset;

    // 히트스톱으로 게임 시간이 멈춰도 흔들림이 진행되게 한다. (Cinemachine Brain 의 Ignore Time Scale 과 함께 켠다)
    private void Awake() => CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
    private void OnEnable() => BindFeedbackEvent();

    private void Update()
    {
        UpdateFov();
        UpdateHoldFov();
    }

    private void OnDisable()
    {
        UnbindFeedbackEvent();
        StopFov();
        _isComboSelectOpen = false;
        _isRunFailed = false;
        _holdOffset = 0f;
    }

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        Shake(GetShake(moment), direction);

        FovPulse fov = GetFov(moment);

        if(fov != null) StartFov(fov);

        if(moment == BattleFeedback.Moment.ComboSelectOpened) _isComboSelectOpen = true;
        else if(moment == BattleFeedback.Moment.ComboSelectClosed) _isComboSelectOpen = false;
        else if(moment == BattleFeedback.Moment.RunFailed) _isRunFailed = true;
    }

    // 지금 상황의 유지 줌 목표로 서서히 옮긴다. 상황이 겹치면 가장 많이 당기는(가장 작은) 값을 목표로 한다.
    private void UpdateHoldFov()
    {
        float comboTarget = _isComboSelectOpen ? _comboSelectFov : 0f;
        float slowMotionTarget = _slowMotion.IsActive ? _slowMotionFov : 0f;
        float runFailedTarget = _isRunFailed ? _runFailedFov : 0f;
        float target = Mathf.Min(comboTarget, Mathf.Min(slowMotionTarget, runFailedTarget));

        _holdOffset = Mathf.MoveTowards(_holdOffset, target, _holdFovSpeed * Time.unscaledDeltaTime);
    }

    private void Shake(CinemachineImpulseSource source, Vector3 direction)
    {
        if(source == null) return;

        Vector3 velocity = GetScreenDirection(direction) * source.DefaultVelocity.magnitude;

        // Source 오브젝트 위치가 아닌 카메라 위치에서 내야, 카메라와 멀어도 거리 감쇠로 약해지지 않는다.
        source.GenerateImpulseAtPositionWithVelocity(_mainCamera.transform.position, velocity);
    }

    private void StartFov(FovPulse fov)
    {
        _currentFov = fov;
        _fovTimer = 0f;
        _pulseOffset = 0f;
    }

    private void UpdateFov()
    {
        if(_currentFov == null) return;

        _fovTimer += Time.unscaledDeltaTime;

        if(_fovTimer >= _currentFov.Duration)
        {
            StopFov();
            return;
        }

        _pulseOffset = _currentFov.Evaluate(_fovTimer);
    }

    private void StopFov()
    {
        _currentFov = null;
        _pulseOffset = 0f;
    }

    // 충격 방향을 화면 평면(상하좌우)의 방향으로 바꾼다. 방향이 없으면 화면 평면의 무작위 방향이다.
    // 카메라는 보통 캐릭터 뒤에서 적 쪽을 보므로, 적 → 캐릭터 방향은 대부분 카메라 깊이 방향이다.
    // 깊이 방향 흔들림은 화면에서 거의 보이지 않아, 깊이 성분을 세로로 옮긴다. (정면에서 막으면 아래로 눌림, 옆에서 막으면 좌우)
    private Vector3 GetScreenDirection(Vector3 worldDirection)
    {
        Vector3 local = _mainCamera.transform.InverseTransformDirection(worldDirection);
        Vector2 screen = new Vector2(local.x, local.z);

        if(screen == Vector2.zero) return Random.insideUnitCircle.normalized;

        return screen.normalized;
    }

    private CinemachineImpulseSource GetShake(BattleFeedback.Moment moment)
    {
        switch(moment)
        {
            case BattleFeedback.Moment.ImpactHit: return _impactHitShake;
            case BattleFeedback.Moment.ParrySuccess: return _parrySuccessShake;
            case BattleFeedback.Moment.Knockback: return _knockbackShake;
            // 완벽 회피는 "피했다" 는 순간이라 흔들지 않는다. (FOV 효과로 표현)
            default: return null;
        }
    }

    private FovPulse GetFov(BattleFeedback.Moment moment)
    {
        switch(moment)
        {
            case BattleFeedback.Moment.ParryStart: return _parryStartFov;
            case BattleFeedback.Moment.ParrySuccess: return _parrySuccessFov;
            case BattleFeedback.Moment.WaveFinish: return _waveFinishFov;
            case BattleFeedback.Moment.BossFinish: return _bossFinishFov;
            default: return null;
        }
    }

    private void BindFeedbackEvent()
    {
        BattleFeedback.Occurred += OnMoment;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.Occurred -= OnMoment;
    }
}
