using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TextResolution;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <inheritdoc cref="Text" />
/// <summary>
/// 文本控件，支持超链接、图片
/// </summary>
[AddComponentMenu("UI/LinkImageText", 10)]
public class LinkImageText : Text, IPointerClickHandler
{
    /// <summary>
    /// 解析完最终的文本
    /// </summary>
    private string m_OutputText;

    /// <summary>
    /// 图片池
    /// </summary>
    protected readonly List<Image> m_ImagesPool = new List<Image>();

    /// <summary>
    /// 图片的最后一个顶点的索引
    /// </summary>
    private readonly List<int> m_ImagesLastVertexIndexList = new List<int>();

    /// <inheritdoc />
    /// <summary>
    /// HrefClick 事件
    /// </summary>
    [Serializable]
    public class HrefClickEvent : UnityEvent<string> { }

    /// <summary>
    /// 超链接点击事件
    /// </summary>
    [SerializeField]
    public HrefClickEvent OnHrefClick { get; set; }

    /// <summary>
    /// 图片正则
    /// <para>
    /// name：
    ///     .+? ：任意字符至少出现一次，非贪婪匹配，表示 name 特性至少得有内容（例如，name=test会匹配到name=t，但是name=就无法匹配）
    /// </para>
    /// <para>
    /// size/width：匹配任意数字
    ///     \d*：数字出现0或多次（多次包括1次）
    ///     \.?：小数点.出现0或1次（.本身代表任意字符，所以要用\转义）
    ///     \d+：数字出现1或多次
    ///     %?：%出现0或1次
    ///     \s*?：任意空格（可以不出现，也可以出现n次）
    /// </para>
    /// </summary>
    private static readonly Regex s_ImageRegex =
        new Regex(@"<quad(?:\s+?(?<Property>\S+?)=(?<PropertyValue>""\S+?""))*?\s*?/>", RegexOptions.Singleline);

    /// <summary>
    /// 加载精灵图片方法
    /// </summary>
    public static Func<string, Sprite> funLoadSprite;

    /// <summary>
    /// LinkImageText 构造函数
    /// </summary>
    public LinkImageText()
    {
        OnHrefClick = new HrefClickEvent();
    }

    protected override void Awake()
    {
        base.Awake();
        this.RegisterDirtyMaterialCallback(OnFontMaterialChanged);
        this.font.RequestCharactersInTexture("*", this.fontSize, this.fontStyle);
    }

    protected override void OnDestroy()
    {
        this.UnregisterDirtyMaterialCallback(OnFontMaterialChanged);
        base.OnDestroy();
    }

    private Vector2 GetUnderlineCharUV()
    {
        const char ch = '*';
        CharacterInfo info;
        if (this.font.GetCharacterInfo(ch, out info, this.fontSize, this.fontStyle))
        {
            return (info.uvBottomLeft + info.uvBottomRight + info.uvTopLeft + info.uvTopRight) * 0.25f;
        }
        return Vector2.zero;
    }

    private void OnFontMaterialChanged()
    {
        this.font.RequestCharactersInTexture("*", this.fontSize, this.fontStyle);
    }

    /// <summary>
    /// 重写父类 SetVerticesDirty 方法
    /// </summary>
    public override void SetVerticesDirty()
    {
        base.SetVerticesDirty();
        UpdateQuadImage();
    }

    /// <summary>
    /// 更新 Quad 图片
    /// </summary>
    protected void UpdateQuadImage()
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.GetPrefabType(this) == UnityEditor.PrefabType.Prefab)
        {
            return;
        }
