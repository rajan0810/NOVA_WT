using UnityEngine;
using TMPro;

public class UIUpdate : MonoBehaviour
{
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI descriptionText;

    public string text1;
    public string text2;

    public void TextUpdate()
    {
        headerText.text = text1;
        descriptionText.text = text2;
    }
}
