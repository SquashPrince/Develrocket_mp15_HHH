using UnityEngine;

// 완벽 회피 후 슬로모션. 적만 정해진 배속으로 느려지고, 플레이어 캐릭터는 원래 속도로 움직인다.
// 캐릭터 하나가 아닌 전투 전체에 거는 효과라 파티에 하나만 둔다. 어느 캐릭터가 완벽 회피했든 같은 슬로모션을 쓴다.
// 지속 시간은 실제 시간으로 잰다. 적에게 거는 전투 효과라 캐릭터 시간(히트렉 등)과 상관없이 흐른다.
// 슬로모션 중 공격 입력은 회피 반격이 되고, 회피 반격을 시작하면 슬로모션은 바로 끝난다.
// 지속 시간 · 적 배속 · 임시 표시는 파티의 공통 설정에서 읽는다.
public class BattleSlowMotion : MonoBehaviour
{
    private PlayerParty _party;
    private GameObject _marker;
    private float _timer;

    public bool IsActive => _timer > 0f;
    private CommonCharacterSettings Common => _party.Common;

    private void Awake() => CacheComponent();
    private void Update() => UpdateTimer();
    private void OnDisable() => End();

    // 슬로모션 중 다시 시작하면 시간을 처음부터 다시 센다.
    // markerPosition : 임시 표시를 만들 위치. 완벽 회피한 캐릭터의 위치를 넘긴다.
    public void Begin(Vector3 markerPosition)
    {
        _timer = Common.SlowMotionDuration;
        EnemyTime.Scale = Common.SlowMotionEnemyTimeScale;

        if(_marker == null && Common.SlowMotionMarkerPrefab != null) _marker = Instantiate(Common.SlowMotionMarkerPrefab, markerPosition, Quaternion.identity);

#if UNITY_EDITOR
        Debug.Log($"[슬로모션 시작] 적 배속 {Common.SlowMotionEnemyTimeScale} / {Common.SlowMotionDuration}초");
#endif
    }

    // 슬로모션 중일 때만 끝낸다. 공격 시작 · 컴포넌트 비활성화 때 부른다.
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

    private void CacheComponent()
    {
        _party = GetComponent<PlayerParty>();
    }
}
