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
        hpBar = GetComponentInChildren<Slider>();
        
        hpBar.maxValue = playerState.maxHp;
    }

    private void Update()
    {
        if (hpBar)
        {
            
        }
        else
        {
            hpBar = GetComponent<Slider>();
        }
    }
}
