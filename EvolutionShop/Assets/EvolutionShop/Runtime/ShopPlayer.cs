using UnityEngine;
using UnityEngine.InputSystem;

namespace EvolutionShop
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ShopPlayer : MonoBehaviour
    {
        public Camera View { get; private set; }
        public float Sensitivity { get; set; } = 1f;
        public bool InputEnabled { get; set; }
        CharacterController body;
        float pitch;
        public void Initialize(Camera view)
        {
            View = view;
            body = GetComponent<CharacterController>();
            body.height = 1.75f;
            body.radius = 0.3f;
            body.center = Vector3.up * 0.88f;
            body.stepOffset = 0.25f;
            // Preserve small movements at high frame rates, including editor batch playback.
            body.minMoveDistance = 0f;
            view.transform.SetParent(transform, false);
            view.transform.localPosition = new Vector3(0, 1.6f, 0);
        }
        void Update()
        {
            if (!InputEnabled || View == null) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * (0.075f * Sensitivity);
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -75, 75);
                View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                move.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                move.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            }
            move = Vector2.ClampMagnitude(move, 1);
            Vector3 worldMove = transform.right * move.x + transform.forward * move.y;
            body.Move((worldMove * 3.2f + Vector3.down * 3f) * Time.deltaTime);
        }
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            pitch = 0;
            View.transform.localRotation = Quaternion.identity;
        }
    }
}

