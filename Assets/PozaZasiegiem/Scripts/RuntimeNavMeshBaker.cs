using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Buduje NavMesh przy starcie gry ze wszystkich colliderów pod levelRoot.
/// Dzięki temu nie trzeba instalować pakietu AI Navigation ani niczego "bake'ować".
/// Dodajecie nowe ściany/meble? Wrzućcie je pod obiekt "Level (NavMesh)".
/// </summary>
[DefaultExecutionOrder(-1000)]
public class RuntimeNavMeshBaker : MonoBehaviour
{
    public Transform levelRoot;
    public int agentTypeId = 0;

    NavMeshDataInstance instance;

    void Awake()
    {
        if (levelRoot == null) { Debug.LogError("RuntimeNavMeshBaker: brak levelRoot!"); return; }

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(levelRoot, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), sources);

        var cols = levelRoot.GetComponentsInChildren<Collider>();
        Bounds b = new Bounds(levelRoot.position, Vector3.one);
        for (int i = 0; i < cols.Length; i++)
        {
            if (i == 0) b = cols[i].bounds; else b.Encapsulate(cols[i].bounds);
        }
        b.Expand(2f);

        var settings = NavMesh.GetSettingsByID(agentTypeId);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, b, Vector3.zero, Quaternion.identity);
        instance = NavMesh.AddNavMeshData(data);
    }

    void OnDestroy()
    {
        if (instance.valid) NavMesh.RemoveNavMeshData(instance);
    }
}
