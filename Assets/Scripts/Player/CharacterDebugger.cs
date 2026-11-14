using UnityEngine;

// [개발용] 캐릭터 디버그 기능. 판정 범위를 Scene 창에 그린다.
// 판정 기능이 범위를 찾을 때마다 그 동작의 범위를 기억해, 잠깐 동안 빨간 선으로 그린다.
// 구간 판정은 켜져 있는 동안 계속 보이고, 한 순간 판정 · 강타도 짧게 남아 보인다. 캐릭터를 선택하지 않아도 그린다.
// Game 창에서는 Gizmos 버튼을 켰을 때만 보인다.
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

        switch(_lastRangeStep.Shape)
        {
            case ActionStep.HitShape.Sphere:
                Gizmos.DrawWireSphere(transform.TransformPoint(_lastRangeStep.HitCenter), _lastRangeStep.HitRadius);
                break;
            case ActionStep.HitShape.Capsule:
                DrawCapsule(_lastRangeStep);
                break;
            default:
                Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(_lastRangeStep.HitCenter), transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, _lastRangeStep.HitSize);
                Gizmos.matrix = Matrix4x4.identity;
                break;
        }
    }

    // 캡슐은 양 끝 구와 그 사이를 잇는 선으로 그린다.
    private void DrawCapsule(ActionStep step)
    {
        _judge.GetCapsulePoints(step, out Vector3 start, out Vector3 end);

        Gizmos.DrawWireSphere(start, step.HitRadius);
        Gizmos.DrawWireSphere(end, step.HitRadius);
        Gizmos.DrawLine(start + transform.right * step.HitRadius, end + transform.right * step.HitRadius);
        Gizmos.DrawLine(start - transform.right * step.HitRadius, end - transform.right * step.HitRadius);
        Gizmos.DrawLine(start + transform.up * step.HitRadius, end + transform.up * step.HitRadius);
        Gizmos.DrawLine(start - transform.up * step.HitRadius, end - transform.up * step.HitRadius);
    }

    private void CacheComponent()
    {
        _judge = GetComponent<PlayerAttackJudge>();
    }

    private void BindJudgeEvent() => _judge.RangeChecked += OnRangeChecked;

    private void UnbindJudgeEvent() => _judge.RangeChecked -= OnRangeChecked;
}
