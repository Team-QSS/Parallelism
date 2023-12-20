using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool red;

    private PlayerState playerState;
    private Slider      hpBar;

    private void Update()
    {
        if (playerState)
        {
            hpBar.value = playerState.currentHp;
        }
        // else
        // {
        //     var name = red ? "PlayerRed(Clone)" : "PlayerBlue(Clone)";
        //     playerState = GameObject.Find(name).GetComponentInChildren<PlayerState>();
        // }
    }

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
}
