using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;

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

    private bool red;
    
    private Slider hpBar;

    private void Awake()
    {
        animator = GetComponent<NetworkAnimator>();
    }

    private void Start()
    {
        red = gameObject.name == "PlayerRed(Clone)";
        
        currentHp   = maxHp;
        hpBar.value = currentHp;
        
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

    //죽는 함수
    private void Die()
    {
        isDie = true;
        animator.Animator.SetTrigger("Die");
    }

    //누구 죽으면 다 호출 bool은 red가 이기면 true
    public void GameEnd(bool winRed)
    {
        var canvas = GameObject.Find("End Canvas").GetComponent<Canvas>();
        if (canvas.enabled) return;
        if (winRed && red)
        {
            canvas.transform.Find("Dead").GetComponent<TextMeshProUGUI>().text = "You Win!";
            canvas.enabled                                                     = true;
        }
        else
        {
            canvas.transform.Find("Dead").GetComponent<TextMeshProUGUI>().text = "You Lose!";
            canvas.enabled                                                     = true;
        }
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
        
        currentHp         -= damage;
        currentTimeForDam =  0f;
        hpBar.value       =  currentHp;
        Debug.Log($"Hit Character {currentHp}");
    }
    
    public void Heal(float healMount)
    {
        if (currentTimeForHel < helColTime) return;
        currentHp += healMount;
        currentTimeForHel = 0f;
    }
}
