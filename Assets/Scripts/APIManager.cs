using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class APIManager : MonoBehaviour
{
    [SerializeField] private string gasUrl;
    [SerializeField] private string prompt;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(SendDataToGas());
        }
    }

    private IEnumerator SendDataToGas()
    {
        WWWForm form  = new WWWForm();
        form.AddField("parameter", prompt);
        UnityWebRequest www = UnityWebRequest.Post(gasUrl, form);

        yield return www.SendWebRequest();
        string response = "";

        if (www.result == UnityWebRequest.Result.Success)
        {
            response = www.downloadHandler.text;
        }
        else
        {
            response = "There was an error!";
        }

        Debug.Log(response);
    }
}
