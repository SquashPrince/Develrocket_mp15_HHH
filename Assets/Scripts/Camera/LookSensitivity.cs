using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

// 카메라 회전(Look) 감도를 마우스와 스틱으로 나눠 정한다. 마우스는 프레임당 수~수십, 스틱은 최대 1 이라 같은 감도로 맞출 수 없어
// Look 액션의 바인딩마다 컨트롤 스킴에 맞는 배율 Processor 를 덮어쓴다.
// 마우스 감도는 설정 저장값, 스틱 감도는 Inspector 값을 쓴다.
public class LookSensitivity : MonoBehaviour
{
    // 입력 에셋의 컨트롤 스킴 이름과 같아야 한다.
    private const string MOUSE_GROUP = "Keyboard&Mouse";
    private static readonly string[] STICK_GROUPS = { "Gamepad", "Switch" };

    [SerializeField] private InputActionReference _lookAction;
    [Tooltip("마우스 가로(x) · 세로(y) 감도 배율. 1이면 기본 감도. 실행하면 설정의 저장값으로 바뀐다")]
    [SerializeField] private Vector2 _mouseSensitivity = Vector2.one;
    [Tooltip("스틱 가로(x) · 세로(y) 감도 배율. 1이면 기본 감도")]
    [SerializeField] private Vector2 _stickSensitivity = Vector2.one;

    public Vector2 MouseSensitivity => _mouseSensitivity;
    public Vector2 StickSensitivity => _stickSensitivity;

    private void Start() => ApplySavedSensitivity();
    private void OnEnable() => BindSettingsEvent();
    private void OnDisable() => UnbindSettingsEvent();

#if UNITY_EDITOR
    // 플레이 중 Inspector 에서 값을 바꾸면 바로 반영한다.
    private void OnValidate()
    {
        if(Application.isPlaying && _lookAction != null) ApplySensitivity();
    }
#endif

    public void SetStickSensitivity(Vector2 sensitivity)
    {
        _stickSensitivity = sensitivity;
        ApplySensitivity();
    }

    // 설정의 마우스 감도를 가져와 반영한다. 시작할 때와 설정이 바뀔 때 부른다.
    private void ApplySavedSensitivity()
    {
        _mouseSensitivity = SettingsStore.Current.MouseSensitivity;
        ApplySensitivity();
    }

    private void ApplySensitivity()
    {
        InputAction action = _lookAction.action;

        for(int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if(binding.isComposite) continue;

            Vector2 scale;

            if(HasGroup(binding.groups, MOUSE_GROUP)) scale = _mouseSensitivity;
            else if(HasAnyGroup(binding.groups, STICK_GROUPS)) scale = _stickSensitivity;
            else continue;

            // 키 재설정으로 바뀐 경로 · 상호작용은 그대로 두고 Processor 만 덮어쓴다.
            action.ApplyBindingOverride(i, new InputBinding
            {
                overridePath = binding.overridePath,
                overrideInteractions = binding.overrideInteractions,
                overrideProcessors = GetScaleProcessor(scale)
            });
        }
    }

    private static string GetScaleProcessor(Vector2 scale)
    {
        string x = scale.x.ToString(CultureInfo.InvariantCulture);
        string y = scale.y.ToString(CultureInfo.InvariantCulture);

        return $"ScaleVector2(x={x},y={y})";
    }

    // 바인딩의 그룹 문자열은 ";Keyboard&Mouse;Touch" 처럼 ';' 로 구분된다.
    private static bool HasGroup(string groups, string group)
    {
        if(string.IsNullOrEmpty(groups)) return false;

        return Array.IndexOf(groups.Split(InputBinding.Separator), group) >= 0;
    }

    private static bool HasAnyGroup(string groups, string[] targetGroups)
    {
        foreach(string group in targetGroups)
        {
            if(HasGroup(groups, group)) return true;
        }

        return false;
    }

    // 볼륨이 바뀌어도 함께 알림이 오지만, 바인딩 몇 개를 덮어쓰는 정도라 따로 구분하지 않는다.
    private void BindSettingsEvent()
    {
        SettingsStore.Changed += ApplySavedSensitivity;
    }

    private void UnbindSettingsEvent()
    {
        SettingsStore.Changed -= ApplySavedSensitivity;
    }
}
