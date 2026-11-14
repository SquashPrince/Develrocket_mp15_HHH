using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// FBX 클립의 Start · End 를 바꿀 때 Event · Curve 를 원본 프레임 위치에 고정해 주는 에디터 창
public class ClipRangeEditorWindow : EditorWindow
{
    private const float RANGE_EPSILON = 0.0001f;
    private const float SLOPE_SAMPLE_OFFSET = 0.0001f;
    private const float FRAME_LABEL_WIDTH = 40f;

    private GameObject _model;
    private ModelImporter _importer;
    private ModelImporterClipAnimation[] _clips;
    private float[] _newFirstFrames;
    private float[] _newLastFrames;
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Clip Range Editor")]
    private static void Open() => GetWindow<ClipRangeEditorWindow>("Clip Range Editor");

    private void OnGUI()
    {
        DrawModelField();

        if(_clips == null) return;

        DrawClipList();
        DrawButtons();
    }

    private void DrawModelField()
    {
        EditorGUI.BeginChangeCheck();

        _model = (GameObject)EditorGUILayout.ObjectField("FBX", _model, typeof(GameObject), false);

        if(EditorGUI.EndChangeCheck()) LoadClips();

        if(_model != null && _importer == null)
        {
            EditorGUILayout.HelpBox("모델 에셋(FBX 등)이 아닙니다.", MessageType.Warning);
        }
    }

