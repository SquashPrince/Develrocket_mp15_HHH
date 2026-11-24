using Unity.Cinemachine;
using UnityEngine;

// 록온 중에만 록온 카메라를 켜고, 플레이어 뒤에서 록온 대상 쪽을 보도록 궤도의 가로 각도를 맞춘다.
// 록온 카메라에는 마우스 입력(Input Axis Controller)을 붙이지 않으므로 록온 중에는 마우스로 카메라가 돌지 않는다.
// 플레이어와 대상을 한 화면에 담는 것은 Target Group 을 바라보는 Rotation Composer 가 맡는다.
// 카메라 사이의 전환은 Cinemachine Brain 이 섞어 준다.
public class LockOnCamera : MonoBehaviour
{
    [Tooltip("록온 상태를 들고 있는 파티. 록온 대상과 조작 중인 캐릭터를 읽는다")]
    [SerializeField] private PlayerParty _party;
    [SerializeField] private CinemachineCamera _lockOnCamera;
    [Tooltip("록온 카메라의 Orbital Follow. Binding Mode 는 World Space 여야 가로 각도가 월드 방향과 일치한다")]
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
    [Tooltip("플레이어가 미리 들어 있는 Target Group. 록온 대상은 코드가 넣고 뺀다")]
    [SerializeField] private CinemachineTargetGroup _targetGroup;
    [Tooltip("록온 대상 쪽으로 카메라 각도가 따라가는 시간(초)")]
    [SerializeField, Min(0f)] private float _turnTime = 0.2f;
    [Tooltip("Target Group 에서 록온 대상의 반경. 화면에 담을 대상의 크기")]
    [SerializeField, Min(0f)] private float _targetRadius = 1f;
    [Tooltip("벽에 가려졌던 록온 대상이 다시 보일 때 원래 구도로 돌아오는 시간(초). 가려질 때는 즉시 플레이어 기준으로 바뀐다")]
    [SerializeField, Min(0.01f)] private float _blockedBlendTime = 0.5f;

    private Transform _groupedTarget;
    private float _yawVelocity;

    private void Start() => Init();
    private void Update() => FollowLockTarget();

    private void FollowLockTarget()
    {
        IEnemyTargetable lockTarget = _party.LockOn.LockTarget;
        Transform target = lockTarget != null ? lockTarget.transform : null;

        if(target != _groupedTarget) ChangeLockTarget(target);

        if(target == null) return;

        float yaw = Mathf.SmoothDampAngle(_orbitalFollow.HorizontalAxis.Value, GetYawToTarget(target), ref _yawVelocity, _turnTime);

        // 각도를 -180 ~ 180 범위로 맞춰 축의 범위를 벗어나지 않게 한다.
        _orbitalFollow.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw);

        UpdateTargetWeight();
    }

    // 록온 대상이 벽에 가려지면 Target Group 에서 대상의 비중을 즉시 0 으로 내려 카메라가 플레이어를 바라보게 하고,
    // 다시 보이면 서서히 1 로 되돌린다. 카메라 각도는 계속 대상 쪽을 향한다.
    // 바라보는 지점(플레이어와 대상의 중간)이 벽 너머로 가면 Deoccluder 가 카메라를 벽 앞까지 당겨 플레이어가 화면에서 사라지기 때문이다.
    // 가려질 때 서서히 내리면 그동안 바라보는 지점이 벽 너머에 남아 카메라가 벽 앞으로 갔다가 돌아오므로 즉시 내린다.
    private void UpdateTargetWeight()
    {
        CinemachineTargetGroup.Target member = _targetGroup.Targets[_targetGroup.FindMember(_groupedTarget)];

        if(_party.LockOn.IsLockTargetBlocked) member.Weight = 0f;
        else member.Weight = Mathf.MoveTowards(member.Weight, 1f, Time.deltaTime / _blockedBlendTime);
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
        Vector3 toTarget = target.position - _party.Controlled.transform.position;

        return Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
    }

    private void Init()
    {
        _lockOnCamera.enabled = false;
    }
}
