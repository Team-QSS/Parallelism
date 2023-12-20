using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public bool red; //현재 클라이언트의 팀 가져오기

    private PlayerState playerState; // (red) 팀에 mover에 playerstate 가져오기
    private Slider      hpBar;

    private Attacker atk = null;
    private PlayerMove mov = null;

    private void Setting()
    {
        try
        {
            atk = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Attacker>();
            mov = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMove>();
        }
        catch (Exception e)
        {
            // ignored
        }
        if (atk is not null)
        {
            red = atk.red;
        }
        else if (mov is not null)
        {
            red = mov.red;
        }
        else
        {
            Debug.LogWarning("err");
        }

        playerState = red ? GameObject.FindGameObjectWithTag("MoverPlayerRed").GetComponent<PlayerState>() : GameObject.FindGameObjectWithTag("MoverPlayerBlue").GetComponent<PlayerState>();
    }
    
    private void Update()
    {
        if (playerState)
        {
            hpBar.value = playerState.currentHp;
        }
        else
        {
            Setting();
        }
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
