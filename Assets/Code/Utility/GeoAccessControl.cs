using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class IpWhoResponse
{
    public string country_code; 
    public string ip;
    public string calling_code; 
}

public class GeoAccessControl : MonoBehaviour
{
    private readonly string[] restrictedCountries = { "RU", "BY" };

    private IpWhoResponse cachedResponse;

    public event Action OnAccessGranted;
    public event Action<string> OnAccessDenied;
    public event Action<string> OnError;

    [SerializeField] private bool autoCheckOnStart = true;
    private static bool IsCheckedGeo = false;

    void Start()
    {
        if (autoCheckOnStart)
        {
            CheckAccess();
        }
    }

    public void CheckAccess()
    {
        StartCoroutine(CheckGeoLocationRoutine());
    }

    private IEnumerator CheckGeoLocationRoutine()
    {
        string url = "https://ipwho.is/";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();
            IsCheckedGeo = true;

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    cachedResponse = JsonUtility.FromJson<IpWhoResponse>(request.downloadHandler.text);

                    if (cachedResponse == null || string.IsNullOrEmpty(cachedResponse.country_code))
                    {
                         OnError?.Invoke("json");
                         yield break;
                    }
                    
                    if (IsCountryRestricted(cachedResponse.country_code))
                    {
                        Debug.LogWarning($"[Geo] Access Denied. Country: {cachedResponse.country_code}");
                        OnAccessDenied?.Invoke(cachedResponse.country_code);
                    }
                    else 
                    {
                        Debug.Log($"[Geo] Access Granted. Country: {cachedResponse.country_code}");
                        OnAccessGranted?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Geo] Parse Error: {e.Message}");
                    OnError?.Invoke("parse");
                }
            }
            else
            {
                Debug.LogError($"[Geo] Network Error: {request.error}");
                //OnError?.Invoke(request.error); 
                OnAccessGranted?.Invoke();
            }
        }
    }

    public string GetCountryCallingCode()
    {
        if (cachedResponse != null && !string.IsNullOrEmpty(cachedResponse.calling_code))
        {
            return cachedResponse.calling_code;
        }
        
        return null;
    }

    private bool IsCountryRestricted(string code)
    {
        foreach (var restricted in restrictedCountries)
        {
            if (code == restricted) return true;
        }
        return false;
    }
}
