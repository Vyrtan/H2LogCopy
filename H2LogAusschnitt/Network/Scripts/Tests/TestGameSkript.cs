using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using CustomEventSystem;

public class TestGameSkript : MonoBehaviour, IRawDataCallback
{

    public int counter = 0;
    Text ourText;
    bool server;

    

    // Use this for initialization
    void Awake() {

    ourText = GameObject.Find("Display").GetComponent<Text>();
        GameObject managerObject = GameObject.Find("LockstepManager");
        EventManager manager = managerObject.GetComponent<EventManager>();
        //manager.RegisterCallback(this.DoubleCounter, typeof(DoubleCounterEvent));
        manager.RegisterCallback<DoubleCounterEvent>(DoubleCounter);
        //Debug.Log("RegisterCallback<DoubleCounterEvent>");
    }


    public void OnRawDataReceived(object[] data)
    {
        if (data[0].Equals("up"))
        {
            counter++;
        }
        else
        {
            counter--;
        }
        ourText.text = counter.ToString();
    }

    public void SetTo42()
    {
        SetTo(42);
    }

    public void SetTo(int value)
    {
        counter = value;
        ourText.text = counter.ToString();
    }

    public void sendSetTo123Rpc()
    {
        RpcMsg rpcMsg = new RpcMsg();
        rpcMsg.gameObjName = "LockstepManager";
        rpcMsg.componentName = "TestGameSkript";
        rpcMsg.methodName = "SetTo";
        rpcMsg.parameters = new System.Object[] {123};

        GameObject.Find("LockstepManager").GetComponent<LockStepManager>().CallRpc(rpcMsg);
    }

    public void sendDoubleItEvent()
    {
        DoubleCounterEvent ev = new DoubleCounterEvent();
        GameObject.Find("LockstepManager").GetComponent<LockStepManager>().TriggerSingleEvent(ev);
    }

    public void DoubleCounter(CustomEventSystem.Event dc)
    {
        counter *= 2;
        ourText.text = counter.ToString();
    }

}