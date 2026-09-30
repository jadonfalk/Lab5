using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public sealed class ShowcasePlayer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    private CharacterController controller;
    private void Awake() { controller = GetComponent<CharacterController>(); }
    private void Update()
    {
        Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y++;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y--;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x++;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x--;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        Vector3 movement = new Vector3(input.x, 0, input.y);
        controller.SimpleMove(Vector3.ClampMagnitude(movement, 1f) * moveSpeed);
        if (movement.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Euler(0, Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg, 0);
    }
}
