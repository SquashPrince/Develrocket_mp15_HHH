using Unity.Cinemachine;
using UnityEngine;

// 록온 중에만 록온 카메라를 켜고, 플레이어 뒤에서 록온 대상 쪽을 보도록 궤도의 가로 각도를 맞춘다.
// 록온 카메라에는 마우스 입력(Input Axis Controller)을 붙이지 않으므로 록온 중에는 마우스로 카메라가 돌지 않는다.
// 플레이어와 대상을 한 화면에 담는 것은 Target Group 을 바라보는 Rotation Composer 가 맡는다.
// 카메라 사이의 전환은 Cinemachine Brain 이 섞어 준다.
public class LockOnCamera : MonoBehaviour
{
    [SerializeField] private PlayerTargeting _targeting;
    [SerializeField] private CinemachineCamera _lockOnCamera;
    [Tooltip("록온 카메라의 Orbital Follow. Binding Mode 는 World Space 여야 가로 각도가 월드 방향과 일치한다")]
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
    [Tooltip("플레이어가 미리 들어 있는 Target Group. 록온 대상은 코드가 넣고 뺀다")]
    [SerializeField] private CinemachineTargetGroup _targetGroup;
    [Tooltip("록온 대상 쪽으로 카메라 각도가 따라가는 시간(초)")]
    [SerializeField, Min(0f)] private float _turnTime = 0.2f;
    [Tooltip("Target Group 에서 록온 대상의 반경. 화면에 담을 대상의 크기")]
    [SerializeField, Min(0f)] private float _targetRadius = 1f;

    private Transform _groupedTarget;
    private float _yawVelocity;

    private void Start() => Init();
    private void Update() => FollowLockTarget();

    private void FollowLockTarget()
    {
        DummyEnemy lockTarget = _targeting.LockTarget;
        Transform target = lockTarget != null ? lockTarget.transform : null;

        if(target != _groupedTarget) ChangeLockTarget(target);

        if(target == null) return;

        float yaw = Mathf.SmoothDampAngle(_orbitalFollow.HorizontalAxis.Value, GetYawToTarget(target), ref _yawVelocity, _turnTime);

        // 각도를 -180 ~ 180 범위로 맞춰 축의 범위를 벗어나지 않게 한다.
        _orbitalFollow.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw);
    }

    // 록온 대상이 바뀌면 Target Group 의 대상을 바꾸고, 록온 여부에 따라 록온 카메라를 켜고 끈다.
    // 새로 켜질 때는 각도를 대상 쪽으로 바로 맞춰, 이전 카메라에서 섞여 넘어오는 동안 엉뚱한 쪽을 거치지 않게 한다.
    private void ChangeLockTarget(Transform target)
    {
        if(_groupedTarget != null) _targetGroup.RemoveMember(_groupedTarget);

        if(target != null)
        {
            _targetGroup.AddMember(target, 1f, _targetRadius);
            _orbitalFollow.HorizontalAxis.Value = GetYawToTarget(target);
            _yawVelocity = 0f;
        }

        _groupedTarget = target;
        _lockOnCamera.enabled = target != null;
    }

    // 플레이어에서 대상을 향하는 수평 방향의 각도(월드 기준, 도). 카메라는 이 방향의 반대편, 곧 플레이어 뒤에 선다.
    private float GetYawToTarget(Transform target)
    {
        Vector3 toTarget = target.position - _targeting.transform.position;

        return Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
    }

    private void Init()
    {
        _lockOnCamera.enabled = false;
    }
}
