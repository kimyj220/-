using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Unity.AI.Navigation;

public static class GallerySceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GalleryPrototype.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialFolder = "Assets/Materials";
    private const int ArtworkSelectableLayer = 6;

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static readonly Dictionary<string, Transform> Rooms = new Dictionary<string, Transform>();

    private readonly struct PlayerBuildSnapshot
    {
        public PlayerBuildSnapshot(
            bool hasSnapshot,
            Vector3 position,
            Quaternion rotation,
            float moveSpeed,
            float sprintSpeed,
            float jumpHeight,
            float gravity,
            float groundSnapForce,
            float cameraPivotLocalY,
            float mouseSensitivity,
            float minPitch,
            float maxPitch,
            float fieldOfView,
            bool lockCursorOnStart)
        {
            HasSnapshot = hasSnapshot;
            Position = position;
            Rotation = rotation;
            MoveSpeed = moveSpeed;
            SprintSpeed = sprintSpeed;
            JumpHeight = jumpHeight;
            Gravity = gravity;
            GroundSnapForce = groundSnapForce;
            CameraPivotLocalY = cameraPivotLocalY;
            MouseSensitivity = mouseSensitivity;
            MinPitch = minPitch;
            MaxPitch = maxPitch;
            FieldOfView = fieldOfView;
            LockCursorOnStart = lockCursorOnStart;
        }

        public bool HasSnapshot { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float MoveSpeed { get; }

        public float SprintSpeed { get; }

        public float JumpHeight { get; }

        public float Gravity { get; }

        public float GroundSnapForce { get; }

        public float CameraPivotLocalY { get; }

        public float MouseSensitivity { get; }

        public float MinPitch { get; }

        public float MaxPitch { get; }

        public float FieldOfView { get; }

        public bool LockCursorOnStart { get; }
    }

    /// <summary>
    /// Holds a single ordered route point for the generated path markers.
    /// </summary>
    private readonly struct RouteMarkerPoint
    {
        public RouteMarkerPoint(string name, Vector3 position)
        {
            Name = name;
            Position = position;
        }

        public string Name { get; }

        public Vector3 Position { get; }
    }

    [MenuItem("Gallery/Build Prototype Scene")]
    public static void BuildGalleryPrototypeScene()
    {
        BuildGalleryPrototypeSceneInternal();
    }

    public static void BuildGalleryPrototypeSceneBatch()
    {
        BuildGalleryPrototypeSceneInternal();
    }

    [MenuItem("Gallery/Merge Prototype Systems Into SampleScene")]
    public static void MergePrototypeSystemsIntoSampleScene()
    {
        MergePrototypeSystemsIntoSampleSceneInternal();
    }

    public static void MergePrototypeSystemsIntoSampleSceneBatch()
    {
        MergePrototypeSystemsIntoSampleSceneInternal();
    }

    [MenuItem("Gallery/Tune SampleScene Player View")]
    public static void TuneSampleScenePlayerView()
    {
        TuneSampleScenePlayerViewInternal();
    }

    public static void TuneSampleScenePlayerViewBatch()
    {
        TuneSampleScenePlayerViewInternal();
    }

    public static void BuildGalleryPrototypeSceneInternal()
    {
        EnsureFolders();
        CreateMaterials();
        PlayerBuildSnapshot playerSnapshot = CaptureExistingPlayerSnapshot();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "GalleryPrototype";

        GameObject root = new GameObject("GalleryRoot");
        Rooms.Clear();

        CreateRoomRoots(root.transform);
        BuildArchitecture(root.transform);
        BuildRoomObjects();
        BuildLighting(root.transform);
        Transform player = BuildPlayer(root.transform, playerSnapshot);
        EnsureCuratorRuntimeComponents(root.transform);
        WireAICuratorFollowers(root.transform, player);
        BuildZoneFlow(root.transform, player);
        BuildRouteUI(root.transform);
        BuildArtworkPopupUI(root.transform);
        BuildArtworkSelectorSystem(root.transform);
        BuildCuratorSubtitleUI(root.transform);
        BuildCuratorDocentSystems(root.transform);
        BuildViewReturnControllerSystem(root.transform);
        BuildMediaSystems(root.transform);
        BuildReactiveLightingSystem(root.transform);
        BuildNavigationSurface(root.transform);
        BuildNavigationMarkers(root.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Gallery prototype scene built at {ScenePath}");
    }

    public static void MergePrototypeSystemsIntoSampleSceneInternal()
    {
        EnsureFolders();
        CreateMaterials();

        if (!File.Exists(SampleScenePath))
        {
            throw new FileNotFoundException($"Sample scene not found: {SampleScenePath}");
        }

        Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        GameObject galleryRootObject = FindGameObjectByName(sampleScene.GetRootGameObjects(), "GalleryRoot");
        if (galleryRootObject == null)
        {
            throw new MissingReferenceException("GalleryRoot was not found in SampleScene. Merge cannot continue.");
        }

        Transform galleryRoot = galleryRootObject.transform;
        PlayerBuildSnapshot playerSnapshot = CaptureScenePlayerSnapshot(galleryRoot);

        RemoveExistingPrototypeSystems(galleryRoot);

        Transform player = BuildPlayer(galleryRoot, playerSnapshot);
        EnsureCuratorRuntimeComponents(galleryRoot);
        WireAICuratorFollowers(galleryRoot, player);
        BuildZoneFlow(galleryRoot, player);
        BuildRouteUI(galleryRoot);
        BuildArtworkPopupUI(galleryRoot);
        BuildArtworkSelectorSystem(galleryRoot);
        BuildCuratorSubtitleUI(galleryRoot);
        BuildCuratorDocentSystems(galleryRoot);
        BuildViewReturnControllerSystem(galleryRoot);
        BuildMediaSystems(galleryRoot);
        BuildReactiveLightingSystem(galleryRoot);
        BuildNavigationSurface(galleryRoot);
        BuildNavigationMarkers(galleryRoot);

        EditorSceneManager.SaveScene(sampleScene, SampleScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Prototype systems merged into {SampleScenePath}");
    }

    public static void TuneSampleScenePlayerViewInternal()
    {
        if (!File.Exists(SampleScenePath))
        {
            throw new FileNotFoundException($"Sample scene not found: {SampleScenePath}");
        }

        Scene sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        GameObject galleryRootObject = FindGameObjectByName(sampleScene.GetRootGameObjects(), "GalleryRoot");
        if (galleryRootObject == null)
        {
            throw new MissingReferenceException("GalleryRoot was not found in SampleScene. Player tuning cannot continue.");
        }

        Transform galleryRoot = galleryRootObject.transform;
        GameObject playerObject = FindNamedChild(galleryRoot, "Player");
        if (playerObject == null)
        {
            playerObject = FindNamedChild(galleryRoot, "Player_FirstPerson_Gallery");
        }

        if (playerObject == null)
        {
            throw new MissingReferenceException("Player was not found in SampleScene. Player tuning cannot continue.");
        }

        HO_FirstPersonCamera firstPersonCamera = playerObject.GetComponent<HO_FirstPersonCamera>();
        if (firstPersonCamera == null)
        {
            throw new MissingComponentException("HO_FirstPersonCamera was not found on the SampleScene player.");
        }

        float currentSensitivity = ReadFloatField(firstPersonCamera, "mouseSensitivity", 1.0f);
        AssignFloatValue(firstPersonCamera, "mouseSensitivity", Mathf.Max(0.01f, currentSensitivity * 3.0f));

        Transform cameraPivot = playerObject.transform.Find("PlayerCameraPivot");
        if (cameraPivot == null)
        {
            throw new MissingReferenceException("PlayerCameraPivot was not found under the SampleScene player.");
        }

        Vector3 localPosition = cameraPivot.localPosition;
        localPosition.y += 3.0f;
        cameraPivot.localPosition = localPosition;

        EditorSceneManager.SaveScene(sampleScene, SampleScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"SampleScene player view tuned at {SampleScenePath}");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets/Scripts", "Runtime");
        EnsureFolder("Assets/Scripts", "Editor");
        EnsureFolder("Assets", "Materials");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void CreateMaterials()
    {
        Materials.Clear();
        AddMaterial("M_Floor_LightConcrete", new Color(0.72f, 0.70f, 0.66f));
        AddMaterial("M_Floor_WarmConcrete", new Color(0.62f, 0.58f, 0.51f));
        AddMaterial("M_Floor_DarkCharcoal", new Color(0.055f, 0.058f, 0.062f));
        AddMaterial("M_Floor_Stone", new Color(0.42f, 0.43f, 0.42f));
        AddMaterial("M_Rug_SoftAmber", new Color(0.55f, 0.36f, 0.29f));
        AddMaterial("M_Wall_GalleryWhite", new Color(0.86f, 0.85f, 0.80f));
        AddMaterial("M_Wall_DarkGraphite", new Color(0.075f, 0.075f, 0.083f));
        AddMaterial("M_Wood_Walnut", new Color(0.34f, 0.22f, 0.14f));
        AddMaterial("M_Wood_Light", new Color(0.58f, 0.43f, 0.27f));
        AddMaterial("M_Metal_Brushed", new Color(0.55f, 0.55f, 0.52f), 0.75f, 0.26f);
        AddMaterial("M_Metal_Dark", new Color(0.11f, 0.11f, 0.12f), 0.65f, 0.32f);
        AddMaterial("M_Frame_Black", new Color(0.015f, 0.014f, 0.013f));
        AddMaterial("M_Canvas_White", new Color(0.93f, 0.91f, 0.85f));
        AddMaterial("M_Canvas_RedField", new Color(0.52f, 0.055f, 0.045f));
        AddMaterial("M_Canvas_Blue", new Color(0.06f, 0.19f, 0.42f));
        AddMaterial("M_Canvas_Yellow", new Color(0.96f, 0.75f, 0.21f));
        AddMaterial("M_Canvas_Teal", new Color(0.04f, 0.48f, 0.46f));
        AddMaterial("M_Canvas_Pink", new Color(0.83f, 0.36f, 0.48f));
        AddMaterial("M_Glass_Clear", new Color(0.70f, 0.92f, 1.0f, 0.28f), 0.0f, 0.02f, true);
        AddMaterial("M_Plant_Leaf", new Color(0.08f, 0.32f, 0.17f));
        AddMaterial("M_Plant_Pot", new Color(0.28f, 0.22f, 0.18f));
        AddMaterial("M_Upholstery_Burgundy", new Color(0.34f, 0.06f, 0.10f));
        AddMaterial("M_Upholstery_Grey", new Color(0.32f, 0.34f, 0.34f));
        AddMaterial("M_Emissive_Blue", new Color(0.05f, 0.34f, 0.85f), 0.0f, 0.3f, false, new Color(0.02f, 0.28f, 1.0f) * 2.2f);
        AddMaterial("M_Emissive_Video", new Color(0.06f, 0.14f, 0.25f), 0.0f, 0.2f, false, new Color(0.0f, 0.45f, 1.0f) * 2.5f);
        AddMaterial("M_Emissive_Exit", new Color(0.1f, 0.55f, 0.32f), 0.0f, 0.2f, false, new Color(0.05f, 1.0f, 0.42f) * 2.0f);
        AddMaterial("M_Panel_Cork", new Color(0.64f, 0.45f, 0.28f));
    }

    private static void AddMaterial(string name, Color color, float metallic = 0.0f, float smoothness = 0.45f, bool transparent = false, Color? emission = null)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetColor(material, color);
        SetFloatIfPresent(material, "_Metallic", metallic);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_Glossiness", smoothness);

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission.Value);
            }
        }

        if (transparent)
        {
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        Materials[name] = material;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void CreateRoomRoots(Transform root)
    {
        AddRoom(root, "Room_01_Lobby");
        AddRoom(root, "Room_02_Time_Corridor");
        AddRoom(root, "Room_03_Abstract_Color_Field");
        AddRoom(root, "Room_04_Media_Extension");
        AddRoom(root, "Room_05_Curator_Focus");
        AddRoom(root, "Room_06_Reflection_Lounge");
        AddRoom(root, "Central_Deck_Stairs_Rails");
    }

    private static void AddRoom(Transform root, string name)
    {
        GameObject room = new GameObject(name);
        room.transform.SetParent(root);
        Rooms[name] = room.transform;
    }

    private static void BuildArchitecture(Transform root)
    {
        BuildFloors();
        BuildWalls();
        BuildGlassPortals();
        BuildDeckStairsAndRails();
        BuildCeilingGuides(root);
    }

    private static void BuildFloors()
    {
        CreateCube("FLOOR_01_LOBBY", new Vector3(0, -0.04f, 0), new Vector3(11.0f, 0.08f, 7.5f), Mat("M_Floor_LightConcrete"), Rooms["Room_01_Lobby"]);
        CreateCube("FLOOR_02_CORRIDOR", new Vector3(-5.8f, 0.56f, 8.6f), new Vector3(15.5f, 0.08f, 6.0f), Mat("M_Floor_WarmConcrete"), Rooms["Room_02_Time_Corridor"]);
        CreateCube("FLOOR_03_ABSTRACT", new Vector3(5.0f, 0.56f, 13.8f), new Vector3(13.5f, 0.08f, 10.0f), Mat("M_Floor_WarmConcrete"), Rooms["Room_03_Abstract_Color_Field"]);
        CreateCube("FLOOR_04_MEDIA", new Vector3(17.0f, 0.56f, 13.4f), new Vector3(10.0f, 0.08f, 8.0f), Mat("M_Floor_DarkCharcoal"), Rooms["Room_04_Media_Extension"]);
        CreateCube("FLOOR_05_CURATOR", new Vector3(14.0f, 0.56f, 6.3f), new Vector3(9.0f, 0.08f, 6.0f), Mat("M_Floor_WarmConcrete"), Rooms["Room_05_Curator_Focus"]);
        CreateCube("FLOOR_06_LOUNGE", new Vector3(15.2f, 0.56f, 0.2f), new Vector3(9.0f, 0.08f, 7.0f), Mat("M_Floor_WarmConcrete"), Rooms["Room_06_Reflection_Lounge"]);
        CreateCube("FLOOR_CENTRAL_DECK", new Vector3(5.0f, 0.56f, 5.8f), new Vector3(9.0f, 0.08f, 6.0f), Mat("M_Floor_WarmConcrete"), Rooms["Central_Deck_Stairs_Rails"]);
        CreateCube("FLOOR_ENTRY_PORCH", new Vector3(-2.8f, -0.49f, -6.2f), new Vector3(6.0f, 0.08f, 2.0f), Mat("M_Floor_Stone"), Rooms["Room_01_Lobby"]);
        CreateCube("FLOOR_EXIT_PORCH", new Vector3(20.8f, 0.11f, -1.2f), new Vector3(3.0f, 0.08f, 3.2f), Mat("M_Floor_Stone"), Rooms["Room_06_Reflection_Lounge"]);
    }

    private static void BuildWalls()
    {
        Transform lobby = Rooms["Room_01_Lobby"];
        CreateCube("W01_LOBBY_SOUTH_L", new Vector3(-4.2f, 1.8f, -3.9f), new Vector3(2.6f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lobby);
        CreateCube("W01_LOBBY_SOUTH_R", new Vector3(2.2f, 1.8f, -3.9f), new Vector3(5.0f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lobby);
        CreateCube("W01_LOBBY_WEST", new Vector3(-5.65f, 1.8f, -0.6f), new Vector3(0.35f, 3.6f, 6.6f), Mat("M_Wall_GalleryWhite"), lobby);
        CreateCube("W01_LOBBY_EAST_LOW", new Vector3(5.65f, 1.2f, -1.0f), new Vector3(0.35f, 2.4f, 5.4f), Mat("M_Wall_GalleryWhite"), lobby);
        CreateCube("W01_LOBBY_NORTH_L_OPENED", new Vector3(-1.35f, 1.8f, 3.8f), new Vector3(2.7f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lobby);
        CreateCube("W01_LOBBY_NORTH_R_OPENED", new Vector3(1.45f, 1.8f, 3.8f), new Vector3(1.9f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lobby);

        Transform corridor = Rooms["Room_02_Time_Corridor"];
        CreateCube("W02_CORRIDOR_WEST", new Vector3(-13.55f, 2.4f, 8.6f), new Vector3(0.35f, 3.6f, 6.0f), Mat("M_Wall_GalleryWhite"), corridor);
        CreateCube("W02_CORRIDOR_NORTH_OPENED", new Vector3(-7.6f, 2.4f, 11.75f), new Vector3(11.9f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), corridor);
        CreateCube("W02_CORRIDOR_SOUTH_A", new Vector3(-10.4f, 2.4f, 5.45f), new Vector3(6.3f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), corridor);
        CreateCube("W02_CORRIDOR_SOUTH_B", new Vector3(-2.2f, 2.4f, 5.45f), new Vector3(3.4f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), corridor);
        CreateCube("W02_CORRIDOR_EAST_PART", new Vector3(2.05f, 2.4f, 8.2f), new Vector3(0.35f, 3.6f, 3.2f), Mat("M_Wall_GalleryWhite"), corridor);

        Transform abstractRoom = Rooms["Room_03_Abstract_Color_Field"];
        CreateCube("W03_ABSTRACT_NORTH", new Vector3(5.0f, 2.6f, 18.95f), new Vector3(13.5f, 4.0f, 0.35f), Mat("M_Wall_GalleryWhite"), abstractRoom);
        CreateCube("W03_ABSTRACT_WEST_A", new Vector3(-1.9f, 2.6f, 15.6f), new Vector3(0.35f, 4.0f, 6.6f), Mat("M_Wall_GalleryWhite"), abstractRoom);
        CreateCube("W03_ABSTRACT_WEST_B", new Vector3(-1.9f, 2.6f, 10.0f), new Vector3(0.35f, 4.0f, 1.8f), Mat("M_Wall_GalleryWhite"), abstractRoom);
        CreateCube("W03_ABSTRACT_EAST_A", new Vector3(11.9f, 2.6f, 16.2f), new Vector3(0.35f, 4.0f, 5.1f), Mat("M_Wall_GalleryWhite"), abstractRoom);
        CreateCube("W03_ABSTRACT_EAST_B", new Vector3(11.9f, 2.6f, 10.2f), new Vector3(0.35f, 4.0f, 1.7f), Mat("M_Wall_GalleryWhite"), abstractRoom);
        CreateCube("W03_ABSTRACT_SOUTH_A", new Vector3(2.0f, 2.6f, 8.65f), new Vector3(5.6f, 4.0f, 0.35f), Mat("M_Wall_GalleryWhite"), abstractRoom);
        CreateCube("W03_ABSTRACT_SOUTH_B_OPENED", new Vector3(8.15f, 2.6f, 8.65f), new Vector3(1.7f, 4.0f, 0.35f), Mat("M_Wall_GalleryWhite"), abstractRoom);

        Transform media = Rooms["Room_04_Media_Extension"];
        CreateCube("W04_MEDIA_NORTH", new Vector3(17.0f, 2.5f, 17.6f), new Vector3(10.0f, 3.8f, 0.35f), Mat("M_Wall_DarkGraphite"), media);
        CreateCube("W04_MEDIA_EAST", new Vector3(22.15f, 2.5f, 13.4f), new Vector3(0.35f, 3.8f, 8.0f), Mat("M_Wall_DarkGraphite"), media);
        CreateCube("W04_MEDIA_SOUTH_A_L", new Vector3(12.65f, 2.5f, 9.2f), new Vector3(1.3f, 3.8f, 0.35f), Mat("M_Wall_DarkGraphite"), media);
        CreateCube("W04_MEDIA_SOUTH_A_R", new Vector3(16.25f, 2.5f, 9.2f), new Vector3(1.5f, 3.8f, 0.35f), Mat("M_Wall_DarkGraphite"), media);
        CreateCube("W04_MEDIA_SOUTH_B", new Vector3(20.4f, 2.5f, 9.2f), new Vector3(3.6f, 3.8f, 0.35f), Mat("M_Wall_DarkGraphite"), media);
        CreateCube("W04_MEDIA_WEST_A", new Vector3(12.0f, 2.5f, 16.0f), new Vector3(0.35f, 3.8f, 3.2f), Mat("M_Wall_DarkGraphite"), media);
        CreateCube("W04_MEDIA_WEST_B", new Vector3(12.0f, 2.5f, 10.6f), new Vector3(0.35f, 3.8f, 2.4f), Mat("M_Wall_DarkGraphite"), media);

        Transform curator = Rooms["Room_05_Curator_Focus"];
        CreateCube("W05_CURATOR_EAST", new Vector3(18.65f, 2.4f, 6.3f), new Vector3(0.35f, 3.6f, 6.0f), Mat("M_Wall_GalleryWhite"), curator);
        CreateCube("W05_CURATOR_SOUTH_A", new Vector3(12.4f, 2.4f, 3.15f), new Vector3(3.5f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), curator);
        CreateCube("W05_CURATOR_SOUTH_B", new Vector3(17.6f, 2.4f, 3.15f), new Vector3(2.1f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), curator);
        CreateCube("W05_CURATOR_WEST", new Vector3(9.35f, 2.4f, 6.3f), new Vector3(0.35f, 3.6f, 6.0f), Mat("M_Wall_GalleryWhite"), curator);

        Transform lounge = Rooms["Room_06_Reflection_Lounge"];
        CreateCube("W06_LOUNGE_EAST_N", new Vector3(19.85f, 2.4f, 1.7f), new Vector3(0.35f, 3.6f, 4.0f), Mat("M_Wall_GalleryWhite"), lounge);
        CreateCube("W06_LOUNGE_EAST_S", new Vector3(19.85f, 2.4f, -2.95f), new Vector3(0.35f, 3.6f, 1.0f), Mat("M_Wall_GalleryWhite"), lounge);
        CreateCube("W06_LOUNGE_SOUTH", new Vector3(15.2f, 2.4f, -3.45f), new Vector3(9.0f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lounge);
        CreateCube("W06_LOUNGE_WEST_A", new Vector3(10.55f, 2.4f, -1.2f), new Vector3(0.35f, 3.6f, 4.4f), Mat("M_Wall_GalleryWhite"), lounge);
        CreateCube("W06_LOUNGE_WEST_B", new Vector3(10.55f, 2.4f, 2.55f), new Vector3(0.35f, 3.6f, 1.1f), Mat("M_Wall_GalleryWhite"), lounge);
        CreateCube("W06_LOUNGE_NORTH_A", new Vector3(13.0f, 2.4f, 3.85f), new Vector3(4.2f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lounge);
        CreateCube("W06_LOUNGE_NORTH_B", new Vector3(18.0f, 2.4f, 3.85f), new Vector3(2.8f, 3.6f, 0.35f), Mat("M_Wall_GalleryWhite"), lounge);
    }

    private static void BuildGlassPortals()
    {
        Transform lobby = Rooms["Room_01_Lobby"];
        CreateCube("P_ENTRY_GLASS_LEFT_DOOR", new Vector3(-2.75f, 1.2f, -3.78f), new Vector3(1.25f, 2.4f, 0.04f), Mat("M_Glass_Clear"), lobby, false);
        CreateCube("P_ENTRY_GLASS_RIGHT_DOOR", new Vector3(-1.45f, 1.2f, -3.78f), new Vector3(1.25f, 2.4f, 0.04f), Mat("M_Glass_Clear"), lobby, false);
        CreateCube("P_ENTRY_METAL_HEADER", new Vector3(-2.1f, 2.48f, -3.74f), new Vector3(2.9f, 0.08f, 0.12f), Mat("M_Metal_Brushed"), lobby, false);

        Transform media = Rooms["Room_04_Media_Extension"];
        CreateCube("P_ABSTRACT_TO_MEDIA_DARK_PORTAL_HEADER", new Vector3(12.0f, 2.95f, 13.7f), new Vector3(0.18f, 0.18f, 3.2f), Mat("M_Metal_Dark"), media, false);

        Transform lounge = Rooms["Room_06_Reflection_Lounge"];
        CreateCube("P_LOUNGE_EXIT_GLASS", new Vector3(19.72f, 1.2f, -1.2f), new Vector3(0.04f, 2.4f, 2.2f), Mat("M_Glass_Clear"), lounge, false);
    }

    private static void BuildDeckStairsAndRails()
    {
        Transform deck = Rooms["Central_Deck_Stairs_Rails"];
        CreateCube("DECK_LOW_WALL_WEST", new Vector3(0.45f, 1.15f, 5.8f), new Vector3(0.25f, 1.1f, 5.2f), Mat("M_Wall_GalleryWhite"), deck);
        CreateCube("DECK_LOW_WALL_SOUTH", new Vector3(5.0f, 1.15f, 2.75f), new Vector3(8.6f, 1.1f, 0.25f), Mat("M_Wall_GalleryWhite"), deck);

        CreateStair("STAIR_LOBBY_TO_DECK", new Vector3(4.2f, 0.0f, 3.0f), Vector3.forward, 4, 0.15f, 0.35f, 2.4f, deck);
        CreateStair("STAIR_CORRIDOR_SMALL", new Vector3(-4.0f, 0.0f, 3.5f), Vector3.forward, 4, 0.15f, 0.35f, 2.4f, deck);
        CreateStair("STAIR_EXIT", new Vector3(20.6f, 0.6f, -1.2f), Vector3.right, 3, 0.15f, 0.35f, 2.4f, deck);

        CreateRail("RAIL_DECK_SOUTH", new Vector3(5.0f, 1.15f, 2.85f), new Vector3(8.0f, 0.08f, 0.08f), deck);
        CreateRail("RAIL_DECK_EAST", new Vector3(9.45f, 1.15f, 5.7f), new Vector3(0.08f, 0.08f, 4.8f), deck);
        CreateRail("RAIL_ABSTRACT_SOUTH", new Vector3(5.0f, 1.15f, 8.35f), new Vector3(7.0f, 0.08f, 0.08f), deck);
        CreateRail("RAIL_LOUNGE_WEST", new Vector3(10.75f, 1.15f, -0.4f), new Vector3(0.08f, 0.08f, 3.2f), deck);
        CreateRail("RAIL_EXIT_PORCH", new Vector3(20.8f, 1.05f, 0.35f), new Vector3(2.8f, 0.08f, 0.08f), deck);
    }

    private static void CreateStair(string name, Vector3 start, Vector3 direction, int count, float riser, float tread, float width, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);

        Vector3 dir = direction.normalized;
        for (int i = 0; i < count; i++)
        {
            float height = riser * (i + 1);
            Vector3 center = start + dir * (tread * i + tread * 0.5f);
            center.y += height * 0.5f;
            Vector3 size = Mathf.Abs(dir.x) > 0.5f
                ? new Vector3(tread, height, width)
                : new Vector3(width, height, tread);
            CreateCube($"{name}_STEP_{i + 1:00}", center, size, Mat("M_Floor_WarmConcrete"), root.transform);
        }
    }

    private static void CreateRail(string name, Vector3 center, Vector3 size, Transform parent)
    {
        CreateCube(name, center, size, Mat("M_Metal_Brushed"), parent);

        bool xRail = size.x > size.z;
        float length = xRail ? size.x : size.z;
        int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.2f) + 1);
        for (int i = 0; i < posts; i++)
        {
            float t = posts == 1 ? 0.0f : (float)i / (posts - 1) - 0.5f;
            Vector3 pos = center;
            if (xRail)
            {
                pos.x += t * length;
            }
            else
            {
                pos.z += t * length;
            }

            pos.y = 0.67f;
            CreateCube($"{name}_POST_{i + 1:00}", pos, new Vector3(0.06f, 1.0f, 0.06f), Mat("M_Metal_Brushed"), parent);
        }
    }

    private static void BuildCeilingGuides(Transform root)
    {
        CreateCube("OBJ_02_SPOTLIGHT_TRACK", new Vector3(-6.0f, 3.45f, 11.35f), new Vector3(10.0f, 0.05f, 0.05f), Mat("M_Metal_Dark"), Rooms["Room_02_Time_Corridor"], false);
        CreateCube("OBJ_03_SPOT_RING_N", new Vector3(5.0f, 3.7f, 15.9f), new Vector3(2.7f, 0.04f, 0.04f), Mat("M_Metal_Dark"), Rooms["Room_03_Abstract_Color_Field"], false);
        CreateCube("OBJ_03_SPOT_RING_S", new Vector3(5.0f, 3.7f, 11.1f), new Vector3(2.7f, 0.04f, 0.04f), Mat("M_Metal_Dark"), Rooms["Room_03_Abstract_Color_Field"], false);
        CreateCube("OBJ_03_SPOT_RING_E", new Vector3(7.4f, 3.7f, 13.5f), new Vector3(0.04f, 0.04f, 2.7f), Mat("M_Metal_Dark"), Rooms["Room_03_Abstract_Color_Field"], false);
        CreateCube("OBJ_03_SPOT_RING_W", new Vector3(2.6f, 3.7f, 13.5f), new Vector3(0.04f, 0.04f, 2.7f), Mat("M_Metal_Dark"), Rooms["Room_03_Abstract_Color_Field"], false);
    }

    private static void BuildRoomObjects()
    {
        BuildLobby();
        BuildCorridor();
        BuildAbstractRoom();
        BuildMediaRoom();
        BuildCuratorZone();
        BuildLounge();
    }

    private static void BuildLobby()
    {
        Transform room = Rooms["Room_01_Lobby"];
        GameObject desk = CreateCube("OBJ_01_RECEPTION_DESK", new Vector3(-2.2f, 0.55f, 1.9f), new Vector3(3.2f, 1.1f, 0.8f), Mat("M_Wood_Light"), room);
        CreateCube("OBJ_01_RECEPTION_DESK_TOP", new Vector3(-2.2f, 1.12f, 1.9f), new Vector3(3.35f, 0.08f, 0.95f), Mat("M_Metal_Brushed"), room, false);
        AddInteractable(desk, "Lobby Reception", "Start here. Follow the blue floor line through the gallery sequence: lobby, time corridor, abstract room, media room, curator focus, and reflection lounge.", GalleryInteractableType.InfoPanel);

        CreateLamp("OBJ_01_DESK_LAMP", new Vector3(-1.0f, 1.2f, 1.9f), room);
        CreateAICuratorAvatar("OBJ_01_CURATOR", new Vector3(-0.6f, 0.0f, 2.1f), 200.0f, Mat("M_Upholstery_Grey"), room, "Curator - Welcome", "Welcome. This prototype maps the exhibition path as a playable first-person gallery. Use WASD or arrow keys to move, mouse to look, and E or click to inspect works.", GalleryInteractableType.Curator);

        GameObject panel = CreateWallPanel("OBJ_01_INTRO_PANEL", new Vector3(-5.45f, 1.55f, 0.8f), new Vector3(0.06f, 1.8f, 2.4f), 90.0f, Mat("M_Panel_Cork"), room);
        AddInteractable(panel, "Exhibition Introduction", "Theme: from modern painting into spatial, media, and participatory forms. The route is linear but each room gives a different viewing rhythm.", GalleryInteractableType.InfoPanel);
        AddText("TEXT_01_INTRO_PANEL", "BOUNDARIES\nAND MODERN VISION", new Vector3(-5.50f, 2.1f, 0.8f), Quaternion.Euler(0, 90, 0), 0.16f, room);

        CreatePlant("OBJ_01_PLANT", new Vector3(-4.8f, 0.0f, -2.8f), room);
        CreateGuideStrip("OBJ_01_GUIDE_LINE_A", new Vector3(-2.6f, 0.03f, -1.0f), new Vector3(0.08f, 0.03f, 3.4f), room);
        CreateGuideStrip("OBJ_01_GUIDE_LINE_B", new Vector3(-3.55f, 0.33f, 3.2f), new Vector3(0.08f, 0.03f, 1.6f), room);
    }

    private static void BuildCorridor()
    {
        Transform room = Rooms["Room_02_Time_Corridor"];
        GameObject art1 = CreateArtwork("ART_001_FRAME", new Vector3(-12.95f, 2.0f, 7.2f), new Vector3(0.08f, 1.5f, 2.0f), 90.0f, Mat("M_Canvas_Blue"), room);
        AddInteractable(art1, "ART-001 Initial Surface", "A virtual early-modern painting study. Look for flat structure, restrained marks, and blue tonal fields that begin the timeline.", GalleryInteractableType.Artwork);

        GameObject art2 = CreateArtwork("ART_002_FRAME", new Vector3(-12.95f, 2.0f, 9.8f), new Vector3(0.08f, 1.5f, 2.0f), 90.0f, Mat("M_Canvas_Yellow"), room);
        AddInteractable(art2, "ART-002 Fragmented Figure", "Fragmented planes and multiple viewpoints introduce the shift from representation to constructed perception.", GalleryInteractableType.Artwork);

        CreateCube("OBJ_02_TIMELINE_STRIP", new Vector3(-12.92f, 1.15f, 8.6f), new Vector3(0.04f, 0.08f, 5.0f), Mat("M_Emissive_Blue"), room, false);

        GameObject archive = CreateWallPanel("OBJ_02_ARCHIVE_WALL", new Vector3(-6.0f, 2.0f, 11.55f), new Vector3(8.0f, 1.8f, 0.06f), 0.0f, Mat("M_Canvas_White"), room);
        AddInteractable(archive, "Archive Wall", "A grid of small image placeholders acts as an archive layer, giving the corridor a historical reading rhythm.", GalleryInteractableType.InfoPanel);
        for (int x = 0; x < 6; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                Vector3 pos = new Vector3(-9.2f + x * 1.25f, 1.65f + y * 0.55f, 11.50f);
                CreateCube($"OBJ_02_ARCHIVE_TILE_{x}_{y}", pos, new Vector3(0.75f, 0.38f, 0.03f), (x + y) % 2 == 0 ? Mat("M_Canvas_Teal") : Mat("M_Canvas_Pink"), room, false);
            }
        }

        CreateBench("OBJ_02_BENCH", new Vector3(-4.6f, 0.95f, 6.6f), 2.4f, 0.55f, room);
    }

    private static void BuildAbstractRoom()
    {
        Transform room = Rooms["Room_03_Abstract_Color_Field"];
        GameObject red = CreateArtwork("ART_003_RED_FIELD", new Vector3(7.2f, 2.35f, 18.74f), new Vector3(4.2f, 2.1f, 0.08f), 180.0f, Mat("M_Canvas_RedField"), room);
        AddInteractable(red, "ART-003 Red Horizontal", "A large color-field canvas. Step back and notice how scale changes the room more than the image itself.", GalleryInteractableType.Artwork);

        GameObject sideA = CreateArtwork("ART_003_SIDE_PANEL_A", new Vector3(1.8f, 2.2f, 18.74f), new Vector3(2.2f, 1.9f, 0.08f), 180.0f, Mat("M_Canvas_Teal"), room);
        AddInteractable(sideA, "Side Panel A", "A supporting color study that frames the main canvas and balances the north wall.", GalleryInteractableType.Artwork);

        GameObject sideB = CreateArtwork("ART_003_SIDE_PANEL_B", new Vector3(10.6f, 2.2f, 15.8f), new Vector3(0.08f, 1.8f, 1.4f), 270.0f, Mat("M_Canvas_Pink"), room);
        AddInteractable(sideB, "Side Panel B", "A vertical color accent on the east wall, marking the route toward the media room.", GalleryInteractableType.Artwork);

        CreateCube("ART_004_SCULPTURE_BASE", new Vector3(5.0f, 0.95f, 13.5f), new Vector3(3.0f, 0.7f, 1.6f), Mat("M_Wood_Walnut"), room);
        GameObject sculpture = new GameObject("ART_004_FLOATING_METAL");
        sculpture.transform.SetParent(room);
        sculpture.transform.position = new Vector3(5.0f, 1.75f, 13.5f);
        sculpture.transform.rotation = Quaternion.Euler(0, 25.0f, 0);
        CreateCylinder("ART_004_FLOATING_METAL_BAR_A", Vector3.zero, 0.08f, 2.2f, Mat("M_Metal_Brushed"), sculpture.transform, Quaternion.Euler(0, 0, 72));
        CreateCylinder("ART_004_FLOATING_METAL_BAR_B", Vector3.zero, 0.08f, 1.8f, Mat("M_Metal_Brushed"), sculpture.transform, Quaternion.Euler(0, 90, 18));
        CreateSphere("ART_004_FLOATING_METAL_NODE_A", new Vector3(-0.75f, 0.35f, 0.25f), new Vector3(0.42f, 0.42f, 0.42f), Mat("M_Metal_Brushed"), sculpture.transform);
        CreateSphere("ART_004_FLOATING_METAL_NODE_B", new Vector3(0.65f, -0.25f, -0.2f), new Vector3(0.55f, 0.55f, 0.55f), Mat("M_Metal_Brushed"), sculpture.transform);
        RotatingExhibit rotator = sculpture.AddComponent<RotatingExhibit>();
        rotator.degreesPerSecond = 18.0f;
        GalleryInteractable interactable = AddInteractable(sculpture, "ART-004 Buoyant Metal", "A primitive-built kinetic sculpture. Interact to toggle its rotation and inspect the changing silhouette.", GalleryInteractableType.Sculpture);
        UnityEventTools.AddPersistentListener(interactable.onInteract, rotator.ToggleRotation);

        GameObject wallText = CreateWallPanel("OBJ_03_WALL_TEXT", new Vector3(-1.72f, 1.8f, 12.5f), new Vector3(0.06f, 1.2f, 2.1f), 90.0f, Mat("M_Panel_Cork"), room);
        AddInteractable(wallText, "Abstract Room Notes", "The room opens around the central sculpture so visitors can orbit it. Color fields on the walls set the slower viewing tempo.", GalleryInteractableType.InfoPanel);
    }

    private static void BuildMediaRoom()
    {
        Transform room = Rooms["Room_04_Media_Extension"];
        GameObject videoWall = CreateWallPanel("ART_005_VIDEO_WALL", new Vector3(17.0f, 2.15f, 17.42f), new Vector3(7.2f, 2.9f, 0.08f), 180.0f, Mat("M_Emissive_Video"), room);
        AddInteractable(videoWall, "ART-005 Signal Room", "A placeholder video wall. The emissive surface and low light establish the black-box media-room mood.", GalleryInteractableType.VideoWall);

        CreateCube("OBJ_04_PROJECTOR", new Vector3(17.0f, 3.25f, 10.3f), new Vector3(0.65f, 0.28f, 0.45f), Mat("M_Metal_Dark"), room, false);
        GameObject sensor = CreateCube("OBJ_04_SENSOR_PAD", new Vector3(17.0f, 0.62f, 12.3f), new Vector3(3.0f, 0.03f, 1.6f), Mat("M_Emissive_Blue"), room, false);
        AddInteractable(sensor, "Interactive Sensor Pad", "A floor sensor placeholder for future reactive video or sound cues.", GalleryInteractableType.InfoPanel);

        GameObject audioL = CreateSpeaker("ART_006_AUDIO_POINT_L", new Vector3(13.2f, 1.4f, 12.0f), room);
        AddInteractable(audioL, "ART-006 Audio Archive L", "Left audio point. Future build can attach spatial audio here with a six-meter listening radius.", GalleryInteractableType.AudioPoint);

        GameObject audioR = CreateSpeaker("ART_006_AUDIO_POINT_R", new Vector3(20.8f, 1.4f, 12.0f), room);
        AddInteractable(audioR, "ART-006 Audio Archive R", "Right audio point. Stereo placement gives the dark room a directional sound stage.", GalleryInteractableType.AudioPoint);

        CreateBench("OBJ_04_BENCH_A", new Vector3(15.5f, 0.85f, 11.0f), 2.4f, 0.45f, room);
        CreateBench("OBJ_04_BENCH_B", new Vector3(18.8f, 0.85f, 10.7f), 1.8f, 0.45f, room);
        CreateGuideStrip("OBJ_04_FLOOR_GUIDE", new Vector3(14.2f, 0.64f, 9.5f), new Vector3(3.0f, 0.03f, 0.08f), room);
    }

    private static void BuildCuratorZone()
    {
        Transform room = Rooms["Room_05_Curator_Focus"];
        CreateBookcase("OBJ_05_BOOKCASE", new Vector3(16.6f, 1.45f, 8.95f), room);
        CreateDesk("OBJ_05_DESK", new Vector3(16.6f, 0.95f, 7.9f), room);
        CreateAICuratorAvatar("OBJ_05_CURATOR", new Vector3(14.5f, 0.6f, 7.2f), 220.0f, Mat("M_Upholstery_Burgundy"), room, "Curator - Focus", "This focus zone is for interpretation. The desk, bookcase, memory window, and info panel are arranged as a teaching corner.", GalleryInteractableType.Curator);

        GameObject memory = CreateWallPanel("ART_007_MEMORY_WINDOW", new Vector3(12.0f, 1.65f, 7.1f), new Vector3(1.4f, 1.8f, 0.12f), 90.0f, Mat("M_Glass_Clear"), room);
        AddInteractable(memory, "ART-007 Memory Window", "A transparent installation placeholder. It suggests layered memory, reflection, and annotation.", GalleryInteractableType.Artwork);

        GameObject info = CreateWallPanel("OBJ_05_INFO_PANEL", new Vector3(18.45f, 1.85f, 5.5f), new Vector3(0.06f, 2.0f, 2.8f), 270.0f, Mat("M_Panel_Cork"), room);
        AddInteractable(info, "Curator Information Panel", "A readable panel anchor for longer text, captions, and guided explanation in the next iteration.", GalleryInteractableType.InfoPanel);

        GameObject smallFrame = CreateArtwork("OBJ_05_SMALL_FRAME", new Vector3(10.9f, 1.9f, 6.9f), new Vector3(0.08f, 1.2f, 1.0f), 90.0f, Mat("M_Canvas_Blue"), room);
        AddInteractable(smallFrame, "Small Study Frame", "A compact reference artwork placed opposite the desk to complete the curator corner composition.", GalleryInteractableType.Artwork);
    }

    private static void BuildLounge()
    {
        Transform room = Rooms["Room_06_Reflection_Lounge"];
        CreateCube("OBJ_06_RUG", new Vector3(15.0f, 0.62f, 0.1f), new Vector3(5.2f, 0.03f, 4.0f), Mat("M_Rug_SoftAmber"), room, false);
        CreateSofa("OBJ_06_SOFA_CURVED_A", new Vector3(14.6f, 0.9f, 1.5f), new Vector3(3.2f, 0.7f, 0.85f), 15.0f, room);
        CreateSofa("OBJ_06_SOFA_CURVED_B", new Vector3(16.7f, 0.9f, 0.4f), new Vector3(0.85f, 0.7f, 2.3f), 25.0f, room);
        CreateCylinder("OBJ_06_TABLE_MAIN", new Vector3(15.1f, 0.85f, -0.5f), 0.75f, 0.35f, Mat("M_Wood_Walnut"), room);
        CreateChair("OBJ_06_CHAIR_A", new Vector3(13.4f, 0.85f, -0.7f), 45.0f, room);
        CreateChair("OBJ_06_CHAIR_B", new Vector3(16.4f, 0.85f, -1.0f), -25.0f, room);
        CreatePlant("OBJ_06_PLANT_TALL", new Vector3(11.6f, 0.0f, 1.6f), room, 1.4f);

        GameObject summary = CreateWallPanel("ART_008_SUMMARY_WALL", new Vector3(15.4f, 2.0f, 3.65f), new Vector3(4.2f, 1.8f, 0.06f), 0.0f, Mat("M_Canvas_White"), room);
        AddInteractable(summary, "ART-008 Six Moments", "A six-card summary wall for the route: entrance, timeline, color field, media, curator focus, and reflection.", GalleryInteractableType.LoungePoint);
        for (int i = 0; i < 6; i++)
        {
            float x = 13.55f + i * 0.74f;
            CreateCube($"ART_008_SUMMARY_CARD_{i + 1:00}", new Vector3(x, 2.0f, 3.60f), new Vector3(0.52f, 1.2f, 0.03f), i % 2 == 0 ? Mat("M_Canvas_Teal") : Mat("M_Canvas_Pink"), room, false);
        }

        GameObject exit = CreateWallPanel("OBJ_06_EXIT_SIGN", new Vector3(19.6f, 2.5f, -1.2f), new Vector3(0.06f, 0.4f, 1.0f), 270.0f, Mat("M_Emissive_Exit"), room);
        AddInteractable(exit, "Exit", "End of the prototype route. The exterior porch beyond the glass door marks the completion point.", GalleryInteractableType.Exit);
    }

    private static void BuildLighting(Transform root)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.34f, 0.31f);
        RenderSettings.fog = false;

        GameObject sun = new GameObject("L_DIRECTIONAL_SOFT_SKYLIGHT");
        sun.transform.SetParent(root);
        sun.transform.rotation = Quaternion.Euler(48.0f, -32.0f, 0.0f);
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1.0f, 0.95f, 0.86f);
        sunLight.intensity = 1.15f;

        CreatePointLight("L_01_LOBBY_AMBIENT", new Vector3(0, 3.2f, 0), new Color(1.0f, 0.88f, 0.68f), 450.0f, 8.0f, Rooms["Room_01_Lobby"]);
        CreateSpotLight("L_02_TRACK_A", new Vector3(-10.5f, 3.35f, 11.2f), new Vector3(-12.95f, 2.0f, 7.2f), new Color(1.0f, 0.90f, 0.75f), 520.0f, Rooms["Room_02_Time_Corridor"]);
        CreateSpotLight("L_02_TRACK_B", new Vector3(-5.0f, 3.35f, 11.2f), new Vector3(-6.0f, 2.0f, 11.55f), new Color(1.0f, 0.90f, 0.75f), 500.0f, Rooms["Room_02_Time_Corridor"]);
        CreateSpotLight("L_03_SCULPTURE_KEY", new Vector3(5.0f, 3.8f, 13.5f), new Vector3(5.0f, 1.3f, 13.5f), Color.white, 900.0f, Rooms["Room_03_Abstract_Color_Field"]);
        CreateSpotLight("L_03_CANVAS", new Vector3(7.2f, 3.4f, 16.5f), new Vector3(7.2f, 2.2f, 18.7f), new Color(1.0f, 0.88f, 0.76f), 650.0f, Rooms["Room_03_Abstract_Color_Field"]);
        CreatePointLight("L_04_VIDEO_GLOW", new Vector3(17.0f, 2.1f, 17.25f), new Color(0.1f, 0.42f, 1.0f), 210.0f, 7.0f, Rooms["Room_04_Media_Extension"]);
        CreatePointLight("L_04_FLOOR_GUIDE", new Vector3(14.2f, 0.8f, 9.5f), new Color(0.0f, 0.22f, 1.0f), 120.0f, 4.0f, Rooms["Room_04_Media_Extension"]);
        CreateSpotLight("L_05_CURATOR_SPOT", new Vector3(14.5f, 3.2f, 7.2f), new Vector3(14.5f, 1.2f, 7.2f), new Color(1.0f, 0.88f, 0.70f), 600.0f, Rooms["Room_05_Curator_Focus"]);
        CreatePointLight("L_05_DESK_LAMP", new Vector3(16.0f, 1.45f, 7.8f), new Color(1.0f, 0.58f, 0.28f), 160.0f, 3.4f, Rooms["Room_05_Curator_Focus"]);
        CreatePointLight("L_06_LOUNGE_AMBER", new Vector3(15.2f, 3.0f, 0.2f), new Color(1.0f, 0.64f, 0.36f), 380.0f, 7.0f, Rooms["Room_06_Reflection_Lounge"]);
        CreatePointLight("L_06_EXIT_GUIDE", new Vector3(19.2f, 2.3f, -1.2f), new Color(0.8f, 1.0f, 0.78f), 180.0f, 4.0f, Rooms["Room_06_Reflection_Lounge"]);
    }

    private static Transform BuildPlayer(Transform root, PlayerBuildSnapshot snapshot)
    {
        Vector3 playerPosition = snapshot.HasSnapshot ? snapshot.Position : new Vector3(-2.8f, 0.0f, -3.9f);
        Quaternion playerRotation = snapshot.HasSnapshot ? snapshot.Rotation : Quaternion.Euler(0.0f, 20.0f, 0.0f);
        float moveSpeed = snapshot.HasSnapshot ? snapshot.MoveSpeed : 10.0f;
        float sprintSpeed = snapshot.HasSnapshot ? snapshot.SprintSpeed : 15.0f;
        float jumpHeight = snapshot.HasSnapshot ? snapshot.JumpHeight : 5.0f;
        float gravity = snapshot.HasSnapshot ? snapshot.Gravity : 18.0f;
        float groundSnapForce = snapshot.HasSnapshot ? snapshot.GroundSnapForce : 1.0f;
        float cameraPivotLocalY = snapshot.HasSnapshot ? snapshot.CameraPivotLocalY : 1.65f;
        float mouseSensitivity = snapshot.HasSnapshot ? snapshot.MouseSensitivity : 1.0f;
        float minPitch = snapshot.HasSnapshot ? snapshot.MinPitch : -75.0f;
        float maxPitch = snapshot.HasSnapshot ? snapshot.MaxPitch : 75.0f;
        float fieldOfView = snapshot.HasSnapshot ? snapshot.FieldOfView : 68.0f;
        bool lockCursorOnStart = snapshot.HasSnapshot ? snapshot.LockCursorOnStart : true;

        GameObject player = new GameObject("Player");
        player.transform.SetParent(root);
        player.transform.position = playerPosition;
        player.transform.rotation = playerRotation;

        NavMeshModifier playerNavMeshModifier = player.AddComponent<NavMeshModifier>();
        playerNavMeshModifier.ignoreFromBuild = true;

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.32f;
        controller.center = new Vector3(0.0f, 0.9f, 0.0f);
        controller.stepOffset = 0.35f;
        controller.slopeLimit = 45.0f;

        GameObject cameraPivotObject = new GameObject("PlayerCameraPivot");
        cameraPivotObject.transform.SetParent(player.transform);
        cameraPivotObject.transform.localPosition = new Vector3(0.0f, cameraPivotLocalY, 0.0f);
        cameraPivotObject.transform.localRotation = Quaternion.identity;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(cameraPivotObject.transform);
        cameraObject.transform.localPosition = Vector3.zero;
        cameraObject.transform.localRotation = Quaternion.identity;
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.03f;
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<AudioListener>();

        HO_PlayerMovement movement = player.AddComponent<HO_PlayerMovement>();
        AssignObjectReference(movement, "characterController", controller);
        AssignFloatValue(movement, "moveSpeed", moveSpeed);
        AssignFloatValue(movement, "sprintSpeed", sprintSpeed);
        AssignFloatValue(movement, "jumpHeight", jumpHeight);
        AssignFloatValue(movement, "gravity", gravity);
        AssignFloatValue(movement, "groundSnapForce", groundSnapForce);

        HO_FirstPersonCamera firstPersonCamera = player.AddComponent<HO_FirstPersonCamera>();
        AssignObjectReference(firstPersonCamera, "playerBody", player.transform);
        AssignObjectReference(firstPersonCamera, "cameraPivot", cameraPivotObject.transform);
        AssignFloatValue(firstPersonCamera, "mouseSensitivity", mouseSensitivity);
        AssignFloatValue(firstPersonCamera, "minPitch", minPitch);
        AssignFloatValue(firstPersonCamera, "maxPitch", maxPitch);
        AssignBooleanValue(firstPersonCamera, "lockCursorOnStart", lockCursorOnStart);

        return player.transform;
    }

    /// <summary>
    /// Connects generated AI curator followers to the generated player transform.
    /// </summary>
    private static void WireAICuratorFollowers(Transform root, Transform player)
    {
        HO_AICuratorFollower[] followers = root.GetComponentsInChildren<HO_AICuratorFollower>(true);
        for (int index = 0; index < followers.Length; index++)
        {
            AssignObjectReference(followers[index], "followTarget", player);
        }
    }

    /// <summary>
    /// Ensures existing curator scene objects keep the runtime AI components needed for follow and docent behavior.
    /// </summary>
    private static void EnsureCuratorRuntimeComponents(Transform root)
    {
        EnsureCuratorRuntimeComponent(root, "OBJ_01_CURATOR", "Curator - Welcome", "Welcome. This prototype maps the exhibition path as a playable first-person gallery. Use WASD or arrow keys to move, mouse to look, and E or click to inspect works.");
        EnsureCuratorRuntimeComponent(root, "OBJ_05_CURATOR", "Curator - Focus", "This focus zone is for interpretation. The desk, bookcase, memory window, and info panel are arranged as a teaching corner.");
    }

    /// <summary>
    /// Re-attaches AI avatar and follower behaviors to a curator object that may have been preserved from a user-edited scene.
    /// </summary>
    private static void EnsureCuratorRuntimeComponent(Transform root, string curatorName, string title, string description)
    {
        GameObject curatorObject = FindNamedChild(root, curatorName);
        if (curatorObject == null)
        {
            return;
        }

        AddInteractable(curatorObject, title, description, GalleryInteractableType.Curator);

        NavMeshModifier navMeshModifier = curatorObject.GetComponent<NavMeshModifier>();
        if (navMeshModifier == null)
        {
            navMeshModifier = curatorObject.AddComponent<NavMeshModifier>();
        }

        navMeshModifier.ignoreFromBuild = true;

        NavMeshAgent navMeshAgent = curatorObject.GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            navMeshAgent = curatorObject.AddComponent<NavMeshAgent>();
        }

        navMeshAgent.speed = 3.2f;
        navMeshAgent.acceleration = 8.0f;
        navMeshAgent.angularSpeed = 540.0f;
        navMeshAgent.radius = 0.38f;
        navMeshAgent.height = 1.8f;
        navMeshAgent.baseOffset = 0.0f;
        navMeshAgent.stoppingDistance = 0.05f;
        navMeshAgent.autoBraking = true;
        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
        navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        navMeshAgent.avoidancePriority = 45;

        CapsuleCollider bodyCollider = curatorObject.GetComponent<CapsuleCollider>();
        if (bodyCollider == null)
        {
            bodyCollider = curatorObject.AddComponent<CapsuleCollider>();
        }

        bodyCollider.isTrigger = false;
        bodyCollider.direction = 1;
        bodyCollider.radius = 0.38f;
        bodyCollider.height = 1.8f;
        bodyCollider.center = new Vector3(0.0f, 0.9f, 0.0f);

        Rigidbody bodyRigidbody = curatorObject.GetComponent<Rigidbody>();
        if (bodyRigidbody == null)
        {
            bodyRigidbody = curatorObject.AddComponent<Rigidbody>();
        }

        bodyRigidbody.isKinematic = true;
        bodyRigidbody.useGravity = false;
        bodyRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        bodyRigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        Transform visualRoot = EnsureCuratorVisualRoot(curatorObject.transform);
        Light statusLight = EnsureCuratorStatusLight(visualRoot, curatorName);

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        Object[] rendererReferences = new Object[renderers.Length];
        for (int index = 0; index < renderers.Length; index++)
        {
            rendererReferences[index] = renderers[index];
        }

        HO_AICuratorAvatar avatar = curatorObject.GetComponent<HO_AICuratorAvatar>();
        if (avatar == null)
        {
            avatar = curatorObject.AddComponent<HO_AICuratorAvatar>();
        }

        AssignObjectReference(avatar, "visualRoot", visualRoot);
        AssignObjectReference(avatar, "statusLight", statusLight);
        AssignObjectReferenceArray(avatar, "emissiveRenderers", rendererReferences);
        AssignFloatValue(avatar, "lookRadius", 6.0f);
        AssignFloatValue(avatar, "turnSpeed", 5.0f);
        AssignFloatValue(avatar, "idleBobHeight", 0.025f);
        AssignFloatValue(avatar, "idleBobSpeed", 1.2f);

        HO_AICuratorFollower follower = curatorObject.GetComponent<HO_AICuratorFollower>();
        if (follower == null)
        {
            follower = curatorObject.AddComponent<HO_AICuratorFollower>();
        }

        AssignFloatValue(follower, "followDistance", 3.0f);
        AssignFloatValue(follower, "distanceTolerance", 0.25f);
        AssignFloatValue(follower, "moveSpeed", 3.2f);
        AssignFloatValue(follower, "acceleration", 8.0f);
        AssignFloatValue(follower, "turnSpeed", 540.0f);
        AssignFloatValue(follower, "agentRadius", 0.38f);
        AssignFloatValue(follower, "agentHeight", 1.8f);
        AssignLayerMaskValue(follower, "blockingLayerMask", ~0);
        AssignFloatValue(follower, "collisionSkinWidth", 0.08f);
        AssignFloatValue(follower, "maxManualStepDistance", 0.22f);
    }

    /// <summary>
    /// Wraps existing curator meshes under one visual root so runtime idle motion does not offset navigation components.
    /// </summary>
    private static Transform EnsureCuratorVisualRoot(Transform curatorRoot)
    {
        string visualRootName = $"{curatorRoot.name}_VISUAL";
        Transform visualRoot = curatorRoot.Find(visualRootName);
        if (visualRoot != null)
        {
            return visualRoot;
        }

        GameObject visualRootObject = new GameObject(visualRootName);
        visualRoot = visualRootObject.transform;
        visualRoot.SetParent(curatorRoot);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        List<Transform> directChildren = new List<Transform>();
        for (int index = 0; index < curatorRoot.childCount; index++)
        {
            Transform child = curatorRoot.GetChild(index);
            if (child != visualRoot)
            {
                directChildren.Add(child);
            }
        }

        for (int index = 0; index < directChildren.Count; index++)
        {
            directChildren[index].SetParent(visualRoot, true);
        }

        return visualRoot;
    }

    /// <summary>
    /// Adds a lightweight point light if the preserved curator object did not already include one.
    /// </summary>
    private static Light EnsureCuratorStatusLight(Transform visualRoot, string curatorName)
    {
        Light statusLight = visualRoot.GetComponentInChildren<Light>(true);
        if (statusLight != null)
        {
            return statusLight;
        }

        GameObject statusLightObject = new GameObject($"{curatorName}_STATUS_LIGHT");
        statusLightObject.transform.SetParent(visualRoot);
        statusLightObject.transform.localPosition = new Vector3(0.0f, 1.42f, 0.36f);
        statusLightObject.transform.localRotation = Quaternion.identity;

        statusLight = statusLightObject.AddComponent<Light>();
        statusLight.type = LightType.Point;
        statusLight.color = new Color(0.25f, 0.78f, 1.0f);
        statusLight.intensity = 80.0f;
        statusLight.range = 2.4f;
        return statusLight;
    }

    /// <summary>
    /// Adds and bakes a prototype NavMeshSurface so curator agents can follow without walking through gallery walls.
    /// </summary>
    private static void BuildNavigationSurface(Transform root)
    {
        GameObject surfaceObject = new GameObject("NavMeshSurface_Gallery");
        surfaceObject.transform.SetParent(root);

        NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.defaultArea = 0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = false;

        try
        {
            surface.BuildNavMesh();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Gallery NavMesh build skipped during prototype scene generation: {exception.Message}");
        }
    }

    private static void BuildZoneFlow(Transform root, Transform player)
    {
        GameObject flowObject = new GameObject("GalleryZoneFlow");
        flowObject.transform.SetParent(root);

        HO_GalleryZoneFlow zoneFlow = flowObject.AddComponent<HO_GalleryZoneFlow>();
        HO_GalleryZoneFlow.ZoneDefinition[] zoneDefinitions = zoneFlow.ZoneDefinitions;
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            zoneDefinitions = HO_GalleryZoneFlow.CreateDefaultZoneDefinitions();
        }

        Collider[] zoneColliders = new Collider[zoneDefinitions.Length];
        string[] zoneIds = new string[zoneDefinitions.Length];

        for (int index = 0; index < zoneDefinitions.Length; index++)
        {
            HO_GalleryZoneFlow.ZoneDefinition zone = zoneDefinitions[index];
            zoneIds[index] = zone != null ? zone.id : string.Empty;
            zoneColliders[index] = CreateZoneTrigger(
                GetZoneTriggerObjectName(zone, index),
                zone != null ? zone.triggerCenter : Vector3.zero,
                zone != null ? zone.triggerSize : Vector3.one,
                flowObject.transform);
        }

        AssignObjectReferenceArray(zoneFlow, "zoneTriggers", zoneColliders);
        AssignStringArray(zoneFlow, "zoneIds", zoneIds);
        AssignStringValue(zoneFlow, "defaultZoneId", "LOBBY");
        AssignObjectReference(zoneFlow, "playerTransform", player);
    }

    private static void BuildRouteUI(Transform root)
    {
        GameObject canvasObject = new GameObject("Canvas_RouteUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
        scaler.matchWidthOrHeight = 1.0f;

        HO_MiniRouteUI miniRouteUI = canvasObject.AddComponent<HO_MiniRouteUI>();
        HO_GalleryZoneFlow zoneFlow = FindFirstComponentInChildren<HO_GalleryZoneFlow>(root);
        AssignObjectReference(miniRouteUI, "galleryZoneFlow", zoneFlow);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject panelObject = CreateUiObject("RoutePanel", canvasObject.transform);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1.0f, 1.0f);
        panelRect.anchorMax = new Vector2(1.0f, 1.0f);
        panelRect.pivot = new Vector2(1.0f, 1.0f);
        panelRect.anchoredPosition = new Vector2(-32.0f, -32.0f);
        panelRect.sizeDelta = new Vector2(360.0f, 214.0f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.07f, 0.82f);

        TMP_FontAsset defaultFont = GetOrCreateTmpFontAsset();

        TextMeshProUGUI currentText = CreateTmpLabel("ZoneLabel_Current", panelObject.transform, defaultFont, 28.0f, FontStyles.Bold);
        RectTransform currentRect = currentText.rectTransform;
        currentRect.anchorMin = new Vector2(0.0f, 1.0f);
        currentRect.anchorMax = new Vector2(1.0f, 1.0f);
        currentRect.pivot = new Vector2(0.0f, 1.0f);
        currentRect.anchoredPosition = new Vector2(18.0f, -18.0f);
        currentRect.sizeDelta = new Vector2(-36.0f, 36.0f);

        TextMeshProUGUI nextText = CreateTmpLabel("ZoneLabel_Next", panelObject.transform, defaultFont, 22.0f, FontStyles.Normal);
        RectTransform nextRect = nextText.rectTransform;
        nextRect.anchorMin = new Vector2(0.0f, 1.0f);
        nextRect.anchorMax = new Vector2(1.0f, 1.0f);
        nextRect.pivot = new Vector2(0.0f, 1.0f);
        nextRect.anchoredPosition = new Vector2(18.0f, -58.0f);
        nextRect.sizeDelta = new Vector2(-36.0f, 32.0f);

        TextMeshProUGUI routeHintText = CreateTmpLabel("ZoneLabel_RouteHint", panelObject.transform, defaultFont, 16.0f, FontStyles.Normal);
        RectTransform hintRect = routeHintText.rectTransform;
        hintRect.anchorMin = new Vector2(0.0f, 1.0f);
        hintRect.anchorMax = new Vector2(1.0f, 1.0f);
        hintRect.pivot = new Vector2(0.0f, 1.0f);
        hintRect.anchoredPosition = new Vector2(18.0f, -92.0f);
        hintRect.sizeDelta = new Vector2(-36.0f, 24.0f);
        routeHintText.color = new Color(0.82f, 0.88f, 0.95f, 0.9f);

        GameObject markersRoot = CreateUiObject("ZoneMarkers", panelObject.transform);
        RectTransform markersRect = markersRoot.GetComponent<RectTransform>();
        markersRect.anchorMin = new Vector2(0.0f, 0.0f);
        markersRect.anchorMax = new Vector2(1.0f, 0.0f);
        markersRect.pivot = new Vector2(0.5f, 0.0f);
        markersRect.anchoredPosition = new Vector2(0.0f, 16.0f);
        markersRect.sizeDelta = new Vector2(-24.0f, 64.0f);

        HO_GalleryZoneFlow.ZoneDefinition[] zoneDefinitions = zoneFlow != null ? zoneFlow.ZoneDefinitions : null;
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            zoneDefinitions = HO_GalleryZoneFlow.CreateDefaultZoneDefinitions();
        }
        string[] markerNames = new string[zoneDefinitions.Length];
        GameObject[] markerObjects = new GameObject[markerNames.Length];

        for (int index = 0; index < markerNames.Length; index++)
        {
            markerNames[index] = GetZoneMarkerLabel(zoneDefinitions, index);

            GameObject markerObject = CreateUiObject($"RouteMarker_{index + 1:00}", markersRoot.transform);
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.0f, 0.0f);
            markerRect.anchorMax = new Vector2(0.0f, 1.0f);
            markerRect.pivot = new Vector2(0.0f, 0.5f);
            markerRect.anchoredPosition = new Vector2(index * 54.0f, 0.0f);
            markerRect.sizeDelta = new Vector2(48.0f, 48.0f);

            Image markerImage = markerObject.AddComponent<Image>();
            markerImage.color = new Color(0.35f, 0.38f, 0.44f, 0.88f);

            TextMeshProUGUI markerText = CreateTmpLabel("Label", markerObject.transform, defaultFont, 18.0f, FontStyles.Bold);
            RectTransform markerTextRect = markerText.rectTransform;
            markerTextRect.anchorMin = Vector2.zero;
            markerTextRect.anchorMax = Vector2.one;
            markerTextRect.offsetMin = Vector2.zero;
            markerTextRect.offsetMax = Vector2.zero;
            markerText.text = markerNames[index];
            markerText.alignment = TextAlignmentOptions.Center;

            markerObjects[index] = markerObject;
        }

        AssignObjectReference(miniRouteUI, "currentZoneText", currentText);
        AssignObjectReference(miniRouteUI, "nextZoneText", nextText);
        AssignObjectReference(miniRouteUI, "routeHintText", routeHintText);
        AssignObjectReferenceArray(miniRouteUI, "zoneMarkers", markerObjects);
    }

    private static void BuildArtworkPopupUI(Transform root)
    {
        EnsureEventSystem(root);
        TMP_FontAsset fontAsset = GetOrCreateTmpFontAsset();

        GameObject canvasObject = new GameObject("Canvas_ArtworkPopup", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        HO_ArtworkPopup artworkPopup = canvasObject.AddComponent<HO_ArtworkPopup>();

        GameObject panelObject = CreateUiObject("ArtworkPopupPanel", canvasObject.transform);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1.0f, 1.0f);
        panelRect.anchorMax = new Vector2(1.0f, 1.0f);
        panelRect.pivot = new Vector2(1.0f, 1.0f);
        panelRect.anchoredPosition = new Vector2(-36.0f, -36.0f);
        panelRect.sizeDelta = new Vector2(560.0f, 320.0f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.07f, 0.09f, 0.92f);

        TextMeshProUGUI titleText = CreateTmpLabel("ArtworkTitleText", panelObject.transform, fontAsset, 26.0f, FontStyles.Bold);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.0f, 1.0f);
        titleRect.anchorMax = new Vector2(1.0f, 1.0f);
        titleRect.pivot = new Vector2(0.0f, 1.0f);
        titleRect.offsetMin = new Vector2(24.0f, -56.0f);
        titleRect.offsetMax = new Vector2(-120.0f, -18.0f);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.enableWordWrapping = true;
        titleText.text = "Artwork Title";

        TextMeshProUGUI metaText = CreateTmpLabel("ArtworkMetaText", panelObject.transform, fontAsset, 16.0f, FontStyles.Normal);
        RectTransform metaRect = metaText.rectTransform;
        metaRect.anchorMin = new Vector2(0.0f, 1.0f);
        metaRect.anchorMax = new Vector2(1.0f, 1.0f);
        metaRect.pivot = new Vector2(0.0f, 1.0f);
        metaRect.offsetMin = new Vector2(24.0f, -96.0f);
        metaRect.offsetMax = new Vector2(-24.0f, -66.0f);
        metaText.color = new Color(0.88f, 0.90f, 0.94f, 0.95f);
        metaText.text = "Artwork meta";

        TextMeshProUGUI keywordText = CreateTmpLabel("ArtworkKeywordText", panelObject.transform, fontAsset, 15.0f, FontStyles.Italic);
        RectTransform keywordRect = keywordText.rectTransform;
        keywordRect.anchorMin = new Vector2(0.0f, 1.0f);
        keywordRect.anchorMax = new Vector2(1.0f, 1.0f);
        keywordRect.pivot = new Vector2(0.0f, 1.0f);
        keywordRect.offsetMin = new Vector2(24.0f, -128.0f);
        keywordRect.offsetMax = new Vector2(-24.0f, -98.0f);
        keywordText.color = new Color(0.95f, 0.82f, 0.44f, 1.0f);
        keywordText.text = "Artwork keywords";

        TextMeshProUGUI descriptionText = CreateTmpLabel("ArtworkDescriptionText", panelObject.transform, fontAsset, 17.0f, FontStyles.Normal);
        RectTransform descriptionRect = descriptionText.rectTransform;
        descriptionRect.anchorMin = new Vector2(0.0f, 0.0f);
        descriptionRect.anchorMax = new Vector2(1.0f, 1.0f);
        descriptionRect.offsetMin = new Vector2(24.0f, 24.0f);
        descriptionRect.offsetMax = new Vector2(-24.0f, -144.0f);
        descriptionText.alignment = TextAlignmentOptions.TopLeft;
        descriptionText.enableWordWrapping = true;
        descriptionText.color = new Color(0.94f, 0.94f, 0.94f, 1.0f);
        descriptionText.text = "Artwork description";

        GameObject closeButtonObject = CreateUiObject("CloseButton", panelObject.transform);
        RectTransform closeButtonRect = closeButtonObject.GetComponent<RectTransform>();
        closeButtonRect.anchorMin = new Vector2(1.0f, 1.0f);
        closeButtonRect.anchorMax = new Vector2(1.0f, 1.0f);
        closeButtonRect.pivot = new Vector2(1.0f, 1.0f);
        closeButtonRect.anchoredPosition = new Vector2(-24.0f, -22.0f);
        closeButtonRect.sizeDelta = new Vector2(82.0f, 34.0f);

        Image closeButtonImage = closeButtonObject.AddComponent<Image>();
        closeButtonImage.color = new Color(0.18f, 0.21f, 0.26f, 0.96f);

        Button closeButton = closeButtonObject.AddComponent<Button>();
        closeButton.targetGraphic = closeButtonImage;

        TextMeshProUGUI closeLabel = CreateTmpLabel("CloseButtonLabel", closeButtonObject.transform, fontAsset, 15.0f, FontStyles.Bold);
        RectTransform closeLabelRect = closeLabel.rectTransform;
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.text = "Close";

        AssignObjectReference(artworkPopup, "popupRoot", panelObject);
        AssignObjectReference(artworkPopup, "titleText", titleText);
        AssignObjectReference(artworkPopup, "metaText", metaText);
        AssignObjectReference(artworkPopup, "keywordText", keywordText);
        AssignObjectReference(artworkPopup, "descriptionText", descriptionText);
        AssignObjectReference(artworkPopup, "closeButton", closeButton);

        panelObject.SetActive(false);
    }

    private static void EnsureEventSystem(Transform root)
    {
        if (FindFirstComponentInChildren<EventSystem>(root) != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("UI_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(root);
    }

    private static void BuildArtworkSelectorSystem(Transform root)
    {
        GameObject selectorObject = new GameObject("ArtworkInteractionSystem");
        selectorObject.transform.SetParent(root);

        HO_ArtworkSelector selector = selectorObject.AddComponent<HO_ArtworkSelector>();
        Camera targetCamera = FindFirstComponentInChildren<Camera>(root);
        HO_ArtworkPopup artworkPopup = FindFirstComponentInChildren<HO_ArtworkPopup>(root);

        AssignObjectReference(selector, "targetCamera", targetCamera);
        AssignLayerMaskValue(selector, "artworkLayerMask", 1 << ArtworkSelectableLayer);
        AssignFloatValue(selector, "maxSelectDistance", 6.0f);
        AssignObjectReference(selector, "artworkPopup", artworkPopup);
        AssignStringValue(selector, "selectableTag", string.Empty);
    }

    private static void BuildCuratorSubtitleUI(Transform root)
    {
        EnsureEventSystem(root);
        TMP_FontAsset fontAsset = GetOrCreateTmpFontAsset();

        GameObject canvasObject = new GameObject("Canvas_CuratorSubtitle", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 75;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        HO_CuratorSubtitle curatorSubtitle = canvasObject.AddComponent<HO_CuratorSubtitle>();

        GameObject subtitlePanelObject = CreateUiObject("SubtitlePanel", canvasObject.transform);
        RectTransform subtitlePanelRect = subtitlePanelObject.GetComponent<RectTransform>();
        subtitlePanelRect.anchorMin = new Vector2(0.5f, 0.0f);
        subtitlePanelRect.anchorMax = new Vector2(0.5f, 0.0f);
        subtitlePanelRect.pivot = new Vector2(0.5f, 0.0f);
        subtitlePanelRect.anchoredPosition = new Vector2(0.0f, 54.0f);
        subtitlePanelRect.sizeDelta = new Vector2(920.0f, 160.0f);

        Image subtitlePanelImage = subtitlePanelObject.AddComponent<Image>();
        subtitlePanelImage.color = new Color(0.05f, 0.07f, 0.10f, 0.90f);

        TextMeshProUGUI subtitleText = CreateTmpLabel("SubtitleText", subtitlePanelObject.transform, fontAsset, 24.0f, FontStyles.Normal);
        RectTransform subtitleTextRect = subtitleText.rectTransform;
        subtitleTextRect.anchorMin = new Vector2(0.0f, 0.0f);
        subtitleTextRect.anchorMax = new Vector2(1.0f, 1.0f);
        subtitleTextRect.offsetMin = new Vector2(28.0f, 22.0f);
        subtitleTextRect.offsetMax = new Vector2(-28.0f, -22.0f);
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.enableWordWrapping = true;
        subtitleText.text = "AI Curator Subtitle";

        TextMeshProUGUI interactionHintText = CreateTmpLabel("InteractionHintText", canvasObject.transform, fontAsset, 22.0f, FontStyles.Bold);
        RectTransform interactionHintRect = interactionHintText.rectTransform;
        interactionHintRect.anchorMin = new Vector2(0.5f, 0.0f);
        interactionHintRect.anchorMax = new Vector2(0.5f, 0.0f);
        interactionHintRect.pivot = new Vector2(0.5f, 0.0f);
        interactionHintRect.anchoredPosition = new Vector2(0.0f, 228.0f);
        interactionHintRect.sizeDelta = new Vector2(520.0f, 42.0f);
        interactionHintText.alignment = TextAlignmentOptions.Center;
        interactionHintText.color = new Color(0.86f, 0.94f, 1.0f, 0.96f);
        interactionHintText.text = "E - Ask AI Curator";

        AssignObjectReference(curatorSubtitle, "subtitleRoot", subtitlePanelObject);
        AssignObjectReference(curatorSubtitle, "subtitleText", subtitleText);
        AssignObjectReference(curatorSubtitle, "interactionHintText", interactionHintText);
        AssignFloatValue(curatorSubtitle, "subtitleDisplayTime", 4.5f);
        AssignFloatValue(curatorSubtitle, "interactionHintDistance", 2.0f);

        subtitlePanelObject.SetActive(false);
        interactionHintText.gameObject.SetActive(false);
    }

    private static void BuildCuratorDocentSystems(Transform root)
    {
        HO_GalleryZoneFlow zoneFlow = FindFirstComponentInChildren<HO_GalleryZoneFlow>(root);
        HO_CuratorSubtitle curatorSubtitle = FindFirstComponentInChildren<HO_CuratorSubtitle>(root);
        HO_ArtworkSelector artworkSelector = FindFirstComponentInChildren<HO_ArtworkSelector>(root);
        HO_PlayerMovement playerMovement = FindFirstComponentInChildren<HO_PlayerMovement>(root);
        GameObject curatorObject = FindNamedChild(root, "OBJ_01_CURATOR");
        if (curatorObject == null)
        {
            return;
        }

        HO_CuratorDocent docent = curatorObject.GetComponent<HO_CuratorDocent>();
        if (docent == null)
        {
            docent = curatorObject.AddComponent<HO_CuratorDocent>();
        }

        AssignObjectReference(docent, "galleryZoneFlow", zoneFlow);
        AssignObjectReference(docent, "curatorSubtitle", curatorSubtitle);
        AssignObjectReference(docent, "artworkSelector", artworkSelector);
        AssignObjectReference(docent, "curatorInteractable", curatorObject.GetComponent<GalleryInteractable>());
        AssignObjectReference(docent, "playerTransform", playerMovement != null ? playerMovement.transform : null);
        AssignFloatValue(docent, "curatorInteractionDistance", 2.4f);
        AssignStringValue(docent, "defaultDocentLine", "이 공간의 핵심 흐름을 짧게 정리해 드릴게요.");
        AssignStringValue(docent, "curatorHintMessage", "E - AI 큐레이터와 대화");
        AssignStringValue(docent, "artworkHintMessage", "E - 선택 작품 해설 듣기");
    }

    private static void BuildViewReturnControllerSystem(Transform root)
    {
        GameObject controllerObject = new GameObject("ViewReturnController");
        controllerObject.transform.SetParent(root);

        HO_ViewReturnController viewReturnController = controllerObject.AddComponent<HO_ViewReturnController>();
        HO_ArtworkPopup artworkPopup = FindFirstComponentInChildren<HO_ArtworkPopup>(root);
        HO_CuratorSubtitle curatorSubtitle = FindFirstComponentInChildren<HO_CuratorSubtitle>(root);
        HO_ArtworkSelector artworkSelector = FindFirstComponentInChildren<HO_ArtworkSelector>(root);
        HO_PlayerMovement playerMovement = FindFirstComponentInChildren<HO_PlayerMovement>(root);
        HO_FirstPersonCamera firstPersonCamera = FindFirstComponentInChildren<HO_FirstPersonCamera>(root);

        GameObject[] closablePanels =
        {
            FindNamedChild(root, "ArtworkPopupPanel"),
            FindNamedChild(root, "SubtitlePanel")
        };

        AssignObjectReference(viewReturnController, "artworkPopup", artworkPopup);
        AssignObjectReference(viewReturnController, "curatorSubtitle", curatorSubtitle);
        AssignObjectReference(viewReturnController, "artworkSelector", artworkSelector);
        AssignObjectReferenceArray(viewReturnController, "closablePanels", closablePanels);
        AssignObjectReferenceArray(viewReturnController, "inputLockTargets", new Object[]
        {
            playerMovement,
            firstPersonCamera
        });

        GameObject closeButtonObject = FindNamedChild(root, "CloseButton");
        if (closeButtonObject != null)
        {
            Button closeButton = closeButtonObject.GetComponent<Button>();
            if (closeButton != null)
            {
                UnityEventTools.AddPersistentListener(closeButton.onClick, viewReturnController.HandleCloseButtonPressed);
            }
        }
    }

    private static void BuildMediaSystems(Transform root)
    {
        GameObject mediaWallObject = FindNamedChild(root, "ART_005_VIDEO_WALL");
        if (mediaWallObject == null)
        {
            return;
        }

        Renderer mediaWallRenderer = mediaWallObject.GetComponent<Renderer>();
        HO_GalleryZoneFlow zoneFlow = FindFirstComponentInChildren<HO_GalleryZoneFlow>(root);

        VideoPlayer videoPlayer = mediaWallObject.GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            videoPlayer = mediaWallObject.AddComponent<VideoPlayer>();
        }

        AudioSource audioSource = mediaWallObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = mediaWallObject.AddComponent<AudioSource>();
        }

        if (mediaWallRenderer != null)
        {
            string textureProperty = mediaWallRenderer.sharedMaterial != null && mediaWallRenderer.sharedMaterial.HasProperty("_BaseMap")
                ? "_BaseMap"
                : "_MainTex";

            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = mediaWallRenderer;
            videoPlayer.targetMaterialProperty = textureProperty;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0.0f;
        audioSource.volume = 0.85f;

        HO_MediaVideoPlayer mediaVideoPlayer = mediaWallObject.GetComponent<HO_MediaVideoPlayer>();
        if (mediaVideoPlayer == null)
        {
            mediaVideoPlayer = mediaWallObject.AddComponent<HO_MediaVideoPlayer>();
        }

        AssignObjectReference(mediaVideoPlayer, "videoPlayer", videoPlayer);
        AssignObjectReference(mediaVideoPlayer, "audioSource", audioSource);
        AssignBooleanValue(mediaVideoPlayer, "playOnZoneEnter", true);
        AssignBooleanValue(mediaVideoPlayer, "playOnClick", true);
        AssignObjectReference(mediaVideoPlayer, "galleryZoneFlow", zoneFlow);
        AssignBooleanValue(mediaVideoPlayer, "loopPlayback", true);
        AssignStringValue(mediaVideoPlayer, "mediaZoneId", "MEDIA");
    }

    private static void BuildReactiveLightingSystem(Transform root)
    {
        GameObject lightingDirectorObject = new GameObject("LightingDirector");
        lightingDirectorObject.transform.SetParent(root);

        HO_ReactiveLighting reactiveLighting = lightingDirectorObject.AddComponent<HO_ReactiveLighting>();
        HO_GalleryZoneFlow zoneFlow = FindFirstComponentInChildren<HO_GalleryZoneFlow>(root);
        HO_ArtworkSelector artworkSelector = FindFirstComponentInChildren<HO_ArtworkSelector>(root);
        HO_MediaVideoPlayer[] mediaVideoPlayers = root.GetComponentsInChildren<HO_MediaVideoPlayer>(true);
        Light[] lights = root.GetComponentsInChildren<Light>(true);

        string[] emissiveRendererNames =
        {
            "OBJ_02_TIMELINE_STRIP",
            "ART_005_VIDEO_WALL",
            "OBJ_04_SENSOR_PAD",
            "ART_006_AUDIO_POINT_L_DRIVER",
            "ART_006_AUDIO_POINT_R_DRIVER",
            "OBJ_06_EXIT_SIGN"
        };

        System.Collections.Generic.List<Renderer> emissiveRenderers = new System.Collections.Generic.List<Renderer>();
        for (int index = 0; index < emissiveRendererNames.Length; index++)
        {
            GameObject targetObject = FindNamedChild(root, emissiveRendererNames[index]);
            if (targetObject == null)
            {
                continue;
            }

            Renderer renderer = targetObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                emissiveRenderers.Add(renderer);
            }
        }

        AssignObjectReference(reactiveLighting, "galleryZoneFlow", zoneFlow);
        AssignObjectReference(reactiveLighting, "artworkSelector", artworkSelector);
        AssignObjectReferenceArray(reactiveLighting, "mediaVideoPlayers", mediaVideoPlayers);
        AssignObjectReferenceArray(reactiveLighting, "targetLights", lights);
        AssignObjectReferenceArray(reactiveLighting, "emissiveRenderers", emissiveRenderers.ToArray());
        AssignFloatValue(reactiveLighting, "transitionDuration", 0.75f);
    }

    private static void BuildNavigationMarkers(Transform root)
    {
        GameObject markers = new GameObject("Nav_Path_Markers");
        markers.transform.SetParent(root);

        HO_GalleryZoneFlow zoneFlow = FindFirstComponentInChildren<HO_GalleryZoneFlow>(root);
        HO_GalleryZoneFlow.ZoneDefinition[] zoneDefinitions = zoneFlow != null ? zoneFlow.ZoneDefinitions : null;
        if (zoneDefinitions == null || zoneDefinitions.Length == 0)
        {
            zoneDefinitions = HO_GalleryZoneFlow.CreateDefaultZoneDefinitions();
        }
        List<RouteMarkerPoint> waypoints = CollectOrderedRouteWaypoints(zoneDefinitions);

        for (int index = 0; index < waypoints.Count; index++)
        {
            AddMarker(waypoints[index].Name, waypoints[index].Position, markers.transform);

            if (index > 0)
            {
                AddMarkerSegment($"N_PATH_{index:00}", waypoints[index - 1].Position, waypoints[index].Position, markers.transform);
            }
        }
    }

    /// <summary>
    /// Flattens the ordered zone route data into a single marker path.
    /// </summary>
    private static List<RouteMarkerPoint> CollectOrderedRouteWaypoints(HO_GalleryZoneFlow.ZoneDefinition[] zoneDefinitions)
    {
        List<RouteMarkerPoint> points = new List<RouteMarkerPoint>();
        if (zoneDefinitions == null)
        {
            return points;
        }

        for (int zoneIndex = 0; zoneIndex < zoneDefinitions.Length; zoneIndex++)
        {
            HO_GalleryZoneFlow.ZoneDefinition zone = zoneDefinitions[zoneIndex];
            if (zone == null)
            {
                continue;
            }

            if (zone.routeWaypoints != null && zone.routeWaypoints.Length > 0)
            {
                for (int waypointIndex = 0; waypointIndex < zone.routeWaypoints.Length; waypointIndex++)
                {
                    HO_GalleryZoneFlow.RouteWaypoint waypoint = zone.routeWaypoints[waypointIndex];
                    if (waypoint == null)
                    {
                        continue;
                    }

                    AddRouteMarkerPoint(points, BuildWaypointName(zone, waypoint, zoneIndex, waypointIndex), waypoint.position);
                }
                continue;
            }

            AddRouteMarkerPoint(points, GetZoneMarkerLabel(zone, zoneIndex), zone.triggerCenter);
        }

        return points;
    }

    /// <summary>
    /// Adds a route point when it is distinct from the last point in the sequence.
    /// </summary>
    private static void AddRouteMarkerPoint(List<RouteMarkerPoint> points, string name, Vector3 position)
    {
        if (points.Count > 0 && Vector3.Distance(points[points.Count - 1].Position, position) <= 0.05f)
        {
            return;
        }

        points.Add(new RouteMarkerPoint(name, position));
    }

    /// <summary>
    /// Builds a readable waypoint name from the ordered zone metadata.
    /// </summary>
    private static string BuildWaypointName(HO_GalleryZoneFlow.ZoneDefinition zone, HO_GalleryZoneFlow.RouteWaypoint waypoint, int zoneIndex, int waypointIndex)
    {
        if (waypoint != null && !string.IsNullOrWhiteSpace(waypoint.name))
        {
            return waypoint.name;
        }

        string zoneLabel = GetZoneMarkerLabel(zone, zoneIndex);
        if (!string.IsNullOrWhiteSpace(zoneLabel))
        {
            return $"{zoneLabel}_{waypointIndex + 1:00}";
        }

        return $"N_ROUTE_{zoneIndex + 1:00}_{waypointIndex + 1:00}";
    }

    /// <summary>
    /// Builds a stable GameObject name for one generated gallery zone trigger.
    /// </summary>
    private static string GetZoneTriggerObjectName(HO_GalleryZoneFlow.ZoneDefinition zone, int zoneIndex)
    {
        string zoneId = zone != null && !string.IsNullOrWhiteSpace(zone.id)
            ? zone.id
            : $"ZONE_{zoneIndex + 1:00}";

        return $"ZoneTrigger_{zoneId}";
    }

    /// <summary>
    /// Returns a compact label for UI marker chips.
    /// </summary>
    private static string GetZoneMarkerLabel(HO_GalleryZoneFlow.ZoneDefinition[] zoneDefinitions, int zoneIndex)
    {
        if (zoneDefinitions == null || zoneIndex < 0 || zoneIndex >= zoneDefinitions.Length)
        {
            return $"Z{zoneIndex + 1:00}";
        }

        return GetZoneMarkerLabel(zoneDefinitions[zoneIndex], zoneIndex);
    }

    /// <summary>
    /// Returns a compact label for UI marker chips.
    /// </summary>
    private static string GetZoneMarkerLabel(HO_GalleryZoneFlow.ZoneDefinition zone, int zoneIndex)
    {
        if (zone != null)
        {
            if (!string.IsNullOrWhiteSpace(zone.id))
            {
                return zone.id;
            }

            if (!string.IsNullOrWhiteSpace(zone.displayName))
            {
                return zone.displayName.Length > 10 ? zone.displayName.Substring(0, 10) : zone.displayName;
            }
        }

        return $"Z{zoneIndex + 1:00}";
    }

    private static void AddMarker(string name, Vector3 position, Transform parent)
    {
        GameObject marker = CreateCylinder(name, position + new Vector3(0.0f, 0.08f, 0.0f), 0.24f, 0.045f, Mat("M_Emissive_Blue"), parent);
        marker.GetComponent<Collider>().enabled = false;
    }

    private static void AddMarkerSegment(string name, Vector3 start, Vector3 end, Transform parent)
    {
        Vector3 flatDirection = end - start;
        flatDirection.y = 0.0f;

        float length = flatDirection.magnitude;
        if (length <= 0.05f)
        {
            return;
        }

        Vector3 center = (start + end) * 0.5f + new Vector3(0.0f, 0.065f, 0.0f);
        GameObject segment = CreateCube(name, center, new Vector3(0.28f, 0.03f, length), Mat("M_Emissive_Blue"), parent, false);
        segment.transform.localRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
    }

    private static GameObject CreateArtwork(string name, Vector3 center, Vector3 size, float yaw, Material canvasMaterial, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);

        CreateCube($"{name}_BACKING", Vector3.zero, size, canvasMaterial, root.transform);

        Vector3 frameLongA = size;
        Vector3 frameLongB = size;
        Vector3 frameShortA = size;
        Vector3 frameShortB = size;

        if (size.x < size.z)
        {
            frameLongA = new Vector3(size.x + 0.04f, 0.08f, size.z + 0.18f);
            frameLongB = frameLongA;
            frameShortA = new Vector3(size.x + 0.05f, size.y + 0.18f, 0.08f);
            frameShortB = frameShortA;
            CreateCube($"{name}_FRAME_TOP", new Vector3(0, size.y * 0.5f + 0.06f, 0), frameLongA, Mat("M_Frame_Black"), root.transform, false);
            CreateCube($"{name}_FRAME_BOTTOM", new Vector3(0, -size.y * 0.5f - 0.06f, 0), frameLongB, Mat("M_Frame_Black"), root.transform, false);
            CreateCube($"{name}_FRAME_LEFT", new Vector3(0, 0, -size.z * 0.5f - 0.06f), frameShortA, Mat("M_Frame_Black"), root.transform, false);
            CreateCube($"{name}_FRAME_RIGHT", new Vector3(0, 0, size.z * 0.5f + 0.06f), frameShortB, Mat("M_Frame_Black"), root.transform, false);
        }
        else
        {
            frameLongA = new Vector3(size.x + 0.18f, 0.08f, size.z + 0.04f);
            frameLongB = frameLongA;
            frameShortA = new Vector3(0.08f, size.y + 0.18f, size.z + 0.05f);
            frameShortB = frameShortA;
            CreateCube($"{name}_FRAME_TOP", new Vector3(0, size.y * 0.5f + 0.06f, 0), frameLongA, Mat("M_Frame_Black"), root.transform, false);
            CreateCube($"{name}_FRAME_BOTTOM", new Vector3(0, -size.y * 0.5f - 0.06f, 0), frameLongB, Mat("M_Frame_Black"), root.transform, false);
            CreateCube($"{name}_FRAME_LEFT", new Vector3(-size.x * 0.5f - 0.06f, 0, 0), frameShortA, Mat("M_Frame_Black"), root.transform, false);
            CreateCube($"{name}_FRAME_RIGHT", new Vector3(size.x * 0.5f + 0.06f, 0, 0), frameShortB, Mat("M_Frame_Black"), root.transform, false);
        }

        return root;
    }

    private static GameObject CreateWallPanel(string name, Vector3 center, Vector3 size, float yaw, Material material, Transform parent)
    {
        GameObject panel = CreateCube(name, center, size, material, parent);
        panel.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
        return panel;
    }

    private static void CreateLamp(string name, Vector3 position, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        CreateCylinder($"{name}_STEM", new Vector3(0, 0.2f, 0), 0.035f, 0.4f, Mat("M_Metal_Brushed"), root.transform);
        CreateSphere($"{name}_SHADE", new Vector3(0, 0.44f, 0), new Vector3(0.35f, 0.18f, 0.35f), Mat("M_Emissive_Exit"), root.transform);
        CreatePointLight($"{name}_LIGHT", position + Vector3.up * 0.45f, new Color(1.0f, 0.65f, 0.32f), 65.0f, 2.3f, parent);
    }

    private static void CreatePlant(string name, Vector3 position, Transform parent, float leafHeight = 0.8f)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        CreateCylinder($"{name}_POT", new Vector3(0, 0.25f, 0), 0.28f, 0.5f, Mat("M_Plant_Pot"), root.transform);
        CreateCylinder($"{name}_STEM", new Vector3(0, 0.75f, 0), 0.05f, 0.9f, Mat("M_Plant_Leaf"), root.transform);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60.0f;
            GameObject leaf = CreateCube($"{name}_LEAF_{i + 1:00}", new Vector3(0, 0.85f + leafHeight * 0.15f, 0), new Vector3(0.08f, leafHeight, 0.32f), Mat("M_Plant_Leaf"), root.transform, false);
            leaf.transform.localRotation = Quaternion.Euler(22.0f, angle, 0.0f);
            leaf.transform.localPosition += leaf.transform.forward * 0.22f;
        }
    }

    private static void CreateGuideStrip(string name, Vector3 center, Vector3 size, Transform parent)
    {
        CreateCube(name, center, size, Mat("M_Emissive_Blue"), parent, false);
    }

    private static void CreateBench(string name, Vector3 center, float length, float depth, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        CreateCube($"{name}_SEAT", Vector3.zero, new Vector3(length, 0.16f, depth), Mat("M_Wood_Walnut"), root.transform);
        CreateCube($"{name}_LEG_L", new Vector3(-length * 0.38f, -0.32f, 0), new Vector3(0.12f, 0.55f, 0.12f), Mat("M_Metal_Dark"), root.transform);
        CreateCube($"{name}_LEG_R", new Vector3(length * 0.38f, -0.32f, 0), new Vector3(0.12f, 0.55f, 0.12f), Mat("M_Metal_Dark"), root.transform);
    }

    private static GameObject CreateSpeaker(string name, Vector3 center, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        CreateCylinder($"{name}_BODY", Vector3.zero, 0.22f, 0.6f, Mat("M_Metal_Dark"), root.transform);
        CreateSphere($"{name}_DRIVER", new Vector3(0, 0.18f, -0.18f), new Vector3(0.22f, 0.22f, 0.06f), Mat("M_Emissive_Blue"), root.transform);
        return root;
    }

    private static void CreateBookcase(string name, Vector3 center, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        CreateCube($"{name}_BACK", Vector3.zero, new Vector3(2.8f, 1.9f, 0.25f), Mat("M_Wood_Walnut"), root.transform);
        for (int i = 0; i < 4; i++)
        {
            CreateCube($"{name}_SHELF_{i + 1:00}", new Vector3(0, -0.75f + i * 0.5f, -0.18f), new Vector3(2.85f, 0.06f, 0.42f), Mat("M_Wood_Light"), root.transform, false);
        }

        for (int i = 0; i < 16; i++)
        {
            float x = -1.18f + (i % 8) * 0.34f;
            float y = -0.52f + (i / 8) * 0.5f;
            CreateCube($"{name}_BOOK_{i + 1:00}", new Vector3(x, y, -0.42f), new Vector3(0.18f, 0.38f, 0.12f), i % 3 == 0 ? Mat("M_Canvas_Blue") : Mat("M_Canvas_RedField"), root.transform, false);
        }
    }

    private static void CreateDesk(string name, Vector3 center, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        CreateCube($"{name}_TOP", Vector3.zero, new Vector3(2.4f, 0.12f, 0.75f), Mat("M_Wood_Light"), root.transform);
        CreateCube($"{name}_LEFT_SIDE", new Vector3(-1.0f, -0.36f, 0), new Vector3(0.12f, 0.72f, 0.65f), Mat("M_Wood_Walnut"), root.transform);
        CreateCube($"{name}_RIGHT_SIDE", new Vector3(1.0f, -0.36f, 0), new Vector3(0.12f, 0.72f, 0.65f), Mat("M_Wood_Walnut"), root.transform);
    }

    private static void CreateSofa(string name, Vector3 center, Vector3 size, float yaw, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);
        CreateCube($"{name}_SEAT", Vector3.zero, new Vector3(size.x, size.y * 0.45f, size.z), Mat("M_Upholstery_Burgundy"), root.transform);
        CreateCube($"{name}_BACK", new Vector3(0, size.y * 0.35f, size.z * 0.42f), new Vector3(size.x, size.y, 0.18f), Mat("M_Upholstery_Burgundy"), root.transform);
    }

    private static void CreateChair(string name, Vector3 center, float yaw, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);
        CreateCube($"{name}_SEAT", Vector3.zero, new Vector3(0.8f, 0.18f, 0.8f), Mat("M_Upholstery_Grey"), root.transform);
        CreateCube($"{name}_BACK", new Vector3(0, 0.36f, 0.34f), new Vector3(0.8f, 0.7f, 0.12f), Mat("M_Upholstery_Grey"), root.transform);
    }

    /// <summary>
    /// Builds a physical AI curator avatar from primitive meshes and wires its prototype interaction metadata.
    /// </summary>
    private static void CreateAICuratorAvatar(string name, Vector3 position, float yaw, Material bodyMaterial, Transform parent, string title, string description, GalleryInteractableType type)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);

        NavMeshModifier navMeshModifier = root.AddComponent<NavMeshModifier>();
        navMeshModifier.ignoreFromBuild = true;

        GameObject visualRootObject = new GameObject($"{name}_VISUAL");
        visualRootObject.transform.SetParent(root.transform);
        visualRootObject.transform.localPosition = Vector3.zero;
        visualRootObject.transform.localRotation = Quaternion.identity;
        Transform visualRoot = visualRootObject.transform;

        CreateCuratorFollowCapsule($"{name}_FOLLOW_CAPSULE", root.transform);

        CreateCylinder($"{name}_BASE", new Vector3(0.0f, 0.05f, 0.0f), 0.44f, 0.10f, Mat("M_Metal_Dark"), visualRoot);
        GameObject floorRing = CreateCylinder($"{name}_FLOOR_SIGNAL", new Vector3(0.0f, 0.11f, 0.0f), 0.56f, 0.025f, Mat("M_Emissive_Blue"), visualRoot);
        CreateCube($"{name}_LEG_L", new Vector3(-0.11f, 0.48f, 0.0f), new Vector3(0.12f, 0.76f, 0.13f), Mat("M_Metal_Dark"), visualRoot);
        CreateCube($"{name}_LEG_R", new Vector3(0.11f, 0.48f, 0.0f), new Vector3(0.12f, 0.76f, 0.13f), Mat("M_Metal_Dark"), visualRoot);
        CreateCylinder($"{name}_BODY", new Vector3(0.0f, 1.02f, 0.0f), 0.29f, 0.92f, bodyMaterial, visualRoot);
        CreateCube($"{name}_COLLAR", new Vector3(0.0f, 1.48f, 0.03f), new Vector3(0.42f, 0.08f, 0.22f), Mat("M_Metal_Brushed"), visualRoot, false);
        CreateSphere($"{name}_HEAD", new Vector3(0.0f, 1.72f, 0.0f), new Vector3(0.38f, 0.42f, 0.38f), Mat("M_Canvas_White"), visualRoot);
        GameObject visor = CreateCube($"{name}_AI_VISOR", new Vector3(0.0f, 1.73f, 0.22f), new Vector3(0.34f, 0.08f, 0.055f), Mat("M_Emissive_Blue"), visualRoot, false);
        GameObject core = CreateSphere($"{name}_CHEST_CORE", new Vector3(0.0f, 1.16f, 0.29f), new Vector3(0.18f, 0.18f, 0.06f), Mat("M_Emissive_Blue"), visualRoot);
        CreateCube($"{name}_ARM_L", new Vector3(-0.36f, 1.08f, 0.02f), new Vector3(0.10f, 0.66f, 0.10f), Mat("M_Metal_Brushed"), visualRoot);
        CreateCube($"{name}_ARM_R", new Vector3(0.36f, 1.08f, 0.02f), new Vector3(0.10f, 0.66f, 0.10f), Mat("M_Metal_Brushed"), visualRoot);
        GameObject promptPanel = CreateCube($"{name}_HOLOGRAM_PANEL", new Vector3(0.0f, 1.15f, 0.52f), new Vector3(0.78f, 0.42f, 0.035f), Mat("M_Glass_Clear"), visualRoot, false);
        CreateCylinder($"{name}_HALO_DISC", new Vector3(0.0f, 2.02f, 0.0f), 0.23f, 0.025f, Mat("M_Emissive_Blue"), visualRoot, Quaternion.Euler(90.0f, 0.0f, 0.0f));

        GameObject statusLightObject = new GameObject($"{name}_STATUS_LIGHT");
        statusLightObject.transform.SetParent(visualRoot);
        statusLightObject.transform.localPosition = new Vector3(0.0f, 1.42f, 0.36f);
        Light statusLight = statusLightObject.AddComponent<Light>();
        statusLight.type = LightType.Point;
        statusLight.color = new Color(0.25f, 0.78f, 1.0f);
        statusLight.intensity = 80.0f;
        statusLight.range = 2.4f;

        HO_AICuratorAvatar avatar = root.AddComponent<HO_AICuratorAvatar>();
        AssignObjectReference(avatar, "visualRoot", visualRoot);
        AssignObjectReference(avatar, "statusLight", statusLight);
        AssignObjectReferenceArray(avatar, "emissiveRenderers", new Object[]
        {
            floorRing.GetComponent<Renderer>(),
            visor.GetComponent<Renderer>(),
            core.GetComponent<Renderer>(),
            promptPanel.GetComponent<Renderer>()
        });

        HO_AICuratorFollower follower = root.AddComponent<HO_AICuratorFollower>();
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        agent.radius = 0.38f;
        agent.height = 1.8f;
        agent.speed = 3.2f;
        agent.acceleration = 8.0f;
        agent.angularSpeed = 540.0f;
        agent.updatePosition = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        CapsuleCollider bodyCollider = root.GetComponent<CapsuleCollider>();
        bodyCollider.center = new Vector3(0.0f, 0.9f, 0.0f);
        bodyCollider.radius = 0.38f;
        bodyCollider.height = 1.8f;
        Rigidbody bodyRigidbody = root.GetComponent<Rigidbody>();
        bodyRigidbody.isKinematic = true;
        bodyRigidbody.useGravity = false;
        bodyRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        AssignFloatValue(follower, "followDistance", 3.0f);
        AssignFloatValue(follower, "distanceTolerance", 0.25f);
        AssignFloatValue(follower, "moveSpeed", 3.2f);
        AssignFloatValue(follower, "acceleration", 8.0f);
        AssignFloatValue(follower, "turnSpeed", 540.0f);
        AssignFloatValue(follower, "agentRadius", 0.38f);
        AssignFloatValue(follower, "agentHeight", 1.8f);
        AssignLayerMaskValue(follower, "blockingLayerMask", ~0);
        AssignFloatValue(follower, "collisionSkinWidth", 0.08f);
        AssignFloatValue(follower, "maxManualStepDistance", 0.22f);

        AddInteractable(root, title, description, type);
    }

    /// <summary>
    /// Creates the requested capsule child as a visible movement body reference and removes its collider.
    /// </summary>
    private static GameObject CreateCuratorFollowCapsule(string name, Transform parent)
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = name;
        capsule.transform.SetParent(parent);
        capsule.transform.localPosition = new Vector3(0.0f, 0.9f, 0.0f);
        capsule.transform.localRotation = Quaternion.identity;
        capsule.transform.localScale = new Vector3(0.76f, 0.9f, 0.76f);
        capsule.GetComponent<Renderer>().sharedMaterial = Mat("M_Glass_Clear");

        Collider capsuleCollider = capsule.GetComponent<Collider>();
        if (capsuleCollider != null)
        {
            Object.DestroyImmediate(capsuleCollider);
        }

        return capsule;
    }

    private static void AddText(string name, string text, Vector3 position, Quaternion rotation, float size, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = rotation;
        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 42;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.black;
    }

    private static GalleryInteractable AddInteractable(GameObject target, string title, string description, GalleryInteractableType type)
    {
        GalleryInteractable interactable = target.GetComponent<GalleryInteractable>();
        if (interactable == null)
        {
            interactable = target.AddComponent<GalleryInteractable>();
        }

        interactable.title = title;
        interactable.description = description;
        interactable.interactableType = type;
        interactable.highlightColor = type == GalleryInteractableType.VideoWall ? new Color(0.25f, 0.55f, 1.0f) : new Color(1.0f, 0.76f, 0.25f);
        interactable.interactionRange = type == GalleryInteractableType.VideoWall ? 6.0f : 4.5f;

        if (IsArtworkSelectableType(type))
        {
            SetLayerRecursively(target, ArtworkSelectableLayer);
        }

        return interactable;
    }

    private static bool IsArtworkSelectableType(GalleryInteractableType type)
    {
        switch (type)
        {
            case GalleryInteractableType.Artwork:
            case GalleryInteractableType.Sculpture:
            case GalleryInteractableType.VideoWall:
            case GalleryInteractableType.AudioPoint:
                return true;
            default:
                return false;
        }
    }

    private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateSpotLight(string name, Vector3 position, Vector3 target, Color color, float intensity, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = 8.0f;
        light.spotAngle = 42.0f;
        light.innerSpotAngle = 24.0f;
        light.shadows = LightShadows.Soft;
    }

    private static GameObject CreateCube(string name, Vector3 center, Vector3 size, Material material, Transform parent, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;

        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = collider;
        }

        if (collider && IsNavigationWallBlocker(name))
        {
            AddNavigationWallObstacle(go);
        }

        return go;
    }

    /// <summary>
    /// Marks generated walls and rails as navigation blockers so curator agents cannot path through them.
    /// </summary>
    private static bool IsNavigationWallBlocker(string name)
    {
        return name.StartsWith("W", System.StringComparison.Ordinal) ||
            name.StartsWith("RAIL_", System.StringComparison.Ordinal) ||
            name.StartsWith("DECK_LOW_WALL", System.StringComparison.Ordinal) ||
            name.Contains("_WALL", System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Adds a carving NavMesh obstacle matching the cube's local bounds for runtime wall avoidance.
    /// </summary>
    private static void AddNavigationWallObstacle(GameObject target)
    {
        NavMeshObstacle obstacle = target.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = target.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingMoveThreshold = 0.05f;
        obstacle.carvingTimeToStationary = 0.1f;
    }

    private static BoxCollider CreateZoneTrigger(string name, Vector3 center, Vector3 size, Transform parent)
    {
        GameObject triggerObject = new GameObject(name);
        triggerObject.transform.SetParent(parent);
        triggerObject.transform.localPosition = center;
        triggerObject.transform.localRotation = Quaternion.identity;

        BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        return collider;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static TextMeshProUGUI CreateTmpLabel(string name, Transform parent, TMP_FontAsset fontAsset, float fontSize, FontStyles fontStyle)
    {
        GameObject labelObject = CreateUiObject(name, parent);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = Color.white;
        label.text = string.Empty;
        label.enableWordWrapping = false;
        return label;
    }

    private static TMP_FontAsset GetOrCreateTmpFontAsset()
    {
        const string fontAssetPath = "Assets/Materials/TMP_RuntimeFallback.asset";
        EnsureTmpEssentialResources();
        EnsureTmpSettingsAsset();

        TMP_FontAsset existingFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (existingFontAsset != null)
        {
            return existingFontAsset;
        }

        TMP_FontAsset fontAsset = TryCreateSystemTmpFontAsset("Malgun Gothic", "Regular");
        if (fontAsset == null)
        {
            fontAsset = TryCreateSystemTmpFontAsset("Arial", "Regular");
        }

        if (fontAsset == null)
        {
            try
            {
                Font systemFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial", "Segoe UI" }, 16);
                if (systemFont != null)
                {
                    fontAsset = TMP_FontAsset.CreateFontAsset(systemFont);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Failed to create TMP font asset from OS font fallback: {exception.Message}");
            }
        }

        if (fontAsset == null)
        {
            fontAsset = LoadFirstProjectTmpFontAsset();
        }

        if (fontAsset == null)
        {
            Debug.LogWarning("TMP fallback font asset could not be created. Route UI labels will be created without an assigned font.");
            return null;
        }

        if (AssetDatabase.GetAssetPath(fontAsset) == string.Empty)
        {
            AssetDatabase.CreateAsset(fontAsset, fontAssetPath);

            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
                {
                    if (atlasTexture != null)
                    {
                        AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                    }
                }
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static void EnsureTmpEssentialResources()
    {
        if (AssetDatabase.FindAssets("t:TMP_FontAsset").Length > 0 &&
            AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
        {
            return;
        }

        const string essentialPackagePath = "Library/PackageCache/com.unity.ugui@1f2d1ab0d950/Package Resources/TMP Essential Resources.unitypackage";
        if (!File.Exists(essentialPackagePath))
        {
            return;
        }

        AssetDatabase.ImportPackage(essentialPackagePath, false);
        AssetDatabase.Refresh();
    }

    private static void EnsureTmpSettingsAsset()
    {
        const string resourcesFolderPath = "Assets/Resources";
        const string settingsAssetPath = "Assets/Resources/TMP Settings.asset";

        TMP_Settings existingSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsAssetPath);
        if (existingSettings != null)
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(resourcesFolderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        TMP_Settings settings = ScriptableObject.CreateInstance<TMP_Settings>();
        AssetDatabase.CreateAsset(settings, settingsAssetPath);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static TMP_FontAsset TryCreateSystemTmpFontAsset(string familyName, string styleName)
    {
        try
        {
            return TMP_FontAsset.CreateFontAsset(familyName, styleName, 90);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Failed to create TMP font asset for {familyName} {styleName}: {exception.Message}");
            return null;
        }
    }

    private static TMP_FontAsset LoadFirstProjectTmpFontAsset()
    {
        string[] fontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        for (int index = 0; index < fontGuids.Length; index++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(fontGuids[index]);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset != null)
            {
                return fontAsset;
            }
        }

        return null;
    }

    private static GameObject CreateCylinder(string name, Vector3 center, float radius, float height, Material material, Transform parent, Quaternion? localRotation = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = center;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.transform.localScale = new Vector3(radius * 2.0f, height * 0.5f, radius * 2.0f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject CreateSphere(string name, Vector3 center, Vector3 size, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void AssignObjectReference(Component targetComponent, string propertyName, Object referenceValue)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = referenceValue;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignObjectReferenceArray(Component targetComponent, string propertyName, Object[] referenceValues)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            return;
        }

        property.arraySize = referenceValues.Length;
        for (int index = 0; index < referenceValues.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = referenceValues[index];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignStringArray(Component targetComponent, string propertyName, string[] values)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            return;
        }

        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).stringValue = values[index];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignStringValue(Component targetComponent, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignFloatValue(Component targetComponent, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignBooleanValue(Component targetComponent, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignLayerMaskValue(Component targetComponent, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(targetComponent);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static PlayerBuildSnapshot CaptureExistingPlayerSnapshot()
    {
        if (!File.Exists(ScenePath))
        {
            return default;
        }

        Scene existingScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] rootObjects = existingScene.GetRootGameObjects();
            GameObject playerObject = FindGameObjectByName(rootObjects, "Player");
            if (playerObject == null)
            {
                return default;
            }

            HO_PlayerMovement movement = playerObject.GetComponent<HO_PlayerMovement>();
            HO_FirstPersonCamera firstPersonCamera = playerObject.GetComponent<HO_FirstPersonCamera>();
            Camera mainCamera = playerObject.GetComponentInChildren<Camera>(true);
            Transform cameraPivot = playerObject.transform.Find("PlayerCameraPivot");

            return new PlayerBuildSnapshot(
                true,
                playerObject.transform.position,
                playerObject.transform.rotation,
                movement != null ? ReadFloatField(movement, "moveSpeed", 10.0f) : 10.0f,
                movement != null ? ReadFloatField(movement, "sprintSpeed", 15.0f) : 15.0f,
                movement != null ? ReadFloatField(movement, "jumpHeight", 5.0f) : 5.0f,
                movement != null ? ReadFloatField(movement, "gravity", 18.0f) : 18.0f,
                movement != null ? ReadFloatField(movement, "groundSnapForce", 1.0f) : 1.0f,
                cameraPivot != null ? cameraPivot.localPosition.y : 1.65f,
                firstPersonCamera != null ? ReadFloatField(firstPersonCamera, "mouseSensitivity", 1.0f) : 1.0f,
                firstPersonCamera != null ? ReadFloatField(firstPersonCamera, "minPitch", -75.0f) : -75.0f,
                firstPersonCamera != null ? ReadFloatField(firstPersonCamera, "maxPitch", 75.0f) : 75.0f,
                mainCamera != null ? mainCamera.fieldOfView : 68.0f,
                firstPersonCamera != null ? ReadBooleanField(firstPersonCamera, "lockCursorOnStart", true) : true);
        }
        finally
        {
            EditorSceneManager.CloseScene(existingScene, true);
        }
    }

    private static PlayerBuildSnapshot CaptureScenePlayerSnapshot(Transform root)
    {
        GameObject playerObject = FindNamedChild(root, "Player");
        if (playerObject == null)
        {
            playerObject = FindNamedChild(root, "Player_FirstPerson_Gallery");
        }

        if (playerObject == null)
        {
            return default;
        }

        float moveSpeed = 10.0f;
        float sprintSpeed = 15.0f;
        float jumpHeight = 5.0f;
        float gravity = 18.0f;
        float groundSnapForce = 1.0f;
        float cameraPivotLocalY = 1.65f;
        float mouseSensitivity = 1.0f;
        float minPitch = -75.0f;
        float maxPitch = 75.0f;
        float fieldOfView = 68.0f;
        bool lockCursorOnStart = true;

        HO_PlayerMovement movement = playerObject.GetComponent<HO_PlayerMovement>();
        HO_FirstPersonCamera firstPersonCamera = playerObject.GetComponent<HO_FirstPersonCamera>();
        GalleryPlayerController galleryPlayerController = playerObject.GetComponent<GalleryPlayerController>();
        Camera mainCamera = playerObject.GetComponentInChildren<Camera>(true);
        Transform cameraPivot = playerObject.transform.Find("PlayerCameraPivot");

        if (movement != null)
        {
            moveSpeed = ReadFloatField(movement, "moveSpeed", moveSpeed);
            sprintSpeed = ReadFloatField(movement, "sprintSpeed", sprintSpeed);
            jumpHeight = ReadFloatField(movement, "jumpHeight", jumpHeight);
            gravity = ReadFloatField(movement, "gravity", gravity);
            groundSnapForce = ReadFloatField(movement, "groundSnapForce", groundSnapForce);
        }
        else if (galleryPlayerController != null)
        {
            moveSpeed = ReadFloatField(galleryPlayerController, "walkSpeed", moveSpeed);
            sprintSpeed = ReadFloatField(galleryPlayerController, "sprintSpeed", sprintSpeed);
            jumpHeight = ReadFloatField(galleryPlayerController, "jumpHeight", jumpHeight);
            gravity = Mathf.Abs(ReadFloatField(galleryPlayerController, "gravity", -gravity));
        }

        if (firstPersonCamera != null)
        {
            mouseSensitivity = ReadFloatField(firstPersonCamera, "mouseSensitivity", mouseSensitivity);
            minPitch = ReadFloatField(firstPersonCamera, "minPitch", minPitch);
            maxPitch = ReadFloatField(firstPersonCamera, "maxPitch", maxPitch);
            lockCursorOnStart = ReadBooleanField(firstPersonCamera, "lockCursorOnStart", lockCursorOnStart);
        }
        else if (galleryPlayerController != null)
        {
            mouseSensitivity = ReadFloatField(galleryPlayerController, "mouseSensitivity", mouseSensitivity);
            minPitch = ReadFloatField(galleryPlayerController, "minPitch", minPitch);
            maxPitch = ReadFloatField(galleryPlayerController, "maxPitch", maxPitch);
            lockCursorOnStart = ReadBooleanField(galleryPlayerController, "lockCursorOnStart", lockCursorOnStart);
        }

        if (mainCamera != null)
        {
            fieldOfView = mainCamera.fieldOfView;
        }

        if (cameraPivot != null)
        {
            cameraPivotLocalY = cameraPivot.localPosition.y;
        }

        return new PlayerBuildSnapshot(
            true,
            playerObject.transform.position,
            playerObject.transform.rotation,
            moveSpeed,
            sprintSpeed,
            jumpHeight,
            gravity,
            groundSnapForce,
            cameraPivotLocalY,
            mouseSensitivity,
            minPitch,
            maxPitch,
            fieldOfView,
            lockCursorOnStart);
    }

    private static void RemoveExistingPrototypeSystems(Transform root)
    {
        string[] objectNames =
        {
            "Player",
            "Player_FirstPerson_Gallery",
            "GalleryZoneFlow",
            "Canvas_RouteUI",
            "Canvas_ArtworkPopup",
            "Canvas_CuratorSubtitle",
            "ArtworkInteractionSystem",
            "ViewReturnController",
            "LightingDirector",
            "NavMeshSurface_Gallery",
            "Nav_Path_Markers",
            "UI_EventSystem"
        };

        for (int index = 0; index < objectNames.Length; index++)
        {
            DestroyAllNamedChildren(root, objectNames[index]);
        }
    }

    private static void DestroyAllNamedChildren(Transform root, string name)
    {
        while (true)
        {
            GameObject target = FindNamedChild(root, name);
            if (target == null)
            {
                return;
            }

            Object.DestroyImmediate(target);
        }
    }

    private static GameObject FindGameObjectByName(GameObject[] rootObjects, string name)
    {
        for (int index = 0; index < rootObjects.Length; index++)
        {
            Transform[] descendants = rootObjects[index].GetComponentsInChildren<Transform>(true);
            for (int descendantIndex = 0; descendantIndex < descendants.Length; descendantIndex++)
            {
                if (descendants[descendantIndex].name == name)
                {
                    return descendants[descendantIndex].gameObject;
                }
            }
        }

        return null;
    }

    private static float ReadFloatField(Component component, string propertyName, float fallbackValue)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.floatValue : fallbackValue;
    }

    private static bool ReadBooleanField(Component component, string propertyName, bool fallbackValue)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.boolValue : fallbackValue;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;

        for (int index = 0; index < target.transform.childCount; index++)
        {
            SetLayerRecursively(target.transform.GetChild(index).gameObject, layer);
        }
    }

    private static T FindFirstComponentInChildren<T>(Transform root) where T : Component
    {
        return root.GetComponentInChildren<T>(true);
    }

    private static GameObject FindNamedChild(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
        {
            if (descendants[index].name == name)
            {
                return descendants[index].gameObject;
            }
        }

        return null;
    }

    private static Material Mat(string name)
    {
        return Materials[name];
    }
}

