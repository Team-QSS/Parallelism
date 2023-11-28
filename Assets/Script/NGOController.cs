using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class NGOController : NetworkBehaviour
{
    [Header("obj")]
    [SerializeField] private GameObject mover;
    [SerializeField] private GameObject attacker;
    [Header("controller")]
    [SerializeField] private NetworkController _networkController;
    
    private List<ulong> clients = new();
    private List<uint> objs = new() { 2408260115 ,823301668 };
    private NetworkVariable<bool> teamRed = new();
    private NetworkVariable<bool> teamBlue = new();

    private Team Team;
    
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    
    
    
    public void StartHost(Allocation allocation)
    {
        try
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.ConnectionApprovalCallback += (_, response) =>
            {
                response.CreatePlayerObject = false;
                response.Approved = true;
            };
            
            NetworkManager.Singleton.StartHost();
            
            teamRed.Value = Random.Range(0, 2) != 0;
            teamBlue.Value = Random.Range(0, 2) != 0;
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            
            NetworkManager.Singleton.SceneManager.LoadScene("Yeonjun Scene",LoadSceneMode.Single);
            
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        
    }
    
    public async void StartClient(string joincode)
    {
        try
        {
            await _networkController.UpdatePlayerDataAsync(new Dictionary<string, string> { { NetworkController.key_Userstatus, ((int)PlayerStatus.Connecting).ToString()} }) ;
            var allocation = await RelayService.Instance.JoinAllocationAsync(joincode);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.ConnectionApprovalCallback += (_, response) =>
            {
                response.CreatePlayerObject = false;
                response.Approved = true;
            };
            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    private async void OnClientConnectedCallback(ulong clientid)
    {
        await _networkController.UpdatePlayerDataAsync(new Dictionary<string, string> { { NetworkController.key_Userstatus, ((int)PlayerStatus.Connecting).ToString()} }) ;
        Debug.Log("connect " + clientid);
        clients.Add(clientid);
                
        Team = _networkController.m_LocalUser.Team.Value == Team.Red ? Team.Red : Team.Blue;

        GameObject playerPrefab;
        if (Team == Team.Red)
        {
            playerPrefab = teamRed.Value ? mover : attacker;
            teamRed.Value = !teamRed.Value;
        }
        else
        {
            playerPrefab = teamBlue.Value ? mover : attacker;
            teamBlue.Value = !teamBlue.Value;
        }
                
        var go = Instantiate(playerPrefab, Vector3.zero, quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(NetworkManager.Singleton.LocalClient.ClientId);
    }

    private void OnClientDisconnectCallback(ulong clientid)
    {
        Debug.Log("disconnect " + clientid);
        clients.Remove(clientid);
    }
}
