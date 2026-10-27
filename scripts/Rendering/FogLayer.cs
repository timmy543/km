using System.Collections.Generic;
using Godot;

namespace km;

/// <summary>
/// Neprozkoumaná krajina je černá jako v Knights and Merchants. Mesh kopíruje terén,
/// průhlednost je ve vrcholech, takže okraj mlhy je měkký. Kreslí se nad objekty.
/// </summary>
public partial class FogLayer : Node2D
{
    readonly MeshInstance2D mesh = new();
    float timer;

    public override void _Ready()
    {
        AddChild(mesh);
        Rebuild();
    }

    public override void _Process(double delta)
    {
        timer -= (float)delta;
        if (timer > 0f) return;
        timer = 0.25f;
        if (!Main.Instance.Map.FogChanged) return;
        Rebuild();
    }

    public void Rebuild()
    {
        var map = Main.Instance.Map;
        map.FogChanged = false;
        int w = map.Width, h = map.Height, vw = w + 1;
        var verts = new Vector2[vw * (h + 1)];
        var cols = new Color[verts.Length];
        for (int j = 0; j <= h; j++)
        for (int i = 0; i <= w; i++)
        {
            int idx = j * vw + i;
            verts[idx] = Iso.ToScreen(new Vector2(i, j)) - new Vector2(0, map.Heights[i, j]);
            float e = map.Explored[i, j];
            cols[idx] = new Color(0, 0, 0, 1f - e * e * (3f - 2f * e));
        }

        var idxs = new List<int>();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int a = y * vw + x, b = a + 1, c = a + vw + 1, d = a + vw;
            if (cols[a].A + cols[b].A + cols[c].A + cols[d].A < 0.01f) continue;
            idxs.AddRange(new[] { a, b, c, a, c, d });
        }

        if (idxs.Count == 0)
        {
            mesh.Mesh = null;
            return;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        arrays[(int)Mesh.ArrayType.Index] = idxs.ToArray();
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.Mesh = m;
    }
}
