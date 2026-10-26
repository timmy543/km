using Godot;

namespace km;

/// <summary>
/// Terén jako jeden mesh s výškami ve vrcholech. Shader míchá 4 textury (tráva, hlína, skála,
/// písek) podle vah ve vrcholech, okraje rozbíjí šumem, přidává velkoplošnou variaci barvy
/// a pomalu putující stíny mraků. Voda je samostatná vrstva s hloubkou a pěnou u břehu.
///
/// Kódování barvy vrcholu terénu: R = tráva, G = hlína, B = skála (písek = zbytek do 1),
/// A = osvětlení / 2 (stínování svahů a lesa).
/// </summary>
public partial class TerrainView : Node2D
{
    const string GroundShader = @"
shader_type canvas_item;
uniform sampler2D grass_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D dirt_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D rock_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D sand_tex : repeat_enable, filter_linear_mipmap;
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;

void fragment() {
    vec2 g = UV;
    vec4 v = COLOR;
    vec2 g1 = g * 0.25;
    vec2 g2 = g * 0.085 + vec2(0.37, 0.71);

    float n1 = texture(noise_tex, g * 0.11).r;
    float n2 = texture(noise_tex, g * 0.23 + vec2(0.5, 0.2)).r;
    vec4 w = vec4(v.r, v.g, v.b, max(0.0, 1.0 - v.r - v.g - v.b));
    w *= vec4(0.6 + n1 * 0.8, 1.4 - n1 * 0.8, 0.6 + n2 * 0.8, 1.4 - n2 * 0.8);
    w = w * w * w * w;
    w /= max(w.r + w.g + w.b + w.a, 0.0001);

    vec3 grass = mix(texture(grass_tex, g1).rgb, texture(grass_tex, g2).rgb, 0.35);
    vec3 dirt  = mix(texture(dirt_tex, g1).rgb,  texture(dirt_tex, g2).rgb, 0.35);
    vec3 rock  = mix(texture(rock_tex, g1).rgb,  texture(rock_tex, g2).rgb, 0.3);
    vec3 sand  = mix(texture(sand_tex, g1).rgb,  texture(sand_tex, g2).rgb, 0.35);
    vec3 c = grass * w.r + dirt * w.g + rock * w.b + sand * w.a;

    // velké skvrny – sušší / svěžejší tráva
    float macro = texture(noise_tex, g * 0.022 + vec2(0.13, 0.57)).r;
    c *= mix(0.86, 1.12, macro);
    c = mix(c, c * vec3(1.08, 1.04, 0.8), smoothstep(0.55, 0.8, texture(noise_tex, g * 0.031 + vec2(0.7, 0.1)).r) * 0.5);

    c *= v.a * 2.0;

    // stíny mraků
    float cloud = texture(noise_tex, g * 0.016 + vec2(TIME * 0.0035, TIME * 0.0022)).r;
    c *= 1.0 - smoothstep(0.55, 0.75, cloud) * 0.17;

    COLOR = vec4(c, 1.0);
}
";

    const string WaterShader = @"
shader_type canvas_item;
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;

void fragment() {
    float a = COLOR.a;      // hladký okraj břehu (0.5 = čára břehu)
    float depth = COLOR.r;  // 0 u břehu, 1 hluboko
    vec2 g = UV;
    float n1 = texture(noise_tex, g * 0.06 + vec2(TIME * 0.010, TIME * 0.006)).r;
    float n2 = texture(noise_tex, g * 0.09 + vec2(-TIME * 0.008, TIME * 0.011)).r;
    float wv = (n1 + n2) * 0.5;

    vec3 shallow = vec3(0.30, 0.55, 0.58);
    vec3 deep = vec3(0.08, 0.22, 0.36);
    vec3 col = mix(shallow, deep, smoothstep(0.0, 0.8, depth));
    col *= mix(0.88, 1.12, smoothstep(0.3, 0.7, wv));

    // odlesky
    float glint = smoothstep(0.63, 0.65, wv) - smoothstep(0.65, 0.69, wv);
    col += glint * 0.07;

    // pěna u břehu, pulzuje
    float foamBand = smoothstep(0.66, 0.52, a);
    float foamNoise = texture(noise_tex, g * 0.35 + vec2(TIME * 0.03, 0.0)).r;
    float pulse = 0.6 + 0.4 * sin(TIME * 1.4 + foamNoise * 6.0);
    col = mix(col, vec3(0.86, 0.92, 0.90), foamBand * smoothstep(0.35, 0.65, foamNoise) * pulse * 0.75);

    float alpha = smoothstep(0.47, 0.53, a) * mix(0.72, 0.95, smoothstep(0.0, 0.5, depth));
    COLOR = vec4(col, alpha);
}
";

    GameMap map = null!;
    readonly MeshInstance2D ground = new();
    readonly MeshInstance2D water = new();

