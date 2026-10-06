using System;
using UnityEngine;

public class BlockHSSideLiftOutsidePanel : BlockPowered
{
    public override TileEntityPowered CreateTileEntity(Chunk chunk)
    {
        return base.CreateTileEntity(chunk);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var lift = HSSideLiftConfiguration.RegisteredPanelOwner(pos);
            if (lift == null) return new BlockActivationCommand[0];
            HSSideLiftConfiguration.Use(lift);
            return new[] { new BlockActivationCommand("HSSideLiftCall", "electric_switch", true, false, null) };
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Outside panel commands failed", e);
            return new BlockActivationCommand[0];
        }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var lift = HSSideLiftConfiguration.RegisteredPanelOwner(pos);
            if (lift == null)
                return Localization.Get("HSSideLiftUnregistered");
            var ctrl = HSSideLiftController.Of(lift);
            if (ctrl != null && ctrl.IsThisMoving) return Localization.Get("HSSideLiftBusy");
            var notReady = NotReadyText(lift);
            if (notReady != null) return notReady;
            return Localization.Get("HSSideLiftCall");
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Outside panel text failed", e);
            return "";
        }
    }

    public static string NotReadyText(HSSideLiftConfigData d)
    {
        string problem;
        if (!HSSideLiftPower.HasWorkingPower(d, out problem))
            return string.Format(Localization.Get("HSSideLiftNotReady"), problem);
        return null;
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSSideLiftPanelCollider.Ensure(_ebcd, _blockPos);
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (HSSideLiftActivation.IsTake(_commandName))
            return true;
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var lift = HSSideLiftConfiguration.RegisteredPanelOwner(pos);
            if (lift != null) HSSideLiftConfiguration.Use(lift);
            var problem = HSSideLiftController.RequestFromOutsidePanel(pos);
            if (problem != null && _player != null)
                GameManager.ShowTooltip(_player, string.Format(Localization.Get("HSSideLiftNotReady"), problem));
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Outside panel press failed", e);
        }
        return true;
    }

    public static Vector3i ParentPos(Vector3i pos, BlockValue bv)
    {
        try
        {
            if (bv.ischild && bv.Block != null)
                return bv.Block.multiBlockPos.GetParentPos(pos, bv);
        }
        catch { }
        return pos;
    }
}

public static class HSSideLiftPanelCollider
{
    public static void Ensure(BlockEntityData ebcd, Vector3i pos)
    {
        if (ebcd == null || ebcd.transform == null) return;
        try
        {
            if (ebcd.transform.Find("HSSideLiftPanelCollider") != null) return;
            var go = new GameObject("HSSideLiftPanelCollider");
            go.transform.SetParent(ebcd.transform, false);
            go.transform.localPosition = new Vector3(0.5f, 1f, 0.5f);
            go.transform.localScale = Vector3.one;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.85f, 1.6f, 0.85f);
            box.center = Vector3.zero;
            box.isTrigger = true;
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Panel look collider failed at " + pos, e);
        }
    }
}
