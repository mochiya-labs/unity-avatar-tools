// Adapted from AnimationClipToVrmaSample by Baku Dreameater.
// https://github.com/malaybaku/AnimationClipToVrmaSample
// MIT License
// Copyright (c) 2023 Baku Dreameater
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using UniGLTF;
using UniVRM10;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mochiya.AvatarTools.Editor
{
    /// <summary>Exports a Humanoid AnimationClip as VRMA using an internal T-pose reference.</summary>
    public static class MochiyaAnimationExporter
    {
        public const int FramesPerSecond = 30;

        public static void Export(AnimationClip clip, string path, Action<float> progress = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !string.Equals(Path.GetExtension(path), ".vrma", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a .vrma output path.", nameof(path));
            // Build and validate completely before replacing a user's output file.
            var bytes = Create(clip, progress);
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>Creates VRMA bytes without modifying the clip or any scene avatar.</summary>
        public static byte[] Create(AnimationClip clip, Action<float> progress = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Animation export requires Edit Mode.");
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            if (clip.legacy || !clip.isHumanMotion)
                throw new ArgumentException("Choose a Humanoid AnimationClip. Generic and Legacy clips require their original skeleton and are not supported.", nameof(clip));
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0)
                throw new ArgumentException("The clip contains object-reference animation, which VRMA cannot export.", nameof(clip));
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.type != typeof(Animator) || !string.IsNullOrEmpty(binding.path))
                    throw new ArgumentException("The clip contains object, transform or blendshape curves. Export a Humanoid-only clip.", nameof(clip));
            var times = SampleTimes(clip.length);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                using (var rig = new MochiyaAnimationRig(scene))
                {
                    // This frame never moves, even if Unity sampling moves the Animator root.
                    var frame = new GameObject("MochiyaAnimationFrame");
                    SceneManager.MoveGameObjectToScene(frame, scene);
                    var data = new ExportingGltfData();
                    using (var exporter = new VrmAnimationExporter(data, new GltfExportSettings()))
                    {
                        ValidatePose(rig);
                        exporter.Prepare(rig.Root); // The glTF nodes store the unsampled T-pose.
                        exporter.Export(anim =>
                        {
                            anim.SetPositionBoneAndParent(rig.Bones[HumanBodyBones.Hips], frame.transform);
                            foreach (var pair in rig.Bones)
                                anim.AddRotationBoneAndParent(pair.Key, pair.Value,
                                    pair.Key == HumanBodyBones.Hips ? frame.transform : rig.Parent(pair.Key));
                            for (int i = 0; i < times.Length; i++)
                            {
                                progress?.Invoke((float)i / times.Length);
                                clip.SampleAnimation(rig.Root, times[i]);
                                ValidatePose(rig);
                                // Unity's .NET profile rounds FromSeconds to milliseconds.
                                anim.AddFrame(TimeSpan.FromTicks((long)Math.Round((double)times[i] * TimeSpan.TicksPerSecond)));
                            }
                        });
                        progress?.Invoke(1);
                        return data.ToGlbBytes();
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        internal static float[] SampleTimes(float duration)
        {
            if (!Finite(duration) || duration < 0 || duration > (int.MaxValue - 2) / (double)FramesPerSecond)
                throw new ArgumentException("Animation duration is invalid.");
            if (duration == 0) return new[] { 0f };
            var times = new List<float>();
            var intervals = (int)Math.Ceiling((double)duration * FramesPerSecond);
            for (int i = 0; i < intervals; i++)
            {
                var time = (float)((double)i / FramesPerSecond);
                if (time < duration) times.Add(time);
            }
            times.Add(duration);
            return times.ToArray();
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static void ValidatePose(MochiyaAnimationRig rig)
        {
            foreach (var bone in rig.Bones.Values)
            {
                var p = bone.position; var q = bone.rotation;
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) ||
                    !Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) ||
                    Mathf.Abs(Quaternion.Dot(q, q) - 1) > .01f)
                    throw new InvalidOperationException("Animation contains an invalid transform at " + bone.name + ".");
            }
        }
    }
}
