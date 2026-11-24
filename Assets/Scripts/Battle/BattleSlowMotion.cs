using UnityEngine;

// 완벽 회피 후 슬로모션. 적만 느려지고 플레이어는 원래 속도로 움직인다.
// 전투 전체에 거는 효과라 파티에 하나만 두고, 캐릭터 시간(히트렉 등)과 상관없이 실제 시간으로 잰다.
public class BattleSlowMotion : MonoBehaviour
{
    private PlayerParty _party;
    private float _timer;

    public bool IsActive => _timer > 0f;
    private CommonCharacterSettings Common => _party.Common;

    private void Awake() => CacheComponent();
    private void Update() => UpdateTimer();
    private void OnDisable() => End();

    // 슬로모션 중 다시 시작하면 시간을 처음부터 다시 센다.
    public void Begin()
    {
        _timer = Common.SlowMotionDuration;
        EnemyTime.Scale = Common.SlowMotionEnemyTimeScale;

#if UNITY_EDITOR
        Debug.Log($"[슬로모션 시작] 적 배속 {Common.SlowMotionEnemyTimeScale} / {Common.SlowMotionDuration}초");
#endif

        BattleFeedback.Raise(BattleFeedback.Moment.PerfectDodge);
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

#if UNITY_EDITOR
        Debug.Log("[슬로모션 종료]");
#endif
    }

    private void CacheComponent()
    {
        _party = GetComponent<PlayerParty>();
    }
}
