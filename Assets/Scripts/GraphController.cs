using UnityEngine;
using XCharts.Runtime;

[RequireComponent(typeof(LineChart))]
public class GraphController : MonoBehaviour
{
    private LineChart _chart;

    private void Awake()
    {
        _chart = GetComponent<LineChart>();
    }

    private void OnEnable() => CharacterAnalyser.OnCharactersAnalysed += PopulateChart;
    private void OnDisable() => CharacterAnalyser.OnCharactersAnalysed -= PopulateChart;

    private void PopulateChart(CharacterAnalyser.CharacterData[] characters)
    {
        _chart.RemoveData();

        _chart.ClearData();
        var xAxis = _chart.EnsureChartComponent<XAxis>();
        xAxis.splitNumber = characters.Length > 0 ? characters[0].counts.Length : 10;
        xAxis.boundaryGap = false;

        for (int i = 0; i < xAxis.splitNumber; i++)
        {
            int pct = (int)((i / (float)(xAxis.splitNumber - 1)) * 100f);
            _chart.AddXAxisData($"{pct}%");
        }

        // ── Y axis label ──────────────────────────────────────────────────────
        var yAxis = _chart.EnsureChartComponent<YAxis>();
        yAxis.minMaxType = Axis.AxisMinMaxType.Default;

        // ── Plot only the first (selected) character ──────────────────────────
        if (characters.Length == 0)
        {
            Debug.LogWarning("[GraphController] No characters to display.");
            return;
        }

        var selected = characters[0];
        var serie = _chart.AddSerie<Line>(selected.name);

        foreach (int count in selected.counts)
            _chart.AddData(serie.index, count);

        Debug.Log($"[GraphController] Chart populated for '{selected.name}'.");
    }
}