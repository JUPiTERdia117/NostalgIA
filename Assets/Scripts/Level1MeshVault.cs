using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bóveda centralizada de mallas 3D procedurales para Nivel 1.
/// Genera y cachea en memoria mallas 3D de alta fidelidad garantizadas en tiempo de ejecución y editor.
/// </summary>
public static class Level1MeshVault
{
    private static Mesh hexIslandMesh;
    private static Mesh hoverPlatformMesh;
    private static Mesh grapplePylonMesh;
    private static Mesh spikeTrapMesh;
    private static Mesh sawBladeMesh;
    private static Mesh laserPillarMesh;
    private static Mesh droneMesh;
    private static Mesh starMesh;
    private static Mesh checkpointMesh;
    private static Mesh energyCubeMesh;
    private static Mesh victoryArchMesh;

    public static Mesh GetMesh(ProceduralModelView.ModelType type)
    {
        switch (type)
        {
            case ProceduralModelView.ModelType.HexIsland:
                if (hexIslandMesh == null) hexIslandMesh = CreateHexIslandMesh();
                return hexIslandMesh;

            case ProceduralModelView.ModelType.HoverPlatform:
                if (hoverPlatformMesh == null) hoverPlatformMesh = CreateHoverPlatformMesh();
                return hoverPlatformMesh;

            case ProceduralModelView.ModelType.GrapplePylon:
                if (grapplePylonMesh == null) grapplePylonMesh = CreateGrapplePylonMesh();
                return grapplePylonMesh;

            case ProceduralModelView.ModelType.SpikeTrap:
                if (spikeTrapMesh == null) spikeTrapMesh = CreateSpikeTrapMesh();
                return spikeTrapMesh;

            case ProceduralModelView.ModelType.SawBlade:
                if (sawBladeMesh == null) sawBladeMesh = CreateSawBladeMesh();
                return sawBladeMesh;

            case ProceduralModelView.ModelType.LaserPillar:
                if (laserPillarMesh == null) laserPillarMesh = CreateLaserPillarMesh();
                return laserPillarMesh;

            case ProceduralModelView.ModelType.SentinelDrone:
                if (droneMesh == null) droneMesh = CreateDroneMesh();
                return droneMesh;

            case ProceduralModelView.ModelType.CollectibleStar:
                if (starMesh == null) starMesh = CreateStarMesh();
                return starMesh;

            case ProceduralModelView.ModelType.CheckpointShrine:
                if (checkpointMesh == null) checkpointMesh = CreateCheckpointMesh();
                return checkpointMesh;

            case ProceduralModelView.ModelType.EnergyCube:
                if (energyCubeMesh == null) energyCubeMesh = CreateEnergyCubeMesh();
                return energyCubeMesh;

            case ProceduralModelView.ModelType.VictoryArch:
                if (victoryArchMesh == null) victoryArchMesh = CreateVictoryArchMesh();
                return victoryArchMesh;

            default:
                return null;
        }
    }

    // 1. Isla Hexagonal con base piramidal flotante
    public static Mesh CreateHexIslandMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Platform_HexIsland" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float rTop = 4.5f, rRim = 4.8f, hTop = 0f, hRim = -0.5f, hBot = -3.5f;
        Vector3 centerTop = new Vector3(0, hTop, 0);
        Vector3 apexBot = new Vector3(0, hBot, 0);

        for (int i = 0; i < 6; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 6f);
            float a2 = (i + 1) * (Mathf.PI * 2f / 6f);

            Vector3 pt1 = new Vector3(rTop * Mathf.Cos(a1), hTop, rTop * Mathf.Sin(a1));
            Vector3 pt2 = new Vector3(rTop * Mathf.Cos(a2), hTop, rTop * Mathf.Sin(a2));
            AddTriangle(verts, norms, uvs, tris, centerTop, pt1, pt2, Vector3.up);

