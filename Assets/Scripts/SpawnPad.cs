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
        GameObject[] m_PrefabsToSpawn = System.Array.Empty<GameObject>();

        [SerializeField]
        Transform m_SpawnPoint;

        [SerializeField]
        float m_SpawnCooldown = 0.6f;

        [SerializeField]
        Vector3 m_Impulse = new Vector3(0f, 0.15f, 0.2f);

        [SerializeField]
        int m_CurrentIndex;

        [SerializeField]
        bool m_AdvanceAfterSpawn = true;

        [SerializeField]
        bool m_ShowCatalogLabel = true;

        [SerializeField]
        Vector3 m_LabelOffset = new Vector3(0f, 0.35f, 0.05f);

        Transform m_LabelRoot;
        TextMesh m_Label;
        GameObject m_LabelBackplate;
        float m_NextSpawnTime;
        XRSimpleInteractable m_Interactable;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
            EnsureCatalogLabel();
            RefreshCatalogLabel();

            Debug.Log($"{nameof(SpawnPad)} ready on {name}. Catalog size: {GetCatalogCount()}. Spawn point assigned: {m_SpawnPoint != null}.", this);
        }

        void OnValidate()
        {
            if (m_PrefabsToSpawn != null && m_PrefabsToSpawn.Length > 0)
                m_CurrentIndex = Mathf.Clamp(m_CurrentIndex, 0, m_PrefabsToSpawn.Length - 1);
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

        void LateUpdate()
        {
            if (m_LabelRoot == null || Camera.main == null)
                return;

            var toCamera = Camera.main.transform.position - m_LabelRoot.position;
            if (toCamera.sqrMagnitude > 0.001f)
                m_LabelRoot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            var interactorName = args.interactorObject != null ? args.interactorObject.transform.name : "unknown interactor";
            TrySpawn($"XRI select from {interactorName}");
        }

        void TrySpawn(string source)
        {
            var prefabToSpawn = GetCurrentPrefab();
            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"{nameof(SpawnPad)} on {name} cannot spawn: no prefab is assigned.", this);
                return;
            }

            if (m_SpawnPoint == null)
            {
                Debug.LogWarning($"{nameof(SpawnPad)} on {name} cannot spawn {prefabToSpawn.name}: no spawn point is assigned.", this);
                return;
            }

            if (Time.time < m_NextSpawnTime)
                return;

            var spawned = Instantiate(prefabToSpawn, m_SpawnPoint.position, m_SpawnPoint.rotation);
            spawned.name = $"{FormatPrefabName(prefabToSpawn.name)} Spawned";
            if (spawned.TryGetComponent<Rigidbody>(out var rigidbody))
                rigidbody.AddForce(m_SpawnPoint.TransformDirection(m_Impulse), ForceMode.Impulse);

            Debug.Log($"{nameof(SpawnPad)} on {name} spawned {spawned.name} via {source}.", this);
            AdvanceCatalog();
            m_NextSpawnTime = Time.time + m_SpawnCooldown;
        }

        GameObject GetCurrentPrefab()
        {
            if (m_PrefabsToSpawn != null && m_PrefabsToSpawn.Length > 0)
            {
                m_CurrentIndex = Mathf.Clamp(m_CurrentIndex, 0, m_PrefabsToSpawn.Length - 1);
                return m_PrefabsToSpawn[m_CurrentIndex] != null ? m_PrefabsToSpawn[m_CurrentIndex] : m_PrefabToSpawn;
            }

            return m_PrefabToSpawn;
        }

        int GetCatalogCount()
        {
            return m_PrefabsToSpawn != null && m_PrefabsToSpawn.Length > 0 ? m_PrefabsToSpawn.Length : (m_PrefabToSpawn != null ? 1 : 0);
        }

        void AdvanceCatalog()
        {
            if (!m_AdvanceAfterSpawn || m_PrefabsToSpawn == null || m_PrefabsToSpawn.Length <= 1)
            {
                RefreshCatalogLabel();
                return;
            }

            m_CurrentIndex = (m_CurrentIndex + 1) % m_PrefabsToSpawn.Length;
            RefreshCatalogLabel();
        }

        void EnsureCatalogLabel()
        {
            if (!m_ShowCatalogLabel)
                return;

            var labelRootObject = new GameObject("Spawn Catalog Label");
            labelRootObject.transform.SetParent(transform, false);
            labelRootObject.transform.localPosition = m_LabelOffset;
            m_LabelRoot = labelRootObject.transform;

            m_LabelBackplate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            m_LabelBackplate.name = "Spawn Catalog Label Backplate";
            m_LabelBackplate.transform.SetParent(m_LabelRoot, false);
            m_LabelBackplate.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            m_LabelBackplate.transform.localScale = new Vector3(0.95f, 0.48f, 1f);

            if (m_LabelBackplate.TryGetComponent<Collider>(out var backplateCollider))
                Destroy(backplateCollider);

            var backplateShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (backplateShader == null)
                backplateShader = Shader.Find("Standard");

            var backplateRenderer = m_LabelBackplate.GetComponent<MeshRenderer>();
            backplateRenderer.material = new Material(backplateShader)
            {
                color = new Color(0f, 0f, 0f, 0.88f)
            };

            var labelObject = new GameObject("Spawn Catalog Text");
            labelObject.transform.SetParent(m_LabelRoot, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            m_Label = labelObject.AddComponent<TextMesh>();
            m_Label.anchor = TextAnchor.MiddleCenter;
            m_Label.alignment = TextAlignment.Center;
            m_Label.characterSize = 0.075f;
            m_Label.fontSize = 42;
            m_Label.color = new Color(1f, 0.92f, 0.18f, 1f);
        }

        void RefreshCatalogLabel()
        {
            if (m_Label == null)
                return;

            var prefabToSpawn = GetCurrentPrefab();
            var itemName = prefabToSpawn != null ? FormatPrefabName(prefabToSpawn.name) : "None";
            var count = m_PrefabsToSpawn != null && m_PrefabsToSpawn.Length > 0 ? m_PrefabsToSpawn.Length : 1;
            var index = count > 1 ? m_CurrentIndex + 1 : 1;
            m_Label.text = $"Look here\nGrip/trigger\nNext: {itemName}\n{index}/{count}";
        }

        static string FormatPrefabName(string prefabName)
        {
            return prefabName.Replace("Spawnable_1_", string.Empty)
                .Replace("Spawnable_2_", string.Empty)
                .Replace('_', ' ');
        }

    }
}
