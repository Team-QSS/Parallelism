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
    
    private Transform s1;
    private Transform s2;
    
    //private Dictionary<string, uint> hashs = new() { {"PlayerRed", 2408260115}, {"PlayerBlue", 1853558016}, {"AttackerRed", 823301668},{"AttackerBlue", 3024824944} };
    
    [Header("controller")]
    [SerializeField] private NetworkController _networkController;
    [SerializeField] private NetworkManager _networkManager;
    private Dictionary<ulong, Team> clientTeams = new();
    
    private Team Team;
    private bool spawned;
    
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
            
            NetworkManager.Singleton.SceneManager.LoadScene("Yeonjun Scene", LoadSceneMode.Single);
            
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
        if (clientTeams.Count >= 4 && !spawned)
        {
            ObjSpawn();
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "Yeonjun Scene" && clientTeams.Count >= 4 && !spawned)
        {
            ObjSpawn();
        }
    }

    private void ObjSpawn()
    {
        spawned = true;
        s1 = GameObject.Find("SpawnPoint1").transform;
        s2 = GameObject.Find("SpawnPoint2").transform;
        
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
                    selectedPrefab = Instantiate(attackerRed, s1);
                    var attacker = selectedPrefab.GetComponent<Attacker>();
                    attacker.enabled = true;
                    selectedPrefab.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientid, true);
                    Debug.Log(1);
                }
                else
                {
                    selectedPrefab = Instantiate(moverRed, s1);
                    selectedPrefab.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientid, true);
                    Debug.Log(2);
                    red = true;
                }
            }
            else
            {
                if (blue)
                {
                    selectedPrefab = Instantiate(attackerBlue, s2);
                    var attacker = selectedPrefab.GetComponent<Attacker>();
                    attacker.enabled = true;
                    selectedPrefab.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientid, true);
                    Debug.Log(3);
                }
                else
                {
                    selectedPrefab = Instantiate(moverBlue, s2);
                    selectedPrefab.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientid, true);
                    Debug.Log(4);
                    blue = true;
                }
            }
        }

        InheritanceClientRpc();
    }

    [ClientRpc]
    private void InheritanceClientRpc()
    {
        GameObject.FindGameObjectWithTag("AttackerPlayerRed").GetComponent<Attacker>().moverTransform =
            GameObject.FindGameObjectWithTag("MoverPlayerRed").transform;
        
        GameObject.FindGameObjectWithTag("AttackerPlayerBlue").GetComponent<Attacker>().moverTransform =
            GameObject.FindGameObjectWithTag("MoverPlayerBlue").transform;
    }
    
    private void OnClientDisconnectCallback(ulong clientid)
    {
        Debug.Log("disconnect " + clientid);
    }

    public async void ToTitle()
    {
        await _networkController.KickPlayer();
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Title");
        Destroy(_networkController.gameObject);
        Destroy(_networkManager.gameObject);
        Destroy(gameObject);
    }

    public void Restart()
    {
        Debug.Log("restart");
    }
}
