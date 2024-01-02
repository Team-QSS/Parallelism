using System;
using System.Collections;
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
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class NetworkController : MonoBehaviour
{
    public LocalPlayer m_LocalUser;
    public LocalLobby m_LocalLobby;
    
    //public const string GAMEMODE = "gameMode";

    public const string key_RelayCode = nameof(LocalLobby.RelayCode);
    public const string key_LobbyState = nameof(LocalLobby.LocalLobbyState);

    public const string key_Displayname = nameof(LocalPlayer.DisplayName);
    public const string key_Userstatus = nameof(LocalPlayer.UserStatus);
    public const string key_Team = nameof(LocalPlayer.Team);
    
    private Lobby m_CurrentLobby;
    private LobbyEventCallbacks m_LobbyEventCallbacks = new();
    
    public static LobbyEventCallbacks callBacks;

    private float heartbeatTimer;
    private float lobbyUpdateTimer;

    private ServiceRateLimiter m_QueryCooldown = new(1, 1f);
    private ServiceRateLimiter m_CreateCooldown = new(2, 6f);
    private ServiceRateLimiter m_JoinCooldown = new(2, 6f);
    private ServiceRateLimiter m_QuickJoinCooldown = new(1, 10f);
    private ServiceRateLimiter m_GetLobbyCooldown = new(1, 2f);
    private ServiceRateLimiter m_DeleteLobbyCooldown = new(2, 1f);
    private ServiceRateLimiter m_UpdateLobbyCooldown = new(5, 5f);
    private ServiceRateLimiter m_UpdatePlayerCooldown = new(5, 5f);
    private ServiceRateLimiter m_LeaveLobbyOrRemovePlayer = new(5, 1);
    private ServiceRateLimiter m_HeartBeatCooldown = new(5, 30);

    public bool IsExit;
    
    [SerializeField] private SpawnLocation _spawnLocation;
    [SerializeField] private NGOController _ngoController;
    [SerializeField] private UIController _uiController;
    
    private async void Awake()
    {
        DontDestroyOnLoad(gameObject);
        m_LocalUser = new LocalPlayer("", 0, false, "LocalPlayer");
        m_LocalLobby = new LocalLobby { LocalLobbyState = { Value = LobbyState.Lobby } };

        Application.wantsToQuit += Application_wantsToQuit;
        
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
        
        SmartJoinLobby();
    }
    
    private bool Application_wantsToQuit()
    {
        var canQuit = m_CurrentLobby == null;
        if (!canQuit)
        {
            StartCoroutine(LeaveBeforeQuit());
        }
        return canQuit;
    }

    private IEnumerator LeaveBeforeQuit()
    {
        var task = KickPlayer();
        yield return new WaitUntil(() => task.IsCompleted);
        Application.Quit();
    }

    private async void StartHeartBeat()
    {
        while (m_CurrentLobby != null)
        {
            if (IsExit)
            {
                break;
            }
            
            await HandleLobbyHeartbeat();
            await Task.Delay(8000);
        }
    }

    private async void GetLobby()
    {
        while (m_CurrentLobby != null)
        {
            if (IsExit)
            {
                break;
            }
            
            await HandleLobbyPollForUpdates();
        }
    }
    
    private bool IsLobbyHost()
    {
        return m_LocalLobby.LobbyName.Value != null && m_LocalLobby.HostID.Value == AuthenticationService.Instance.PlayerId;
    }

    private async Task HandleLobbyHeartbeat()
    {
        if (!IsLobbyHost())
        {
            return;
        }
        if (m_HeartBeatCooldown.IsCoolingDown)
        {
            return;
        }
        
        await m_HeartBeatCooldown.QueueUntilCooldown();
        
        await LobbyService.Instance.SendHeartbeatPingAsync(m_CurrentLobby.Id);
    }

    private async Task HandleLobbyPollForUpdates()
    {
        try
        {
            if (m_CurrentLobby == null)
            {
                return;
            }
            
            if (m_GetLobbyCooldown.IsCoolingDown)
            {
                return;
            }
            
            await m_GetLobbyCooldown.QueueUntilCooldown();
            
            Debug.Log("update!!");
            
            m_CurrentLobby = await LobbyService.Instance.GetLobbyAsync(m_CurrentLobby.Id);
            m_LocalLobby = new LocalLobby();
            LobbyConverters.RemoteToLocal(m_CurrentLobby,m_LocalLobby);
            
            Check();
            
            _spawnLocation.OnPlayerChanged();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
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
    
    private Dictionary<string, PlayerDataObject> CreateInitialPlayerData(LocalPlayer user)
    {
        var data = new Dictionary<string, PlayerDataObject>();

        var displayNameObject =
            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, user.DisplayName.Value);
        var UserstatusObject =
            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ((int)user.UserStatus.Value).ToString());
        var TeamObject =
            new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ((int)user.Team.Value).ToString());
        data.Add(key_Displayname, displayNameObject);
        data.Add(key_Userstatus, UserstatusObject);
        data.Add(key_Team, TeamObject);
        return data;
    }

    public void CreateLobbyFunc()
    {
        CreateLobby();
    }
    
    [Command]
    public async void CreateLobby(string lobbyName = "a", bool isPrivate = false)
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
            
            await UpdatePlayerDataAsync(new Dictionary<string, string> { { key_Userstatus, ((int)PlayerStatus.Lobby).ToString()} }) ;
            
            m_LocalUser.IsHost.ForceSet(true);
            
            await BindLobby();
            
            StartHeartBeat();
            GetLobby();
            
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
    
    public async Task UpdateLobbyDataAsync(Dictionary<string, string> data)
    {
        try
        {
            Debug.Log("update lobby");
            if (m_CurrentLobby == null)
            {
                return;
            }

            var dataCurr = m_CurrentLobby.Data ?? new Dictionary<string, DataObject>();

            var shouldLock = false;
            foreach (var (key, value) in data)
            {
                if (key == key_RelayCode)
                {
                    DataObject dataObj = new DataObject(DataObject.VisibilityOptions.Member, value);
                    dataCurr[key] = dataObj;
                }
                else
                {
                    DataObject dataObj = new DataObject(DataObject.VisibilityOptions.Public, value);
                    dataCurr[key] = dataObj;

                }
                if (key == key_LobbyState)
                {
                    Enum.TryParse(value, out LobbyState lobbyState);
                    shouldLock = lobbyState != LobbyState.Lobby;
                }
            }

            if (m_UpdateLobbyCooldown.TaskQueued)
            {
                return;
            }
            await m_UpdateLobbyCooldown.QueueUntilCooldown();

            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions { Data = dataCurr, IsLocked = shouldLock };
            m_CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(m_CurrentLobby.Id, updateOptions);
        
            LobbyConverters.RemoteToLocal(m_CurrentLobby, m_LocalLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
    
    public async Task UpdatePlayerDataAsync(Dictionary<string, string> data)
    {
        Debug.Log("update");
        if (m_CurrentLobby == null)
        {
            Debug.LogWarning("lobby is null");
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
            Debug.LogWarning("too many request");
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
        
        LobbyConverters.RemoteToLocal(m_CurrentLobby, m_LocalLobby);
        
        foreach (var plr in m_LocalLobby.LocalPlayers)
        {
            if (plr.ID.Value == AuthenticationService.Instance.PlayerId)
            {
                m_LocalUser.DisplayName.Value = plr.DisplayName.Value;
                m_LocalUser.ID.Value = plr.ID.Value;
                m_LocalUser.IsHost.Value = plr.IsHost.Value;
                m_LocalUser.UserStatus.Value = plr.UserStatus.Value;
                m_LocalUser.Team.Value = plr.Team.Value;
                m_LocalUser.Index.Value = plr.Index.Value;
                m_LocalUser.LastUpdated = plr.LastUpdated;
            }
        }
        
        _spawnLocation.OnPlayerChanged();
    }
    
    [Command]
    public void StartGame()
    {
        if (!IsLobbyHost())
        {
            return;
        }
        
        var readyCount = 0;
        
        foreach (var plr in m_LocalLobby.LocalPlayers)
        {
            if (plr.UserStatus.Value == PlayerStatus.Ready)
            {
                readyCount++;
            }
        }
        
        if (readyCount == 4)
        {
            Game();
        }
    }

    private async void Game()
    {
        Allocation allocation = await AllocateRelay();
        var relayJoinCode = await GetRelayJoinCode(allocation);
        m_LocalLobby.RelayCode.Value = relayJoinCode;
        
        m_LocalLobby.LocalLobbyState.Value = LobbyState.InGame;
        await UpdateLobbyDataAsync(LobbyConverters.LocalToRemoteLobbyData(m_LocalLobby));
        _ngoController.StartHost(allocation);
        
    }
    
    private async void Check()
    {
        if (m_LocalLobby.LocalLobbyState.Value == LobbyState.InGame && !IsLobbyHost() && m_LocalUser.UserStatus.Value != PlayerStatus.Connecting && m_LocalUser.UserStatus.Value != PlayerStatus.InGame)
        {
            _ngoController.StartClient(m_LocalLobby.RelayCode.Value);
        }

        if (m_LocalLobby.LocalLobbyState.Value == LobbyState.FastSetting)
        {
            if (m_LocalUser.Index.Value < 2)
            {
                await ChangeTeam("red", true);
            }
            else
            {
                await ChangeTeam("blue",true);
            }

            await PlayerReady();

            if (IsLobbyHost())
            {
                foreach (var plr in m_LocalLobby.LocalPlayers)
                {
                    if (plr.Team.Value == Team.None || plr.UserStatus.Value != PlayerStatus.Ready)
                    {
                        return;
                    }
                }

                await UpdateLobbyDataAsync(new Dictionary<string, string> { { key_LobbyState, ((int)LobbyState.Lobby).ToString() } });
            }
        }
        
        _uiController.ChangeText();
    }

    [Command]
    public async void SmartJoinLobby()
    {
        var cnt = await ListLobbies();
        if (cnt > 0)
        {
            Debug.Log("join");
            QuickJoinLobby();
        }
        else
        {
            Debug.Log("create");
            CreateLobby();
        }
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

            if (m_QuickJoinCooldown.IsCoolingDown)
            {
                return;
            }
            
#pragma warning disable CS4014
            m_QuickJoinCooldown.QueueUntilCooldown();
#pragma warning restore CS4014
            
            var joinRequest = new QuickJoinLobbyOptions
            {
                Player = new Player(id: AuthenticationService.Instance.PlayerId, data: CreateInitialPlayerData(m_LocalUser))
            };
            
            m_CurrentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(joinRequest);
            
            await UpdatePlayerDataAsync(new Dictionary<string, string> { { key_Userstatus, ((int)PlayerStatus.Lobby).ToString()} }) ;
            if (m_CurrentLobby == null)
            {
                await KickPlayer();
                throw new Exception("jobby Data is null");
            }
            
            m_LocalUser.IsHost.ForceSet(false);
            
            await BindLobby();
            
            GetLobby();
            
            Debug.Log("quick join " + m_CurrentLobby);
            
        }
        catch (LobbyServiceException e)
        {
            await KickPlayer();
            Debug.Log(e);
        }
        catch (Exception e)
        {
            await KickPlayer();
            Debug.Log(e);
        }
    }

    [Command]
    public async Task<int> ListLobbies()
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
            return queryResponse.Results.Count;
            
            /*Debug.Log("lobby" + " " + queryResponse.Results.Count);
            foreach (var lobby in queryResponse.Results)
            {
                Debug.Log(lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            }*/
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
            return default;
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

            if (m_JoinCooldown.IsCoolingDown)
            {
                return;
            }
            
            await m_JoinCooldown.QueueUntilCooldown();
            
            var joinRequest = new JoinLobbyByCodeOptions
            {
                Player = new Player(id: AuthenticationService.Instance.PlayerId, data: CreateInitialPlayerData(m_LocalUser))
            };
            
            m_CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinRequest);
            await UpdatePlayerDataAsync(new Dictionary<string, string> { { key_Userstatus, ((int)PlayerStatus.Lobby).ToString()} }) ;
            if (m_CurrentLobby == null)
            {
                await KickPlayer();
                throw new Exception("jobby Data is null");
            }
            
            m_LocalUser.IsHost.ForceSet(false);
            
            await BindLobby();
            
            GetLobby();
            
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
                _spawnLocation.Clear();
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(m_CurrentLobby.Id, playerId);
            }

            m_CurrentLobby = null;
            m_LocalLobby = null;
            m_LocalUser = null;
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
        Debug.Log("in lobby " + lobby.Name + " " + lobby.Players.Count + " " + lobby.HostId);
        foreach (var player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data[key_Displayname].Value + " " + (PlayerStatus)int.Parse(player.Data[key_Userstatus].Value) + " " + (Team)int.Parse(player.Data[key_Team].Value) + " " );
        }
    }

    [Command]
    private void PrintLocalLobby()
    {
        var l = m_LocalLobby;
        Debug.Log(l.LobbyName.Value);
        Debug.Log(l.LobbyID.Value);
        Debug.Log(l.LobbyCode.Value);
        Debug.Log(l.LocalLobbyState.Value);
        Debug.Log(l.RelayCode.Value);
        Debug.Log(l.AvailableSlots.Value);
        foreach (var plr in l.LocalPlayers)
        {
            Debug.Log(plr.DisplayName.Value);
            Debug.Log(plr.ID.Value);
            Debug.Log(plr.IsHost.Value);
            Debug.Log(plr.UserStatus.Value);
            Debug.Log(plr.Team.Value);
            Debug.Log(plr.Index.Value);
            Debug.Log(plr.LastUpdated);
            Debug.Log(" ");
        }
    }

    [Command]
    private void PrintLocalUser()
    {
        var l = m_LocalUser;
        Debug.Log(l.DisplayName.Value);
        Debug.Log(l.ID.Value);
        Debug.Log(l.IsHost.Value);
        Debug.Log(l.UserStatus.Value);
        Debug.Log(l.Team.Value);
        Debug.Log(l.Index.Value);
        Debug.Log(l.LastUpdated);
    }

    public List<LocalPlayer> GetPlayers()
    {
        return m_LocalLobby.LocalPlayers;
    }
    
    [Command]
    private async Task PlayerReady(bool ready = true, bool withoutUpdate = false)
    {
        if (m_CurrentLobby == null)
        {
            return;
        }

        m_LocalUser.UserStatus.Value = ready ? PlayerStatus.Ready : PlayerStatus.Lobby;
        
        if (withoutUpdate)
        {
            return;
        }
        
        await UpdatePlayerDataAsync(LobbyConverters.LocalToRemoteUserData(m_LocalUser)) ;
    }

    public async void ToggleChangeTeam()
    {
        m_LocalUser.Team.Value = m_LocalUser.Team.Value is Team.None or Team.Blue ? Team.Red : Team.Blue;
        await UpdatePlayerDataAsync(LobbyConverters.LocalToRemoteUserData(m_LocalUser));
    }
    
    public async void ToggleReady()
    {
        m_LocalUser.UserStatus.Value = m_LocalUser.UserStatus.Value is PlayerStatus.Lobby ? PlayerStatus.Ready : PlayerStatus.Lobby;
        await UpdatePlayerDataAsync(LobbyConverters.LocalToRemoteUserData(m_LocalUser));
    }

    public async void BtnKick()
    {
        await KickPlayer();
        SceneManager.LoadScene("Title");
    }
    
    [Command]
    private async Task ChangeTeam(string team = "red", bool withoutUpdate = false)
    {
        if (m_CurrentLobby == null)
        {
            return;
        }
        
        m_LocalUser.Team.Value = team switch
        {
            "red" => Team.Red,
            "blue" => Team.Blue,
            _ => Team.None
        };

        if (!withoutUpdate)
        { 
            await UpdatePlayerDataAsync(LobbyConverters.LocalToRemoteUserData(m_LocalUser));   
        }
    }

    [Command]
    public async void FastSetting()
    {
        try
        {
            await UpdateLobbyDataAsync(new Dictionary<string, string> { { key_LobbyState, ((int)LobbyState.FastSetting).ToString()} });
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        
    }
    
    public async Task BindLocalLobbyToRemote(string lobbyID, LocalLobby localLobby)
        {
            m_LobbyEventCallbacks.LobbyDeleted += async () =>
            {
                await KickPlayer();
            };

            m_LobbyEventCallbacks.DataChanged += changes =>
            {
                Debug.Log("data changed");
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
                    }
                }
            };

            m_LobbyEventCallbacks.DataAdded += changes =>
            {
                Debug.Log("data added");
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
                    }
                }
            };

            m_LobbyEventCallbacks.DataRemoved += changes =>
            {
                Debug.Log("data removed");
                foreach (var change in changes)
                {
                    var changedKey = change.Key;
                    if (changedKey == key_RelayCode)
                        localLobby.RelayCode.Value = "";
                }
            };

            m_LobbyEventCallbacks.PlayerLeft += players =>
            {
                Debug.Log("player left");
                foreach (var leftPlayerIndex in players)
                {
                    localLobby.RemovePlayer(leftPlayerIndex);
                }

                int[] ints = { -2, -2, -2, -2 };

                foreach (var plr in localLobby.LocalPlayers)
                {
                    ints[plr.Index.Value] = plr.Index.Value;
                }
                
                Debug.Log(ints[0] + " " + ints[1] + " "+ ints[2] + " " + ints[3]);
                for (var i = 1; i < ints.Length; i++)
                {
                    if (ints[i] - 1 > ints[i - 1])
                    {
                        ints[i - 1] = ints[i] - 1;
                        ints[i] = -2;
                    }
                    Debug.Log(ints[0] + " " + ints[1] + " "+ ints[2] + " " + ints[3]);
                }
                
                for (var i = 0; i < localLobby.LocalPlayers.Count; i++)
                {
                    if (ints[i] == -2)
                    {
                        break;
                    }
                    localLobby.LocalPlayers[i].Index.Value = ints[i];
                }
                
                _spawnLocation.OnPlayerChanged();
            };

            m_LobbyEventCallbacks.PlayerJoined += players =>
            {
                Debug.Log("player joined");
                foreach (var playerChanges in players)
                {
                    Player joinedPlayer = playerChanges.Player;

                    var id = joinedPlayer.Id;
                    var index = playerChanges.PlayerIndex;
                    var isHost = localLobby.HostID.Value == id;

                    var newPlayer = new LocalPlayer(id, index, isHost,status:PlayerStatus.Lobby);
                    
                    Debug.Log("joined player " + joinedPlayer.Data[key_Displayname].Value);
                    
                    foreach (var (key, dataObject) in joinedPlayer.Data)
                    {
                        ParseCustomPlayerData(newPlayer, key, dataObject.Value);
                    }

                    localLobby.AddPlayer(index, newPlayer);
                }
                
                List<string> list = new();
                foreach (var plr in localLobby.LocalPlayers)
                {
                    if (list.Contains(plr.ID.Value))
                    {
                        Debug.LogWarning("id error occured! please restart game");
                    }
                    list.Add(plr.ID.Value);
                }
                
                _spawnLocation.OnPlayerChanged();
            };

            m_LobbyEventCallbacks.PlayerDataChanged += changes =>
            {
                Debug.Log("player data changed");
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
                
                _spawnLocation.OnPlayerChanged();
            };

            m_LobbyEventCallbacks.PlayerDataAdded += changes =>
            {
                Debug.Log("player data added");
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
                Debug.Log("player data removed");
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
                Debug.Log("lobby changed");
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
                _spawnLocation.Clear();
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
            case key_Team:
                player.Team.Value = (Team)int.Parse(playerDataValue);
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
        public ServiceRateLimiter(int callTimes, float coolDown, int pingBuffer = 200)
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