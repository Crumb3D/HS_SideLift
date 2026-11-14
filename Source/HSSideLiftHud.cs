using UnityEngine.Scripting;

// Right of the crosshair while the setup tool is in hand. Hidden the rest of the time.
[Preserve]
public class HSSideLiftHud : XUiController
{
    string info = "";
    bool show;

    public override void Update(float _dt)
    {
        base.Update(_dt);
        var player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer as EntityPlayerLocal : null;
        bool holding = ItemActionHSSideLiftTool.IsHolding(player);
        var next = holding ? HSSideLiftConfiguration.HoldingHud() : "";
        if (holding != show || next != info)
        {
            show = holding;
            info = next;
            IsDirty = true;
        }
        if (!IsDirty) return;
        RefreshBindings();
        IsDirty = false;
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        if (_bindingName == "info") { _value = info ?? ""; return true; }
        if (_bindingName == "show") { _value = show ? "true" : "false"; return true; }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }
}
