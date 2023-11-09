using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QFSW.QC;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Random = UnityEngine.Random;

public class NetworkController : NetworkBehaviour
{
    public const string KEY_RELAY_JOIN_CODE = "joinCode";
    public const string GAMEMODE = "gameMode";

    public const string PLAYER_NAME = "playerName";
    public const string PLAYER_ID = "playerId";
    public const string PLAYER_READY = "playerReady";

    public Lobby joinedLobby;
    private ILobbyEvents joinedLobbyEvents;
    public static LobbyEventCallbacks callBacks;

    private float heartbeatTimer;
    private float lobbyUpdateTimer;

    private static string playerName = "player";
    private static string playerClientId = "-1";
    private static string playerReady = false.ToString();

    [SerializeField] private SpawnLocation _spawnLocation;
    
    private async void Awake()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions initializationOptions = new InitializationOptions();
            initializationOptions.SetProfile(Random.Range(0, 10000).ToString());

            await UnityServices.InitializeAsync(initializationOptions);
            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log("signin " + AuthenticationService.Instance.PlayerId + " " +
                          AuthenticationService.Instance.PlayerName);
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private bool IsLobbyHost()
    {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates();
    }

    private async void HandleLobbyHeartbeat()
    {
        if (IsLobbyHost())
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                const float heartbeatTimerMax = 15f;
                heartbeatTimer = heartbeatTimerMax;
                await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }
    }

    private async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby == null) return;
        lobbyUpdateTimer -= Time.deltaTime;
        if (lobbyUpdateTimer < 0f)
        {
            const float lobbyUpdateTimerMax = 1.1f;
            lobbyUpdateTimer = lobbyUpdateTimerMax;

            Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            joinedLobby = lobby;
        }
    }

    private async Task<string> GetRelayJoinCode(Allocation allocation)
    {
        try
        {
            var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            return relayJoinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return default;
        }
    }

    private async Task<JoinAllocation> joinRelay(string joinCode)
    {
        try
        {
            Debug.Log("join " + joinCode);
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            return joinAllocation;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            return default;
        }
    }

    [Command]
    public async void CreateLobby(string lobbyName, bool isPrivate)
    {
        try
        {
            const int maxPlayers = 4;
            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, new CreateLobbyOptions
            {
                IsPrivate = isPrivate
            });
            Allocation allocation = await AllocateRelay();
            var relayJoinCode = await GetRelayJoinCode(allocation);

            callBacks = new LobbyEventCallbacks();
            callBacks.PlayerDataChanged += _spawnLocation.OnPlayerDataChanged;
            callBacks.KickedFromLobby += OnKickedFromLobby;

            joinedLobbyEvents = await Lobbies.Instance.SubscribeToLobbyEventsAsync(joinedLobby.Id, callBacks);


            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            });

            await Task.Delay(1000);
            
            UpdatePlayer(playerName, playerReady);

            Debug.Log("create " + joinedLobby.Name + " " + joinedLobby.MaxPlayers + " " + joinedLobby.Id + " " +
                      joinedLobby.LobbyCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private void OnKickedFromLobby()
    {
        joinedLobbyEvents = null;
    }

    private async Task<Allocation> AllocateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            return allocation;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);

            return default;
        }
    }

    [Command]
    private void UpdatePlayer(string newPlayerName, string newPlayerReady)
    {
        playerName = newPlayerName;
        playerReady = newPlayerReady;
        LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId,
            new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { PLAYER_NAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                    { PLAYER_ID, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerClientId) },
                    { PLAYER_READY, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerReady) }
                }
            });

        _spawnLocation.OnLobbyUpdated();
    }

    [Command]
    public async void QuickJoinLobby()
    {
        try
        {
            if (joinedLobby != null) return;

            joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            Debug.Log("quick join " + joinedLobby);
            if (joinedLobby.Data == null)
            {
                KickPlayer();
                throw new Exception("jobby Data is null");
            }

            callBacks = new LobbyEventCallbacks();
            callBacks.PlayerDataChanged += _spawnLocation.OnPlayerDataChanged;
            callBacks.KickedFromLobby += OnKickedFromLobby;

            joinedLobbyEvents = await Lobbies.Instance.SubscribeToLobbyEventsAsync(joinedLobby.Id, callBacks);
            
            UpdatePlayer(playerName, playerReady);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    [Command]
    public async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                },
                Order = new List<QueryOrder>
                {
                    new(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            Debug.Log("lobby" + " " + queryResponse.Results.Count);
            foreach (var lobby in queryResponse.Results)
            {
                Debug.Log(lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    [Command]
    public async void JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            if (joinedLobby != null) return;

            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            Debug.Log("join w code " + lobbyCode);

            if (joinedLobby.Data == null)
            {
                KickPlayer();
                throw new Exception("jobby Data is null");
            }

            callBacks = new LobbyEventCallbacks();
            callBacks.PlayerDataChanged += _spawnLocation.OnPlayerDataChanged;
            callBacks.KickedFromLobby += OnKickedFromLobby;

            joinedLobbyEvents = await Lobbies.Instance.SubscribeToLobbyEventsAsync(joinedLobby.Id, callBacks);
            
            UpdatePlayer(playerName, playerReady);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    [Command]
    public async void DeleteLobby()
    {
        if (joinedLobby == null) return;
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
            joinedLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    public async void KickPlayer(string playerId = null)
    {
        if (joinedLobby == null) return;
        try
        {
            if (playerId == null)
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
            }

            joinedLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command(nameof(PrintPlayers))]
    private void PrintPlayersCMD()
    {
        PrintPlayers(joinedLobby);
    }

    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log("in lobby " + lobby.Name + " ");
        foreach (var player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data[PLAYER_NAME].Value + " " + player.Data[PLAYER_ID].Value + " " +
                      player.Data[PLAYER_READY].Value);
        }
    }

    public List<Player> GetPlayers()
    {
        return joinedLobby.Players;
    }
    
    [Command]
    private void PlayerReady(bool ready)
    {
        UpdatePlayer(playerName, ready.ToString());
    }
}

public static class LobbyConverters
    {
        const string key_RelayCode = nameof(LocalLobby.RelayCode);
        const string key_LobbyState = nameof(LocalLobby.LocalLobbyState);
        const string key_LobbyColor = nameof(LocalLobby.LocalLobbyColor);
        const string key_LastEdit = nameof(LocalLobby.LastUpdated);

        const string key_Displayname = nameof(LocalPlayer.DisplayName);
        const string key_Userstatus = nameof(LocalPlayer.UserStatus);

        public static Dictionary<string, string> LocalToRemoteLobbyData(LocalLobby lobby)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            data.Add(key_RelayCode, lobby.RelayCode.Value);
            data.Add(key_LobbyState, ((int)lobby.LocalLobbyState.Value).ToString());
            data.Add(key_LobbyColor, ((int)lobby.LocalLobbyColor.Value).ToString());
            data.Add(key_LastEdit, lobby.LastUpdated.Value.ToString());

            return data;
        }

        public static Dictionary<string, string> LocalToRemoteUserData(LocalPlayer user)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            if (user == null || string.IsNullOrEmpty(user.ID.Value))
                return data;
            data.Add(key_Displayname, user.DisplayName.Value);
            data.Add(key_Userstatus, ((int)user.UserStatus.Value).ToString());
            return data;
        }

        /// <summary>
        /// Create a new LocalLobby from the content of a retrieved lobby. Its data can be copied into an existing LocalLobby for use.
        /// </summary>
        public static void RemoteToLocal(Lobby remoteLobby, LocalLobby localLobby)
        {
            if (remoteLobby == null)
            {
                Debug.LogError("Remote lobby is null, cannot convert.");
                return;
            }

            if (localLobby == null)
            {
                Debug.LogError("Local Lobby is null, cannot convert");
                return;
            }

            localLobby.LobbyID.Value = remoteLobby.Id;
            localLobby.HostID.Value = remoteLobby.HostId;
            localLobby.LobbyName.Value = remoteLobby.Name;
            localLobby.LobbyCode.Value = remoteLobby.LobbyCode;
            localLobby.Private.Value = remoteLobby.IsPrivate;
            localLobby.AvailableSlots.Value = remoteLobby.AvailableSlots;
            localLobby.MaxPlayerCount.Value = remoteLobby.MaxPlayers;
            localLobby.LastUpdated.Value = remoteLobby.LastUpdated.ToFileTimeUtc();

            //Custom Lobby Data Conversions
            localLobby.RelayCode.Value = remoteLobby.Data?.ContainsKey(key_RelayCode) == true
                ? remoteLobby.Data[key_RelayCode].Value
                : localLobby.RelayCode.Value;
            localLobby.LocalLobbyState.Value = remoteLobby.Data?.ContainsKey(key_LobbyState) == true
                ? (LobbyState)int.Parse(remoteLobby.Data[key_LobbyState].Value)
                : LobbyState.Lobby;
            localLobby.LocalLobbyColor.Value = remoteLobby.Data?.ContainsKey(key_LobbyColor) == true
                ? (LobbyColor)int.Parse(remoteLobby.Data[key_LobbyColor].Value)
                : LobbyColor.None;

            //Custom User Data Conversions
            List<string> remotePlayerIDs = new List<string>();
            int index = 0;
            foreach (var player in remoteLobby.Players)
            {
                var id = player.Id;
                remotePlayerIDs.Add(id);
                var isHost = remoteLobby.HostId.Equals(player.Id);
                var displayName = player.Data?.ContainsKey(key_Displayname) == true
                    ? player.Data[key_Displayname].Value
                    : default;
                var userStatus = player.Data?.ContainsKey(key_Userstatus) == true
                    ? (PlayerStatus)int.Parse(player.Data[key_Userstatus].Value)
                    : PlayerStatus.Lobby;

                LocalPlayer localPlayer = localLobby.GetLocalPlayer(index);

                if (localPlayer == null)
                {
                    localPlayer = new LocalPlayer(id, index, isHost, displayName, userStatus);
                    localLobby.AddPlayer(index, localPlayer);
                }
                else
                {
                    localPlayer.ID.Value = id;
                    localPlayer.Index.Value = index;
                    localPlayer.IsHost.Value = isHost;
                    localPlayer.DisplayName.Value = displayName;
                    localPlayer.UserStatus.Value = userStatus;
                }

                index++;
            }
        }

        /// <summary>
        /// Create a list of new LocalLobbies from the result of a lobby list query.
        /// </summary>
        public static List<LocalLobby> QueryToLocalList(QueryResponse response)
        {
            List<LocalLobby> retLst = new List<LocalLobby>();
            foreach (var lobby in response.Results)
                retLst.Add(RemoteToNewLocal(lobby));
            return retLst;
        }

        //This might be heavy handed,
        static LocalLobby RemoteToNewLocal(Lobby lobby)
        {
            LocalLobby data = new LocalLobby();
            RemoteToLocal(lobby, data);
            return data;
        }
    }