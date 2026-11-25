using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 버튼 칸 하나. 입력이 눌린 순간 아이콘이 잠깐 커진다. 실제 행동이 나갔는지는 따지지 않는다.
// 사용 가능 여부와 쿨타임은 ActionButtonHud 가 정해 알려준다. 화면 연출이라 실제 시간으로 흐른다.
public class ActionButton : MonoBehaviour
{
    [Tooltip("이 칸이 반응할 입력. 교체처럼 키가 둘인 칸은 둘 다 넣는다")]
    [SerializeField] private InputActionReference[] _actions;
    [SerializeField] private Image _icon;

    [Header("눌림 표시")]
    [Tooltip("눌린 순간 아이콘 크기 배율. 이 크기에서 원래 크기로 돌아온다")]
    [SerializeField, Min(1f)] private float _pressScale = 1.2f;
    [Tooltip("원래 크기로 돌아오는 데 걸리는 시간(초, 실제 시간)")]
    [SerializeField, Min(0.01f)] private float _pressDuration = 0.15f;

    [Header("사용 불가 · 쿨타임")]
    [Tooltip("쓸 수 없을 때 아이콘에 곱할 색")]
    [SerializeField] private Color _unavailableColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Tooltip("[선택] 쿨타임 표시 이미지 (Image Type : Filled). 쿨타임 중에만 켜고 남은 비율만큼 채운다")]
    [SerializeField] private Image _cooldownImage;

    private Color _availableColor;
    private Vector3 _baseScale;
    private float _pressTimer;

    private void Awake() => CacheComponent();
    private void Update()
    {
        CheckPressed();
        UpdatePressScale();
    }

    public void SetAvailable(bool isAvailable) => _icon.color = isAvailable ? _availableColor : _unavailableColor;

    // remainingRatio : 남은 쿨타임 비율(1 → 0). 0 이면 쿨타임 표시를 숨긴다.
    public void SetCooldown(float remainingRatio)
    {
        if(_cooldownImage == null) return;

        bool isCooldown = remainingRatio > 0f;

        if(_cooldownImage.gameObject.activeSelf != isCooldown) _cooldownImage.gameObject.SetActive(isCooldown);

        _cooldownImage.fillAmount = remainingRatio;
    }

    private void CheckPressed()
    {
        foreach(InputActionReference action in _actions)
        {
            if(!action.action.WasPressedThisFrame()) continue;

            _pressTimer = _pressDuration;
            return;
        }
    }

    private void UpdatePressScale() => ScalePop.Tick(ref _pressTimer, _pressDuration, _pressScale, _icon.transform, _baseScale);

    private void CacheComponent()
    {
        _availableColor = _icon.color;
        _baseScale = _icon.transform.localScale;
    }
}
