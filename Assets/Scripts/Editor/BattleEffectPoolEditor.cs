using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 전투 이펙트 풀 Inspector 에 "데이터에서 이펙트 모으기" 버튼을 더한다.
// 데이터 폴더(Assets/Datas)의 ScriptableObject 를 훑어, 전투 이펙트로 쓰는 칸에 연결된 Prefab 을 미리 만들 목록에 넣는다.
//   전투 이펙트 칸 : 적중 이펙트 설정 · 동작 이펙트 설정의 Prefab, 패링 이펙트, 적 사망 이펙트
// 이미 목록에 있는 이펙트는 개수를 그대로 두고, 없는 것만 종류별 기본 개수로 더한다. 데이터에서 빠진 이펙트는 지우지 않고 알려 준다.
[CustomEditor(typeof(BattleEffectPool))]
public class BattleEffectPoolEditor : Editor
{
    // 종류별 기본 개수. 적중은 여러 적을 한 번에 칠 때, 사망은 광역기로 여러 적이 한꺼번에 쓰러질 때 동시에 많이 나온다.
    private const int HIT_COUNT = 8;
    private const int DEATH_COUNT = 8;
    private const int DEFAULT_COUNT = 2;
    // 훑을 데이터 폴더. 임포트 에셋 · Behavior 그래프 같은 다른 ScriptableObject 는 훑지 않는다.
    private const string DATA_FOLDER = "Assets/Datas";

    // Prefab 칸을 가진 이펙트 설정 묶음의 타입 이름과, 그 안의 Prefab 칸 이름
    private const string HIT_SETTING_TYPE = "HitEffectSetting";
    private const string MOTION_SETTING_TYPE = "MotionEffectSetting";
    private const string SETTING_PREFAB_FIELD = "_prefab";
    // 데이터에 바로 있는 이펙트 칸 이름
    private const string PARRY_FIELD = "_parryEffectPrefab";
    private const string DEATH_FIELD = "_deathEffectPrefab";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.Space();

        if(GUILayout.Button("데이터에서 이펙트 모으기")) CollectEffects();
    }

    private void CollectEffects()
    {
        Dictionary<GameObject, int> found = FindEffects();
        SerializedProperty list = serializedObject.FindProperty("_prewarm");
        HashSet<GameObject> existing = new HashSet<GameObject>();

        for(int i = 0; i < list.arraySize; i++)
        {
            Object prefab = list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue;

            if(prefab is GameObject gameObject) existing.Add(gameObject);
        }

        int added = 0;

        foreach(KeyValuePair<GameObject, int> pair in found)
        {
            if(existing.Contains(pair.Key)) continue;

            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);

            entry.FindPropertyRelative("Prefab").objectReferenceValue = pair.Key;
            entry.FindPropertyRelative("Count").intValue = pair.Value;
            added++;
        }

        serializedObject.ApplyModifiedProperties();
        ReportMissing(existing, found);
        Debug.Log($"[이펙트 풀] 데이터에서 이펙트 {found.Count}종을 찾아 {added}종을 더했습니다.", target);
    }

    // 목록에는 있지만 데이터에서 더 이상 쓰지 않는 이펙트를 알린다. 직접 넣어 둔 것일 수 있어 지우지 않는다.
    private void ReportMissing(HashSet<GameObject> existing, Dictionary<GameObject, int> found)
    {
        foreach(GameObject prefab in existing)
        {
            if(!found.ContainsKey(prefab)) Debug.Log($"[이펙트 풀] 데이터에서 쓰지 않는 이펙트 : {prefab.name}", prefab);
        }
    }

    // 모든 ScriptableObject 데이터의 칸을 훑어 이펙트 Prefab 과 기본 개수를 모은다. 여러 종류로 쓰이면 큰 개수를 쓴다.
    private static Dictionary<GameObject, int> FindEffects()
    {
        Dictionary<GameObject, int> found = new Dictionary<GameObject, int>();

        foreach(string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { DATA_FOLDER }))
        {
            ScriptableObject data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));

            if(data == null) continue;

            SerializedProperty property = new SerializedObject(data).GetIterator();
            bool enterChildren = true;

            // 관리 참조(SerializeReference) 안으로는 들어가지 않는다. Behavior 그래프처럼 서로를 가리키는 참조가 있으면 끝없이 돌기 때문이다.
            while(property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.ManagedReference;

                if(property.type == HIT_SETTING_TYPE) Add(found, property.FindPropertyRelative(SETTING_PREFAB_FIELD), HIT_COUNT);
                else if(property.type == MOTION_SETTING_TYPE) Add(found, property.FindPropertyRelative(SETTING_PREFAB_FIELD), DEFAULT_COUNT);
                else if(property.name == PARRY_FIELD) Add(found, property, DEFAULT_COUNT);
                else if(property.name == DEATH_FIELD) Add(found, property, DEATH_COUNT);
            }
        }

        return found;
    }

    private static void Add(Dictionary<GameObject, int> found, SerializedProperty property, int count)
    {
        if(property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;
        if(!(property.objectReferenceValue is GameObject prefab)) return;

        found[prefab] = found.TryGetValue(prefab, out int current) ? Mathf.Max(current, count) : count;
    }
}
