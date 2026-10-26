using Godot;

namespace km;

/// <summary>Celoobrazovkové doladění: lehce teplejší barvy, víc sytosti a kontrastu, vinětace.</summary>
public partial class PostFx : CanvasLayer
{
    const string Shader = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;

void fragment() {
    vec3 c = texture(screen_tex, SCREEN_UV).rgb;
    float l = dot(c, vec3(0.299, 0.587, 0.114));
    c = mix(vec3(l), c, 1.04);
    c = (c - 0.5) * 1.06 + 0.5;
    c *= vec3(1.035, 1.0, 0.94);
    vec2 d = (SCREEN_UV - 0.5) * vec2(1.0, 0.85);
    c *= mix(0.74, 1.0, smoothstep(0.82, 0.32, length(d)));
    COLOR = vec4(c, 1.0);
}
";

    public override void _Ready()
    {
        Layer = 1;
        var rect = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = Shader } },
        };
        AddChild(rect);
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    }
}
