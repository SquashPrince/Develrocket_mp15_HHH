using UnityEngine;

// 플레이어의 에너지. 시간이 지나거나 공격이 적중하면 쌓이고, 특수스킬을 쓰면 줄어든다.
// 최대치와 초당 회복량은 캐릭터 데이터에서 읽는다.
// 현재 에너지는 Inspector 를 Debug 모드로 바꾸면 실시간으로 볼 수 있다.
[RequireComponent(typeof(PlayerController))]
public class PlayerEnergy : MonoBehaviour
{
    private PlayerController _controller;
    private float _currentEnergy;

    // 값을 바꿀 때 항상 0 ~ 최대치 사이로 맞춘다.
    public float CurrentEnergy
    {
        get => _currentEnergy;
        private set => _currentEnergy = Mathf.Clamp(value, 0f, _controller.Character.MaxEnergy);
    }

    private void Awake() => CacheComponent();
    private void Update() => CurrentEnergy += _controller.Character.EnergyRegenPerSecond * Time.deltaTime;

    public void Add(float amount) => CurrentEnergy += amount;

    // 에너지가 충분하면 소모하고 true 를 돌려준다. 모자라면 소모하지 않는다.
    public bool TryConsume(float amount)
    {
        if(_currentEnergy < amount) return false;

        CurrentEnergy -= amount;
        return true;
    }

    private void CacheComponent()
    {
        _controller = GetComponent<PlayerController>();
    }
}
