using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

[Flags] // Some UI elements will want to specify multiple states in which to be active, so this is Flags.
public enum LobbyState
{
    Lobby = 1,
    FastSetting = 2,
    InGame = 4
}


/// <summary>
/// A local wrapper around a lobby's remote data, with additional functionality for providing that data to UI elements and tracking local player objects.
/// (The way that the Lobby service handles its data doesn't necessarily match our needs, so we need to map from that to this LocalLobby for use in the sample code.)
/// </summary>
[Serializable]
public class LocalLobby
{
    public Action<LocalPlayer> onUserJoined;

    public Action<int> onUserLeft;

    public Action<int> onUserReadyChange;

    public CallbackValue<string> LobbyID = new();

    public CallbackValue<string> LobbyCode = new();

    public CallbackValue<string> RelayCode = new();

    public CallbackValue<string> LobbyName = new();

    public CallbackValue<string> HostID = new();

    public CallbackValue<LobbyState> LocalLobbyState = new();

    public CallbackValue<bool> Locked = new();

    public CallbackValue<bool> Private = new();

    public CallbackValue<int> AvailableSlots = new();

    public CallbackValue<int> MaxPlayerCount = new();

    public CallbackValue<long> LastUpdated = new();

    public int PlayerCount => LocalPlayers.Count;

    public List<LocalPlayer> LocalPlayers { get; } = new();

    public void ResetLobby()
    {
        LocalPlayers.Clear();

        LobbyName.Value = "";
        LobbyID.Value = "";
        LobbyCode.Value = "";
        Locked.Value = false;
        Private.Value = false;
        AvailableSlots.Value = 4;
        MaxPlayerCount.Value = 4;
        onUserJoined = null;
        onUserLeft = null;
    }

    public LocalLobby()
    {
        LastUpdated.Value = DateTime.Now.ToFileTimeUtc();
        HostID.onChanged += OnHostChanged;
    }

    ~LocalLobby()
    {
        HostID.onChanged -= OnHostChanged;
    }

    public LocalPlayer GetLocalPlayer(int index)
    {
        return PlayerCount > index ? LocalPlayers[index] : null;
    }

    private void OnHostChanged(string newHostId)
    {
        //Debug.Log("host change");
        foreach(var player in LocalPlayers)
        {
            player.IsHost.Value = player.ID.Value == newHostId;
        }
    }
    
    public void AddPlayer(int index, LocalPlayer user)
    {
        LocalPlayers.Insert(index, user);
        user.UserStatus.onChanged += OnUserChangedStatus;
        onUserJoined?.Invoke(user);
        //Debug.Log($"Added User: {user.DisplayName.Value} - {user.ID.Value} to slot {index + 1}/{PlayerCount}");
    }

    public void RemovePlayer(int playerIndex)
    {
        LocalPlayers[playerIndex].UserStatus.onChanged -= OnUserChangedStatus;
        LocalPlayers.RemoveAt(playerIndex);
        onUserLeft?.Invoke(playerIndex);
    }

    void OnUserChangedStatus(PlayerStatus status)
    {
        var readyCount = 0;
        foreach (var player in LocalPlayers)
        {
            if (player.UserStatus.Value == PlayerStatus.Ready)
                readyCount++;
        }

        onUserReadyChange?.Invoke(readyCount);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder("Lobby : ");
        sb.AppendLine(LobbyName.Value);
        sb.Append("ID: ");
        sb.AppendLine(LobbyID.Value);
        sb.Append("Code: ");
        sb.AppendLine(LobbyCode.Value);
        sb.Append("Locked: ");
        sb.AppendLine(Locked.Value.ToString());
        sb.Append("Private: ");
        sb.AppendLine(Private.Value.ToString());
        sb.Append("AvailableSlots: ");
        sb.AppendLine(AvailableSlots.Value.ToString());
        sb.Append("Max Players: ");
        sb.AppendLine(MaxPlayerCount.Value.ToString());
        sb.Append("LocalLobbyState: ");
        sb.AppendLine(LocalLobbyState.Value.ToString());
        sb.Append("Lobby LocalLobbyState Last Edit: ");
        sb.AppendLine(new DateTime(LastUpdated.Value).ToString(CultureInfo.InvariantCulture));
        sb.Append("RelayCode: ");
        sb.AppendLine(RelayCode.Value);

        return sb.ToString();
    }
}