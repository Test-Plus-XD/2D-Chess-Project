using System;
using UnityEngine;

/// ScriptableObject for configuring opponent pawn AI behaviours and modifiers.
/// Create instances via Assets → Create → Game → Pawn Customiser to tweak AI parameters in Unity Editor.
/// Reference this in Pawn Controller to apply custom AI behaviour and modifier configurations.
[CreateAssetMenu(fileName = "New Pawn Customiser", menuName = "Game/Pawn Customiser", order = 2)]
public class PawnCustomiser : ScriptableObject
{
    // Configuration for per-AI-type chess mode weights.
    [Serializable]
    public class AITypeWeights
    {
        [Tooltip("Weight for the tile closest to player")]
        [Range(0f, 10f)]
        public float closestWeight = 5f;
        [Tooltip("Weight for diagonal moves (Shotgun AI only)")]
        [Range(0f, 10f)]
        public float diagonalWeight = 3f;
        [Tooltip("Weight for side moves (Shotgun AI only)")]
        [Range(0f, 10f)]
        public float sideWeight = 2f;
        [Tooltip("Weight for tiles farthest from player")]
        [Range(0f, 10f)]
        public float farthestWeight = 1f;
    }

    // Configuration for per-AI-type standoff mode distance preferences.
    [Serializable]
    public class AITypeStandoffDistances
    {
        [Tooltip("Minimum optimal distance for this AI type (Standoff mode)")]
        [Range(0f, 10f)]
        public float minDistance = 2f;
        [Tooltip("Maximum optimal distance for this AI type (Standoff mode)")]
        [Range(0f, 10f)]
        public float maxDistance = 4f;
    }

    // Configuration for modifier effects and multipliers.
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

    // Configuration for platformer movement and jumping.
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

    // Configuration for AI decision-making timing.
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

    [Header("Chess Mode Weights")]
    [Tooltip("Weight configurations for Basic AI type in chess mode")]
    public AITypeWeights basicChessWeights = new AITypeWeights() { closestWeight = 5f, farthestWeight = 1f };
    [Tooltip("Weight configurations for Handcannon AI type in chess mode")]
    public AITypeWeights handcannonChessWeights = new AITypeWeights() { closestWeight = 3f, farthestWeight = 1f };
    [Tooltip("Weight configurations for Shotgun AI type in chess mode")]
    public AITypeWeights shotgunChessWeights = new AITypeWeights() { closestWeight = 4f, diagonalWeight = 3f, sideWeight = 2f, farthestWeight = 1f };
    [Tooltip("Weight configurations for Sniper AI type in chess mode")]
    public AITypeWeights sniperChessWeights = new AITypeWeights() { closestWeight = 1f, farthestWeight = 4f };

    [Header("Standoff Mode Distances")]
    [Tooltip("Distance preferences for Basic AI type in standoff mode")]
    public AITypeStandoffDistances basicStandoffDistances = new AITypeStandoffDistances() { minDistance = 1.5f, maxDistance = 10f };
    [Tooltip("Distance preferences for Handcannon AI type in standoff mode")]
    public AITypeStandoffDistances handcannonStandoffDistances = new AITypeStandoffDistances() { minDistance = 2f, maxDistance = 4f };
    [Tooltip("Distance preferences for Shotgun AI type in standoff mode")]
    public AITypeStandoffDistances shotgunStandoffDistances = new AITypeStandoffDistances() { minDistance = 1.5f, maxDistance = 10f };
    [Tooltip("Distance preferences for Sniper AI type in standoff mode")]
    public AITypeStandoffDistances sniperStandoffDistances = new AITypeStandoffDistances() { minDistance = 6f, maxDistance = 20f };

    [Header("Modifier Effects")]
    [Tooltip("Multipliers and values for modifier effects")]
    public ModifierEffects modifierEffects = new ModifierEffects();

    [Header("Platformer Movement")]
    [Tooltip("Movement and jumping parameters for standoff mode")]
    public PlatformerMovement platformerMovement = new PlatformerMovement();

    [Header("AI Decision Making")]
    [Tooltip("Timing parameters for AI thinking and animation")]
    public AIThinking aiThinking = new AIThinking();

    [Header("Modifier Visual Icons")]
    [Tooltip("Sprite for Tenacious modifier icon")]
    public Sprite tenaciousIcon;
    [Tooltip("Sprite for Confrontational modifier icon")]
    public Sprite confrontationalIcon;
    [Tooltip("Sprite for Fleet modifier icon")]
    public Sprite fleetIcon;
    [Tooltip("Sprite for Observant modifier icon")]
    public Sprite observantIcon;
    [Tooltip("Sprite for Reflexive modifier icon")]
    public Sprite reflexiveIcon;

    // Get chess mode weights for the specified AI type.
    public AITypeWeights GetChessWeights(PawnController.AIType aiType)
    {
        return aiType switch
        {
            PawnController.AIType.Basic => basicChessWeights,
            PawnController.AIType.Handcannon => handcannonChessWeights,
            PawnController.AIType.Shotgun => shotgunChessWeights,
            PawnController.AIType.Sniper => sniperChessWeights,
            _ => basicChessWeights
        };
    }

    // Get standoff mode distances for the specified AI type.
    public AITypeStandoffDistances GetStandoffDistances(PawnController.AIType aiType)
    {
        return aiType switch
        {
            PawnController.AIType.Basic => basicStandoffDistances,
            PawnController.AIType.Handcannon => handcannonStandoffDistances,
            PawnController.AIType.Shotgun => shotgunStandoffDistances,
            PawnController.AIType.Sniper => sniperStandoffDistances,
            _ => basicStandoffDistances
        };
    }

    // Get fire interval multiplier for a modifier.
    public float GetFireIntervalMultiplier(PawnController.Modifier modifier)
    {
        return modifier switch
        {
            PawnController.Modifier.Confrontational => modifierEffects.confrontationalFireIntervalMultiplier,
            _ => 1.0f
        };
    }

    // Get firing delay multiplier for a modifier.
    public float GetFiringDelayMultiplier(PawnController.Modifier modifier)
    {
        return modifier switch
        {
            PawnController.Modifier.Observant => modifierEffects.observantFiringDelayMultiplier,
            PawnController.Modifier.Reflexive => modifierEffects.reflexiveFiringDelayMultiplier,
            _ => 1.0f
        };
    }

    // Get movement speed multiplier for a modifier.
    public float GetMoveSpeedMultiplier(PawnController.Modifier modifier)
    {
        return modifier switch
        {
            PawnController.Modifier.Fleet => modifierEffects.fleetMoveSpeedMultiplier,
            _ => 1.0f
        };
    }

    // Get the sprite icon for a specific modifier.
    public Sprite GetModifierIcon(PawnController.Modifier modifier)
    {
        return modifier switch
        {
            PawnController.Modifier.Tenacious => tenaciousIcon,
            PawnController.Modifier.Confrontational => confrontationalIcon,
            PawnController.Modifier.Fleet => fleetIcon,
            PawnController.Modifier.Observant => observantIcon,
            PawnController.Modifier.Reflexive => reflexiveIcon,
            _ => null
        };
    }
}