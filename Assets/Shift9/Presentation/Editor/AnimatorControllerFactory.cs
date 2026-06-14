using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Shift9.Presentation.Editor
{
    /// <summary>
    /// Generates the placeholder player locomotion AnimatorController: a "Speed" float parameter
    /// driving a 1D blend tree with Idle / Jog / Sprint slots (thresholds 0 / 0.5 / 1). Motions are
    /// left empty so a rigged Humanoid model's clips can be dropped in later. Run from the
    /// Shift9 menu; the controller is created once and committed alongside the rig.
    /// </summary>
    public static class AnimatorControllerFactory
    {
        private const string Dir = "Assets/Shift9/Presentation/Generated";
        private const string Path = Dir + "/PlayerLocomotion.controller";

        [MenuItem("Shift9/Create Player Locomotion Controller")]
        public static void CreateLocomotionController()
        {
            Directory.CreateDirectory(Dir);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(Path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("HasBall", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDefending", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Pass", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Rebound", AnimatorControllerParameterType.Trigger);

            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(null, 0f);   // Idle   — assign clip later
            tree.AddChild(null, 0.5f); // Jog    — assign clip later
            tree.AddChild(null, 1f);   // Sprint — assign clip later

            controller.layers[0].stateMachine.defaultState = state;

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(controller);
            Debug.Log($"Created locomotion controller at {Path}. Assign Idle/Jog/Sprint clips to the blend tree, " +
                      "and add action states/clips driven by HasBall / IsDefending / Shoot / Pass / Rebound.");
        }
    }
}
