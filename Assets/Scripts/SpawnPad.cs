using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CSE165.Project1
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class SpawnPad : MonoBehaviour
    {
        [SerializeField]
        GameObject m_PrefabToSpawn;

        [SerializeField]
        Transform m_SpawnPoint;

        [SerializeField]
        float m_SpawnCooldown = 0.6f;

        [SerializeField]
        Vector3 m_Impulse = new Vector3(0f, 0.15f, 0.2f);

        XRSimpleInteractable m_Interactable;
        float m_NextSpawnTime;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
        }

        void OnEnable()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs _)
        {
            if (m_PrefabToSpawn == null || m_SpawnPoint == null || Time.time < m_NextSpawnTime)
                return;

            var spawned = Instantiate(m_PrefabToSpawn, m_SpawnPoint.position, m_SpawnPoint.rotation);
            if (spawned.TryGetComponent<Rigidbody>(out var rigidbody))
                rigidbody.AddForce(m_SpawnPoint.TransformDirection(m_Impulse), ForceMode.Impulse);

            m_NextSpawnTime = Time.time + m_SpawnCooldown;
        }
    }
}
