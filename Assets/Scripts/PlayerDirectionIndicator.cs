using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace CSE165.Project1
{
    [DisallowMultipleComponent]
    public sealed class PlayerDirectionIndicator : MonoBehaviour
    {
        [SerializeField]
        XROrigin m_XROrigin;

        [SerializeField]
        Transform m_MoveStem;

        [SerializeField]
        Transform m_MoveHead;

        [SerializeField]
        GameObject m_TurnLeftIndicator;

        [SerializeField]
        GameObject m_TurnRightIndicator;

        [SerializeField]
        float m_MaxTeleportDistance = 14f;

        [SerializeField]
        float m_GroundOffset = 0.02f;

        [SerializeField]
        float m_MinimumUpAlignment = 0.7f;

        [SerializeField]
        bool m_AllowLeftTriggerTeleport = true;

        [SerializeField]
        bool m_AllowRightTriggerTeleport = true;

        [SerializeField]
        float m_TriggerThreshold = 0.65f;

        [SerializeField]
        float m_FallbackTeleportDistance = 3.5f;

        InputDevice m_LeftController;
        InputDevice m_RightController;
        bool m_LeftTriggerWasPressed;
        bool m_RightTriggerWasPressed;
        bool m_HasTeleportTarget;
        Vector3 m_TeleportTargetPosition;
        Vector3 m_TeleportTargetForward;
        TeleportationArea m_TeleportArea;
        Collider m_TeleportSurfaceCollider;

        public void SetOrigin(XROrigin xrOrigin)
        {
            m_XROrigin = xrOrigin;
        }

        public void SetVisuals(Transform moveStem, Transform moveHead, GameObject turnLeftIndicator, GameObject turnRightIndicator)
        {
            m_MoveStem = moveStem;
            m_MoveHead = moveHead;
            m_TurnLeftIndicator = turnLeftIndicator;
            m_TurnRightIndicator = turnRightIndicator;
        }

        void LateUpdate()
        {
            if (m_XROrigin == null || m_XROrigin.Camera == null)
                return;

            RefreshControllers();
            m_HasTeleportTarget = TryGetTeleportTarget(out m_TeleportTargetPosition, out m_TeleportTargetForward);
            RefreshVisuals();

            var leftTriggerPressed = ReadTriggerButton(ref m_LeftController, XRNode.LeftHand);
            var rightTriggerPressed = ReadTriggerButton(ref m_RightController, XRNode.RightHand);

            if (m_HasTeleportTarget)
            {
                if (m_AllowLeftTriggerTeleport && leftTriggerPressed && !m_LeftTriggerWasPressed)
                    TeleportToTarget();
                else if (m_AllowRightTriggerTeleport && rightTriggerPressed && !m_RightTriggerWasPressed)
                    TeleportToTarget();
            }

            m_LeftTriggerWasPressed = leftTriggerPressed;
            m_RightTriggerWasPressed = rightTriggerPressed;
        }

        void RefreshControllers()
        {
            if (!m_LeftController.isValid)
                m_LeftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            if (!m_RightController.isValid)
                m_RightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            if (m_TeleportArea == null)
            {
                m_TeleportArea = FindAnyObjectByType<TeleportationArea>();
                if (m_TeleportArea != null)
                    m_TeleportSurfaceCollider = m_TeleportArea.GetComponent<Collider>();
            }
        }

        bool TryGetTeleportTarget(out Vector3 targetPosition, out Vector3 targetForward)
        {
            if (m_TeleportArea == null || m_TeleportSurfaceCollider == null)
            {
                targetPosition = default;
                targetForward = Vector3.forward;
                return false;
            }

            var cameraTransform = m_XROrigin.Camera.transform;
            var cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (cameraForward.sqrMagnitude < 0.001f)
                cameraForward = m_XROrigin.transform.forward;

            cameraForward.Normalize();
            targetForward = cameraForward;
            targetPosition = default;

            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hit, m_MaxTeleportDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return TryGetProjectedTeleportTarget(cameraTransform, out targetPosition);

            if (Vector3.Dot(hit.normal, Vector3.up) < m_MinimumUpAlignment)
                return TryGetProjectedTeleportTarget(cameraTransform, out targetPosition);

            var teleportArea = hit.collider.GetComponentInParent<TeleportationArea>();
            if (teleportArea == null)
                return TryGetProjectedTeleportTarget(cameraTransform, out targetPosition);

            targetPosition = hit.point;
            return true;
        }

        bool TryGetProjectedTeleportTarget(Transform cameraTransform, out Vector3 targetPosition)
        {
            targetPosition = default;

            var floorPlane = new Plane(Vector3.up, m_TeleportSurfaceCollider.bounds.center);
            if (floorPlane.Raycast(new Ray(cameraTransform.position, cameraTransform.forward), out var planeDistance))
            {
                var planePoint = cameraTransform.position + cameraTransform.forward * planeDistance;
                if (IsInsideTeleportSurface(planePoint))
                {
                    targetPosition = planePoint;
                    return true;
                }
            }

            var flattenedForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (flattenedForward.sqrMagnitude < 0.001f)
                flattenedForward = Vector3.forward;

            flattenedForward.Normalize();
            var fallbackPoint = cameraTransform.position + (flattenedForward * m_FallbackTeleportDistance);
            fallbackPoint.y = m_TeleportSurfaceCollider.bounds.max.y;

            if (!IsInsideTeleportSurface(fallbackPoint))
                return false;

            targetPosition = fallbackPoint;
            return true;
        }

        bool IsInsideTeleportSurface(Vector3 point)
        {
            var bounds = m_TeleportSurfaceCollider.bounds;
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                   point.z >= bounds.min.z && point.z <= bounds.max.z;
        }

        void RefreshVisuals()
        {
            if (!m_HasTeleportTarget)
            {
                if (m_MoveStem != null)
                    m_MoveStem.gameObject.SetActive(false);

                if (m_MoveHead != null)
                    m_MoveHead.gameObject.SetActive(false);

                if (m_TurnLeftIndicator != null)
                    m_TurnLeftIndicator.SetActive(false);

                if (m_TurnRightIndicator != null)
                    m_TurnRightIndicator.SetActive(false);

                return;
            }

            var indicatorPosition = m_TeleportTargetPosition;
            indicatorPosition.y += m_GroundOffset;

            transform.position = indicatorPosition;
            transform.rotation = Quaternion.LookRotation(m_TeleportTargetForward, Vector3.up);

            if (m_MoveStem != null)
                m_MoveStem.gameObject.SetActive(true);

            if (m_MoveHead != null)
                m_MoveHead.gameObject.SetActive(true);

            if (m_TurnLeftIndicator != null)
                m_TurnLeftIndicator.SetActive(false);

            if (m_TurnRightIndicator != null)
                m_TurnRightIndicator.SetActive(false);
        }

        void TeleportToTarget()
        {
            var originTransform = m_XROrigin.transform;
            var cameraTransform = m_XROrigin.Camera.transform;

            var translation = m_TeleportTargetPosition - cameraTransform.position;
            translation.y = 0f;
            originTransform.position += translation;

            var currentForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (currentForward.sqrMagnitude < 0.001f)
                currentForward = originTransform.forward;

            currentForward.Normalize();
            var signedAngle = Vector3.SignedAngle(currentForward, m_TeleportTargetForward, Vector3.up);
            originTransform.RotateAround(cameraTransform.position, Vector3.up, signedAngle);
        }

        bool ReadTriggerButton(ref InputDevice device, XRNode node)
        {
            if (!device.isValid)
                device = InputDevices.GetDeviceAtXRNode(node);

            if (device.TryGetFeatureValue(CommonUsages.trigger, out var triggerValue) && triggerValue >= m_TriggerThreshold)
                return true;

            return device.TryGetFeatureValue(CommonUsages.triggerButton, out var isPressed) && isPressed;
        }
    }
}
