using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// [일회용] 적 공격 설정을 Prefab 의 공격 목록에서 공격 데이터 에셋으로 옮기는 변환 도구. 변환이 끝나면 이 파일을 지운다. (#52)
// 1. 적 Prefab 의 적 공격 기능이 가진 공격마다 에셋을 만들고, 그 적의 적 데이터 에셋 공격 목록에 넣는다. Prefab 은 바꾸지 않는다.
//    공격 공통 값은 같은 이름의 칸으로 복사하고, 단계별 상태 이름은 동작 목록으로 옮긴다.
//    판정 범위 · 패링 값은 모든 동작에 복사하고, 판정 지속 공격은 구간 방식, 아니면 한 순간 방식으로 둔다. 회전 속도는 그 적의 값을 쓴다.
// 2. 적 애니메이션 폴더의 클립 이벤트 Hit 을 HitStart 로 바꾼다. 옛 플레이어 클립에도 Hit 이 있어 적 폴더만 바꾼다.
public static class EnemyAttackConverter
{
    private const string ATTACK_FOLDER = "Assets/Datas/Enemy/Attacks";
    private const string OLD_HIT_FUNCTION = "Hit";
    private const string NEW_HIT_FUNCTION = "HitStart";

    // 변환할 적 : Prefab, 공격 에셋 폴더 이름, 공격 목록을 넣을 적 데이터 에셋들
    private static readonly (string prefabPath, string folderName, string[] dataPaths)[] ENEMIES =
    {
        ("Assets/Prefabs/Enemy/Earth Golem.prefab", "Earth Golem", new[] { "Assets/Datas/Enemy/Earth Golem.asset" }),
        ("Assets/Prefabs/Enemy/Boss_Black Knight.prefab", "Black Knight", new[] { "Assets/Datas/Enemy/Black Knight.asset" })
    };

    // 엘리트 : 씬의 엘리트 개체가 공격 목록을 덮어써 회전 공격 하나만 쓰고 있어, 그 씬 값으로 엘리트 전용 공격을 만든다 (사용자 결정)
    private const string ELITE_INSTANCE_NAME = "Earth Golem (1)";
    private const string ELITE_FOLDER_NAME = "Earth Golem";
    private const string ELITE_ATTACK_NAME = "Spin Attack Elite";
    private const string ELITE_DATA_PATH = "Assets/Datas/Enemy/Earth Golem Elite.asset";

    // 이벤트를 바꿀 적 애니메이션 폴더
    private static readonly string[] ENEMY_CLIP_FOLDERS =
    {
        "Assets/Imports/Monsters/Monsters Ultimate Pack 04 Cute Series/Golem Earth Cute Series/FBX",
        "Assets/Imports/Monsters/RPGMonsterBundlePolyart/RPGMonsterWave02Polyart/Animations/Black Knight"
    };

    [MenuItem("Tools/HHH/Enemy Attack Converter/1. Convert")]
    private static void Convert()
    {
        if(AssetDatabase.IsValidFolder(ATTACK_FOLDER))
        {
            Debug.LogError($"[적 공격 변환] 이미 변환한 폴더가 있다 : {ATTACK_FOLDER}. 다시 변환하려면 폴더를 지운 뒤 실행한다");
            return;
        }

        AssetDatabase.CreateFolder("Assets/Datas/Enemy", "Attacks");

        int attackCount = 0;

        foreach((string prefabPath, string folderName, string[] dataPaths) in ENEMIES)
        {
            attackCount += ConvertEnemy(prefabPath, folderName, dataPaths);
        }

        if(ConvertEliteFromScene()) attackCount++;

        AssetDatabase.SaveAssets();

        int eventCount = RenameHitEvents();

        Debug.Log($"[적 공격 변환] 완료. 공격 에셋 {attackCount} 개, 클립 이벤트 {eventCount} 개를 바꿨다. 에셋 값 · 적 데이터 공격 목록 · 클립 이벤트를 확인한다");
    }

    // 적 Prefab 의 공격 목록을 공격 에셋으로 만들고 적 데이터들의 공격 목록에 넣는다. 만든 공격 수를 돌려준다.
    private static int ConvertEnemy(string prefabPath, string folderName, string[] dataPaths)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        SerializedObject attackerObject = new SerializedObject(prefab.GetComponent<EnemyAttacker>());
        SerializedProperty attacks = attackerObject.FindProperty("_attacks");
        float turnSpeed = attackerObject.FindProperty("_turnSpeed").floatValue;

        string folder = $"{ATTACK_FOLDER}/{folderName}";
        AssetDatabase.CreateFolder(ATTACK_FOLDER, folderName);

        List<EnemyAttackData> created = new List<EnemyAttackData>();

        for(int i = 0; i < attacks.arraySize; i++)
        {
            SerializedProperty source = attacks.GetArrayElementAtIndex(i);
            created.Add(CreateAttack(source, turnSpeed, folder, source.FindPropertyRelative("_name").stringValue));
        }

        foreach(string dataPath in dataPaths)
        {
            SetAttackList(dataPath, created);
        }

