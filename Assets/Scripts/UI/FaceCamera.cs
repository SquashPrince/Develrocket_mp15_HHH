using UnityEngine;

// 월드 공간 UI(적 머리 위 바)가 항상 카메라를 향하게 한다. 카메라와 같은 방향을 보게 해 글자 · 막대가 뒤집히지 않는다.
// 카메라 이동이 끝난 뒤 맞추도록 LateUpdate 에서 돈다.
public class FaceCamera : MonoBehaviour
{
    private Transform _camera;

    private void Awake() => CacheComponent();
    private void LateUpdate() => transform.rotation = _camera.rotation;

    private void CacheComponent()
    {
        _camera = Camera.main.transform;
    }
}
