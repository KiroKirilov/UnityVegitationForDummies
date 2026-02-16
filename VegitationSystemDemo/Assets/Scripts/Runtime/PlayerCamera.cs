using UnityEngine;

    public class PlayerCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform target;
        [SerializeField] PlayerMotor playerMotor;

        [Header("Follow Settings")]
        [SerializeField] float distance = 8f;
        [SerializeField] float heightOffset = 2f;
        [SerializeField] float followSmoothTime = 0.1f;

        [Header("Orbit Settings")]
        [SerializeField] float mouseSensitivity = 0.15f;
        [SerializeField] float gamepadSensitivity = 120f;
        [SerializeField] float minPitch = -20f;
        [SerializeField] float maxPitch = 60f;

        [Header("Input")]
        [SerializeField] PlayerInputReader playerInput;
        [SerializeField] bool lockCursorWhileLooking = true;

        [Header("Dead Zones")]
        [SerializeField] float xzDeadZone = 0.3f;
        [SerializeField] float yDeadZone = 0.15f;
        [SerializeField] float yReturnSpeed = 2f;

        float yaw;
        float pitch = 20f;

        Vector3 currentFollowPoint;
        Vector3 lockedPosition;
        float lockedY;
        Vector3 velocity;

        bool wasJumping;
        bool initialized;

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Start()
        {
            if (target == null) return;

            currentFollowPoint = target.position;
            currentFollowPoint.y += heightOffset;
            lockedPosition = target.position;
            lockedY = target.position.y;
            yaw = target.eulerAngles.y;
            initialized = true;

            UpdateCameraPosition(true);
        }

        void LateUpdate()
        {
            if (target == null || playerInput == null) return;

            if (!initialized)
            {
                currentFollowPoint = target.position;
                lockedPosition = target.position;
                lockedY = target.position.y;
                initialized = true;
            }

            HandleRotation();
            UpdateFollowPoint();
            UpdateCameraPosition(false);
            UpdateCursor();
        }

        void HandleRotation()
        {
            Vector2 look = playerInput.LookInput;

            if (playerInput.IsMouseLook)
            {
                if (playerInput.IsMouseControllingCamera)
                {
                    yaw += look.x * mouseSensitivity;
                    pitch -= look.y * mouseSensitivity;
                }
            }
            else if (look.sqrMagnitude > 0.01f)
            {
                yaw += look.x * gamepadSensitivity * Time.deltaTime;
                pitch -= look.y * gamepadSensitivity * Time.deltaTime;
            }

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        void UpdateCursor()
        {
            if (!lockCursorWhileLooking) return;

            if (playerInput.IsMouseControllingCamera)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void UpdateFollowPoint()
        {
            Vector3 targetPos = target.position;
            bool isJumping = playerMotor != null && !playerMotor.IsGrounded;

            float xDiff = targetPos.x - lockedPosition.x;
            float zDiff = targetPos.z - lockedPosition.z;
            float horizontalDist = Mathf.Sqrt(xDiff * xDiff + zDiff * zDiff);

            if (horizontalDist > xzDeadZone)
            {
                float overshoot = horizontalDist - xzDeadZone;
                float followStrength = Mathf.Clamp01(overshoot / xzDeadZone) * 10f;
                lockedPosition.x = Mathf.Lerp(lockedPosition.x, targetPos.x, Time.deltaTime * followStrength);
                lockedPosition.z = Mathf.Lerp(lockedPosition.z, targetPos.z, Time.deltaTime * followStrength);
            }

            currentFollowPoint.x = Mathf.SmoothDamp(currentFollowPoint.x, lockedPosition.x, ref velocity.x, followSmoothTime);
            currentFollowPoint.z = Mathf.SmoothDamp(currentFollowPoint.z, lockedPosition.z, ref velocity.z, followSmoothTime);

            if (!isJumping)
            {
                float yDiff = targetPos.y - lockedY;
                if (Mathf.Abs(yDiff) > yDeadZone)
                {
                    float followSpeed = wasJumping ? yReturnSpeed : yReturnSpeed * 3f;
                    lockedY = Mathf.Lerp(lockedY, targetPos.y, Time.deltaTime * followSpeed);
                }
            }

            wasJumping = isJumping;
            currentFollowPoint.y = lockedY + heightOffset;
        }

        void UpdateCameraPosition(bool snap)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            Vector3 desiredPosition = currentFollowPoint + offset;

            transform.position = snap
                ? desiredPosition
                : desiredPosition;

            transform.LookAt(currentFollowPoint);
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            currentFollowPoint = target.position;
            currentFollowPoint.y += heightOffset;
            lockedPosition = target.position;
            lockedY = target.position.y;
            velocity = Vector3.zero;
            UpdateCameraPosition(true);
        }

        public void SetOrbitAngles(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (target == null) return;

            Vector3 targetPos = Application.isPlaying ? lockedPosition : target.position;
            Vector3 followPoint = Application.isPlaying ? currentFollowPoint : target.position + Vector3.up * heightOffset;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            DrawWireCircle(new Vector3(targetPos.x, targetPos.y + 0.05f, targetPos.z), xzDeadZone, 32);
            DrawWireCircle(new Vector3(targetPos.x, targetPos.y + heightOffset, targetPos.z), xzDeadZone, 32);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 off = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * xzDeadZone;
                Gizmos.DrawLine(
                    new Vector3(targetPos.x, targetPos.y + 0.05f, targetPos.z) + off,
                    new Vector3(targetPos.x, targetPos.y + heightOffset, targetPos.z) + off
                );
            }

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            float yBase = Application.isPlaying ? lockedY : targetPos.y;
            DrawWireCircle(new Vector3(targetPos.x, yBase + yDeadZone, targetPos.z), 0.5f, 16);
            DrawWireCircle(new Vector3(targetPos.x, yBase - yDeadZone, targetPos.z), 0.5f, 16);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(followPoint, 0.2f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(followPoint, transform.position);
        }

        static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                float a2 = ((i + 1) / (float)segments) * 360f * Mathf.Deg2Rad;
                Vector3 p1 = center + new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * radius;
                Vector3 p2 = center + new Vector3(Mathf.Sin(a2), 0, Mathf.Cos(a2)) * radius;
                Gizmos.DrawLine(p1, p2);
            }
        }
#endif
    }
