using UnityEngine;
using UnityEngine.InputSystem;

namespace FuturatsTest.Cameras
{
    public class CameraController : MonoBehaviour
    {

        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference zoomAction;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 10f;

        [Header("Zoom")]
        [SerializeField] private float zoomStep = 2f;
        [SerializeField] private float zoomSmoothness = 10f;
        [SerializeField] private float maxZoomIn = 100f;
        [SerializeField] private float maxZoomOut = 100f;

        private float currentZoom;
        private float targetZoom;


        private void OnEnable()
        {
            if (moveAction != null) moveAction.action.Enable();
            if (zoomAction != null) zoomAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null) moveAction.action.Disable();
            if (zoomAction != null) zoomAction.action.Disable();
        }

        private void Update()
        {
            HandleMovement();
            HandleZoom();
        }

        private void HandleMovement()
        {
            Vector2 input = GetMoveInput();
            if (input == Vector2.zero) return;

            Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 forward = facing * Vector3.forward;
            Vector3 right = facing * Vector3.right;

            Vector3 moveDirection = (forward * input.y + right * input.x).normalized;
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }

        private void HandleZoom()
        {
            float scroll = GetScrollInput();
            if (scroll != 0f)
            {
                targetZoom += Mathf.Sign(scroll) * zoomStep;
                targetZoom = Mathf.Clamp(targetZoom, -maxZoomOut, maxZoomIn);
            }

            float newZoom = Mathf.Lerp(currentZoom, targetZoom, zoomSmoothness * Time.deltaTime);
            transform.position += transform.forward * (newZoom - currentZoom);
            currentZoom = newZoom;
        }

        private Vector2 GetMoveInput()
        {
            if (moveAction == null) return Vector2.zero;
            return moveAction.action.ReadValue<Vector2>();
        }

        private float GetScrollInput()
        {
            if (zoomAction == null) return 0f;
            return zoomAction.action.ReadValue<float>();
        }

    }
}