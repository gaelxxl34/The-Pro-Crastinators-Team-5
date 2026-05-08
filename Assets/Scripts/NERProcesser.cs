using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.InferenceEngine;
using TMPro;

public class NERProcessor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private Transform content;

    [Header("Input")]
    [SerializeField] private string documentFileName = "";


    private Model runtimeModel;
    private Worker worker;
    private BERTTokenizer tokenizer = new();

    public Dictionary<string, int> CharacterCounts { get; private set; } = new();
    public Dictionary<string, int[]> CharacterCountsByInterval { get; private set; } = new();

    public event System.Action<Dictionary<string, int>, Dictionary<string, int[]>> OnProcessingComplete;

    public bool IsProcessing { get; private set; } = false;

    private void Start()
    {
        string modelPath = Path.Combine(Application.streamingAssetsPath, "ner_model_17.sentis");
        string vocabPath = Path.Combine(Application.streamingAssetsPath, "vocab.txt");

        if (!File.Exists(modelPath))
        {
            Debug.LogError($"[NERProcessor] Model not found at: {modelPath}");
            return;
        }

        tokenizer.Load(vocabPath);
        runtimeModel = ModelLoader.Load(modelPath);
        worker = new Worker(runtimeModel, BackendType.CPU);
        Debug.Log("[NERProcessor] Model loaded and ready.");

        OnProcessingComplete += (counts, intervals) => transform.parent.gameObject.SetActive(false);

        if (!string.IsNullOrEmpty(documentFileName))
            ProcessDocument(documentFileName);
    }

    public void ProcessDocument(string fileName)
    {
        if (!Path.HasExtension(fileName))
            fileName += ".txt";

        string documentPath = Path.Combine(Application.dataPath, "Papers", fileName);
        Debug.Log($"[NERProcessor] Looking for document at: {documentPath}");

        if (!File.Exists(documentPath))
        {
            Debug.LogError($"[NERProcessor] Document not found at: {documentPath}");
            return;
        }

        string text = File.ReadAllText(documentPath);
        Debug.Log($"[NERProcessor] Loaded document: {fileName} ({text.Length} characters)");
        ProcessText(text);
    }

    public void ProcessText(string text)
    {
        if (worker == null)
        {
            Debug.LogError("[NERProcessor] Worker not initialized.");
            return;
        }

        if (IsProcessing)
        {
            Debug.LogWarning("[NERProcessor] Already processing, request ignored.");
            return;
        }

        StartCoroutine(ProcessCoroutine(text));
    }

    private IEnumerator ProcessCoroutine(string text)
    {
        IsProcessing = true;

        var counts = new Dictionary<string, int>();
        CharacterCountsByInterval = new Dictionary<string, int[]>();

        if (!tokenizer.IsLoaded)
        {
            Debug.LogError("[NERProcessor] Tokenizer not loaded.");
            IsProcessing = false;
            yield break;
        }

        List<int[]> chunks = tokenizer.TokenizeChunked(text);
        int totalChunks = chunks.Count;
        Debug.Log($"[NERProcessor] Processing {totalChunks} chunk(s) across 10 intervals...");

        for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            int[] inputIds = chunks[chunkIndex];
            int length = inputIds.Length;
            int interval = Mathf.Min((int)((float)chunkIndex / totalChunks * 10), 9);

            int[] attentionMask = new int[length];
            int[] tokenTypeIds = new int[length];
            for (int i = 0; i < length; i++) attentionMask[i] = 1;

            using var inputTensor = new Tensor<int>(new TensorShape(1, length), inputIds);
            using var attentionTensor = new Tensor<int>(new TensorShape(1, length), attentionMask);
            using var tokenTypeTensor = new Tensor<int>(new TensorShape(1, length), tokenTypeIds);

            worker.SetInput("input_ids", inputTensor);
            worker.SetInput("attention_mask", attentionTensor);
            worker.SetInput("token_type_ids", tokenTypeTensor);
            worker.Schedule();

            using var rawOutput = worker.PeekOutput("logits") as Tensor<float>;
            using var output = rawOutput.ReadbackAndClone();

            string currentName = "";

            for (int i = 1; i < output.shape[1] - 1; i++)
            {
                string label = IdToLabel(ArgMax(output, i));
                string rawToken = tokenizer.IdToRawToken(inputIds[i]);
                bool isSubword = rawToken.StartsWith("##");
                string cleanToken = rawToken.Replace("##", "");

                if (isSubword && !string.IsNullOrEmpty(currentName))
                {
                    currentName += cleanToken;
                }
                else if (label == "B-PER")
                {
                    if (!string.IsNullOrEmpty(currentName))
                    {
                        Increment(counts, currentName);
                        IncrementInterval(CharacterCountsByInterval, currentName, interval);
                    }
                    currentName = cleanToken;
                }
                else if (label == "I-PER" && !string.IsNullOrEmpty(currentName))
                {
                    currentName += " " + cleanToken;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentName))
                    {
                        Increment(counts, currentName);
                        IncrementInterval(CharacterCountsByInterval, currentName, interval);
                    }
                    currentName = "";
                }
            }

            if (!string.IsNullOrEmpty(currentName))
            {
                Increment(counts, currentName);
                IncrementInterval(CharacterCountsByInterval, currentName, interval);
            }

            // Yield every chunk so the main thread stays responsive
            yield return null;
        }

        CharacterCounts = counts;

        Debug.Log($"[NERProcessor] Found {counts.Count} unique characters across all chunks.");

        PopulateContent();
        IsProcessing = false;
        OnProcessingComplete?.Invoke(CharacterCounts, CharacterCountsByInterval);
    }

    private void PopulateContent()
    {
        if (entryPrefab == null || content == null)
        {
            Debug.LogError("[NERProcessor] entryPrefab or content is not assigned.");
            return;
        }

        foreach (Transform child in content)
            Destroy(child.gameObject);

        var sorted = new List<KeyValuePair<string, int>>(CharacterCounts);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        var chart = FindObjectOfType<CharacterLineChart>();

        foreach (var kvp in sorted)
        {
            GameObject entry = Instantiate(entryPrefab, content);
            entry.name = kvp.Key;

            var entryController = entry.GetComponent<EntryController>();
            if (entryController != null)
                entryController.Setup(kvp.Key, kvp.Value, chart);
            else
            {
                var label = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = $"{kvp.Key}: {kvp.Value}";
            }
        }

        Debug.Log($"[NERProcessor] Populated {CharacterCounts.Count} characters into content.");
    }

    private void IncrementInterval(Dictionary<string, int[]> dict, string key, int interval)
    {
        if (!dict.ContainsKey(key))
            dict[key] = new int[10];
        dict[key][interval]++;
    }

    private int ArgMax(Tensor<float> tensor, int position)
    {
        int numLabels = tensor.shape[2];
        int best = 0;
        float bestVal = tensor[0, position, 0];

        for (int i = 1; i < numLabels; i++)
        {
            float val = tensor[0, position, i];
            if (val > bestVal) { bestVal = val; best = i; }
        }

        return best;
    }

    private string IdToLabel(int id)
    {
        return id switch
        {
            0 => "O",
            1 => "B-MISC",
            2 => "I-MISC",
            3 => "B-PER",
            4 => "I-PER",
            5 => "B-ORG",
            6 => "I-ORG",
            7 => "B-LOC",
            8 => "I-LOC",
            _ => "O"
        };
    }

    private void Increment(Dictionary<string, int> dict, string key)
    {
        if (!dict.ContainsKey(key)) dict[key] = 0;
        dict[key]++;
    }

    private void OnDestroy() => worker?.Dispose();
}