using UnityEngine;

// 월드 공간 UI(적 머리 위 바)가 항상 카메라를 향하게 한다. 카메라와 같은 방향을 보게 해 글자 · 막대가 뒤집히지 않는다.
// 카메라가 정한 거리보다 가까워지면 가까워진 만큼 줄여, 화면에서 그 거리일 때보다 커지지 않게 한다.
// 카메라 이동이 끝난 뒤 맞추도록 LateUpdate 에서 돈다.
public class FaceCamera : MonoBehaviour
{
    [Tooltip("카메라가 이 거리(m)보다 가까워지면 화면 크기를 이 거리일 때로 고정한다. 0 이면 고정하지 않는다")]
    [SerializeField, Min(0f)] private float _fixedSizeDistance = 8.0f;

    private Transform _camera;
    // 처음 크기. 줄일 때 기준으로 쓴다.
    private Vector3 _baseScale;

    private void Awake()
    {
        CacheComponent();
        Init();
    }

    private void LateUpdate()
    {
        transform.rotation = _camera.rotation;
        LimitScreenSize();
    }

    // 화면 크기는 거리에 반비례하므로, 고정 거리 안에서는 거리에 비례해 줄이면 화면 크기가 같게 유지된다.
    private void LimitScreenSize()
    {
        if(_fixedSizeDistance <= 0f) return;

        float distance = Vector3.Distance(_camera.position, transform.position);

        transform.localScale = _baseScale * Mathf.Min(1f, distance / _fixedSizeDistance);
    }

    private void CacheComponent()
    {
        _camera = Camera.main.transform;
    }

    private void Init()
    {
        _baseScale = transform.localScale;
    }
}
