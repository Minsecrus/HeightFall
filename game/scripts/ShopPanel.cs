using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HeightFall.Sim;

namespace HeightFall.Game;

/// <summary>共享的装备商店。全屏遮罩接收输入，购买和出售都交给对局逻辑校验。</summary>
public partial class ShopPanel : Control
{
    const float Gap = 12f;
    static readonly string[] CategoryNames = ["全部", "攻击", "防御", "移动"];
    static readonly Color Border = new("#D9D9D7");
    static readonly Color Muted = new("#898986");
    static readonly Color Track = new("#F1F1EF");

    public World World { get; set; } = null!;
    public PlayerController Player { get; set; } = null!;
    public bool TouchMode { get; set; }
    public bool IsOpen => Visible;
    public event Action? Closed;

    readonly List<int> _catalog = new();
    readonly Rect2[] _categoryButtons = new Rect2[4];
    readonly Rect2[] _inventorySlots = new Rect2[Unit.InventoryCapacity];
    readonly List<Rect2> _itemCards = new();
    Rect2 _card;
    Rect2 _detail;
    Rect2 _buyButton;
    Rect2 _sellButton;
    Rect2 _closeButton;
    Rect2 _heroButton;
    Vector2 _logicalSize = new(1000, 640);
    float _cardScale = 1f;
    bool _portrait;
    int _category;
    int _selectedCatalog;
    int _selectedInventory = -1;
    Unit? _shopHero;
    (string Kind, int Index)? _mouseTarget;
    Vector2 _mousePress;
    int _touchFinger = -1;
    (string Kind, int Index)? _touchTarget;
    Vector2 _touchPress;
    long _touchCancelSerial;
    bool _touchMoved;
    string? _feedback;
    bool _feedbackError;
    double _feedbackLeft;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        FocusMode = FocusModeEnum.All;
        Visible = false;
        SetLayout(GetViewport().GetVisibleRect());
    }

    public void Open()
    {
        ClearInteraction();
        _shopHero = Player.Hero;
        _selectedInventory = -1;
        _feedback = null;
        RefreshCatalog();
        Visible = true;
        GrabFocus();
        QueueRedraw();
    }

    public void Close()
    {
        if (!Visible) return;
        ClearInteraction();
        Visible = false;
        ReleaseFocus();
        Closed?.Invoke();
    }

    /// <summary>available 是 HUD 的可用 viewport 区域；遮罩覆盖整个区域，卡片在其中居中缩放。</summary>
    public void SetLayout(Rect2 available)
    {
        ClearInteraction();
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Position = available.Position;
        Size = available.Size;
        Scale = Vector2.One;
        _portrait = available.Size.Y > available.Size.X || available.Size.X < 740f;
        _logicalSize = _portrait ? new Vector2(720, 1180) : new Vector2(1000, 640);
        var margin = TouchMode ? 20f : 24f;
        var usable = new Vector2(Mathf.Max(1f, Size.X - margin * 2), Mathf.Max(1f, Size.Y - margin * 2));
        _cardScale = Mathf.Max(0.1f, Mathf.Min(usable.X / _logicalSize.X, usable.Y / _logicalSize.Y));
        _cardScale = Mathf.Min(_cardScale, TouchMode ? 1.4f : 1f);
        _card = new Rect2((Size - _logicalSize * _cardScale) / 2f, _logicalSize * _cardScale);
        _closeButton = new Rect2(_logicalSize.X - 68, 16, 48, 48);
        _heroButton = new Rect2(_portrait ? 206 : 248, 16, _portrait ? 148 : 220, 48);
        var categoryWidth = (_portrait ? 672f : 548f) / 4f;
        for (var i = 0; i < _categoryButtons.Length; i++)
            _categoryButtons[i] = new Rect2(24 + i * categoryWidth, 90, categoryWidth - 6, 48);
        _detail = _portrait ? new Rect2(24, 558, 672, 270) : new Rect2(596, 90, 380, 334);
        _buyButton = _portrait ? new Rect2(402, 742, 270, 64) : new Rect2(612, 348, 348, 56);
        _sellButton = _portrait ? new Rect2(402, 1040, 270, 64) : new Rect2(612, 510, 348, 56);
        var slotSize = _portrait ? 102f : 80f;
        var slotY = _portrait ? 904f : 488f;
        for (var i = 0; i < _inventorySlots.Length; i++)
            _inventorySlots[i] = new Rect2(24 + i * (slotSize + Gap), slotY, slotSize, slotSize);
        RefreshCatalog();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsOpen) return;
        if (_shopHero != Player.Hero)
        {
            ClearInteraction();
            _shopHero = Player.Hero;
            _selectedInventory = -1;
            _feedback = null;
        }
        if (_selectedInventory >= Player.Hero.Items.Count) _selectedInventory = -1;
        if (_touchFinger >= 0 && TouchPlatform.ReadCancelSerial() != _touchCancelSerial)
            ClearInteraction();
        if (_feedback is not null && (_feedbackLeft -= delta) <= 0) _feedback = null;
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if ((long)what is NotificationApplicationFocusOut or NotificationWMWindowFocusOut or NotificationApplicationPaused
            || what == NotificationVisibilityChanged && !IsVisibleInTree())
            ClearInteraction();
    }

    public override bool _HasPoint(Vector2 point) => IsOpen && new Rect2(Vector2.Zero, Size).HasPoint(point);

    public override void _Input(InputEvent e)
    {
        if (!IsOpen || !IsVisibleInTree()) return;
        if (e is InputEventKey key)
        {
            GetViewport().SetInputAsHandled();
            if (key.Pressed && !key.Echo && key.Keycode == Key.Escape) Close();
            return;
        }
        if (e is InputEventMouse { Device: -1 })
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventScreenTouch touch)
        {
            // 未经过商店按下的松手（例如打开商店的那根手指）始终只消费，不执行操作。
            GetViewport().SetInputAsHandled();
            var point = ToCard(GetGlobalTransformWithCanvas().AffineInverse() * touch.Position);
            var cancelSerial = TouchPlatform.ReadCancelSerial();
            if (_touchFinger >= 0 && cancelSerial != _touchCancelSerial) ClearInteraction();
            if (touch.Index == _touchFinger && (!touch.Pressed || touch.Canceled))
            {
                var target = _touchTarget;
                var activate = !touch.Canceled && !_touchMoved && cancelSerial == _touchCancelSerial
                    && point.DistanceTo(_touchPress) <= 24f && target is not null && HitTest(point) == target;
                ClearInteraction();
                if (activate) Activate(target!.Value);
            }
            else if (touch.Pressed && !touch.Canceled && _touchFinger < 0)
            {
                _touchFinger = touch.Index;
                _touchTarget = HitTest(point);
                _touchPress = point;
                _touchCancelSerial = cancelSerial;
                _touchMoved = false;
            }
            return;
        }
        if (e is InputEventScreenDrag drag)
        {
            GetViewport().SetInputAsHandled();
            if (drag.Index == _touchFinger)
            {
                var point = ToCard(GetGlobalTransformWithCanvas().AffineInverse() * drag.Position);
                if (point.DistanceTo(_touchPress) > 24f) _touchMoved = true;
                if (TouchPlatform.ReadCancelSerial() != _touchCancelSerial) ClearInteraction();
            }
            return;
        }
        // 普通鼠标交给 GUI，Stop + AcceptEvent 屏蔽地图操作；HUD 在打开时暂停镜头输入和轮询。
        if (e is not InputEventMouse) GetViewport().SetInputAsHandled();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (!IsOpen) return;
        AcceptEvent();
        if (e is InputEventMouse { Device: -1 }) return;
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click) return;
        var point = ToCard(click.Position);
        if (click.Pressed)
        {
            _mouseTarget = HitTest(point);
            _mousePress = point;
            return;
        }
        var target = _mouseTarget;
        _mouseTarget = null;
        if (target is not null && HitTest(point) == target && point.DistanceTo(_mousePress) <= 16f)
            Activate(target.Value);
    }

    Vector2 ToCard(Vector2 local) => (local - _card.Position) / _cardScale;

    void ClearInteraction()
    {
        _mouseTarget = null;
        _touchFinger = -1;
        _touchTarget = null;
        _touchMoved = false;
    }

    void RefreshCatalog()
    {
        _catalog.Clear();
        _itemCards.Clear();
        for (var i = 0; i < Items.All.Count; i++)
        {
            var item = Items.All[i];
            if (_category > 0 && (int)item.Category != _category - 1) continue;
            _catalog.Add(i);
            var index = _catalog.Count - 1;
            var width = _portrait ? 216f : 174.66f;
            var height = _portrait ? 120f : 84f;
            _itemCards.Add(new Rect2(24 + index % 3 * (width + Gap), 154 + index / 3 * (height + Gap), width, height));
        }
        if (_catalog.Count > 0 && !_catalog.Contains(_selectedCatalog)) _selectedCatalog = _catalog[0];
    }

    (string Kind, int Index)? HitTest(Vector2 point)
    {
        // 保留原矩形的命中，扩大的区域不能抢走相邻装备格或其他按钮的正常点击。
        var exact = HitExact(point);
        if (exact is not null || !TouchMode) return exact;
        var minimum = TouchHitMinimum();
        var pixelsPerUnit = new Vector2(44f / minimum.X, 44f / minimum.Y);
        (string Kind, int Index)? nearest = null;
        var nearestDistance = float.PositiveInfinity;

        void Consider(Rect2 rect, string kind, int index)
        {
            var expandedSize = new Vector2(Mathf.Max(rect.Size.X, minimum.X), Mathf.Max(rect.Size.Y, minimum.Y));
            if (!new Rect2(rect.GetCenter() - expandedSize / 2f, expandedSize).HasPoint(point)) return;
            var distance = ((point - rect.GetCenter()) * pixelsPerUnit).LengthSquared();
            if (distance >= nearestDistance) return;
            nearestDistance = distance;
            nearest = (kind, index);
        }

        // 扩展区域相交时按最近中心划分，每个位置只会命中一个目标。
        Consider(_closeButton, "close", 0);
        Consider(_heroButton, "hero", 0);
        for (var i = 0; i < _categoryButtons.Length; i++) Consider(_categoryButtons[i], "category", i);
        for (var i = 0; i < _itemCards.Count; i++) Consider(_itemCards[i], "item", _catalog[i]);
        for (var i = 0; i < _inventorySlots.Length; i++) Consider(_inventorySlots[i], "inventory", i);
        Consider(_buyButton, "buy", 0);
        Consider(_sellButton, "sell", 0);
        return nearest;
    }

    Vector2 TouchHitMinimum()
    {
        var transform = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
        var pixelsPerUnit = new Vector2(transform.X.Length(), transform.Y.Length()) * _cardScale;
        if (OS.HasFeature("web"))
        {
            // FinalTransform 到达 canvas backing pixels；高 DPI 浏览器还要换算成 CSS pixels。
            var ratios = JavaScriptBridge.Eval("(() => { const c = document.getElementById('canvas'); if (!c) return '1,1'; const r = c.getBoundingClientRect(); return [r.width > 0 ? c.width / r.width : 1, r.height > 0 ? c.height / r.height : 1].join(','); })()").AsString().Split(',');
            if (ratios.Length == 2
                && float.TryParse(ratios[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var ratioX)
                && float.TryParse(ratios[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ratioY)
                && ratioX > 0 && ratioY > 0)
                pixelsPerUnit /= new Vector2(ratioX, ratioY);
        }
        return new Vector2(44f / Mathf.Max(0.001f, pixelsPerUnit.X), 44f / Mathf.Max(0.001f, pixelsPerUnit.Y));
    }

    (string Kind, int Index)? HitExact(Vector2 point)
    {
        if (_closeButton.HasPoint(point)) return ("close", 0);
        if (_heroButton.HasPoint(point)) return ("hero", 0);
        for (var i = 0; i < _categoryButtons.Length; i++)
            if (_categoryButtons[i].HasPoint(point)) return ("category", i);
        for (var i = 0; i < _itemCards.Count; i++)
            if (_itemCards[i].HasPoint(point)) return ("item", _catalog[i]);
        for (var i = 0; i < _inventorySlots.Length; i++)
            if (_inventorySlots[i].HasPoint(point)) return ("inventory", i);
        if (_buyButton.HasPoint(point)) return ("buy", 0);
        if (_sellButton.HasPoint(point)) return ("sell", 0);
        return null;
    }

    ItemDef? SelectedItem()
    {
        if (_selectedInventory >= 0 && _selectedInventory < Player.Hero.Items.Count)
            return Player.Hero.Items[_selectedInventory];
        return (uint)_selectedCatalog < (uint)Items.All.Count ? Items.All[_selectedCatalog] : null;
    }

    string? BuyReason(ItemDef item)
    {
        var reason = World.CheckBuy(Player.Hero, item);
        return reason == "金币不足" ? $"还差 {Mathf.Max(0, item.Cost - Player.Hero.Gold)} 金币" : reason;
    }

    void Activate((string Kind, int Index) target)
    {
        switch (target)
        {
            case ("close", _):
                Close();
                return;
            case ("hero", _):
                ClearInteraction();
                Player.SwitchHero();
                _shopHero = Player.Hero;
                _selectedInventory = -1;
                _feedback = null;
                break;
            case ("category", var category):
                _category = category;
                _selectedInventory = -1;
                RefreshCatalog();
                break;
            case ("item", var item):
                _selectedCatalog = item;
                _selectedInventory = -1;
                break;
            case ("inventory", var slot):
                _selectedInventory = slot < Player.Hero.Items.Count ? slot : -1;
                break;
            case ("buy", _):
                if (SelectedItem() is not { } selected) break;
                var reason = BuyReason(selected);
                if (reason is not null) Feedback(reason, error: true);
                else if (World.BuyItem(Player.Hero, selected)) Feedback($"已购买 {selected.Name}");
                else Feedback(BuyReason(selected) ?? "购买失败", error: true);
                break;
            case ("sell", _):
                if (_selectedInventory < 0 || _selectedInventory >= Player.Hero.Items.Count)
                {
                    Feedback("先选择要出售的装备", error: true);
                    break;
                }
                var sold = Player.Hero.Items[_selectedInventory];
                var value = HeightFall.Sim.World.ItemSellValue(sold);
                if (World.SellItem(Player.Hero, _selectedInventory))
                {
                    _selectedInventory = -1;
                    Feedback($"已出售 {sold.Name}，获得 {value} 金币");
                }
                else Feedback("出售失败", error: true);
                break;
        }
        QueueRedraw();
    }

    void Feedback(string text, bool error = false)
    {
        _feedback = text;
        _feedbackError = error;
        _feedbackLeft = 3.5;
    }

    public override void _Draw()
    {
        if (!IsOpen) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.14f, 0.16f, 0.18f, 0.42f));
        DrawRect(new Rect2(_card.Position + new Vector2(0, 7), _card.Size), new Color(0f, 0f, 0f, 0.12f));
        DrawSetTransform(_card.Position, 0, Vector2.One * _cardScale);
        DrawRect(new Rect2(Vector2.Zero, _logicalSize), Colors.White);
        DrawRect(new Rect2(Vector2.Zero, _logicalSize), Border, filled: false, width: 1.5f);
        Text(new Vector2(24, 49), "装备商店", _portrait ? 32 : 28, Palette.Neutral);
        Button(_heroButton, $"切换 {Player.Hero.Def.Name}", false, true, _portrait ? 22 : 20);
        var goldX = _portrait ? 376f : 690f;
        Gold(new Vector2(goldX, 37), Player.Hero.Gold, _portrait ? 28 : 24);
        DrawLine(new Vector2(24, 76), new Vector2(_logicalSize.X - 24, 76), Border, 1f, true);
        DrawLine(_closeButton.GetCenter() + new Vector2(-9, -9), _closeButton.GetCenter() + new Vector2(9, 9), Muted, 2.5f, true);
        DrawLine(_closeButton.GetCenter() + new Vector2(-9, 9), _closeButton.GetCenter() + new Vector2(9, -9), Muted, 2.5f, true);
        for (var i = 0; i < _categoryButtons.Length; i++)
            Button(_categoryButtons[i], CategoryNames[i], _category == i, true, _portrait ? 24 : 20);
        for (var i = 0; i < _itemCards.Count; i++) DrawItemCard(_itemCards[i], Items.All[_catalog[i]], _catalog[i]);
        DrawDetail();
        DrawInventory();
        if (_feedback is not null)
            Text(new Vector2(24, _logicalSize.Y - 28), _feedback, _portrait ? 24 : 19,
                _feedbackError ? Palette.Enemy : Palette.Ally, _logicalSize.X - 48);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    void DrawItemCard(Rect2 rect, ItemDef item, int index)
    {
        var selected = _selectedInventory < 0 && _selectedCatalog == index;
        DrawRect(rect, selected ? Palette.Ally.Lightened(0.9f) : Track);
        DrawRect(rect, selected ? Palette.Ally : Border, filled: false, width: selected ? 2f : 1f);
        if (_portrait)
        {
            DrawItemIcon(item, new Vector2(rect.GetCenter().X, rect.Position.Y + 32), 22f);
            Text(new Vector2(rect.Position.X + 8, rect.Position.Y + 76), item.Name, 25, Palette.Neutral,
                rect.Size.X - 16, HorizontalAlignment.Center);
            Text(new Vector2(rect.Position.X + 8, rect.Position.Y + 106), $"{item.Cost}", 22,
                Player.Hero.Gold >= item.Cost ? Palette.Gold : Muted, rect.Size.X - 16, HorizontalAlignment.Center);
        }
        else
        {
            DrawItemIcon(item, rect.Position + new Vector2(28, 42), 19f);
            Text(rect.Position + new Vector2(56, 34), item.Name, 19, Palette.Neutral, rect.Size.X - 64);
            Text(rect.Position + new Vector2(56, 62), $"{item.Cost}", 18, Player.Hero.Gold >= item.Cost ? Palette.Gold : Muted);
        }
    }

    void DrawDetail()
    {
        DrawRect(_detail, Track);
        if (SelectedItem() is not { } item) return;
        var x = _detail.Position.X + 18;
        var top = _detail.Position.Y;
        DrawItemIcon(item, new Vector2(x + 21, top + 37), 22f);
        Text(new Vector2(x + 58, top + 46), item.Name, _portrait ? 30 : 26, Palette.Neutral, _detail.Size.X - 96);
        var stats = ItemStats(item);
        for (var i = 0; i < stats.Count; i++)
        {
            var column = _portrait ? i % 2 : 0;
            var row = _portrait ? i / 2 : i;
            Text(new Vector2(x + column * 310, top + 90 + row * (_portrait ? 36 : 32)), stats[i], _portrait ? 24 : 21,
                Palette.Neutral, _portrait ? 294 : _detail.Size.X - 36);
        }
        var reason = BuyReason(item);
        var priceAt = _portrait ? new Vector2(x, top + 225) : new Vector2(x, top + 236);
        Gold(priceAt - new Vector2(0, 7), item.Cost, _portrait ? 26 : 22);
        Button(_buyButton, "购买", true, reason is null, _portrait ? 27 : 23);
        if (reason is not null)
        {
            var at = _portrait ? new Vector2(x, top + 261) : new Vector2(_detail.Position.X, _detail.End.Y + 27);
            Text(at, reason, _portrait ? 22 : 19, Palette.Enemy, _detail.Size.X - (_portrait ? 18 : 0));
        }
    }

    void DrawInventory()
    {
        var titleY = _portrait ? 866f : 464f;
        Text(new Vector2(24, titleY), $"已有装备  {Player.Hero.Items.Count} / {Unit.InventoryCapacity}", _portrait ? 27 : 22, Palette.Neutral);
        for (var i = 0; i < _inventorySlots.Length; i++)
        {
            var rect = _inventorySlots[i];
            var selected = i == _selectedInventory;
            DrawRect(rect, selected ? Palette.Ally.Lightened(0.9f) : Track);
            DrawRect(rect, selected ? Palette.Ally : Border, filled: false, width: selected ? 2.5f : 1f);
            Text(rect.Position + new Vector2(8, 20), $"{i + 1}", 16, Muted);
            if (i < Player.Hero.Items.Count) DrawItemIcon(Player.Hero.Items[i], rect.GetCenter(), _portrait ? 28 : 22);
        }
        var canSell = _selectedInventory >= 0 && _selectedInventory < Player.Hero.Items.Count;
        var sellText = canSell ? $"出售  {HeightFall.Sim.World.ItemSellValue(Player.Hero.Items[_selectedInventory])}" : "出售";
        Button(_sellButton, sellText, false, canSell, _portrait ? 27 : 23);
        var hintAt = _portrait ? new Vector2(24, 1077) : new Vector2(612, 595);
        Text(hintAt, canSell ? Player.Hero.Items[_selectedInventory].Name : "选择装备后出售", _portrait ? 23 : 18, Muted,
            _portrait ? 360 : 348);
    }

    static List<string> ItemStats(ItemDef item)
    {
        var result = new List<string>(5);
        if (item.HpBonus != 0) result.Add($"生命 +{item.HpBonus:0.#}");
        if (item.ManaBonus != 0) result.Add($"法力 +{item.ManaBonus:0.#}");
        if (item.DamageBonus != 0) result.Add($"攻击 +{item.DamageBonus:0.#}");
        if (item.ArmorBonus != 0) result.Add($"护甲 +{item.ArmorBonus:0.#}");
        if (item.MoveSpeedBonus != 0) result.Add($"移速 +{item.MoveSpeedBonus:0.##}");
        return result;
    }

    void DrawItemIcon(ItemDef item, Vector2 center, float radius)
    {
        var icon = item.Icon switch
        {
            "sword" or "axe" or "spear" => "arrow",
            "shield" or "armor" => "purify",
            "boots" => "sprint",
            "ruby" => "heal",
            "sapphire" => "restore",
            _ => item.Icon,
        };
        SkillIcons.Draw(this, icon, center, radius, item.Category == ItemCategory.Movement ? Palette.Ally : Palette.Neutral);
    }

    void Button(Rect2 rect, string label, bool primary, bool enabled, int fontSize)
    {
        DrawRect(rect, enabled && primary ? Palette.Ally : enabled ? Colors.White : Border.Lightened(0.45f));
        DrawRect(rect, enabled && primary ? Palette.Ally : Border, filled: false, width: 1.5f);
        Text(new Vector2(rect.Position.X, rect.GetCenter().Y + fontSize * 0.35f), label, fontSize,
            enabled && primary ? Colors.White : enabled ? Palette.Neutral : Muted, rect.Size.X, HorizontalAlignment.Center);
    }

    void Gold(Vector2 center, int value, int fontSize)
    {
        DrawColoredPolygon([center + new Vector2(0, -9), center + new Vector2(7, 0), center + new Vector2(0, 9),
            center + new Vector2(-7, 0)], Palette.Gold);
        Text(center + new Vector2(16, fontSize * 0.35f), $"{value}", fontSize, Palette.Neutral);
    }

    void Text(Vector2 baseline, string text, int fontSize, Color color, float width = -1,
        HorizontalAlignment alignment = HorizontalAlignment.Left) =>
        DrawString(ThemeDB.FallbackFont, baseline, text, alignment, width, fontSize, color);
}
