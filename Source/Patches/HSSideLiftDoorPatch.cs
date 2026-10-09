using System;
using HarmonyLib;

// Composite door commands arrive as "TEFeatureDoor:open" / "TEFeatureDoor:close".
// Ped: cabin + landing at a floor open/close together. Vehicle: one garage/roll-up set per landing, no cabin pair.
// Opening is blocked unless the parked car is at that floor. Close does not send the car; pick a floor with hold E.
[HarmonyPatch(typeof(BlockCompositeTileEntity), "OnBlockActivated",
    new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class HSSideLiftDoorPatch
{
    static bool IsCommand(string name, string cmd)
    {
        return name != null && (name == cmd || name.EndsWith(":" + cmd, StringComparison.Ordinal));
    }

    static bool Prefix(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player, ref bool __result)
    {
        try
        {
            if (_commandName != null && _commandName.StartsWith("HSSideLiftFloor", StringComparison.Ordinal))
            {
                var playerLift = HSSideLiftConfiguration.LiftForPlayer(_player);
                if (playerLift != null) HSSideLiftConfiguration.Use(playerLift);
                int floor = HSSideLiftFloorMenu.Parse(_commandName, playerLift ?? HSSideLiftConfiguration.Data);
                if (floor >= 0)
                {
                    var err = HSSideLiftController.RequestFromDoorMenu(_player, floor);
                    if (err != null && _player != null)
                        GameManager.ShowTooltip(_player, string.Format(Localization.Get("HSSideLiftNotReady"), err));
                    __result = true;
                    return false;
                }
            }
            if (HSSideLiftActivation.IsTake(_commandName) && HSSideLiftDoors.IsElevatorDoor(_blockValue.Block))
            {
                var takeParent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
                if (HSSideLiftDoors.BindDoor(takeParent, _world.GetBlock(takeParent)) != null)
                {
                    __result = true;
                    return false;
                }
            }
            bool open = IsCommand(_commandName, "open");
            bool close = IsCommand(_commandName, "close");
            if ((!open && !close) || !HSSideLiftDoors.IsElevatorDoor(_blockValue.Block)) return true;
            var parent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
            var parentBv = _world.GetBlock(parent);
            if (HSSideLiftDoors.BindDoor(parent, parentBv) == null)
            {
                HSSideLiftDebug.Info("Door " + _commandName + " at " + parent + " is not bound to a lift");
                return true;
            }
            bool behind = HSSideLiftDoors.CarIsBehind(parent, parentBv);
            bool vehicle = HSSideLiftConfiguration.IsVehicle;
            var doorFloor = HSSideLiftConfiguration.FloorForPanel(parent);
            bool parkedHere = behind || (doorFloor != null && HSSideLiftConfiguration.Data.CurrentY == doorFloor.Y && !HSSideLiftController.IsMoving);
            HSSideLiftDebug.Info("Door " + _commandName + " at " + parent + " parkedHere=" + parkedHere + " behind=" + behind);

            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (close)
            {
                if (parkedHere && !vehicle)
                {
                    if (world != null) HSSideLiftDoors.CloseAtCar(world);
                    if (HSSideLiftNet.IsRemoteClient)
                    {
                        var id = HSSideLiftConfiguration.Data != null ? HSSideLiftConfiguration.Data.ElevatorId : "";
                        HSSideLiftNet.SendDoors(id, false);
                    }
                    else HSSideLiftController.OnLiftDoorClosed(_player);
                    __result = true;
                    return false;
                }
                return true;
            }
            if (parkedHere)
            {
                if (world != null && HSSideLiftDoors.IsCarDoor(world, parent) && !HSSideLiftDoors.HasLandingOutside(world, parent))
                {
                    if (_player != null)
                        GameManager.ShowTooltip(_player, Localization.Get("HSSideLiftDoorNoLanding"));
                    Audio.Manager.BroadcastPlayByLocalPlayer(parent.ToVector3() + UnityEngine.Vector3.one * 0.5f,
                        vehicle ? "door_garage_metal_locked" : "door_elevator_locked");
                    HSSideLiftDebug.Verbose("Blocked opening lift door at " + parent + " (no landing outside)");
                    __result = false;
                    return false;
                }
                if (!vehicle)
                {
                    if (world != null) HSSideLiftDoors.OpenAtCar(world);
                    if (HSSideLiftNet.IsRemoteClient)
                    {
                        var id = HSSideLiftConfiguration.Data != null ? HSSideLiftConfiguration.Data.ElevatorId : "";
                        HSSideLiftNet.SendDoors(id, true);
                    }
                    __result = true;
                    return false;
                }
                return true;
            }

            if (_player != null)
            {
                string msg;
                if (HSSideLiftController.IsMoving) msg = Localization.Get("HSSideLiftDoorMoving");
                else
                {
                    var problem = HSSideLiftController.RequestFromLockedDoor(parent);
                    msg = problem == null ? Localization.Get("HSSideLiftCalled") : string.Format(Localization.Get("HSSideLiftNotReady"), problem);
                }
                GameManager.ShowTooltip(_player, msg);
            }
            Audio.Manager.BroadcastPlayByLocalPlayer(parent.ToVector3() + UnityEngine.Vector3.one * 0.5f,
                HSSideLiftConfiguration.IsVehicle ? "door_garage_metal_locked" : "door_elevator_locked");
            HSSideLiftDebug.Verbose("Blocked opening lift door at " + parent + " (car at Y" + HSSideLiftConfiguration.Data.CurrentY + ", state " + HSSideLiftController.State + ")");
            __result = false;
            return false;
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Door check failed", e);
            return true;
        }
    }

}

// Standing in the parked car (no inside panel), the lift door's hold-E menu also lists the floors.
[HarmonyPatch(typeof(BlockCompositeTileEntity), "GetBlockActivationCommands",
    new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) })]
