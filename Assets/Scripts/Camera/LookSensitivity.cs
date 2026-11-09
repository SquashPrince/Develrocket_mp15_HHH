using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

// 카메라 회전(Look) 입력의 감도를 마우스와 스틱으로 나눠 정한다.
// 마우스와 스틱이 같은 Look 액션과 Cinemachine 감도를 함께 쓰는데, 마우스는 한 프레임에 수~수십씩 움직이고 스틱은 최대 1이라 같은 감도로는 맞출 수 없다.
// 그래서 Look 액션의 바인딩마다 컨트롤 스킴에 맞는 배율 Processor 를 덮어쓴다.
// 설정 UI 를 만들면 SetMouseSensitivity · SetStickSensitivity 로 값을 바꾼다.
public class LookSensitivity : MonoBehaviour
{
    // 입력 에셋의 컨트롤 스킴 이름과 같아야 한다.
    private const string MOUSE_GROUP = "Keyboard&Mouse";
    private static readonly string[] STICK_GROUPS = { "Gamepad", "Switch" };

    [SerializeField] private InputActionReference _lookAction;
    [Tooltip("마우스 가로(x) · 세로(y) 감도 배율. 1이면 기본 감도")]
    [SerializeField] private Vector2 _mouseSensitivity = Vector2.one;
    [Tooltip("스틱 가로(x) · 세로(y) 감도 배율. 1이면 기본 감도")]
    [SerializeField] private Vector2 _stickSensitivity = Vector2.one;

    public Vector2 MouseSensitivity => _mouseSensitivity;
    public Vector2 StickSensitivity => _stickSensitivity;

    private void Start() => ApplySensitivity();

#if UNITY_EDITOR
    // 플레이 중 Inspector 에서 값을 바꾸면 바로 반영한다.
    private void OnValidate()
    {
        if(Application.isPlaying && _lookAction != null) ApplySensitivity();
    }
#endif

    public void SetMouseSensitivity(Vector2 sensitivity)
    {
        _mouseSensitivity = sensitivity;
        ApplySensitivity();
    }

    public void SetStickSensitivity(Vector2 sensitivity)
    {
        _stickSensitivity = sensitivity;
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
}
