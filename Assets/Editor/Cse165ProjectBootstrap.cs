using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSE165.Project1;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace CSE165.Project1.Editor
{
    public static class Cse165ProjectBootstrap
    {
        const string k_ScenePath = "Assets/Scenes/MedicalTentSetup.unity";
        const string k_GeneratedRoot = "Assets/Generated";
        const string k_GeneratedMaterials = k_GeneratedRoot + "/Materials";
        const string k_GeneratedPrefabs = k_GeneratedRoot + "/Prefabs";
        const string k_XrRigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        static readonly Vector3 k_TentFootprint = new Vector3(5.4f, 2.8f, 5.4f);

        [MenuItem("CSE165/Bootstrap Project")]
        public static void BootstrapProject()
        {
            EnsureDirectories();
            ImportAssignmentPackageIfAvailable();
            ConfigurePlayerSettings();
            ConfigureOpenXR();

            var generatedMaterials = CreateMaterials();
            var spawnPrefabs = CreateSpawnableInteractables(generatedMaterials);
            var tentPrefab = FindTentPrefab();

            BuildScene(tentPrefab, spawnPrefabs, generatedMaterials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CSE165 project bootstrap complete.");
        }

        static void EnsureDirectories()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Generated"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Generated/Materials"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Generated/Prefabs"));
        }

        static void ImportAssignmentPackageIfAvailable()
        {
            var packagePath = GetAssignmentPackagePath();
            if (!File.Exists(packagePath))
            {
                Debug.LogWarning($"Assignment package not found at {packagePath}. Using placeholders only.");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Tauhid";
            PlayerSettings.productName = "CSE165Project1";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        static void ConfigureOpenXR()
        {
            var buildTargetGroup = BuildTargetGroup.Android;
            var settingsPerTarget = GetOrCreateBuildTargetSettings();

            if (!settingsPerTarget.HasSettingsForBuildTarget(buildTargetGroup))
                settingsPerTarget.CreateDefaultSettingsForBuildTarget(buildTargetGroup);

            if (!settingsPerTarget.HasManagerSettingsForBuildTarget(buildTargetGroup))
                settingsPerTarget.CreateDefaultManagerSettingsForBuildTarget(buildTargetGroup);

            var generalSettings = settingsPerTarget.SettingsForBuildTarget(buildTargetGroup);
            generalSettings.InitManagerOnStart = true;

            var managerSettings = generalSettings.AssignedSettings;
            if (managerSettings != null && managerSettings.activeLoaders.All(loader => loader == null || loader.GetType().FullName != "UnityEngine.XR.OpenXR.OpenXRLoader"))
                XRPackageMetadataStore.AssignLoader(managerSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", buildTargetGroup);

            var openXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(buildTargetGroup);
            if (openXRSettings == null)
            {
                Debug.LogError("OpenXR settings could not be located for Android.");
                return;
            }

            EnableFeature<MetaQuestFeature>(openXRSettings);
            EnableFeature<OculusTouchControllerProfile>(openXRSettings);
            EnableFeature<MetaQuestTouchProControllerProfile>(openXRSettings);
            EnableFeature<MetaQuestTouchPlusControllerProfile>(openXRSettings);

            EditorUtility.SetDirty(settingsPerTarget);
            EditorUtility.SetDirty(generalSettings);
            if (managerSettings != null)
                EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(openXRSettings);
        }

        static XRGeneralSettingsPerBuildTarget GetOrCreateBuildTargetSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existingSettings) &&
                existingSettings != null)
            {
                return existingSettings;
            }

            var assetGuid = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").FirstOrDefault();
            if (!string.IsNullOrEmpty(assetGuid))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                existingSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(assetPath);
                if (existingSettings != null)
                {
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, existingSettings, true);
                    return existingSettings;
                }
            }

            var createdSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            const string assetPathForSettings = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
            AssetDatabase.CreateAsset(createdSettings, assetPathForSettings);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, createdSettings, true);
            return createdSettings;
        }

        static void EnableFeature<TFeature>(OpenXRSettings openXRSettings) where TFeature : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = openXRSettings.GetFeature<TFeature>();
            if (feature == null)
            {
                Debug.LogWarning($"OpenXR feature {typeof(TFeature).Name} was not found.");
                return;
            }

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        static MaterialSet CreateMaterials()
        {
            return new MaterialSet
            {
                floor = CreateOrUpdateMaterial("Floor", new Color(0.17f, 0.23f, 0.27f)),
                tent = CreateOrUpdateMaterial("Tent", new Color(0.78f, 0.83f, 0.88f)),
                accent = CreateOrUpdateMaterial("Accent", new Color(0.11f, 0.56f, 0.78f)),
                caution = CreateOrUpdateMaterial("Caution", new Color(0.93f, 0.59f, 0.17f)),
                indicator = CreateOrUpdateMaterial("Indicator", new Color(0.17f, 0.88f, 0.77f)),
                board = CreateOrUpdateMaterial("Board", new Color(0.09f, 0.11f, 0.14f)),
                placeholderA = CreateOrUpdateMaterial("PlaceholderA", new Color(0.80f, 0.89f, 0.95f)),
                placeholderB = CreateOrUpdateMaterial("PlaceholderB", new Color(0.96f, 0.82f, 0.65f)),
            };
        }

        static Material CreateOrUpdateMaterial(string shortName, Color color)
        {
            var path = $"{k_GeneratedMaterials}/{shortName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Glossiness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static List<SpawnSource> CreateSpawnableInteractables(MaterialSet materials)
        {
            var orderedAssets = FindSpawnSourceAssets().ToList();
            var selectedAssets = new List<GameObject>();
            var usedCategories = new HashSet<string>();

            foreach (var asset in orderedAssets)
            {
                var category = GetSpawnCategory(asset.name);
                if (!usedCategories.Add(category))
                    continue;

                selectedAssets.Add(asset);
                if (selectedAssets.Count == 2)
                    break;
            }

            if (selectedAssets.Count < 2)
            {
                foreach (var asset in orderedAssets)
                {
                    if (selectedAssets.Contains(asset))
                        continue;

                    selectedAssets.Add(asset);
                    if (selectedAssets.Count == 2)
                        break;
                }
            }

            var spawnSources = selectedAssets
                .Select((prefab, index) => CreateInteractableWrapper(prefab, index))
                .Where(result => result.prefab != null)
                .ToList();

            if (spawnSources.Count >= 2)
                return spawnSources;

            if (spawnSources.Count == 0)
                spawnSources.Add(CreatePlaceholderInteractable("Supply Crate", PrimitiveType.Cube, new Vector3(0.45f, 0.28f, 0.28f), materials.placeholderA, 0));

            if (spawnSources.Count == 1)
                spawnSources.Add(CreatePlaceholderInteractable("Oxygen Canister", PrimitiveType.Cylinder, new Vector3(0.2f, 0.42f, 0.2f), materials.placeholderB, 1));

            return spawnSources;
        }

        static IEnumerable<GameObject> FindSpawnSourceAssets()
        {
            var assetGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
            var results = new List<GameObject>();

            foreach (var guid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsProjectContentPath(path))
                {
                    continue;
                }

                var lowerName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (lowerName.Contains("tent") ||
                    lowerName.Contains("xr") ||
                    lowerName.Contains("anchor") ||
                    lowerName.Contains("pointcloud") ||
                    lowerName.Contains("teleport") ||
                    lowerName.Contains("controller") ||
                    lowerName.Contains("interactor"))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    continue;

                results.Add(prefab);
            }

            return results
                .OrderBy(prefab => GetSpawnPriority(prefab.name))
                .ThenBy(prefab => prefab.name)
                .ToList();
        }

        static SpawnSource CreateInteractableWrapper(GameObject sourcePrefab, int index)
        {
            var safeName = SanitizeFileName(sourcePrefab.name);
            var assetPath = $"{k_GeneratedPrefabs}/Spawnable_{index + 1}_{safeName}.prefab";

            var root = new GameObject($"{sourcePrefab.name} Interactable");
            var visual = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (visual == null)
            {
                Object.DestroyImmediate(root);
                return default;
            }

            visual.transform.SetParent(root.transform, false);
            NormalizeVisualTransform(visual);
            ConfigureInteractableRoot(root);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);

            return new SpawnSource
            {
                displayName = sourcePrefab.name,
                prefab = saved,
            };
        }

        static SpawnSource CreatePlaceholderInteractable(string displayName, PrimitiveType primitiveType, Vector3 scale, Material material, int index)
        {
            var assetPath = $"{k_GeneratedPrefabs}/Spawnable_{index + 1}_{SanitizeFileName(displayName)}.prefab";
            var root = new GameObject(displayName);

            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = scale;

            var visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer != null)
                visualRenderer.sharedMaterial = material;

            var visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Object.DestroyImmediate(visualCollider);

            ConfigureInteractableRoot(root);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);

            return new SpawnSource
            {
                displayName = displayName,
                prefab = saved,
            };
        }

        static void NormalizeVisualTransform(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = CalculateBounds(renderers);
            var maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDimension > 0.001f)
            {
                var scaleFactor = 0.45f / maxDimension;
                visual.transform.localScale *= scaleFactor;
            }

            bounds = CalculateBounds(visual.GetComponentsInChildren<Renderer>(true));
            var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            visual.transform.position += offset;
        }

        static void ConfigureInteractableRoot(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = CalculateBounds(renderers);

            var collider = root.GetComponent<BoxCollider>();
            if (collider == null)
                collider = root.AddComponent<BoxCollider>();

            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;

            var rigidbody = root.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = root.AddComponent<Rigidbody>();

            rigidbody.mass = Mathf.Clamp(bounds.size.x * bounds.size.y * bounds.size.z * 15f, 0.75f, 6f);
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grabInteractable = root.GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
                grabInteractable = root.AddComponent<XRGrabInteractable>();

            var transformer = root.GetComponent<XRGeneralGrabTransformer>();
            if (transformer == null)
                transformer = root.AddComponent<XRGeneralGrabTransformer>();

            transformer.allowOneHandedScaling = false;
            transformer.allowTwoHandedScaling = true;

            grabInteractable.useDynamicAttach = true;
            grabInteractable.matchAttachPosition = true;
            grabInteractable.matchAttachRotation = true;
            grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grabInteractable.selectMode = InteractableSelectMode.Multiple;
            grabInteractable.attachEaseInTime = 0.05f;
            grabInteractable.addDefaultGrabTransformers = false;
            grabInteractable.startingSingleGrabTransformers.Clear();
            grabInteractable.startingMultipleGrabTransformers.Clear();
            grabInteractable.startingSingleGrabTransformers.Add(transformer);
            grabInteractable.startingMultipleGrabTransformers.Add(transformer);

            if (root.GetComponent<SelectionHighlight>() == null)
                root.AddComponent<SelectionHighlight>();
        }

        static GameObject FindTentPrefab()
        {
            var tentGuids = AssetDatabase.FindAssets("tent t:GameObject", new[] { "Assets" });
            foreach (var guid in tentGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsProjectContentPath(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        static void BuildScene(GameObject tentPrefab, IReadOnlyList<SpawnSource> spawnSources, MaterialSet materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.39f, 0.43f);

            CreateDirectionalLight();
            CreateFloor(materials.floor);

            if (tentPrefab != null)
                InstantiateTent(tentPrefab);
            else
                CreatePlaceholderTent(materials.tent);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_XrRigPrefabPath);
            if (rigPrefab == null)
                throw new FileNotFoundException($"XR rig prefab missing at {k_XrRigPrefabPath}");

            var rig = PrefabUtility.InstantiatePrefab(rigPrefab) as GameObject;
            if (rig == null)
                throw new FileNotFoundException("XR rig prefab could not be instantiated.");

            rig.name = "XR Rig";
            rig.transform.position = Vector3.zero;

            var xrOrigin = rig.GetComponent<XROrigin>();
            if (xrOrigin == null)
                throw new MissingComponentException("The XR rig prefab is missing XROrigin.");

            ConfigureRigInteractions(rig.transform);
            AddDirectSelectionIndicators(rig.transform, materials.indicator);
            CreateMovementIndicator(xrOrigin, materials.indicator);

            CreateSpawnPad(spawnSources[0], new Vector3(-0.65f, 0f, 1.2f), materials.accent, materials.board);
            CreateSpawnPad(spawnSources[1], new Vector3(0.65f, 0f, 1.2f), materials.caution, materials.board);

            CreateInstructionBoard(
                "Controls",
                "Grip: direct grab or tap a nearby pad\nTrigger: distance select and remote grab\nTwo hands on one item: scale\nLeft stick: move | Right stick: turn / teleport",
                new Vector3(0f, 1.45f, 1.95f),
                materials.board);

            CreateInstructionBoard(
                "Selection + Travel",
                "Hand halos = direct selection\nCurved ray = distance selection\nFloor arrow + chevrons = move + turn\nTeleport ray = travel target",
                new Vector3(2.1f, 1.45f, 0f),
                materials.board);

            EditorSceneManager.SaveScene(scene, k_ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(k_ScenePath, true)
            };
        }

        static void CreateDirectionalLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.92f);
        }

        static void CreateFloor(Material floorMaterial)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Tent Floor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(k_TentFootprint.x, 0.1f, k_TentFootprint.z);

            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = floorMaterial;

            floor.AddComponent<TeleportationArea>();
        }

        static void InstantiateTent(GameObject tentPrefab)
        {
            var tent = PrefabUtility.InstantiatePrefab(tentPrefab) as GameObject;
            if (tent == null)
                return;

            tent.name = "Tent";
            FitToFootprint(tent, k_TentFootprint.x, 0f);
        }

        static void CreatePlaceholderTent(Material tentMaterial)
        {
            var tentRoot = new GameObject("Tent");
            CreateTentPiece(tentRoot.transform, "Back Wall", new Vector3(0f, 1.15f, -2.55f), new Vector3(5.4f, 2.3f, 0.08f), tentMaterial);
            CreateTentPiece(tentRoot.transform, "Left Wall", new Vector3(-2.55f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 5.4f), tentMaterial);
            CreateTentPiece(tentRoot.transform, "Right Wall", new Vector3(2.55f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 5.4f), tentMaterial);
            CreateTentPiece(tentRoot.transform, "Canopy", new Vector3(0f, 2.35f, 0f), new Vector3(5.4f, 0.08f, 5.4f), tentMaterial);
        }

        static void CreateTentPiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;

            var renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        static void FitToFootprint(GameObject root, float targetFootprint, float yOffset)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = CalculateBounds(renderers);
            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint > 0.001f)
            {
                var scaleFactor = targetFootprint / footprint;
                root.transform.localScale *= scaleFactor;
            }

            bounds = CalculateBounds(root.GetComponentsInChildren<Renderer>(true));
            root.transform.position += new Vector3(-bounds.center.x, yOffset - bounds.min.y, -bounds.center.z);
        }

        static void AddDirectSelectionIndicators(Transform rigRoot, Material indicatorMaterial)
        {
            foreach (var interactor in rigRoot.GetComponentsInChildren<NearFarInteractor>(true))
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Direct Grab Indicator";
                sphere.transform.SetParent(interactor.transform, false);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * 0.045f;

                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = indicatorMaterial;

                var collider = sphere.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }
        }

        static void ConfigureRigInteractions(Transform rigRoot)
        {
            ConfigureNearFarInteractor(rigRoot, "Left Controller");
            ConfigureNearFarInteractor(rigRoot, "Right Controller");
            DisableOptionalLocomotion(rigRoot);
        }

        static void ConfigureNearFarInteractor(Transform rigRoot, string controllerName)
        {
            var controller = FindChildByName(rigRoot, controllerName);
            if (controller == null)
                return;

            var nearFarRoot = FindChildByNameContains(controller, "NearFarInteractor");
            if (nearFarRoot == null)
                return;

            foreach (var behaviour in nearFarRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                AssignTriggerSelect(behaviour);
                DisableManipulationInput(behaviour);
            }
        }

        static void DisableOptionalLocomotion(Transform rigRoot)
        {
            var jump = FindChildByName(rigRoot, "Jump");
            if (jump != null)
                jump.gameObject.SetActive(false);
        }

        static void AssignTriggerSelect(MonoBehaviour behaviour)
        {
            var serializedObject = new SerializedObject(behaviour);
            var selectPerformed = serializedObject.FindProperty("m_SelectInput.m_InputActionReferencePerformed");
            var selectValue = serializedObject.FindProperty("m_SelectInput.m_InputActionReferenceValue");
            var activatePerformed = serializedObject.FindProperty("m_ActivateInput.m_InputActionReferencePerformed");
            var activateValue = serializedObject.FindProperty("m_ActivateInput.m_InputActionReferenceValue");

            if (selectPerformed == null || selectValue == null || activatePerformed == null || activateValue == null)
                return;

            if (activatePerformed.objectReferenceValue == null && activateValue.objectReferenceValue == null)
                return;

            selectPerformed.objectReferenceValue = activatePerformed.objectReferenceValue;
            selectValue.objectReferenceValue = activateValue.objectReferenceValue;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static void DisableManipulationInput(MonoBehaviour behaviour)
        {
            var serializedObject = new SerializedObject(behaviour);
            var useManipulationInput = serializedObject.FindProperty("m_UseManipulationInput");
            var manipulationInputReference = serializedObject.FindProperty("m_ManipulationInput.m_InputActionReference");

            var changed = false;

            if (useManipulationInput != null)
            {
                useManipulationInput.boolValue = false;
                changed = true;
            }

            if (manipulationInputReference != null)
            {
                manipulationInputReference.objectReferenceValue = null;
                changed = true;
            }

            if (changed)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateMovementIndicator(XROrigin xrOrigin, Material indicatorMaterial)
        {
            var arrowRoot = new GameObject("Move Direction Indicator");
            var indicator = arrowRoot.AddComponent<PlayerDirectionIndicator>();
            indicator.SetOrigin(xrOrigin);

            var moveStem = CreateArrowPiece(arrowRoot.transform, "Move Stem", new Vector3(0f, 0f, 0.14f), new Vector3(0.12f, 0.015f, 0.36f), indicatorMaterial);
            var moveHead = CreateArrowPiece(arrowRoot.transform, "Move Head", new Vector3(0f, 0f, 0.34f), new Vector3(0.3f, 0.015f, 0.18f), indicatorMaterial);
            var turnLeft = CreateTurnChevron(arrowRoot.transform, "Turn Left Indicator", new Vector3(-0.24f, 0f, 0.04f), -1f, indicatorMaterial);
            var turnRight = CreateTurnChevron(arrowRoot.transform, "Turn Right Indicator", new Vector3(0.24f, 0f, 0.04f), 1f, indicatorMaterial);

            indicator.SetVisuals(moveStem, moveHead, turnLeft, turnRight);
            turnLeft.SetActive(false);
            turnRight.SetActive(false);
        }

        static Transform CreateArrowPiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;

            var renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            var collider = piece.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            return piece.transform;
        }

        static GameObject CreateTurnChevron(Transform parent, string name, Vector3 localPosition, float direction, Material material)
        {
            var chevronRoot = new GameObject(name);
            chevronRoot.transform.SetParent(parent, false);
            chevronRoot.transform.localPosition = localPosition;

            CreateArrowPiece(chevronRoot.transform, "Bar A", new Vector3(0f, 0f, 0.08f), new Vector3(0.065f, 0.012f, 0.14f), material)
                .localRotation = Quaternion.Euler(0f, direction * 32f, 0f);
            CreateArrowPiece(chevronRoot.transform, "Bar B", new Vector3(0f, 0f, -0.01f), new Vector3(0.065f, 0.012f, 0.14f), material)
                .localRotation = Quaternion.Euler(0f, direction * -32f, 0f);

            return chevronRoot;
        }

        static void CreateSpawnPad(SpawnSource source, Vector3 position, Material accentMaterial, Material boardMaterial)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = $"{source.displayName} Pad";
            pad.transform.position = position;
            pad.transform.localScale = new Vector3(0.22f, 0.05f, 0.22f);

            var renderer = pad.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = accentMaterial;

            pad.AddComponent<XRSimpleInteractable>();
            pad.AddComponent<SelectionHighlight>();

            var spawnPad = pad.AddComponent<SpawnPad>();
            var spawnPoint = new GameObject("Spawn Point").transform;
            spawnPoint.position = position + new Vector3(0f, 0.28f, 0.42f);
            spawnPoint.rotation = Quaternion.identity;

            SerializedObject serializedSpawnPad = new SerializedObject(spawnPad);
            serializedSpawnPad.FindProperty("m_PrefabToSpawn").objectReferenceValue = source.prefab;
            serializedSpawnPad.FindProperty("m_SpawnPoint").objectReferenceValue = spawnPoint;
            serializedSpawnPad.ApplyModifiedPropertiesWithoutUndo();

            CreateInstructionBoard(source.displayName, "Grip nearby or trigger from a distance", position + new Vector3(0f, 1.05f, 0f), boardMaterial, 0.48f, 16);
        }

        static void CreateInstructionBoard(string title, string content, Vector3 position, Material boardMaterial, float boardWidth = 0.9f, int fontSize = 24)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = $"{title} Board";
            board.transform.position = position;
            board.transform.rotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
            board.transform.localScale = new Vector3(boardWidth, 0.04f, 0.6f);

            var renderer = board.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = boardMaterial;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(board.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, 0.51f);
            textObject.transform.localRotation = Quaternion.identity;

            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = $"{title}\n{content}";
            textMesh.fontSize = fontSize;
            textMesh.characterSize = 0.03f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
        }

        static Bounds CalculateBounds(IEnumerable<Renderer> renderers)
        {
            using (var enumerator = renderers.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return new Bounds(Vector3.zero, Vector3.one * 0.25f);

                var bounds = enumerator.Current.bounds;
                while (enumerator.MoveNext())
                    bounds.Encapsulate(enumerator.Current.bounds);

                return bounds;
            }
        }

        static string SanitizeFileName(string value)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var sanitized = new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray());
            return sanitized.Replace(' ', '_');
        }

        static string GetAssignmentPackagePath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, "HW1.unitypackage");
        }

        static int GetSpawnPriority(string name)
        {
            var category = GetSpawnCategory(name);
            if (category == "bed")
                return 0;
            if (category == "chair")
                return 1;
            if (category == "cabinet")
                return 2;
            if (category == "screen")
                return 3;
            if (category == "package")
                return 4;
            if (category == "equipment")
                return 5;

            return 10;
        }

        static string GetSpawnCategory(string name)
        {
            var lowerName = name.ToLowerInvariant();
            if (lowerName.Contains("bed"))
                return "bed";
            if (lowerName.Contains("chair"))
                return "chair";
            if (lowerName.Contains("cabinet"))
                return "cabinet";
            if (lowerName.Contains("screen"))
                return "screen";
            if (lowerName.Contains("package"))
                return "package";
            if (lowerName.Contains("equipment"))
                return "equipment";

            return "other";
        }

        static bool IsProjectContentPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return !path.StartsWith("Assets/Samples/") &&
                   !path.StartsWith("Assets/Generated/") &&
                   !path.StartsWith("Assets/XR/") &&
                   !path.StartsWith("Assets/XRI/") &&
                   !path.StartsWith("Assets/CompositionLayers/") &&
                   !path.StartsWith("Assets/Editor/") &&
                   !path.StartsWith("Assets/Scenes/") &&
                   !path.StartsWith("Assets/Scripts/");
        }

        static Transform FindChildByName(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        }

        static Transform FindChildByNameContains(Transform root, string partialName)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name.Contains(partialName));
        }

        struct MaterialSet
        {
            public Material floor;
            public Material tent;
            public Material accent;
            public Material caution;
            public Material indicator;
            public Material board;
            public Material placeholderA;
            public Material placeholderB;
        }

        struct SpawnSource
        {
            public string displayName;
            public GameObject prefab;
        }
    }
}
