using System;
using System.Collections.Generic;
using UnityEngine;

// 전투 이펙트 풀. 타격처럼 자주 나오는 이펙트를 매번 만들고 지우지 않도록 Prefab 별로 미리 만들어 두고 다시 쓴다.
// 게임 시간이 멈추거나 느려져도 재생되게, 풀이 만든 복사본만 실제 시간 재생으로 바꾼다(원본 Prefab 은 그대로).
// 위치에 띄운 이펙트는 한 번 재생하고, 부모에 붙여 띄운 이펙트는 Prefab 의 반복 설정을 따른다.
// 자세한 흐름 : Docs/Technical/Battle/BattleEffect.md
public class BattleEffectPool : MonoBehaviour
{
    // Parent : 이 풀 아래에서 재생 중이거나 꺼져 있으면 null. Loops : 파티클마다 Prefab 원래의 반복 설정
    private class PooledEffect
    {
        public GameObject Root;
        public Transform Parent;
        public ParticleSystem[] Systems;
        public bool[] Loops;
        public Vector3 BaseScale;
        public float Elapsed;
        public bool IsLooping;
    }

    // 로딩 중에 미리 만들어 둘 이펙트 하나와 개수
    [Serializable]
    private struct PrewarmEntry
    {
        public GameObject Prefab;
        [Min(1)] public int Count;
    }

    [Tooltip("이펙트 하나가 켜져 있을 수 있는 최대 시간(초, 실제 시간). 파티클이 끝나지 않거나 파티클이 없는 이펙트도 이 시간이 지나면 회수한다. 반복 중인 이펙트는 멈춘 때부터 잰다")]
    [SerializeField, Min(0.1f)] private float _maxLifetime = 5f;
    [Tooltip("로딩 중에 미리 만들어 둘 이펙트와 개수. 전투 중 한꺼번에 많이 나오는 이펙트(타격 · 베기)는 동시에 나오는 최대 수만큼 둔다")]
    [SerializeField] private PrewarmEntry[] _prewarm;

    // Prefab 별로 만든 이펙트 목록 · 지금 재생 중인 이펙트
    private readonly Dictionary<GameObject, List<PooledEffect>> _pools = new Dictionary<GameObject, List<PooledEffect>>();
    private readonly List<PooledEffect> _playing = new List<PooledEffect>();

    private void OnEnable() => BindEffectEvent();
    private void OnDisable() => UnbindEffectEvent();
    private void Update() => UpdatePlaying();

    private void OnEffectRequested(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        PooledEffect effect = GetEffect(prefab);

        effect.Root.transform.SetPositionAndRotation(position, rotation);
        StartEffect(effect, false);
    }

    // 부모 아래로 옮겨 부모 기준 위치 · 회전에 놓고, Prefab 원래 크기에 크기 배수를 곱한다.
    private void OnAttachedEffectRequested(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        PooledEffect effect = GetEffect(prefab);
        Transform root = effect.Root.transform;

        effect.Parent = parent;
        root.SetParent(parent, false);
        root.SetLocalPositionAndRotation(localPosition, localRotation);
        root.localScale = Vector3.Scale(effect.BaseScale, localScale);
        StartEffect(effect, true);
    }

