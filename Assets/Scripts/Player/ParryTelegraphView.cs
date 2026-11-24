using System.Collections.Generic;
using UnityEngine;

// 적의 패링 공격 예고 표시. 적은 패링 가능 시간을 열고 예고 위치만 알려 주고, 어떤 표시를 띄울지는 플레이어 쪽이 정한다.
// 교체할 대기 캐릭터가 있으면 패링 가능(노란 표시), 없으면 패링 불가(빨간 표시)를 띄운다.
//   패링은 교체로 나온 캐릭터가 하므로(지원 공격), 대기 캐릭터가 없으면 패링할 수 없기 때문이다.
//   적이 연속 패링 패턴 중이면 필드의 파티원이 다시 패링할 수 있으므로 대기 캐릭터가 없어도 노란 표시를 띄운다.
//   패링 가능 여부는 파티가 예고가 뜬 순간 정해 두고, 실제 패링 판정도 같은 값을 쓴다. 예고 색과 판정이 어긋나지 않게 하기 위해서다.
// 색은 예고가 뜨는 순간 정하고, 예고 중에 대기 캐릭터 수가 바뀌어도 그대로 둔다.
// 표시는 적의 예고 위치 자식으로 만들어 적을 따라다니고, 패링 가능 시간이 닫히면 지운다.
// 표시 Prefab 은 파티의 공통 설정에서 읽는다.
[RequireComponent(typeof(PlayerParty))]
public class ParryTelegraphView : MonoBehaviour
{
    // 지금 띄워 둔 예고 표시. 예고를 연 적마다 하나다.
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
