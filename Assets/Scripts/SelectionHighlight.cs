using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CSE165.Project1
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class SelectionHighlight : MonoBehaviour
    {
        [SerializeField]
        Color m_HoverTint = new Color(1f, 0.86f, 0.38f, 1f);

        [SerializeField]
        Color m_SelectedTint = new Color(0.42f, 0.82f, 1f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        float m_HoverBlend = 0.25f;

        [SerializeField]
        [Range(0f, 1f)]
        float m_SelectedBlend = 0.65f;

        [SerializeField]
        [Range(0f, 3f)]
        float m_EmissionStrength = 1.1f;

        static readonly int k_ColorId = Shader.PropertyToID("_Color");
        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        readonly Dictionary<Material, Color> m_BaseColors = new Dictionary<Material, Color>();
        readonly List<Material> m_Materials = new List<Material>();

        XRBaseInteractable m_Interactable;
        bool m_IsHovered;
        bool m_IsSelected;

        void Awake()
        {
            m_Interactable = GetComponent<XRBaseInteractable>();
            CacheMaterials();
        }

        void OnEnable()
        {
            if (m_Interactable == null)
                return;

            m_Interactable.hoverEntered.AddListener(OnHoverEntered);
            m_Interactable.hoverExited.AddListener(OnHoverExited);
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
            m_Interactable.selectExited.AddListener(OnSelectExited);

            RefreshVisuals();
        }

        void OnDisable()
        {
            if (m_Interactable != null)
            {
                m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
                m_Interactable.hoverExited.RemoveListener(OnHoverExited);
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
                m_Interactable.selectExited.RemoveListener(OnSelectExited);
            }

            RestoreBaseVisuals();
        }

        void CacheMaterials()
        {
            m_BaseColors.Clear();
            m_Materials.Clear();

            var uniqueMaterials = new HashSet<Material>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.materials)
                {
                    if (material == null || !uniqueMaterials.Add(material))
                        continue;

                    m_Materials.Add(material);
                    m_BaseColors[material] = ReadBaseColor(material);
                }
            }
        }

        void OnHoverEntered(HoverEnterEventArgs _)
        {
            m_IsHovered = true;
            RefreshVisuals();
        }

        void OnHoverExited(HoverExitEventArgs _)
        {
            m_IsHovered = m_Interactable != null && m_Interactable.interactorsHovering.Count > 0;
            RefreshVisuals();
        }

        void OnSelectEntered(SelectEnterEventArgs _)
        {
            m_IsSelected = true;
            RefreshVisuals();
        }

        void OnSelectExited(SelectExitEventArgs _)
        {
            m_IsSelected = m_Interactable != null && m_Interactable.interactorsSelecting.Count > 0;
            RefreshVisuals();
        }

        void RestoreBaseVisuals()
        {
            foreach (var material in m_Materials)
            {
                if (!m_BaseColors.TryGetValue(material, out var baseColor))
                    continue;

                ApplyVisuals(material, baseColor, Color.black);
            }
        }

        void RefreshVisuals()
        {
            foreach (var material in m_Materials)
            {
                if (!m_BaseColors.TryGetValue(material, out var baseColor))
                    continue;

                var tint = baseColor;
                var emission = Color.black;

                if (m_IsSelected)
                {
                    tint = Color.Lerp(baseColor, m_SelectedTint, m_SelectedBlend);
                    emission = m_SelectedTint * m_EmissionStrength;
                }
                else if (m_IsHovered)
                {
                    tint = Color.Lerp(baseColor, m_HoverTint, m_HoverBlend);
                    emission = m_HoverTint * (m_EmissionStrength * 0.25f);
                }

                ApplyVisuals(material, tint, emission);
            }
        }

        static Color ReadBaseColor(Material material)
        {
            if (material.HasProperty(k_BaseColorId))
                return material.GetColor(k_BaseColorId);

            if (material.HasProperty(k_ColorId))
                return material.GetColor(k_ColorId);

            return Color.white;
        }

        static void ApplyVisuals(Material material, Color tint, Color emission)
        {
            if (material.HasProperty(k_BaseColorId))
                material.SetColor(k_BaseColorId, tint);
            else if (material.HasProperty(k_ColorId))
                material.SetColor(k_ColorId, tint);

            if (!material.HasProperty(k_EmissionColorId))
                return;

            if (emission.maxColorComponent > 0.001f)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");

            material.SetColor(k_EmissionColorId, emission);
        }
    }
}
