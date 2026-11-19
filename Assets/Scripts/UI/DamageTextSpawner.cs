using System.Collections.Generic;
using UnityEngine;

// 대미지 숫자 표시. 플레이어 공격이 적에게 대미지를 줄 때마다 맞은 적 위에 숫자를 띄운다.
// 숫자는 미리 만들어 두고 다시 쓴다(풀링). 꺼져 있는 숫자가 없으면 하나 더 만든다. 다단 히트처럼 숫자가 많이 나올 때 매번 만들고 지우지 않기 위해서다.
// 위치 : 적의 록온 표시 위치(머리 · 가슴)를 쓰고, 없으면 적 기준점에서 정한 높이만큼 위다.
//   같은 순간 여러 숫자가 겹치지 않게 화면에서 조금씩 무작위로 비켜 놓는다(월드 수평 방향).
// 전투 HUD 의 화면 공간(Overlay) Canvas 아래에 둔다. 숫자들은 이 오브젝트의 자식으로 만든다.
public class DamageTextSpawner : MonoBehaviour
{
    [SerializeField] private DamageText _prefab;
    [Tooltip("월드 위치를 화면 좌표로 바꿀 카메라 (Main Camera)")]
    [SerializeField] private Camera _camera;
    [Tooltip("시작할 때 미리 만들어 둘 숫자 수")]
    [SerializeField, Min(0)] private int _initialCount = 10;
    [Tooltip("록온 표시 위치가 없는 적의 숫자 높이(m, 적 기준점 위)")]
    [SerializeField, Min(0f)] private float _defaultHeight = 2f;
    [Tooltip("숫자를 무작위로 비켜 놓는 최대 거리(m, 월드 수평)")]
    [SerializeField, Min(0f)] private float _randomOffset = 0.3f;

    private readonly List<DamageText> _pool = new List<DamageText>();

    private void Start() => Init();
    private void OnEnable() => BindFeedbackEvent();
    private void OnDisable() => UnbindFeedbackEvent();

    private void OnDamageDealt(Transform target, float amount, bool isImpact, bool isCritical)
    {
        Vector2 offset = Random.insideUnitCircle * _randomOffset;
        Vector3 position = GetAnchorPosition(target) + new Vector3(offset.x, 0f, offset.y);

        GetText().Show(_camera, position, amount, isImpact, isCritical);
    }

    private Vector3 GetAnchorPosition(Transform target)
    {
        if(target.TryGetComponent(out IEnemyTargetable targetable) && targetable.LockOnPoint != null) return targetable.LockOnPoint.position;

        return target.position + Vector3.up * _defaultHeight;
    }

    // 꺼져 있는 숫자를 꺼내고, 없으면 하나 더 만든다.
    private DamageText GetText()
    {
        foreach(DamageText text in _pool)
        {
            if(!text.gameObject.activeSelf) return text;
        }

        return CreateText();
    }

    private DamageText CreateText()
    {
        DamageText text = Instantiate(_prefab, transform);

        text.gameObject.SetActive(false);
        _pool.Add(text);
        return text;
    }

    private void Init()
    {
        for(int i = 0; i < _initialCount; i++)
        {
            CreateText();
        }
    }

    private void BindFeedbackEvent()
    {
        BattleFeedback.DamageDealt += OnDamageDealt;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.DamageDealt -= OnDamageDealt;
    }
}
