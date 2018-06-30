using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public interface IChatCallback : IEventSystemHandler
{
    /// <summary>
    /// Interface for chat callbacks. Here you can specify how you want to handle different chat callbacks.
    /// Implement a new class using the interface.
    /// Called by the chat manager.
    /// </summary>
    /// <param name="msg"></param>
     void OnChatMessageReceived(ChatManager.ChatMessage msg);
    

}
