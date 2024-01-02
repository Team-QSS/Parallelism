using System;
using System.Collections;
using System.Collections.Generic;
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

    public void Totitle()
    {
        _ngoController.ToTitle();
    }
}
