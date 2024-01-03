using System;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    
    public bool red; //현재 클라이언트의 팀 가져오기
    public      bool        isEnd;

    private PlayerState playerState; // (red) 팀에 mover에 playerstate 가져오기
    private Slider      hpBar;
    
    private Attacker atk;
    private PlayerMove mov;

    private void Setting()
    {
        try
        {
            atk = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Attacker>();
            mov = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMove>();


            if (atk is not null)
            {
                red = atk.red;
            }
            else if (mov is not null)
            {
                red = mov.red;
            }
            
            playerState =
                red
                    ? GameObject.FindGameObjectWithTag("MoverPlayerRed").GetComponent<PlayerState>()
                    : GameObject.FindGameObjectWithTag("MoverPlayerBlue").GetComponent<PlayerState>();
        }
        catch (NullReferenceException)
        {
            // ignored
        }
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);   
        }
    }

    private void Start()
    {
        hpBar = GameObject.Find("HpBar").GetComponent<Slider>();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            FindObjectOfType<Title>().BattleAgein();
        }
        
        if (playerState)
        {
            hpBar.value = playerState.currentHp / playerState.maxHp;
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
        if ((winRed && red) || (!winRed && !red))
        {
            canvas.transform.Find("Dead").GetComponent<TextMeshProUGUI>().text = "승리!";
            canvas.enabled                                                     = true;
        }
        else
        {
            canvas.transform.Find("Dead").GetComponent<TextMeshProUGUI>().text = "패배!";
            canvas.enabled                                                     = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        isEnd = true;
    }

    public void Restart()
    {
        isEnd                                                    = false;
        FindObjectOfType<Title>().GetComponent<Canvas>().enabled = false;
        var movers = FindObjectsOfType<PlayerMove>();
        
        foreach (var mover in movers)
        {
            mover.ReSetup();
        }
    }
}
