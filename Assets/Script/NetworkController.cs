using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QFSW.QC;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Random = UnityEngine.Random;

public class NetworkController : MonoBehaviour
{
    private LocalPlayer m_LocalUser;
    private LocalLobby m_LocalLobby;
    
    public const string KEY_RELAY_JOIN_CODE = "joinCode";
    public const string GAMEMODE = "gameMode";

    private const string key_RelayCode = nameof(LocalLobby.RelayCode);
    private const string key_LobbyState = nameof(LocalLobby.LocalLobbyState);
    private const string key_LobbyColor = nameof(LocalLobby.LocalLobbyColor);

    private const string key_Displayname = nameof(LocalPlayer.DisplayName);
    private const string key_Userstatus = nameof(LocalPlayer.UserStatus);
    
    private Lobby m_CurrentLobby;
    private LobbyEventCallbacks m_LobbyEventCallbacks = new();
    
    public static LobbyEventCallbacks callBacks;

    private float heartbeatTimer;
    private float lobbyUpdateTimer;

    private ServiceRateLimiter m_QueryCooldown = new(1, 1f);
    private ServiceRateLimiter m_CreateCooldown = new(2, 6f);
    private ServiceRateLimiter m_JoinCooldown = new(2, 6f);
    private ServiceRateLimiter m_QuickJoinCooldown = new(1, 10f);
    private ServiceRateLimiter m_GetLobbyCooldown = new(1, 1f);
    private ServiceRateLimiter m_DeleteLobbyCooldown = new(2, 1f);
    private ServiceRateLimiter m_UpdateLobbyCooldown = new(5, 5f);
    private ServiceRateLimiter m_UpdatePlayerCooldown = new(5, 5f);
    private ServiceRateLimiter m_LeaveLobbyOrRemovePlayer = new(5, 1);
    private ServiceRateLimiter m_HeartBeatCooldown = new(5, 30);

    
    [SerializeField] private SpawnLocation _spawnLocation;
    
