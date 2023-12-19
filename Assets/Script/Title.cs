using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Title : MonoBehaviour
{
    public void Action()
    {
        SceneManager.LoadScene("Waiting Room");
        Destroy(gameObject);
    }
    
    public void Exit()
    {
        Application.Quit();
    }
}
