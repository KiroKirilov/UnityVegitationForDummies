using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    InputSystem_Actions actions;

    Vector2 moveInput;
    Vector2 lookInput;
    bool jumpPressed;
    bool sprintHeld;
    bool isMouseLook;
    bool mouseControllingCamera;

    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;
    public bool JumpPressed => jumpPressed;
    public bool SprintHeld => sprintHeld;
    public bool IsMouseLook => isMouseLook;
    public bool IsMouseControllingCamera => mouseControllingCamera;

    public void ConsumeJump() => jumpPressed = false;

    void Awake()
    {
        actions = new InputSystem_Actions();
        actions.Player.AddCallbacks(this);
    }

    void OnEnable() => actions.Player.Enable();
    void OnDisable() => actions.Player.Disable();

    void OnDestroy()
    {
        actions.Player.RemoveCallbacks(this);
        actions.Dispose();
    }

    void LateUpdate()
    {
        jumpPressed = false;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
        isMouseLook = context.control.device is Mouse;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            jumpPressed = true;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = !context.canceled;
    }

    public void OnAttack(InputAction.CallbackContext context) { }
    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnPrevious(InputAction.CallbackContext context) { }
    public void OnNext(InputAction.CallbackContext context) { }

    public void OnMouseControlCamera(InputAction.CallbackContext context)
    {
        mouseControllingCamera = !context.canceled;
    }

}
