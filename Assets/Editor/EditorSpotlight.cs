using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

public class EditorSpotlight : EditorWindow, IHasCustomMenu
{
    static class Styles
    {
        public static readonly GUIStyle inputFieldStyle;
        public static readonly GUIStyle placeholderStyle;
        public static readonly GUIStyle resultLabelStyle;
        public static readonly GUIStyle entryEven;
        public static readonly GUIStyle entryOdd;

        public static readonly string proSkinHighlightColor = "eeeeee";
        public static readonly string proSkinNormalColor = "cccccc";

        public static readonly string personalSkinHighlightColor = "eeeeee";
        public static readonly string personalSkinNormalColor = "222222";

        static Styles()
        {
            inputFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                contentOffset = new Vector2(10, 10),
                fontSize = 32,
                focused = new GUIStyleState()
            };

            placeholderStyle = new GUIStyle(inputFieldStyle) {normal =
            {
                textColor = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, .2f) : new Color(.2f, .2f, .2f, .4f)
            }};


            resultLabelStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            entryOdd = new GUIStyle("CN EntryBackOdd");
            entryEven = new GUIStyle("CN EntryBackEven");
        }
    }

    [MenuItem("Window/Spotlight %k")]
    private static void Init()
    {
        var window = CreateInstance<EditorSpotlight>();
        window.titleContent = new GUIContent("Spotlight");
        var pos = window.position;
        pos.height = BaseHeight;
        pos.xMin = Screen.currentResolution.width / 2 - 500 / 2;
        pos.yMin = Screen.currentResolution.height * .3f;
        window.position = pos;
        window.EnforceWindowSize();
        window.ShowUtility();

        window.Reset();
    }

    [Serializable] private class SearchHistory : ISerializationCallbackReceiver
    {
        public readonly Dictionary<string, int> clicks = new Dictionary<string, int>();

        [SerializeField] List<string> clickKeys = new List<string>();
        [SerializeField] List<int> clickValues = new List<int>();

        public void OnBeforeSerialize()
        {
            clickKeys.Clear();
            clickValues.Clear();

            int i = 0;
            foreach (var pair in clicks)
            {
                clickKeys.Add(pair.Key);
                clickValues.Add(pair.Value);
                i++;
            }
        }

        public void OnAfterDeserialize()
        {
            clicks.Clear();
            for (var i = 0; i < clickKeys.Count; i++)
                clicks.Add(clickKeys[i], clickValues[i]);
        }
    }

    const string PlaceholderInput = "Open Asset...";
    const string SearchHistoryKey = "SearchHistoryKey";
    public const int BaseHeight = 90;

    List<string> hits = new List<string>();
    string input;
    int selectedIndex = 0;

    SearchHistory history;

    void Reset()
    {
        input = "";
        hits.Clear();
        var json = EditorPrefs.GetString(SearchHistoryKey, JsonUtility.ToJson(new SearchHistory()));
        history = JsonUtility.FromJson<SearchHistory>(json);
        Focus();
    }

    void OnLostFocus()
    {
        Close();
    }

    void OnGUI()
    {
        EnforceWindowSize();
        HandleEvents();


        GUILayout.BeginHorizontal();
        GUILayout.Space(15);
        GUILayout.BeginVertical();
        GUILayout.Space(15);

        GUI.SetNextControlName("SpotlightInput");
        var prevInput = input;
        input = GUILayout.TextField(input, Styles.inputFieldStyle, GUILayout.Height(60));
        EditorGUI.FocusTextInControl("SpotlightInput");

        if (input != prevInput)
            ProcessInput();

        if (selectedIndex >= hits.Count)
            selectedIndex = hits.Count - 1;
        else if (selectedIndex <= 0)
            selectedIndex = 0;

        if (string.IsNullOrEmpty(input))
            GUI.Label(GUILayoutUtility.GetLastRect(), PlaceholderInput, Styles.placeholderStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Space(6);

        if (!string.IsNullOrEmpty(input))
            VisualizeHits();

        GUILayout.Space(6);
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        GUILayout.EndVertical();
        GUILayout.Space(15);
        GUILayout.EndHorizontal();
    }

    void ProcessInput()
    {
        input = input.ToLower();
        var assetHits = AssetDatabase.FindAssets(input) ?? new string[0];
        hits = assetHits.ToList();

        // Sort the search hits
        hits.Sort((x, y) =>
        {
            // Generally, use click history
            int xScore;
            history.clicks.TryGetValue(x, out xScore);
            int yScore;
            history.clicks.TryGetValue(y, out yScore);

            // Value files that actually begin with the search input higher
            if (xScore != 0 && yScore != 0)
            {
                var xName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(x)).ToLower();
                var yName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(y)).ToLower();
                if (xName.StartsWith(input) && !yName.StartsWith(input))
                    return -1;
                if (!xName.StartsWith(input) && yName.StartsWith(input))
                    return 1;
            }

            return yScore - xScore;
        });

        hits = hits.Take(10).ToList();
    }

    void HandleEvents()
    {
        var current = Event.current;

        if (current.type == EventType.keyDown)
        {
            if (current.keyCode == KeyCode.UpArrow)
            {
                current.Use();
                selectedIndex--;
            }
            else if (current.keyCode == KeyCode.DownArrow)
            {
                current.Use();
                selectedIndex++;
            }
            else if (current.keyCode == KeyCode.Return)
            {
                OpenSelectedAssetAndClose();
                current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Escape)
                Close();
        }
    }

    void VisualizeHits()
    {
        var current = Event.current;

        var windowRect = this.position;
        windowRect.height = BaseHeight;

        GUILayout.BeginVertical();
        GUILayout.Space(5);

        if (hits.Count == 0)
        {
            windowRect.height += EditorGUIUtility.singleLineHeight;
            GUILayout.Label("No hits");
        }

        for (int i = 0; i < hits.Count; i++)
        {
            var style = i % 2 == 0 ? Styles.entryOdd : Styles.entryEven;

            GUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight * 2),
                GUILayout.ExpandWidth(true));

            var elementRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            GUILayout.EndHorizontal();

            windowRect.height += EditorGUIUtility.singleLineHeight * 2;

            if (current.type == EventType.Repaint)
            {
                style.Draw(elementRect, false, false, i == selectedIndex, false);
                var assetPath = AssetDatabase.GUIDToAssetPath(hits[i]);
                var icon = AssetDatabase.GetCachedIcon(assetPath);

                var iconRect = elementRect;
                iconRect.x = 30;
                iconRect.width = 25;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);


                var assetName = Path.GetFileName(assetPath);
                StringBuilder coloredAssetName = new StringBuilder();

                int start = assetName.ToLower().IndexOf(input);
                int end = start + input.Length;

                var highlightColor = EditorGUIUtility.isProSkin
                    ? Styles.proSkinHighlightColor
                    : Styles.personalSkinHighlightColor;

                var normalColor = EditorGUIUtility.isProSkin
                    ? Styles.proSkinNormalColor
                    : Styles.personalSkinNormalColor;

                // Sometimes the AssetDatabase finds assets without the search input in it.
                if (start == -1)
                    coloredAssetName.Append(string.Format("<color=#{0}>{1}</color>", normalColor, assetName));
                else
                {
                    if (0 != start)
                        coloredAssetName.Append(string.Format("<color=#{0}>{1}</color>",
                            normalColor, assetName.Substring(0, start)));

                    coloredAssetName.Append(
                        string.Format("<color=#{0}><b>{1}</b></color>", highlightColor, assetName.Substring(start, end - start)));

                    if (end != assetName.Length - end)
                        coloredAssetName.Append(string.Format("<color=#{0}>{1}</color>",
                            normalColor, assetName.Substring(end, assetName.Length - end)));
                }

                var labelRect = elementRect;
                labelRect.x = 60;
                GUI.Label(labelRect, coloredAssetName.ToString(), Styles.resultLabelStyle);
            }

            if (current.type == EventType.mouseDown && elementRect.Contains(current.mousePosition))
            {
                selectedIndex = i;
                if (current.clickCount == 2)
                    OpenSelectedAssetAndClose();
                else
                {
                    Selection.activeObject = GetSelectedAsset();
                    EditorGUIUtility.PingObject(Selection.activeGameObject);
                }

                Repaint();
            }
        }

        windowRect.height += 5;
        position = windowRect;

        GUILayout.EndVertical();
    }

    void OpenSelectedAssetAndClose()
    {
        Close();

        AssetDatabase.OpenAsset(GetSelectedAsset());

        var guid = hits[selectedIndex];
        if (!history.clicks.ContainsKey(guid))
            history.clicks[guid] = 0;

        history.clicks[guid]++;
        EditorPrefs.SetString(SearchHistoryKey, JsonUtility.ToJson(history));
    }

    UnityEngine.Object GetSelectedAsset()
    {
        var assetPath = AssetDatabase.GUIDToAssetPath(hits[selectedIndex]);
        return (AssetDatabase.LoadMainAssetAtPath(assetPath));
    }

    public void EnforceWindowSize()
    {
        var pos = position;
        pos.width = 500;
        pos.height = BaseHeight;
        position = pos;
    }

    public void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(new GUIContent("Reset history"), false, () =>
        {
            EditorPrefs.SetString(SearchHistoryKey, JsonUtility.ToJson(new SearchHistory()));
            Reset();
        });

        menu.AddItem(new GUIContent("Output history"), false, () =>
        {
            var json = EditorPrefs.GetString(SearchHistoryKey, JsonUtility.ToJson(new SearchHistory()));
            Debug.Log(json);
        });
    }
}
