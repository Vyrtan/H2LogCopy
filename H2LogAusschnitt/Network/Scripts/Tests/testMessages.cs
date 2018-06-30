using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class testMessages : MonoBehaviour {

    // Use this for initialization
    byte[] testArr = new byte[128];
    int counterTest = 0;
    LockStepManager test;

    void Start () {

        test = GameObject.Find("LockstepManager").GetComponent<LockStepManager>();
        List<byte> data = SerializationCtrl.Serialize(new byte[10000]);
        testArr = data.ToArray();
    }
	
	// Update is called once per frame
	void Update () {
        counterTest = 0;
        while (counterTest < 100)
        {
            test.SendToAll(testArr);
            counterTest++;
        }
             Debug.Log(counterTest);
        }
    }
