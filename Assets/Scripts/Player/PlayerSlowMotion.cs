using UnityEngine;

// 완벽 회피 후 슬로모션. 적만 정해진 배속으로 느려지고, 플레이어는 원래 속도로 움직인다.
// 지속 시간은 실제 시간으로 잰다. 플레이어 시간은 느려지지 않으므로 Time.deltaTime 이 곧 실제 시간이다.
// 슬로모션 중 공격 입력은 회피 반격이 되고, 회피 반격을 시작하면 슬로모션은 바로 끝난다.
// 지속 시간 · 적 배속 · 임시 표시는 모든 캐릭터가 같이 쓰는 공통 설정에서 읽는다.
[RequireComponent(typeof(PlayerController))]
public class PlayerSlowMotion : MonoBehaviour
{
    private PlayerController _controller;
    private GameObject _marker;
    private float _timer;

    public bool IsActive => _timer > 0f;
    private CommonCharacterSettings Common => _controller.Common;

    private void Awake() => CacheComponent();
    private void Update() => UpdateTimer();
    private void OnDisable() => End();

    // 슬로모션 중 다시 시작하면 시간을 처음부터 다시 센다.
    public void Begin()
    {
        _timer = Common.SlowMotionDuration;
        EnemyTime.Scale = Common.SlowMotionEnemyTimeScale;

        if(_marker == null && Common.SlowMotionMarkerPrefab != null) _marker = Instantiate(Common.SlowMotionMarkerPrefab, transform.position, Quaternion.identity);

#if UNITY_EDITOR
        Debug.Log($"[슬로모션 시작] 적 배속 {Common.SlowMotionEnemyTimeScale} / {Common.SlowMotionDuration}초");
#endif
    }

    // 슬로모션 중일 때만 끝낸다. 회피 반격 시작 · 컴포넌트 비활성화 때 부른다.
    public void End()
    {
        if(IsActive) Finish();
    }

    private void UpdateTimer()
    {
        if(!IsActive) return;

        _timer -= Time.deltaTime;

        if(_timer <= 0f) Finish();
    }

    private void Finish()
    {
        _timer = 0f;
        EnemyTime.Scale = 1f;

        if(_marker != null) Destroy(_marker);

#if UNITY_EDITOR
        Debug.Log("[슬로모션 종료]");
#endif
    }

#if UNITY_EDITOR
    // [테스트] 플레이 중 컴포넌트 우클릭 메뉴에서 슬로모션을 바로 건다.
    [ContextMenu("Test Begin Slow Motion")]
    private void TestBegin() => Begin();
#endif

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
    }
}
