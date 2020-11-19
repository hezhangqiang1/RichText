using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Href 测试
/// </summary>
public class TestHref : MonoBehaviour
{
    private LinkImageText textPic;

    void Awake()
    {
        textPic = GetComponent<LinkImageText>();
    }

    void OnEnable()
    {
        textPic.OnHrefClick.AddListener(OnHrefClick);
    }

    void OnDisable()
    {
        textPic.OnHrefClick.RemoveListener(OnHrefClick);
    }

    private void OnHrefClick(string href)
    {
        Text text = GameObject.Find("TextResult").GetComponent<Text>();
        text.text = "点击了" + href;
        Debug.Log("点击了 " + href);
    }

}
