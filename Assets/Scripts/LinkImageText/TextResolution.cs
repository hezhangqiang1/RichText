using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TextResolution
{
    internal class TextResolution
    {
        /// <summary>
        /// 下划线信息列表
        /// </summary>
        internal static readonly List<LineInfo> m_UnderlineInfoList = new List<LineInfo>();

        /// <summary>
        /// 删除线信息列表
        /// </summary>
        internal static readonly List<LineInfo> m_DeletelineInfoList = new List<LineInfo>();

        /// <summary>
        /// 超链接信息列表
        /// </summary>
        internal static readonly List<HrefInfo> m_HrefInfoList = new List<HrefInfo>();

        private const string s_HrefOpenTagRegexPattern =
            @"<a.*?>";

        private const string s_HrefCloseTagRegexPattern =
            @"</a>";

        private const string s_ExceptHrefTagRegexPattern = @"(?!(?:<a.*?>|</a>|<color=.*?>|</color>|<size=.*?>|</size>|<material=.*?>|</material>|</?b>|</?i>))(?:<[^/]*?>|</.+?>)";

        /// <summary>
        /// 超链接标签匹配正则表达式 pattern
        /// </summary>
        private const string s_HrefTagRegexPattern =
            @"(?<OpenTag><(?<Tag>a)(?:\s+?(?<Property>\S+?)=(?<PropertyValue>""\S+?""))*?\s*?>)(?<Content>.*?)(?<CloseTag></a>)";

        /// <summary>
        /// 除了超链接之外的标签匹配正则表达式
        /// </summary>
        private static readonly Regex s_ExceptHrefTagRegex =
            new Regex(@"(?!(?:<a.*?>|</a>|<color=.*?>|</color>|<size=.*?>|</size>|<material=.*?>|</material>|</?b>|</?i>))(?:<[^/]*?>|</.+?>)", RegexOptions.Singleline);

        /// <summary>
        /// 标签匹配正则表达式
        /// <para>
        /// 示例：&lt;a href="test"&gt;链&lt;d height="3" offset="0" color="#E6FF00FF"&gt;Link&lt;/d&gt;接&lt;/a&gt;
        /// </para>
        /// <para>
        /// 捕获：
        /// </para>
        /// <para>
        /// OpenTag：&lt;a href="test"&gt;
        /// </para>
        /// <para>
        /// Tag：a
        /// </para>
        /// <para>
        /// Property：href
        /// </para>
        /// <para>
        /// PropertyValue："test"
        /// </para>
        /// <para>
        /// Content：链&lt;d height="3" offset="0" color="#E6FF00FF"&gt;Link&lt;/d&gt;接
        /// </para>
        /// <para>
        /// CloseTag：&lt;/a&gt;
        /// </para>
        /// </summary>
        private const string s_TagRegexPattern =
            @"(?<OpenTag><(?<Tag>[^<>bi]*?)(?:\s+?(?<Property>\S+?)=(?<PropertyValue>""\S+?""))*?\s*?>)(?<Content>.*?)(?<CloseTag></\k<Tag>>)";

        /// <summary>
        /// 单个标签匹配正则表达式
        /// <para>
        /// 示例：&lt;u height="2" offset="0" color="#EC1010FF"&gt;Link&lt;/u&gt;
        /// </para>
        /// <para>
        /// 捕获：
        /// </para>
        /// <para>
        /// &lt;u height="2" offset="0" color="#EC1010FF"&gt;
        /// </para>
        /// <para>
        /// &lt;/u&gt;
        /// </para>
        /// </summary>
        private const string s_SingleTagRegexPattern = @"(?!(?:<color=.*?>|</color>|<size=.*?>|</size>|<material=.*?>|</material>|</?b>|</?i>))(?:<[^/]*?>|</.+?>)";

        /// <summary>
        /// 匹配标签，并返回无标签的字符串
        /// </summary>
        /// <param name="inputString">输入字符串</param>
        /// <returns>无标签字符串</returns>
        internal static string Match(string inputString)
        {
            // 清空下划线、删除线、超链接信息列表
            m_UnderlineInfoList.Clear();
            m_DeletelineInfoList.Clear();
            m_HrefInfoList.Clear();
            inputString = MatchHrefTag(inputString); // 匹配标签
            inputString = MatchTag(inputString); // 匹配标签
            string outputString = Regex.Replace(inputString, s_SingleTagRegexPattern,
                string.Empty, RegexOptions.Singleline); // 直接返回去掉所有非自闭合标签的字符串(即最终结果)
            return outputString;
        }

        /// <summary>
        /// 匹配链接标签
        /// </summary>
        /// <param name="inputString">输入字符串</param>
        private static string MatchHrefTag(string inputString)
        {
            while (true)
            {
                Match match = Regex.Match(inputString, s_HrefTagRegexPattern, RegexOptions.Singleline);
                if (match.Success)
                {
                    string beforeMatchString = inputString.Substring(0, match.Index);
                    int beforeMatchStringTagLengthSum = TagLengthSum(beforeMatchString);
                    string afterMatchString = inputString.Substring(match.Index + match.Length);
                    string contentString = match.Groups["Content"].ToString();
                    string cleanTagContentString = Regex.Replace(contentString, s_SingleTagRegexPattern, string.Empty,
                        RegexOptions.Singleline); // 无标签的 ContentString
                    HrefInfo hrefInfo = new HrefInfo
                    {
                        startIndex = match.Index - beforeMatchStringTagLengthSum + 12,
                        endIndex = match.Index - beforeMatchStringTagLengthSum + 12 + cleanTagContentString.Length
                    };
                    LinkHandle(match, hrefInfo);
                    inputString = beforeMatchString + "<color=blue>" + contentString + "</color>" + afterMatchString; // inputString 去掉已经处理的标签
                }
                else
                {
                    return inputString;
                }
            }
        }

        /// <summary>
        /// 计算除了Href以外的标签的长度总和
        /// </summary>
        /// <param name="inputString">输入字符串</param>
        /// <returns>标签的长度总和</returns>
        private static int TagLengthSum(string inputString)
        {
            int lengthSum = 0;
            foreach (Match match in s_ExceptHrefTagRegex.Matches(inputString))
            {
                lengthSum += match.Length;
            }
            return lengthSum;
        }

        /// <summary>
        /// 链接处理
        /// </summary>
        /// <param name="match">匹配</param>
        /// <param name="hrefInfo"></param>
        private static void LinkHandle(Match match, HrefInfo hrefInfo)
        {
            for (int i = 0; i < match.Groups["Property"].Captures.Count; i++)
            {
                string propertyValue = match.Groups["PropertyValue"].Captures[i].ToString().Trim('"');
                switch (match.Groups["Property"].Captures[i].ToString())
                {
                    case "href":
                        hrefInfo.href = propertyValue;
                        break;
                }
            }
            m_HrefInfoList.Add(hrefInfo);
        }

        /// <summary>
        /// 匹配链接以外的标签
        /// </summary>
        /// <param name="inputString">输入字符串</param>
        private static string MatchTag(string inputString)
        {
            while (true)
            {
                Match match = Regex.Match(inputString, s_TagRegexPattern, RegexOptions.Singleline);
                if (match.Success)
                {
                    string tagString = match.Groups["Tag"].ToString();
                    string contentString = match.Groups["Content"].ToString();
                    string cleanTagContentString = Regex.Replace(contentString, s_SingleTagRegexPattern, string.Empty,
                        RegexOptions.Singleline); // 无标签的 ContentString
                    switch (tagString)
                    {
                        case "u":
                            UnderlineHandle(match, cleanTagContentString.Length);
                            break;
                        case "d":
                            DeletelineHandle(match, cleanTagContentString.Length);
                            break;
                        default:
                            Debug.Log("未知标签");
                            break;
                    }
                    inputString = inputString.Substring(0, match.Index) + contentString +
                                  inputString.Substring(match.Index + match.Length); // inputString 去掉已经处理的标签
                }
                else
                {
                    return inputString;
                }
            }
        }

        /// <summary>
        /// 下划线处理
        /// </summary>
        /// <param name="match">匹配</param>
        /// <param name="cleanTagContentStringLength">清除了标签的 Content 长度</param>
        private static void UnderlineHandle(Match match, int cleanTagContentStringLength)
        {
            LineInfo lineInfo = new LineInfo
            {
                startIndex = match.Index,
                endIndex = match.Index + cleanTagContentStringLength
            };
            for (int i = 0; i < match.Groups["Property"].Captures.Count; i++)
            {
                string propertyValue = match.Groups["PropertyValue"].Captures[i].ToString().Trim('"');
                switch (match.Groups["Property"].Captures[i].ToString())
                {
                    case "height":
                        float.TryParse(propertyValue, out lineInfo.height);
                        break;
                    case "offset":
                        float.TryParse(propertyValue, out lineInfo.offset);
                        break;
                    case "color":
                        ColorUtility.TryParseHtmlString(propertyValue, out lineInfo.color);
                        break;
                }
            }
            m_UnderlineInfoList.Add(lineInfo);
        }

        /// <summary>
        /// 删除线处理
        /// </summary>
        /// <param name="match">匹配</param>
        /// <param name="cleanTagContentStringLength">清除了标签的 Content 长度</param>
        private static void DeletelineHandle(Match match, int cleanTagContentStringLength)
        {
            LineInfo lineInfo = new LineInfo
            {
                startIndex = match.Index,
                endIndex = match.Index + cleanTagContentStringLength
            };
            for (int i = 0; i < match.Groups["Property"].Captures.Count; i++)
            {
                string propertyValue = match.Groups["PropertyValue"].Captures[i].ToString().Trim('"');
                switch (match.Groups["Property"].Captures[i].ToString())
                {
                    case "height":
                        float.TryParse(propertyValue, out lineInfo.height);
                        break;
                    case "offset":
                        float.TryParse(propertyValue, out lineInfo.offset);
                        break;
                    case "color":
                        ColorUtility.TryParseHtmlString(propertyValue, out lineInfo.color);
                        break;
                }
            }
            m_DeletelineInfoList.Add(lineInfo);
        }

    }

    /// <summary>
    /// 超链接信息类
    /// <para>
    /// startIndex：开始索引
    /// </para>
    /// <para>
    /// endIndex：结束索引
    /// </para>
    /// <para>
    /// href：链接地址
    /// </para>
    /// <para>
    /// boxes 点击矩形区域列表
    /// </para>
    /// </summary>
    internal class HrefInfo
    {
        public int startIndex;

        public int endIndex;

        public string href;

        public readonly List<Rect> boxes = new List<Rect>();
    }

    /// <summary>
    /// 下划线/删除线信息类
    /// <para>
    /// startIndex：开始索引
    /// </para>
    /// <para>
    /// endIndex：结束索引
    /// </para>
    /// <para>
    /// height：高度，默认为 1
    /// </para>
    /// <para>
    /// offset：偏移量，默认为 0
    /// </para>
    /// <para>
    /// color：颜色，默认为黑色
    /// </para>
    /// </summary>
    internal class LineInfo
    {
        public int startIndex;

        public int endIndex;

        public float height = 1;

        public float offset = 0;

        public Color color = Color.black;
    }
}
