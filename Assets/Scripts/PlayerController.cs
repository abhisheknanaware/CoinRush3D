using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraPivot;
    public float walkSpeed = 6f;
    public float sprintMultiplier = 1.6f;
    public float jumpHeight = 1.4f;
    public float gravity = -22f;
    public float lookSensitivity = 0.12f;
    public float acceleration = 12f;
    public float eyeHeight = 1.6f;

    CharacterController controller;
    Vector3 velocity;
    Vector3 knockback;
    float verticalVelocity;
    float yaw;
    float pitch;
    float bobPhase;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
    }

    static bool Playing => GameManager.Instance && GameManager.Instance.State == GameState.Playing;

    void Update()
    {
        if (!Playing) return;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        float dt = Time.deltaTime;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector2 input = Vector2.zero;
        bool sprint = false;
        bool jump = false;
        if (kb != null)
        {
            input.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            input.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            jump = kb.spaceKey.wasPressedThisFrame;
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) ChangeSpeed(1f);
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) ChangeSpeed(-1f);
        }

        Vector3 wish = (transform.right * input.x + transform.forward * input.y);
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        wish *= walkSpeed * (sprint ? sprintMultiplier : 1f);
        velocity = Vector3.Lerp(velocity, wish, 1f - Mathf.Exp(-acceleration * dt));

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;
            if (jump) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalVelocity += gravity * dt;

        Vector3 move = (velocity + knockback) * dt + Vector3.up * verticalVelocity * dt;
        controller.Move(move);
        knockback = Vector3.Lerp(knockback, Vector3.zero, 1f - Mathf.Exp(-6f * dt));

        bool moving = controller.isGrounded && velocity.magnitude > 0.5f;
        bobPhase = moving ? bobPhase + dt * velocity.magnitude * 1.8f : 0f;
        cameraPivot.localPosition = new Vector3(0f, eyeHeight + Mathf.Sin(bobPhase) * 0.05f, 0f);
    }

    public void ChangeSpeed(float delta)
    {
        walkSpeed = Mathf.Clamp(walkSpeed + delta, 3f, 10f);
    }

    public void Knockback(Vector3 force)
    {
        knockback = force;
        verticalVelocity = 4f;
    }

    public void Teleport(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        velocity = Vector3.zero;
        knockback = Vector3.zero;
    }
}
