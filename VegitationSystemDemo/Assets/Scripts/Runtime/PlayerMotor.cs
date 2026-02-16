using UnityEngine;

    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerInputReader playerInput;
        [SerializeField] Transform cameraTransform;
        [SerializeField] Animator animator;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float rotationSpeed = 10f;
        [SerializeField] float acceleration = 50f;
        [SerializeField] float deceleration = 40f;

        [Header("Jump")]
        [SerializeField] float jumpForce = 7f;
        [SerializeField, Range(0f, 1f)] float airControlMultiplier = 0.4f;
        [SerializeField] float fallMultiplier = 2.5f;

        [Header("Ground Check")]
        [SerializeField] float groundCheckRadius = 0.2f;
        [SerializeField] float groundCheckDistance = 0.1f;
        [SerializeField] Vector3 groundCheckOffset = new Vector3(0f, 0.25f, 0f);
        [SerializeField] LayerMask groundMask = 1 << 6;

        Rigidbody rb;
        bool isGrounded;
        float currentSpeed;
        Vector3 moveDirection;

        public bool IsGrounded => isGrounded;

        static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        void FixedUpdate()
        {
            CheckGrounded();
            HandleJump();
            Move();
            ApplyEnhancedGravity();
        }

        void Update()
        {
            if (animator != null)
                animator.SetFloat(SpeedHash, currentSpeed);
        }

        void CheckGrounded()
        {
            Vector3 origin = transform.position + groundCheckOffset;
            isGrounded = Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out _, groundCheckDistance, groundMask);
        }

        void HandleJump()
        {
            if (playerInput.JumpPressed && isGrounded)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = jumpForce;
                rb.linearVelocity = vel;
                playerInput.ConsumeJump();
            }
        }

        void Move()
        {
            Vector2 input = playerInput.MoveInput;
            bool hasInput = input.sqrMagnitude > 0.01f;

            if (hasInput && cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = cameraTransform.right;
                camRight.y = 0f;
                camRight.Normalize();

                moveDirection = camRight * input.x + camForward * input.y;
                moveDirection.Normalize();
            }

            float targetSpeed = hasInput
                ? (playerInput.SprintHeld ? sprintSpeed : walkSpeed) * Mathf.Clamp01(input.magnitude)
                : 0f;

            float rate = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);

            Vector3 desiredHorizontal = moveDirection * currentSpeed;

            if (!isGrounded)
            {
                Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                desiredHorizontal = Vector3.Lerp(currentHorizontal, desiredHorizontal, airControlMultiplier);
            }

            rb.linearVelocity = new Vector3(desiredHorizontal.x, rb.linearVelocity.y, desiredHorizontal.z);

            if (hasInput && moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }

        void ApplyEnhancedGravity()
        {
            if (rb.linearVelocity.y < 0f)
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }
