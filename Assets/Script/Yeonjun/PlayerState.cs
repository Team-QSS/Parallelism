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
        hpBar    = GetComponentInChildren<Slider>();
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

        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log(currentHp);
        }
        hpBar.value = currentHp;
        // Debug.Log(isDie);
        if(currentHp <= 0 && !isDie) Die(red);
    }

    //죽는 함수
    private void Die(bool red)
    {
        isDie = true;
        animator.Animator.SetTrigger("Die");

        DieServerRpc(!red);
    }

    [ServerRpc (RequireOwnership = false)]
    private void DieServerRpc(bool red)
    {
        DieClientRpc(red);
    }

    [ClientRpc]
    private void DieClientRpc(bool red)
    {
        GameEnd(red);
        GameObject.FindGameObjectWithTag("AttackerPlayerRed").GetComponent<Attacker>().GameEnd(red);
        GameObject.FindGameObjectWithTag("AttackerPlayerBlue").GetComponent<Attacker>().GameEnd(red);
    }

    //누구 죽으면 다 호출 bool은 red가 이기면 true
    public void GameEnd(bool winRed)
    {
        var canvas = FindObjectOfType<Title>().GetComponent<Canvas>();
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
        Debug.Log($"Hit Character {currentHp}");
    }
    
    public void Heal(float healMount)
    {
        if (currentTimeForHel < helColTime) return;
        currentHp += healMount;
        currentTimeForHel = 0f;
    }
}
