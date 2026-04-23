using Unity.XR.CoreUtils;
using UnityEngine;

namespace CSE165.Project1
{
    [DisallowMultipleComponent]
    public sealed class PlayerDirectionIndicator : MonoBehaviour
    {
        [SerializeField]
        XROrigin m_XROrigin;

        [SerializeField]
        float m_ForwardOffset = 0.55f;

        [SerializeField]
        float m_GroundOffset = 0.02f;

        public void SetOrigin(XROrigin xrOrigin)
        {
            m_XROrigin = xrOrigin;
        }

        void LateUpdate()
        {
            if (m_XROrigin == null || m_XROrigin.Camera == null)
                return;

            var cameraTransform = m_XROrigin.Camera.transform;
            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            forward.Normalize();

            var position = cameraTransform.position + forward * m_ForwardOffset;
            position.y = m_XROrigin.transform.position.y + m_GroundOffset;

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
