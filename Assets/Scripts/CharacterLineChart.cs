using System.Collections.Generic;
using UnityEngine;
using XCharts.Runtime;

public class CharacterLineChart : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NERProcessor nerProcessor;
    [SerializeField] private LineChart chart;

    private Dictionary<string, int> cachedCounts;
    private Dictionary<string, int[]> cachedIntervals;
    private string selectedCharacter;

    private void Start()
    {
        if (nerProcessor == null)
        {
            Debug.LogError("[CharacterLineChart] NERProcessor not assigned.");
            return;
        }

        nerProcessor.OnProcessingComplete += OnProcessingComplete;
    }

    private void OnProcessingComplete(Dictionary<string, int> counts, Dictionary<string, int[]> intervals)
    {
        cachedCounts = counts;
        cachedIntervals = intervals;

        string top = GetTopCharacter();
        if (top != null)
            DisplayCharacter(top);
    }

    public void DisplayCharacter(string characterName)
    {
        if (cachedIntervals == null || !cachedIntervals.ContainsKey(characterName))
        {
            Debug.LogWarning($"[CharacterLineChart] No interval data for: {characterName}");
            return;
        }

        selectedCharacter = characterName;
        RebuildChart();
    }

    private void RebuildChart()
    {
        if (chart == null || cachedIntervals == null) return;

        chart.RemoveData();

        chart.AddXAxisData("0-10%");
        chart.AddXAxisData("10-20%");
        chart.AddXAxisData("20-30%");
        chart.AddXAxisData("30-40%");
        chart.AddXAxisData("40-50%");
        chart.AddXAxisData("50-60%");
        chart.AddXAxisData("60-70%");
        chart.AddXAxisData("70-80%");
        chart.AddXAxisData("80-90%");
        chart.AddXAxisData("90-100%");

        if (string.IsNullOrEmpty(selectedCharacter)) return;
        if (!cachedIntervals.ContainsKey(selectedCharacter)) return;

        int[] intervalData = cachedIntervals[selectedCharacter];
        var serie = chart.AddSerie<Line>(selectedCharacter);
        serie.serieName = selectedCharacter;

        for (int j = 0; j < 10; j++)
            chart.AddData(serie.serieName, j < intervalData.Length ? intervalData[j] : 0);

        Debug.Log($"[CharacterLineChart] Displaying: {selectedCharacter}");
    }

    private string GetTopCharacter()
    {
        if (cachedCounts == null || cachedCounts.Count == 0) return null;

        string top = null;
        int topCount = -1;

        foreach (var kvp in cachedCounts)
        {
            if (kvp.Value > topCount)
            {
                topCount = kvp.Value;
                top = kvp.Key;
            }
        }

        return top;
    }

    private void OnDestroy()
    {
        if (nerProcessor != null)
            nerProcessor.OnProcessingComplete -= OnProcessingComplete;
    }
}