using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class NetworkManagerTest : NetworkManager {

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        
        Debug.Log("OnServerAddPlayer=" + playerControllerId);
        var player = (GameObject)GameObject.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        player.GetComponent<H2LogPlayer>().ownConnection = conn;
        NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
    }
}
