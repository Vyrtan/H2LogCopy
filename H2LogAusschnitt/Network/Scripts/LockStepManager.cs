using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class LockStepManager : Singleton<LockStepManager>
{
    /// <summary>
    /// This constant value is used to identify an empty LockStepMessage. 
    /// It is used is the sequence ID for quick look up. When a LockStepMessage with this 
    /// sequence ID is received it can be discarded without analysing its contents.
    /// </summary>
    public const int NO_OP_MSG_ID = int.MinValue;

    /// <summary>
    /// This List is used to buffer NetworkData received from client devices.
    /// The buffered NetworkData will be sent out with the next lock step.
    /// </summary>
    private List<NetworkData> ServerSideBuffer = new List<NetworkData>();
    /// <summary>
    /// This List is used to buffer NetworkData that is to be sent out to the 
    /// server with the next lock step. It is filled by the various "send" methods.
    /// </summary>
    private List<NetworkData> ClientSideBuffer = new List<NetworkData>();
    /// <summary>
    /// This List contains all IRawDataCallbacks registered via the AddRawDataCallback 
    /// method. When raw data is received (by a client) the list will be iterated and 
    /// each callback be called independently.
    /// </summary>
    private List<IRawDataCallback> Callbacks = new List<IRawDataCallback>();
    /// <summary>
    /// This List is used to buffer LockStepMessages received by a client out of order.
    /// The messages are stored until all previous messages have been received and then 
    /// they are executed in their correct order.
    /// </summary>
    private List<LockStepMessage> ClientSideMsgBuffer = new List<LockStepMessage>();

    /// <summary>
    /// The time (in milliseconds) in between two lock steps. This will be synchronized 
    /// over the network.
    /// </summary>
    public int LockStepIntervall = 250;

    /// <summary>
    /// Not used right now
    /// </summary>
    public int LockStepDelayBuffer = 0;

    /// <summary>
    /// If set to true the LockStepManager will try to adjust the LockStepInterval dynamically to react to latency.
    /// </summary>
    public bool DynamicLockStepIntervalEnabled = false;

    /// <summary>
    /// Determines which log messages will be displayed during the game.
    /// </summary>
    public DebugLevel DebugLvl = DebugLevel.OFF;
    /// <summary>
    /// Determines how the system will react to an error situation.
    /// </summary>
    public ErrorResponse ErrorBehavior = ErrorResponse.PRINT_TO_CONSOLE;

    /// <summary>
    /// Time (in milliseconds) since the last lock step. Used to wait before performing 
    /// the next lock step.
    /// </summary>
    private float AccumulatedTime = 0;
    /// <summary>
    /// The next message sequence ID sent out by the server.
    /// </summary>
    private int ServerSideMsgSeqID = NO_OP_MSG_ID + 1;
    /// <summary>
    /// The next message sequence ID that needs to be executed by the client. If a received 
    /// message has a sequence ID above this value it was received out of order and needs to 
    /// be delayed.
    /// If a received message has a sequence ID below this value an error happened.
    /// </summary>
    private int ClientSideExpectedMsgSeqID = NO_OP_MSG_ID + 1;

    /// <summary>
    /// The different DebugLevels used by the PrintDebug method.
    /// </summary>
    public enum DebugLevel
    {
        OFF,
        DATA_ONLY,
        VERBOSE,
        DEBUG,
    }

    /// <summary>
    /// The different error responses used by the ReactToError method.
    /// </summary>
    public enum ErrorResponse
    {
        IGNORE,
        PRINT_TO_CONSOLE,
        THROW_EXCEPTION,
    }

    /// <summary>
    /// Prints the given message only if the given debug level is equal to or below the current 
    /// debug level set in the public DebugLvl field.
    /// </summary>
    /// <param name="lvl">The debug level of the message.</param>
    /// <param name="format">The message to print. Numbers in between curly braces will be 
    /// replaced by elements from the args array.</param>
    /// <param name="args">An array of objects used to fill place holders within the format string.</param>
    private void PrintDebug(DebugLevel lvl, string format, params object[] args)
    {
        if (lvl <= DebugLvl)
        {
            string input = string.Format(null, format, args);
            string output = string.Format(null, "[{0}] {1}\n", lvl, input);
            Debug.Log(output);
        }
    }

    /// <summary>
    /// Reacts to an error based on the ErrorResponse set in the public ErrorBehavior field.
    /// </summary>
    /// <param name="format">A string used to describe the error. Numbers in between curly 
    /// braces will be replaced by elements from the args array.</param>
    /// <param name="args">An array of objects used to fill place holders within the format string.</param>
    private void ReactToError(string format, params object[] args)
    {
        if (ErrorBehavior == ErrorResponse.PRINT_TO_CONSOLE)
        {
            Debug.LogFormat(format, args);
        }
        else if (ErrorBehavior == ErrorResponse.PRINT_TO_CONSOLE)
        {
            string output = string.Format(format, args);
            throw new H2LogNetworkException(output);
        }
    }

    /// <summary>
    /// This method should be called after every time the SerializationCtrl is used to perform 
    /// serialization or deserialization. This method will usethe ReactToError method to react 
    /// to any errors during the serialization process.
    /// </summary>
    private void ReactToSerializationError()
    {
        foreach (var err in SerializationCtrl.GetCurrentErrors())
        {
            ReactToError(err.GetFormatString(), err.GetFormatArgs());
        }
        SerializationCtrl.ClearCurrentErrors();
    }

    private void TranslateAll(object[] obj)
    {
        foreach (var o in obj) {
            if (o.GetType().IsArray) {
                TranslateArray((System.Array) o);
            }
            else if (o.GetType() == typeof(List<NetworkData>))
            {
                TranslateList((List<NetworkData>)o);
            }
        }
    }

    private string TranslateArray(System.Array arr)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[");
        foreach (var o in arr) {
            sb.Append(o);
            sb.Append(", ");
        }
        sb.Append("]");
        return sb.ToString();
    }

    private string TranslateList<T>(List<T> list)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[");
        foreach (var o in list)
        {
            sb.Append(o);
            sb.Append(", ");
        }
        sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// A class used for all Exceptions thrown by the networking system.
    /// </summary>
    [System.Serializable]
    public class H2LogNetworkException : System.Exception
    {
        public H2LogNetworkException(string message) : base(message) { }
        public H2LogNetworkException(string message, System.Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Add default classes used by the networking system to the SerializationCtrl.
    /// Query NetworkEvents from the EventManager and register them at the SerializationCtrl.
    /// </summary>
    public void Start()
    {
        // Data types used by the networking system
        SerializationCtrl.RegisterSerializer(
            typeof(List<NetworkData>), new SerializationCtrl.ListSerializer());
        SerializationCtrl.RegisterSerializer(
            typeof(RawNetworkData), new SerializationCtrl.ClassSerializer());
        SerializationCtrl.RegisterSerializer(
            typeof(RpcNetworkData), new SerializationCtrl.ClassSerializer());
        SerializationCtrl.RegisterSerializer(
            typeof(RpcMsg), new SerializationCtrl.ClassSerializer());
        SerializationCtrl.RegisterSerializer(
            typeof(EventNetworkData), new SerializationCtrl.ClassSerializer());
        SerializationCtrl.RegisterSerializer(
            typeof(LockStepMessage), new SerializationCtrl.ClassSerializer());
        SerializationCtrl.RegisterSerializer(
            typeof(Vector3), new Vector3Serializer());

		
		CustomEventSystem.EventManager evtMngr = CustomEventSystem.EventManager.Instance;
        List<System.Type> nwEvtTypes = evtMngr.GetRegisteredNetworkEventTypes();
        PrintDebug(DebugLevel.VERBOSE, "Start.NetworkEventCount {0}", nwEvtTypes.Count);
		foreach (var eventType in nwEvtTypes) {
            PrintDebug(DebugLevel.VERBOSE, "Start.AddNetworkEventType {0}", eventType);
			SerializationCtrl.RegisterSerializer(
				eventType, new SerializationCtrl.ClassSerializer());
		}
    }

    public void Awake()
    {
        DontDestroyOnLoad(transform.gameObject);
    }

    /// <summary>
    /// Count time until next lock step. If time is reached perform next lock step.
    /// Calls the SendLockStep() method when it is time to perform a lock step.
    /// </summary>
    public void Update()
    {
        AccumulatedTime += Time.deltaTime;
        //PrintDebug(DebugLevel.DEBUG, "AccumulatedTime={0}", AccumulatedTime);
        float lockStepIntervallSeconds = GetCurrentLockStepInterval() / 1000.0f;
        if (AccumulatedTime >= lockStepIntervallSeconds)
        {
            AccumulatedTime %= lockStepIntervallSeconds;
            SendLockStep();
            UpdateLockStepIntervalDynamic();
        }
    }

    private int GetCurrentLockStepInterval()
    {
        if (!IsNetworkEnabled()) {
            return LockStepIntervall;
        }
        if (H2LogPlayer.localPlayer == null)
        {
            return LockStepIntervall;
        }
        int adjVal = H2LogPlayer.localPlayer.AdjustedLockStepInterval;
        if (adjVal < 0) {
            H2LogPlayer.localPlayer.AdjustedLockStepInterval = LockStepIntervall;
            return LockStepIntervall;
        }
        else
        {
            return adjVal;
        }
    }

    private void UpdateLockStepIntervalDynamic()
    {
        if (!DynamicLockStepIntervalEnabled || !IsHost() || !IsNetworkEnabled())
        {
            return;
        }
        if (NetworkServer.connections.Count < 2)
        {
            return;
        }
        double avgRtt = 0;
        int rttCount = 0;
        foreach (NetworkConnection conn in NetworkServer.connections)
        {
            // if conn.hostId == -1 it is the connection to ourself
            if (conn == null || conn.hostId == -1) {
                continue;
            }
            byte error;
            int rtt = NetworkTransport.GetCurrentRtt(conn.hostId, conn.connectionId, out error);
            avgRtt += rtt;
            rttCount++;
            PrintDebug(DebugLevel.OFF, "rtt={0}", rtt);
        }
        avgRtt /= rttCount;
        PrintDebug(DebugLevel.DEBUG, "avgRtt={0}", avgRtt);
        int newInterval = (int)(GetCurrentLockStepInterval() + avgRtt) / 2;
        if (newInterval > 0)
        {
            PrintDebug(DebugLevel.DEBUG, "newInterval={0}", newInterval);
            foreach (H2LogPlayer nwPlayer in H2LogPlayer.instances)
            {
                nwPlayer.AdjustedLockStepInterval = newInterval;
            }
        }
    }

    /// <summary>
    /// Returns true if this is a networked game.
    /// </summary>
    /// <returns>True if networking is enabled</returns>
    public bool IsNetworkEnabled()
    {
        return H2LogPlayer.instances.Count > 0;
    }

    /// <summary>
    /// Returns true if this device is the host of the game and thus the server.
    /// Only one device can be the host of the game. The host has special 
    /// responsibility during the game.
    /// 
    /// If networking is disabled (and thus there is only one device!) this device 
    /// is _always_ the host. (since it is the only device)
    /// </summary>
    /// <returns>True if networking is _DISABLED_ or if this device is the host of 
    /// a networked game</returns>
    public bool IsHost()
    {
        return !IsNetworkEnabled() || H2LogPlayer.instances[0].isServer;
    }

    /// <summary>
    /// If this device is the host then all currently buffered messages within the 
    /// ServerSideBuffer will be sent out to all devices (including this one!).
    /// 
    /// If this device is a client then all currently buffered messages within the 
    /// ClientSideBuffer will be sent out to the host device (server).
    /// 
    /// If networking is disabled this device will simply execute all local messages 
    /// buffered in the ClientSideBuffer.
    /// </summary>
    private void SendLockStep()
    {
        PrintDebug(DebugLevel.DEBUG, "SendLockStep.IsHost={0}", IsHost());
        if (IsHost())
        {
            ServerSideBuffer.AddRange(ClientSideBuffer);

            LockStepMessage msg = BuildLockStepMessage(ServerSideBuffer);
            if (IsNetworkEnabled())
            {
                List<byte> byteList = SerializationCtrl.Serialize(msg);
                ReactToSerializationError();

                byte[] byteArr = byteList.ToArray();
                foreach (var player in H2LogPlayer.instances) {
                    player.RpcClientReceiveLockStep(byteArr);
                }
            }
            else
            {
                ClientProcessLockStep(msg);
            }
            ServerSideBuffer.Clear();
        }
        else
        {
            LockStepMessage msg = BuildLockStepMessage(ClientSideBuffer);
            if (IsNetworkEnabled())
            {
                List<byte> byteList = SerializationCtrl.Serialize(msg);
                ReactToSerializationError();

                byte[] byteArr = byteList.ToArray();
                H2LogPlayer.localPlayer.CmdServerReceiveLockStep(byteArr);
            }
            else
            {
                ServerReceiveMessage(msg);
            }
        }
        ClientSideBuffer.Clear();
    }

    /// <summary>
    /// Called by the H2LogPlayer after byte data was received from the server.
    /// This method is called on all devices, even the server.
    /// </summary>
    /// <param name="byteArr">bytes from the network</param>
    public void ClientProcessLockStep(byte[] byteArr)
    {
        PrintDebug(DebugLevel.DEBUG, "ClientProcessLockStep (byte[])");
        List<object> list = SerializationCtrl.Deserialize(byteArr);
        ReactToSerializationError();

        LockStepMessage msg = (LockStepMessage)list[0];
        ClientProcessLockStep(msg);
    }

    /// <summary>
    /// Called by the ClientProcessLockStep method if we are playing over the network 
    /// and we just received data from the server or by the SendLockStep method if we 
    /// are playing locally.
    /// This method should buffer a message received out of order or execute the network 
    /// data from a message received in order.
    /// </summary>
    /// <param name="msg">The LockStepMessage to process</param>
    private void ClientProcessLockStep(LockStepMessage msg)
    {
        if (msg.seqID == NO_OP_MSG_ID)
        {
            return;
        }
        PrintDebug(DebugLevel.VERBOSE, "ClientProcessLockStep seqID={0}", msg.seqID);
        if (msg.seqID == ClientSideExpectedMsgSeqID)
        {
            ClientSideExpectedMsgSeqID++;
            ExecuteLockStepMessage(msg);

            if (ClientSideMsgBuffer.Count > 0)
            {
                ClientSideMsgBuffer.Sort(delegate(LockStepMessage m1, LockStepMessage m2)
                {
                    return m1.seqID.CompareTo(m2.seqID);
                });
                while (ClientSideMsgBuffer.Count > 0)
                {
                    LockStepMessage bufMsg = ClientSideMsgBuffer[0];
                    if (bufMsg.seqID < ClientSideExpectedMsgSeqID)
                    {
                        ClientSideMsgBuffer.RemoveAt(0);
                        ReactToError("Executed buffered LockStepMessage with sequence ID from the past. SeqID={0}", bufMsg.seqID);
                    }
                    else if (bufMsg.seqID == ClientSideExpectedMsgSeqID)
                    {
                        ClientSideMsgBuffer.RemoveAt(0);
                        ExecuteLockStepMessage(bufMsg);
                        ClientSideExpectedMsgSeqID++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        else if (msg.seqID > ClientSideExpectedMsgSeqID)
        {
            ClientSideMsgBuffer.Add(msg);
            PrintDebug(DebugLevel.VERBOSE, "BufferLockStepMessage seqID={0}, currentBufferSize={1}", 
                msg.seqID, ClientSideMsgBuffer.Count);
        }
        else
        {
            ReactToError("Received LockStepMessage with sequence ID from the past. SeqID={0}", msg.seqID);
        }
    }

    /// <summary>
    /// Calls the OnReceive method of all NetworkData within the given LockStepMessage.
    /// </summary>
    /// <param name="msg">The LockStepMessage to execute</param>
    private void ExecuteLockStepMessage(LockStepMessage msg)
    {
        PrintDebug(DebugLevel.DEBUG, "ExecuteLockStepMessage SeqID={0}", msg.seqID);
        foreach (var data in msg.dataList)
        {
            data.OnReceive(this);
        }
    }

    /// <summary>
    /// Called by the H2LogPlayer when a Client has sent a LockStepMessage over the network.
    /// This method is supposed to buffer the data from the client for the next LockStep.
    /// </summary>
    /// <param name="byteArr">bytes from the network</param>
    public void ServerReceiveMessage(byte[] byteArr)
    {
        PrintDebug(DebugLevel.DEBUG, "ServerReceiveMessage (byte[])");
        List<object> list = SerializationCtrl.Deserialize(byteArr);
        ReactToSerializationError();

        LockStepMessage msg = (LockStepMessage)list[0];
        ServerReceiveMessage(msg);
    }

    /// <summary>
    /// Called by the ServerReceiveMessage method when we are playing over the network or 
    /// the SendLockStep method if we are playing without networking.
    /// </summary>
    /// <param name="msg">A LockStepMessage sent by a client to the server</param>
    private void ServerReceiveMessage(LockStepMessage msg)
    {
        ServerSideBuffer.AddRange(msg.dataList);
    }

    /// <summary>
    /// Builds a LockStepMessage from the given List of NetworkData.
    /// This method guarantees that the constructed message has the correct sequence ID.
    /// </summary>
    /// <param name="dataList">The NetworkData contained in the message</param>
    /// <returns>A new LockStepMessage</returns>
    private LockStepMessage BuildLockStepMessage(List<NetworkData> dataList)
    {
        LockStepMessage msg = new LockStepMessage();
        // When the dataList is empty we need to send a No-Op message (to save bandwidth)
        if (dataList.Count == 0)
        {
            msg.seqID = NO_OP_MSG_ID;
            // We do not need to clone the dataList in this case since it is empty anyways
            // Furthermore,  the special NO_OP_MSG_ID makes sure no NetworkData will be read
            msg.dataList = dataList;
            PrintDebug(DebugLevel.DEBUG, "BuildLockStepMessage NO_OP_MSG_ID");
            return msg;
        }
        if (IsHost()) {
            msg.seqID = ServerSideMsgSeqID++;
        }
        else
        {
            // When we are the client we dont need to count MsgSeqID's since the server 
            // does no verification of order
            msg.seqID = NO_OP_MSG_ID;
        }
        // We clone the dataList because it might get cleared by the LockStepManager
        msg.dataList = new List<NetworkData>(dataList);
        PrintDebug(DebugLevel.DEBUG, "BuildLockStepMessage seqID={0}, count={1}", 
            msg.seqID, msg.dataList.Count);
        return msg;
    }

    /// <summary>
    /// Sends the given String as raw data to all devices.
    /// </summary>
    /// <param name="rawString">A string or null</param>
    public void SendToAll(string rawString)
    {
        SendToAll(new object[] { rawString });
    }

    /// <summary>
    /// Sends the object array to all devices.
    /// </summary>
    /// <param name="rawData">a non-null object array</param>
    public void SendToAll(params object[] rawData)
    {
        PrintDebug(DebugLevel.DATA_ONLY, "SendToAll rawData={0}", rawData);
        if (rawData == null) {
            ReactToError("SendToAll called with a array rawData == null");
        }

        RawNetworkData data = new RawNetworkData();
        data.rawArr = rawData;

        if (IsNetworkEnabled())
        {
            List<byte> byteList = SerializationCtrl.Serialize(data);
            ReactToSerializationError();

            byte[] byteArr = byteList.ToArray();
            H2LogPlayer.localPlayer.CmdServerReceiveRawData(byteArr);
        }
        else
        {
            data.OnReceive(this);
        }
    }

    public void ServerReceiveRawData(byte[] byteArr)
    {
        H2LogPlayer.localPlayer.RpcClientReceiveRawData(byteArr);
        //foreach (var player in H2LogPlayer.instances)
        //{
        //    player.RpcClientReceiveRawData(byteArr);
        //}
    }

    public void ClientReceiveRawData(byte[] byteArr)
    {
        RawNetworkData data = (RawNetworkData) SerializationCtrl.Deserialize(byteArr, 0);
        data.OnReceive(this);
    }

    /// <summary>
    /// Calls a method remotely on all devices. The string must contain the name of the 
    /// GameObject, the Component and the method all separated by dots. 
    /// Example: "GameManager.LockStepManager.Start"
    /// </summary>
    /// <param name="rpcIdentifier">the method identifier</param>
    public void CallRpc (string rpcIdentifier)
    {
        CallRpc(rpcIdentifier, new object[0]);
    }

    /// <summary>
    /// Calls a method remotely on all devices. The string must contain the name of the 
    /// GameObject, the Component and the method all separated by dots. The arguments must 
    /// match the parameter list of the method or else an error will be generated.
    /// Example: "GameManager.LockStepManager.Start"
    /// </summary>
    /// <param name="rpcIdentifier">the method identifier</param>
    /// <param name="rpcArgs">the arguments passed to the method</param>
    public void CallRpc(string rpcIdentifier, params object[] rpcArgs)
    {
        PrintDebug(DebugLevel.DATA_ONLY, "CallRpc identifier={0}, args={1}", 
            rpcIdentifier,  rpcArgs);
        if (rpcIdentifier == null)
        {
            ReactToError("CallRpc called with rpcIdentifier == null");
            return;
        }
        if (rpcArgs == null)
        {
            ReactToError("CallRpc called with rpcArgs == null");
            return;
        }
        string[] parts = rpcIdentifier.Split('.');
        if (parts.Length != 3)
        {
            ReactToError("CallRpc called with illegal rpcIdentifier={0}", rpcIdentifier);
            return;
        }
        RpcMsg rpcMsg = new RpcMsg();
        rpcMsg.gameObjName = parts[0];
        rpcMsg.componentName = parts[1];
        rpcMsg.methodName = parts[2];
        rpcMsg.parameters = rpcArgs;

        CallRpc(rpcMsg);
    }

    /// <summary>
    /// Calls a method remotely on all devices.
    /// </summary>
    /// <param name="rpcMsg">Data about the method that is to be called remotely.</param>
    public void CallRpc(RpcMsg rpcMsg)
    {
        PrintDebug(DebugLevel.DATA_ONLY, "CallRpc={0}.{1}.{2} ({3})", 
            rpcMsg.gameObjName, rpcMsg.componentName, 
            rpcMsg.methodName, rpcMsg.parameters);
        RpcNetworkData data = new RpcNetworkData();
        data.msgObj = rpcMsg;
        ClientSideBuffer.Add(data);
    }

    /// <summary>
    /// Sends the given event over the network to all devices. Once received the event 
    /// will be handled by the local EventManager of the device.
    /// </summary>
    /// <param name="evtObj">the Event to send</param>
    public void TriggerSingleEvent(CustomEventSystem.Event evtObj)
    {
        PrintDebug(DebugLevel.DATA_ONLY, "TriggerSingleEvent={0}", evtObj);
        if (evtObj == null) {
            ReactToError("TriggerSingleEvent called with evtObj == null");
            return;
        }
        EventNetworkData data = new EventNetworkData();
        data.eventObj = evtObj;
        ClientSideBuffer.Add(data);
    }

    /// <summary>
    /// Triggers all Events attached to the given GameObject at once.
    /// </summary>
    /// <param name="eventObj">a unity GameObject</param>
    public void TriggerAllEvents(GameObject eventObj)
    {
        foreach (var ev in eventObj.GetComponents<CustomEventSystem.Event>()) {
            TriggerSingleEvent(ev);
        }
    }

    /// <summary>
    /// Registers the callback
    /// </summary>
    /// <param name="call"></param>
    public void AddRawDataCallback(IRawDataCallback call)
    {
        Callbacks.Add(call);
    }

    public void RemoveRawDataCallback(IRawDataCallback call)
    {
        Callbacks.Remove(call);
    }

    private void TriggerRawDataCallbacks(params object[] rawData)
    {
        PrintDebug(DebugLevel.DEBUG, "TriggerRawDataCallbacks={0}", rawData);
        foreach (var call in Callbacks)
        {
            call.OnRawDataReceived(rawData);
        }

        ExecuteEvents.Execute<IRawDataCallback>(gameObject, null, (x, y) => x.OnRawDataReceived(rawData));
    }

    private void PrintList<T>(List<T> list)
    {
        if (list.Count > 0)
        {
            foreach (var o in list)
            {
                Debug.Log(o.ToString());
            }
        }
    }

    public interface NetworkData
    {
        void OnReceive(LockStepManager mngr);
    }

    public class EventNetworkData : NetworkData
    {
        public CustomEventSystem.Event eventObj;

        public void OnReceive(LockStepManager mngr)
        {
            mngr.PrintDebug(DebugLevel.DATA_ONLY, "EventNetworkData eventObj={0}", eventObj);
            CustomEventSystem.EventManager.Instance.HandleEvent(eventObj);
        }
    }

    public class RpcNetworkData : NetworkData
    {

        public RpcMsg msgObj;

        public void OnReceive(LockStepManager mngr)
        {
            mngr.PrintDebug(DebugLevel.DATA_ONLY, "RpcNetworkData msgObj={0}", msgObj);
            try
            {
                string nameObject = msgObj.gameObjName;
                string nameComponent = msgObj.componentName;
                string nameMethod = msgObj.methodName;

                GameObject obj = GameObject.Find(nameObject);
                Component component = obj.GetComponent(nameComponent);
                System.Reflection.MethodInfo method = component.GetType().GetMethod(nameMethod);
                method.Invoke(component, msgObj.parameters);
            }
            catch (System.Exception e)
            {
                mngr.ReactToError("Caught Exception during RPC invocation. Exception={0}", e);
            }
        }
    }
    
    public class RawNetworkData : NetworkData
    {

        public object[] rawArr;

        public void OnReceive(LockStepManager mngr)
        {
            mngr.PrintDebug(DebugLevel.DATA_ONLY, "RawNetworkData rawArr={0}", rawArr);
            mngr.TriggerRawDataCallbacks(rawArr);
        }
    }

    public struct LockStepMessage
    {
        public int seqID;
        public List<NetworkData> dataList;
    }

}