    private void DrawClipList()
    {
        if(_clips.Length == 0)
        {
            EditorGUILayout.HelpBox("이 모델에는 애니메이션 클립이 없습니다.", MessageType.Info);
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        for(int i = 0; i < _clips.Length; i++)
        {
            DrawClipRow(i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawClipRow(int index)
    {
        ModelImporterClipAnimation clip = _clips[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(clip.name, EditorStyles.boldLabel);

        float labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = FRAME_LABEL_WIDTH;

        EditorGUILayout.BeginHorizontal();
        _newFirstFrames[index] = EditorGUILayout.FloatField("Start", _newFirstFrames[index]);
        _newLastFrames[index] = EditorGUILayout.FloatField("End", _newLastFrames[index]);
        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = labelWidth;

        EditorGUILayout.LabelField($"원래 구간 {clip.firstFrame} ~ {clip.lastFrame}  ·  Event {clip.events.Length}  ·  Curve {clip.curves.Length}");

        DrawClipWarning(index);

        EditorGUILayout.EndVertical();
    }

    private void DrawClipWarning(int index)
    {
        if(!IsValidRange(index))
        {
            EditorGUILayout.HelpBox("End 는 Start 보다 커야 합니다.", MessageType.Error);
            return;
        }

        int outEventCount = CountOutOfRangeEvents(index);
        int outKeyCount = CountOutOfRangeKeys(index);

        if(outEventCount == 0 && outKeyCount == 0) return;

        EditorGUILayout.HelpBox($"새 구간 밖 : Event {outEventCount}개 · Curve 키 {outKeyCount}개", MessageType.Warning);
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if(GUILayout.Button("다시 불러오기")) LoadClips();

        if(GUILayout.Button("적용"))
        {
            ApplyRanges();

            // 확인 창 · 재임포트 뒤 레이아웃이 어긋나지 않도록 이번 OnGUI 를 끝낸다
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void LoadClips()
    {
        _importer = null;
        _clips = null;

        if(_model == null) return;

        _importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(_model)) as ModelImporter;
        if(_importer == null) return;

        _clips = _importer.clipAnimations;

        // 클립을 한 번도 편집하지 않은 모델은 clipAnimations 가 비어 있으므로 기본 클립에서 시작한다
        if(_clips.Length == 0) _clips = _importer.defaultClipAnimations;

        _newFirstFrames = new float[_clips.Length];
        _newLastFrames = new float[_clips.Length];

        for(int i = 0; i < _clips.Length; i++)
        {
            _newFirstFrames[i] = _clips[i].firstFrame;
            _newLastFrames[i] = _clips[i].lastFrame;
        }
    }

    private void ApplyRanges()
    {
        int outEventCount = 0;
        int outKeyCount = 0;

        for(int i = 0; i < _clips.Length; i++)
        {
            if(!IsValidRange(i))
            {
                EditorUtility.DisplayDialog("구간 오류", $"{_clips[i].name} : End 는 Start 보다 커야 합니다.", "확인");
                return;
            }

            if(!IsRangeChanged(i)) continue;

            outEventCount += CountOutOfRangeEvents(i);
            outKeyCount += CountOutOfRangeKeys(i);
        }

        if(outEventCount > 0 || outKeyCount > 0)
        {
            string message = $"새 구간 밖으로 나가는 Event {outEventCount}개는 삭제됩니다.\n" +
                $"Curve 키 {outKeyCount}개는 삭제하고, 구간 경계(0 · 1)에 그 시점의 값 · 기울기로 키를 새로 만듭니다.";

            if(!EditorUtility.DisplayDialog("구간 밖 항목", message, "삭제하고 적용", "취소")) return;
        }

        for(int i = 0; i < _clips.Length; i++)
        {
            if(IsRangeChanged(i)) RemapClip(i);
        }

        _importer.clipAnimations = _clips;
        _importer.SaveAndReimport();

        LoadClips();
    }

    private void RemapClip(int index)
    {
        ModelImporterClipAnimation clip = _clips[index];

        List<AnimationEvent> events = new List<AnimationEvent>();

        foreach(AnimationEvent animationEvent in clip.events)
        {
            animationEvent.time = RemapTime(index, animationEvent.time);

            if(IsOutOfRange(animationEvent.time)) continue;

            events.Add(animationEvent);
        }

        clip.events = events.ToArray();

        // 정규화 시간 1 이 가리키는 프레임 길이가 바뀌므로, 접선 기울기도 같은 비율로 바꿔 곡선 모양을 유지한다
        float tangentScale = (_newLastFrames[index] - _newFirstFrames[index]) / (clip.lastFrame - clip.firstFrame);
        ClipAnimationInfoCurve[] curves = clip.curves;

        for(int c = 0; c < curves.Length; c++)
        {
            RemapCurve(index, curves[c].curve, tangentScale);
        }

        clip.curves = curves;

        // 위의 시간 환산이 기존 구간을 기준으로 하므로, 구간 값은 마지막에 바꾼다
        clip.firstFrame = _newFirstFrames[index];
        clip.lastFrame = _newLastFrames[index];
    }

    // 키 시간을 새 구간 기준으로 바꾸고, 구간 밖으로 나간 키는 경계(0 · 1) 키로 대신한다
    private void RemapCurve(int index, AnimationCurve curve, float tangentScale)
    {
        Keyframe[] keys = curve.keys;

        for(int k = 0; k < keys.Length; k++)
        {
            keys[k].time = RemapTime(index, keys[k].time);
            keys[k].inTangent *= tangentScale;
            keys[k].outTangent *= tangentScale;
        }

        curve.keys = keys;

        List<Keyframe> rangeKeys = new List<Keyframe>();
        bool hasKeyBefore = false;
        bool hasKeyAfter = false;

        foreach(Keyframe key in keys)
        {
            if(key.time < -RANGE_EPSILON) hasKeyBefore = true;
            else if(key.time > 1f + RANGE_EPSILON) hasKeyAfter = true;
            else rangeKeys.Add(key);
        }

        // 경계 키의 값 · 기울기는 밖의 키를 지우기 전 곡선에서 읽어야 클립 안의 모양이 유지된다
        if(hasKeyBefore) rangeKeys.Insert(0, CreateBoundaryKey(curve, 0f));
        if(hasKeyAfter) rangeKeys.Add(CreateBoundaryKey(curve, 1f));

        curve.keys = rangeKeys.ToArray();
    }

    private Keyframe CreateBoundaryKey(AnimationCurve curve, float time)
    {
        float slope = (curve.Evaluate(time + SLOPE_SAMPLE_OFFSET) - curve.Evaluate(time - SLOPE_SAMPLE_OFFSET)) / (2f * SLOPE_SAMPLE_OFFSET);

        return new Keyframe(time, curve.Evaluate(time), slope, slope);
    }

    // 기존 구간의 정규화 시간을 원본 프레임으로 환산한 뒤, 새 구간의 정규화 시간으로 바꾼다
    private float RemapTime(int index, float normalizedTime)
    {
        ModelImporterClipAnimation clip = _clips[index];
        float sourceFrame = clip.firstFrame + normalizedTime * (clip.lastFrame - clip.firstFrame);

        return (sourceFrame - _newFirstFrames[index]) / (_newLastFrames[index] - _newFirstFrames[index]);
    }

    private int CountOutOfRangeEvents(int index)
    {
        int count = 0;

        foreach(AnimationEvent animationEvent in _clips[index].events)
        {
            if(IsOutOfRange(RemapTime(index, animationEvent.time))) count++;
        }

        return count;
    }

    private int CountOutOfRangeKeys(int index)
    {
        int count = 0;

        foreach(ClipAnimationInfoCurve curve in _clips[index].curves)
        {
            foreach(Keyframe key in curve.curve.keys)
            {
                if(IsOutOfRange(RemapTime(index, key.time))) count++;
            }
        }

        return count;
    }

    private bool IsOutOfRange(float normalizedTime) => normalizedTime < -RANGE_EPSILON || normalizedTime > 1f + RANGE_EPSILON;

    private bool IsValidRange(int index) => _newLastFrames[index] > _newFirstFrames[index];

    private bool IsRangeChanged(int index) => !Mathf.Approximately(_newFirstFrames[index], _clips[index].firstFrame) || !Mathf.Approximately(_newLastFrames[index], _clips[index].lastFrame);
}
