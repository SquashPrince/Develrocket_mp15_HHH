using Unity.Cinemachine;
using UnityEngine;

// 전투 연출 알림을 받아 카메라를 흔든다. 흔드는 순간(강타 · 패링 · 넉백)마다 Impulse Source 를 따로 연결하고, 비워 두면 흔들지 않는다.
// 흔들림 모양 · 길이 · 세기(Default Velocity 의 크기)는 각 Impulse Source 에서 정한다. 방향만 여기서 정한다.
//   충격 방향이 있는 순간(패링) : 그 방향으로 밀리는 반동 / 없는 순간 : 화면 기준 상하좌우 무작위
// 각 Cinemachine 카메라의 Impulse Listener 가 받으며, Listener 의 Use Camera Space 가 켜져 있어야 방향이 화면 기준이 된다.
public class CameraImpactFeedback : MonoBehaviour
{
    [Tooltip("화면을 비추는 메인 카메라. 충격 방향을 화면 기준으로 바꾸고, 흔들림을 카메라 위치에서 낸다")]
    [SerializeField] private Camera _mainCamera;

    [Header("순간별 흔들림 (비우면 흔들지 않음)")]
    [SerializeField] private CinemachineImpulseSource _impactHitShake;
    [SerializeField] private CinemachineImpulseSource _parrySuccessShake;
    [SerializeField] private CinemachineImpulseSource _knockbackShake;

    private void OnEnable() => BindFeedbackEvent();
    private void OnDisable() => UnbindFeedbackEvent();

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        CinemachineImpulseSource source = GetShake(moment);

        if(source == null) return;

        Vector3 velocity = GetScreenDirection(direction) * source.DefaultVelocity.magnitude;

        // Source 오브젝트 위치가 아닌 카메라 위치에서 내야, 카메라와 멀어도 거리 감쇠로 약해지지 않는다.
        source.GenerateImpulseAtPositionWithVelocity(_mainCamera.transform.position, velocity);
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

    private void BindFeedbackEvent()
    {
        BattleFeedback.Occurred += OnMoment;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.Occurred -= OnMoment;
    }
}
