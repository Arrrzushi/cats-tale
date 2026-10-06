using System.Collections.Generic;
using System.IO;
using System.Linq;
using PawTown;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PawTown > 1. Build Prefabs: FBX in Assets/PawTown/Models/* -> toon materials + prefabs + tile library.
/// PawTown > 2. Create Demo Scene: endless town you can walk around in with WASD.
/// </summary>
public static class PawTownBuilder
{
    const string Root = "Assets/PawTown";
    const string Models = Root + "/Models";
    const string Mats = Root + "/Materials";
    const string Prefabs = Root + "/Prefabs";
    static readonly string[] PropFolders = { "Props", "Buildings", "Backdrop", "Vehicles", "Characters", "Treats" };
    static readonly HashSet<string> GroundKeys = new HashSet<string>
        { "Asphalt", "Sidewalk", "Grass", "Gravel", "Sand", "Stone", "StoneDark", "Rubber", "Dirt" };
    static readonly HashSet<string> NoShadowKeys = new HashSet<string>
        { "Outline", "LineWhite", "LineYellow", "Water", "WaterEdge" };

    [MenuItem("PawTown/1. Build Prefabs")]
    public static void BuildAll()
    {
        Ensure(Mats); Ensure(Prefabs);
        ConfigureImporters();
        var mats = new Dictionary<string, Material>();
        int n = 0;
        foreach (var folder in PropFolders)
            foreach (var path in Fbx(folder))
            {
                BuildPrefab(path, folder, mats, false);
                n++;
            }
        var lib = LoadOrCreate<PawTownLibrary>(Root + "/PawTownLibrary.asset");
        lib.tiles.Clear();
        lib.backdrop.Clear();
        foreach (var path in Fbx("Tiles"))
        {
            var pf = BuildPrefab(path, "Tiles", mats, true);
            lib.tiles.Add(Describe(pf));
            n++;
        }
        lib.clouds.Clear();
        foreach (var path in Directory.GetFiles(Prefabs + "/Backdrop", "*.prefab"))
        {
            var bg = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (path.Contains("Cloud")) lib.clouds.Add(bg); else lib.backdrop.Add(bg);
        }
        ConfigureAudio();
        ConfigureUI();
        lib.ground = Mat("GrassDark", mats);
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PawTown] built {n} prefabs, {lib.tiles.Count} tiles, {mats.Count} materials");
    }

    // ------------------------------------------------------------------ import
    static void ConfigureImporters()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
            bool rigged = path.Contains("/Characters/");
            bool dirty = false;
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (mi.importCameras || mi.importLights) { mi.importCameras = false; mi.importLights = false; dirty = true; }
            if (!rigged && mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (rigged && mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; dirty = true; }
            // the cat's fur patterns read its rest-pose vertices at runtime
            bool readable = path.Contains("/Characters/Cat_");
            if (readable && !mi.isReadable) { mi.isReadable = true; dirty = true; }
            if (rigged)
            {
                // always rebuild from the FBX takes so newly added clips (e.g. Run) are never dropped
                var clips = mi.defaultClipAnimations;
                var have = mi.clipAnimations.Select(c => c.name).OrderBy(x => x).ToArray();
                var want = clips.Select(c => c.name).OrderBy(x => x).ToArray();
                if (!have.SequenceEqual(want) || mi.clipAnimations.Any(c => !c.loopTime))
                {
                    foreach (var c in clips) { c.loopTime = true; c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true; }
                    mi.clipAnimations = clips;
                    dirty = true;
                }
            }
            if (mi.importNormals != ModelImporterNormals.Import) { mi.importNormals = ModelImporterNormals.Import; dirty = true; }
            if (dirty) mi.SaveAndReimport();
        }
    }

    static void ConfigureAudio()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) continue;
            string n = Path.GetFileNameWithoutExtension(path);
            bool longClip = n.StartsWith("music") || n.StartsWith("amb");
            var st = ai.defaultSampleSettings;
            var want = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            var fmt = longClip ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            if (st.loadType != want || st.compressionFormat != fmt || !ai.forceToMono)
            {
                st.loadType = want;
                st.compressionFormat = fmt;
                st.quality = 0.6f;
                ai.defaultSampleSettings = st;
                ai.forceToMono = true;
                ai.SaveAndReimport();
            }
        }
    }

    static AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{n}.wav");

    static AudioClip[] ClipsStartingWith(string prefix) =>
        Directory.GetFiles(Root + "/Audio", prefix + "*.wav").OrderBy(p => p)
            .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).Where(c => c != null).ToArray();

    static IEnumerable<string> Fbx(string folder)
    {
        string dir = Models + "/" + folder;
        if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
        return Directory.GetFiles(dir, "*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p);
    }

    // ------------------------------------------------------------------ materials
    static Material Mat(string key, Dictionary<string, Material> cache)
    {
        if (cache.TryGetValue(key, out var m)) return m;
        string path = $"{Mats}/M_{key}.mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool emissive = PawTownPalette.Emissive.ContainsKey(key);
        string hex = emissive ? PawTownPalette.Emissive[key] : (PawTownPalette.Base.TryGetValue(key, out var h) ? h : "#FF00FF");
        ColorUtility.TryParseHtmlString(hex, out var col);
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader sh;
        if (key == "Outline" || emissive)
            sh = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        else
            sh = urp ? Shader.Find("PawTown/Toon") : Shader.Find("Standard");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else m.shader = sh;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        cache[key] = m;
        return m;
    }

    static string KeyOf(string objectName)
    {
        string n = objectName;
        int dot = n.LastIndexOf('.');
        if (dot > 0 && n.Substring(dot + 1).All(char.IsDigit)) n = n.Substring(0, dot);
        int us = n.LastIndexOf('_');
        return us >= 0 ? n.Substring(us + 1) : n;
    }

    static string MarkerName(string objectName, string prefix)
    {
        string n = objectName.Substring(prefix.Length);
        int dot = n.LastIndexOf('.');
        if (dot > 0 && n.Substring(dot + 1).All(char.IsDigit)) n = n.Substring(0, dot);
        return n;
    }

    // ------------------------------------------------------------------ prefabs
    static GameObject BuildPrefab(string fbxPath, string folder, Dictionary<string, Material> mats, bool isTile)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        string name = Path.GetFileNameWithoutExtension(fbxPath);
        go.name = name;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            string key = KeyOf(r.gameObject.name);
            var m = Mat(key, mats);
            r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            if (NoShadowKeys.Contains(key) || PawTownPalette.Emissive.ContainsKey(key))
                r.shadowCastingMode = ShadowCastingMode.Off;
            if (key == "Outline") r.receiveShadows = false;
            if (isTile && GroundKeys.Contains(key) && r is MeshRenderer)
                r.gameObject.AddComponent<MeshCollider>();
        }

        if (isTile)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (!t.name.StartsWith("P_")) continue;
                string prop = MarkerName(t.name, "P_");
                GameObject src = null;
                foreach (var f in PropFolders)
                {
                    src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{f}/{prop}.prefab");
                    if (src) break;
                }
                if (src == null) { Debug.LogWarning($"[PawTown] {name}: no prefab for marker {prop}"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, t);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
            }
            if (name == "Rail_RoadCrossing") go.AddComponent<RailCrossingTile>();
            if (name.StartsWith("River_") && false)   // the river is swimmable now (no invisible bank walls)
            {
                // invisible banks so nobody walks into the water; leave the bridge corridor open
                float gap = name == "River_RoadBridge" ? 6.3f : name == "River_RailBridge" ? 2.7f : 0f;
                foreach (float z in new[] { -4.1f, 4.1f })
                    foreach (float sx in gap > 0 ? new[] { -1f, 1f } : new[] { 0f })
                    {
                        var bc = go.AddComponent<BoxCollider>();
                        float len = gap > 0 ? 12f - gap : 24f;
                        bc.center = new Vector3(sx * (gap + len / 2f), 0f, z);
                        bc.size = new Vector3(len, 2.6f, 0.3f);
                    }
            }
        }
        else
        {
            if (name == "TrafficLight") go.AddComponent<TrafficLightCycle>();
            if (name == "PedLight") go.AddComponent<TrafficLightCycle>().mode = TrafficLightCycle.Mode.PedestrianOnly;
            if (name == "CrossingBarrier" || name == "CrossingSign") go.AddComponent<RailCrossingPart>();
            if (folder == "Characters")
            {
                SetupAnimator(go, fbxPath, name);
                if (!name.StartsWith("Cat"))   // the pet uses a CharacterController instead
                {
                    var c = go.AddComponent<CapsuleCollider>();
                    c.radius = 0.25f; c.height = 1.3f; c.center = new Vector3(0, 0.65f, 0);
                }
            }
            else if (folder == "Buildings" || folder == "Vehicles") AddBox(go);
            else if (folder == "Props")
            {
                if (name.StartsWith("Tree") || name.Contains("Lamp") || name.Contains("Light") || name.Contains("Sign"))
                {
                    var c = go.AddComponent<CapsuleCollider>();   // trunk / pole only, so the canopy does not block
                    c.radius = name.StartsWith("Tree") ? 0.38f : 0.2f; c.height = 3f; c.center = new Vector3(0, 1.5f, 0);
                    if (name.StartsWith("Tree"))
                    {
                        // canopy trigger: walk-through, but the camera uses it to fade the tree when it hides the cat
                        var canopy = go.AddComponent<SphereCollider>();
                        canopy.isTrigger = true;
                        canopy.radius = name.Contains("Pine") ? 1.4f : 1.7f;
                        canopy.center = new Vector3(0, name.Contains("Pine") ? 2.2f : 2.9f, 0);
                    }
                }
                else AddBox(go);   // bushes, benches, fences, swing, slide, rocks, bins, hydrants, flower beds...
            }
        }

        Ensure($"{Prefabs}/{folder}");
        string outPath = $"{Prefabs}/{folder}/{name}.prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(go, outPath);
        Object.DestroyImmediate(go);
        return saved;
    }

    static void SetupAnimator(GameObject go, string fbxPath, string name)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview")).ToList();
        if (clips.Count == 0) { Debug.LogWarning($"[PawTown] {name}: no animation clips found"); return; }
        Ensure(Prefabs + "/Characters");
        string cpath = $"{Prefabs}/Characters/{name}.controller";
        AssetDatabase.DeleteAsset(cpath);
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(cpath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);          // metres per second
        ctrl.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var sm = ctrl.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState Add(string n, AnimationClip c)
        {
            var st = sm.AddState(n);
            st.motion = c;
            st.speedParameter = "AnimSpeed";
            st.speedParameterActive = true;
            return st;
        }
        void Link(UnityEditor.Animations.AnimatorState a, UnityEditor.Animations.AnimatorState b, UnityEditor.Animations.AnimatorConditionMode m, float v, float dur)
        {
            var t = a.AddTransition(b);
            t.AddCondition(m, v, "Speed");
            t.hasExitTime = false;
            t.duration = dur;
        }
        var idleClip = clips.FirstOrDefault(c => c.name.Contains("Idle")) ?? clips[0];
        var walkClip = clips.FirstOrDefault(c => c.name.Contains("Walk"));
        var runClip = clips.FirstOrDefault(c => c.name.Contains("Run"));
        var jumpClip = clips.FirstOrDefault(c => c.name.Contains("Jump"));
        var idle = Add("Idle", idleClip);
        sm.defaultState = idle;
        if (walkClip != null)
        {
            var walk = Add("Walk", walkClip);
            Link(idle, walk, UnityEditor.Animations.AnimatorConditionMode.Greater, 0.08f, 0.12f);
            Link(walk, idle, UnityEditor.Animations.AnimatorConditionMode.Less, 0.06f, 0.15f);
            if (runClip != null)
            {
                var run = Add("Run", runClip);
                Link(walk, run, UnityEditor.Animations.AnimatorConditionMode.Greater, 2.4f, 0.15f);
                Link(run, walk, UnityEditor.Animations.AnimatorConditionMode.Less, 2.1f, 0.18f);
                Link(run, idle, UnityEditor.Animations.AnimatorConditionMode.Less, 0.06f, 0.2f);
            }
        }
        if (jumpClip != null)
        {
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            var jump = sm.AddState("Jump");
            jump.motion = jumpClip;
            var tin = sm.AddAnyStateTransition(jump);
            tin.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Grounded");
            tin.hasExitTime = false; tin.duration = 0.06f; tin.canTransitionToSelf = false;
            var tout = jump.AddTransition(idle);
            tout.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Grounded");
            tout.hasExitTime = false; tout.duration = 0.1f;
        }
        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    static void AddBox(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => KeyOf(r.gameObject.name) != "Outline").ToArray();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(b.center);
        box.size = b.size;
    }

    static TileEntry Describe(GameObject pf)
    {
        var e = new TileEntry { name = pf.name, prefab = pf, isLot = pf.name.StartsWith("Lot_") };
        foreach (var t in pf.GetComponentsInChildren<Transform>(true))
        {
            Vector3 p = pf.transform.InverseTransformPoint(t.position);
            if (t.name.StartsWith("CONN_"))
            {
                string kind = MarkerName(t.name, "CONN_");
                int d = Dir.FromLocal(p);
                if (kind == "Road") e.road |= d;
                else if (kind == "Rail") e.rail |= d;
                else if (kind == "River") e.river |= d;
            }
            else if (t.name.StartsWith("FRONT")) e.front = Dir.FromLocal(p);
        }
        return e;
    }

    // ------------------------------------------------------------------ demo scene
    [MenuItem("PawTown/2. Create Demo Scene")]
    public static void CreateDemoScene()
    {
        if (AssetDatabase.LoadAssetAtPath<PawTownLibrary>(Root + "/PawTownLibrary.asset") == null) BuildAll();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // load AFTER NewScene: NewScene unloads unused assets and would leave us holding a dead reference
        var lib = AssetDatabase.LoadAssetAtPath<PawTownLibrary>(Root + "/PawTownLibrary.asset");

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.74f, 0.9f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.66f, 0.85f, 0.97f);
        RenderSettings.fogStartDistance = 320f;
        RenderSettings.fogEndDistance = 1200f;
        RenderSettings.skybox = null;

        // pet root sits at the feet; CharacterController handles collisions, curbs and slopes
        var pet = new GameObject("Pet");
        pet.transform.position = new Vector3(44f, 0.6f, 5.9f);   // sidewalk in front of the HQ
        var cc = pet.AddComponent<CharacterController>();
        cc.height = 0.56f; cc.radius = 0.17f; cc.center = new Vector3(0, 0.3f, 0);
        cc.stepOffset = 0.3f; cc.slopeLimit = 55f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
        pet.AddComponent<PawTownDemoMover>();
        var cat = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Characters/Cat_Orange.prefab");
        if (cat)
        {
            var c = (GameObject)PrefabUtility.InstantiatePrefab(cat, pet.transform);
            c.transform.localPosition = Vector3.zero;
            c.transform.localRotation = Quaternion.identity;
            c.transform.localScale = Vector3.one * 0.5f;   // pet-sized next to cars and houses
        }

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.66f, 0.85f, 0.97f);
        cam.fieldOfView = 40f;
        cam.farClipPlane = 1500f;
        camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<PawTownFollowCamera>();
        follow.target = pet.transform;
        // one cinematic follow camera (see PawTownFollowCamera for the framing / beats / occlusion fade)
        camGo.transform.position = pet.transform.position + new Vector3(0f, 7f, -7.2f);
        camGo.transform.LookAt(pet.transform);

        var gen = new GameObject("PawTown").AddComponent<PawTownGenerator>();
        gen.library = lib;
        gen.target = pet.transform;

        // gameplay: energy + bag + treats, delivery orders
        var game = new GameObject("Game").AddComponent<PawGame>();
        game.generator = gen; game.pet = pet.transform;
        game.fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Treats/Treat_Fish.prefab");
        game.canPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Treats/Treat_Can.prefab");
        game.yarnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Treats/Treat_Yarn.prefab");
        game.fishIcon = UI("slot_fish"); game.canIcon = UI("slot_can"); game.yarnIcon = UI("slot_yarn");
        var orders = game.gameObject.AddComponent<PawOrders>();
        orders.generator = gen; orders.pet = pet.transform;

        BuildHud(follow, pet.transform, gen);

        // out-of-play screens (title, menu, order board, results, level up), built at runtime from Resources/Screens
        new GameObject("Menus").AddComponent<PawMenus>();

        // sound: music, birds, pet + town effects
        var au = new GameObject("Audio").AddComponent<PawAudio>();
        au.music = Clip("music_town"); au.ambience = Clip("amb_birds");
        au.steps = ClipsStartingWith("sfx_step_");     // every paw step cut from the walking recording
        au.jump = Clip("sfx_jump"); au.land = Clip("sfx_land");
        au.meows = ClipsStartingWith("sfx_meow_");     // every meow cut from the meow recording
        au.engineLoop = Clip("car_engine_loop"); au.horn = Clip("sfx_horn");
        au.trainLoop = Clip("train_loop"); au.trainHorn = Clip("sfx_train_horn");
        au.crossingBell = Clip("crossing_bell_loop"); au.pedBeep = Clip("sfx_ped_beep");
        au.click = Clip("ui_click"); au.success = Clip("sfx_success");
        au.transform.SetAsFirstSibling();

        // traffic: cars, people, trains
        var traffic = new GameObject("Traffic").AddComponent<PawTraffic>();
        traffic.generator = gen;
        traffic.pet = pet.transform;
        foreach (var path in Directory.GetFiles(Prefabs + "/Vehicles", "*.prefab").OrderBy(x => x))
        {
            var v = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (v.name.StartsWith("Train_")) continue;
            traffic.vehiclePrefabs.Add(v);
            if (v.name.StartsWith("Car_")) traffic.vehiclePrefabs.Add(v);   // ordinary cars twice as common
        }
        foreach (var path in Directory.GetFiles(Prefabs + "/Characters", "*.prefab").OrderBy(x => x))
        {
            var h = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            if (!h.name.StartsWith("Cat")) traffic.peoplePrefabs.Add(h);
        }
        traffic.trainLoco = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Vehicles/Train_Loco.prefab");
        traffic.trainCarriage = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Vehicles/Train_Carriage.prefab");

        Ensure(Root + "/Scenes");
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/PawTown_Demo.unity");
        Debug.Log("[PawTown] demo scene saved. Press Play: joystick / WASD to walk, floating stick (push to the rim to sprint), JUMP, tap the ground to walk there, tap the quest card to auto-walk, drag the right side to turn the camera, V / view button toggles cat view (behind her) and top view. Follow the quest card + map, collect treats for energy.");
    }

    // ------------------------------------------------------------------ on-screen controls
    static Sprite CircleSprite()
    {
        string path = Root + "/UI/Circle.png";
        if (!File.Exists(path))
        {
            Ensure(Root + "/UI");
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    float a = Mathf.Clamp01(n / 2f - 1f - d);          // anti-aliased edge
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static UnityEngine.UI.Image Circle(string name, Transform parent, float size, Color c, Sprite sp)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = sp;
        img.color = c;
        return img;
    }

    // ------------------------------------------------------------------ HUD (user's Cat's Tale UI art)
    static Sprite UI(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/UI/{n}.png");

    [MenuItem("PawTown/4. Reset Save (fresh player)")]
    static void ResetSave()
    {
        // everything the game saves lives in PlayerPrefs under "PawTown." (level, coins, bag, wardrobe, pass, daily, settings)
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[PawTown] save data wiped: next Play starts as a brand-new player");
    }

    [MenuItem("PawTown/3. Configure UI Sprites")]
    static void ConfigureUIMenu() { ConfigureUI(); Debug.Log("[PawTown] UI sprites configured"); }

    /// <summary>Trail particle sprites: textures with real transparency, clamped, mipmapped (they shrink as they fade).</summary>
    static void ConfigureTrails()
    {
        string dir = Root + "/Resources/Trails";
        if (!Directory.Exists(dir)) return;
        foreach (var path in Directory.GetFiles(dir, "*.png"))
        {
            var p = path.Replace('\\', '/');
            if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
            bool dirty = ti.textureType != TextureImporterType.Default || !ti.alphaIsTransparency || !ti.mipmapEnabled
                         || ti.alphaSource != TextureImporterAlphaSource.FromInput || ti.wrapMode != TextureWrapMode.Clamp;
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.ASTC_4x4) dirty = true;
            if (!dirty) continue;
            ti.textureType = TextureImporterType.Default;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            android.overridden = true; android.format = TextureImporterFormat.ASTC_4x4; android.maxTextureSize = 256;
            ti.SetPlatformTextureSettings(android);
            ti.SaveAndReimport();
        }
    }

    static void ConfigureUI()
    {
        ConfigureTrails();
        var pngs = Directory.GetFiles(Root + "/UI", "*.png").ToList();
        if (Directory.Exists(Root + "/Resources/Screens")) pngs.AddRange(Directory.GetFiles(Root + "/Resources/Screens", "*.png"));
        foreach (var path in pngs)
        {
            var p = path.Replace('\\', '/');
            if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
            bool dirty = ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || !ti.alphaIsTransparency
                         || ti.textureCompression != TextureImporterCompression.Uncompressed;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            if (st.spriteMeshType != SpriteMeshType.FullRect) dirty = true;
            // 9-slice borders (sprite px) for the stretchable cards / panels
            string fn = Path.GetFileNameWithoutExtension(p);
            // borders scale with the art (the HD sheet pieces are bigger than the first vector ones)
            ti.GetSourceTextureWidthAndHeight(out int tw, out int th);
            float m = Mathf.Min(tw, th);
            Vector4 border = fn == "panel_plain" ? Vector4.one * Mathf.Round(m * 0.24f)
                : (fn == "card" || fn == "card_gold" || fn == "card_well") ? Vector4.one * Mathf.Round(m * 0.26f)
                : fn.StartsWith("btn_") ? new Vector4(Mathf.Round(th * 0.5f), 0, Mathf.Round(th * 0.5f), 0)
                : Vector4.zero;
            if (ti.spriteBorder != border) { ti.spriteBorder = border; dirty = true; }
            int maxSize = fn.StartsWith("bg_") || fn == "htp_book" ? 4096 : 2048;   // full-screen backgrounds are 2560 wide
            if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
            // Android: ASTC 4x4 (8 bits per pixel, visually lossless) instead of 32-bit raw: about 4x less memory and
            // much faster loading on phones; the editor and desktop keep the uncompressed originals
            var android = ti.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != TextureImporterFormat.ASTC_4x4 || android.maxTextureSize != maxSize)
            {
                android.overridden = true;
                android.format = TextureImporterFormat.ASTC_4x4;
                android.maxTextureSize = maxSize;
                android.compressionQuality = 100;
                ti.SetPlatformTextureSettings(android);
                dirty = true;
            }
            if (!dirty) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;   // tight meshes clip soft edges
            ti.SetTextureSettings(st);
            ti.SaveAndReimport();
        }
    }

    static UnityEngine.UI.Image Img(string name, Transform parent, Sprite s, Vector2 anchor, Vector2 pos, Vector2 size, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = s;
        img.preserveAspect = true;
        img.raycastTarget = raycast;
        return img;
    }

    static void Stretch(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static readonly Color Ink = new Color(0.33f, 0.22f, 0.15f);

    static UnityEngine.UI.Text Label(Transform parent, string name, string text, int size, Color col, TextAnchor align, bool outline = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<UnityEngine.UI.Text>();
        var fredoka = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/Fredoka-600.ttf");   // SIL OFL, rounded + cute
        t.font = fredoka != null ? fredoka : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = fredoka != null ? FontStyle.Normal : FontStyle.Bold;
        t.color = col;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline)
        {
            var o = go.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = Ink; o.effectDistance = new Vector2(2.5f, -2.5f);
        }
        return t;
    }

    static UnityEngine.UI.Image SpriteButton(Transform canvas, string name, Sprite s, PawActionButton.Action action, float size, Vector2 anchor, Vector2 pos)
    {
        var b = Img(name, canvas, s, anchor, pos, new Vector2(size, size), true);
        b.gameObject.AddComponent<PawActionButton>().action = action;
        return b;
    }

    static void BuildHud(PawTownFollowCamera follow, Transform pet, PawTownGenerator gen)
    {
        var circle = CircleSprite();
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // camera orbit drag area (right side), lowest so every button sits on top of it
        var orbitZone = new GameObject("CameraDragZone", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        orbitZone.transform.SetParent(root, false);
        Stretch((RectTransform)orbitZone.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f));   // drag = turn camera, tap = walk there
        orbitZone.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 0);
        orbitZone.AddComponent<PawCameraDrag>();

        // joystick (your ring + paw knob)
        var zone = new GameObject("JoystickZone", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        zone.transform.SetParent(root, false);
        Stretch((RectTransform)zone.transform, new Vector2(0f, 0f), new Vector2(0.5f, 0.85f));   // floating stick: touch anywhere here
        zone.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 0);
        var pad = Img("Pad", zone.transform, UI("joystick_base"), Vector2.zero, new Vector2(250, 250), new Vector2(330, 330));
        var knob = Img("Knob", pad.transform, UI("joystick_knob"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
        var stick = zone.AddComponent<PawJoystick>();
        stick.pad = pad.rectTransform;
        stick.knob = knob.rectTransform;

        // action buttons (bottom right)
        SpriteButton(root, "JumpButton", UI("btn_jump"), PawActionButton.Action.Jump, 238, new Vector2(1, 0), new Vector2(-185, 215));

        // quest card (top left): "Deliver the order" / "Head to Miss Luna's Bakery  ·  85 m"
        var card = Img("QuestCard", root, UI("quest_card"), new Vector2(0, 1), new Vector2(330, -122), new Vector2(630, 219), true);
        var cardBtn = card.gameObject.AddComponent<UnityEngine.UI.Button>();   // tap = auto-walk the route
        cardBtn.transition = UnityEngine.UI.Selectable.Transition.None;
        var qt = Label(card.transform, "Title", "Finding an order...", 34, Ink, TextAnchor.LowerLeft);
        Stretch(qt.rectTransform, new Vector2(0.22f, 0.5f), new Vector2(0.94f, 0.8f));
        var qs = Label(card.transform, "Subtitle", "", 26, new Color(0.45f, 0.32f, 0.22f), TextAnchor.UpperLeft);
        Stretch(qs.rectTransform, new Vector2(0.22f, 0.2f), new Vector2(0.94f, 0.5f));
        qs.fontStyle = FontStyle.Normal;
        var toast = Label(root, "Toast", "", 30, Color.white, TextAnchor.MiddleCenter, true);
        toast.rectTransform.anchorMin = toast.rectTransform.anchorMax = new Vector2(0, 1);
        toast.rectTransform.anchoredPosition = new Vector2(330, -248);
        toast.rectTransform.sizeDelta = new Vector2(620, 50);

        // energy bar (left, under the card): track + green/yellow fill that drains from the top
        var track = Img("EnergyTrack", root, UI("energy_track"), new Vector2(0, 1), new Vector2(80, -405), new Vector2(84, 247));
        var fill = Img("EnergyFill", track.transform, UI("energy_fill"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 247));
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
        fill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
        fill.fillAmount = 1f;
        var el = Label(track.transform, "Label", "ENERGY", 20, Color.white, TextAnchor.UpperCenter, true);
        el.rectTransform.anchorMin = el.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        el.rectTransform.anchoredPosition = new Vector2(0, -22);
        el.rectTransform.sizeDelta = new Vector2(160, 30);

        // bag + inventory (bottom centre)
        var bag = Img("BagButton", root, UI("icon_bag"), new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(120, 132), true);
        var bagBtn = bag.gameObject.AddComponent<UnityEngine.UI.Button>();
        var bl = Label(bag.transform, "Label", "BAG", 20, Color.white, TextAnchor.UpperCenter, true);
        bl.rectTransform.anchorMin = bl.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        bl.rectTransform.anchoredPosition = new Vector2(0, -14);
        bl.rectTransform.sizeDelta = new Vector2(120, 28);
        var inv = Img("Inventory", root, UI("inventory_panel"), new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(620, 216), true);
        float[] sx = { 0.1226f, 0.2968f, 0.4903f, 0.6935f, 0.8871f };   // well centres
        var icons = new UnityEngine.UI.Image[5];
        var btns = new UnityEngine.UI.Button[5];
        for (int i = 0; i < 5; i++)
        {
            var ic = Img("Slot" + (i + 1), inv.transform, UI("slot_fish"), new Vector2(sx[i], 0.4167f), Vector2.zero, new Vector2(104, 104), true);
            icons[i] = ic;
            btns[i] = ic.gameObject.AddComponent<UnityEngine.UI.Button>();
        }
        var hint = Label(inv.transform, "Hint", "tap a treat to eat it", 20, new Color(0.45f, 0.32f, 0.22f), TextAnchor.MiddleRight);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 1f);
        hint.rectTransform.anchoredPosition = new Vector2(-150, -34);
        hint.rectTransform.sizeDelta = new Vector2(260, 30);
        hint.fontStyle = FontStyle.Italic;

        // big-map backdrop (behind the map, hidden until the map is maximised)
        var dim = Img("MapBackdrop", root, null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        Stretch(dim.rectTransform, Vector2.zero, Vector2.one);
        dim.color = new Color(0.1f, 0.08f, 0.06f, 0.45f);
        dim.preserveAspect = false;

        // cat-head minimap (top right)
        var widgetGo = new GameObject("MiniMap", typeof(RectTransform));
        widgetGo.transform.SetParent(root, false);
        var widget = (RectTransform)widgetGo.transform;
        widget.anchorMin = widget.anchorMax = widget.pivot = new Vector2(1, 1);
        widget.anchoredPosition = new Vector2(-24, -20);
        widget.sizeDelta = new Vector2(420, 330);
        var maskImg = Img("Window", widget, UI("map_mask"), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        Stretch(maskImg.rectTransform, Vector2.zero, Vector2.one);
        maskImg.preserveAspect = false;
        maskImg.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        var rawGo = new GameObject("MapImage", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        rawGo.transform.SetParent(maskImg.transform, false);
        var raw = rawGo.GetComponent<UnityEngine.UI.RawImage>();
        Stretch(raw.rectTransform, Vector2.zero, Vector2.one);
        raw.raycastTarget = false;
        var overlayGo = new GameObject("Overlay", typeof(RectTransform));
        overlayGo.transform.SetParent(maskImg.transform, false);
        Stretch((RectTransform)overlayGo.transform, Vector2.zero, Vector2.one);
        var frame = Img("Frame", widget, UI("map_frame"), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
        Stretch(frame.rectTransform, Vector2.zero, Vector2.one);
        frame.preserveAspect = false;
        var tip = Label(widget, "Tip", "tap map", 18, Color.white, TextAnchor.UpperCenter, true);
        tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        tip.rectTransform.anchoredPosition = new Vector2(0, -6);
        tip.rectTransform.sizeDelta = new Vector2(160, 26);
        var map = widgetGo.AddComponent<PawMiniMap>();
        map.pet = pet; map.mapImage = raw; map.overlay = (RectTransform)overlayGo.transform; map.widget = widget; map.backdrop = dim.gameObject;
        map.petIcon = UI("icon_cat"); map.targetIcon = UI("slot_bag");
        map.fishIcon = UI("icon_fish"); map.canIcon = UI("slot_can"); map.yarnIcon = UI("slot_yarn");

        // view toggle (paw button under the map) + sound toggle (left of the map)
        // CAT VIEW / TOP VIEW toggle (paw button under the map)
        var view = Img("ViewButton", root, UI("btn_paw"), new Vector2(1, 1), new Vector2(-100, -415), new Vector2(132, 132), true);
        view.gameObject.AddComponent<UnityEngine.UI.Button>();
        var vl = Label(view.transform, "Label", "TOP\nVIEW", 21, Color.white, TextAnchor.UpperCenter, true);
        vl.rectTransform.anchorMin = vl.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        vl.rectTransform.anchoredPosition = new Vector2(0, -26);
        vl.rectTransform.sizeDelta = new Vector2(140, 56);
        var vb = view.gameObject.AddComponent<PawViewButton>();
        vb.followCamera = follow; vb.label = vl;

        var snd = Img("SoundButton", root, UI("btn_sound_on"), new Vector2(1, 1), new Vector2(-510, -72), new Vector2(104, 104), true);
        var sab = snd.gameObject.AddComponent<PawActionButton>();
        sab.action = PawActionButton.Action.Sound;
        sab.soundOn = UI("btn_sound_on"); sab.soundOff = UI("btn_sound_off");

        // HUD logic
        var hud = canvasGo.AddComponent<PawHud>();
        hud.questTitle = qt; hud.questSub = qs; hud.questCard = card.rectTransform;
        hud.energyFill = fill; hud.energyTrack = track;
        hud.bagButton = bagBtn; hud.inventoryPanel = inv.gameObject; hud.slotIcons = icons; hud.slotButtons = btns;
        hud.fishSlot = UI("slot_fish"); hud.canSlot = UI("slot_can"); hud.yarnSlot = UI("slot_yarn");
        hud.toast = toast;

        var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
    }

    // ------------------------------------------------------------------ utils
    static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }
}
