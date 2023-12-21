using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class Delay : MonoBehaviour
{
    public async void ClickDelay()
    {
        GetComponent<Button>().interactable = false;
        await Task.Delay(3000);
        GetComponent<Button>().interactable = true;
    }
}
