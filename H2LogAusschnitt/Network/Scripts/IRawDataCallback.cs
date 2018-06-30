using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public interface IRawDataCallback : IEventSystemHandler
{
    /// <summary>
    /// Interface for the raw data callbacks. Here you can specify how to handle different callbacks for raw data application.
    /// Implement a new class using the interface.
    /// Called by the lockstep manager.
    /// </summary>
    /// <param name="data"></param>
    void OnRawDataReceived(object[] data);
}
