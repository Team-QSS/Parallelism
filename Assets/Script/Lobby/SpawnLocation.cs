using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawnLocation : MonoBehaviour
{ 
    [SerializeField] private NetworkController _networkController;
    [SerializeField] private GameObject LobbyPlayerObj;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void OnPlayerChanged()
    {
        if (SceneManager.GetActiveScene().ToString() == "Yeonjun Scene")
        {
            Destroy(gameObject);
            return;
        }
        var players = _networkController.GetPlayers();
        Clear();
        foreach (var plr in players)
        {
            var obj = transform.Find("Player" + (plr.Index.Value + 1));
            var PlayerObj = Instantiate(LobbyPlayerObj, (obj.position + new Vector3Int(0,1,0)), quaternion.identity, obj);
            var ready = PlayerObj.transform.Find("Ready");
            var team = PlayerObj.transform.Find("Team");
            ready.GetComponent<Renderer>().material.color = 
                plr.UserStatus.Value == PlayerStatus.Ready ? Color.green : Color.red;
            
            team.GetComponent<Renderer>().material.color = plr.Team.Value switch
            {
                Team.Red => Color.red,
                Team.Blue => Color.blue,
                _ => Color.white
            };
        }
    }
    
    public void Clear()
    {
        foreach (Transform child in transform)
        {
            foreach (Transform c in child)
            {
                Destroy(c.gameObject);
            }
        }
    }
}
