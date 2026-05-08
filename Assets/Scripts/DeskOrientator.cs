using UnityEngine;

public class DeskOrientator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform deskRoot;
    [SerializeField] private Transform cameraTransform; 

    [Header("Settings")]
    [SerializeField] private float distanceFromPlayer = 2.0f; 
    [SerializeField] private float transitionSpeed = 5.0f; 
    [SerializeField] private float deskHeightOffset = -0.5f; 

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _isTransitioning = false;

    private void Start()
    {
        if (deskRoot == null)
        {
            Debug.LogError("[DeskOrientator] deskRoot is not assigned.");
            return;
        }

        if (cameraTransform == null)
        {
            // Try to find the main camera automatically
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null)
            {
                Debug.LogError("[DeskOrientator] No camera found. Please assign cameraTransform.");
                return;
            }
        }

        _targetPosition = deskRoot.position;
        _targetRotation = deskRoot.rotation;
    }

    private void Update()
    {
        if (Input.GetKeyDown("joystick button 1"))
            OrientDeskToPlayer();

        // Smoothly transition desk to target position and rotation
        if (_isTransitioning)
        {
            deskRoot.position = Vector3.Lerp(deskRoot.position, _targetPosition, Time.deltaTime * transitionSpeed);
            deskRoot.rotation = Quaternion.Lerp(deskRoot.rotation, _targetRotation, Time.deltaTime * transitionSpeed);

            // Stop transitioning when close enough
            if (Vector3.Distance(deskRoot.position, _targetPosition) < 0.01f &&
                Quaternion.Angle(deskRoot.rotation, _targetRotation) < 0.1f)
            {
                deskRoot.position = _targetPosition;
                deskRoot.rotation = _targetRotation;
                _isTransitioning = false;
            }
        }
    }

    private void OrientDeskToPlayer()
    {
        if (cameraTransform == null || deskRoot == null) return;

        // Get horizontal forward direction only — ignore vertical tilt
        Vector3 horizontalForward = cameraTransform.forward;
        horizontalForward.y = 0f;
        horizontalForward.Normalize();

        // If player is looking straight up or down, fall back to world forward
        if (horizontalForward == Vector3.zero)
            horizontalForward = Vector3.forward;

        // Place desk in front of player at eye level with height offset
        Vector3 playerPosition = cameraTransform.position;
        _targetPosition = new Vector3(
            playerPosition.x + horizontalForward.x * distanceFromPlayer,
            deskRoot.position.y,  // Keep desk at its current height
            playerPosition.z + horizontalForward.z * distanceFromPlayer
        ) + Vector3.up * deskHeightOffset;

        // Rotate desk to face the player horizontally
        _targetRotation = Quaternion.LookRotation(horizontalForward, Vector3.up);

        _isTransitioning = true;

        Debug.Log("[DeskOrientator] Desk repositioned to player's view.");
    }
}