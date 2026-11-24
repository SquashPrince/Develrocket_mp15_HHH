using UnityEngine;
using UnityEngine.Rendering;

// 완벽 회피 슬로모션 동안 화면 효과(같은 오브젝트의 Volume, 예 : 색 보정 · 비네트)를 켠다.
// 슬로모션이 켜져 있는 동안 Volume 의 Weight 를 1 로, 끝나면 0 으로 서서히 바꾼다. 회피 반격으로 일찍 끝나도 같은 방식으로 사라진다.
// 슬로모션은 적만 느려지고 히트스톱 · 콤보 선택은 게임 시간을 바꾸므로, 효과의 전환은 실제 시간으로 잰다.
// 효과의 색 · 세기는 Volume Profile 에서 정한다.
[RequireComponent(typeof(Volume))]
public class SlowMotionScreenEffect : MonoBehaviour
{
    [Tooltip("슬로모션을 가진 파티")]
    [SerializeField] private PlayerParty _party;
    [Tooltip("효과가 켜지는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _fadeInTime = 0.1f;
    [Tooltip("효과가 사라지는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _fadeOutTime = 0.3f;

    private Volume _volume;

    private void Awake()
    {
        CacheComponent();
        Init();
    }

    private void Update() => UpdateWeight();

    private void UpdateWeight()
    {
        bool isActive = _party.SlowMotion.IsActive;
        float speed = 1f / (isActive ? _fadeInTime : _fadeOutTime);

        _volume.weight = Mathf.MoveTowards(_volume.weight, isActive ? 1f : 0f, speed * Time.unscaledDeltaTime);
    }

    private void CacheComponent()
    {
        _volume = GetComponent<Volume>();
    }

    // 씬에 저장된 Weight 와 상관없이 효과가 꺼진 채로 시작한다.
    private void Init()
    {
        _volume.weight = 0f;
    }
}
