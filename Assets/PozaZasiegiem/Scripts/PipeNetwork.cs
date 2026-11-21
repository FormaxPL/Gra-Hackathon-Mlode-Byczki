using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rurociąg pod sufitem: graf węzłów (skrzyżowania, końce, włazy), po którym porusza się Stalker.
/// Gdy potwór przechodzi, rury drżą, stukają i sypie się z nich kurz.
/// Generator poziomu wypełnia tablice; można je też zbudować ręcznie.
/// </summary>
public class PipeNetwork : MonoBehaviour
{
    public static PipeNetwork Instance { get; private set; }

    [Tooltip("Pozycje węzłów w świecie (środek rury)")]
    public Vector3[] nodes = new Vector3[0];
    [Tooltip("Krawędzie grafu: edgeA[i] <-> edgeB[i]")]
    public int[] edgeA = new int[0];
    public int[] edgeB = new int[0];
    public PipeSegment[] segments = new PipeSegment[0];
    [Tooltip("Połączenia rur (kołnierze) – stąd sypie się kurz")]
    public Vector3[] flangePoints = new Vector3[0];
    public ParticleSystem[] dustPool = new ParticleSystem[0];

    List<int>[] adj;
    int dustIndex;

    void Awake()
    {
        Instance = this;
        BuildGraph();
    }

    void BuildGraph()
    {
        adj = new List<int>[nodes.Length];
        for (int i = 0; i < nodes.Length; i++) adj[i] = new List<int>();
        int m = Mathf.Min(edgeA.Length, edgeB.Length);
        for (int e = 0; e < m; e++)
        {
            int a = edgeA[e], b = edgeB[e];
            if (a < 0 || b < 0 || a >= nodes.Length || b >= nodes.Length || a == b) continue;
            adj[a].Add(b);
            adj[b].Add(a);
        }
    }

    public int NearestNode(Vector3 p)
    {
        int best = -1;
        float bd = float.MaxValue;
        for (int i = 0; i < nodes.Length; i++)
        {
            float d = (nodes[i] - p).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>Najkrótsza droga po rurach (Dijkstra). Zwraca listę węzłów od 'from' do 'to' albo null.</summary>
    public List<int> FindPath(int from, int to)
    {
        if (adj == null || adj.Length != nodes.Length) BuildGraph();
        int n = nodes.Length;
        if (from < 0 || to < 0 || from >= n || to >= n) return null;
        if (from == to) return new List<int> { from };

        var dist = new float[n];
        var prev = new int[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
        dist[from] = 0f;

        for (int iter = 0; iter < n; iter++)
        {
            int u = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0) break;
            if (u == to) break;
            done[u] = true;
            foreach (int v in adj[u])
            {
                float nd = dist[u] + Vector3.Distance(nodes[u], nodes[v]);
                if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
            }
        }
        if (prev[to] < 0) return null;

        var path = new List<int>();
        for (int c = to; c != -1; c = prev[c]) path.Add(c);
        path.Reverse();
        return path;
    }

    /// <summary>Drżenie rur w pobliżu punktu.</summary>
    public void ShakeNear(Vector3 p, float amount, float radius)
    {
        foreach (var seg in segments)
        {
            if (seg == null) continue;
            float d = seg.DistanceTo(p);
            if (d < radius) seg.Shake(amount * (1f - d / radius));
        }
    }

    /// <summary>Kurz z najbliższego połączenia rur.</summary>
    public void DustNear(Vector3 p)
    {
        if (dustPool == null || dustPool.Length == 0 || flangePoints.Length == 0) return;
        int best = -1;
        float bd = 4f;
        for (int i = 0; i < flangePoints.Length; i++)
        {
            float d = Vector3.Distance(p, flangePoints[i]);
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) return;
        var ps = dustPool[dustIndex++ % dustPool.Length];
        if (ps == null) return;
        ps.transform.position = flangePoints[best];
        ps.Play();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        int m = Mathf.Min(edgeA.Length, edgeB.Length);
        for (int e = 0; e < m; e++)
            if (edgeA[e] >= 0 && edgeB[e] >= 0 && edgeA[e] < nodes.Length && edgeB[e] < nodes.Length)
                Gizmos.DrawLine(nodes[edgeA[e]], nodes[edgeB[e]]);
        foreach (var n in nodes) Gizmos.DrawWireSphere(n, 0.2f);
    }
}
