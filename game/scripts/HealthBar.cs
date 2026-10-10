using Godot;

namespace HeightFall.Game;

/// <summary>正对相机的血条，颜色区分敌我。</summary>
public partial class HealthBar : MeshInstance3D
{
    const float BarHeight = 0.14f;

    static Shader? _shader;
    static ShaderMaterial? _material;

    public float Width { get; set; } = 1f;
    public Color BarColor { get; set; } = Palette.Ally;

    public override void _Ready()
    {
        _shader ??= GD.Load<Shader>("res://shaders/health_bar.gdshader");
        _material ??= new ShaderMaterial { Shader = _shader };
        _material.SetShaderParameter("back_color", Palette.Neutral);

        Mesh = new QuadMesh { Size = new Vector2(Width, BarHeight) };
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
        SetInstanceShaderParameter("bar_color", BarColor);
        SetFill(1f);
    }

    public void SetFill(float fraction) => SetInstanceShaderParameter("fill", Mathf.Clamp(fraction, 0f, 1f));
}
