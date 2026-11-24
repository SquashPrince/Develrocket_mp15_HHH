using System.Collections.Generic;
using UnityEngine;

// 적의 패링 공격 예고 표시. 적은 예고 위치만 알려 주고, 색은 플레이어 쪽이 정한다.
// 패링은 교체로 나온 캐릭터가 하므로 지금 패링할 수 있으면 노란 표시, 없으면 빨간 표시를 띄운다.
// 색과 실제 판정이 어긋나지 않게 파티가 예고가 뜬 순간 정한 값을 함께 쓴다.
[RequireComponent(typeof(PlayerParty))]
public class ParryTelegraphView : MonoBehaviour
{
    // 예고를 연 적마다 하나다.
    private readonly Dictionary<IEnemyParryable, GameObject> _telegraphs = new Dictionary<IEnemyParryable, GameObject>();
    private PlayerParty _party;

    private void Awake() => CacheComponent();
    private void OnEnable() => BindParryWindowEvent();
    private void OnDisable()
    {
        UnbindParryWindowEvent();
        ClearTelegraphs();
    }

    private void Show(IEnemyParryable attacker)
    {
        GameObject prefab = _party.IsParryable(attacker) ? _party.Common.ParryTelegraphPrefab : _party.Common.UnparryableTelegraphPrefab;

        if(prefab == null) return;

        _telegraphs[attacker] = Instantiate(prefab, attacker.TelegraphPoint);
    }

    private void Hide(IEnemyParryable attacker)
    {
        if(!_telegraphs.TryGetValue(attacker, out GameObject telegraph)) return;

        // 적이 처치되어 함께 사라진 표시는 이미 파괴되어 있을 수 있다.
        if(telegraph != null) Destroy(telegraph);

        _telegraphs.Remove(attacker);
    }

    private void ClearTelegraphs()
    {
        foreach(GameObject telegraph in _telegraphs.Values)
        {
            if(telegraph != null) Destroy(telegraph);
        }

        _telegraphs.Clear();
    }

    private void BindParryWindowEvent()
    {
        ParryWindow.Opened += Show;
        ParryWindow.Closed += Hide;
    }

    private void UnbindParryWindowEvent()
    {
        ParryWindow.Opened -= Show;
        ParryWindow.Closed -= Hide;
    }

    private void CacheComponent()
    {
        _party = GetComponent<PlayerParty>();
    }
}
