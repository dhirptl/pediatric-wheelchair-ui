using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that turns the wheelchair's URDF/STL CAD (vendored under
/// &lt;project&gt;/URDF_Source/meshes) into a Unity prefab whose hierarchy, link
/// offsets and orientation match sim_wheelchair.xacro. There is no STL importer
/// in this project, so we parse the binary STLs ourselves.
///
/// Coordinate frames: ROS REP-103 is +X forward / +Y left / +Z up (right-handed);
/// Unity is +Z forward / +Y up (left-handed). The basis change C: (x,y,z) -> (-y,z,x)
/// is applied to every vertex (with a winding flip for the handedness change), every
/// joint position, and every joint rotation. Because C is a similarity it composes
/// correctly through the hierarchy regardless of parent rotation.
///
/// Run via the menu: Tools/Wheelchair/Import URDF Meshes.
/// </summary>
public static class WheelchairStlImporter
{
    const string MeshOutDir = "Assets/Models/Wheelchair/Meshes";
    const string MatDir = "Assets/Materials";
    const string PrefabPath = "Assets/PreFabs/WheelchairModel.prefab";

    static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../URDF_Source/meshes"));

    // ROS -> Unity basis change for a position / direction vector.
    static Vector3 C(float x, float y, float z) => new Vector3(-y, z, x);
    static Vector3 C(Vector3 v) => new Vector3(-v.y, v.z, v.x);

    // ROS rpy (URDF Rz*Ry*Rx) -> Unity local rotation.
    static Quaternion RpyToUnity(float roll, float pitch, float yaw)
    {
        float cr = Mathf.Cos(roll * 0.5f), sr = Mathf.Sin(roll * 0.5f);
        float cp = Mathf.Cos(pitch * 0.5f), sp = Mathf.Sin(pitch * 0.5f);
        float cy = Mathf.Cos(yaw * 0.5f), sy = Mathf.Sin(yaw * 0.5f);
        float qw = cr * cp * cy + sr * sp * sy;
        float qx = sr * cp * cy - cr * sp * sy;
        float qy = cr * sp * cy + sr * cp * sy;
        float qz = cr * cp * sy - sr * sp * cy;
        // Convert the ROS quaternion into Unity's left-handed frame.
        return new Quaternion(qy, -qz, -qx, qw);
    }

