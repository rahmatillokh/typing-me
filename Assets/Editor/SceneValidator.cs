using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TypingMe.EditorTools
{
    /// <summary>
    /// Walks every Typing Me component in the generated scenes and prefabs and reports serialized
    /// object references that are null.
    /// </summary>
    /// <remarks>
    /// The scenes are generated, so a mis-wired reference produces no compile error and no visible
    /// symptom until that code path runs. This turns it into a build-time failure instead.
    /// </remarks>
    public static class SceneValidator
    {
        /// <summary>Fields that are legitimately empty — optional hooks, not wiring mistakes.</summary>
        private static readonly HashSet<string> OptionalFields = new HashSet<string>
        {
            // AudioManager falls back to procedurally generated clips.
            "musicClip", "keyClip", "clearClip", "missClip", "wrongClip", "uiClip",
            "levelCompleteClip", "gameOverClip",
            // Themes use a generated gradient unless art is supplied.
            "backgroundSprite",
            // Nav tabs are text-only for now.
            "icon",
            // The boss's key is built per rank at runtime; LevelRunner links it on level start.
            "launchAnchor"
        };

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Splash.unity",
            "Assets/Scenes/Menu.unity",
            "Assets/Scenes/Game.unity"
        };

        private static readonly string[] Prefabs =
        {
            "Assets/Prefabs/WordItem.prefab",
            "Assets/Prefabs/KeyboardKey.prefab",
            "Assets/Prefabs/LevelButton.prefab",
            "Assets/Resources/PersistentServices.prefab"
        };

        [MenuItem("Typing Me/Validate Generated Assets", false, 1)]
        public static void Validate()
        {
            var problems = new List<string>();
            int componentsChecked = 0;

            foreach (string prefabPath in Prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    problems.Add($"{prefabPath}: missing.");
                    continue;
                }

                componentsChecked += Inspect(prefab.GetComponentsInChildren<MonoBehaviour>(true), prefabPath, problems);
            }

            foreach (string scenePath in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    problems.Add($"{scenePath}: could not open.");
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                    componentsChecked += Inspect(root.GetComponentsInChildren<MonoBehaviour>(true), scenePath, problems);

                CheckSceneInvariants(scene, scenePath, problems);
            }

            CheckAuthorInfo(problems);

            var report = new StringBuilder();
            report.AppendLine($"[Typing Me] Validation: {componentsChecked} components checked across " +
                              $"{Prefabs.Length} prefabs and {Scenes.Length} scenes.");

            if (problems.Count == 0)
            {
                report.AppendLine("[Typing Me] VALIDATION PASSED — no unassigned references.");
                Debug.Log(report.ToString());
                return;
            }

            report.AppendLine($"[Typing Me] VALIDATION FAILED — {problems.Count} unassigned reference(s):");
            foreach (string problem in problems) report.AppendLine($"  · {problem}");

            Debug.LogError(report.ToString());
        }

        /// <summary>
        /// Reports what the Author asset actually holds, and fails on a link that would render as a
        /// button that does nothing.
        /// </summary>
        /// <remarks>
        /// The asset is hand-edited, so echoing the parsed values is the only way to tell a correct
        /// edit from YAML that Unity quietly dropped on import.
        /// </remarks>
        private static void CheckAuthorInfo(List<string> problems)
        {
            var info = AssetDatabase.LoadAssetAtPath<TypingMe.Data.AuthorInfoSO>(
                ProjectBootstrap.AuthorInfoAssetPath);

            if (info == null)
            {
                problems.Add($"{ProjectBootstrap.AuthorInfoAssetPath}: missing or failed to parse.");
                return;
            }

            int links = info.links?.Length ?? 0;
            var summary = new StringBuilder();
            summary.Append($"[Typing Me] AuthorInfo: name='{info.displayName}' role='{info.role}' ")
                   .Append($"bio={(info.HasBio ? "set" : "empty")} donate={(info.HasDonate ? "set" : "empty")} ")
                   .Append($"links={links}");

            for (int i = 0; i < links; i++)
            {
                TypingMe.Data.AuthorInfoSO.Link link = info.links[i];
                summary.Append($"\n    · {link?.label} -> {link?.url}");

                if (link != null && !string.IsNullOrWhiteSpace(link.label) &&
                    string.IsNullOrWhiteSpace(link.url))
                {
                    problems.Add($"AuthorInfo link '{link.label}' has no URL — it would render as a dead button.");
                }
            }

            Debug.Log(summary.ToString());
        }

        /// <summary>
        /// Components every scene must carry exactly one of.
        /// </summary>
        /// <remarks>
        /// The Editor silently adds some of these for you — "Create > Camera" attaches an
        /// AudioListener, `new GameObject(typeof(Camera))` does not. A generated scene missing one
        /// produces no error and no warning: audio simply never plays, or clicks never register.
        /// These assertions turn that class of bug into a failed validation.
        /// </remarks>
        private static void CheckSceneInvariants(Scene scene, string context, List<string> problems)
        {
            Require<AudioListener>(scene, context, 1, problems, "nothing would be audible");
            Require<Camera>(scene, context, 1, problems, "nothing would render");
            Require<EventSystem>(scene, context, 1, problems, "no UI input");
            Require<Canvas>(scene, context, 1, problems, "no UI at all");
            Require<Volume>(scene, context, 1, problems, "no post-processing, so no bloom");
        }

        private static void Require<T>(Scene scene, string context, int expected, List<string> problems,
            string consequence) where T : Component
        {
            int found = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
                found += root.GetComponentsInChildren<T>(true).Length;

            if (found == expected) return;

            problems.Add($"{context} · expected {expected} {typeof(T).Name}, found {found} — {consequence}");
        }

        private static int Inspect(IEnumerable<MonoBehaviour> behaviours, string context, List<string> problems)
        {
            int checkedCount = 0;

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;

                System.Type type = behaviour.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("TypingMe")) continue;

                checkedCount++;

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();

                while (property.NextVisible(enterChildren: true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue != null) continue;
                    if (OptionalFields.Contains(property.name)) continue;

                    problems.Add($"{context} · {behaviour.gameObject.name}.{type.Name}.{property.propertyPath}");
                }
            }

            return checkedCount;
        }
    }
}
