using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Sterowanie siłą (weight) Volume'ów post-processingu przez refleksję,
/// żeby skrypty kompilowały się także w projektach bez URP.
/// </summary>
public static class VolumeUtil
{
    static readonly Dictionary<System.Type, FieldInfo> fields = new Dictionary<System.Type, FieldInfo>();
    static readonly Dictionary<System.Type, PropertyInfo> props = new Dictionary<System.Type, PropertyInfo>();

    /// <summary>Znajduje komponent Volume na obiekcie o podanej nazwie (albo null).</summary>
    public static Component Find(string objectName)
    {
        var go = GameObject.Find(objectName);
        if (go == null) return null;
        foreach (var c in go.GetComponents<Component>())
            if (c != null && c.GetType().Name == "Volume") return c;
        return null;
    }

    public static void SetWeight(Component volume, float weight)
    {
        if (volume == null) return;
        var t = volume.GetType();
        if (!fields.TryGetValue(t, out FieldInfo f))
        {
            f = t.GetField("weight", BindingFlags.Public | BindingFlags.Instance);
            fields[t] = f;
        }
        if (f != null) { f.SetValue(volume, weight); return; }
        if (!props.TryGetValue(t, out PropertyInfo p))
        {
            p = t.GetProperty("weight", BindingFlags.Public | BindingFlags.Instance);
            props[t] = p;
        }
        if (p != null && p.CanWrite) p.SetValue(volume, weight);
    }
}
