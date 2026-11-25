using UnityEngine;

// 패링 성공 때 게임 전체를 아주 잠깐 멈춘다(Time.timeScale 0). 정지 시간은 실제 시간으로 잰다.
// 카메라 효과가 정지 중에도 보이려면 Cinemachine Brain 의 Ignore Time Scale 을 켜야 한다.
public class BattleHitStop : MonoBehaviour
{
    [Tooltip("패링 성공 때 멈추는 시간(실제 시간, 초). 0 이면 멈추지 않는다")]
    [SerializeField, Min(0f)] private float _parrySuccessStop = 0.08f;

    // 멈추기 전의 시간 배율. 끝날 때 이 값으로 되돌린다.
    private float _previousTimeScale = 1f;
    private float _timer;

    private bool IsStopped => _timer > 0f;

    private void OnEnable() => BindFeedbackEvent();
    private void Update() => UpdateTimer();

    private void OnDisable()
    {
        UnbindFeedbackEvent();

        if(IsStopped) Finish();
    }

    private void OnMoment(BattleFeedback.Moment moment, Vector3 direction)
    {
        if(moment == BattleFeedback.Moment.ParrySuccess) Begin(_parrySuccessStop);
    }

    // 멈춘 중에 다시 걸면 남은 시간과 새 시간 중 긴 쪽으로 이어 간다. 되돌릴 배율은 처음 멈출 때의 값을 유지한다.
    private void Begin(float duration)
    {
        if(duration <= 0f) return;

        if(!IsStopped) _previousTimeScale = Time.timeScale;

        _timer = Mathf.Max(_timer, duration);
        Time.timeScale = 0f;
    }

    private void UpdateTimer()
    {
        if(!IsStopped) return;

        _timer -= Time.unscaledDeltaTime;

        if(_timer <= 0f) Finish();
    }

    private void Finish()
    {
        _timer = 0f;

        // 정지 중에 다른 기능이 시간 배율을 바꿨으면 그쪽 값을 그대로 둔다.
        if(Time.timeScale == 0f) Time.timeScale = _previousTimeScale;
    }

    private void BindFeedbackEvent()
    {
        BattleFeedback.Occurred += OnMoment;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.Occurred -= OnMoment;
    }
}
