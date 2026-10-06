using System;

// No tile entity and no wires, so it can travel with the car. Obeys the same power rule via the outside panels.
public class BlockHSSideLiftInsidePanel : Block
{
    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        var own = new System.Collections.Generic.List<BlockActivationCommand>();
        try
        {
            var pos = BlockHSSideLiftOutsidePanel.ParentPos(_blockPos, _blockValue);
            var lift = HSSideLiftConfiguration.LiftForInsidePanel(pos);
            if (lift == null) return own.ToArray();
            HSSideLiftConfiguration.Use(lift);
            own.AddRange(HSSideLiftFloorMenu.Commands(lift));
        }
        catch (Exception e) { HSSideLiftDebug.Error("Inside panel floor list failed", e); }
        return own.ToArray();
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = BlockHSSideLiftOutsidePanel.ParentPos(_blockPos, _blockValue);
            var lift = HSSideLiftConfiguration.LiftForInsidePanel(pos);
            if (lift == null)
                return Localization.Get("HSSideLiftUnregistered");
            var ctrl = HSSideLiftController.Of(lift);
            if (ctrl != null && ctrl.IsThisMoving) return Localization.Get("HSSideLiftBusy");
            var notReady = BlockHSSideLiftOutsidePanel.NotReadyText(lift);
            if (notReady != null) return notReady;
            return HSSideLiftFloorMenu.Prompt();
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Inside panel text failed", e);
            return "";
        }
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSSideLiftPanelCollider.Ensure(_ebcd, _blockPos);
    }

    // Tap E does nothing. Hold E opens the floor list (command overload below).
    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return true;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (HSSideLiftActivation.IsTake(_commandName))
            return true;
        try
        {
            var pos = BlockHSSideLiftOutsidePanel.ParentPos(_blockPos, _blockValue);
            var lift = HSSideLiftConfiguration.LiftForInsidePanel(pos);
            if (lift == null)
            {
                if (_player != null)
                    GameManager.ShowTooltip(_player, Localization.Get("HSSideLiftUnregistered"));
                return true;
            }
            HSSideLiftConfiguration.Use(lift);
            int floor = HSSideLiftFloorMenu.Parse(_commandName, lift);
            if (floor < 0) return true;
            var problem = HSSideLiftController.RequestFromInsidePanel(pos, floor);
            if (problem != null && _player != null)
                GameManager.ShowTooltip(_player, string.Format(Localization.Get("HSSideLiftNotReady"), problem));
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Inside panel press failed", e);
        }
        return true;
    }
}
