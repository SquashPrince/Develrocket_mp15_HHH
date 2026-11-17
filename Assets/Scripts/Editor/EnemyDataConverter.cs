using UnityEditor;
using UnityEngine;

// [일회용] 적 데이터 변환 도구. 변환이 끝나면 이 파일을 지운다. (#51)
// 데이터 변환 메뉴는 옛 스크립트를 지우면서 함께 지웠다. 옛 스크립트를 지운 뒤 Prefab 에 남은 Missing Script 컴포넌트를 지운다.
public static class EnemyDataConverter
{
    private static readonly string[] PREFAB_PATHS =
    {
        "Assets/Prefabs/Enemy/Earth Golem.prefab",
        "Assets/Prefabs/Enemy/Boss_Black Knight.prefab"
    };

    [MenuItem("Tools/HHH/Enemy Data Converter/2. Remove Missing Scripts")]
    private static void RemoveMissingScripts()
    {
        foreach(string prefabPath in PREFAB_PATHS)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log("[적 데이터 변환] Prefab 의 Missing Script 정리 완료");
    }
}
