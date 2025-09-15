using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// Manage pawn HP and select an appropriate sprite from an array based on current HP.
/// The first sprite (index 0) represents 0 HP. If HP is higher than available sprites,
/// the highest-order sprite (last index) will be used.
public class PlayerPawn : MonoBehaviour
{
    // Maximum HP for the pawn.
    public int MaxHP = 3;
    // Starting HP applied each time the pawn is spawned (OnEnable).
    public int startingHP = 2;
    // Current HP.
    public int HP;
    // SpriteRenderer used to display the pawn; if null the component will be auto-cached.
    public SpriteRenderer spriteRenderer;
    // Array of sprites where index 0 => 0 HP, index 1 => 1 HP, etc.
    public Sprite[] hpSprites;
    // UnityEvent invoked whenever HP changes; provides the new HP as an int parameter.
    public UnityEvent<int> OnHPChanged;
    // UnityEvent invoked when pawn reaches 0 HP (death).
    //public UnityEvent OnDeath;

    // Ensure references and set starting HP on spawn/enable.
    private void OnEnable()
    {
        // Cache SpriteRenderer if not assigned.
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        // Ensure MaxHP is at least 1 to avoid weirdness.
        if (MaxHP < 1) MaxHP = 1;
        // Clamp startingHP to valid range then assign.
        startingHP = Mathf.Clamp(startingHP, 0, MaxHP);
        HP = startingHP;
        // Update visual to match starting HP immediately.
        UpdateSpriteForHP();
        // Notify listeners of initial HP value.
        OnHPChanged?.Invoke(HP);
    }

    // Public method to apply damage; returns true if player died.
    public bool TakeDamage(int amount, string source = "")
    {
        HP -= amount;
        if (HP <= 0)
        {
            HP = 0;
            UpdateSpriteForHP();
            Debug.Log($"[PlayerPawn] Killed by {source} at HP 0.");
            Death();
            return true;
        }
        Debug.Log($"[PlayerPawn] Took {amount} dmg from {source}. HP now {HP}/{MaxHP}.");
        UpdateSpriteForHP();
        return false;
    }

    // Public method to apply damage; negative values heal (use Heal for clarity).
    public void Damage(int amount = 1)
    {
        // Clamp amount to be non-negative; negative damage would heal accidentally.
        amount = Mathf.Max(0, amount);
        SetHP(HP - amount);
    }

    // Public method to heal the pawn by given amount.
    public void Heal(int amount = 1)
    {
        // Clamp amount to be non-negative.
        amount = Mathf.Max(0, amount);
        SetHP(HP + amount);
    }

    // Set HP directly and handle clamping, sprite update, events and death.
    public void SetHP(int newHP)
    {
        // Clamp to valid range [0, MaxHP].
        int clamped = Mathf.Clamp(newHP, 0, MaxHP);
        if (clamped == HP) return; // No change -> skip.
        HP = clamped;
        // Update sprite to reflect new HP value immediately.
        UpdateSpriteForHP();
        // Notify listeners about HP change.
        OnHPChanged?.Invoke(HP);
        // If reached zero, invoke death event.
        if (HP == 0) Death();
    }

    // Reset HP to spawn value (useful for respawn logic).
    public void ResetToSpawnHP()
    {
        SetHP(startingHP);
    }

    // Update the SpriteRenderer.sprite based on HP and hpSprites array.
    private void UpdateSpriteForHP()
    {
        // If there is no SpriteRenderer or no sprites provided, nothing to update.
        if (spriteRenderer == null || hpSprites == null || hpSprites.Length == 0) 
        {
            Debug.LogWarning("[PlayerPawn] UpdateSpriteForHP: No sprites.");
            return; 
        }
        // Determine which sprite index to use:
        // Index mapping rule: index 0 => 0HP. If HP is larger than available indices,
        // use the last sprite (highest-order) as a fallback.
        // Example: HP=3 and hpSprites.Length=2 -> use index 1 (last available).
        int spriteIndex = HP; // initially map HP to same index
        if (spriteIndex >= hpSprites.Length) spriteIndex = hpSprites.Length - 1; // Fallback to last
        if (spriteIndex < 0) spriteIndex = 0; // safety clamp
        spriteRenderer.sprite = hpSprites[spriteIndex];
        Debug.Log($"[PlayerPawn] spriteRenderer -> sprite = {spriteIndex}");
    }

    // Handle death: invoke event and optionally disable the GameObject (customise as needed).
    private void Death()
    {
        // Fire the death event for external listeners to react (VFX, sound, respawn logic).

    }

    // Optional convenience: get a normalised health fraction in [0,1] for UI bars.
    public float GetHealthFraction()
    {
        if (MaxHP <= 0) return 0f;
        return (float)HP / (float)MaxHP;
    }

    // Update method for testing HP sprite changes via keyboard input.
    // Supports the new Input System if present, otherwise falls back to legacy Input.
    /* private void LateUpdate()
    {
        // Try to use the new Input System first for reliable editor/mobile parity.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            // Damage by 1 when 'D' is pressed.
            if (kb.dKey.wasPressedThisFrame)
            {
                Damage(1);
                Debug.Log($"[PlayerPawn] Damaged -> HP = {HP}");
            }
            // Heal by 1 when 'H' is pressed.
            if (kb.hKey.wasPressedThisFrame)
            {
                Heal(1);
                Debug.Log($"[PlayerPawn] Healed -> HP = {HP}");
            }
            // Reset to spawn HP when 'R' is pressed.
            if (kb.rKey.wasPressedThisFrame)
            {
                ResetToSpawnHP();
                Debug.Log($"[PlayerPawn] Reset -> HP = {HP}");
            }
            // Direct-set HP using number keys 0..9 for quick testing.
            // Map numeric key press to concrete HP value and clamp inside SetHP().
            if (kb.digit0Key.wasPressedThisFrame) { SetHP(0); Debug.Log($"[PlayerPawn] SetHP 0 -> HP = {HP}"); }
            if (kb.digit1Key.wasPressedThisFrame) { SetHP(1); Debug.Log($"[PlayerPawn] SetHP 1 -> HP = {HP}"); }
            if (kb.digit2Key.wasPressedThisFrame) { SetHP(2); Debug.Log($"[PlayerPawn] SetHP 2 -> HP = {HP}"); }
            if (kb.digit3Key.wasPressedThisFrame) { SetHP(3); Debug.Log($"[PlayerPawn] SetHP 3 -> HP = {HP}"); }
        }
        else
        {
            // Legacy Input fallback for projects using the old Input system.
            if (Input.GetKeyDown(KeyCode.D))
            {
                Damage(1);
                Debug.Log($"[PlayerPawn] Damaged -> HP = {HP}");
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                Heal(1);
                Debug.Log($"[PlayerPawn] Healed -> HP = {HP}");
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetToSpawnHP();
                Debug.Log($"[PlayerPawn] Reset -> HP = {HP}");
            }
            if (Input.GetKeyDown(KeyCode.Alpha0)) { SetHP(0); Debug.Log($"[PlayerPawn] SetHP 0 -> HP = {HP}"); }
            if (Input.GetKeyDown(KeyCode.Alpha1)) { SetHP(1); Debug.Log($"[PlayerPawn] SetHP 1 -> HP = {HP}"); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { SetHP(2); Debug.Log($"[PlayerPawn] SetHP 2 -> HP = {HP}"); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { SetHP(3); Debug.Log($"[PlayerPawn] SetHP 3 -> HP = {HP}"); }
        }
    } */
}