using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerState : MonoBehaviour, IHit
{
    public float maxHp;
    public float currentHp;
    
    [SerializeField] private float damColTime;
    [SerializeField] private float helColTime;
    private float currentTimeForDam;
    private float currentTimeForHel;

    private NetworkAnimator animator;

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
        if(currentHp <= 0) Die();
    }

    private void Die()
    {
        animator.Animator.SetTrigger("Die");
    }

    //맞는 함수
    public void Hit(float damage)
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
