using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class H2LogPlayer : NetworkBehaviour {

    [SyncVar]
    public bool isDummy;
    [SyncVar]
    public int AdjustedLockStepInterval = -1;

    public NetworkConnection ownConnection;

    public static List<H2LogPlayer> instances = new List<H2LogPlayer>();

    public static H2LogPlayer localPlayer;

    /// <summary>
    /// Add the H2LogPlayer object to the list of active players if needed.
    /// </summary>
   void Awake () {
        if (!isDummy)
        {
            instances.Add(this);

        }
        else
        {
            Destroy(gameObject);
        }
	} 

    /// <summary>
    /// Set the local player.
    /// </summary>
    void Start()
    {
        if (isLocalPlayer)
        {
            localPlayer = this;
        }
    }

    void OnDestroy()
    {
        instances.Remove(this);
    }

    /// <summary>
    /// Clientside lockstep call. Given a field of bytes for the serialized parameters.
    /// </summary>
    /// <param name="bytes"></param>
    [ClientRpc]
    public void RpcClientReceiveLockStep(byte[] bytes)
    {
        LockStepManager.Instance.ClientProcessLockStep(bytes);
    }

    /// <summary>
    /// Serverside lockstep call. Given a field of bytes for the serialized parameters.
    /// </summary>
    /// <param name="bytes"></param>
    [Command]
    public void CmdServerReceiveLockStep(byte[] bytes)
    {
        LockStepManager.Instance.ServerReceiveMessage(bytes);
    }

    /// <summary>
    /// Clientside Chat call. Given a Chat message.
    /// </summary>
    /// <param name="msg"></param>
    [ClientRpc]
    public void RpcClientReceiveChat(ChatManager.ChatMessage msg)
    {
        ChatManager.Instance.ClientReceiveChat(msg);
    }

    /// <summary>
    /// Serverside Chat call. Given a chat message.
    /// </summary>
    /// <param name="msg"></param>
    [Command]
    public void CmdServerReceiveChat(ChatManager.ChatMessage msg)
    {
        ChatManager.Instance.ServerReceiveChat(msg);
    }

    /// <summary>
    /// Clientside raw data call. Given the raw data as a field of bytes.
    /// </summary>
    /// <param name="bytes"></param>
    [ClientRpc]
    public void RpcClientReceiveRawData(byte[] bytes)
    {
        LockStepManager.Instance.ClientReceiveRawData(bytes);
    }

    /// <summary>
    /// Serverside raw data call. Given the raw data as a field of bytes.
    /// </summary>
    /// <param name="bytes"></param>
    [Command]
    public void CmdServerReceiveRawData(byte[] bytes)
    {
        LockStepManager.Instance.ServerReceiveRawData(bytes);
    }

}
