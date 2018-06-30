using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public struct RpcMsg {

    public string gameObjName;
    public string componentName;
    public string methodName;
    public object[] parameters;

}
