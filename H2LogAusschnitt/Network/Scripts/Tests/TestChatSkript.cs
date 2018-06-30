using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using CustomEventSystem;


public class TestChatSkript : MonoBehaviour, IChatCallback
{

    public int counter = 0;
    Text ourChatText;
    InputField ourInputText;
    bool server;
    string current = "";
    List<ChatManager.ChatMessage> Chat = new List<ChatManager.ChatMessage>();


    void Awake()
    {

        Debug.Log("Test Chat Initialisiert");
        ourChatText = GameObject.Find("ChatFenster").GetComponent<Text>();
        ourInputText = GameObject.Find("InputField").GetComponent<InputField>();
       GameObject managerObject = GameObject.Find("ChatInterface");
    }
   
    // Use this for initialization
    public void OnChatMessageReceived(ChatManager.ChatMessage data)
    {
        Debug.Log("ONChat");
        Chat.Add(data);
        current = "";
        if(Chat.Count > 3)
        {
            Chat.RemoveAt(0);
        }
        foreach(var msg in Chat)
        {
            current = current + msg.chattext +"\n";
            ourChatText.text = current;
        }
        ourInputText.text = "";
    }

    /*public void sendChat(string msg)
    {
        Debug.Log(msg);
        GameObject.Find("ChatInterface").GetComponent<ChatInterface>().SendToAll(msg);
    }*/

}