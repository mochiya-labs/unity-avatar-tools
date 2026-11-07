using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    internal static class MochiyaDiagnosticPath
    {
        internal static string Of(Transform node)
        {
            if (node == null) return "<missing GameObject>";
            var parts = new List<string>();
            for (var current = node; current != null; current = current.parent)
            {
                var siblings = current.parent != null ? current.parent.Cast<Transform>()
                    : current.gameObject.scene.IsValid() ? current.gameObject.scene.GetRootGameObjects().Select(root => root.transform)
                    : Enumerable.Empty<Transform>();
                var name = current.name.Replace("\\", "\\\\").Replace("/", "\\/");
                if (siblings.Count(sibling => sibling.name == current.name) > 1)
                    name += $" [sibling {current.GetSiblingIndex()}]";
                parts.Add(name);
            }
            parts.Reverse();
            return node.gameObject.scene.name + ":/" + string.Join("/", parts);
        }
    }
}
