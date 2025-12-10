using System;
using UnityEngine;

/// ScriptableObject for configuring opponent pawn AI behaviors and modifiers.
/// Create instances via Assets → Create → Game → Pawn Customiser to tweak AI parameters in Unity Editor.
/// Reference this in Pawn Controller to apply custom AI behavior and modifier configurations.
[CreateAssetMenu(fileName = "New Pawn Customiser", menuName = "Game/Pawn Customiser", order = 2)]
public class PawnCustomiser : ScriptableObject
{
    #region Nested Classes

    /// Configuration for chess mode AI movement weights
    [Serializable]
    public class ChessModeWeights
    {
        [Header("Basic AI Type")]
        [Tooltip("Weight for tiles closest to player (Basic AI)")]
        [Range(0f, 10f)]
        public float basicClosestWeight = 5f;
        [Tooltip("Weight for other allowed tiles (Basic AI)")]
        [Range(0f, 10f)]
        public float basicOtherWeight = 1f;

        [Header("Handcannon AI Type")]
        [Tooltip("Weight for tiles closest to player (Handcannon AI)")]
        [Range(0f, 10f)]
        public float handcannonClosestWeight = 3f;
        [Tooltip("Weight for other tiles (Handcannon AI)")]
        [Range(0f, 10f)]
        public float handcannonOtherWeight = 1f;

        [Header("Shotgun AI Type")]
        [Tooltip("Weight for tiles closest to player (Shotgun AI - highest priority)")]
        [Range(0f, 10f)]
        public float shotgunClosestWeight = 4f;
        [Tooltip("Weight for diagonal upper moves (Shotgun AI - flanking)")]
        [Range(0f, 10f)]
        public float shotgunDiagonalWeight = 3f;
        [Tooltip("Weight for side moves (Shotgun AI)")]
        [Range(0f, 10f)]
        public float shotgunSideWeight = 2f;
        [Tooltip("Weight for tiles farthest from player (Shotgun AI - lowest priority)")]
        [Range(0f, 10f)]
        public float shotgunFarthestWeight = 1f;

        [Header("Sniper AI Type")]
        [Tooltip("Weight for tiles farthest from player (Sniper AI - defensive positioning)")]
        [Range(0f, 10f)]
        public float sniperFarthestWeight = 4f;
        [Tooltip("Weight for medium distance tiles (Sniper AI)")]
        [Range(0f, 10f)]
        public float sniperMediumWeight = 2f;
        [Tooltip("Weight for tiles closest to player (Sniper AI - avoid close quarters)")]
        [Range(0f, 10f)]
        public float sniperClosestWeight = 1f;
    }

    /// Configuration for standoff mode AI distance preferences
    [Serializable]
    public class StandoffModeDistances
    {
        [Header("Basic/Shotgun AI Type")]
        [Tooltip("Distance threshold for Basic/Shotgun to approach player (Standoff mode)")]
        [Range(0f, 10f)]
        public float aggressiveApproachDistance = 1.5f;

        [Header("Handcannon AI Type")]
        [Tooltip("Minimum optimal distance for Handcannon (Standoff mode)")]
        [Range(0f, 10f)]
        public float handcannonMinDistance = 2f;
        [Tooltip("Maximum optimal distance for Handcannon (Standoff mode)")]
        [Range(0f, 10f)]
        public float handcannonMaxDistance = 4f;

        [Header("Sniper AI Type")]
        [Tooltip("Retreat distance threshold for Sniper (Standoff mode)")]
        [Range(0f, 15f)]
        public float sniperRetreatDistance = 6f;
    }

    /// Configuration for modifier effects and multipliers
    [Serializable]
    public class ModifierEffects
    {
        [Header("Tenacious Modifier")]
        [Tooltip("Max HP granted by Tenacious modifier")]
        [Range(1, 10)]
        public int tenaciousMaxHP = 2;

        [Header("Confrontational Modifier")]
        [Tooltip("Fire interval multiplier for Confrontational modifier (Standoff mode)")]
        [Range(0.1f, 1f)]
        public float confrontationalFireIntervalMultiplier = 0.75f;

        [Header("Fleet Modifier")]
        [Tooltip("Movement speed multiplier for Fleet modifier (Standoff mode)")]
        [Range(1f, 2f)]
        public float fleetMoveSpeedMultiplier = 1.25f;

        [Header("Observant Modifier")]
        [Tooltip("Firing delay multiplier for Observant modifier (Standoff mode)")]
        [Range(0.1f, 1f)]
        public float observantFiringDelayMultiplier = 0.5f;

        [Header("Reflexive Modifier")]
        [Tooltip("Firing delay multiplier for Reflexive modifier (Standoff mode)")]
        [Range(0.1f, 1f)]
        public float reflexiveFiringDelayMultiplier = 0.75f;
    }

    /// Configuration for platformer movement and jumping
    [Serializable]
    public class PlatformerMovement
    {
        [Header("Movement")]
        [Tooltip("Base movement speed in Standoff mode (before modifier multipliers)")]
        [Range(1f, 10f)]
        public float baseMoveSpeed = 3f;

