using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Title : MonoBehaviour
{
    private NGOController _ngoController;

    private void Awake()
    {
        _ngoController = GameObject.Find("NGOController").GetComponent<NGOController>();
    }

    public void Action()
    {
        SceneManager.LoadScene("Waiting Room");
        Destroy(gameObject);
    }
    
    public void Exit()
    {
        Application.Quit();
    }

    public void BattleAgein()
    {
        var movers = FindObjectsOfType<PlayerMove>();
        var attackers = FindObjectsOfType<Attacker>();
        foreach (var mover in movers)
        {
            mover.Setup();
        }

        foreach (var attacker in attackers)
        {
            attacker.Setup();
        }
    }
}
