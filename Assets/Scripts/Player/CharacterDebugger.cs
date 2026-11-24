using UnityEngine;

// [개발용] 판정 기능이 범위를 찾을 때마다 그 범위를 잠깐 동안 Scene 창에 빨간 선으로 그린다.
// 판정 기능은 알림만 보내므로, 이 컴포넌트를 빼도 다른 기능은 그대로 동작한다.
[RequireComponent(typeof(PlayerAttackJudge))]
public class CharacterDebugger : MonoBehaviour
{
    [Tooltip("범위를 찾은 뒤 판정 범위를 그려 두는 시간(초, 실제 시간). 한 순간 판정처럼 한 프레임만 찾는 범위도 보이게 하기 위해서다")]
    [SerializeField, Min(0f)] private float _rangeDisplayTime = 0.2f;

    private PlayerAttackJudge _judge;
    private ActionStep _lastRangeStep;
    private float _lastRangeTime = float.NegativeInfinity;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindJudgeEvent();
    private void OnDisable() => UnbindJudgeEvent();
    private void OnDrawGizmos() => DrawHitRange();

    private void OnRangeChecked(ActionStep step)
    {
        _lastRangeStep = step;
        _lastRangeTime = Time.time;
    }

    // 실행 중 표시 시간 안에 찾은 범위가 있으면 그 동작의 모양대로 그린다.
    private void DrawHitRange()
    {
        if(!Application.isPlaying || _lastRangeStep == null || Time.time - _lastRangeTime > _rangeDisplayTime) return;

        Gizmos.color = Color.red;
        _lastRangeStep.Range.DrawGizmos(transform);
    }

    private void CacheComponent()
    {
        _judge = GetComponent<PlayerAttackJudge>();
    }

    private void BindJudgeEvent() => _judge.RangeChecked += OnRangeChecked;

    private void UnbindJudgeEvent() => _judge.RangeChecked -= OnRangeChecked;
}
