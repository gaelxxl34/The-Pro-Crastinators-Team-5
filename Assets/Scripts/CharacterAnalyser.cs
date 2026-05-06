using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterAnalyser : MonoBehaviour
{
    [Header("Scroll View")]
    public RectTransform scrollContent;
    public GameObject entryPrefab;

    [Header("Analysis")]
    [Tooltip("Number of equal-width segments to split the document into.")]
    public int numSegments = 10;

    [Tooltip("Minimum number of non-sentence-initial occurrences for a capitalized word to be considered a name.")]
    public int minProperNounEvidence = 2;

    [Tooltip("Maximum number of characters to display/return.")]
    public int maxCharacters = 50;

    // Fired when analysis completes — GraphController listens to this
    public static event Action<CharacterData[]> OnCharactersAnalysed;

    private string PaperPath =>
        Path.Combine(Application.dataPath, "Papers", "paper.txt");

    // Common English words / titles that frequently appear capitalized but aren't names.
    private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","if","then","so","yet","for","nor","as","at","by","of","on","in","to","from",
        "with","without","about","into","onto","upon","over","under","through","between","among","across","after",
        "before","behind","below","above","around","near","off","out","up","down","this","that","these","those",
        "i","me","my","mine","myself","we","us","our","ours","ourselves","you","your","yours","yourself","yourselves",
        "he","him","his","himself","she","her","hers","herself","it","its","itself","they","them","their","theirs",
        "themselves","what","which","who","whom","whose","when","where","why","how","there","here","is","am","are",
        "was","were","be","been","being","have","has","had","having","do","does","did","doing","will","would","shall",
        "should","may","might","must","can","could","ought","not","no","yes","yeah","oh","ah","well","now","just",
        "still","ever","never","always","sometimes","often","rarely","perhaps","maybe","really","very","quite","too",
        "also","even","only","again","once","twice","more","most","less","least","much","many","some","any","all",
        "each","every","both","either","neither","other","another","such","same","one","two","three","four","five",
        "six","seven","eight","nine","ten","first","second","third","next","last","new","old","good","bad","great",
        "long","short","high","low","big","little","young","mr","mrs","ms","miss","dr","sir","madam","mister",
        "chapter","monday","tuesday","wednesday","thursday","friday","saturday","sunday","january","february","march",
        "april","may","june","july","august","september","october","november","december","god","lord",
    };

    // Word = letters, plus internal apostrophes/hyphens.
    private static readonly Regex WordRegex = new Regex(@"[A-Za-z][A-Za-z'\-]*", RegexOptions.Compiled);

    private void Start()
    {
        try
        {
            if (!File.Exists(PaperPath))
            {
                Debug.LogError($"[CharacterAnalyser] File not found: {PaperPath}");
                return;
            }

            string text = File.ReadAllText(PaperPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[CharacterAnalyser] paper.txt is empty.");
                return;
            }

            CharacterData[] characters = AnalyseText(text);
            Debug.Log($"[CharacterAnalyser] Found {characters.Length} character(s).");

            PopulateScrollView(characters);
            OnCharactersAnalysed?.Invoke(characters);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterAnalyser] Analysis failed: {ex}");
        }
    }

    // ── Pure-C# name extraction ───────────────────────────────────────────────
    private CharacterData[] AnalyseText(string text)
    {
        int segCount = Mathf.Max(1, numSegments);

        // 1. Tokenise the document, recording each word's position and whether it's at sentence start.
        var tokens = TokeniseWithSentenceFlags(text);

        // 2. Score each distinct capitalised word: how many times it appears
        //    NOT at the start of a sentence (strong evidence it's a proper noun).
        var midSentenceCaps = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var t in tokens)
        {
            if (t.IsCapitalized && !t.AtSentenceStart)
                midSentenceCaps[t.Word] = midSentenceCaps.TryGetValue(t.Word, out var n) ? n + 1 : 1;
        }

        bool IsNameWord(string word)
        {
            if (word.Length < 2) return false;
            if (!char.IsUpper(word[0])) return false;
            if (StopWords.Contains(word)) return false;
            return midSentenceCaps.TryGetValue(word, out int hits) && hits >= minProperNounEvidence;
        }

        // 3. Walk the token stream and group consecutive name-words into spans
        //    (e.g. "Tom" + "Buchanan" → "Tom Buchanan"). Each unique span is a candidate.
        var spanCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int i = 0;
        while (i < tokens.Count)
        {
            if (!IsNameWord(tokens[i].Word)) { i++; continue; }

            int j = i + 1;
            while (j < tokens.Count
                   && tokens[j].FollowsImmediately
                   && IsNameWord(tokens[j].Word))
            {
                j++;
            }

            string span = string.Join(" ", tokens.GetRange(i, j - i).Select(t => t.Word));
            spanCounts[span] = spanCounts.TryGetValue(span, out var c) ? c + 1 : 1;

            // Also register the head word so single mentions get tracked too.
            if (j - i > 1)
            {
                string head = tokens[i].Word;
                spanCounts[head] = spanCounts.TryGetValue(head, out var h) ? h + 1 : 1;
            }

            i = j;
        }

        if (spanCounts.Count == 0) return Array.Empty<CharacterData>();

        // 4. Keep the most prominent names.
        var names = spanCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(Mathf.Max(1, maxCharacters))
            .Select(kv => kv.Key)
            .ToList();

        // 5. Count occurrences of each name in each segment using whole-word matching.
        int segSize = Mathf.Max(1, text.Length / segCount);
        var segments = new string[segCount];
        for (int s = 0; s < segCount; s++)
        {
            int start = s * segSize;
            int len = (s == segCount - 1) ? text.Length - start : segSize;
            segments[s] = text.Substring(start, len);
        }

        var result = new List<CharacterData>(names.Count);
        foreach (string name in names)
        {
            var pattern = new Regex(@"\b" + Regex.Escape(name) + @"\b");
            int[] counts = new int[segCount];
            for (int s = 0; s < segCount; s++)
                counts[s] = pattern.Matches(segments[s]).Count;
            result.Add(new CharacterData { name = name, counts = counts });
        }

        return result.ToArray();
    }

    private struct Token
    {
        public string Word;
        public bool IsCapitalized;
        public bool AtSentenceStart;
        public bool FollowsImmediately; // true if only whitespace separates this word from the previous one
    }

    private static List<Token> TokeniseWithSentenceFlags(string text)
    {
        var list = new List<Token>(text.Length / 5);
        bool nextIsSentenceStart = true;
        int prevEnd = -1;

        foreach (Match m in WordRegex.Matches(text))
        {
            string word = m.Value;

            // Determine if only whitespace lies between the previous word's end and this word's start.
            bool follows = false;
            if (prevEnd >= 0)
            {
                follows = true;
                for (int k = prevEnd; k < m.Index; k++)
                {
                    if (!char.IsWhiteSpace(text[k])) { follows = false; break; }
                }
            }

            list.Add(new Token
            {
                Word = word,
                IsCapitalized = char.IsUpper(word[0]),
                AtSentenceStart = nextIsSentenceStart,
                FollowsImmediately = follows,
            });

            // Look ahead through punctuation/whitespace until the next letter to decide
            // whether the next word is at a sentence start.
            nextIsSentenceStart = false;
            int scanEnd = m.Index + m.Length;
            prevEnd = scanEnd;
            for (int k = scanEnd; k < text.Length; k++)
            {
                char ch = text[k];
                if (char.IsLetter(ch)) break;
                if (ch == '.' || ch == '!' || ch == '?' || ch == '\n' || ch == '\r')
                    nextIsSentenceStart = true;
            }
        }

        return list;
    }

    // ── Scroll view ───────────────────────────────────────────────────────────
    private void PopulateScrollView(CharacterData[] characters)
    {
        if (scrollContent == null || entryPrefab == null) return;

        foreach (Transform child in scrollContent)
            Destroy(child.gameObject);

        if (characters.Length == 0) { SpawnEntry("(no characters found)"); return; }

        foreach (var c in characters) SpawnEntry(c.name);

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
    }

    private void SpawnEntry(string label)
    {
        var obj = Instantiate(entryPrefab, scrollContent);
        var tmp = obj.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = label;
    }

    // ── Data type (kept compatible with GraphController) ─────────────────────
    [Serializable] public class CharacterData { public string name; public int[] counts; }
}
