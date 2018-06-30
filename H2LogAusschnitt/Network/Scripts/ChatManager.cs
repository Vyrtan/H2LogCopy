using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ChatManager : Singleton<ChatManager>
{

    
    private List<IChatCallback> Callbacks = new List<IChatCallback>();

    public bool IsNetworkEnabled()
    {
        return H2LogPlayer.instances.Count > 0;
    }

    public bool IsHost()
    {
        return !IsNetworkEnabled() || H2LogPlayer.instances[0].isServer;
    }

    /// <summary>
    /// send the given chat message to all associated players.
    /// </summary>
    /// <param name="msg"></param>
    private void sendChats(ChatMessage msg)
    {
            if (IsNetworkEnabled())
            {
                foreach (var player in H2LogPlayer.instances)
                {
                    player.CmdServerReceiveChat(msg);
                }
        }
            else
            {
                ClientReceiveChat(msg);
            }
    }

    /// <summary>
    /// Trigger the clientside chat callback. Given the chat message.
    /// Change callback behaviour using the interface.
    /// </summary>
    /// <param name="msg"></param>
    public void ClientReceiveChat(ChatMessage msg)
    {
        TriggerCallbacks(msg);
    }

    /// <summary>
    /// Trigger the serverside chat callback. Given the chat message.
    /// Change callback behaviour using the interface.
    /// </summary>
    /// <param name="msg"></param>
    public void ServerReceiveChat(ChatMessage msg)
    {
        Debug.Log(msg.chattext);
        foreach (var player in H2LogPlayer.instances)
        {
            player.RpcClientReceiveChat(msg);
        }
    }

    /// <summary>
    /// Send the string to every Role in the game.
    /// </summary>
    /// <param name="chatText"></param>
    public void SendToAll(string chatText)
    {
        Debug.Log("SendToAll");
        SendToRole("", chatText);
    }

    /// <summary>
    /// Send the chat message to every associated role.
    /// Iterate through the given GameObject chatObj to find the role and message, calling SendToRole(string string).
    /// </summary>
    /// <param name="chatObj"></param>
    public void SendToRole(GameObject chatObj)
    {
        foreach (var ev in chatObj.GetComponents<ChatMsg>())
        {
            SendToRole(ev.Role, ev.Text);
        }
    }

    /// <summary>
    /// Send the chat message to one specific Role. Given the roleName and the chatText.
    /// </summary>
    /// <param name="roleName"></param>
    /// <param name="chatText"></param>
    public void SendToRole(string roleName, string chatText)
    {
        Debug.Log("SendToRole role="+roleName+"; text="+chatText);
        ChatMessage data = new ChatMessage();
        data.chattext = chatText;
        data.role = roleName;

        sendChats(data);
    }

    /// <summary>
    /// Add the given callback to the local list.
    /// </summary>
    /// <param name="call"></param>
    public void AddCallback(IChatCallback call)
    {
        //Da Monobehaviour nicht fest steht muss eigene Liste verwaltet werden.
        Callbacks.Add(call);
    }

    /// <summary>
    /// Remove the given callback from the local list.
    /// </summary>
    /// <param name="call"></param>
    public void RemoveCallback(IChatCallback call)
    {
        Callbacks.Remove(call);
    }

    /// <summary>
    /// Trigger the callback associated with the given ChatMessage.
    /// </summary>
    /// <param name="msg"></param>
    private void TriggerCallbacks(ChatMessage msg)
    {
        Debug.Log("TriggerCallbacks "+msg);
        foreach (var call in Callbacks)
        {
            call.OnChatMessageReceived(msg);
        }

        ExecuteEvents.Execute<IChatCallback>(gameObject, null, (x, y) => x.OnChatMessageReceived(msg));
    }

    /// <summary>
    /// Struct for the Chat Message. Containing a role and chattext string.
    /// </summary>
    [System.Serializable]
    public struct ChatMessage
    {
        public string role;
        public string chattext;
    }

}
