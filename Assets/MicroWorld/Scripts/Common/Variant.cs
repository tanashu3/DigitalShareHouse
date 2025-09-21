using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MicroWorldNS
{
    public class Variant : MonoBehaviour
    {
        public bool Exclusive = true;

        public static void Build(GameObject holder, Rnd rnd, bool deactivateOnly = false)
        {
            Build(holder.GetComponentsInChildren<Variant>(deactivateOnly), rnd, deactivateOnly);
        }

        static List<Variant> buffer = new List<Variant>();

        public static void Build(IEnumerable<Variant> variants, Rnd rnd, bool deactivateOnly = false)
        {
            var variantsByParent = new Dictionary<Transform, List<Variant>>();
            foreach (var v in variants)
            {
                if (!variantsByParent.TryGetValue(v.transform.parent, out var list))
                    variantsByParent[v.transform.parent] = list = new List<Variant>();
                list.Add(v);
            }

            foreach (var pair in variantsByParent)
            {
                var parent = pair.Key;
                if (!parent) continue;
                buffer.Clear();
                buffer.AddRange(pair.Value.Where(v => v != null && v.Exclusive));
                if (buffer.Count == 0) continue;
                var selected = rnd.Int(buffer.Count);
                for (int i = 0; i < buffer.Count; i++)
                {
                    // destroy unselected object
                    if (i != selected)
                    {
                        var go = buffer[i].gameObject;
                        go.SetActive(false);
                        if (!deactivateOnly)
                            Helper.DestroySafe(go);
                        continue;
                    }
                    // destroy variant script
                    buffer[i].enabled = false;
                    if (!deactivateOnly)
                        Helper.DestroySafe(buffer[i]);
                }
            }
        }
    }
}