        [Header("Jumping")]
        [Tooltip("Jump force applied to Rigidbody2D")]
        [Range(1f, 20f)]
        public float jumpForce = 8f;
        [Tooltip("Maximum height of jumpable obstacles")]
        [Range(0.5f, 5f)]
        public float maxJumpableHeight = 2f;
        [Tooltip("Maximum distance of jumpable gaps")]
        [Range(0.5f, 5f)]
        public float maxJumpableGap = 2f;

        [Header("Ground Detection")]
        [Tooltip("Raycast distance for ground detection")]
        [Range(0.05f, 1f)]
        public float groundCheckDistance = 0.1f;
        [Tooltip("Edge check offset multiplier (distance * currentMoveDirection)")]
        [Range(0.1f, 2f)]
        public float edgeCheckOffset = 0.5f;
        [Tooltip("Edge check vertical offset")]
        [Range(0.1f, 2f)]
        public float edgeCheckVerticalOffset = 0.5f;
        [Tooltip("Maximum edge raycast distance")]
        [Range(0.5f, 3f)]
        public float edgeRaycastDistance = 1f;
        [Tooltip("Far ground check raycast distance (for gap jumps)")]
        [Range(1f, 5f)]
        public float farGroundCheckDistance = 2f;
    }

    /// Configuration for AI decision-making timing
    [Serializable]
    public class AIThinking
    {
        [Header("Chess Mode")]
        [Tooltip("Duration of AI movement animation in Chess mode")]
        [Range(0.05f, 1f)]
        public float chessMoveAnimationDuration = 0.12f;

        [Header("Standoff Mode")]
        [Tooltip("Interval at which AI makes movement decisions in Standoff mode")]
        [Range(0.1f, 2f)]
        public float standoffThinkInterval = 0.5f;
    }

    #endregion

    #region Configuration Fields

    [Header("Chess Mode AI Weights")]
    [Tooltip("Weight configurations for chess mode AI movement strategies")]
    public ChessModeWeights chessModeWeights = new ChessModeWeights();

    [Header("Standoff Mode Distances")]
    [Tooltip("Distance preferences for standoff mode AI positioning")]
    public StandoffModeDistances standoffDistances = new StandoffModeDistances();

    [Header("Modifier Effects")]
    [Tooltip("Multipliers and values for modifier effects")]
    public ModifierEffects modifierEffects = new ModifierEffects();

    [Header("Platformer Movement")]
    [Tooltip("Movement and jumping parameters for standoff mode")]
    public PlatformerMovement platformerMovement = new PlatformerMovement();

    [Header("AI Decision Making")]
    [Tooltip("Timing parameters for AI thinking and animation")]
    public AIThinking aiThinking = new AIThinking();

    #endregion

    #region Helper Methods

    /// Get chess mode weight for a tile based on AI type and distance to player
    public float GetChessModeWeight(PawnController.AIType aiType, bool isClosest, bool isFarthest, int directionIndex)
    {
        switch (aiType)
        {
            case PawnController.AIType.Basic:
                return isClosest ? chessModeWeights.basicClosestWeight : chessModeWeights.basicOtherWeight;

            case PawnController.AIType.Handcannon:
                return isClosest ? chessModeWeights.handcannonClosestWeight : chessModeWeights.handcannonOtherWeight;

            case PawnController.AIType.Shotgun:
                if (isClosest)
                    return chessModeWeights.shotgunClosestWeight;
                else if (isFarthest)
                    return chessModeWeights.shotgunFarthestWeight;
                else if (directionIndex == 1 || directionIndex == 2) // Diagonal upper
                    return chessModeWeights.shotgunDiagonalWeight;
                else if (directionIndex == 4 || directionIndex == 5) // Side
                    return chessModeWeights.shotgunSideWeight;
                else
                    return chessModeWeights.shotgunFarthestWeight;

            case PawnController.AIType.Sniper:
                if (isFarthest)
                    return chessModeWeights.sniperFarthestWeight;
                else if (isClosest)
                    return chessModeWeights.sniperClosestWeight;
                else
                    return chessModeWeights.sniperMediumWeight;

            default:
                return 1f;
        }
    }

    /// Get fire interval multiplier for a modifier
    public float GetFireIntervalMultiplier(PawnController.Modifier modifier)
    {
        switch (modifier)
        {
            case PawnController.Modifier.Confrontational:
                return modifierEffects.confrontationalFireIntervalMultiplier;
            default:
                return 1.0f;
        }
    }

    /// Get firing delay multiplier for a modifier
    public float GetFiringDelayMultiplier(PawnController.Modifier modifier)
    {
        switch (modifier)
        {
            case PawnController.Modifier.Observant:
                return modifierEffects.observantFiringDelayMultiplier;
            case PawnController.Modifier.Reflexive:
                return modifierEffects.reflexiveFiringDelayMultiplier;
            default:
                return 1.0f;
        }
    }

    /// Get movement speed multiplier for a modifier
    public float GetMoveSpeedMultiplier(PawnController.Modifier modifier)
    {
        switch (modifier)
        {
            case PawnController.Modifier.Fleet:
                return modifierEffects.fleetMoveSpeedMultiplier;
            default:
                return 1.0f;
        }
    }

    #endregion
}
