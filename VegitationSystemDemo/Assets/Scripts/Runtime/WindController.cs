using UnityEngine;

    public class WindController : MonoBehaviour
    {
        [Header("Base Wind Settings")]
        [SerializeField, Range(0f, 5f)] float windSpeed = 1f;
        [SerializeField, Range(0f, 0.3f)] float windStrength = 0.05f;
        [SerializeField] Vector2 windDirection = new Vector2(1f, 0.3f);
        [SerializeField, Range(0f, 2f)] float noiseScale = 0.5f;

        [Header("Gusts")]
        [SerializeField] bool enableGusts = false;
        [SerializeField, Range(0.01f, 1f)] float gustFrequency = 0.2f;
        [SerializeField, Range(0f, 0.2f)] float gustStrengthBoost = 0.05f;

        [Header("Player Interaction")]
        [SerializeField] bool enablePlayerInteraction = true;
        [SerializeField] Transform playerTransform;
        [SerializeField] Rigidbody playerRigidbody;
        [SerializeField, Range(0.5f, 5f)] float interactionRadius = 2f;
        [SerializeField, Range(0f, 1f)] float interactionStrength = 0.3f;

        static readonly int WindSpeedId = Shader.PropertyToID("_GlobalWindSpeed");
        static readonly int WindStrengthId = Shader.PropertyToID("_GlobalWindStrength");
        static readonly int WindDirectionId = Shader.PropertyToID("_GlobalWindDirection");
        static readonly int NoiseScaleId = Shader.PropertyToID("_GlobalNoiseScale");

        static readonly int PlayerPosId = Shader.PropertyToID("_PlayerInteractionPos");
        static readonly int PlayerVelocityId = Shader.PropertyToID("_PlayerInteractionVelocity");
        static readonly int InteractionRadiusId = Shader.PropertyToID("_InteractionRadius");
        static readonly int InteractionStrengthId = Shader.PropertyToID("_InteractionStrength");

        float currentStrength;

        void OnEnable()
        {
            ApplyWindSettings();
        }

        void Update()
        {
            currentStrength = windStrength;

            if (enableGusts)
            {
                float gustNoise = Mathf.PerlinNoise(Time.time * gustFrequency, 0f);
                currentStrength += gustNoise * gustStrengthBoost;
            }

            Shader.SetGlobalFloat(WindStrengthId, currentStrength);

            UpdatePlayerInteraction();
        }

        void UpdatePlayerInteraction()
        {
            if (!enablePlayerInteraction || playerTransform == null)
            {
                Shader.SetGlobalFloat(InteractionStrengthId, 0f);
                return;
            }

            Vector3 playerPos = playerTransform.position;
            Vector3 playerVelocity = playerRigidbody != null ? playerRigidbody.linearVelocity : Vector3.zero;

            Shader.SetGlobalVector(PlayerPosId, playerPos);
            Shader.SetGlobalVector(PlayerVelocityId, playerVelocity);
            Shader.SetGlobalFloat(InteractionRadiusId, interactionRadius);
            Shader.SetGlobalFloat(InteractionStrengthId, interactionStrength);
        }

        void ApplyWindSettings()
        {
            Shader.SetGlobalFloat(WindSpeedId, windSpeed);
            Shader.SetGlobalFloat(WindStrengthId, windStrength);
            Shader.SetGlobalVector(WindDirectionId, new Vector4(windDirection.x, windDirection.y, 0, 0));
            Shader.SetGlobalFloat(NoiseScaleId, noiseScale);
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(WindSpeedId, 0);
            Shader.SetGlobalFloat(WindStrengthId, 0);
            Shader.SetGlobalFloat(InteractionStrengthId, 0);
        }

        public void SetWindStrength(float strength)
        {
            windStrength = Mathf.Clamp(strength, 0f, 0.3f);
            Shader.SetGlobalFloat(WindStrengthId, windStrength);
        }

        public void SetWindSpeed(float speed)
        {
            windSpeed = Mathf.Clamp(speed, 0f, 5f);
            Shader.SetGlobalFloat(WindSpeedId, windSpeed);
        }

        public void SetWindDirection(Vector2 direction)
        {
            windDirection = direction.normalized;
            Shader.SetGlobalVector(WindDirectionId, new Vector4(windDirection.x, windDirection.y, 0, 0));
        }
    }
