using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class RTDNetworkTrace : MonoBehaviour {
    sealed class Pending { public WWW Request; public int Id; public float Started; public float Warned; public string Url; }
    static readonly List<Pending> pending = new List<Pending>();
    static RTDNetworkTrace instance;
    static int sequence;
    static string SafeUrl(string url) { return url == null ? "<null>" : url.Split('?')[0]; }
    public static WWW Send(string url, byte[] data, Hashtable headers) {
        int id = ++sequence;
        Debug.Log("RTD_NET SEND id=" + id + " url=" + SafeUrl(url) + " bytes=" + (data == null ? 0 : data.Length));
        WWW request;
        try { request = new WWW(url, data, headers); }
        catch (Exception e) { Debug.LogError("RTD_NET THROW id=" + id + " type=" + e.GetType().FullName + " message=" + e.Message); throw; }
        Debug.Log("RTD_NET CREATED id=" + id);
        try {
            if (instance == null) {
                GameObject go = new GameObject("RTDNetworkTrace");
                UnityEngine.Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<RTDNetworkTrace>();
            }
            float now = Time.realtimeSinceStartup;
            pending.Add(new Pending { Request=request, Id=id, Started=now, Warned=now, Url=SafeUrl(url) });
        } catch (Exception e) { Debug.LogWarning("RTD_NET MONITOR_ERROR " + e.GetType().FullName); }
        return request;
    }
    void Update() {
        float now = Time.realtimeSinceStartup;
        for (int i=pending.Count-1; i>=0; i--) {
            Pending p=pending[i];
            try {
                if (p.Request.isDone) {
                    Debug.Log("RTD_NET DONE id=" + p.Id + " url=" + p.Url + " elapsed=" + (now-p.Started) + " error=" + (p.Request.error ?? "<none>"));
                    pending.RemoveAt(i);
                } else if (now-p.Warned>=15f) {
                    Debug.LogWarning("RTD_NET WAIT id=" + p.Id + " url=" + p.Url + " elapsed=" + (now-p.Started) + " progress=" + p.Request.progress);
                    p.Warned=now;
                }
            } catch (Exception e) { Debug.LogWarning("RTD_NET OBSERVE_ERROR id=" + p.Id + " type=" + e.GetType().FullName); pending.RemoveAt(i); }
        }
    }
}
