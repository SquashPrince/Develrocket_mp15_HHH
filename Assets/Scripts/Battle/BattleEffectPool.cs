using System.Collections.Generic;
using UnityEngine;

// 전투 이펙트 풀. 이펙트 재생 요청을 받아 Prefab 별로 미리 만들어 둔 이펙트를 꺼내 띄우고, 재생이 끝나면 다시 넣어 둔다.
// 타격처럼 이펙트가 자주 나오는 순간에 매번 만들고 지우지 않기 위해서다. 꺼져 있는 이펙트가 없으면 하나 더 만든다.
// 실제 시간 재생 : 히트스톱 · 콤보 선택처럼 게임 시간이 멈추거나 느려져도 이펙트는 그대로 재생되게 한다.
//   임포트한 이펙트 에셋은 게임 시간으로 재생하도록 만들어져 있어서, 원본 Prefab 은 두고 풀이 만든 복사본만 바꾼다.
//   파티클 : 실제 시간 재생, 끝나도 스스로 지우지 않음(풀이 다시 쓰도록)
//   Animator(메시를 움직이는 이펙트) : 실제 시간 갱신
// 반복 : 위치에 띄운 이펙트는 반복을 꺼 한 번 재생 후 끝나게 한다.
//   부모에 붙여 띄운 이펙트는 Prefab 의 반복 설정을 따른다. 반복하는 이펙트(펀치 러시의 먼지 등)는 멈춤 요청이 올 때까지 반복하고,
//   요청이 오면 새 파티클 생성만 멈춰 이미 나온 파티클이 사라진 뒤 회수한다.
// 회수 : 파티클이 모두 끝나거나, 최대 재생 시간이 지나면 끈다. 파티클이 없는 이펙트는 최대 재생 시간으로만 끝난다.
//   반복 중인 이펙트는 최대 재생 시간을 재지 않고, 멈춘 때부터 다시 잰다.
//   부모에 붙여 띄운 이펙트는 부모가 꺼지면(교체로 퇴장한 캐릭터 등) 바로 회수한다.
// 계층 : 이펙트는 이 오브젝트의 자식으로 만들어 꺼진 채 둔다. 부모에 붙여 띄운 이펙트는 재생하는 동안만 그 부모 아래에 있다.
//   회수하면 이 오브젝트 아래로 되돌리고 위치 · 회전 · 크기를 처음 값으로 돌린 뒤 끈다.
public class BattleEffectPool : MonoBehaviour
{
    // 풀에서 관리하는 이펙트 하나. 이펙트 아래 파티클과 Prefab 원래 크기를 미리 기억해 둔다.
    // Parent : 붙여 띄운 부모. 이 풀 아래에서 재생 중이거나 꺼져 있으면 null 이다.
    // Loops : 파티클마다 Prefab 원래의 반복 설정. IsLooping : 지금 반복 재생 중인지(멈춤 요청 전까지)
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

    [Tooltip("이펙트 하나가 켜져 있을 수 있는 최대 시간(초, 실제 시간). 파티클이 끝나지 않거나 파티클이 없는 이펙트도 이 시간이 지나면 회수한다. 반복 중인 이펙트는 멈춘 때부터 잰다")]
    [SerializeField, Min(0.1f)] private float _maxLifetime = 5f;

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

    // 꺼져 있는 이펙트를 꺼내고, 없으면 하나 더 만든다.
    private PooledEffect GetEffect(GameObject prefab)
    {
        if(!_pools.TryGetValue(prefab, out List<PooledEffect> pool))
        {
            pool = new List<PooledEffect>();
            _pools.Add(prefab, pool);
        }

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
