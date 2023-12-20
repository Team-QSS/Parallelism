using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerState : NetworkBehaviour, IHit
{
    public float maxHp;
    public float currentHp;
    
    [SerializeField] private float damColTime;
    [SerializeField] private float helColTime;
    private float currentTimeForDam;
    private float currentTimeForHel;

    private NetworkAnimator animator;
    private bool            isDie;

    private void Awake()
    {
        animator = GetComponent<NetworkAnimator>();
    }

    private void Start()
    {
        currentHp = maxHp;
        
        currentTimeForDam = damColTime;
        currentTimeForHel = helColTime;
    }

    private void Update()
    {
        currentTimeForDam += Time.deltaTime;
        currentTimeForHel += Time.deltaTime;
        // Debug.Log(isDie);
        if(currentHp <= 0 && !isDie) Die();
    }

    private void Die()
    {
        isDie = true;
        animator.Animator.SetTrigger("Die");
    }

    //맞는 함수
    public void Hit(float damage)
    {
        if (IsServer)
        {
            Debug.Log("server hit");
        }
        Debug.Log("hit");
        HitServerRpc(damage);
    }

    [ServerRpc (RequireOwnership = false)]
    private void HitServerRpc(float damage)
    {
        HitClientRpc(damage);
    }

    [ClientRpc]
    private void HitClientRpc(float damage)
    {
        if (currentTimeForDam < damColTime) return;
        currentHp -= damage;
        currentTimeForDam = 0f;
        Debug.Log($"Hit Character {currentHp}");
    }
    
    public void Heal(float healMount)
    {
        if (currentTimeForHel < helColTime) return;
        currentHp += healMount;
        currentTimeForHel = 0f;
    }
}
