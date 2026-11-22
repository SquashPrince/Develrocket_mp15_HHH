using System.Collections.Generic;
using UnityEngine;

// 대미지 숫자 표시. 플레이어 공격이 적에게 대미지를 줄 때마다 맞은 적 위에 숫자를 띄운다.
// 숫자는 미리 만들어 두고 다시 쓴다(풀링). 꺼져 있는 숫자가 없으면 하나 더 만든다. 미리 만들기는 씬 준비(로딩 중)가 맡는다.
// 합치기 : 같은 적이 정한 시간 안에 다시 맞으면 새 숫자를 띄우지 않고 떠 있는 숫자에 더한다. 다단 히트로 숫자가 쏟아지지 않고 읽기 쉽게 하기 위해서다.
// 상한 : 동시에 떠 있는 숫자가 상한에 닿으면 가장 오래된 숫자를 거둬 다시 쓴다.
// 갱신 : 떠 있는 숫자만 모아 여기서 한 번에 갱신한다. 숫자마다 Update 를 돌리지 않기 위해서다.
// 위치 : 적중 이펙트와 같은 맞은 점(공격한 캐릭터 쪽 적 표면)이다.
//   같은 순간 여러 숫자가 겹치지 않게 화면에서 조금씩 무작위로 비켜 놓는다(월드 수평 방향).
// 숫자 전용 화면 공간(Overlay) Canvas 아래에 둔다. 숫자들은 이 오브젝트의 자식으로 만든다.
//   전투 HUD 와 같은 Canvas 에 두면 숫자가 움직일 때마다 HUD 전체를 다시 묶어(Rebatch) 그리기 때문이다.
// 게임 시간으로 갱신해 일시정지 중에는 멈춘다.
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

        Vector2 offset = Random.insideUnitCircle * _randomOffset;
        Vector3 position = hitPoint + new Vector3(offset.x, 0f, offset.y);
        DamageText text = GetText();

        text.Show(_camera, target, position, amount, isImpact, isCritical);
        _active.Add(text);
    }

    // 정한 수가 될 때까지 숫자를 미리 만든다. 씬 준비(로딩 중)에서 부른다.
    public void Prewarm()
    {
        while(_pool.Count < _initialCount)
        {
            CreateText();
        }
    }

    // 같은 적에게 합치기 시간 안에 띄운 숫자. 가장 최근 것부터 본다.
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

    private void UpdateActive()
    {
        float deltaTime = Time.deltaTime;

        for(int i = _active.Count - 1; i >= 0; i--)
        {
            if(_active[i].Tick(deltaTime)) _active.RemoveAt(i);
        }
    }

    // 상한에 닿았으면 가장 오래된 숫자를 거둬 쓰고, 아니면 꺼져 있는 숫자를 꺼낸다. 없으면 하나 더 만든다.
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