#endif
        Debug.Log("调用 Match()");
        m_OutputText = TextResolution.TextResolution.Match(this.text);
        m_ImagesLastVertexIndexList.Clear();
        foreach (Match match in s_ImageRegex.Matches(m_OutputText))
        {
            Debug.Log(string.Format("图片 match.Index：{0}", match.Index));
            int picIndex = match.Index;
            int endIndex = picIndex * 4 + 3;
            m_ImagesLastVertexIndexList.Add(endIndex); // 添加顶点到 m_ImagesLastVertexIndexList 列表

            m_ImagesPool.RemoveAll(image => image == null);// 移除为 null 的 image
            if (m_ImagesPool.Count == 0)
            {
                GetComponentsInChildren<Image>(m_ImagesPool); // 将子对象（Image）添加进 m_ImagesPool 图片池
            }
            if (m_ImagesLastVertexIndexList.Count > m_ImagesPool.Count)
            {
                DefaultControls.Resources resources = new DefaultControls.Resources();
                GameObject go = DefaultControls.CreateImage(resources);
                go.layer = gameObject.layer;
                RectTransform rt = go.transform as RectTransform;
                if (rt)
                {
                    rt.SetParent(rectTransform);
                    rt.localPosition = Vector3.zero;
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                }
                m_ImagesPool.Add(go.GetComponent<Image>());
            }

            string spriteName = string.Empty; // 图片名称
            float size = 35; // 图片大小（初始值 35）
            for (int i = 0; i < match.Groups["Property"].Captures.Count; i++)
            {
                string propertyValue = match.Groups["PropertyValue"].Captures[i].ToString().Trim('"');
                switch (match.Groups["Property"].Captures[i].ToString())
                {
                    case "name":
                        spriteName = propertyValue;
                        break;
                    case "size":
                        float.TryParse(propertyValue, out size);
                        break;
                }
            }
            Image img = m_ImagesPool[m_ImagesLastVertexIndexList.Count - 1];
            if (img.sprite == null || img.sprite.name != spriteName)
            {
                img.sprite = funLoadSprite != null ? funLoadSprite(spriteName) :
                    Resources.Load<Sprite>(spriteName);
            }
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.enabled = true;
        }

        for (int i = m_ImagesLastVertexIndexList.Count; i < m_ImagesPool.Count; i++)
        {
            if (m_ImagesPool[i])
            {
                m_ImagesPool[i].enabled = false;
            }
        }
    }

    /// <summary>
    /// 重写父类 OnPopulateMesh 方法
    /// </summary>
    /// <param name="vertexHelper"></param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        int indexCount = vertexHelper.currentIndexCount;
        int vertCount = vertexHelper.currentVertCount;
        Debug.Log(string.Format("indexCount：{0}\nvertCount：{1}", indexCount, vertCount));
        Debug.Log("触发OnPopulateMesh");
        Debug.Log(string.Format("m_Text：{0}", m_Text));
        string originText = m_Text;
        Debug.Log(string.Format("m_OutputText：{0}", m_OutputText));
        m_Text = m_OutputText;
        base.OnPopulateMesh(vertexHelper);
        m_Text = originText;

        UIVertex vertex = new UIVertex();
        for (int i = 0; i < m_ImagesLastVertexIndexList.Count; i++)
        {
            int endIndex = m_ImagesLastVertexIndexList[i];
            RectTransform rt = m_ImagesPool[i].rectTransform;
            Vector2 size = rt.sizeDelta;
            if (endIndex < vertexHelper.currentVertCount)
            {
                vertexHelper.PopulateUIVertex(ref vertex, endIndex);
                rt.anchoredPosition = new Vector2(vertex.position.x + size.x / 2, vertex.position.y + size.y / 2); // TODO 可以在这里添加图片偏移量

                // 抹掉左下角的小黑点
                vertexHelper.PopulateUIVertex(ref vertex, endIndex - 3);
                Vector3 vertexPosition = vertex.position;
                for (int j = endIndex, k = endIndex - 3; j > k; j--)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, endIndex);
                    vertex.position = vertexPosition;
                    vertexHelper.SetUIVertex(vertex, j);
                }
            }
        }

        if (m_ImagesLastVertexIndexList.Count != 0)
        {
            m_ImagesLastVertexIndexList.Clear();
        }

        // 获取 uv
        Vector2 uv = GetUnderlineCharUV();
        // 获取当前顶点数
        int currentVertexCount = vertexHelper.currentVertCount;

        // 处理超链接
        OnPopulateMeshLinkHandle(vertexHelper);

        // 处理下划线
        OnPopulateMeshUnderLineHandle(vertexHelper, uv, currentVertexCount);

        // 处理删除线
        OnPopulateMeshDeleteLineHandle(vertexHelper, uv, currentVertexCount);

        int charCount = 0;
        for (int i = 0; i < vertexHelper.currentVertCount; i++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, i);
            if (i % 4 == 0)
            {
               // Debug.Log(string.Format("第 {0} 个矩形", charCount));
                charCount++;
            }
           // Debug.Log(string.Format("vertex.x：{0}\nvertex.y：{1}", vertex.position.x, vertex.position.y));
        }
        Debug.Log(vertexHelper.currentIndexCount);
        Debug.Log(vertexHelper.currentVertCount);
    }

    /// <summary>
    /// 处理超链接
    /// </summary>
    /// <param name="vertexHelper">一个工具类的实例，可以帮助为 UI 生成 mesh</param>
    private void OnPopulateMeshLinkHandle(VertexHelper vertexHelper)
    {
        UIVertex vertex = new UIVertex();
        // 处理超链接包围框
        foreach (HrefInfo hrefInfo in TextResolution.TextResolution.m_HrefInfoList)
        {
            int vertexStartIndex = hrefInfo.startIndex * 4; // 超链接里的文本起始顶点索引
            int vertexEndIndex = (hrefInfo.endIndex - 1) * 4 + 3;// 超链接里的文本终止顶点索引

            Debug.Log("开始处理超链接包围框");
            hrefInfo.boxes.Clear();
            if (vertexStartIndex >= vertexHelper.currentVertCount)
            {
                continue;
            }

            // 将超链接里面的文本顶点索引坐标加入到包围框
            vertexHelper.PopulateUIVertex(ref vertex, vertexStartIndex);
            Debug.Log(string.Format("vertexStartIndex：{0}", vertexStartIndex));
            Vector3 currentPosition = vertex.position;
            Bounds bounds = new Bounds(currentPosition, Vector3.zero);
            Vector3 previousPos = Vector3.zero;
            for (int i = vertexStartIndex, j = vertexEndIndex; i < j; i++)
            {
                if (i >= vertexHelper.currentVertCount)
                {
                    break;
                }
                vertexHelper.PopulateUIVertex(ref vertex, i);
                currentPosition = vertex.position;
                if ((i - vertexStartIndex) % 4 == 2)
                {
                    previousPos = currentPosition;
                }
                Debug.Log(i - vertexStartIndex);
                Debug.Log(string.Format("bounds.min：{0}\nbounds.size：{1}", bounds.min, bounds.size));
                Debug.Log(string.Format("vertex.x：{0}\nvertex.y：{1}", vertex.position.x, vertex.position.y));
                if (previousPos != Vector3.zero && (i - vertexStartIndex) % 4 == 0 && currentPosition.x < previousPos.x && currentPosition.y < previousPos.y) // 换行重新添加包围框
                {
                    hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                    Debug.Log("超链接换行");
                    bounds = new Bounds(currentPosition, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(currentPosition); // 扩展包围框
                }
            }
            hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }

        //Debug.Log(string.Format("vertexHelper.currentVertCount：{0}", vertexHelper.currentVertCount));
        //for (int i = 0; i < vertexHelper.currentVertCount; i++)
        //{
        //    vertexHelper.PopulateUIVertex(ref vertex, i);
        //    //Debug.Log(string.Format("vertex.x：{0}\nvertex.y：{1}", vertex.position.x, vertex.position.y));
        //}
    }

    /// <summary>
    /// 处理下划线
    /// </summary>
    /// <param name="vertexHelper">一个工具类的实例，可以帮助为 UI 生成 mesh</param>
    /// <param name="uv">uv</param>
    /// <param name="currentVertexCount">顶点数</param>
    private void OnPopulateMeshUnderLineHandle(VertexHelper vertexHelper, Vector2 uv, int currentVertexCount)
    {
        UIVertex vertex = new UIVertex();
        foreach (LineInfo lineInfo in TextResolution.TextResolution.m_UnderlineInfoList)
        {
            int vertexStartIndex = lineInfo.startIndex * 4; // 下划线文本起始顶点索引
            int vertexEndIndex = (lineInfo.endIndex - 1) * 4 + 3; // 下划线文本终止顶点索引

            Debug.Log(string.Format("开始处理下划线，vertexStartIndex={0}", vertexStartIndex));
            // 获取第一个字符的底部位置
            //Debug.LogFormat("------veetexStart的值为{0}，vertexHelper的长度为{1}-------", vertexStartIndex+3, vertexHelper.currentIndexCount);
            if (vertexStartIndex + 3 > vertexHelper.currentIndexCount)
            {
                Debug.LogError("标签输入格式有问题，请检查！！！");
                break;
            }
           
            vertexHelper.PopulateUIVertex(ref vertex, vertexStartIndex + 3);
            float currentBottom = vertex.position.y;
            Debug.Log(string.Format("currentBottom 初始值：{0}", currentBottom));
            // 推算起始顶点之前的字符底部位置
            if (vertexStartIndex != 0)
            {
                Vector3 BeforeStartPreviousPos = Vector3.zero;
                for (int i = vertexStartIndex; i >= 0; i--)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    Vector3 currentPosition = vertex.position;
                    if (BeforeStartPreviousPos != Vector3.zero && i % 4 == 2 && currentPosition.x > BeforeStartPreviousPos.x && currentPosition.y > BeforeStartPreviousPos.y) // 换行中断判断
                    {
                        break;
                    }
                    if (i % 4 == 3) // 左下顶点
                    {
                        BeforeStartPreviousPos = currentPosition;
                        if (currentPosition.y < currentBottom)
                        {
                            currentBottom = currentPosition.y;
                            Debug.Log(string.Format("currentBottom 更新（前）：{0}", currentBottom));
                        }
                    }
                }
            }
            // 推算起始顶点之后的字符底部位置
            Vector3 AfterStartPreviousPos = Vector3.zero;
            for (int i = vertexStartIndex; i < currentVertexCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector3 currentPosition = vertex.position;
                Debug.Log( string.Format("currentPosition (x, y)：{0}, {1}", currentPosition.x, currentPosition.y));
                int localIndex = i - vertexStartIndex;
                if (localIndex % 4 == 2)
                {
                    AfterStartPreviousPos = currentPosition;
                }
                if (localIndex % 4 == 3)
                {
                    if (currentPosition.y < currentBottom)
                    {
                        currentBottom = currentPosition.y;
                        Debug.Log(string.Format("currentBottom 更新（后）：{0}", currentBottom));
                    }
                }
                if (AfterStartPreviousPos != Vector3.zero && localIndex % 4 == 0 && currentPosition.x < AfterStartPreviousPos.x && currentPosition.y < AfterStartPreviousPos.y)
                {
                        break;
                }
            }

            vertexHelper.PopulateUIVertex(ref vertex, vertexStartIndex + 3); // 第一个字符的左下顶点
            Vector3 underlineLeftTopPoint = vertex.position;
            Vector3 previousPos = Vector3.zero;
            Vector3 lineHeight = new Vector3(0f, lineInfo.height, 0f);
            for (int i = vertexStartIndex, j = vertexEndIndex; i < j; i++)
            {
                //Debug.LogFormat("------I的值为{0}，vertexHelper的长度为{1}-------", i, vertexHelper.currentIndexCount);
                if (i > vertexHelper.currentIndexCount)
                {
                    Debug.LogError("标签输入格式有问题，请检查！！！");
                    break;
                }
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector3 currentPosition = vertex.position;
                Debug.Log( string.Format("currentPosition (x, y)：{0}, {1}", currentPosition.x, currentPosition.y));
                int localIndex = i - vertexStartIndex;
                if (localIndex % 4 == 2) // 右下顶点
                {
                    previousPos = currentPosition;
                }
                if (localIndex % 4 == 3) // 左下顶点
                {
                    if (currentPosition.y < currentBottom)
                    {
                        currentBottom = currentPosition.y;
                    }
                }
                if (previousPos != Vector3.zero && localIndex % 4 == 0 && currentPosition.x < previousPos.x && currentPosition.y < previousPos.y) // 换行重新计算宽高
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i - 3);
                    AddVertexPos(vertexHelper, vertex, underlineLeftTopPoint, currentBottom + lineInfo.offset, lineInfo.color, uv, lineHeight);

                    Debug.Log("下划线换行");
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    underlineLeftTopPoint = vertex.position;
                    vertexHelper.PopulateUIVertex(ref vertex, i + 3);
                    currentBottom = vertex.position.y;
                }
            }
            Debug.Log(string.Format("lineInfo.color：{0}", lineInfo.color));
            // 推算终止顶点所在行之后的字符底部位置
            Vector3 AfterEndPreviousPos = Vector3.zero;
            for (int i = vertexEndIndex + 1; i < currentVertexCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector3 currentPosition = vertex.position;
                Debug.Log(string.Format("currentPosition (x, y)：{0}, {1}", currentPosition.x, currentPosition.y));
                int localIndex = i - vertexEndIndex -1;
                if (localIndex % 4 == 2)
                {
                    AfterEndPreviousPos = currentPosition;
                }
                if (localIndex % 4 == 3)
                {
                    if (currentPosition.y < currentBottom)
                    {
                        currentBottom = currentPosition.y;
                        Debug.Log(string.Format("currentBottom 更新（后）：{0}", currentBottom));
                    }
                }
                if (AfterEndPreviousPos != Vector3.zero && localIndex % 4 == 0 && currentPosition.x < AfterEndPreviousPos.x && currentPosition.y < AfterEndPreviousPos.y)
                {
                    break;
                }
            }
            vertexHelper.PopulateUIVertex(ref vertex, vertexEndIndex - 1);
            AddVertexPos(vertexHelper, vertex, underlineLeftTopPoint, currentBottom + lineInfo.offset, lineInfo.color, uv, lineHeight);

            Debug.Log(string.Format("vertexHelper.currentVertCount：{0}", vertexHelper.currentVertCount));
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                //Debug.Log(string.Format("vertex.x：{0}\nvertex.y：{1}", vertex.position.x, vertex.position.y));
            }
        }
    }

    /// <summary>
    /// 处理删除线
    /// </summary>
    /// <param name="vertexHelper">一个工具类的实例，可以帮助为 UI 生成 mesh</param>
    /// <param name="uv">uv</param>
    /// <param name="currentVertexCount">顶点数</param>
    private void OnPopulateMeshDeleteLineHandle(VertexHelper vertexHelper, Vector2 uv, int currentVertexCount)
    {
        UIVertex vertex = new UIVertex();
        foreach (LineInfo lineInfo in TextResolution.TextResolution.m_DeletelineInfoList)
        {
            int vertexStartIndex = lineInfo.startIndex * 4; // 删除线文本起始顶点索引
            int vertexEndIndex = (lineInfo.endIndex - 1) * 4 + 3; // 删除线文本终止顶点索引

            Debug.Log(string.Format("开始处理删除线，vertexStartIndex={0}", vertexStartIndex));
            // 获取第一个字符的顶部位置
            vertexHelper.PopulateUIVertex(ref vertex, vertexStartIndex);
            float currentTop = vertex.position.y;
            // 获取第一个字符的底部位置
            vertexHelper.PopulateUIVertex(ref vertex, vertexStartIndex + 3);
            float currentBottom = vertex.position.y;
            Debug.Log(string.Format("currentTop 初始值：{0}", currentTop));
            Debug.Log(string.Format("currentBottom 初始值：{0}", currentBottom));
            // 推算起始顶点之前的字符位置
            if (vertexStartIndex != 0)
            {
                Vector3 BeforeStartPreviousPos = Vector3.zero;
                for (int i = vertexStartIndex; i >= 0; i--)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    Vector3 currentPosition = vertex.position;
                    if (BeforeStartPreviousPos != Vector3.zero && i % 4 == 2 && currentPosition.x > BeforeStartPreviousPos.x && currentPosition.y > BeforeStartPreviousPos.y) // 换行中断判断
                    {
                        break;
                    }
                    if (i % 4 == 3) // 左下顶点
                    {
                        if (currentPosition.y < currentBottom)
                        {
                            currentBottom = currentPosition.y;
                            Debug.Log(string.Format("currentBottom 更新（前）：{0}", currentBottom));
                        }
                    }
                    if (i % 4 == 0) // 左上顶点
                    {
                        BeforeStartPreviousPos = currentPosition;
                        if (currentPosition.y > currentTop)
                        {
                            currentTop = currentPosition.y;
                            Debug.Log(string.Format("currentTop 更新（前）：{0}", currentTop));
                        }
                    }
                }
            }
            // 推算起始顶点之后的字符位置
            Vector3 AfterStartPreviousPos = Vector3.zero;
            for (int i = vertexStartIndex; i < currentVertexCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector3 currentPosition = vertex.position;
                int localIndex = i - vertexStartIndex;
                if (localIndex % 4 == 2)
                {
                    AfterStartPreviousPos = currentPosition;
                }
                if (localIndex % 4 == 3)
                {
                    if (currentPosition.y < currentBottom)
                    {
                        currentBottom = currentPosition.y;
                        Debug.Log(string.Format("currentBottom 更新（后）：{0}", currentBottom));
                    }
                }
                if (localIndex % 4 == 0) // 左上顶点
                {
                    if (currentPosition.y > currentTop)
                    {
                        currentTop = currentPosition.y;
                        Debug.Log(string.Format("currentTop 更新（后）：{0}", currentTop));
                    }
                }
                if (AfterStartPreviousPos != Vector3.zero && localIndex % 4 == 0 && currentPosition.x < AfterStartPreviousPos.x && currentPosition.y < AfterStartPreviousPos.y)
                {
                    break;
                }
            }

            vertexHelper.PopulateUIVertex(ref vertex, vertexStartIndex + 3); // 第一个字符的左下顶点
            Vector3 deletelineLeftTopPoint = vertex.position;
            Vector3 previousPos = Vector3.zero;
            Vector3 lineHeight = new Vector3(0f, lineInfo.height, 0f);
            float calculationPositionY = 0;
            for (int i = vertexStartIndex, j = vertexEndIndex; i < j; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector3 currentPosition = vertex.position;
                Debug.Log(string.Format("currentPosition.x：{0}", currentPosition.x));
                Debug.Log(string.Format("currentPosition.y：{0}", currentPosition.y));
                int localIndex = i - vertexStartIndex;
                if (localIndex % 4 == 2) // 右下顶点
                {
                    previousPos = currentPosition;
                }
                if (localIndex % 4 == 0) // 左上顶点
                {
                    if (currentPosition.y > currentTop)
                    {
                        currentTop = currentPosition.y;
                    }
                }
                if (localIndex % 4 == 3) // 左下顶点
                {
                    if (currentPosition.y < currentBottom)
                    {
                        currentBottom = currentPosition.y;
                    }
                }
                if (previousPos != Vector3.zero && localIndex % 4 == 0 && currentPosition.x < previousPos.x && currentPosition.y < previousPos.y) // 换行重新计算宽高
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i - 3);
                    calculationPositionY = (currentBottom + currentTop + lineInfo.height) / 2 + lineInfo.offset;
                    AddVertexPos(vertexHelper, vertex, deletelineLeftTopPoint, calculationPositionY, lineInfo.color, uv, lineHeight);

                    Debug.Log("删除线换行");
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    deletelineLeftTopPoint = vertex.position;
                    currentTop = vertex.position.y;
                    Debug.Log(string.Format("currentTop：{0}", currentTop));
                    vertexHelper.PopulateUIVertex(ref vertex, i + 3);
                    currentBottom = vertex.position.y;
                    Debug.Log(string.Format("currentBottom：{0}", currentBottom));
                }
            }
            Debug.Log(string.Format("lineInfo.color：{0}", lineInfo.color));
            // 推算终止顶点之后的字符位置
            Vector3 AfterEndPreviousPos = Vector3.zero;
            for (int i = vertexEndIndex + 1; i < currentVertexCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector3 currentPosition = vertex.position;
                int localIndex = i - vertexEndIndex - 1;
                if (localIndex % 4 == 2)
                {
                    AfterEndPreviousPos = currentPosition;
                }
                if (localIndex % 4 == 3)
                {
                    if (currentPosition.y < currentBottom)
                    {
                        currentBottom = currentPosition.y;
                        Debug.Log(string.Format("currentBottom 更新（后）：{0}", currentBottom));
                    }
                }
                if (localIndex % 4 == 0) // 左上顶点
                {
                    if (currentPosition.y > currentTop)
                    {
                        currentTop = currentPosition.y;
                        Debug.Log(string.Format("currentTop 更新（后）：{0}", currentTop));
                    }
                }
                if (AfterEndPreviousPos != Vector3.zero && localIndex % 4 == 0 && currentPosition.x < AfterEndPreviousPos.x && currentPosition.y < AfterEndPreviousPos.y)
                {
                    break;
                }
            }
            vertexHelper.PopulateUIVertex(ref vertex, vertexEndIndex - 1);
            calculationPositionY = (currentBottom + currentTop + lineInfo.height) / 2 + lineInfo.offset;
            AddVertexPos(vertexHelper, vertex, deletelineLeftTopPoint, calculationPositionY, lineInfo.color, uv, lineHeight);


            Debug.Log(string.Format("vertexHelper.currentVertCount：{0}", vertexHelper.currentVertCount));
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                //Debug.Log(string.Format("vertex.x：{0}\nvertex.y：{1}", vertex.position.x, vertex.position.y));
            }
        }
    }

    /// <summary>
    /// 添加顶点坐标公共方法
    /// </summary>
    /// <param name="vertexHelper">VertexHelper</param>
    /// <param name="vertex">UIVertex</param>
    /// <param name="leftTopPoint">vertex.position</param>
    /// <param name="currentY">currentPosition.y</param>
    /// <param name="color">LineInfo中的颜色</param>
    /// <param name="uv">Vector</param>
    /// <param name="lineHeight">new Vector3(0f, lineInfo.height, 0f);</param>
    private static void AddVertexPos(VertexHelper vertexHelper, UIVertex vertex, Vector3 leftTopPoint, float currentY, Color color, Vector2 uv, Vector3 lineHeight)
    {
        vertexHelper.AddVert(new Vector3(leftTopPoint.x, currentY, 0f), color, uv);
        vertexHelper.AddVert(new Vector3(vertex.position.x, currentY, 0f), color, uv);
        vertexHelper.AddVert(new Vector3(vertex.position.x, currentY, 0f) - lineHeight, color, uv);
        vertexHelper.AddVert(new Vector3(leftTopPoint.x, currentY, 0f) - lineHeight, color, uv);
        int currentVertCount = vertexHelper.currentVertCount;
        vertexHelper.AddTriangle(currentVertCount - 4, currentVertCount - 3, currentVertCount - 2);
        vertexHelper.AddTriangle(currentVertCount - 4, currentVertCount - 2, currentVertCount - 1);
    }

    /// <inheritdoc />
    /// <summary>
    /// 点击事件检测是否点击到超链接文本
    /// </summary>
    /// <param name="pointerEventData">点击事件数据</param>
    public void OnPointerClick(PointerEventData pointerEventData)
    {
        Vector2 pointerPosition;
        // 将点击的屏幕坐标转换为本地坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            this.rectTransform, pointerEventData.position, pointerEventData.pressEventCamera, out pointerPosition);

        // 遍历 m_HrefInfoList（超链接信息列表）
        foreach (HrefInfo hrefInfo in TextResolution.TextResolution.m_HrefInfoList)
        {
            // 获取超链接信息的 boxes
            List<Rect> boxes = hrefInfo.boxes;
            // 遍历每个box判断点击位置是否在box里，如果是，则反射 href
            for (int i = 0; i < boxes.Count; ++i)
            {
                if (boxes[i].Contains(pointerPosition))
                {
                    OnHrefClick.Invoke(hrefInfo.href);
                    return;
                }
            }
        }
    }

}
