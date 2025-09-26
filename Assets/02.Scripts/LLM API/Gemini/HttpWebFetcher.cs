using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class HttpWebFetcher
{
    public string baseUri;

    public HttpWebFetcher(string baseUri)
    {
        this.baseUri = baseUri;
    }

    private UnityWebRequest CreateRequest(
        string url,
        string method,
        object data,
        Dictionary<string, string> headers = null
    )
    {
        UnityWebRequest request = new(baseUri + url, method, new DownloadHandlerBuffer(), null);

        if (data is not null)
        {
            string jsonData = JsonConvert.SerializeObject(data);
            Debug.Log(jsonData);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        }

        request.SetRequestHeader("Content-Type", "application/json");
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }
        }

        return request;
    }

    private async UniTask<T> SendRequest<T>(
        string url,
        string method,
        object data,
        Dictionary<string, string> headers = null
    )
    {
        using UnityWebRequest request = CreateRequest(url, method, data, headers);

        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await UniTask.Yield();
        }

        switch (request.result)
        {
            case UnityWebRequest.Result.Success:
                string responseJson = request.downloadHandler.text;
                Debug.Log("Chat_POST > 받는 json : " + responseJson);
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)responseJson;
                }
                try
                {
                    var responseData = JsonConvert.DeserializeObject<T>(responseJson);
                    return responseData;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                    throw new Exception(e.Message);
                }
            case UnityWebRequest.Result.InProgress:
                Debug.LogError("In Progress, Not Waiting Above");
                throw new Exception("In Progress, Not Waiting Above");
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.ProtocolError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.LogError(request.error);
                Debug.LogError(request.downloadHandler.text);
                throw new Exception(request.error);
            default:
                throw new Exception("Unknown error");
        }
    }

    public async UniTask<T> Get<T>(string url, Dictionary<string, string> headers = null)
    {
        try
        {
            return await SendRequest<T>(url, "GET", null, headers);
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public async UniTask<T> Post<T>(
        string url,
        object data,
        Dictionary<string, string> headers = null
    )
    {
        try
        {
            return await SendRequest<T>(url, "POST", data, headers);
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public async UniTask<T> Put<T>(
        string url,
        object data,
        Dictionary<string, string> headers = null
    )
    {
        try
        {
            return await SendRequest<T>(url, "PUT", data, headers);
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    public async UniTask<T> Delete<T>(string url, Dictionary<string, string> headers = null)
    {
        try
        {
            return await SendRequest<T>(url, "DELETE", null, headers);
        }
        catch (Exception e)
        {
            throw e;
        }
    }
}