    [MenuItem("Tools/Wheelchair/Import URDF Meshes")]
    public static void Import()
    {
        if (!Directory.Exists(SourceDir))
        {
            EditorUtility.DisplayDialog("Wheelchair Import",
                "Could not find STL source folder:\n" + SourceDir +
                "\n\nCopy the URDF meshes to <project>/URDF_Source/meshes first.", "OK");
            return;
        }

        Directory.CreateDirectory(MeshOutDir);

        // Materials (URP Lit, matching the shader used project-wide).
        var frameMat = MakeMaterial("WC_Frame_Mat", new Color(0.18f, 0.42f, 0.85f), 0.1f, 0.5f);
        var wheelMat = MakeMaterial("WC_Wheel_Mat", new Color(0.08f, 0.08f, 0.09f), 0.0f, 0.35f);
        var casterMat = MakeMaterial("WC_Caster_Mat", new Color(0.40f, 0.40f, 0.43f), 0.3f, 0.6f);
        var accMat = MakeMaterial("WC_Accessory_Mat", new Color(0.22f, 0.22f, 0.25f), 0.2f, 0.5f);

        // Root == URDF base_link.
        var root = new GameObject("WheelchairModel");

        // body_frame: attached to base_link with rpy (pi,0,0).
        var body = MakeMeshLink("body_frame", "bodyframe.STL", root.transform,
            Vector3.zero, RpyToUnity(Mathf.PI, 0f, 0f), frameMat);

        // Driven wheels (powered, continuous joints) - parented to body_frame.
        MakeMeshLink("L_driven", "leftdrivenwheel_link.STL", body.transform,
            C(0.0381f, -0.31115f, -0.05715f), Quaternion.identity, wheelMat);
        MakeMeshLink("R_driven", "rightdrivenwheel_link.STL", body.transform,
            C(0.0381f, 0.31115f, -0.05715f), Quaternion.identity, wheelMat);

        // Caster wheels (fixed in URDF; we swivel them at runtime for realism).
        MakeMeshLink("L_front", "frontleftcastor_link.STL", body.transform,
            C(0.2667f, -0.27622f, -0.00635f), Quaternion.identity, casterMat);
        MakeMeshLink("R_front", "frontrightcastor_link.STL", body.transform,
            C(0.2667f, 0.27622f, -0.00635f), Quaternion.identity, casterMat);
        MakeMeshLink("L_back", "backleftcastor_link.STL", body.transform,
            C(-0.254f, -0.20637f, -0.00635f), Quaternion.identity, casterMat);
        MakeMeshLink("R_back", "backrightcastor_link.STL", body.transform,
            C(-0.254f, 0.20637f, -0.00635f), Quaternion.identity, casterMat);

        // Accessories (joystick mount / armrest posts) - simple boxes on base_link.
        var acc = new GameObject("Accessories");
        acc.transform.SetParent(root.transform, false);
        MakeBox("add", acc.transform, C(0.415f, -0.24f, 0.725f), Quaternion.identity, new Vector3(0.03f, 0.1f, 0.035f), accMat);
        MakeBox("add2", acc.transform, C(0.45f, -0.19f, 0.338f), Quaternion.identity, new Vector3(0.18f, 0.12f, 0.028f), accMat);
        MakeBox("add3_joystick", acc.transform, C(0.26f, -0.20f, 0.423f), Quaternion.identity, new Vector3(0.05f, 0.1f, 0.03f), frameMat);
        MakeBox("add4", acc.transform, C(0.26f, -0.235f, 0.56f), Quaternion.identity, new Vector3(0.02f, 0.02f, 0.3f), accMat);
        MakeBox("add5", acc.transform, C(0.33f, -0.235f, 0.7f), RpyToUnity(0f, 1.570795f, 0f), new Vector3(0.02f, 0.02f, 0.15f), accMat);
        MakeBox("add6", acc.transform, C(0.37f, -0.235f, 0.52f), Quaternion.identity, new Vector3(0.02f, 0.02f, 0.37f), accMat);

        // Vertical offset: drop the assembly so its lowest point rests at the
        // capsule base (avatar-local y = -1, i.e. half the 2-unit capsule height).
        const float capsuleBottom = -1f;
        var bounds = CombinedBounds(root);
        root.transform.position = new Vector3(0f, capsuleBottom - bounds.min.y, 0f);

        // Save as a prefab.
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        Debug.Log("[WheelchairStlImporter] Built " + PrefabPath +
                  "  (meshes in " + MeshOutDir + ")");
    }

    static GameObject MakeMeshLink(string name, string stlFile, Transform parent,
        Vector3 localPos, Quaternion localRot, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        var mesh = ParseBinaryStl(Path.Combine(SourceDir, stlFile), name);
        if (mesh != null)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        return go;
    }

    static void MakeBox(string name, Transform parent, Vector3 localPos,
        Quaternion localRot, Vector3 rosSize, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        // Box dimensions: ROS (x,y,z) -> Unity (y,z,x).
        go.transform.localScale = new Vector3(rosSize.y, rosSize.z, rosSize.x);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MatDir + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Bounds CombinedBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Parses a binary STL into a Unity Mesh, converting ROS->Unity coords.</summary>
    static Mesh ParseBinaryStl(string path, string meshName)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("[WheelchairStlImporter] Missing STL: " + path);
            return null;
        }

        byte[] data = File.ReadAllBytes(path);
        using (var ms = new MemoryStream(data))
        using (var br = new BinaryReader(ms))
        {
            br.ReadBytes(80);                       // header
            uint triCount = br.ReadUInt32();

            var verts = new Vector3[triCount * 3];
            var tris = new int[triCount * 3];

            for (int t = 0; t < triCount; t++)
            {
                br.ReadBytes(12);                   // face normal (ignored, recomputed)
                int baseIdx = t * 3;
                for (int v = 0; v < 3; v++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    verts[baseIdx + v] = C(x, y, z);
                }
                // Winding flip (handedness change): keep v0, swap v1 and v2.
                tris[baseIdx + 0] = baseIdx + 0;
                tris[baseIdx + 1] = baseIdx + 2;
                tris[baseIdx + 2] = baseIdx + 1;
                br.ReadBytes(2);                    // attribute byte count
            }

            var mesh = new Mesh { name = meshName };
            if (verts.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string assetPath = MeshOutDir + "/" + meshName + ".asset";
            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }
    }
}
