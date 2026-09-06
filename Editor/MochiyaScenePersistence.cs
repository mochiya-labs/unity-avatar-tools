using Mochiya.AvatarComposition;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Mochiya.AvatarTools.Editor
{
    [InitializeOnLoad]
    internal static class MochiyaScenePersistence
    {
        static MochiyaScenePersistence()
        {
            EditorSceneManager.sceneSaving += (scene, path) => {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var resources in root.GetComponentsInChildren<MochiyaSceneResources>(true))
                    { resources.Capture(); EditorUtility.SetDirty(resources); }
            };
        }
    }
}
