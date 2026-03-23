using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Removes null transitions, default states, and BlendTree children from AnimatorController
// assets. Runs once per session on load, also available via the Tools menu.
[InitializeOnLoad]
public static class RepairAnimatorControllers
{
    private const string SessionFlag = "RepairAnimatorControllers.Ran";

    static RepairAnimatorControllers()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (SessionState.GetBool(SessionFlag, false)) return;
        SessionState.SetBool(SessionFlag, true);
        Run();
    }

    [MenuItem("Tools/Repair Animator Controllers")]
    public static void Run()
    {
        int scanned = 0;
        int fixedCount = 0;
        List<string> issues = new();
        List<string> reimported = new();

        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) continue;
            scanned++;

            bool changed = false;
            for (int li = 0; li < ctrl.layers.Length; li++)
            {
                AnimatorControllerLayer layer = ctrl.layers[li];
                if (layer == null || layer.stateMachine == null)
                {
                    issues.Add($"  - '{path}' layer[{li}] has a null state machine.");
                    continue;
                }
                changed |= CleanStateMachine(layer.stateMachine, path, li, issues);
            }

            if (changed)
            {
                EditorUtility.SetDirty(ctrl);
                fixedCount++;
                issues.Add($"  + Repaired '{path}'.");
                reimported.Add(path);
            }
        }

        // Override controllers: they wrap a base controller and can ship with null overrides.
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorOverrideController"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorOverrideController aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (aoc == null) continue;
            scanned++;

            if (aoc.runtimeAnimatorController == null)
            {
                issues.Add($"  - '{path}' override controller has no base - graph cannot resolve any edge.");
            }
        }

        AssetDatabase.SaveAssets();

        // Re-import touched controllers so Unity drops stale in-memory graph state.
        foreach (string path in reimported)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // Close open Animator/Graph windows; they hold dangling refs to the cleaned objects.
        int closedWindows = CloseStaleGraphWindows();

        // Strip missing-script components from open scenes.
        int strippedComponents = StripMissingScripts();

        Debug.Log($"[RepairAnimatorControllers] Scanned {scanned} assets, repaired {fixedCount}, closed {closedWindows} stale graph window(s), stripped {strippedComponents} missing-script component(s)." +
                  (issues.Count == 0 ? "" : "\n" + string.Join("\n", issues)));
    }

    private static int CloseStaleGraphWindows()
    {
        int count = 0;
        EditorWindow[] all = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (EditorWindow w in all)
        {
            if (w == null) continue;
            string tn = w.GetType().FullName ?? "";
            // Matches the legacy AnimatorControllerTool and any UnityEditor.Graphs.* window.
            if (tn.StartsWith("UnityEditor.Graphs.") || tn.EndsWith("AnimatorControllerTool") || tn.EndsWith("AnimatorWindow"))
            {
                w.Close();
                count++;
            }
        }
        return count;
    }

    private static int StripMissingScripts()
    {
        int stripped = 0;
        // Open scenes only; don't mass-edit asset prefabs here.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            bool dirty = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (removed > 0)
                    {
                        stripped += removed;
                        dirty = true;
                    }
                }
            }
            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        return stripped;
    }

    private static bool CleanStateMachine(AnimatorStateMachine sm, string path, int layerIdx, List<string> issues)
    {
        bool changed = false;

        // Any-state transitions with null destinations.
        var anyStates = new List<AnimatorStateTransition>(sm.anyStateTransitions);
        int removed = anyStates.RemoveAll(t => t == null || (t.destinationState == null && t.destinationStateMachine == null && !t.isExit));
        if (removed > 0)
        {
            sm.anyStateTransitions = anyStates.ToArray();
            issues.Add($"  * '{path}' layer[{layerIdx}] sm '{sm.name}': removed {removed} orphan AnyState transition(s).");
            changed = true;
        }

        // Entry transitions with null destinations.
        var entryTransitions = new List<AnimatorTransition>(sm.entryTransitions);
        int removedEntry = entryTransitions.RemoveAll(t => t == null || (t.destinationState == null && t.destinationStateMachine == null));
        if (removedEntry > 0)
        {
            sm.entryTransitions = entryTransitions.ToArray();
            issues.Add($"  * '{path}' layer[{layerIdx}] sm '{sm.name}': removed {removedEntry} orphan Entry transition(s).");
            changed = true;
        }

        // Per-state transitions with null destinations, plus per-state BlendTree cleanup.
        foreach (ChildAnimatorState child in sm.states)
        {
            AnimatorState state = child.state;
            if (state == null) continue;
            var ts = new List<AnimatorStateTransition>(state.transitions);
            int rm = ts.RemoveAll(t => t == null || (t.destinationState == null && t.destinationStateMachine == null && !t.isExit));
            if (rm > 0)
            {
                state.transitions = ts.ToArray();
                issues.Add($"  * '{path}' layer[{layerIdx}] state '{state.name}': removed {rm} orphan transition(s).");
                changed = true;
            }

            // BlendTree children: drop null entries.
            if (state.motion is BlendTree bt)
                changed |= CleanBlendTree(bt, path, layerIdx, state.name, issues);
        }

        // State-machine-level transitions, read per source SM since the API doesn't expose
        // them via the top-level property.
        foreach (ChildAnimatorStateMachine cSm in sm.stateMachines)
        {
            if (cSm.stateMachine == null) continue;
            var smTs = new List<AnimatorTransition>(sm.GetStateMachineTransitions(cSm.stateMachine));
            int rmSm = smTs.RemoveAll(t => t == null || (t.destinationState == null && t.destinationStateMachine == null && !t.isExit));
            if (rmSm > 0)
            {
                sm.SetStateMachineTransitions(cSm.stateMachine, smTs.ToArray());
                issues.Add($"  * '{path}' layer[{layerIdx}] sm '{sm.name}' -> sub '{cSm.stateMachine.name}': removed {rmSm} orphan sm-transition(s).");
                changed = true;
            }
        }

        // Default state missing: fall back to the first remaining state.
        if (sm.defaultState == null && sm.states != null && sm.states.Length > 0)
        {
            sm.defaultState = sm.states[0].state;
            issues.Add($"  * '{path}' layer[{layerIdx}] sm '{sm.name}': default state was null, set to '{sm.defaultState.name}'.");
            changed = true;
        }

        // Recurse into nested state machines.
        foreach (ChildAnimatorStateMachine childSm in sm.stateMachines)
        {
            if (childSm.stateMachine == null) continue;
            changed |= CleanStateMachine(childSm.stateMachine, path, layerIdx, issues);
        }

        return changed;
    }

    private static bool CleanBlendTree(BlendTree bt, string path, int layerIdx, string stateName, List<string> issues)
    {
        if (bt == null || bt.children == null) return false;
        bool changed = false;

        // Remove children whose motion is null.
        var kept = new List<ChildMotion>();
        int removed = 0;
        foreach (ChildMotion cm in bt.children)
        {
            if (cm.motion == null) { removed++; continue; }
            kept.Add(cm);
            if (cm.motion is BlendTree nested)
                changed |= CleanBlendTree(nested, path, layerIdx, stateName, issues);
        }
        if (removed > 0)
        {
            bt.children = kept.ToArray();
            issues.Add($"  * '{path}' layer[{layerIdx}] state '{stateName}' BlendTree '{bt.name}': removed {removed} child motion(s) with null motion.");
            changed = true;
        }

        return changed;
    }
}