    // 부모에 붙어 반복 중인 이펙트의 새 파티클 생성을 멈춘다. 이미 나온 파티클이 모두 사라지면 회수한다.
    private void OnLoopStopRequested(Transform parent)
    {
        foreach(PooledEffect effect in _playing)
        {
            if(!effect.IsLooping || effect.Parent != parent) continue;

            effect.IsLooping = false;
            effect.Elapsed = 0f;

            foreach(ParticleSystem system in effect.Systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    // keepLoop : Prefab 의 반복 설정을 따를지. 끄면 반복 이펙트도 한 번 재생 후 끝난다.
    private void StartEffect(PooledEffect effect, bool keepLoop)
    {
        effect.Elapsed = 0f;
        effect.IsLooping = false;

        // 켜기 전에 반복을 정해, 켜질 때 자동 재생(Play On Awake)하는 파티클도 이번 설정으로 재생하게 한다.
        for(int i = 0; i < effect.Systems.Length; i++)
        {
            ParticleSystem.MainModule main = effect.Systems[i].main;

            main.loop = keepLoop && effect.Loops[i];
            effect.IsLooping |= main.loop;
        }

        effect.Root.SetActive(true);

        // 켜질 때 자동 재생(Play On Awake)이 꺼진 파티클도 재생한다. 이미 재생 중인 파티클은 그대로다.
        foreach(ParticleSystem system in effect.Systems)
        {
            system.Play(false);
        }

        _playing.Add(effect);
    }

    // 재생 중인 이펙트의 시간을 실제 시간으로 재고, 끝난 이펙트를 회수한다.
    // 부모가 파괴되면 붙어 있던 이펙트도 함께 파괴되므로, 그런 이펙트는 회수하지 않고 목록에서만 지운다.
    private void UpdatePlaying()
    {
        for(int i = _playing.Count - 1; i >= 0; i--)
        {
            PooledEffect effect = _playing[i];

            if(effect.Root == null)
            {
                RemoveDestroyed(effect);
                _playing.RemoveAt(i);
                continue;
            }

            effect.Elapsed += Time.unscaledDeltaTime;

            if(!IsFinished(effect)) continue;

            Recall(effect);
            _playing.RemoveAt(i);
        }
    }

    private bool IsFinished(PooledEffect effect)
    {
        if(!effect.IsLooping && effect.Elapsed >= _maxLifetime) return true;

        // 부모가 꺼지면 이펙트도 화면에서 사라지고 파티클도 멈추므로 바로 회수한다.
        if(effect.Parent != null && !effect.Parent.gameObject.activeInHierarchy) return true;

        // 파티클이 없는 이펙트는 최대 재생 시간까지 둔다.
        if(effect.Systems.Length == 0) return false;

        foreach(ParticleSystem system in effect.Systems)
        {
            if(system.IsAlive(false)) return false;
        }

        return true;
    }

    // 이 오브젝트 아래로 되돌리고, 위치 · 회전 · 크기를 만들 때 값으로 돌린 뒤 끈다.
    private void Recall(PooledEffect effect)
    {
        Transform root = effect.Root.transform;

        effect.Root.SetActive(false);
        effect.Parent = null;
        effect.IsLooping = false;
        root.SetParent(transform, false);
        root.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.localScale = effect.BaseScale;
    }

    private void RemoveDestroyed(PooledEffect effect)
    {
        foreach(List<PooledEffect> pool in _pools.Values)
        {
            if(pool.Remove(effect)) return;
        }
    }

    // 미리 만들 목록의 이펙트를 개수만큼 만들고, 모두 position 에 한 번씩 켜서 재생한다.
    // 로딩 화면이 덮고 있는 동안 처음 그리는 끊김(셰이더 · 파티클 준비)을 미리 겪게 하기 위해서다. 끝나면 RecallAll 로 거둔다.
    public void Prewarm(Vector3 position)
    {
        foreach(PrewarmEntry entry in _prewarm)
        {
            List<PooledEffect> pool = GetPool(entry.Prefab);

            while(pool.Count < entry.Count)
            {
                pool.Add(CreateEffect(entry.Prefab));
            }

            foreach(PooledEffect effect in pool)
            {
                if(effect.Root.activeSelf) continue;

                effect.Root.transform.position = position;
                StartEffect(effect, false);
            }
        }
    }

    // 재생 중인 이펙트를 모두 바로 거둔다.
    public void RecallAll()
    {
        foreach(PooledEffect effect in _playing)
        {
            if(effect.Root != null) Recall(effect);
            else RemoveDestroyed(effect);
        }

        _playing.Clear();
    }

    private List<PooledEffect> GetPool(GameObject prefab)
    {
        if(!_pools.TryGetValue(prefab, out List<PooledEffect> pool))
        {
            pool = new List<PooledEffect>();
            _pools.Add(prefab, pool);
        }

        return pool;
    }

    // 꺼져 있는 이펙트를 꺼내고, 없으면 하나 더 만든다.
    private PooledEffect GetEffect(GameObject prefab)
    {
        List<PooledEffect> pool = GetPool(prefab);

        // 부모와 함께 파괴된 이펙트는 다음 갱신에서 목록에서 지우므로 건너뛴다.
        foreach(PooledEffect effect in pool)
        {
            if(effect.Root != null && !effect.Root.activeSelf) return effect;
        }

        PooledEffect created = CreateEffect(prefab);

        pool.Add(created);
        return created;
    }

    private PooledEffect CreateEffect(GameObject prefab)
    {
        GameObject root = Instantiate(prefab, transform);
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        PooledEffect effect = new PooledEffect { Root = root, Systems = systems, Loops = new bool[systems.Length], BaseScale = root.transform.localScale };

        root.SetActive(false);

        // 반복은 재생할 때마다 띄우는 방식에 따라 정하므로, Prefab 원래 값을 기억해 둔다.
        for(int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;

            effect.Loops[i] = main.loop;
            main.useUnscaledTime = true;
            main.stopAction = ParticleSystemStopAction.None;
        }

        foreach(Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        return effect;
    }

    private void BindEffectEvent()
    {
        BattleEffect.Requested += OnEffectRequested;
        BattleEffect.AttachedRequested += OnAttachedEffectRequested;
        BattleEffect.LoopStopRequested += OnLoopStopRequested;
    }

    private void UnbindEffectEvent()
    {
        BattleEffect.Requested -= OnEffectRequested;
        BattleEffect.AttachedRequested -= OnAttachedEffectRequested;
        BattleEffect.LoopStopRequested -= OnLoopStopRequested;
    }
}
