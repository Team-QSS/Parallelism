using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HpBar : MonoBehaviour
{
    [SerializeField] private PlayerState playerState;
    
    private Slider hpBar;

    private void Start()
    {
        hpBar = GetComponent<Slider>();
        hpBar.maxValue = playerState.maxHp;
    }

    private void Update()
    {
        hpBar.value = playerState.currentHp;
    }
}
