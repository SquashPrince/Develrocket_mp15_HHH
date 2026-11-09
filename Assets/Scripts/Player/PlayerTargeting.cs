using UnityEngine;

// 공격 · 록온 대상을 찾는다.
// 범위 안의 적 중 카메라 중앙 레이와의 수직 거리가 가장 짧은 적을 대상으로 고른다.
// 같은 화면 위치라면 가까운 적일수록 레이와의 거리가 짧아 근접 공격 대상으로 우선된다.
public class PlayerTargeting : MonoBehaviour
{
    // 한 번의 대상 찾기에서 검사할 수 있는 최대 콜라이더 수
    private const int MAX_CANDIDATE_COLLIDERS = 32;

    [Header("참조")]
    [Tooltip("화면 중앙 레이의 기준이 되는 카메라 (Main Camera)")]
    [SerializeField] private Transform _cameraTransform;

    [Header("사거리")]
    [Tooltip("공격 사거리. 자동 조준 범위와 같다")]
    [SerializeField, Min(0f)] private float _attackRange = 5f;
    [Tooltip("록온 사거리. 록온 대상이 이보다 멀어지면 록온이 풀린다")]
    [SerializeField, Min(0f)] private float _lockOnRange = 12f;

    private readonly Collider[] _candidateColliders = new Collider[MAX_CANDIDATE_COLLIDERS];

    public float AttackRange => _attackRange;
    public float LockOnRange => _lockOnRange;

    private void OnDrawGizmosSelected() => DrawRange();

    // 플레이어 기준 range 안의 적 중 카메라 중앙 레이와 가장 가까운 적을 돌려준다. 없으면 null 이다.
    public DummyEnemy FindTarget(float range)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, range, _candidateColliders, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        Vector3 rayOrigin = _cameraTransform.position;
        Vector3 rayDirection = _cameraTransform.forward;
        DummyEnemy closestEnemy = null;
        float closestSqrDistance = float.MaxValue;

        for(int i = 0; i < count; i++)
        {
            DummyEnemy enemy = _candidateColliders[i].GetComponentInParent<DummyEnemy>();

            if(enemy == null) continue;

            Vector3 toEnemy = _candidateColliders[i].bounds.center - rayOrigin;

            // 카메라 뒤쪽의 적은 화면에 보이지 않으므로 제외한다.
            if(Vector3.Dot(rayDirection, toEnemy) < 0f) continue;

            // 레이 방향(길이 1)과의 외적 크기가 레이까지의 수직 거리다. 크기 비교만 하므로 제곱근 없이 제곱으로 비교한다.
            float sqrDistance = Vector3.Cross(rayDirection, toEnemy).sqrMagnitude;

            if(sqrDistance >= closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closestEnemy = enemy;
        }

        return closestEnemy;
    }

    // Scene 창에서 플레이어를 선택하면 공격 사거리(노랑)와 록온 사거리(하늘)를 보여준다.
    // 플레이 중에는 지금 공격하면 선택될 대상까지 선을 긋는다.
    private void DrawRange()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _lockOnRange);

        if(!Application.isPlaying) return;

        DummyEnemy target = FindTarget(_attackRange);

        if(target == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.transform.position);
    }
}
