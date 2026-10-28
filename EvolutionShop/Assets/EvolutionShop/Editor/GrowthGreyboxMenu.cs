using UnityEditor;
using UnityEditor.SceneManagement;

namespace EvolutionShop.Editor
{
    /// <summary>
    /// Play in the Editor plays the game (GR-14). Since GR-17 it is the only game: the classic game and its menu are gone.
    /// </summary>
    public static class GrowthGreyboxMenu
    {
        [MenuItem("Evolution Shop/Play the game")]
        public static void Play()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
