using Unity.Cinemachine;
using UnityEngine;

// 전투 연출의 FOV 변화량을 이 Cinemachine 카메라의 최종 렌즈에 더한다. 카메라에 설정한 기본 FOV 는 바꾸지 않는다.
// 기본 FOV 를 직접 바꾸지 않으므로 효과가 끝나면 항상 원래 FOV 로 돌아오고, 카메라 사이 전환 중에도 어긋나지 않는다.
// 조작 캐릭터를 따라가는 Cinemachine 카메라(일반 · 록온)마다 붙이고, 같은 연출 기능을 연결한다.
public class CameraFovOffset : CinemachineExtension
{
    [Tooltip("FOV 변화량을 만드는 카메라 연출 기능")]
    [SerializeField] private CameraImpactFeedback _feedback;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        // Cinemachine 확장 기능은 편집 중에도 실행되므로, 연결 전 오류를 막고 플레이 중에만 더한다.
        if(stage != CinemachineCore.Stage.Finalize || !Application.isPlaying) return;

        state.Lens.FieldOfView += _feedback.FovOffset;
    }
}
