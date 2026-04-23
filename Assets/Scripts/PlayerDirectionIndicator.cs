using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

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
        float m_ForwardOffset = 0.55f;

        [SerializeField]
        float m_GroundOffset = 0.02f;

        [SerializeField]
        float m_MoveDeadzone = 0.15f;

        [SerializeField]
        float m_TurnDeadzone = 0.55f;

        InputDevice m_LeftController;
        InputDevice m_RightController;

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

            var cameraTransform = m_XROrigin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward).normalized;

            var moveAxis = ReadPrimary2DAxis(ref m_LeftController, XRNode.LeftHand);
            var turnAxis = ReadPrimary2DAxis(ref m_RightController, XRNode.RightHand);

            var moveDirection = forward;
            if (moveAxis.sqrMagnitude > m_MoveDeadzone * m_MoveDeadzone)
            {
                moveDirection = (right * moveAxis.x) + (forward * moveAxis.y);
                if (moveDirection.sqrMagnitude > 0.001f)
                    moveDirection.Normalize();
                else
                    moveDirection = forward;
            }

            var position = cameraTransform.position + moveDirection * m_ForwardOffset;
            position.y = m_XROrigin.transform.position.y + m_GroundOffset;

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);

            if (m_MoveStem != null)
                m_MoveStem.gameObject.SetActive(true);

            if (m_MoveHead != null)
                m_MoveHead.gameObject.SetActive(true);

            if (m_TurnLeftIndicator != null)
                m_TurnLeftIndicator.SetActive(turnAxis.x < -m_TurnDeadzone);

            if (m_TurnRightIndicator != null)
                m_TurnRightIndicator.SetActive(turnAxis.x > m_TurnDeadzone);
        }

        void RefreshControllers()
        {
            if (!m_LeftController.isValid)
                m_LeftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            if (!m_RightController.isValid)
                m_RightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        static Vector2 ReadPrimary2DAxis(ref InputDevice device, XRNode node)
        {
            if (!device.isValid)
                device = InputDevices.GetDeviceAtXRNode(node);

            return device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis) ? axis : Vector2.zero;
        }
    }
}
