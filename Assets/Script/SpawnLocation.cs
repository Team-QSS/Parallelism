using System;
using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Object = UnityEngine.Object;

public class SpawnLocation : MonoBehaviour
{
    [SerializeField] private List<GameObject> _players;

    [SerializeField] private NetworkController _networkController;

    public void OnLobbyUpdated()
    {
        // var plrList = _networkController.GetPlayers();
        //
        // foreach (var plr in plrList)
        // {
        //     foreach (var (i,j) in plr.Data)
        //     {
        //         Debug.Log(i + " " + j.Value);
        //     }
        //
        //     Debug.Log(" ");
        // }
        //
        //  for (var i = 0; i < playerDatas.Count; i++)
        //  {
        //      PlayerDataObject data = playerDatas[i].Data;
        //      _players[i].SetData(data);
        //  }
    }

    public void OnPlayerDataChanged(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> obj)
    {
        OnLobbyUpdated();
    }
}
