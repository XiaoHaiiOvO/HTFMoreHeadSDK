using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HTFMoreHead.Editor
{
    internal sealed class HTFMoreHeadPackageBuilder : EditorWindow
    {
        private const string GeneratedRoot = "Assets/MoreHead/Generated";
        private const string DescriptorPath = GeneratedRoot + "/htfmorehead.json";
        private const string DefaultOutputFolder = "Build/HTFMoreHead";
        private const string ExpectedHeadAnchorName = "HTFMoreHead_HeadAnchor";
        private const string ExpectedHeadAnchorPath = "Armature/Body/Head/HTFMoreHead_HeadAnchor";

        private CosmeticCategory _category = CosmeticCategory.Hat;
        private Transform _anchor;
        private GameObject _cosmeticObject;
        private string _displayName = "My Head Item";
        private string _authorName = string.Empty;
        private string _outputFolder = DefaultOutputFolder;
        private bool _showAdvancedIds;
        private string _autoIdentitySeed = string.Empty;
        private string _packId = string.Empty;
        private string _packVersion = "1.0.0";
        private string _cosmeticId = string.Empty;
        private string _lastBuildReport = string.Empty;

        [MenuItem("Tools/HTF MoreHead/Open .htfhhh Builder")]
        private static void Open()
        {
            GetWindow<HTFMoreHeadPackageBuilder>("HTF MoreHead Builder");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HTF MoreHead SDK", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "先选择分类，再在参考角色对应挂点上校对装扮。帽子使用 Head Anchor，装饰使用 Accessory，衣服使用 Outfit。显示名和作者名支持任意语言；内部 ID 自动生成。",
                MessageType.Info);

            CosmeticCategory previousCategory = _category;
            _category = (CosmeticCategory)EditorGUILayout.EnumPopup("Category", _category);
            if (previousCategory != _category)
            {
                _anchor = null;
                _autoIdentitySeed = string.Empty;
            }

            _anchor = (Transform)EditorGUILayout.ObjectField("Category Anchor", _anchor, typeof(Transform), true);
            bool hasValidAnchor = IsExpectedAnchor(_anchor, _category);
            if (_anchor != null && !hasValidAnchor)
            {
                EditorGUILayout.HelpBox(
                    "当前选中的是错误挂点：" + GetHierarchyPath(_anchor) +
                    "\n需要：" + GetExpectedAnchorDescription(_category),
                    MessageType.Error);
            }

            if (!hasValidAnchor && GUILayout.Button("Auto Find Correct Category Anchor"))
            {
                _anchor = FindExpectedAnchor(_anchor != null ? _anchor.root : null, _category);
                hasValidAnchor = IsExpectedAnchor(_anchor, _category);
                if (!hasValidAnchor)
                {
                    Debug.LogError("[HTFMoreHead SDK] Could not find " + GetExpectedAnchorDescription(_category) + " in the active authoring scene.");
                }
            }

            if (hasValidAnchor)
            {
                EditorGUILayout.HelpBox("正确挂点：" + GetHierarchyPath(_anchor), MessageType.Info);
            }

            _cosmeticObject = (GameObject)EditorGUILayout.ObjectField("Cosmetic Object", _cosmeticObject, typeof(GameObject), true);
            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            _authorName = EditorGUILayout.TextField("Author Name", _authorName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

            EnsureAutomaticIds(false);
            _showAdvancedIds = EditorGUILayout.Foldout(
                _showAdvancedIds,
                "Advanced Internal IDs (optional)",
                true);
            if (_showAdvancedIds)
            {
                EditorGUI.indentLevel++;
                _packId = EditorGUILayout.TextField("Pack ID", _packId);
                _cosmeticId = EditorGUILayout.TextField("Cosmetic ID", _cosmeticId);
                _packVersion = EditorGUILayout.TextField("Pack Version", _packVersion);
                if (GUILayout.Button("Regenerate IDs From Selected Object"))
                {
                    EnsureAutomaticIds(true);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(
                       !hasValidAnchor ||
                       _cosmeticObject == null ||
                       string.IsNullOrWhiteSpace(_displayName) ||
                       string.IsNullOrWhiteSpace(_authorName)))
            {
                if (GUILayout.Button("Validate and Build .htfhhh", GUILayout.Height(34f)))
                {
                    Build();
                }
            }

            if (!string.IsNullOrEmpty(_lastBuildReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_lastBuildReport, MessageType.Info);
            }
        }

        private void Build()
        {
            if (string.IsNullOrWhiteSpace(_displayName) || string.IsNullOrWhiteSpace(_authorName) ||
                _displayName.Length > 128 || _authorName.Length > 128)
            {
                Debug.LogError("[HTFMoreHead SDK] Display Name and Author Name are required and must be 128 characters or fewer. Unicode is supported.");
                return;
            }

            if (_anchor == null || _cosmeticObject == null || EditorUtility.IsPersistent(_cosmeticObject))
            {
                Debug.LogError("[HTFMoreHead SDK] Select the category anchor and a cosmetic GameObject from the active scene.");
                return;
            }

            if (!_cosmeticObject.scene.IsValid() || string.IsNullOrWhiteSpace(_cosmeticObject.scene.path))
            {
                Debug.LogError(
                    "[HTFMoreHead SDK] Save the authoring scene before building. " +
                    "A saved scene is required for deterministic internal IDs.");
                return;
            }

            EnsureAutomaticIds(false);
            if (!IsStableId(_packId) || !IsStableId(_cosmeticId))
            {
                Debug.LogError("[HTFMoreHead SDK] Pack ID and Cosmetic ID must use lowercase ASCII: a-z, 0-9, '.', '_' or '-'.");
                return;
            }

            if (!ValidateRigidCosmetic(_anchor, _cosmeticObject, out BuildMetrics sourceMetrics))
            {
                return;
            }

            if (!IsExpectedAnchor(_anchor, _category))
            {
                Transform resolved = FindExpectedAnchor(_anchor.root, _category);
                if (resolved == null)
                {
                    Debug.LogError("[HTFMoreHead SDK] Category Anchor must be " + GetExpectedAnchorDescription(_category) + ". Current: " + GetHierarchyPath(_anchor));
                    return;
                }

                Debug.LogWarning("[HTFMoreHead SDK] Replaced incorrect Category Anchor '" + GetHierarchyPath(_anchor) +
                                 "' with '" + GetHierarchyPath(resolved) + "'.");
                _anchor = resolved;
            }

            EnsureFolder("Assets", "MoreHead");
            EnsureFolder("Assets/MoreHead", "Generated");
            string safeId = SanitizeFileName(_cosmeticId);
            string prefabPath = GeneratedRoot + "/" + safeId + ".prefab";

            GameObject packageRoot = new GameObject("HTFMoreHead_" + safeId);
            try
            {
                GameObject cosmeticClone = Instantiate(_cosmeticObject);
                cosmeticClone.name = _cosmeticObject.name;
                cosmeticClone.transform.SetParent(packageRoot.transform, false);
                ApplyRelativeTransform(_anchor, _cosmeticObject.transform, cosmeticClone.transform);
                sourceMetrics.StrippedComponents = StripUnsafeComponents(cosmeticClone);

                packageRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                packageRoot.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(packageRoot, prefabPath);
            }
            finally
            {
                DestroyImmediate(packageRoot);
            }

            EmbeddedManifest manifest = new EmbeddedManifest
            {
                schemaVersion = 1,
                packId = _packId,
                packVersion = _packVersion,
                authorName = _authorName.Trim(),
                bundleFile = "embedded",
                cosmetics = new[]
                {
                    new EmbeddedCosmetic
                    {
                        id = _cosmeticId,
                        displayName = _displayName.Trim(),
                        slot = GetSlotName(_category),
                        anchor = GetAnchorName(_category),
                        prefab = Path.GetFileNameWithoutExtension(prefabPath),
                        localPosition = new Float3(0f, 0f, 0f),
                        localEulerAngles = new Float3(0f, 0f, 0f),
                        localScale = new Float3(1f, 1f, 1f)
                    }
                }
            };

            File.WriteAllText(DescriptorPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(DescriptorPath, ImportAssetOptions.ForceSynchronousImport);

            string absoluteOutput = Path.GetFullPath(_outputFolder);
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedOutput = absoluteOutput
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedOutput, assetsRoot, StringComparison.OrdinalIgnoreCase) ||
                normalizedOutput.StartsWith(
                    assetsRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError(
                    "[HTFMoreHead SDK] Output Folder must be outside Assets. " +
                    "Unity creates .meta files for outputs inside Assets, which violates the single-file package format.");
                return;
            }

            Directory.CreateDirectory(absoluteOutput);
            string outputBaseName = SanitizeFileName(_displayName.Trim()).TrimEnd('.', ' ');
            if (string.IsNullOrWhiteSpace(outputBaseName))
            {
                Debug.LogError("[HTFMoreHead SDK] Display Name cannot be converted to a valid Windows file name.");
                return;
            }

            string fileName = outputBaseName + ".htfhhh";
            string finalPath = Path.Combine(absoluteOutput, fileName);
            string[] existingFiles = Directory.GetFiles(absoluteOutput, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < existingFiles.Length; i++)
            {
                if (!string.Equals(
                        Path.GetExtension(existingFiles[i]),
                        ".htfhhh",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError(
                        "[HTFMoreHead SDK] Output Folder contains a non-.htfhhh file. " +
                        "Choose a clean content-package folder: " + existingFiles[i]);
                    return;
                }
            }

            string stagingOutput = Path.Combine(
                Path.GetTempPath(),
                "HTFMoreHeadBuilder",
                Guid.NewGuid().ToString("N"));
            string stagingBundleName = BuildInternalBundleName(_packId, _cosmeticId);
            Directory.CreateDirectory(stagingOutput);
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = stagingBundleName,
                assetNames = new[] { prefabPath, DescriptorPath }
            };
            try
            {
                AssetBundleManifest result = BuildPipeline.BuildAssetBundles(
                    stagingOutput,
                    new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                    BuildTarget.StandaloneWindows64);
                string stagedBundle = Path.Combine(stagingOutput, stagingBundleName);
                if (result == null || !File.Exists(stagedBundle))
                {
                    Debug.LogError("[HTFMoreHead SDK] .htfhhh build failed.");
                    return;
                }

                File.Copy(stagedBundle, finalPath, true);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingOutput))
                    {
                        Directory.Delete(stagingOutput, true);
                    }
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning("[HTFMoreHead SDK] Temporary build cleanup failed: " + cleanupException.Message);
                }
            }

            AssetDatabase.Refresh();
            long bundleBytes = new FileInfo(finalPath).Length;
            _lastBuildReport = BuildReportText(
                sourceMetrics,
                bundleBytes,
                finalPath,
                stagingBundleName);
            Debug.Log("[HTFMoreHead SDK] BUILD REPORT\n" + _lastBuildReport);
            EditorUtility.RevealInFinder(finalPath);
        }

        private static string BuildInternalBundleName(string packId, string cosmeticId)
        {
            string stableIdentity = (packId ?? string.Empty) + ":" + (cosmeticId ?? string.Empty);
            return "htfmorehead_" + Hash128.Compute(stableIdentity).ToString().ToLowerInvariant();
        }

        private static bool ValidateRigidCosmetic(
            Transform anchor,
            GameObject cosmeticObject,
            out BuildMetrics metrics)
        {
            metrics = new BuildMetrics();
            if (cosmeticObject.transform == anchor || anchor.IsChildOf(cosmeticObject.transform))
            {
                Debug.LogError(
                    "[HTFMoreHead SDK] Cosmetic Object cannot be the category anchor or an ancestor of it. " +
                    "Select only the custom cosmetic model, not the reference player.");
                return false;
            }

            if (cosmeticObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
            {
                Debug.LogError(
                    "[HTFMoreHead SDK] Rigid cosmetic packages do not support SkinnedMeshRenderer. " +
                    "Use MeshFilter + MeshRenderer for this stage.");
                return false;
            }

            if (cosmeticObject.GetComponentsInChildren<Collider>(true).Length > 0 ||
                cosmeticObject.GetComponentsInChildren<Rigidbody>(true).Length > 0 ||
                cosmeticObject.GetComponentsInChildren<Joint>(true).Length > 0 ||
                cosmeticObject.GetComponentsInChildren<Camera>(true).Length > 0 ||
                cosmeticObject.GetComponentsInChildren<AudioListener>(true).Length > 0 ||
                cosmeticObject.GetComponentsInChildren<MonoBehaviour>(true).Length > 0)
            {
                Debug.LogError(
                    "[HTFMoreHead SDK] Cosmetic contains scripts, physics, Camera, AudioListener, or other " +
                    "unsafe components. Remove them before building.");
                return false;
            }

            MeshFilter[] filters = cosmeticObject.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers = cosmeticObject.GetComponentsInChildren<MeshRenderer>(true);
            if (filters.Length == 0 || renderers.Length == 0)
            {
                Debug.LogError("[HTFMoreHead SDK] Cosmetic must contain MeshFilter + MeshRenderer.");
                return false;
            }

            HashSet<Mesh> meshes = new HashSet<Mesh>();
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    Debug.LogError("[HTFMoreHead SDK] MeshFilter '" + filters[i].name + "' has no Mesh.");
                    return false;
                }

                if (!meshes.Add(mesh))
                {
                    continue;
                }

                metrics.Meshes++;
                metrics.Vertices += mesh.vertexCount;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    metrics.Triangles += (long)mesh.GetIndexCount(subMesh) / 3L;
                }
            }

            HashSet<Material> materials = new HashSet<Material>();
            HashSet<Texture> textures = new HashSet<Texture>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] sharedMaterials = renderers[i].sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    Debug.LogError("[HTFMoreHead SDK] MeshRenderer '" + renderers[i].name + "' has no Material.");
                    return false;
                }

                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material material = sharedMaterials[materialIndex];
                    if (material == null || material.shader == null)
                    {
                        Debug.LogError("[HTFMoreHead SDK] Renderer '" + renderers[i].name + "' has a missing Material or Shader.");
                        return false;
                    }

                    if (!materials.Add(material))
                    {
                        continue;
                    }

                    string[] textureNames = material.GetTexturePropertyNames();
                    for (int textureIndex = 0; textureIndex < textureNames.Length; textureIndex++)
                    {
                        Texture texture = material.GetTexture(textureNames[textureIndex]);
                        if (texture == null || !textures.Add(texture))
                        {
                            continue;
                        }

                        metrics.Textures++;
                        metrics.MaxTextureSize = Math.Max(
                            metrics.MaxTextureSize,
                            Math.Max(texture.width, texture.height));
                    }
                }
            }

            metrics.Renderers = renderers.Length;
            metrics.Materials = materials.Count;
            if (metrics.Triangles > 50000)
            {
                Debug.LogWarning(
                    "[HTFMoreHead SDK] High triangle count: " + metrics.Triangles +
                    ". Recommended <= 20,000; high-risk > 50,000.");
            }

            if (metrics.Materials > 4)
            {
                Debug.LogWarning(
                    "[HTFMoreHead SDK] High material count: " + metrics.Materials +
                    ". Recommended <= 2; high-risk > 4.");
            }

            if (metrics.MaxTextureSize > 2048)
            {
                Debug.LogWarning(
                    "[HTFMoreHead SDK] Large texture detected: " + metrics.MaxTextureSize +
                    ". Recommended <= 1024; high-risk > 2048.");
            }

            return true;
        }

        private static string BuildReportText(
            BuildMetrics metrics,
            long bundleBytes,
            string finalPath,
            string internalBundleName)
        {
            string risk = metrics.Triangles > 50000 || metrics.Materials > 4 ||
                          metrics.MaxTextureSize > 2048 || bundleBytes > 20L * 1024L * 1024L
                ? "HIGH - optimize before public release"
                : "PASS";
            return "Output: " + finalPath + "\n" +
                   "Internal bundle name: " + internalBundleName + "\n" +
                   "Meshes: " + metrics.Meshes + ", Renderers: " + metrics.Renderers +
                   ", Vertices: " + metrics.Vertices + ", Triangles: " + metrics.Triangles + "\n" +
                   "Materials: " + metrics.Materials + ", Textures: " + metrics.Textures +
                   ", Max texture: " + metrics.MaxTextureSize + "\n" +
                   "Non-render components stripped from package clone: " + metrics.StrippedComponents + "\n" +
                   "Bundle: " + (bundleBytes / 1024f / 1024f).ToString("F2") + " MiB\n" +
                   "Risk: " + risk + "\n" +
                   "Final output contains exactly one .htfhhh file from this build.";
        }

        private static void ApplyRelativeTransform(Transform anchor, Transform source, Transform destination)
        {
            Matrix4x4 relative = anchor.worldToLocalMatrix * source.localToWorldMatrix;
            destination.localPosition = relative.GetColumn(3);
            destination.localRotation = relative.rotation;
            destination.localScale = new Vector3(
                relative.GetColumn(0).magnitude,
                relative.GetColumn(1).magnitude,
                relative.GetColumn(2).magnitude);
        }

        private void EnsureAutomaticIds(bool force)
        {
            if (_cosmeticObject == null)
            {
                return;
            }

            string seed = GetIdentitySeed(_cosmeticObject) + "|" + _category;
            if (!force && string.IsNullOrEmpty(_autoIdentitySeed) &&
                IsStableId(_packId) && IsStableId(_cosmeticId))
            {
                _autoIdentitySeed = seed;
                return;
            }

            if (!force && seed == _autoIdentitySeed && IsStableId(_packId) && IsStableId(_cosmeticId))
            {
                return;
            }

            string token = Hash128.Compute(seed).ToString().ToLowerInvariant();
            _packId = "htfmorehead.pack." + token.Substring(0, 16);
            _cosmeticId = GetIdPrefix(_category) + token.Substring(16, 16);
            _autoIdentitySeed = seed;
        }

        private static string GetIdentitySeed(GameObject cosmeticObject)
        {
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(cosmeticObject);
            string objectIdentity = globalId.ToString();
            if (string.IsNullOrEmpty(objectIdentity))
            {
                objectIdentity = cosmeticObject.scene.path + "/" + GetHierarchyPath(cosmeticObject.transform);
            }

            return objectIdentity;
        }

        private static bool IsExpectedAnchor(Transform candidate, CosmeticCategory category)
        {
            if (candidate == null)
            {
                return false;
            }

            if (category == CosmeticCategory.Accessory || category == CosmeticCategory.Outfit)
            {
                string expectedName = category == CosmeticCategory.Accessory ? "Accessory" : "Outfit";
                Transform referenceRoot = candidate.parent;
                return candidate.name == expectedName && referenceRoot != null &&
                       referenceRoot.Find("Armature") != null && referenceRoot.Find("Hat") != null &&
                       referenceRoot.Find("Accessory") != null && referenceRoot.Find("Outfit") != null;
            }

            if (candidate.name != ExpectedHeadAnchorName)
            {
                return false;
            }

            Transform head = candidate.parent;
            Transform body = head != null ? head.parent : null;
            Transform armature = body != null ? body.parent : null;
            return head != null && head.name == "Head" &&
                   body != null && body.name == "Body" &&
                   armature != null && armature.name == "Armature";
        }

        private static Transform FindExpectedAnchor(Transform preferredRoot, CosmeticCategory category)
        {
            if (preferredRoot != null)
            {
                Transform[] preferred = preferredRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < preferred.Length; i++)
                {
                    if (IsExpectedAnchor(preferred[i], category))
                    {
                        return preferred[i];
                    }
                }
            }

            Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            Transform found = null;
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (!candidate.gameObject.scene.IsValid() || !IsExpectedAnchor(candidate, category))
                {
                    continue;
                }

                if (found != null && found != candidate)
                {
                    Debug.LogError("[HTFMoreHead SDK] Multiple valid category anchors are open. Select the intended reference character explicitly.");
                    return null;
                }

                found = candidate;
            }

            return found;
        }

        private static string GetExpectedAnchorDescription(CosmeticCategory category)
        {
            switch (category)
            {
                case CosmeticCategory.Accessory:
                    return "the reference player's Accessory child";
                case CosmeticCategory.Outfit:
                    return "the reference player's Outfit child";
                default:
                    return ExpectedHeadAnchorPath;
            }
        }

        private static string GetSlotName(CosmeticCategory category)
        {
            switch (category)
            {
                case CosmeticCategory.Accessory:
                    return "accessory";
                case CosmeticCategory.Outfit:
                    return "outfit";
                default:
                    return "hat";
            }
        }

        private static string GetAnchorName(CosmeticCategory category)
        {
            return category == CosmeticCategory.Hat ? "head" : GetSlotName(category);
        }

        private static string GetIdPrefix(CosmeticCategory category)
        {
            return GetSlotName(category) + ".";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "<none>";
            }

            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static int StripUnsafeComponents(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            int stripped = 0;
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is MeshFilter || component is MeshRenderer)
                {
                    continue;
                }

                DestroyImmediate(component);
                stripped++;
            }

            if (root.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
            {
                throw new InvalidOperationException("Static cosmetic package contains no MeshRenderer.");
            }

            return stripped;
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '.' && c != '_' && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        [Serializable]
        private sealed class EmbeddedManifest
        {
            public int schemaVersion;
            public string packId;
            public string packVersion;
            public string authorName;
            public string bundleFile;
            public EmbeddedCosmetic[] cosmetics;
        }

        [Serializable]
        private sealed class EmbeddedCosmetic
        {
            public string id;
            public string displayName;
            public string slot;
            public string anchor;
            public string prefab;
            public Float3 localPosition;
            public Float3 localEulerAngles;
            public Float3 localScale;
        }

        private enum CosmeticCategory
        {
            Hat,
            Accessory,
            Outfit
        }

        [Serializable]
        private struct Float3
        {
            public float x;
            public float y;
            public float z;

            public Float3(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        private struct BuildMetrics
        {
            public int Meshes;
            public int Renderers;
            public long Vertices;
            public long Triangles;
            public int Materials;
            public int Textures;
            public int MaxTextureSize;
            public int StrippedComponents;
        }
    }
}