public static class HSSideLiftDoorMenuPatch
{
    static void Postfix(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing, ref BlockActivationCommand[] __result)
    {
        try
        {
            if (!HSSideLiftDoors.IsElevatorDoor(_blockValue.Block)) return;
            var list = new System.Collections.Generic.List<BlockActivationCommand>();
            if (__result != null)
                foreach (var c in __result)
                    if (!HSSideLiftActivation.IsTake(c.text)) list.Add(c);
            HSSideLiftActivation.PutOpenCloseFirst(list);

            var parent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
            var parentBv = _world.GetBlock(parent);
            if (HSSideLiftDoors.BindDoor(parent, parentBv) == null)
            {
                __result = list.ToArray();
                return;
            }

            var d = HSSideLiftConfiguration.Data;
            var player = _entityFocusing as EntityPlayerLocal;
            if (!HSSideLiftController.IsMoving && d.HasCar && d.Floors.Count > 1
                && HSSideLiftController.PlayerInCar(player)
                && HSSideLiftDoors.CarIsBehind(parent, parentBv))
                list.AddRange(HSSideLiftFloorMenu.Commands(d));

            __result = list.ToArray();
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Door floor menu failed", e);
        }
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), "GetActivationText",
    new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) })]
public static class HSSideLiftDoorTextPatch
{
    static void Postfix(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing, ref string __result)
    {
        try
        {
            if (!HSSideLiftDoors.IsElevatorDoor(_blockValue.Block)) return;
            var parent = _blockValue.ischild ? _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue) : _blockPos;
            var te = _world.GetTileEntity(parent) as TileEntityComposite;
            var door = te != null ? te.GetFeature<TEFeatureDoor>() : null;
            if (door == null) return;
            __result = door.IsOpen() ? Localization.Get("HSSideLiftDoorClose") : Localization.Get("HSSideLiftDoorOpen");
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Door activation text failed", e);
        }
    }
}
