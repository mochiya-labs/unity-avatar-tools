using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mochiya.AvatarTools.Editor
{
    // Read authoring references without invoking MA's build, reference setters or component callbacks.
    internal static class MochiyaParentDependency
    {
        internal sealed class Result
        {
            internal readonly List<string> Reasons = new List<string>();
            internal readonly List<string> Errors = new List<string>();
        }

        internal static Transform ReferenceRoot(Component component, GameObject fallback)
        {
            var utility = component.GetType().Assembly.GetType("nadena.dev.modular_avatar.core.RuntimeUtil");
            var find = utility?.GetMethod("FindAvatarTransformInParents", BindingFlags.Public | BindingFlags.Static);
            return find?.Invoke(null, new object[] { component.transform }) as Transform ?? fallback.transform;
        }

        internal static Transform Resolve(object reference, Transform root)
        {
            if (reference is Transform transform) return transform != null ? transform : null;
            if (reference is Component component) return component != null ? component.transform : null;
            if (reference is GameObject go) return go != null ? go.transform : null;
            var direct = OptionalAvatarReaders.Read(reference, "targetObject") as GameObject;
            var path = OptionalAvatarReaders.String(reference, "referencePath");
            return Resolve(direct, path, root);
        }

        private static Transform Resolve(GameObject direct, string path, Transform root)
        {
            if (direct != null) return direct.transform;
            if (root == null || string.IsNullOrEmpty(path)) return null;
            if (path == "$$$AVATAR_ROOT$$$") return root;
            var target = root.Find(path);
            // MA path references tolerate an empty decoy Armature next to the real armature.
            if (target != null && target.name == "Armature" && target.childCount == 0 && target.parent != null)
                foreach (Transform sibling in target.parent)
                    if (sibling.name == target.name && sibling.childCount > 0) return sibling;
            return target;
        }

        internal static Transform ProxyTarget(Component component, Transform root)
        {
            var path = OptionalAvatarReaders.String(component, "subPath");
            var bone = (HumanBodyBones)(int)OptionalAvatarReaders.Number(component, "boneReference", (int)HumanBodyBones.LastBone);
            if (path == "$$AVATAR") return root;
            if (bone == HumanBodyBones.LastBone) return string.IsNullOrWhiteSpace(path) ? null : root.Find(path);
            var animator = root.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return null;
            var target = animator.GetBoneTransform(bone);
            return string.IsNullOrEmpty(path) ? target : target?.Find(path);
        }

        internal static Result Inspect(GameObject scope)
        {
            var result = new Result();
            var parent = scope.transform.parent;
            void Check(Transform target, Component owner, string field)
            {
                if (target == null || target.IsChildOf(scope.transform)) return;
                if (parent == null || !target.IsChildOf(parent))
                    result.Errors.Add($"{owner.name}: MA {field} targets an object outside the asset and its direct parent. Place the asset under its reference avatar.");
                else result.Reasons.Add($"{owner.name}: {owner.GetType().Name} depends on {target.name} outside this asset.");
            }
            foreach (var component in scope.GetComponentsInChildren<Component>(true).Where(c => c != null &&
                c.GetType().Namespace == "nadena.dev.modular_avatar.core" && (!(c is Behaviour b) || b.enabled)))
            {
                var root = ReferenceRoot(component, parent != null && MochiyaAvatarConverter.HasValidHumanoid(parent.gameObject) ? parent.gameObject : scope);
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    var enter = true;
                    while (property.Next(enter))
                    {
                        enter = true;
                        if (property.propertyPath == "m_CorrespondingSourceObject" || property.propertyPath == "m_PrefabInstance" || property.propertyPath == "m_PrefabAsset")
                        { enter = false; continue; }
                        if (property.propertyType == SerializedPropertyType.Generic && property.type == "AvatarObjectReference")
                        {
                            var direct = property.FindPropertyRelative("targetObject")?.objectReferenceValue as GameObject;
                            var path = property.FindPropertyRelative("referencePath")?.stringValue;
                            var target = Resolve(direct, path, root);
                            if (target == null && !string.IsNullOrEmpty(path))
                                result.Errors.Add($"{component.name}: MA reference '{path}' cannot be resolved. Repair the reference before conversion.");
                            Check(target, component, property.propertyPath);
                            enter = false;
                        }
                        else if (property.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            var value = property.objectReferenceValue;
                            if (value is GameObject go) Check(go.transform, component, property.propertyPath);
                            else if (value is Component referenced) Check(referenced.transform, component, property.propertyPath);
                        }
                    }
                }
                if (component.GetType().Name == "ModularAvatarBoneProxy")
                    Check(ProxyTarget(component, root), component, "target");
                // These MA components modify their avatar's controller/menu/parameters without a scene-object target.
                if (OptionalAvatarReaders.Read(component, "animator") is RuntimeAnimatorController ||
                    OptionalAvatarReaders.Read(component, "menuToAppend") is UnityEngine.Object menu && menu != null ||
                    component.GetType().Name == "ModularAvatarParameters" && OptionalAvatarReaders.List(component, "parameters").Any() ||
                    component.GetType().Name == "ModularAvatarGlobalCollider")
                    Check(root, component, "avatar settings");
            }
            return result;
        }
    }
}