            Vector3 pr1 = new Vector3(rRim * Mathf.Cos(a1), hRim, rRim * Mathf.Sin(a1));
            Vector3 pr2 = new Vector3(rRim * Mathf.Cos(a2), hRim, rRim * Mathf.Sin(a2));
            AddQuad(verts, norms, uvs, tris, pt1, pt2, pr2, pr1);
            AddTriangle(verts, norms, uvs, tris, pr2, pr1, apexBot);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 2. Plataforma Móvil con propulsores
    public static Mesh CreateHoverPlatformMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "MovingPlatform_Hover" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float sx = 2.0f, sz = 2.0f, h = 0.4f;
        AddQuad(verts, norms, uvs, tris, new Vector3(-sx, h, -sz), new Vector3(sx, h, -sz), new Vector3(sx, h, sz), new Vector3(-sx, h, sz), Vector3.up);
        AddQuad(verts, norms, uvs, tris, new Vector3(-sx, 0, sz), new Vector3(sx, 0, sz), new Vector3(sx, 0, -sz), new Vector3(-sx, 0, -sz), Vector3.down);
        AddQuad(verts, norms, uvs, tris, new Vector3(-sx, 0, -sz), new Vector3(sx, 0, -sz), new Vector3(sx, h, -sz), new Vector3(-sx, h, -sz), Vector3.back);
        AddQuad(verts, norms, uvs, tris, new Vector3(sx, 0, -sz), new Vector3(sx, 0, sz), new Vector3(sx, h, sz), new Vector3(sx, h, -sz), Vector3.right);
        AddQuad(verts, norms, uvs, tris, new Vector3(sx, 0, sz), new Vector3(-sx, 0, sz), new Vector3(-sx, h, sz), new Vector3(sx, h, sz), Vector3.forward);
        AddQuad(verts, norms, uvs, tris, new Vector3(-sx, 0, sz), new Vector3(-sx, 0, -sz), new Vector3(-sx, h, -sz), new Vector3(-sx, h, sz), Vector3.left);

