using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// 이동 입력이 있는 동안 카메라의 가로 자동 복귀(Recentering)를 막는다.
// 카메라 기준 이동 중에 카메라가 캐릭터 뒤로 돌아가면 이동 기준이 계속 바뀌어 원을 그리기 때문이다.
// 결과적으로 자동 복귀는 회전 · 이동 입력이 모두 없는 상태가 대기 시간만큼 지났을 때만 일어난다.
public class CameraRecenterBlocker : MonoBehaviour
{
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;

    private void Update() => BlockRecenterWhileMoving();

    private void BlockRecenterWhileMoving()
    {
        if(_moveAction.action.ReadValue<Vector2>() == Vector2.zero) return;

        // 진행 중인 복귀를 멈추고 대기 시간을 처음부터 다시 센다.
        _orbitalFollow.HorizontalAxis.CancelRecentering();
    }
}
