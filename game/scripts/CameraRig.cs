using System;
using Godot;

namespace HeightFall.Game;

/// <summary>
/// Dota 式相机：透视，俯角 60°，视场角 35°，朝向正北，滚轮缩放 ±20%。
/// 默认锁定并平滑跟随英雄，按 Y 切换锁定；解锁时可以按住中键拖动地图、用方向键或鼠标贴边平移，
/// 按住空格临时回到英雄身上。
/// </summary>
public partial class CameraRig : Node3D
{
    const float PitchDegrees = 60f;
    const float FovDegrees = 35f;
    const float DefaultDistance = 22f;
    const float ZoomRange = 0.2f;
    const float ZoomStep = 0.05f;
    const float PanSpeed = 45f;
    const float EdgeMargin = 24f;
    const float FollowSharpness = 10f;
    const float ShakeDecay = 7f;

    Vector3 _target;
    float _distance = DefaultDistance;
    bool _mouseInWindow;
    bool _dragging;
    Vector2 _dragLast;
    float _shake;

    public Camera3D Camera { get; private set; } = null!;

    /// <summary>镜头跟随的位置（当前英雄）。</summary>
    public Func<Vector3>? Follow { get; set; }

    public bool Locked { get; set; } = true;
    public Rect2 Bounds { get; set; }
    public bool EdgeScroll { get; set; } = true;

    public override void _Ready()
    {
        Camera = new Camera3D { Fov = FovDegrees, Near = 0.1f, Far = 1000f, Current = true };
        AddChild(Camera);
        if (Follow is not null)
            Focus(Follow());

        // 鼠标离开窗口后，Godot 仍保留离开前的位置；只有鼠标在窗口内时才边缘滚屏
        GetWindow().MouseExited += () => _mouseInWindow = false;
    }

    public void Focus(Vector3 target)
    {
        _target = target;
        UpdateCamera();
    }

    /// <summary>不受缩放范围限制，用于截图俯瞰全图。</summary>
    public void SetDistance(float distance)
    {
        _distance = distance;
        UpdateCamera();
    }

    /// <summary>震屏，strength 为最大偏移（米），随后快速衰减；多次震动取最强的一次。</summary>
    public void Shake(float strength) => _shake = Mathf.Max(_shake, strength);

    public override void _Process(double delta)
    {
        if (_shake > 0.001f)
        {
            _shake *= Mathf.Exp(-ShakeDecay * (float)delta);
            UpdateCamera();
        }
        else
        {
            _shake = 0f;
        }

        var keys = Vector2.Zero;
        if (Input.IsKeyPressed(Key.Left)) keys.X -= 1;
        if (Input.IsKeyPressed(Key.Right)) keys.X += 1;
        if (Input.IsKeyPressed(Key.Up)) keys.Y -= 1;
        if (Input.IsKeyPressed(Key.Down)) keys.Y += 1;
        if (keys != Vector2.Zero)
            Locked = false;

        if (Follow is not null && Input.IsKeyPressed(Key.Space))
        {
            Focus(Follow());
            return;
        }
        if (Follow is not null && Locked)
        {
            var weight = 1f - Mathf.Exp(-FollowSharpness * (float)delta);
            Focus(_target.Lerp(Follow(), weight));
            return;
        }

        var move = keys;
        if (EdgeScroll && !_dragging && _mouseInWindow && DisplayServer.WindowIsFocused())
        {
            var mouse = GetViewport().GetMousePosition();
            var size = GetViewport().GetVisibleRect().Size;
            if (mouse.X <= EdgeMargin) move.X -= 1;
            if (mouse.X >= size.X - EdgeMargin) move.X += 1;
            if (mouse.Y <= EdgeMargin) move.Y -= 1;
            if (mouse.Y >= size.Y - EdgeMargin) move.Y += 1;
        }
        if (move != Vector2.Zero)
            Pan(move.Normalized() * PanSpeed * (float)delta);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseMotion)
            _mouseInWindow = true;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle:
                _dragging = middle.Pressed;
                _dragLast = middle.Position;
                if (_dragging)
                    Locked = false;
                break;
            case InputEventMouseMotion motion when _dragging:
                // 拖动时让鼠标下的那块地面跟着鼠标走
                if (GroundPoint(_dragLast) is { } from && GroundPoint(motion.Position) is { } to)
                    Pan(new Vector2(from.X - to.X, from.Z - to.Z));
                _dragLast = motion.Position;
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Zoom(-ZoomStep);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Zoom(ZoomStep);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Y }:
                Locked = !Locked;
                break;
        }
    }

    void Pan(Vector2 step)
    {
        _target.X = Mathf.Clamp(_target.X + step.X, Bounds.Position.X, Bounds.End.X);
        _target.Z = Mathf.Clamp(_target.Z + step.Y, Bounds.Position.Y, Bounds.End.Y);
        UpdateCamera();
    }

    /// <summary>屏幕点在镜头目标高度的水平面上对应的位置。</summary>
    Vector3? GroundPoint(Vector2 screen)
    {
        var origin = Camera.ProjectRayOrigin(screen);
        var dir = Camera.ProjectRayNormal(screen);
        if (dir.Y >= -0.001f)
            return null;
        return origin + dir * ((_target.Y - origin.Y) / dir.Y);
    }

    void Zoom(float amount)
    {
        _distance = Mathf.Clamp(_distance + amount * DefaultDistance,
            DefaultDistance * (1 - ZoomRange), DefaultDistance * (1 + ZoomRange));
        UpdateCamera();
    }

    void UpdateCamera()
    {
        var pitch = Mathf.DegToRad(PitchDegrees);
        var back = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch)) * _distance;
        var shake = _shake > 0f
            ? new Vector3(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * _shake
            : Vector3.Zero;
        Camera.LookAtFromPosition(_target + back + shake, _target + shake, Vector3.Up);
    }
}
