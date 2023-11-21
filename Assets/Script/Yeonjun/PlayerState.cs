using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [SerializeField] private float hp;
    [SerializeField] private float damColTime;
    [SerializeField] private float helColTime;
    private float currentTimeForDam;
    private float currentTimeForHel;

    private void Start()
    {
        currentTimeForDam = damColTime;
        currentTimeForHel = helColTime;
    }

    private void Update()
    {
        currentTimeForDam += Time.deltaTime;
        currentTimeForHel += Time.deltaTime;
        if(hp <= 0) Die();
    }

    private void Die()
    {
        //
    }

    public void Attacked(float dam)
    {
        if (currentTimeForDam < damColTime) return;
        hp -= dam;
        currentTimeForDam = 0f;
    }

    public void Heal(float healMount)
    {
        if (currentTimeForHel < helColTime) return;
        hp += healMount;
        currentTimeForHel = 0f;
    }
}
