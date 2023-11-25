using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NGOController : NetworkBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    
    public void StartHost(Allocation allocation)
    {
        try
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("Yeonjun Scene",LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        
    }
    
    private bool connect;
    
    public async void StartClient(string joincode)
    {
        try
        {
            connect = true;
            
            var allocation = await RelayService.Instance.JoinAllocationAsync(joincode);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public bool IsConnected()
    {
        return NetworkManager.Singleton.IsConnectedClient || connect;
    }
}
