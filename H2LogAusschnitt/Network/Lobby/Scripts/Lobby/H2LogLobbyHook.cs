using UnityEngine;
using Prototype.NetworkLobby;
using UnityEngine.Networking;
using System.Collections;
using PlayerSystem;



public class H2LogLobbyHook : LobbyHook
{
    /// <summary>
    /// Here you can specify in which objects you want to put your data from the lobbymanager.
    /// Take an object and extract all components you want to use, then specify where to put it.
    /// Given the gameObjects from the Lobby and the associated objects from the game.
    /// Lobbyplayer: saving the data (eg. color) from the lobby screen.
    /// H2LogPlayer: Our Device/Player object used by the lockstep manager.
    /// player: the local player object (eg. god). we can have multiple players on one device.
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="lobbyPlayer"></param>
    /// <param name="gamePlayer"></param>
    public override void OnLobbyServerSceneLoadedForPlayer(NetworkManager manager, GameObject lobbyPlayer, GameObject gamePlayer)
    {
        LobbyPlayer lobby = lobbyPlayer.GetComponent<LobbyPlayer>();
        H2LogPlayer localPlayer = gamePlayer.GetComponent<H2LogPlayer>();
        Player localGamePlayer = gamePlayer.GetComponent<Player>();

        localPlayer.isDummy = lobby.isDummy;

        localGamePlayer.SetName(lobby.playerName);
        

    }

}
