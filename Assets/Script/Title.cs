using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Title : MonoBehaviour
{
    public void Action()
    {
        Destroy(gameObject);
    }
    
    public void Exit()
    {
        Application.Quit();
    }
}
