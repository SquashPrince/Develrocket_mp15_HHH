using UnityEngine;

// 필살기 게이지와 필살기 수치. 에너지와 별개로 쌓이는 게이지이며, 공격 적중 · 패링 성공 · 완벽 회피로 쌓이고 줄어들지 않는다.
// 공격 적중 획득량은 공격마다 공격 설정에서 정하고, 패링 성공 · 완벽 회피 획득량은 여기서 정한다.
// 현재 게이지는 Inspector 를 Debug 모드로 바꾸면 실시간으로 볼 수 있다.
public class PlayerUltimate : MonoBehaviour
{
    [Header("게이지")]
    [SerializeField, Min(1f)] private float _maxGauge = 3000f;
    [Tooltip("패링 성공 한 번에 얻는 게이지")]
    [SerializeField, Min(0f)] private float _parryGaugeGain = 300f;
    [Tooltip("완벽 회피 한 번에 얻는 게이지")]
    [SerializeField, Min(0f)] private float _perfectDodgeGaugeGain = 300f;

    private float _currentGauge;

    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞춘다.
    public float CurrentGauge
    {
        get => _currentGauge;
        private set => _currentGauge = Mathf.Clamp(value, 0f, _maxGauge);
    }

    public bool IsGaugeFull => _currentGauge >= _maxGauge;

    public void AddGauge(float amount)
    {
        if(amount <= 0f) return;

        CurrentGauge += amount;

#if UNITY_EDITOR
        Debug.Log($"[필살기 게이지] +{amount} → {_currentGauge:F0} / {_maxGauge:F0}");
#endif
    }

    public void AddParryGauge() => AddGauge(_parryGaugeGain);

    public void AddPerfectDodgeGauge() => AddGauge(_perfectDodgeGaugeGain);
}