        return created.Count;
    }

    // 열린 씬의 엘리트 개체가 덮어쓴 첫 공격(회전 공격)으로 엘리트 전용 공격을 만들어 엘리트 데이터에 넣는다. 개체가 없으면 false 를 돌려준다.
    private static bool ConvertEliteFromScene()
    {
        foreach(EnemyAttacker attacker in Object.FindObjectsByType<EnemyAttacker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(attacker.name != ELITE_INSTANCE_NAME) continue;

            SerializedObject attackerObject = new SerializedObject(attacker);
            SerializedProperty source = attackerObject.FindProperty("_attacks").GetArrayElementAtIndex(0);
            float turnSpeed = attackerObject.FindProperty("_turnSpeed").floatValue;

            EnemyAttackData eliteAttack = CreateAttack(source, turnSpeed, $"{ATTACK_FOLDER}/{ELITE_FOLDER_NAME}", ELITE_ATTACK_NAME);
            SetAttackList(ELITE_DATA_PATH, new List<EnemyAttackData> { eliteAttack });
            return true;
        }

        Debug.LogError($"[적 공격 변환] 열린 씬에 {ELITE_INSTANCE_NAME} 이 없어 엘리트 공격을 만들지 못했다. CharacterTest 씬을 연 상태에서 다시 실행한다");
        return false;
    }

    private static void SetAttackList(string dataPath, List<EnemyAttackData> attacks)
    {
        SerializedObject dataObject = new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath));
        SerializedProperty list = dataObject.FindProperty("_attacks");

        list.arraySize = attacks.Count;

        for(int i = 0; i < attacks.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = attacks[i];
        }

        dataObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static EnemyAttackData CreateAttack(SerializedProperty source, float turnSpeed, string folder, string attackName)
    {
        EnemyAttackData attack = ScriptableObject.CreateInstance<EnemyAttackData>();
        string fileName = MakeFileName(attackName);
        AssetDatabase.CreateAsset(attack, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset"));

        SerializedObject attackObject = new SerializedObject(attack);
        SerializedProperty property = attackObject.GetIterator();

        // 공격 공통 칸 : 옛 설정에 같은 이름의 칸이 있으면 복사한다.
        property.NextVisible(true);

        while(property.NextVisible(false))
        {
            SerializedProperty oldValue = source.FindPropertyRelative(property.name);

            if(oldValue != null) CopyValue(oldValue, property);
        }

        attackObject.FindProperty("_turnSpeed").floatValue = turnSpeed;
        CreateSteps(source, attackObject.FindProperty("_steps"));

        attackObject.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    // 단계별 상태 이름을 동작 목록으로 옮기고, 판정 범위 · 방식 · 패링 값을 모든 동작에 복사한다.
    private static void CreateSteps(SerializedProperty source, SerializedProperty steps)
    {
        SerializedProperty states = source.FindPropertyRelative("_animationStates");
        bool isContinuousHit = source.FindPropertyRelative("_isContinuousHit").boolValue;

        steps.arraySize = states.arraySize;

        for(int i = 0; i < states.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);

            step.FindPropertyRelative("_animationState").stringValue = states.GetArrayElementAtIndex(i).stringValue;
            step.FindPropertyRelative("_hitMode").enumValueIndex = (int)(isContinuousHit ? EnemyAttackStep.HitMode.Window : EnemyAttackStep.HitMode.Instant);
            step.FindPropertyRelative("_hitCenter").vector3Value = source.FindPropertyRelative("_hitCenter").vector3Value;
            step.FindPropertyRelative("_hitRadius").floatValue = source.FindPropertyRelative("_hitRadius").floatValue;
            step.FindPropertyRelative("_isParryable").boolValue = source.FindPropertyRelative("_isParryable").boolValue;
            step.FindPropertyRelative("_parryStaggerTime").floatValue = source.FindPropertyRelative("_parryStaggerTime").floatValue;
        }
    }

    private static void CopyValue(SerializedProperty from, SerializedProperty to)
    {
        switch(to.propertyType)
        {
            case SerializedPropertyType.Float:
                to.floatValue = from.floatValue;
                break;
            case SerializedPropertyType.Integer:
                to.intValue = from.intValue;
                break;
            case SerializedPropertyType.Boolean:
                to.boolValue = from.boolValue;
                break;
            case SerializedPropertyType.Enum:
                to.enumValueIndex = from.enumValueIndex;
                break;
        }
    }

    // 파일 이름에 쓸 수 없는 글자를 지운다. 비면 기본 이름을 쓴다.
    private static string MakeFileName(string name)
    {
        foreach(char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid.ToString(), "");
        }

        return string.IsNullOrWhiteSpace(name) ? "Attack" : name.Trim();
    }

    // 적 애니메이션 폴더의 모델 클립 이벤트 중 Hit 을 HitStart 로 바꾼다. 바꾼 이벤트 수를 돌려준다.
    private static int RenameHitEvents()
    {
        int count = 0;

        foreach(string guid in AssetDatabase.FindAssets("t:Model", ENEMY_CLIP_FOLDERS))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if(importer == null) continue;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            int renamed = 0;

            foreach(ModelImporterClipAnimation clip in clips)
            {
                AnimationEvent[] events = clip.events;

                foreach(AnimationEvent animationEvent in events)
                {
                    if(animationEvent.functionName != OLD_HIT_FUNCTION) continue;

                    animationEvent.functionName = NEW_HIT_FUNCTION;
                    renamed++;
                }

                clip.events = events;
            }

            if(renamed == 0) continue;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();

#if UNITY_EDITOR
            Debug.Log($"[적 공격 변환] {path} : 이벤트 {renamed} 개");
#endif

            count += renamed;
        }

        return count;
    }
}
