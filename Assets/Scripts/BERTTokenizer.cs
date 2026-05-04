using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class BERTTokenizer
{
    private Dictionary<string, int> vocab = new();
    private Dictionary<int, string> reverseVocab = new();

    private const int CLS = 101;
    private const int SEP = 102;
    private const int UNK = 100;
    private const int MAX_LENGTH = 512;
    private const int STRIDE = 64;

    public bool IsLoaded { get; private set; } = false;

    public void Load(string vocabPath)
    {
        if (!File.Exists(vocabPath))
        {
            Debug.LogError($"[BERTTokenizer] vocab.txt not found at: {vocabPath}");
            return;
        }

        string[] lines = File.ReadAllLines(vocabPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string token = lines[i].Trim();
            vocab[token] = i;
            reverseVocab[i] = token;
        }

        IsLoaded = true;
        Debug.Log($"[BERTTokenizer] Loaded vocab with {vocab.Count} tokens, reverse vocab with {reverseVocab.Count} entries.");
    }

    public string IdToRawToken(int id)
    {
        if (reverseVocab.TryGetValue(id, out string token))
            return token; // Keep ## prefix intact
        return "[UNK]";
    }

    public Dictionary<string, int> GetVocab() => vocab;

    public List<int[]> TokenizeChunked(string text)
    {
        var chunks = new List<int[]>();
        string[] words = Regex.Split(text.Trim(), @"\s+");
        var allTokens = new List<int>();

        foreach (string word in words)
        {
            string cleaned = Regex.Replace(word, @"^[^\w]+|[^\w]+$", "");
            if (string.IsNullOrEmpty(cleaned)) continue;
            allTokens.AddRange(TokenizeWord(cleaned));
        }

        if (allTokens.Count <= MAX_LENGTH - 2)
        {
            chunks.Add(WrapChunk(allTokens, 0, allTokens.Count));
            return chunks;
        }

        int contentLength = MAX_LENGTH - 2;
        int start = 0;

        while (start < allTokens.Count)
        {
            int end = Mathf.Min(start + contentLength, allTokens.Count);
            chunks.Add(WrapChunk(allTokens, start, end));
            if (end >= allTokens.Count) break;
            start += contentLength - STRIDE;
        }

        Debug.Log($"[BERTTokenizer] Split text into {chunks.Count} chunks.");
        return chunks;
    }

    private int[] WrapChunk(List<int> tokens, int start, int end)
    {
        var chunk = new List<int> { CLS };
        for (int i = start; i < end; i++)
            chunk.Add(tokens[i]);
        chunk.Add(SEP);
        return chunk.ToArray();
    }

    public int[] Tokenize(string text)
    {
        var tokens = new List<int> { CLS };
        string[] words = Regex.Split(text.Trim(), @"\s+");

        foreach (string word in words)
        {
            if (tokens.Count >= MAX_LENGTH - 1) break;
            string cleaned = Regex.Replace(word, @"^[^\w]+|[^\w]+$", "");
            if (string.IsNullOrEmpty(cleaned)) continue;
            foreach (int t in TokenizeWord(cleaned))
            {
                if (tokens.Count >= MAX_LENGTH - 1) break;
                tokens.Add(t);
            }
        }

        tokens.Add(SEP);
        return tokens.ToArray();
    }

    private List<int> TokenizeWord(string word)
    {
        var result = new List<int>();

        if (vocab.TryGetValue(word, out int id))
        {
            result.Add(id);
            return result;
        }

        int start = 0;
        bool isBad = false;

        while (start < word.Length)
        {
            int end = word.Length;
            string cur = null;

            while (start < end)
            {
                string substr = word.Substring(start, end - start);
                if (start > 0) substr = "##" + substr;

                if (vocab.TryGetValue(substr, out int subId))
                {
                    cur = substr;
                    result.Add(subId);
                    break;
                }
                end--;
            }

            if (cur == null) { isBad = true; break; }
            start = end;
        }

        if (isBad) result.Add(UNK);
        return result;
    }
}