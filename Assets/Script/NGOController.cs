using System;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private GameObject moverRed;
    [SerializeField] private GameObject attackerRed;
    [SerializeField] private GameObject moverBlue;
    [SerializeField] private GameObject attackerBlue;

    //private Dictionary<string, uint> hashs = new() { {"PlayerRed", 2408260115}, {"PlayerBlue", 1853558016}, {"AttackerRed", 823301668},{"AttackerBlue", 3024824944} };
    
    [Header("controller")]
    [SerializeField] private NetworkController _networkController;
    private Dictionary<ulong, Team> clientTeams = new();
    
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
            
            OnClientConnectedCallback(NetworkManager.Singleton.LocalClient.ClientId);
            
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

    private async void UpdateClient(string status)
    {
        switch (status)
        {
            case "Connecting":
                await _networkController.UpdatePlayerDataAsync(new Dictionary<string, string> { { NetworkController.key_Userstatus, ((int)PlayerStatus.Connecting).ToString()} }) ;
                break;
            case "InGame":
                await _networkController.UpdatePlayerDataAsync(new Dictionary<string, string> { { NetworkController.key_Userstatus, ((int)PlayerStatus.InGame).ToString()} }) ;
                break;
            default:
                Debug.LogError(";.;");
                break;
        }
    }

    private ushort cnt;
    private void OnClientConnectedCallback(ulong clientid)
    {
        Debug.Log("connect " + clientid);

        ClientRpcParams clientRpcParams = new ClientRpcParams()
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientid }
            }
        };
        ConnectClientRpc(clientRpcParams);
    }

    [ClientRpc]
    private void ConnectClientRpc(ClientRpcParams clientRpcParams = default)
    {
        UpdateClient("InGame");
        TeamServerRpc(_networkController.m_LocalUser.Team.Value == Team.Red);
    }

    [ServerRpc (RequireOwnership = false)]
    private void TeamServerRpc(bool team, ServerRpcParams serverRpcParams = default)
    {
        var clientid = serverRpcParams.Receive.SenderClientId;
        clientTeams.Add(clientid,team ? Team.Red : Team.Blue);
        if (clientTeams.Count >= 4)
        {
            ObjSpawn();
        }
    }

    private void ObjSpawn()
    {
        var red = false;
        var blue = false;
        
        clientTeams = clientTeams.OrderBy(x => Random.Range(0,100)).ToDictionary(item => item.Key, item => item.Value);
        
        foreach (var (clientid, team) in clientTeams)
        {
            var isRedTeam = team == Team.Red;
            GameObject selectedPrefab = moverRed;
            
            if (isRedTeam)
            {
                if (red)
                {
                    selectedPrefab = moverRed;
                }
                else
                {
                    selectedPrefab = attackerRed;
                    attackerRed.GetComponent<Attacker>().moverTransform = moverRed.transform;
                    attackerRed.GetComponent<Attacker>().enabled = true;
                }
            }
            else
            {
                if (blue)
                {
                    selectedPrefab = moverBlue;
                }
                else
                {
                    selectedPrefab = attackerBlue;
                    attackerBlue.GetComponent<Attacker>().moverTransform = moverBlue.transform;
                    attackerBlue.GetComponent<Attacker>().enabled = true;
                }
            }
            
            var obj = Instantiate(selectedPrefab);
            obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientid);
            
            if (isRedTeam)
            {
                red = true;
            }
            else
            {
                blue = true;
            }
        }
        
    }

    private void OnClientDisconnectCallback(ulong clientid)
    {
        Debug.Log("disconnect " + clientid);
    }
}
