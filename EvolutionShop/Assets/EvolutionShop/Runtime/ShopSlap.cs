using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    public sealed partial class ShopSession
    {
        public string SlapKey { get; private set; } = "F";
        public float HumorVolume = 1f;
        public bool ReducedGags;
        public bool WaitingForSlapKey { get; private set; }
        public CustomerVisit SlapTarget { get; private set; }
        public float SlapCooldownRemaining => slapCooldown.Remaining;
        public SlapHand SlapGesture { get; private set; }
        readonly SlapCooldown slapCooldown = new SlapCooldown();

        void InitializeSlap()
        {
            SlapGesture = gameObject.AddComponent<SlapHand>();
            SlapGesture.Initialize(Player.View);
        }

        public void BeginSlapRebind()
        {
            if (Panel != ShopPanel.Pause) return;
            WaitingForSlapKey = true;
            Notify("Press a letter for slap. Movement and E stay reserved. Escape cancels.", 8);
        }

        public bool SetSlapBinding(string key)
        {
            if (Panel != ShopPanel.Pause || !SlapRules.IsAllowedKey(key)) return false;
            SlapKey = key; WaitingForSlapKey = false;
            SaveNow();
            Notify("Slap is now " + SlapKey + ". Aim at a nearby customer with all panels closed.");
            return true;
        }

        bool HandleSlapRebind(Keyboard keyboard)
        {
            if (!WaitingForSlapKey) return false;
            if (Panel != ShopPanel.Pause) { WaitingForSlapKey = false; return false; }
            if (keyboard == null) return true;
            if (keyboard.escapeKey.wasPressedThisFrame)
            { WaitingForSlapKey = false; Notify("Slap binding unchanged."); return true; }
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                string candidate = key.keyCode.ToString();
                if (!SetSlapBinding(candidate)) Notify("Choose a letter other than W, A, S, D or E. Escape cancels.");
                return true; // Binding a key must never also perform the action.
            }
            return true;
        }

        void UpdateSlapInput(Keyboard keyboard, bool gameplayAtFrameStart)
        {
            SlapTarget = Panel == ShopPanel.None ? Customers.FindSlapTarget(Player.View) : null;
            if (!gameplayAtFrameStart || Panel != ShopPanel.None || keyboard == null ||
                (ValidationMode ? !AllowValidationInput : !Application.isFocused)) return;
            if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed ||
                keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) return;
            if (Enum.TryParse<Key>(SlapKey, out var key) && keyboard[key].wasPressedThisFrame)
                TrySlapFocusedCustomer();
        }

        public bool TrySlapFocusedCustomer()
        {
            if (Panel != ShopPanel.None || Paused || SlapCooldownRemaining > 0 ||
                (Cycle.Phase != ShopDayPhase.Open && Cycle.Phase != ShopDayPhase.Closing)) return false;
            // Resolve again at action time: a visible prompt is not authority over a stale visitor.
            var target = Customers.FindSlapTarget(Player.View);
            if (target == null || !Customers.Slap(target)) return false;
            slapCooldown.TryStart();
            SlapTarget = null;
            collectionNeedsRelease = Mouse.current != null && Mouse.current.leftButton.isPressed;
            StopHarvest(); World.SetHarvesting(false, null);
            SlapGesture.Play(ReducedGags);
            World.SetHandContext(Player.transform.position, false, SlapGesture.IsPlaying);
            StartSlapComedy();
            sound.Slap(HumorVolume);
            Notify("HEY! MY DIGNITY!  The customer leaves. Reputation -1; unpaid stock stays.", 4);
            return true;
        }
    }
}
