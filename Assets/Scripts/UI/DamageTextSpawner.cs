using System.Collections.Generic;
using UnityEngine;

// 대미지 숫자 표시. 풀링한 숫자를 맞은 점에 띄우고, 떠 있는 숫자만 모아 한 번에 갱신한다.
// 다단 히트로 숫자가 쏟아지지 않게 같은 적이 짧은 사이에 다시 맞으면 떠 있는 숫자에 더한다.
// 숫자가 움직일 때마다 전투 HUD 전체가 다시 묶이지(Rebatch) 않게 숫자 전용 Canvas 아래에 둔다.
// 자세한 흐름 : Docs/Technical/UI/BattleHud.md
public class DamageTextSpawner : MonoBehaviour
{
    [SerializeField] private DamageText _prefab;
    [Tooltip("월드 위치를 화면 좌표로 바꿀 카메라 (Main Camera)")]
    [SerializeField] private Camera _camera;
    [Tooltip("로딩 중에 미리 만들어 둘 숫자 수. 동시 표시 상한과 같게 두면 전투 중에 새로 만들지 않는다")]
    [SerializeField, Min(0)] private int _initialCount = 40;
    [Tooltip("동시에 떠 있을 수 있는 숫자 수. 넘으면 가장 오래된 숫자를 다시 쓴다")]
    [SerializeField, Min(1)] private int _maxActive = 40;
    [Tooltip("같은 적이 이 시간(초) 안에 다시 맞으면 떠 있는 숫자에 더한다. 0 이면 합치지 않는다")]
    [SerializeField, Min(0f)] private float _mergeTime = 0.15f;
    [Tooltip("숫자를 무작위로 비켜 놓는 최대 거리(m, 월드 수평)")]
    [SerializeField, Min(0f)] private float _randomOffset = 0.3f;

    private readonly List<DamageText> _pool = new List<DamageText>();
    // 떠 있는 숫자. 띄운 순서대로라 맨 앞이 가장 오래된 숫자다.
    private readonly List<DamageText> _active = new List<DamageText>();

    private void OnEnable() => BindFeedbackEvent();
    private void OnDisable() => UnbindFeedbackEvent();
    private void Update() => UpdateActive();

    private void OnDamageDealt(Transform target, Vector3 hitPoint, float amount, bool isImpact, bool isCritical)
    {
        DamageText merged = FindMergeable(target);

        if(merged != null)
        {
            merged.Add(amount, isImpact, isCritical);
            // 더한 숫자는 다시 처음부터 떠오르므로 가장 최근 순서로 옮긴다.
            _active.Remove(merged);
            _active.Add(merged);
            return;
        }

        // 같은 순간 여러 숫자가 겹치지 않게 월드 수평으로 조금씩 비켜 놓는다.
        Vector2 offset = Random.insideUnitCircle * _randomOffset;
        Vector3 position = hitPoint + new Vector3(offset.x, 0f, offset.y);
        DamageText text = GetText();

        text.Show(_camera, target, position, amount, isImpact, isCritical);
        _active.Add(text);
    }

    // 씬 준비(로딩 중)에서 부른다.
    public void Prewarm()
    {
        while(_pool.Count < _initialCount)
        {
            CreateText();
        }
    }

    private DamageText FindMergeable(Transform target)
    {
        if(_mergeTime <= 0f) return null;

        for(int i = _active.Count - 1; i >= 0; i--)
        {
            DamageText text = _active[i];

            if(text.Target == target && text.Age <= _mergeTime) return text;
        }

        return null;
    }

    // 게임 시간으로 갱신해 일시정지 중에는 멈춘다.
    private void UpdateActive()
    {
        float deltaTime = Time.deltaTime;

        for(int i = _active.Count - 1; i >= 0; i--)
        {
            if(_active[i].Tick(deltaTime)) _active.RemoveAt(i);
        }
    }

    // 상한에 닿았으면 가장 오래된 숫자를 거둬 쓴다.
    private DamageText GetText()
    {
        if(_active.Count >= _maxActive)
        {
            DamageText oldest = _active[0];

            _active.RemoveAt(0);
            oldest.Hide();
            return oldest;
        }

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

    private void BindFeedbackEvent()
    {
        BattleFeedback.DamageDealt += OnDamageDealt;
    }

    private void UnbindFeedbackEvent()
    {
        BattleFeedback.DamageDealt -= OnDamageDealt;
    }
}
