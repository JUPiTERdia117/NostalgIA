using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyTestSceneSetup
{
    private const string MaterialsPath = "Assets/_Project/Art/Materials/Prototype";
    private const string PrefabsPath = "Assets/_Project/Prefabs/Enemies/Prototype";
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox/Enemies/EnemyTest.unity";
    private const string PrefKey = "EnemySetup_InitialRunDone_v4";

    [InitializeOnLoadMethod]
    private static void AutoRunOnce()
    {
        if (!EditorPrefs.GetBool(PrefKey, false))
        {
            EditorPrefs.SetBool(PrefKey, true);
            EditorApplication.delayCall += SetupAll;
        }
    }

    [MenuItem("Tools/Setup Enemies in EnemyTest")]
    public static void SetupAll()
    {
        Debug.Log("[EnemySetup] Iniciando configuración de enemigos...");

        // 1. Crear directorios
        if (!Directory.Exists(MaterialsPath)) Directory.CreateDirectory(MaterialsPath);
        if (!Directory.Exists(PrefabsPath)) Directory.CreateDirectory(PrefabsPath);

        // 2. Crear Materiales URP
        Material matBug = GetOrCreateMaterial("Enemy_Bug", new Color(0.18f, 0.58f, 0.22f), 0.4f);
        Material matSkull = GetOrCreateMaterial("Enemy_Skull", new Color(0.78f, 0.12f, 0.12f), 0.3f);
        Material matSpider = GetOrCreateMaterial("Enemy_Spider", new Color(0.12f, 0.08f, 0.16f), 0.6f);
        Material matSpawner = GetOrCreateMaterial("Enemy_SpiderSpawner", new Color(0.48f, 0.08f, 0.38f), 0.3f);
        Material matEye = GetOrCreateEmissiveMaterial("Enemy_GlowEye", new Color(1.0f, 0.8f, 0.1f), new Color(2.5f, 1.8f, 0.2f));
        Material matFireball = GetOrCreateEmissiveMaterial("Enemy_Fireball", new Color(1.0f, 0.35f, 0.05f), new Color(3.0f, 1.0f, 0.1f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Crear Prefab de Proyectil
        GameObject fireballPrefab = CreateFireballPrefab(matFireball);

        // 4. Crear Prefabs de Enemigos
        GameObject bugPrefab = CreateBugPrefab(matBug, matEye);
        GameObject skullPrefab = CreateSkullPrefab(matSkull, matEye, fireballPrefab);
        GameObject spiderPrefab = CreateSpiderPrefab(matSpider, matEye);
        GameObject spawnerPrefab = CreateSpawnerPrefab(matSpawner, matEye, spiderPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 5. Configurar la escena EnemyTest con diferentes niveles de salud
        SetupScene(bugPrefab, skullPrefab, spiderPrefab, spawnerPrefab);

        Debug.Log("[EnemySetup] ¡Configuración de enemigos y escena completada exitosamente!");
    }

    private static Material GetOrCreateMaterial(string name, Color baseColor, float smoothness)
    {
        string path = $"{MaterialsPath}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static Material GetOrCreateEmissiveMaterial(string name, Color baseColor, Color emissionColor)
    {
        string path = $"{MaterialsPath}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_Color", baseColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static GameObject CreateFireballPrefab(Material matFireball)
    {
        string path = $"{PrefabsPath}/Projectile - Fireball.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        GameObject root = new GameObject("Projectile - Fireball");

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Model";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * 0.4f;
        sphere.GetComponent<Renderer>().sharedMaterial = matFireball;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.radius = 0.25f;
        col.isTrigger = true;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        root.AddComponent<EnemyProjectile>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateBugPrefab(Material matBug, Material matEye)
    {
        string path = $"{PrefabsPath}/Enemy - Bug.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        GameObject root = new GameObject("Enemy - Bug");
        root.tag = "Enemy";

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 0.45f, 0);
        body.transform.localRotation = Quaternion.Euler(75f, 0, 0);
        body.transform.localScale = new Vector3(0.7f, 0.55f, 0.6f);
        body.GetComponent<Renderer>().sharedMaterial = matBug;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0, 0.4f, 0.48f);
        head.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
        head.GetComponent<Renderer>().sharedMaterial = matBug;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        CreateEye(head.transform, new Vector3(0.12f, 0.12f, 0.18f), matEye, 0.1f);
        CreateEye(head.transform, new Vector3(-0.12f, 0.12f, 0.18f), matEye, 0.1f);

        CreateMandible(head.transform, new Vector3(0.12f, -0.05f, 0.22f), 15f, matBug);
        CreateMandible(head.transform, new Vector3(-0.12f, -0.05f, 0.22f), -15f, matBug);

        float[] zOffsets = { 0.25f, 0f, -0.25f };
        for (int i = 0; i < 3; i++)
        {
            CreateLeg(root.transform, new Vector3(0.35f, 0.35f, zOffsets[i]), 35f, matBug);
            CreateLeg(root.transform, new Vector3(-0.35f, 0.35f, zOffsets[i]), -35f, matBug);
        }

        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.center = new Vector3(0, 0.4f, 0);
        boxCol.size = new Vector3(0.9f, 0.8f, 1.2f);

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.speed = 3.5f;
        agent.stoppingDistance = 1.5f;
        agent.radius = 0.5f;
        agent.height = 0.8f;
        agent.baseOffset = 0.4f;

        EnemyHealth hp = root.AddComponent<EnemyHealth>();
        SerializedObject soHp = new SerializedObject(hp);
        soHp.FindProperty("baseMaxHealth").intValue = 3;
        soHp.FindProperty("healthBarHeight").floatValue = 1.1f;
        soHp.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<BugEnemyController>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateSkullPrefab(Material matSkull, Material matEye, GameObject fireballPrefab)
    {
        string path = $"{PrefabsPath}/Enemy - FlyingSkull.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        GameObject root = new GameObject("Enemy - FlyingSkull");
        root.tag = "Enemy";

        GameObject skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skull.name = "Skull_Head";
        skull.transform.SetParent(root.transform, false);
        skull.transform.localScale = new Vector3(1.1f, 1.15f, 1.1f);
        skull.GetComponent<Renderer>().sharedMaterial = matSkull;
        Object.DestroyImmediate(skull.GetComponent<Collider>());

        GameObject jaw = GameObject.CreatePrimitive(PrimitiveType.Cube);
        jaw.name = "Jaw";
        jaw.transform.SetParent(root.transform, false);
        jaw.transform.localPosition = new Vector3(0, -0.38f, 0.22f);
        jaw.transform.localRotation = Quaternion.Euler(15f, 0, 0);
        jaw.transform.localScale = new Vector3(0.65f, 0.3f, 0.55f);
        jaw.GetComponent<Renderer>().sharedMaterial = matSkull;
        Object.DestroyImmediate(jaw.GetComponent<Collider>());

        GameObject mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mouth.name = "Mouth_Cavity";
        mouth.transform.SetParent(root.transform, false);
        mouth.transform.localPosition = new Vector3(0, -0.18f, 0.42f);
        mouth.transform.localScale = new Vector3(0.5f, 0.2f, 0.3f);
        mouth.GetComponent<Renderer>().sharedMaterial = matSkull;
        Object.DestroyImmediate(mouth.GetComponent<Collider>());

        CreateEye(root.transform, new Vector3(0.24f, 0.15f, 0.45f), matEye, 0.22f);
        CreateEye(root.transform, new Vector3(-0.24f, 0.15f, 0.45f), matEye, 0.22f);

        CreateHorn(root.transform, new Vector3(0.42f, 0.55f, -0.15f), new Vector3(25f, 30f, 20f), matSkull);
        CreateHorn(root.transform, new Vector3(-0.42f, 0.55f, -0.15f), new Vector3(25f, -30f, -20f), matSkull);

        GameObject shootPt = new GameObject("ShootPoint");
        shootPt.transform.SetParent(root.transform, false);
        shootPt.transform.localPosition = new Vector3(0, -0.15f, 0.75f);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.radius = 0.65f;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        EnemyHealth hp = root.AddComponent<EnemyHealth>();
        SerializedObject soHp = new SerializedObject(hp);
        soHp.FindProperty("baseMaxHealth").intValue = 2;
        soHp.FindProperty("healthBarHeight").floatValue = 1.0f;
        soHp.ApplyModifiedPropertiesWithoutUndo();

        FlyingEnemyController flyingController = root.AddComponent<FlyingEnemyController>();
        SerializedObject so = new SerializedObject(flyingController);
        so.FindProperty("projectilePrefab").objectReferenceValue = fireballPrefab;
        so.FindProperty("shootPoint").objectReferenceValue = shootPt.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateSpiderPrefab(Material matSpider, Material matEye)
    {
        string path = $"{PrefabsPath}/Enemy - Spider.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        GameObject root = new GameObject("Enemy - Spider");
        root.tag = "Enemy";

        GameObject thorax = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        thorax.name = "Thorax";
        thorax.transform.SetParent(root.transform, false);
        thorax.transform.localPosition = new Vector3(0, 0.25f, 0.15f);
        thorax.transform.localScale = new Vector3(0.55f, 0.35f, 0.5f);
        thorax.GetComponent<Renderer>().sharedMaterial = matSpider;
        Object.DestroyImmediate(thorax.GetComponent<Collider>());

        GameObject abdomen = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        abdomen.name = "Abdomen";
        abdomen.transform.SetParent(root.transform, false);
        abdomen.transform.localPosition = new Vector3(0, 0.35f, -0.45f);
        abdomen.transform.localRotation = Quaternion.Euler(15f, 0, 0);
        abdomen.transform.localScale = new Vector3(0.85f, 0.6f, 0.95f);
        abdomen.GetComponent<Renderer>().sharedMaterial = matSpider;
        Object.DestroyImmediate(abdomen.GetComponent<Collider>());

        Vector3[] eyePos = {
            new Vector3(0.08f, 0.32f, 0.4f),
            new Vector3(-0.08f, 0.32f, 0.4f),
            new Vector3(0.16f, 0.28f, 0.38f),
            new Vector3(-0.16f, 0.28f, 0.38f)
        };
        foreach (var pos in eyePos)
        {
            CreateEye(root.transform, pos, matEye, 0.08f);
        }

        CreateMandible(root.transform, new Vector3(0.08f, 0.15f, 0.42f), 10f, matSpider);
        CreateMandible(root.transform, new Vector3(-0.08f, 0.15f, 0.42f), -10f, matSpider);

        float[] legZ = { 0.3f, 0.1f, -0.1f, -0.3f };
        float[] legAngle = { 45f, 75f, 105f, 135f };
        for (int i = 0; i < 4; i++)
        {
            CreateSpiderLeg(root.transform, legZ[i], legAngle[i], true, matSpider);
            CreateSpiderLeg(root.transform, legZ[i], legAngle[i], false, matSpider);
        }

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.center = new Vector3(0, 0.28f, 0);
        col.radius = 0.55f;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.0f;
        rb.isKinematic = true;
        rb.useGravity = false;

        EnemyHealth hp = root.AddComponent<EnemyHealth>();
        SerializedObject soHp = new SerializedObject(hp);
        soHp.FindProperty("baseMaxHealth").intValue = 2;
        soHp.FindProperty("healthBarHeight").floatValue = 0.75f;
        soHp.ApplyModifiedPropertiesWithoutUndo();

        SpiderEnemyController spiderCtrl = root.AddComponent<SpiderEnemyController>();
        SerializedObject soSpider = new SerializedObject(spiderCtrl);
        soSpider.FindProperty("crawlSpeed").floatValue = 3.6f;
        soSpider.FindProperty("surfaceAlignSpeed").floatValue = 14f;
        soSpider.FindProperty("adhereSpeed").floatValue = 16f;
        soSpider.FindProperty("forwardWallCheckDistance").floatValue = 0.9f;
        soSpider.FindProperty("jumpRange").floatValue = 8.5f;
        soSpider.FindProperty("jumpSpeed").floatValue = 12f;
        soSpider.FindProperty("latchDistance").floatValue = 1.2f;
        soSpider.FindProperty("maxLatchDuration").floatValue = 2.5f;
        soSpider.FindProperty("latchDamageInterval").floatValue = 0.9f;
        soSpider.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateSpawnerPrefab(Material matSpawner, Material matEye, GameObject spiderPrefab)
    {
        string path = $"{PrefabsPath}/Enemy - SpiderSpawner.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        GameObject root = new GameObject("Enemy - SpiderSpawner");
        root.tag = "Enemy";

        GameObject mainSac = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mainSac.name = "Main_Sac";
        mainSac.transform.SetParent(root.transform, false);
        mainSac.transform.localPosition = new Vector3(0, 0.75f, 0);
        mainSac.transform.localScale = new Vector3(1.7f, 1.4f, 1.7f);
        mainSac.GetComponent<Renderer>().sharedMaterial = matSpawner;
        Object.DestroyImmediate(mainSac.GetComponent<Collider>());

        Vector3[] clusterPos = {
            new Vector3(0.7f, 0.4f, 0.5f),
            new Vector3(-0.75f, 0.35f, 0.4f),
            new Vector3(0.4f, 0.38f, -0.7f),
            new Vector3(-0.55f, 0.42f, -0.6f)
        };
        for (int i = 0; i < clusterPos.Length; i++)
        {
            GameObject egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            egg.name = $"Egg_Cluster_{i}";
            egg.transform.SetParent(root.transform, false);
            egg.transform.localPosition = clusterPos[i];
            egg.transform.localScale = Vector3.one * Random.Range(0.65f, 0.9f);
            egg.GetComponent<Renderer>().sharedMaterial = matSpawner;
            Object.DestroyImmediate(egg.GetComponent<Collider>());
        }

        GameObject vent = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        vent.name = "Birth_Orifice";
        vent.transform.SetParent(root.transform, false);
        vent.transform.localPosition = new Vector3(0, 1.45f, 0);
        vent.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
        vent.GetComponent<Renderer>().sharedMaterial = matEye;
        Object.DestroyImmediate(vent.GetComponent<Collider>());

        GameObject spawnPt = new GameObject("SpawnPoint");
        spawnPt.transform.SetParent(root.transform, false);
        spawnPt.transform.localPosition = new Vector3(0, 1.6f, 0);

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.center = new Vector3(0, 0.75f, 0);
        col.radius = 1.0f;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        EnemyHealth health = root.AddComponent<EnemyHealth>();
        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("baseMaxHealth").intValue = 6;
        healthSo.FindProperty("healthTier").enumValueIndex = (int)EnemyHealthTier.Jefe;
        healthSo.FindProperty("healthBarHeight").floatValue = 1.9f;
        healthSo.ApplyModifiedPropertiesWithoutUndo();

        SpiderSpawner spawner = root.AddComponent<SpiderSpawner>();
        SerializedObject spawnerSo = new SerializedObject(spawner);
        spawnerSo.FindProperty("spiderPrefab").objectReferenceValue = spiderPrefab;
        spawnerSo.FindProperty("spawnPoint").objectReferenceValue = spawnPt.transform;
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateEye(Transform parent, Vector3 localPos, Material mat, float size)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Eye";
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * size;
        eye.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(eye.GetComponent<Collider>());
    }

    private static void CreateMandible(Transform parent, Vector3 localPos, float rotY, Material mat)
    {
        GameObject mand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mand.name = "Mandible";
        mand.transform.SetParent(parent, false);
        mand.transform.localPosition = localPos;
        mand.transform.localRotation = Quaternion.Euler(15f, rotY, 0f);
        mand.transform.localScale = new Vector3(0.08f, 0.08f, 0.22f);
        mand.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(mand.GetComponent<Collider>());
    }

    private static void CreateHorn(Transform parent, Vector3 localPos, Vector3 localRot, Material mat)
    {
        GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        horn.name = "Horn";
        horn.transform.SetParent(parent, false);
        horn.transform.localPosition = localPos;
        horn.transform.localRotation = Quaternion.Euler(localRot);
        horn.transform.localScale = new Vector3(0.16f, 0.35f, 0.16f);
        horn.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(horn.GetComponent<Collider>());
    }

    private static void CreateLeg(Transform parent, Vector3 localPos, float rotZ, Material mat)
    {
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = "Leg";
        leg.transform.SetParent(parent, false);
        leg.transform.localPosition = localPos;
        leg.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
        leg.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
        leg.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(leg.GetComponent<Collider>());
    }

    private static void CreateSpiderLeg(Transform parent, float zPos, float yaw, bool isRightSide, Material mat)
    {
        float sign = isRightSide ? 1f : -1f;
        GameObject legRoot = new GameObject(isRightSide ? "Leg_R" : "Leg_L");
        legRoot.transform.SetParent(parent, false);
        legRoot.transform.localPosition = new Vector3(sign * 0.25f, 0.25f, zPos);

        GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        upper.name = "Upper";
        upper.transform.SetParent(legRoot.transform, false);
        upper.transform.localPosition = new Vector3(sign * 0.18f, 0.1f, 0);
        upper.transform.localRotation = Quaternion.Euler(0, isRightSide ? yaw : -yaw, sign * -40f);
        upper.transform.localScale = new Vector3(0.06f, 0.22f, 0.06f);
        upper.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(upper.GetComponent<Collider>());

        GameObject lower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lower.name = "Lower";
        lower.transform.SetParent(legRoot.transform, false);
        lower.transform.localPosition = new Vector3(sign * 0.36f, -0.1f, 0);
        lower.transform.localRotation = Quaternion.Euler(0, isRightSide ? yaw : -yaw, sign * 35f);
        lower.transform.localScale = new Vector3(0.05f, 0.24f, 0.05f);
        lower.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(lower.GetComponent<Collider>());
    }

    private static void SetupScene(GameObject bugPrefab, GameObject skullPrefab, GameObject spiderPrefab, GameObject spawnerPrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var existingEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in existingEnemies)
        {
            Object.DestroyImmediate(enemy);
        }

        // 1. Bichos terrestres: Uno Normal (3 HP) y uno Resistente (5 HP)
        GameObject bugInstance1 = (GameObject)PrefabUtility.InstantiatePrefab(bugPrefab);
        bugInstance1.name = "Enemy - Bug (Normal - 3 HP)";
        bugInstance1.transform.position = new Vector3(2.0f, 0.0f, 4.0f);
        bugInstance1.transform.rotation = Quaternion.Euler(0, 180f, 0);
        SetInstanceTier(bugInstance1, EnemyHealthTier.Normal);

        GameObject bugInstance2 = (GameObject)PrefabUtility.InstantiatePrefab(bugPrefab);
        bugInstance2.name = "Enemy - Bug (Resistente - 5 HP)";
        bugInstance2.transform.position = new Vector3(-2.5f, 0.0f, 6.0f);
        bugInstance2.transform.rotation = Quaternion.Euler(0, 150f, 0);
        SetInstanceTier(bugInstance2, EnemyHealthTier.Resistente);

        // 2. Calaveras Voladoras: Una Normal (2 HP) y una Élite (5 HP)
        GameObject skullInstance1 = (GameObject)PrefabUtility.InstantiatePrefab(skullPrefab);
        skullInstance1.name = "Enemy - FlyingSkull (Normal - 2 HP)";
        skullInstance1.transform.position = new Vector3(4.5f, 3.2f, 7.0f);
        skullInstance1.transform.rotation = Quaternion.Euler(0, 200f, 0);
        SetInstanceTier(skullInstance1, EnemyHealthTier.Normal);

        GameObject skullInstance2 = (GameObject)PrefabUtility.InstantiatePrefab(skullPrefab);
        skullInstance2.name = "Enemy - FlyingSkull (Elite - 5 HP)";
        skullInstance2.transform.position = new Vector3(-4.0f, 3.8f, 8.5f);
        skullInstance2.transform.rotation = Quaternion.Euler(0, 160f, 0);
        SetInstanceTier(skullInstance2, EnemyHealthTier.Elite);

        // 3. Arañas: Una en pared vertical y una en suelo
        GameObject spiderWallInstance = (GameObject)PrefabUtility.InstantiatePrefab(spiderPrefab);
        spiderWallInstance.name = "Enemy - Spider (On Wall - Resistente)";
        spiderWallInstance.transform.position = new Vector3(7.7f, 3.0f, 2.6f);
        spiderWallInstance.transform.rotation = Quaternion.Euler(0, 0, 90f);
        SetInstanceTier(spiderWallInstance, EnemyHealthTier.Resistente);

        GameObject spiderFloorInstance = (GameObject)PrefabUtility.InstantiatePrefab(spiderPrefab);
        spiderFloorInstance.name = "Enemy - Spider (Ground - Normal)";
        spiderFloorInstance.transform.position = new Vector3(-0.5f, 0.0f, 7.5f);
        spiderFloorInstance.transform.rotation = Quaternion.Euler(0, 180f, 0);
        SetInstanceTier(spiderFloorInstance, EnemyHealthTier.Normal);

        // 4. Spawner de Arañas: Nido Jefe con alta salud
        GameObject spawnerInstance = (GameObject)PrefabUtility.InstantiatePrefab(spawnerPrefab);
        spawnerInstance.name = "Enemy - SpiderSpawner (Jefe - 24 HP)";
        spawnerInstance.transform.position = new Vector3(5.5f, 0.0f, 9.0f);
        spawnerInstance.transform.rotation = Quaternion.Euler(0, 210f, 0);
        SetInstanceTier(spawnerInstance, EnemyHealthTier.Jefe);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[EnemySetup] Escena EnemyTest actualizada con niveles de salud y adherencia refinada.");
    }

    private static void SetInstanceTier(GameObject obj, EnemyHealthTier tier)
    {
        EnemyHealth eh = obj.GetComponent<EnemyHealth>();
        if (eh != null)
        {
            SerializedObject so = new SerializedObject(eh);
            SerializedProperty prop = so.FindProperty("healthTier");
            if (prop != null)
            {
                prop.enumValueIndex = (int)tier;
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(eh);
                EditorUtility.SetDirty(eh);
                EditorUtility.SetDirty(obj);
            }
        }
    }
}