    public void Init(GameMap m)
    {
        map = m;
        var gm = new ShaderMaterial { Shader = new Shader { Code = GroundShader } };
        gm.SetShaderParameter("grass_tex", TerrainArt.Grass);
        gm.SetShaderParameter("dirt_tex", TerrainArt.Dirt);
        gm.SetShaderParameter("rock_tex", TerrainArt.Rock);
        gm.SetShaderParameter("sand_tex", TerrainArt.Sand);
        gm.SetShaderParameter("noise_tex", TerrainArt.Noise);
        ground.Material = gm;
        AddChild(ground);

        var wm = new ShaderMaterial { Shader = new Shader { Code = WaterShader } };
        wm.SetShaderParameter("noise_tex", TerrainArt.Noise);
        water.Material = wm;
        AddChild(water);

        Rebuild();
    }

    public void Rebuild()
    {
        int w = map.Width, h = map.Height, vw = w + 1;
        int vc = vw * (h + 1);
        var verts = new Vector2[vc];
        var groundCols = new Color[vc];
        var waterCols = new Color[vc];
        var uv = new Vector2[vc];
        var weights = SmoothedWeights();

        for (int j = 0; j <= h; j++)
        for (int i = 0; i <= w; i++)
        {
            int idx = j * vw + i;
            verts[idx] = Iso.ToScreen(new Vector2(i, j)) - new Vector2(0, map.Heights[i, j]);
            uv[idx] = new Vector2(i, j);
            var wt = weights[i, j];
            groundCols[idx] = new Color(wt.X, wt.Y, wt.Z, Mathf.Clamp(Light(i, j) * 0.5f, 0f, 1f));
            float depth = Mathf.Clamp((GameMap.WaterLevel - map.VertexElev[i, j]) * 2.5f, 0f, 1f);
            waterCols[idx] = new Color(depth, 1, 1, WaterAlpha(i, j));
        }

        var groundIdx = new System.Collections.Generic.List<int>(w * h * 6);
        var waterIdx = new System.Collections.Generic.List<int>();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int a = y * vw + x, b = a + 1, c = a + vw + 1, d = a + vw;
            groundIdx.AddRange(new[] { a, b, c, a, c, d });
            bool wet = waterCols[a].A > 0f || waterCols[b].A > 0f || waterCols[c].A > 0f || waterCols[d].A > 0f;
            if (wet) waterIdx.AddRange(new[] { a, b, c, a, c, d });
        }

        ground.Mesh = MakeMesh(verts, groundCols, uv, groundIdx.ToArray());
        water.Mesh = waterIdx.Count > 0 ? MakeMesh(verts, waterCols, uv, waterIdx.ToArray()) : null;
    }

    static ArrayMesh MakeMesh(Vector2[] verts, Color[] cols, Vector2[] uvs, int[] idx)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = idx;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>
    /// Lineární v nadmořské výšce, takže se interpolací po trojúhelnících dostane
    /// hladká vrstevnice břehu (shader ji pak ostře ořízne na 0.5) místo schodů po dlaždicích.
    /// </summary>
    float WaterAlpha(int i, int j) => Mathf.Clamp(0.5f + (GameMap.WaterLevel - map.VertexElev[i, j]) * 6f, 0f, 1f);

    /// <summary>Váhy (tráva, hlína, skála) rozmazané přes sousední vrcholy – měkčí přechody.</summary>
    Vector3[,] SmoothedWeights()
    {
        int w = map.Width, h = map.Height;
        var raw = new Vector3[w + 1, h + 1];
        for (int j = 0; j <= h; j++)
        for (int i = 0; i <= w; i++)
            raw[i, j] = map.VertexGround[i, j] switch
            {
                Terrain.Grass => new Vector3(1, 0, 0),
                Terrain.Dirt => new Vector3(0, 1, 0),
                Terrain.Rock => new Vector3(0, 0, 1),
                _ => Vector3.Zero, // písek a dno
            };

        var res = new Vector3[w + 1, h + 1];
        for (int j = 0; j <= h; j++)
        for (int i = 0; i <= w; i++)
        {
            var sum = raw[i, j] * 2f;
            float n = 2f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = i + dx, y = j + dy;
                if ((dx == 0 && dy == 0) || x < 0 || y < 0 || x > w || y > h) continue;
                sum += raw[x, y];
                n++;
            }
            res[i, j] = sum / n;
        }
        return res;
    }

    float Light(int i, int j)
    {
        // stínování svahu: světlo přichází z levého horního rohu obrazovky (směr -X v mřížce)
        float hx = map.Heights[System.Math.Min(i + 1, map.Width), j] - map.Heights[System.Math.Max(i - 1, 0), j];
        float hy = map.Heights[i, System.Math.Min(j + 1, map.Height)] - map.Heights[i, System.Math.Max(j - 1, 0)];
        float light = 1f + 0.04f * (hx + 0.4f * hy);

        // lesní půda je tmavší
        int trees = 0;
        for (int dy = -1; dy <= 0; dy++)
        for (int dx = -1; dx <= 0; dx++)
        {
            int x = i + dx, y = j + dy;
            if (x >= 0 && y >= 0 && x < map.Width && y < map.Height && map.Trees[x, y] != null) trees++;
        }
        light *= 1f - 0.07f * trees;

        // dno pod vodou tmavší
        if (map.VertexGround[i, j] == Terrain.Water) light *= 0.7f;
        return Mathf.Clamp(light, 0.55f, 1.45f);
    }
}
