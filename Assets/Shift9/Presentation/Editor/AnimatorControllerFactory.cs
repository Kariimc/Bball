using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Shift9.Presentation.Editor
{
    /// <summary>
    /// Generates the player AnimatorController and its full state graph:
    ///   • Base Layer "Locomotion": a 1D blend tree (Idle/Jog/Sprint) on Speed.
    ///   • "Actions" override layer: a None idle, one-shot Shoot/Pass/Rebound states driven from
    ///     Any State by their triggers, and a "Moves" sub-machine routed by the MoveId int (set by
    ///     the driver) + the DoMove trigger — one state per signature/finish/block move.
    /// Every state's clip is left empty; assign clips on a Humanoid rig. MoveId values mirror
    /// <c>Shift9.Presentation.Animation.MoveAnimation</c> (1-5 dribble, 10-13 finish, 20 block).
    /// </summary>
    public static class AnimatorControllerFactory
    {
        private const string Dir = "Assets/Shift9/Presentation/Generated";
        private const string Path = Dir + "/PlayerAnimator.controller";

        private const float EnterDuration = 0.06f;
        private const float ExitTime = 0.85f;
        private const float ExitDuration = 0.10f;

        [MenuItem("Shift9/Create Player Animator Controller")]
        public static void CreatePlayerController()
        {
            Directory.CreateDirectory(Dir);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(Path);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("HasBall", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsDefending", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Pass", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Rebound", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("MoveId", AnimatorControllerParameterType.Int);
            controller.AddParameter("DoMove", AnimatorControllerParameterType.Trigger);

            BuildLocomotion(controller);
            BuildActions(controller);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(controller);
            Debug.Log($"Created player animator at {Path}. Assign clips to: Locomotion (Idle/Jog/Sprint), " +
                      "Actions (Shoot/Pass/Rebound), and the Moves states (Crossover…SignatureDunk, Block).");
        }

        // Base layer: Idle/Jog/Sprint blended on Speed.
        private static void BuildLocomotion(AnimatorController controller)
        {
            AnimatorState loco = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(null, 0f);   // Idle
            tree.AddChild(null, 0.5f); // Jog
            tree.AddChild(null, 1f);   // Sprint
            controller.layers[0].stateMachine.defaultState = loco;
        }

        // Override layer for one-shot actions and signature moves.
        private static void BuildActions(AnimatorController controller)
        {
            controller.AddLayer("Actions");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[1].defaultWeight = 1f;
            controller.layers = layers; // reassign so defaultWeight persists

            AnimatorStateMachine sm = controller.layers[1].stateMachine;
            AnimatorState none = sm.AddState("None");
            sm.defaultState = none;

            AddTriggered(sm, none, "Shoot");
            AddTriggered(sm, none, "Pass");
            AddTriggered(sm, none, "Rebound");

            AnimatorStateMachine moves = sm.AddStateMachine("Moves");
            AddMove(sm, moves, none, "Crossover", 1);
            AddMove(sm, moves, none, "Hesitation", 2);
            AddMove(sm, moves, none, "BehindBack", 3);
            AddMove(sm, moves, none, "BetweenLegs", 4);
            AddMove(sm, moves, none, "SignatureCrossover", 5);
            AddMove(sm, moves, none, "Layup", 10);
            AddMove(sm, moves, none, "Floater", 11);
            AddMove(sm, moves, none, "Dunk", 12);
            AddMove(sm, moves, none, "SignatureDunk", 13);
            AddMove(sm, moves, none, "Block", 20);
        }

        // A state entered from Any State on a trigger, returning to None when it finishes.
        private static void AddTriggered(AnimatorStateMachine sm, AnimatorState back, string trigger)
        {
            AnimatorState state = sm.AddState(trigger);
            AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = EnterDuration;
            enter.canTransitionToSelf = false;
            ReturnTo(state, back);
        }

        // A move state entered on DoMove when MoveId equals its id, returning to None when finished.
        private static void AddMove(AnimatorStateMachine root, AnimatorStateMachine moves, AnimatorState back, string name, int id)
        {
            AnimatorState state = moves.AddState(name);
            AnimatorStateTransition enter = root.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "DoMove");
            enter.AddCondition(AnimatorConditionMode.Equals, id, "MoveId");
            enter.hasExitTime = false;
            enter.duration = EnterDuration;
            enter.canTransitionToSelf = false;
            ReturnTo(state, back);
        }

        private static void ReturnTo(AnimatorState state, AnimatorState back)
        {
            AnimatorStateTransition exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = ExitTime;
            exit.duration = ExitDuration;
        }
    }
}
