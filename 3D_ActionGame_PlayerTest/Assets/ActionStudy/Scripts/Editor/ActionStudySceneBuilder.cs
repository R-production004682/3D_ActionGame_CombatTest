using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace ActionStudy.EditorTools
{
    /// <summary>
    /// メニュー「ActionStudy/Build Sample Scene」で、学習用シーンを Assets/SampleScene.unity に組み立てる。
    /// 何度実行しても同じ結果になる(前回生成したオブジェクトは作り直す)。
    /// 生成物: テクスチャ / マテリアル / Volume Profile / 敵プレハブ (Assets/ActionStudy/Generated)
    /// </summary>
    public static class ActionStudySceneBuilder
    {
        const string k_ScenePath = "Assets/SampleScene.unity";
        const string k_GeneratedDir = "Assets/ActionStudy/Generated";
        const string k_TextureDir = k_GeneratedDir + "/Textures";
        const string k_MaterialDir = k_GeneratedDir + "/Materials";

        static readonly string[] k_GeneratedRoots =
        {
            "Ground", "Player", "SwordTrail", "HitFeel Manager", "HitFeel PostProcess Volume",
        };

        [MenuItem("ActionStudy/Build Sample Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        /// <summary>ダイアログを出さずに組み立てる(外部ツールからの実行用)。</summary>
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != k_ScenePath)
                scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

            EnsureFolder(k_TextureDir);
            EnsureFolder(k_MaterialDir);

            // ---------- テクスチャ ----------
            Texture2D glowTex = SaveTexture("T_SoftGlow", 128, 128, (u, v) =>
            {
                float r = Radius(u, v);
                float a = Mathf.Exp(-r * r * 5f) * 0.6f + Mathf.Exp(-r * r * 45f);
                return White(a * Mathf.Clamp01((1f - r) * 6f));
            });
            Texture2D sparkTex = SaveTexture("T_Spark", 64, 64, (u, v) =>
            {
                float r = Radius(u, v);
                float a = Mathf.Exp(-r * r * 10f) * 0.5f + Mathf.Exp(-r * r * 70f);
                return White(a * Mathf.Clamp01((1f - r) * 6f));
            });
            Texture2D flareTex = SaveTexture("T_Flare", 256, 64, (u, v) =>
            {
                float x = Mathf.Abs(u - 0.5f) * 2f;
                float y = Mathf.Abs(v - 0.5f) * 2f;
                float along = Mathf.Pow(1f - x, 2.5f);
                float a = along * (Mathf.Exp(-y * y * 60f) + 0.25f * Mathf.Exp(-y * y * 6f));
                return White(a * Mathf.Clamp01((1f - y) * 4f));
            });
            Texture2D ringTex = SaveTexture("T_Ring", 256, 256, (u, v) =>
            {
                float r = Radius(u, v);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.07f, 2f));
                float inner = 0.18f * Mathf.Exp(-Mathf.Pow((r - 0.6f) / 0.22f, 2f));
                return White((ring + inner) * Mathf.Clamp01((1f - r) * 8f));
            });
            Texture2D smokeTex = SaveTexture("T_Smoke", 128, 128, (u, v) =>
            {
                float r = Radius(u, v);
                float n = 0f, amp = 0.5f, freq = 3f;
                for (int i = 0; i < 4; i++)
                {
                    n += amp * Mathf.PerlinNoise(u * freq + 13.7f, v * freq + 7.1f);
                    amp *= 0.5f;
                    freq *= 2f;
                }
                float falloff = Mathf.Clamp01(1f - r);
                return White(falloff * falloff * Mathf.Clamp01(n * 1.4f - 0.15f));
            });
            Texture2D trailTex = SaveTexture("T_SwordTrail", 128, 64, (u, v) =>
            {
                // u = 古さ (0 新 → 1 古)、v = 0 根元側 → 1 剣先
                float edge = Mathf.Exp(-Mathf.Pow((1f - v) / 0.1f, 2f));
                float body = Mathf.SmoothStep(0f, 1f, v) * 0.35f;
                return White((edge + body) * (1f - u * 0.6f));
            });
            Texture2D gridTex = SaveTexture("T_Grid", 512, 512, (u, v) =>
            {
                float major = Mathf.Min(Line(u, 0.006f), Line(v, 0.006f));
                float minor = Mathf.Min(Line(u * 4f % 1f, 0.012f), Line(v * 4f % 1f, 0.012f));
                float c = 0.42f;
                c = Mathf.Lerp(0.36f, c, minor);
                c = Mathf.Lerp(0.26f, c, major);
                return new Color(c, c, c * 1.02f, 1f);
            }, srgb: true, wrap: TextureWrapMode.Repeat);

            // ---------- マテリアル ----------
            Material sparkMat = ParticleMaterial("M_Spark", sparkTex, new Color(1f, 0.92f, 0.8f), 9f, true);
            Material emberMat = ParticleMaterial("M_Ember", sparkTex, new Color(1f, 0.7f, 0.4f), 6f, true);
            Material flashMat = ParticleMaterial("M_ImpactFlash", glowTex, Color.white, 6f, true);
            Material flareMat = ParticleMaterial("M_Flare", flareTex, Color.white, 5f, true);
            Material slashMat = ParticleMaterial("M_SlashMark", sparkTex, new Color(0.85f, 0.93f, 1f), 7f, true);
            Material ringMat = ParticleMaterial("M_ShockRing", ringTex, new Color(1f, 0.95f, 0.9f), 2.5f, true);
            Material smokeMat = ParticleMaterial("M_Smoke", smokeTex, Color.white, 1f, false);
            Material trailMat = ParticleMaterial("M_SwordTrail", trailTex, Color.white, 2.5f, true);
            Material enemyEyeMat = ParticleMaterial("M_EnemyEye", glowTex, new Color(1f, 0.55f, 0.15f), 5f, true);

            Material hitFlashMat = SaveMaterial("M_HitFlash", new Material(Shader.Find("ActionStudy/HitFlash")), m =>
            {
                m.SetColor("_FlashColor", Color.white);
                m.SetFloat("_FlashIntensity", 0f);
                m.SetFloat("_RimPower", 2f);
                m.SetFloat("_RimBoost", 2f);
            });
            Material radialBlurMat = SaveMaterial("M_RadialBlur", new Material(Shader.Find("Hidden/ActionStudy/RadialBlur")), null);

            Material groundMat = LitMaterial("M_Ground", Color.white, 0f, 0.35f, gridTex, new Vector2(40f, 40f));
            Material playerMat = LitMaterial("M_PlayerBody", new Color(0.06f, 0.06f, 0.07f), 0f, 0.55f);
            Material visorMat = LitMaterial("M_PlayerVisor", new Color(0.85f, 0.85f, 0.82f), 0f, 0.8f);
            Material swordMat = LitMaterial("M_Sword", new Color(0.86f, 0.87f, 0.9f), 1f, 0.88f);
            Material hiltMat = LitMaterial("M_SwordHilt", new Color(0.12f, 0.1f, 0.09f), 0.6f, 0.5f);
            Material enemyMat = LitMaterial("M_EnemyBody", new Color(0.62f, 0.48f, 0.3f), 0.85f, 0.45f);
            Material debrisMat = LitMaterial("M_MetalDebris", new Color(0.75f, 0.72f, 0.68f), 1f, 0.7f);

            VolumeProfile profile = BuildVolumeProfile(k_GeneratedDir + "/HitFeelVolumeProfile.asset");
            Enemy enemyPrefab = BuildEnemyPrefab(k_GeneratedDir + "/Enemy.prefab", enemyMat, enemyEyeMat, hitFlashMat);

            // ---------- シーン ----------
            foreach (string rootName in k_GeneratedRoots)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == rootName)
                        Object.DestroyImmediate(root);
                }
            }

            // 床
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // プレイヤー
            var player = new GameObject("Player");
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;
            var inputReader = player.AddComponent<PlayerInputReader>();
            var controller = player.AddComponent<PlayerController>();
            var combat = player.AddComponent<PlayerCombat>();

            Transform leanRoot = CreateChild("LeanRoot", player.transform, Vector3.zero);
            Transform spinRoot = CreateChild("SpinRoot", leanRoot, Vector3.zero);
            GameObject playerBody = CreatePrimitive(PrimitiveType.Capsule, "Body", spinRoot, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), playerMat);
            CreatePrimitive(PrimitiveType.Cube, "Visor", spinRoot, new Vector3(0f, 1.62f, 0.26f), new Vector3(0.5f, 0.09f, 0.22f), visorMat);

            // 剣: Capsule の右横に、Cube を長くして剣に見立てる
            Transform swordPivot = CreateChild("SwordPivot", spinRoot, combat.idleHandPosition);
            swordPivot.localRotation = Quaternion.Euler(combat.idleSwordEuler);
            CreatePrimitive(PrimitiveType.Cube, "Grip", swordPivot, new Vector3(0f, 0f, -0.02f), new Vector3(0.045f, 0.045f, 0.24f), hiltMat);
            CreatePrimitive(PrimitiveType.Cube, "Guard", swordPivot, new Vector3(0f, 0f, 0.12f), new Vector3(0.26f, 0.05f, 0.05f), hiltMat);
            CreatePrimitive(PrimitiveType.Cube, "Blade", swordPivot, new Vector3(0f, 0f, 0.8f), new Vector3(0.09f, 0.022f, 1.3f), swordMat);
            Transform bladeBase = CreateChild("BladeBase", swordPivot, new Vector3(0f, 0f, 0.2f));
            Transform bladeTip = CreateChild("BladeTip", swordPivot, new Vector3(0f, 0f, 1.45f));

            // 剣の軌跡
            var trailObject = new GameObject("SwordTrail");
            trailObject.AddComponent<MeshFilter>();
            var trailRenderer = trailObject.AddComponent<MeshRenderer>();
            trailRenderer.sharedMaterial = trailMat;
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            var trail = trailObject.AddComponent<SwordTrail>();
            trail.bladeBase = bladeBase;
            trail.bladeTip = bladeTip;

            // カメラ
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camObject.AddComponent<Camera>();
                camObject.AddComponent<AudioListener>();
            }
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 2.8f, -5f), Quaternion.Euler(15f, 0f, 0f));
            var hdCamera = cam.GetComponent<HDAdditionalCameraData>();
            if (hdCamera == null)
                hdCamera = cam.gameObject.AddComponent<HDAdditionalCameraData>();
            // 速い火花が TAA で滲まないよう SMAA を使う
            hdCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var cameraRig = cam.GetComponent<ThirdPersonCamera>();
            if (cameraRig == null)
                cameraRig = cam.gameObject.AddComponent<ThirdPersonCamera>();
            cameraRig.target = player.transform;
            cameraRig.input = inputReader;
            cameraRig.controller = controller;
            cameraRig.combat = combat;

            controller.input = inputReader;
            controller.cameraRig = cameraRig;
            controller.leanRoot = leanRoot;

            combat.controller = controller;
            combat.input = inputReader;
            combat.spinRoot = spinRoot;
            combat.body = playerBody.transform;
            combat.swordPivot = swordPivot;
            combat.bladeBase = bladeBase;
            combat.bladeTip = bladeTip;
            combat.trail = trail;

            // ポストプロセス
            var volumeObject = new GameObject("HitFeel PostProcess Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            var screenFx = volumeObject.AddComponent<ScreenFxController>();
            screenFx.radialBlurMaterial = radialBlurMat;

            // 演出マネージャー
            var manager = new GameObject("HitFeel Manager");
            manager.AddComponent<HitFeelSettings>();
            var timeController = manager.AddComponent<TimeController>();
            var hitAudio = manager.AddComponent<HitAudio>();
            var feedback = manager.AddComponent<HitFeedback>();
            var panel = manager.AddComponent<HitFeelDebugPanel>();
            var spawner = manager.AddComponent<EnemySpawner>();

            Transform sparksObject = CreateChild("HitSparkSystem", manager.transform, Vector3.zero);
            var sparks = sparksObject.gameObject.AddComponent<HitSparkSystem>();
            sparks.sparkMaterial = sparkMat;
            sparks.emberMaterial = emberMat;
            sparks.flashMaterial = flashMat;
            sparks.flareMaterial = flareMat;
            sparks.slashMaterial = slashMat;
            sparks.ringMaterial = ringMat;
            sparks.smokeMaterial = smokeMat;
            sparks.debrisMaterial = debrisMat;
            sparks.debrisMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            sparks.groundPlane = ground.transform;

            feedback.sparks = sparks;
            feedback.hitAudio = hitAudio;
            feedback.timeController = timeController;
            feedback.cameraRig = cameraRig;
            feedback.screenFx = screenFx;

            panel.input = inputReader;
            panel.combat = combat;

            spawner.enemyPrefab = enemyPrefab;
            spawner.player = player.transform;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = manager;
            Debug.Log("[ActionStudy] SampleScene を組み立てました。Play で開始、Tab でヒット演出パネル。");
        }

        // ------------------------------------------------------------------
        // 敵プレハブ
        // ------------------------------------------------------------------

        static Enemy BuildEnemyPrefab(string path, Material bodyMat, Material eyeMat, Material flashMat)
        {
            var root = new GameObject("Enemy");
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.5f;

            var enemy = root.AddComponent<Enemy>();
            Transform visual = CreateChild("Visual", root.transform, Vector3.zero);
            GameObject body = CreatePrimitive(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 1f, 0f), Vector3.one, bodyMat);
            CreatePrimitive(PrimitiveType.Sphere, "EyeL", visual, new Vector3(-0.17f, 1.55f, 0.42f), Vector3.one * 0.13f, eyeMat);
            CreatePrimitive(PrimitiveType.Sphere, "EyeR", visual, new Vector3(0.17f, 1.55f, 0.42f), Vector3.one * 0.13f, eyeMat);

            enemy.visual = visual;
            enemy.flashRenderers = new[] { body.GetComponent<Renderer>() };
            enemy.flashMaterial = flashMat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Enemy>();
        }

        // ------------------------------------------------------------------
        // Volume Profile
        // ------------------------------------------------------------------

        static VolumeProfile BuildVolumeProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (VolumeComponent component in profile.components.ToArray())
                Object.DestroyImmediate(component, true);
            profile.components.Clear();

            var bloom = AddComponent<Bloom>(profile);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.7f);
            bloom.threshold.Override(0.9f);

            var vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            // 露出を固定: 自動露出だと閃光や着弾ライトのたびに露出が揺れ(ポンピング)、同じヒットでも見え方が変わってしまう。
            // 値は既存の Sky and Fog の自動露出の上限 (EV 14 = 昼光で落ち着く値) に合わせている
            var exposure = AddComponent<Exposure>(profile);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(14f);

            // モーションブラーは物体だけ: カメラ由来のブラーを切り、シェイク中も画面がにじまないようにする。
            // 剣や吹き飛ぶ敵は物体のブラーでブレ、ヒットストップ中は止まるのでくっきり見える
            var motionBlur = AddComponent<MotionBlur>(profile);
            motionBlur.intensity.Override(0.6f);
            motionBlur.cameraMotionBlur.Override(false);

            AddComponent<ChromaticAberration>(profile).intensity.Override(0f);
            AddComponent<LensDistortion>(profile).intensity.Override(0f);
            var colorAdjustments = AddComponent<ColorAdjustments>(profile);
            colorAdjustments.postExposure.Override(0f);
            colorAdjustments.saturation.Override(0f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(false);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------
        // ヘルパー
        // ------------------------------------------------------------------

        static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static float Radius(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            return Mathf.Sqrt(x * x + y * y);
        }

        static float Line(float x, float width)
        {
            float d = Mathf.Min(x, 1f - x);
            return Mathf.SmoothStep(0f, 1f, d / width);
        }

        static Color White(float alpha)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }

        static Texture2D SaveTexture(string name, int width, int height, Func<float, float, Color> pixel,
            bool srgb = false, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            string path = $"{k_TextureDir}/{name}.png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = srgb;
            importer.alphaIsTransparency = !srgb;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.anisoLevel = srgb ? 8 : 1;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material ParticleMaterial(string name, Texture2D texture, Color tint, float intensity, bool additive)
        {
            return SaveMaterial(name, new Material(Shader.Find("ActionStudy/ParticleUnlit")), m =>
            {
                m.SetTexture("_MainTex", texture);
                m.SetColor("_TintColor", tint);
                m.SetFloat("_Intensity", intensity);
                m.SetFloat("_ExposureWeight", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_Cull", (float)CullMode.Off);
            });
        }

        static Material LitMaterial(string name, Color color, float metallic, float smoothness, Texture2D baseMap = null, Vector2 tiling = default)
        {
            return SaveMaterial(name, new Material(Shader.Find("HDRP/Lit")), m =>
            {
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
                if (baseMap != null)
                {
                    m.SetTexture("_BaseColorMap", baseMap);
                    m.SetTextureScale("_BaseColorMap", tiling);
                }
                HDMaterial.ValidateMaterial(m);
            });
        }

        /// <summary>既存のマテリアルがあれば GUID を保ったまま中身を差し替える。</summary>
        static Material SaveMaterial(string name, Material source, Action<Material> setup)
        {
            string path = $"{k_MaterialDir}/{name}.mat";
            setup?.Invoke(source);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = source.shader;
                existing.CopyPropertiesFromMaterial(source);
                existing.shaderKeywords = source.shaderKeywords;
                existing.renderQueue = source.renderQueue;
                Object.DestroyImmediate(source);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(source, path);
            return source;
        }
    }
}