    private async void Awake()
    {
        m_LocalUser = new LocalPlayer("", 0, false, "LocalPlayer");
        m_LocalLobby = new LocalLobby { LocalLobbyState = { Value = LobbyState.Lobby } };
        
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
        return m_LocalLobby.LobbyName.Value != null && m_LocalLobby.HostID.Value == AuthenticationService.Instance.PlayerId;
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
                await LobbyService.Instance.SendHeartbeatPingAsync(m_LocalLobby.LobbyID.Value);
            }
        }
    }

    private async void HandleLobbyPollForUpdates()
    {
        if (m_LocalUser.UserStatus.Value == PlayerStatus.None) return;
        lobbyUpdateTimer -= Time.deltaTime;
        if (lobbyUpdateTimer < 0f)
        {
            const float lobbyUpdateTimerMax = 1.1f;
            lobbyUpdateTimer = lobbyUpdateTimerMax;

            m_CurrentLobby = await LobbyService.Instance.GetLobbyAsync(m_LocalLobby.LobbyID.Value);
            
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

    private Dictionary<string, PlayerDataObject> CreateInitialPlayerData(LocalPlayer user)
    {
        var data = new Dictionary<string, PlayerDataObject>();

        var displayNameObject =
            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, user.DisplayName.Value);
        var UserstatusObject =
            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ((int)user.UserStatus.Value).ToString());
        data.Add(key_Displayname, displayNameObject);
        data.Add(key_Userstatus, UserstatusObject);
        return data;
    }
    
    [Command]
    public async void CreateLobby(string lobbyName, bool isPrivate)
    {
        try
        {
            if (m_CreateCooldown.IsCoolingDown)
            {
                Debug.LogWarning("Create Lobby hit the rate limit.");
                return;
            }
            
            await m_CreateCooldown.QueueUntilCooldown();
            
            const int maxPlayers = 4;
            m_CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = new Player(id: AuthenticationService.Instance.PlayerId, data: CreateInitialPlayerData(m_LocalUser))
                
            });
            LobbyConverters.RemoteToLocal(m_CurrentLobby, m_LocalLobby);
            m_LocalUser.IsHost.Value = true;
            await BindLobby();
            Debug.Log("create " + m_LocalLobby.LobbyName.Value + " " + m_LocalLobby.MaxPlayerCount.Value + " " + m_LocalLobby.LobbyID.Value + " " +
                      m_LocalLobby.LobbyCode.Value);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
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

    private async void SendLocalLobbyData()
    {
        await UpdateLobbyDataAsync(LobbyConverters.LocalToRemoteLobbyData(m_LocalLobby));
    }

    private async void SendLocalUserData()
    {
        await UpdatePlayerDataAsync(LobbyConverters.LocalToRemoteUserData(m_LocalUser));
    }
    
    public async Task UpdateLobbyDataAsync(Dictionary<string, string> data)
    {
        if (m_CurrentLobby == null)
            return;

        Dictionary<string, DataObject> dataCurr = m_CurrentLobby.Data ?? new Dictionary<string, DataObject>();

        var shouldLock = false;
        foreach (var dataNew in data)
        {
            DataObject.IndexOptions index = dataNew.Key == "LocalLobbyColor" ? DataObject.IndexOptions.N1 : 0;
            DataObject dataObj = new DataObject(DataObject.VisibilityOptions.Public, dataNew.Value, index);
            dataCurr[dataNew.Key] = dataObj;

            if (dataNew.Key == "LocalLobbyState")
            {
                Enum.TryParse(dataNew.Value, out LobbyState lobbyState);
                shouldLock = lobbyState != LobbyState.Lobby;
            }
        }

        if (m_UpdateLobbyCooldown.TaskQueued)
            return;
        await m_UpdateLobbyCooldown.QueueUntilCooldown();

        UpdateLobbyOptions updateOptions = new UpdateLobbyOptions { Data = dataCurr, IsLocked = shouldLock };
        m_CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(m_CurrentLobby.Id, updateOptions);
    }
    
    public async Task UpdatePlayerDataAsync(Dictionary<string, string> data)
    {
        Debug.Log("update");
        if (m_CurrentLobby == null)
        {
            Debug.LogError("lobby is null");
            return;
        }

        var playerId = AuthenticationService.Instance.PlayerId;
        var dataCurr = new Dictionary<string, PlayerDataObject>();
        foreach (var (key, value) in data)
        {
            PlayerDataObject dataObj = new PlayerDataObject(visibility: PlayerDataObject.VisibilityOptions.Member, value: value);
            dataCurr[key] = dataObj;
        }

        if (m_UpdatePlayerCooldown.TaskQueued)
        {
            Debug.LogError("why");
            return;
        }
        await m_UpdatePlayerCooldown.QueueUntilCooldown();

        UpdatePlayerOptions updateOptions = new UpdatePlayerOptions
        {
            Data = dataCurr,
            AllocationId = null,
            ConnectionInfo = null
        };
        m_CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(m_CurrentLobby.Id, playerId, updateOptions);
    }
    
    [Command]
    public async void QuickJoinLobby()
    {
        try
        {
            if (m_CurrentLobby != null)
            {
                Debug.LogError("lobby is not null");
                return;
            }
            
            await m_QuickJoinCooldown.QueueUntilCooldown();
            
            var joinRequest = new QuickJoinLobbyOptions
            {
                Player = new Player(id: AuthenticationService.Instance.PlayerId, data: CreateInitialPlayerData(m_LocalUser))
            };
            
            m_CurrentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(joinRequest);
            
            if (m_CurrentLobby == null)
            {
                await KickPlayer();
                throw new Exception("jobby Data is null");
            }
            
            LobbyConverters.RemoteToLocal(m_CurrentLobby, m_LocalLobby);
            m_LocalUser.IsHost.ForceSet(false);
            await BindLobby();
            
            Debug.Log("quick join " + m_CurrentLobby);
            
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
            if (m_CurrentLobby != null)
            {
                Debug.LogError("lobby is not null");
                return;
            }

            await m_JoinCooldown.QueueUntilCooldown();
            
            var joinRequest = new JoinLobbyByCodeOptions
            {
                Player = new Player(id: AuthenticationService.Instance.PlayerId, data: CreateInitialPlayerData(m_LocalUser))
            };
            
            m_CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinRequest);
            
            if (m_CurrentLobby == null)
            {
                await KickPlayer();
                throw new Exception("jobby Data is null");
            }
            
            LobbyConverters.RemoteToLocal(m_CurrentLobby, m_LocalLobby);
            m_LocalUser.IsHost.ForceSet(false);
            await BindLobby();
            
            Debug.Log("join w code " + lobbyCode);
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
        if (m_CurrentLobby == null) return;
        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(m_CurrentLobby.Id);
            m_CurrentLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command]
    public async Task KickPlayer(string playerId = null)
    {
        if (m_CurrentLobby == null) return;
        try
        {
            if (playerId == null)
            {
                await LobbyService.Instance.RemovePlayerAsync(m_CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(m_CurrentLobby.Id, playerId);
            }

            m_CurrentLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    [Command(nameof(PrintPlayers))]
    private void PrintPlayersCMD()
    {
        PrintPlayers(m_CurrentLobby);
    }

    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log("in lobby " + lobby.Name + " " + lobby.Players.Count);
        foreach (var player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data[key_Displayname].Value + " " + player.Data[key_Userstatus].Value);
        }
    }

    [Command]
    private void PrintLocal()
    {
        var l = m_LocalLobby;
        Debug.Log(l.LobbyName.Value);
        Debug.Log(l.LobbyID.Value);
        Debug.Log(l.LobbyCode.Value);
        Debug.Log(l.LocalLobbyState.Value);
        Debug.Log(l.LocalLobbyColor.Value);
        Debug.Log(l.RelayCode.Value);
        Debug.Log(l.AvailableSlots.Value);
        Debug.Log(l.LocalPlayers); 
    }

    [Command]
    private void PrintLocalUser()
    {
        var l = m_LocalUser;
        Debug.Log(l.DisplayName.Value);
        Debug.Log(l.ID.Value);
        Debug.Log(l.IsHost.Value);
        Debug.Log(l.UserStatus.Value);
        Debug.Log(l.Index.Value);
    }

    public List<LocalPlayer> GetPlayers()
    {
        return m_LocalLobby.LocalPlayers;
    }
    
    [Command]
    private async Task PlayerReady(bool ready = true)
    {
        if (m_CurrentLobby == null) return;
        await UpdatePlayerDataAsync(new Dictionary<string, string> { { key_Userstatus, ready ? ((int)PlayerStatus.Ready).ToString() : ((int)PlayerStatus.Lobby).ToString()} }) ;
    }
    
    public async Task BindLocalLobbyToRemote(string lobbyID, LocalLobby localLobby)
        {
            m_LobbyEventCallbacks.LobbyDeleted += async () =>
            {
                await KickPlayer();
            };

            m_LobbyEventCallbacks.DataChanged += changes =>
            {
                foreach (var (changedKey, changedValue) in changes)
                {
                    switch (changedKey)
                    {
                        case key_RelayCode:
                            localLobby.RelayCode.Value = changedValue.Value.Value;
                            break;
                        case key_LobbyState:
                            localLobby.LocalLobbyState.Value = (LobbyState)int.Parse(changedValue.Value.Value);
                            break;
                        case key_LobbyColor:
                            localLobby.LocalLobbyColor.Value = (LobbyColor)int.Parse(changedValue.Value.Value);
                            break;
                    }
                }
            };

            m_LobbyEventCallbacks.DataAdded += changes =>
            {
                foreach (var (changedKey, changedValue) in changes)
                {
                    switch (changedKey)
                    {
                        case key_RelayCode:
                            localLobby.RelayCode.Value = changedValue.Value.Value;
                            break;
                        case key_LobbyState:
                            localLobby.LocalLobbyState.Value = (LobbyState)int.Parse(changedValue.Value.Value);
                            break;
                        case key_LobbyColor:
                            localLobby.LocalLobbyColor.Value = (LobbyColor)int.Parse(changedValue.Value.Value);
                            break;
                    }
                }
            };

            m_LobbyEventCallbacks.DataRemoved += changes =>
            {
                foreach (var change in changes)
                {
                    var changedKey = change.Key;
                    if (changedKey == key_RelayCode)
                        localLobby.RelayCode.Value = "";
                }
            };

            m_LobbyEventCallbacks.PlayerLeft += players =>
            {
                foreach (var leftPlayerIndex in players)
                {
                    localLobby.RemovePlayer(leftPlayerIndex);
                }
            };

            m_LobbyEventCallbacks.PlayerJoined += players =>
            {
                foreach (var playerChanges in players)
                {
                    Player joinedPlayer = playerChanges.Player;

                    var id = joinedPlayer.Id;
                    var index = playerChanges.PlayerIndex;
                    var isHost = localLobby.HostID.Value == id;

                    var newPlayer = new LocalPlayer(id, index, isHost);
                    
                    Debug.Log("data " + joinedPlayer.Data[key_Displayname].Value);
                    
                    foreach (var (key, dataObject) in joinedPlayer.Data)
                    {
                        ParseCustomPlayerData(newPlayer, key, dataObject.Value);
                    }

                    localLobby.AddPlayer(index, newPlayer);
                }
            };

            m_LobbyEventCallbacks.PlayerDataChanged += changes =>
            {
                foreach (var (playerIndex, playerChanges) in changes)
                {
                    var localPlayer = localLobby.GetLocalPlayer(playerIndex);
                    if (localPlayer == null)
                        continue;

                    foreach (var (key, changedValue) in playerChanges)
                    {
                        var playerDataObject = changedValue.Value;
                        ParseCustomPlayerData(localPlayer, key, playerDataObject.Value);
                    }
                }
            };

            m_LobbyEventCallbacks.PlayerDataAdded += changes =>
            {
                foreach (var (playerIndex, playerChanges) in changes)
                {
                    var localPlayer = localLobby.GetLocalPlayer(playerIndex);
                    if (localPlayer == null)
                        continue;

                    foreach (var (key, changedValue) in playerChanges)
                    {
                        var playerDataObject = changedValue.Value;
                        ParseCustomPlayerData(localPlayer, key, playerDataObject.Value);
                    }
                }
            };

            m_LobbyEventCallbacks.PlayerDataRemoved += changes =>
            {
                foreach (var (playerIndex, playerChanges) in changes)
                {
                    var localPlayer = localLobby.GetLocalPlayer(playerIndex);
                    if (localPlayer == null)
                        continue;

                    if (playerChanges == null)
                        continue;

                    foreach (var playerChange in playerChanges.Values)
                    {
                        Debug.LogWarning(playerChange + "This Sample does not remove Player Values currently.");
                    }
                }
            };

            m_LobbyEventCallbacks.LobbyChanged += changes =>
            {
                if (changes.Name.Changed)
                    localLobby.LobbyName.Value = changes.Name.Value;
                if (changes.HostId.Changed)
                    localLobby.HostID.Value = changes.HostId.Value;
                if (changes.IsPrivate.Changed)
                    localLobby.Private.Value = changes.IsPrivate.Value;
                if (changes.IsLocked.Changed)
                    localLobby.Locked.Value = changes.IsLocked.Value;
                if (changes.AvailableSlots.Changed)
                    localLobby.AvailableSlots.Value = changes.AvailableSlots.Value;
                if (changes.MaxPlayers.Changed)
                    localLobby.MaxPlayerCount.Value = changes.MaxPlayers.Value;

                if (changes.LastUpdated.Changed)
                    localLobby.LastUpdated.Value = changes.LastUpdated.Value.ToFileTimeUtc();

                //Custom

                if (changes.PlayerData.Changed)
                    PlayerDataChanged();
                return;

                void PlayerDataChanged()
                {
                    foreach (var (playerIndex, playerChanges) in changes.PlayerData.Value)
                    {
                        var localPlayer = localLobby.GetLocalPlayer(playerIndex);
                        if (localPlayer == null)
                            continue;
                        if (playerChanges.ConnectionInfoChanged.Changed)
                        {
                            var connectionInfo = playerChanges.ConnectionInfoChanged.Value;
                            Debug.Log(
                                $"ConnectionInfo for player {playerIndex} changed to {connectionInfo}");
                        }

                        if (playerChanges.LastUpdatedChanged.Changed) { }
                    }
                }
            };

            m_LobbyEventCallbacks.LobbyEventConnectionStateChanged += lobbyEventConnectionState =>
            {
                Debug.Log($"Lobby ConnectionState Changed to {lobbyEventConnectionState}");
            };

            m_LobbyEventCallbacks.KickedFromLobby += () =>
            {
                Debug.Log("Left Lobby");
                Dispose();
            };
            await LobbyService.Instance.SubscribeToLobbyEventsAsync(lobbyID, m_LobbyEventCallbacks);
        }

    private void ParseCustomPlayerData(LocalPlayer player, string dataKey, string playerDataValue)
    {
        switch (dataKey)
        {
            case key_Userstatus:
                player.UserStatus.Value = (PlayerStatus)int.Parse(playerDataValue);
                break;
            case key_Displayname:
                player.DisplayName.Value = playerDataValue;
                break;
        }
    }
    
    public void Dispose()
    {
        m_CurrentLobby = null;
        m_LobbyEventCallbacks = new LobbyEventCallbacks();
    }

    private async Task BindLobby()
    {
        await BindLocalLobbyToRemote(m_LocalLobby.LobbyID.Value, m_LocalLobby);
    }
    
    public class ServiceRateLimiter
    {
        public Action<bool> onCooldownChange;
        public readonly int coolDownMS;
        public bool TaskQueued { get; private set; }

        private readonly int m_ServiceCallTimes;
        private bool m_CoolingDown;
        private int m_TaskCounter;

        //(If you're still getting rate limit errors, try increasing the pingBuffer)
        public ServiceRateLimiter(int callTimes, float coolDown, int pingBuffer = 100)
        {
            m_ServiceCallTimes = callTimes;
            m_TaskCounter = m_ServiceCallTimes;
            coolDownMS =
                Mathf.CeilToInt(coolDown * 1000) +
                pingBuffer;
        }

        public async Task QueueUntilCooldown()
        {
            if (!m_CoolingDown)
            {
#pragma warning disable 4014
                ParallelCooldownAsync();
#pragma warning restore 4014
            }

            m_TaskCounter--;

            if (m_TaskCounter > 0)
            {
                return;
            }

            if (!TaskQueued)
                TaskQueued = true;
            else
                return;

            while (m_CoolingDown)
            {
                await Task.Delay(10);
            }
        }

        private async Task ParallelCooldownAsync()
        {
            IsCoolingDown = true;
            await Task.Delay(coolDownMS);
            IsCoolingDown = false;
            TaskQueued = false;
            m_TaskCounter = m_ServiceCallTimes;
        }

        public bool IsCoolingDown
        {
            get => m_CoolingDown;
            private set
            {
                if (m_CoolingDown != value)
                {
                    m_CoolingDown = value;
                    onCooldownChange?.Invoke(m_CoolingDown);
                }
            }
        }
    }
    
}