        Vector2[] offsets = { new Vector2(-1.3f, -1.3f), new Vector2(1.3f, -1.3f), new Vector2(-1.3f, 1.3f), new Vector2(1.3f, 1.3f) };
        foreach (var off in offsets)
        {
            for (int i = 0; i < 8; i++)
            {
                float a1 = i * (Mathf.PI * 2f / 8f);
                float a2 = (i + 1) * (Mathf.PI * 2f / 8f);
                float r1 = 0.35f, r2 = 0.25f;

                Vector3 p1 = new Vector3(off.x + r1 * Mathf.Cos(a1), 0f, off.y + r1 * Mathf.Sin(a1));
                Vector3 p2 = new Vector3(off.x + r1 * Mathf.Cos(a2), 0f, off.y + r1 * Mathf.Sin(a2));
                Vector3 p3 = new Vector3(off.x + r2 * Mathf.Cos(a2), -0.35f, off.y + r2 * Mathf.Sin(a2));
                Vector3 p4 = new Vector3(off.x + r2 * Mathf.Cos(a1), -0.35f, off.y + r2 * Mathf.Sin(a1));
                AddQuad(verts, norms, uvs, tris, p1, p2, p3, p4);
                AddTriangle(verts, norms, uvs, tris, new Vector3(off.x, -0.35f, off.y), p4, p3, Vector3.down);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 3. Pilón de Gancho
    public static Mesh CreateGrapplePylonMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Grapple_AnchorPylon" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float hBase = 3.5f, rBase = 0.5f;
        for (int i = 0; i < 8; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 8f), a2 = (i + 1) * (Mathf.PI * 2f / 8f);
            Vector3 p1 = new Vector3(rBase * 1.3f * Mathf.Cos(a1), 0, rBase * 1.3f * Mathf.Sin(a1));
            Vector3 p2 = new Vector3(rBase * 1.3f * Mathf.Cos(a2), 0, rBase * 1.3f * Mathf.Sin(a2));
            Vector3 p3 = new Vector3(rBase * Mathf.Cos(a2), hBase, rBase * Mathf.Sin(a2));
            Vector3 p4 = new Vector3(rBase * Mathf.Cos(a1), hBase, rBase * Mathf.Sin(a1));
            AddQuad(verts, norms, uvs, tris, p1, p2, p3, p4);
        }

        float centerY = hBase + 1.2f, rRing = 1.0f, rTube = 0.18f;
        for (int i = 0; i < 16; i++)
        {
            float phi1 = i * (Mathf.PI * 2f / 16f), phi2 = (i + 1) * (Mathf.PI * 2f / 16f);
            Vector3 c1 = new Vector3(rRing * Mathf.Cos(phi1), centerY + rRing * Mathf.Sin(phi1), 0);
            Vector3 c2 = new Vector3(rRing * Mathf.Cos(phi2), centerY + rRing * Mathf.Sin(phi2), 0);
            Vector3 dir1 = new Vector3(Mathf.Cos(phi1), Mathf.Sin(phi1), 0);
            Vector3 dir2 = new Vector3(Mathf.Cos(phi2), Mathf.Sin(phi2), 0);

            for (int j = 0; j < 8; j++)
            {
                float th1 = j * (Mathf.PI * 2f / 8f), th2 = (j + 1) * (Mathf.PI * 2f / 8f);
                Vector3 q1 = c1 + dir1 * (rTube * Mathf.Cos(th1)) + Vector3.forward * (rTube * Mathf.Sin(th1));
                Vector3 q2 = c2 + dir2 * (rTube * Mathf.Cos(th1)) + Vector3.forward * (rTube * Mathf.Sin(th1));
                Vector3 q3 = c2 + dir2 * (rTube * Mathf.Cos(th2)) + Vector3.forward * (rTube * Mathf.Sin(th2));
                Vector3 q4 = c1 + dir1 * (rTube * Mathf.Cos(th2)) + Vector3.forward * (rTube * Mathf.Sin(th2));
                AddQuad(verts, norms, uvs, tris, q1, q2, q3, q4);
            }
        }

        float orbR = 0.35f;
        Vector3 top = new Vector3(0, centerY + orbR * 1.5f, 0);
        Vector3 bot = new Vector3(0, centerY - orbR * 1.5f, 0);
        Vector3[] midPts = {
            new Vector3(orbR, centerY, 0),
            new Vector3(0, centerY, orbR),
            new Vector3(-orbR, centerY, 0),
            new Vector3(0, centerY, -orbR)
        };
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            AddTriangle(verts, norms, uvs, tris, top, midPts[i], midPts[next]);
            AddTriangle(verts, norms, uvs, tris, bot, midPts[next], midPts[i]);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 4. Trampa de Pinchos
    public static Mesh CreateSpikeTrapMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Hazard_SpikeBlock" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float s = 1.0f, h = 0.2f;
        AddQuad(verts, norms, uvs, tris, new Vector3(-s, h, -s), new Vector3(s, h, -s), new Vector3(s, h, s), new Vector3(-s, h, s), Vector3.up);
        AddQuad(verts, norms, uvs, tris, new Vector3(-s, 0, -s), new Vector3(s, 0, -s), new Vector3(s, h, -s), new Vector3(-s, h, -s), Vector3.back);
        AddQuad(verts, norms, uvs, tris, new Vector3(s, 0, -s), new Vector3(s, 0, s), new Vector3(s, h, s), new Vector3(s, h, -s), Vector3.right);
        AddQuad(verts, norms, uvs, tris, new Vector3(s, 0, s), new Vector3(-s, 0, s), new Vector3(-s, h, s), new Vector3(s, h, s), Vector3.forward);
        AddQuad(verts, norms, uvs, tris, new Vector3(-s, 0, s), new Vector3(-s, 0, -s), new Vector3(-s, h, -s), new Vector3(-s, h, s), Vector3.left);

        Vector2[] spikeOffs = { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) };
        float spkR = 0.35f, spkH = 1.2f;
        foreach (var off in spikeOffs)
        {
            Vector3 tip = new Vector3(off.x, h + spkH, off.y);
            Vector3 p1 = new Vector3(off.x - spkR, h, off.y - spkR);
            Vector3 p2 = new Vector3(off.x + spkR, h, off.y - spkR);
            Vector3 p3 = new Vector3(off.x + spkR, h, off.y + spkR);
            Vector3 p4 = new Vector3(off.x - spkR, h, off.y + spkR);

            AddTriangle(verts, norms, uvs, tris, p1, p2, tip);
            AddTriangle(verts, norms, uvs, tris, p2, p3, tip);
            AddTriangle(verts, norms, uvs, tris, p3, p4, tip);
            AddTriangle(verts, norms, uvs, tris, p4, p1, tip);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 5. Sierra Dentada
    public static Mesh CreateSawBladeMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Hazard_SawBlade" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        int teeth = 16;
        float rBase = 1.0f, rTip = 1.4f, th = 0.08f;

        for (int i = 0; i < teeth; i++)
        {
            float a1 = i * (Mathf.PI * 2f / teeth);
            float a2 = a1 + (Mathf.PI / teeth) * 0.6f;
            float a3 = (i + 1) * (Mathf.PI * 2f / teeth);

            Vector3 pb1F = new Vector3(rBase * Mathf.Cos(a1), rBase * Mathf.Sin(a1), th);
            Vector3 pb1B = new Vector3(rBase * Mathf.Cos(a1), rBase * Mathf.Sin(a1), -th);
            Vector3 pTipF = new Vector3(rTip * Mathf.Cos(a2), rTip * Mathf.Sin(a2), th * 0.5f);
            Vector3 pTipB = new Vector3(rTip * Mathf.Cos(a2), rTip * Mathf.Sin(a2), -th * 0.5f);
            Vector3 pb2F = new Vector3(rBase * Mathf.Cos(a3), rBase * Mathf.Sin(a3), th);
            Vector3 pb2B = new Vector3(rBase * Mathf.Cos(a3), rBase * Mathf.Sin(a3), -th);

            AddTriangle(verts, norms, uvs, tris, new Vector3(0, 0, th), pb1F, pTipF, Vector3.forward);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, 0, th), pTipF, pb2F, Vector3.forward);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, 0, -th), pTipB, pb1B, Vector3.back);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, 0, -th), pb2B, pTipB, Vector3.back);

            AddQuad(verts, norms, uvs, tris, pb1F, pTipF, pTipB, pb1B);
            AddQuad(verts, norms, uvs, tris, pTipF, pb2F, pb2B, pTipB);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 6. Pilón Láser
    public static Mesh CreateLaserPillarMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Hazard_LaserPillar" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float h = 2.2f, r = 0.4f;
        for (int i = 0; i < 8; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 8f), a2 = (i + 1) * (Mathf.PI * 2f / 8f);
            Vector3 p1 = new Vector3(r * Mathf.Cos(a1), 0, r * Mathf.Sin(a1));
            Vector3 p2 = new Vector3(r * Mathf.Cos(a2), 0, r * Mathf.Sin(a2));
            Vector3 p3 = new Vector3(r * Mathf.Cos(a2), h, r * Mathf.Sin(a2));
            Vector3 p4 = new Vector3(r * Mathf.Cos(a1), h, r * Mathf.Sin(a1));
            AddQuad(verts, norms, uvs, tris, p1, p2, p3, p4);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, h, 0), p3, p4, Vector3.up);
        }

        float headY = 1.6f;
        for (int i = 0; i < 8; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 8f), a2 = (i + 1) * (Mathf.PI * 2f / 8f);
            float rHead = 0.25f;
            Vector3 p1 = new Vector3(rHead * Mathf.Cos(a1), headY + rHead * Mathf.Sin(a1), 0.2f);
            Vector3 p2 = new Vector3(rHead * Mathf.Cos(a2), headY + rHead * Mathf.Sin(a2), 0.2f);
            Vector3 p3 = new Vector3(rHead * 0.5f * Mathf.Cos(a2), headY + rHead * 0.5f * Mathf.Sin(a2), 0.65f);
            Vector3 p4 = new Vector3(rHead * 0.5f * Mathf.Cos(a1), headY + rHead * 0.5f * Mathf.Sin(a1), 0.65f);
            AddQuad(verts, norms, uvs, tris, p1, p2, p3, p4);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, headY, 0.65f), p3, p4, Vector3.forward);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 7. Dron Centinela
    public static Mesh CreateDroneMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Enemy_SentinelDrone" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        Vector3 top = new Vector3(0, 0.45f, 0), bot = new Vector3(0, -0.45f, 0);
        Vector3 right = new Vector3(0.65f, 0.1f, 0), left = new Vector3(-0.65f, 0.1f, 0);
        Vector3 back = new Vector3(0, 0.1f, -0.75f), eyePeak = new Vector3(0, 0, 0.65f);

        Vector3 frt = new Vector3(0.35f, 0.2f, 0.4f), flt = new Vector3(-0.35f, 0.2f, 0.4f);
        Vector3 frb = new Vector3(0.35f, -0.2f, 0.4f), flb = new Vector3(-0.35f, -0.2f, 0.4f);

        AddTriangle(verts, norms, uvs, tris, top, frt, right);
        AddTriangle(verts, norms, uvs, tris, top, flt, frt);
        AddTriangle(verts, norms, uvs, tris, top, left, flt);
        AddTriangle(verts, norms, uvs, tris, top, back, left);
        AddTriangle(verts, norms, uvs, tris, top, right, back);

        AddTriangle(verts, norms, uvs, tris, bot, right, frb);
        AddTriangle(verts, norms, uvs, tris, bot, frb, flb);
        AddTriangle(verts, norms, uvs, tris, bot, flb, left);
        AddTriangle(verts, norms, uvs, tris, bot, left, back);
        AddTriangle(verts, norms, uvs, tris, bot, back, right);

        AddTriangle(verts, norms, uvs, tris, eyePeak, frt, flt);
        AddTriangle(verts, norms, uvs, tris, eyePeak, flt, flb);
        AddTriangle(verts, norms, uvs, tris, eyePeak, flb, frb);
        AddTriangle(verts, norms, uvs, tris, eyePeak, frb, frt);

        AddQuad(verts, norms, uvs, tris, new Vector3(0.5f, 0.15f, 0.1f), new Vector3(1.4f, 0.2f, -0.4f), new Vector3(1.3f, -0.1f, -0.6f), new Vector3(0.5f, -0.15f, -0.2f));
        AddQuad(verts, norms, uvs, tris, new Vector3(-0.5f, 0.15f, 0.1f), new Vector3(-0.5f, -0.15f, -0.2f), new Vector3(-1.3f, -0.1f, -0.6f), new Vector3(-1.4f, 0.2f, -0.4f));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 8. Estrella 3D
    public static Mesh CreateStarMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Collectible_Star3D" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float rOut = 1.0f, rIn = 0.42f, zPeak = 0.35f, zRim = 0.08f;
        Vector2[] pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI * 0.5f - i * (Mathf.PI * 2f / 10f);
            float r = (i % 2 == 0) ? rOut : rIn;
            pts[i] = new Vector2(r * Mathf.Cos(ang), r * Mathf.Sin(ang));
        }

        Vector3 fCenter = new Vector3(0, 0, zPeak);
        Vector3 bCenter = new Vector3(0, 0, -zPeak);

        for (int i = 0; i < 10; i++)
        {
            int next = (i + 1) % 10;
            Vector3 pf1 = new Vector3(pts[i].x, pts[i].y, zRim);
            Vector3 pf2 = new Vector3(pts[next].x, pts[next].y, zRim);
            Vector3 pb1 = new Vector3(pts[i].x, pts[i].y, -zRim);
            Vector3 pb2 = new Vector3(pts[next].x, pts[next].y, -zRim);

            AddTriangle(verts, norms, uvs, tris, fCenter, pf1, pf2);
            AddTriangle(verts, norms, uvs, tris, bCenter, pb2, pb1);
            AddQuad(verts, norms, uvs, tris, pf1, pb1, pb2, pf2);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 9. Pedestal de Checkpoint
    public static Mesh CreateCheckpointMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Checkpoint_Shrine" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float r1 = 1.6f, h1 = 0.25f;
        for (int i = 0; i < 8; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 8f), a2 = (i + 1) * (Mathf.PI * 2f / 8f);
            Vector3 p1 = new Vector3(r1 * Mathf.Cos(a1), h1, r1 * Mathf.Sin(a1));
            Vector3 p2 = new Vector3(r1 * Mathf.Cos(a2), h1, r1 * Mathf.Sin(a2));
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, h1, 0), p1, p2, Vector3.up);
            AddQuad(verts, norms, uvs, tris, new Vector3(r1 * Mathf.Cos(a1), 0, r1 * Mathf.Sin(a1)), new Vector3(r1 * Mathf.Cos(a2), 0, r1 * Mathf.Sin(a2)), p2, p1);
        }

        float r2 = 0.9f, h2 = 0.75f;
        for (int i = 0; i < 8; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 8f), a2 = (i + 1) * (Mathf.PI * 2f / 8f);
            Vector3 p1 = new Vector3(r2 * Mathf.Cos(a1), h2, r2 * Mathf.Sin(a1));
            Vector3 p2 = new Vector3(r2 * Mathf.Cos(a2), h2, r2 * Mathf.Sin(a2));
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, h2, 0), p1, p2, Vector3.up);
            AddQuad(verts, norms, uvs, tris, new Vector3(r2 * Mathf.Cos(a1), h1, r2 * Mathf.Sin(a1)), new Vector3(r2 * Mathf.Cos(a2), h1, r2 * Mathf.Sin(a2)), p2, p1);
        }

        float cy = 1.8f, cr = 0.4f, ch = 0.7f;
        Vector3 top = new Vector3(0, cy + ch, 0);
        Vector3 bot = new Vector3(0, cy - ch, 0);
        Vector3[] cPts = {
            new Vector3(cr, cy, 0),
            new Vector3(0, cy, cr),
            new Vector3(-cr, cy, 0),
            new Vector3(0, cy, -cr)
        };
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            AddTriangle(verts, norms, uvs, tris, top, cPts[i], cPts[next]);
            AddTriangle(verts, norms, uvs, tris, bot, cPts[next], cPts[i]);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 10. Cubo de Energía
    public static Mesh CreateEnergyCubeMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Energy_CubeBlock" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float s = 0.5f, b = 0.08f, d = s - b;

        AddQuad(verts, norms, uvs, tris, new Vector3(-d, s, -d), new Vector3(d, s, -d), new Vector3(d, s, d), new Vector3(-d, s, d), Vector3.up);
        AddQuad(verts, norms, uvs, tris, new Vector3(-d, -s, d), new Vector3(d, -s, d), new Vector3(d, -s, -d), new Vector3(-d, -s, -d), Vector3.down);
        AddQuad(verts, norms, uvs, tris, new Vector3(-d, -d, s), new Vector3(d, -d, s), new Vector3(d, d, s), new Vector3(-d, d, s), Vector3.forward);
        AddQuad(verts, norms, uvs, tris, new Vector3(d, -d, -s), new Vector3(-d, -d, -s), new Vector3(-d, d, -s), new Vector3(d, d, -s), Vector3.back);
        AddQuad(verts, norms, uvs, tris, new Vector3(s, -d, -d), new Vector3(s, -d, d), new Vector3(s, d, d), new Vector3(s, d, -d), Vector3.right);
        AddQuad(verts, norms, uvs, tris, new Vector3(-s, -d, d), new Vector3(-s, -d, -d), new Vector3(-s, d, -d), new Vector3(-s, d, d), Vector3.left);

        AddQuad(verts, norms, uvs, tris, new Vector3(-d, s, d), new Vector3(d, s, d), new Vector3(d, d, s), new Vector3(-d, d, s));
        AddQuad(verts, norms, uvs, tris, new Vector3(d, s, -d), new Vector3(-d, s, -d), new Vector3(-d, d, -s), new Vector3(d, d, -s));
        AddQuad(verts, norms, uvs, tris, new Vector3(d, s, d), new Vector3(d, s, -d), new Vector3(s, d, -d), new Vector3(s, d, d));
        AddQuad(verts, norms, uvs, tris, new Vector3(-d, s, -d), new Vector3(-d, s, d), new Vector3(-s, d, d), new Vector3(-s, d, -d));

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 11. Arco Monumental de Victoria
    public static Mesh CreateVictoryArchMesh()
    {
        Mesh mesh = new Mesh { hideFlags = HideFlags.DontSave, name = "Victory_PortalArch" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float pillarW = 0.8f, pillarH = 6.0f, dist = 2.5f;

        foreach (float px in new float[] { -dist, dist })
        {
            float x1 = px - pillarW * 0.5f, x2 = px + pillarW * 0.5f;
            float z1 = -pillarW * 0.5f, z2 = pillarW * 0.5f;
            AddQuad(verts, norms, uvs, tris, new Vector3(x1, 0, z1), new Vector3(x2, 0, z1), new Vector3(x2, pillarH, z1), new Vector3(x1, pillarH, z1), Vector3.back);
            AddQuad(verts, norms, uvs, tris, new Vector3(x2, 0, z1), new Vector3(x2, 0, z2), new Vector3(x2, pillarH, z2), new Vector3(x2, pillarH, z1), Vector3.right);
            AddQuad(verts, norms, uvs, tris, new Vector3(x2, 0, z2), new Vector3(x1, 0, z2), new Vector3(x1, pillarH, z2), new Vector3(x2, pillarH, z2), Vector3.forward);
            AddQuad(verts, norms, uvs, tris, new Vector3(x1, 0, z2), new Vector3(x1, 0, z1), new Vector3(x1, pillarH, z1), new Vector3(x1, pillarH, z2), Vector3.left);
        }

        float bx1 = -dist - pillarW, bx2 = dist + pillarW;
        float by1 = pillarH, by2 = pillarH + 1.2f;
        float bz1 = -pillarW * 0.7f, bz2 = pillarW * 0.7f;
        AddQuad(verts, norms, uvs, tris, new Vector3(bx1, by1, bz1), new Vector3(bx2, by1, bz1), new Vector3(bx2, by2, bz1), new Vector3(bx1, by2, bz1), Vector3.back);
        AddQuad(verts, norms, uvs, tris, new Vector3(bx2, by1, bz2), new Vector3(bx1, by1, bz2), new Vector3(bx1, by2, bz2), new Vector3(bx2, by2, bz2), Vector3.forward);
        AddQuad(verts, norms, uvs, tris, new Vector3(bx1, by1, bz2), new Vector3(bx2, by1, bz2), new Vector3(bx2, by1, bz1), new Vector3(bx1, by1, bz1), Vector3.down);
        AddQuad(verts, norms, uvs, tris, new Vector3(bx1, by2, bz1), new Vector3(bx2, by2, bz1), new Vector3(bx2, by2, bz2), new Vector3(bx1, by2, bz2), Vector3.up);

        float portalCy = 3.5f, portalR = 1.9f;
        for (int i = 0; i < 16; i++)
        {
            float a1 = i * (Mathf.PI * 2f / 16f), a2 = (i + 1) * (Mathf.PI * 2f / 16f);
            Vector3 p1 = new Vector3(portalR * Mathf.Cos(a1), portalCy + portalR * Mathf.Sin(a1), 0);
            Vector3 p2 = new Vector3(portalR * Mathf.Cos(a2), portalCy + portalR * Mathf.Sin(a2), 0);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, portalCy, 0), p1, p2, Vector3.forward);
            AddTriangle(verts, norms, uvs, tris, new Vector3(0, portalCy, 0), p2, p1, Vector3.back);
        }

        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTriangle(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 p1, Vector3 p2, Vector3 p3, Vector3? normal = null)
    {
        int idx = verts.Count;
        verts.Add(p1);
        verts.Add(p2);
        verts.Add(p3);

        Vector3 n = normal ?? Vector3.Cross(p2 - p1, p3 - p1).normalized;
        norms.Add(n);
        norms.Add(n);
        norms.Add(n);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(0.5f, 1));

        tris.Add(idx);
        tris.Add(idx + 1);
        tris.Add(idx + 2);
    }

    private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3? normal = null)
    {
        int idx = verts.Count;
        verts.Add(p1);
        verts.Add(p2);
        verts.Add(p3);
        verts.Add(p4);

        Vector3 n = normal ?? Vector3.Cross(p2 - p1, p4 - p1).normalized;
        norms.Add(n);
        norms.Add(n);
        norms.Add(n);
        norms.Add(n);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        tris.Add(idx);
        tris.Add(idx + 1);
        tris.Add(idx + 2);

        tris.Add(idx);
        tris.Add(idx + 2);
        tris.Add(idx + 3);
    }
}
