using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum HSSideLiftState { Idle, Closing, MovingUp, MovingDown, Stopping, Error }

public class HSSideLiftController : MonoBehaviour
{
    public HSSideLiftConfigData Bound;
    public HSSideLiftState LiftState = HSSideLiftState.Idle;
    public bool IsThisMoving { get { return LiftState != HSSideLiftState.Idle; } }

    static readonly List<HSSideLiftController> all = new List<HSSideLiftController>();
    static bool soundsStarted;

    public static HSSideLiftState State { get { var c = Active(); return c != null ? c.LiftState : HSSideLiftState.Idle; } }
    public static bool IsMoving { get { var c = Active(); return c != null && c.IsThisMoving; } }

    static HSSideLiftConfigData D { get { return HSSideLiftConfiguration.Data; } }

    static HSSideLiftController instance { get { return Of(HSSideLiftConfiguration.Data); } }

    static HSSideLiftController Active()
    {
        return Of(HSSideLiftConfiguration.Data);
    }

    public static bool Bind(HSSideLiftConfigData d)
    {
        if (d == null) return false;
        HSSideLiftConfiguration.Use(d);
        Ensure(d);
        return true;
    }

    public static HSSideLiftController Of(HSSideLiftConfigData d)
    {
        if (d == null) return null;
        return all.Find(c => c != null && c.Bound != null && c.Bound.ElevatorId == d.ElevatorId);
    }

    void Push()
    {
        if (Bound != null) HSSideLiftConfiguration.Use(Bound);
    }

    public static void EnsureCreated()
    {
        foreach (var d in HSSideLiftConfiguration.Lifts) Ensure(d);
    }

    public static HSSideLiftController Ensure(HSSideLiftConfigData d)
    {
        if (d == null) return null;
        var c = Of(d);
        if (c != null) { c.Bound = d; return c; }
        var go = new GameObject("HSSideLift_" + d.ElevatorId);
        DontDestroyOnLoad(go);
        c = go.AddComponent<HSSideLiftController>();
        c.Bound = d;
        all.Add(c);
        if (!soundsStarted)
        {
            c.StartCoroutine(HSSideLiftSounds.LoadAll());
            soundsStarted = true;
        }
        c.LiftState = HSSideLiftState.Idle;
        c.recovering = HSSideLiftCar.HasJournalFor(d.ElevatorId);
        if (c.recovering) HSSideLiftDebug.Info("Journal found for " + d.ElevatorId + "; restoring the car once its chunks load.");
        return c;
    }

    const float PowerCheckInterval = 0.25f;
    const float ReleaseDelay = 0.75f;
    const float RetryInterval = 2f;
    const float PreviewSeconds = 30f;

    readonly HSSideLiftMovement move = new HSSideLiftMovement();
    List<HSSideLiftCell> cells;
    int fromY;
    float nextPowerCheck;
    float nextRetry;
    string errorReason;
    bool recovering;
    bool signsPrimed;
    float autoCloseAt;
    float nextDoorBlockedWarn;
    bool watchDoorClose;
    const float AutoCloseSeconds = 20f;

    GameObject releasing;
    float releaseAt;

    GameObject preview;
    float previewUntil;

    public void ScheduleAutoClose()
    {
        autoCloseAt = Time.unscaledTime + AutoCloseSeconds;
    }

    public void CancelAutoClose()
    {
        autoCloseAt = 0f;
    }

    public void WatchDoorClose()
    {
        watchDoorClose = true;
    }

    public void ClearDoorWatch()
    {
        watchDoorClose = false;
    }

    // Reopen immediately if something enters while the doors are closing.
    void TickReopenIfBlocked()
    {
        if (!watchDoorClose) return;
        if (LiftState != HSSideLiftState.Idle && LiftState != HSSideLiftState.Closing)
        {
            watchDoorClose = false;
            return;
        }
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        string why;
        if (HSSideLiftDoors.PersonInDoorway(world, out why) || HSSideLiftDoors.DoorwayBlocked(world, out why))
        {
            bool idle = LiftState == HSSideLiftState.Idle;
            HSSideLiftDoors.OpenAtCar(world, idle);
            if (LiftState == HSSideLiftState.Closing) doorsToldToClose = false;
            WarnDoorBlocked(world, why);
            HSSideLiftDebug.Verbose("Reopened doors (obstruction): " + why);
            return;
        }
        if (!HSSideLiftDoors.AnyOpenAtCar(world) && !HSSideLiftDoors.PersonInDoorway(world, out why))
            watchDoorClose = false;
    }

    void TickAutoClose()
    {
        if (autoCloseAt <= 0f || LiftState != HSSideLiftState.Idle) return;
        if (Time.unscaledTime < autoCloseAt) return;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;
        if (!HSSideLiftDoors.AnyOpenAtCar(world))
        {
            autoCloseAt = 0f;
            return;
        }
        string why;
        if (HSSideLiftDoors.DoorwayBlocked(world, out why))
        {
            autoCloseAt = Time.unscaledTime + 2f;
            WarnDoorBlocked(world, why);
            return;
        }
        HSSideLiftDoors.CloseAtCar(world);
        HSSideLiftDebug.Verbose("Auto-closed doors after " + AutoCloseSeconds + "s");
    }

    void WarnDoorBlocked(World world, string why)
    {
        if (Time.unscaledTime < nextDoorBlockedWarn) return;
        nextDoorBlockedWarn = Time.unscaledTime + 8f;
        if (!string.IsNullOrEmpty(why)) HSSideLiftDebug.Verbose("Doors blocked: " + why);
    }

    // --- requests (outside panels, inside panels, console all end up in RequestMoveTo) ---

    // Console up/down: the next floor in that direction.
    public static string RequestMove(bool up, string source)
    {
        if (!D.HasUpper) return "needs at least two floors (HSSideLift floor add <name>)";
        HSSideLiftFloor next = null;
        foreach (var f in D.Floors)
        {
            if (up && f.Y > D.CurrentY) { next = f; break; }
            if (!up && f.Y < D.CurrentY) next = f;
        }
        if (next == null) return "already at the " + (up ? "top" : "bottom") + " floor";
        return RequestMoveTo(next.Y, source);
    }

    public static string RequestFloor(string name, string source)
    {
        var f = HSSideLiftConfiguration.FloorByName(name);
        if (f == null) return "no floor named '" + name + "' (" + HSSideLiftConfiguration.FloorList() + ")";
        return RequestMoveTo(f.Y, source);
    }

    // Outside panels only call the car to their own floor; the destination is chosen inside the car.
    public static string RequestFromOutsidePanel(Vector3i pos)
    {
        if (HSSideLiftNet.IsRemoteClient)
        {
            HSSideLiftNet.SendUsePanel(pos);
            return null;
        }
        var lift = HSSideLiftConfiguration.RegisteredPanelOwner(pos);
        if (!Bind(lift))
            return "this panel is not registered (aim at it: HSSideLift panel)";
        var entry = lift.Panels.Find(p => p.X == pos.x && p.Y == pos.y && p.Z == pos.z);
        if (entry == null) return "this panel is not registered (aim at it: HSSideLift panel)";
        var floor = lift.Floors.Find(f => string.Equals(f.Name, entry.Stop, StringComparison.OrdinalIgnoreCase));
        if (floor == null)
            floor = HSSideLiftConfiguration.FloorForPanel(lift, entry.Pos);
        if (floor == null) return "this panel is not at a floor (" + HSSideLiftConfiguration.FloorList() + ")";
        var ctrl = Of(lift);
        RelocateParkedCar();
        if (lift.CurrentY == floor.Y && (ctrl == null || !ctrl.IsThisMoving))
        {
            HSSideLiftDoors.OpenAtCar(GameManager.Instance.World);
            return null;
        }
        return RequestMoveTo(floor.Y, "called from floor " + floor.Name);
    }

    // Pressing a locked lift door calls the car to that door's floor, like its outside panel.
    public static string RequestFromLockedDoor(Vector3i doorPos)
    {
        if (HSSideLiftNet.IsRemoteClient)
        {
            HSSideLiftNet.SendUseDoor(doorPos);
            return null;
        }
        var doorLift = HSSideLiftConfiguration.LiftForDoor(doorPos);
        if (!Bind(doorLift))
            return "this door is not part of a lift";
        var floor = HSSideLiftConfiguration.FloorForPanel(doorLift, doorPos);
        if (floor == null) return "this door is not at a floor (" + HSSideLiftConfiguration.FloorList() + ")";
        RelocateParkedCar();
        if (D.CurrentY == floor.Y && !IsMoving)
        {
            HSSideLiftDoors.OpenAtCar(GameManager.Instance.World);
            return null;
        }
        return RequestMoveTo(floor.Y, "locked door on floor " + floor.Name);
    }

    // floorIndex is the hold-E menu slot. A plain press has no destination.
    public static string RequestFromInsidePanel(Vector3i pos, int floorIndex)
    {
        if (HSSideLiftNet.IsRemoteClient)
        {
            HSSideLiftNet.SendUseInside(pos, floorIndex);
            return null;
        }
        if (!Bind(HSSideLiftConfiguration.LiftForInsidePanel(pos)))
            return "this inside panel is not part of the lift car";
        return RequestFromInside(floorIndex, "inside panel");
    }

    public static string RequestFromDoorMenu(EntityPlayerLocal player, int floorIndex)
    {
        if (HSSideLiftNet.IsRemoteClient)
        {
            var clientLift = HSSideLiftConfiguration.LiftForPlayer(player);
            if (clientLift == null || !PlayerInCar(player)) return "stand inside the lift car to choose a floor";
            HSSideLiftNet.SendUseFloor(clientLift.ElevatorId, 0, floorIndex);
            return null;
        }
        if (!Bind(HSSideLiftConfiguration.LiftForPlayer(player))) return "stand inside the lift car to choose a floor";
        if (!PlayerInCar(player)) return "stand inside the lift car to choose a floor";
        return RequestFromInside(floorIndex, "door menu");
    }

    public static string RequestInsideFloor(int floorIndex, string source)
    {
        return RequestFromInside(floorIndex, source);
    }

    public static string RequestMoveToY(int targetY, string source)
    {
        return RequestMoveTo(targetY, source);
    }

    static string RequestFromInside(int floorIndex, string source)
    {
        if (!D.HasUpper) return "needs at least two floors (HSSideLift floor add <name>)";
        if (floorIndex < 0 || floorIndex >= D.Floors.Count) return Localization.Get("HSSideLiftChooseFloor");
        var f = D.Floors[floorIndex];
        return RequestMoveTo(f.Y, source + " -> floor " + f.Name);
    }

    public static bool PlayerInCar(EntityPlayerLocal player)
    {
        if (player == null || !D.HasCar) return false;
        var feet = new Vector3i(Mathf.FloorToInt(player.position.x), Mathf.FloorToInt(player.position.y + 0.1f), Mathf.FloorToInt(player.position.z));
        return HSSideLiftCar.InFootprint(feet.x, feet.z)
            && feet.y >= D.CurrentY && feet.y <= D.CurrentY + D.RideHeight;
    }

    // Closing a door no longer sends the car. Hold E on the inside panel (or the door if there is no panel) to pick a floor.
    public static void OnLiftDoorClosed(EntityPlayerLocal player)
    {
    }

    static string RequestMoveTo(int targetY, string source)
    {
        try
        {
            Ensure(D);
            if (instance == null) return "HSSideLift not started";
            if (HSSideLiftNet.IsRemoteClient)
            {
                HSSideLiftNet.SendUseFloor(D.ElevatorId, targetY, -1);
                return null;
            }
            if (IsMoving)
            {
                HSSideLiftDebug.Verbose("Ignored " + source + " request: lift is " + State);
                return "lift is moving";
            }
            if (instance.recovering) return "restoring the car from an interrupted move";
            if (!D.HasCar) return "car not set (HSSideLift corner1 / corner2)";

            var world = GameManager.Instance.World;
            List<HSSideLiftCell> captured;
            int fromY;
            var problem = CheckCanMove(world, targetY, out captured, out fromY);
            if (problem != null) return Refuse(source, problem);
            if (fromY == targetY)
            {
                HSSideLiftDoors.OpenAtCar(world);
                return "already at " + HSSideLiftConfiguration.FloorLabel(targetY);
            }

            // Wait until the doorway is clear, then shut the doors; the car leaves after they finish closing.
            instance.pendingTarget = targetY;
            instance.pendingSource = source;
            instance.doorsToldToClose = false;
            instance.LiftState = HSSideLiftState.Closing;
            HSSideLiftDebug.Verbose("Waiting to close doors before leaving for " + HSSideLiftConfiguration.FloorLabel(targetY) + " (" + source + ")");
            return null;
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Move request failed", e);
            return "error (see log)";
        }
    }

    static void RelocateParkedCar()
    {
        var world = GameManager.Instance.World;
        if (world == null || !D.HasCar) return;
        List<HSSideLiftCell> cells;
        int fromY;
        HSSideLiftCar.CaptureParked(world, out cells, out fromY);
    }

    static string CheckCanMove(World world, int targetY, out List<HSSideLiftCell> captured)
    {
        int fromY;
        return CheckCanMove(world, targetY, out captured, out fromY);
    }

    static string CheckCanMove(World world, int targetY, out List<HSSideLiftCell> captured, out int fromY)
    {
        captured = null;
        fromY = D.CurrentY;
        string problem;
        if (!HSSideLiftPower.HasWorkingPower(out problem)) return problem;
        problem = HSSideLiftCar.CaptureParked(world, out captured, out fromY);
        if (problem != null) return problem;
        if (fromY == targetY) return null;
        HSSideLiftCar.RemoveLeftoverCopies(world, fromY, targetY, captured);
        return HSSideLiftCar.CheckShaftSupport(world, fromY)
            ?? HSSideLiftCar.CheckShaftSupport(world, targetY)
            ?? HSSideLiftCar.CheckPath(world, fromY, targetY, captured)
            ?? HSSideLiftCar.CheckDestination(world, fromY, targetY, captured);
    }

    static float DoorCloseDelay { get { return HSSideLiftConfiguration.IsVehicle ? 2.75f : 1.5f; } }
    int pendingTarget;
    string pendingSource;
    float departAt;
    bool doorsToldToClose;

    void FinishClosing()
    {
        Push();
        var world = GameManager.Instance.World;
        string why;
        if (HSSideLiftDoors.PersonInDoorway(world, out why) || (HSSideLiftDoors.AnyOpenAtCar(world) && HSSideLiftDoors.DoorwayBlocked(world, out why)))
        {
            HSSideLiftDoors.OpenAtCar(world, false);
            doorsToldToClose = false;
            WarnDoorBlocked(world, why);
            return;
        }
        List<HSSideLiftCell> captured;
        int fromY;
        var problem = CheckCanMove(world, pendingTarget, out captured, out fromY);
        if (problem != null)
        {
            LiftState = HSSideLiftState.Idle;
            Refuse(pendingSource, problem);
            HSSideLiftDoors.OpenAtCar(world);
            HSSideLiftNet.TellLocal(string.Format(Localization.Get("HSSideLiftNotReady"), problem));
            return;
        }
        if (fromY == pendingTarget)
        {
            LiftState = HSSideLiftState.Idle;
            HSSideLiftDoors.OpenAtCar(world);
            return;
        }
        Depart(world, captured, fromY, pendingTarget, pendingSource);
    }

    static string Refuse(string source, string problem)
    {
        HSSideLiftDebug.Info("Move refused (" + source + "): " + problem);
        return problem;
    }

    void Depart(World world, List<HSSideLiftCell> captured, int parkedY, int targetY, string source)
    {
        Push();
        FinishRelease();
        StopPreviewInternal();
        fromY = parkedY;
        if (HSSideLiftDoors.CloseAllShaftDoors(world, false) > 0)
        {
            // Car doors changed state; capture again so they travel closed.
            List<HSSideLiftCell> recaptured;
            if (HSSideLiftCar.Capture(world, fromY, out recaptured) == null) captured = recaptured;
        }
        cells = captured;
        HSSideLiftCar.WriteJournal(fromY, cells);
        var root = HSSideLiftCar.BuildVisual(world, cells, true, fromY);
        move.Begin(root, fromY, targetY);
        HSSideLiftCar.RemoveFromWorld(world, fromY, cells);
        LiftState = targetY > fromY ? HSSideLiftState.MovingUp : HSSideLiftState.MovingDown;
        StartMoveSound(root);
        nextPowerCheck = Time.time + PowerCheckInterval;
        HSSideLiftNet.BroadcastMoveStart(D.ElevatorId, fromY, targetY, fromY, cells);
        HSSideLiftDebug.Info(string.Format("{0}: Y{1} -> Y{2} ({3} blocks, {4})", State, fromY, targetY, cells.Count, source));
    }

    // --- update loop ---

    void FixedUpdate()
    {
        try
        {
            Push();
            if (!HSSideLiftNet.IsAuthority)
            {
                if (LiftState == HSSideLiftState.Idle || LiftState == HSSideLiftState.Error || LiftState == HSSideLiftState.Closing)
                {
                    move.Apply();
                    return;
                }
                float beforeY = move.CurY;
                bool done = move.Step(Time.fixedDeltaTime, D.SpeedBlocksPerSecond);
                CarryRiders(beforeY, move.CurY - beforeY);
                if (done) move.Apply();
                return;
            }
            if (LiftState == HSSideLiftState.Closing)
            {
                var world = GameManager.Instance.World;
                if (!doorsToldToClose)
                {
                    string why;
                    if (HSSideLiftDoors.DoorwayBlocked(world, out why))
                    {
                        WarnDoorBlocked(world, why);
                        return;
                    }
                    bool anyOpen = HSSideLiftDoors.AnyOpenAtCar(world);
                    HSSideLiftDoors.CloseAllShaftDoors(world);
                    doorsToldToClose = true;
                    departAt = anyOpen ? Time.time + DoorCloseDelay : Time.time;
                    HSSideLiftDebug.Verbose((anyOpen ? "Closing doors" : "Doors already shut") + " before leaving for " + HSSideLiftConfiguration.FloorLabel(pendingTarget));
                    return;
                }
                if (doorsToldToClose)
                {
                    string blockedWhy;
                    if (HSSideLiftDoors.PersonInDoorway(world, out blockedWhy) || HSSideLiftDoors.DoorwayBlocked(world, out blockedWhy))
                    {
                        HSSideLiftDoors.OpenAtCar(world, false);
                        doorsToldToClose = false;
                        WarnDoorBlocked(world, blockedWhy);
                        return;
                    }
                    if (Time.time >= departAt) FinishClosing();
                    return;
                }
                return;
            }
            if (LiftState == HSSideLiftState.Idle || LiftState == HSSideLiftState.Error)
            {
                move.Apply();
                return;
            }
            float before = move.CurY;
            bool arrived = move.Step(Time.fixedDeltaTime, D.SpeedBlocksPerSecond);
            CarryRiders(before, move.CurY - before);
            HSSideLiftFloorSigns.UpdatePassing(GameManager.Instance.World, move.CurY, fromY, move.TargetY);
            if (arrived)
            {
                Arrive(move.TargetY);
                return;
            }
            if (LiftState != HSSideLiftState.Stopping && Time.time >= nextPowerCheck)
            {
                nextPowerCheck = Time.time + PowerCheckInterval;
                string problem;
                if (!HSSideLiftPower.HasWorkingPower(out problem)) BeginStopping("power lost: " + problem);
                else
                {
                    problem = CheckAhead();
                    if (problem != null) BeginStopping("obstruction: " + problem);
                }
            }
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Movement tick failed", e);
            EnterError("exception during movement");
        }
    }

    // Players, vehicles, dropped items — anything sitting in the car / on the pad rides with it.
    void CarryRiders(float carY, float dy)
    {
        if (Mathf.Approximately(dy, 0f) || !D.HasCar) return;
        CarryPlayer(carY, dy);
        CarryEntities(carY, dy);
    }

    // The player controller only rides layer-28 platforms, which its ground check can't see, so move riders with the car.
    void CarryPlayer(float carY, float dy)
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var locals = world != null ? world.GetLocalPlayers() : null;
        if (locals == null) return;
        var delta = D.IsAxisZ ? new Vector3(0f, 0f, dy) : new Vector3(dy, 0f, 0f);
        for (int i = 0; i < locals.Count; i++)
        {
            var player = locals[i] as EntityPlayerLocal;
            var fp = player != null ? player.vp_FPController : null;
            if (fp == null || player.AttachedToEntity != null) continue;
            if (!EntityOnCar(player, carY)) continue;
            fp.SetPosition(fp.Transform.position + delta);
        }
    }

    readonly List<Entity> riders = new List<Entity>();

    void CarryEntities(float carY, float dy)
    {
        var world = GameManager.Instance.World;
        if (world == null) return;
        riders.Clear();
        var bb = new Bounds();
        int x0 = HSSideLiftCar.OriginX(Mathf.FloorToInt(carY));
        int z0 = HSSideLiftCar.OriginZ(Mathf.FloorToInt(carY));
        bb.SetMinMax(
            new Vector3(x0 - 0.5f, D.ParkY - 1f, z0 - 0.5f),
            new Vector3(x0 + D.SizeX + 0.5f, D.ParkY + D.RideHeight + 1f, z0 + D.SizeZ + 0.5f));
        world.GetEntitiesInBounds(typeof(Entity), bb, riders);
        var delta = D.IsAxisZ ? new Vector3(0f, 0f, dy) : new Vector3(dy, 0f, 0f);
        for (int i = 0; i < riders.Count; i++)
        {
            var e = riders[i];
            if (e == null || e.AttachedToEntity != null) continue;
            if (e is EntityPlayer) continue;
            if (e is EntityFallingBlock) continue;
            if (!EntityOnCar(e, carY)) continue;
            e.SetPosition(e.position + delta, true);
            var v = e as EntityVehicle;
            if (v != null) v.PhysicsResetAndSleep();
        }
    }

    static bool EntityOnCar(Entity e, float carY)
    {
        if (e == null || !D.HasCar) return false;
        var box = e.boundingBox;
        if (box.max.y < D.ParkY - 0.25f || box.min.y > D.ParkY + D.RideHeight) return false;
        var c = box.center;
        int along = Mathf.FloorToInt(carY);
        if (HSSideLiftCar.InRectAt(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.z), along)) return true;
        return HSSideLiftCar.InRectAt(Mathf.FloorToInt(e.position.x), Mathf.FloorToInt(e.position.z), along);
    }

    void Update()
    {
        try
        {
            Push();
            HSSideLiftCable.Tick(Bound);
            if (releasing != null && Time.time >= releaseAt) FinishRelease();
            if (!HSSideLiftNet.IsAuthority)
            {
                if (preview != null && Time.time >= previewUntil) StopPreviewInternal();
                return;
            }
            TickAutoClose();
            TickReopenIfBlocked();
            if (preview != null)
            {
                if (Time.time >= previewUntil) StopPreviewInternal();
                else preview.transform.position = HSSideLiftCar.UnityPos(D.CurrentY) + new Vector3(D.SizeX + 3, 0, 0);
            }
            if (Time.time < nextRetry) return;
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null) return;

            if (recovering)
            {
                nextRetry = Time.time + RetryInterval;
                if (HSSideLiftCar.TryRecoverJournal(world)) recovering = false;
            }
            else if (!signsPrimed && LiftState == HSSideLiftState.Idle)
            {
                HSSideLiftFloorSigns.UpdateToCurrentFloor(world);
                signsPrimed = true;
            }
            else if (LiftState == HSSideLiftState.Error && cells != null)
            {
                nextRetry = Time.time + RetryInterval;
                Arrive(Mathf.RoundToInt(move.CurY));
            }
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Controller update failed", e);
            nextRetry = Time.time + RetryInterval;
        }
    }

    // Rows the car overlaps now or enters next, limited to the swept path, never its original cells.
    string CheckAhead()
    {
        var world = GameManager.Instance.World;
        int dir = move.Direction;
        if (cells != null)
        {
            int pathLo = Math.Min(fromY, move.TargetY), pathHi = Math.Max(fromY, move.TargetY);
            foreach (var c in cells)
            {
                if (HSSideLiftCar.IsRidePiece(c.Bv)) continue;
                var from = HSSideLiftCar.CellWorld(c.Dx, c.Dy, c.Dz, fromY);
                if (HSSideLiftCar.IsExcludedCell(from.x, c.Dy, from.z)) continue;
                int along = (dir > 0 ? Mathf.CeilToInt(move.CurY) : Mathf.FloorToInt(move.CurY)) + dir;
                if (along < pathLo || along > pathHi) continue;
                if (along == fromY) continue;
                var p = HSSideLiftCar.CellWorld(c.Dx, c.Dy, c.Dz, along);
                if (HSSideLiftCar.InBox(p, fromY)) continue;
                if (HSSideLiftCar.IsRegisteredFloorY(along) && along != fromY) continue;
                var err = HSSideLiftCar.CheckClear(world, p, "is blocking the lift path", true);
                if (err != null) return err;
            }
            return null;
        }
        else
        {
            int along2 = (dir > 0 ? Mathf.CeilToInt(move.CurY) : Mathf.FloorToInt(move.CurY)) + dir;
            int lo = Math.Min(fromY, move.TargetY), hi = Math.Max(fromY, move.TargetY);
            if (along2 < lo || along2 > hi || along2 == fromY) return null;
            for (int dx = 0; dx < D.SizeX; dx++)
            for (int dz = 0; dz < D.SizeZ; dz++)
            {
                var p = HSSideLiftCar.CellWorld(dx, 0, dz, along2);
                if (HSSideLiftCar.IsExcluded(p.x, p.z)) continue;
                if (HSSideLiftCar.IsRegisteredFloorY(along2)) continue;
                var err = HSSideLiftCar.CheckClear(world, p, "is blocking the lift path", true);
                if (err != null) return err;
            }
            return null;
        }
    }

    void BeginStopping(string reason)
    {
        int settle = move.SettleY(fromY);
        HSSideLiftDebug.Info("Stopping safely at Y" + settle + " - " + reason);
        move.TargetY = settle;
        LiftState = HSSideLiftState.Stopping;
    }

    void Arrive(int y)
    {
        Push();
        var world = GameManager.Instance.World;
        var problem = HSSideLiftCar.PlaceInWorld(world, y, cells);
        if (problem != null)
        {
            EnterError("cannot put the car back at Y" + y + ": " + problem);
            return;
        }
        D.SetAlong(y);
        HSSideLiftConfiguration.Save();
        HSSideLiftCar.ClearJournal();
        StopMoveSound();
        PlayAt(D.ArriveSound, y);
        HSSideLiftNet.BroadcastMoveEnd(D.ElevatorId, y);
        HSSideLiftDebug.Info("Arrived at Y" + y + " (" + HSSideLiftConfiguration.FloorLabel(y) + ")");

        // Keep the moving copy a moment so the player stays supported while the chunk rebuilds its collision.
        releasing = move.Root;
        releaseAt = Time.time + ReleaseDelay;
        move.Root = null;
        cells = null;
        errorReason = null;
        LiftState = HSSideLiftState.Idle;
        HSSideLiftDoors.OpenAtCar(world);
    }

    // --- sounds (vanilla sound names from config) ---

    string loopPlaying;
    AudioSource customLoop;

    void StartMoveSound(GameObject carRoot)
    {
        try
        {
            customLoop = HSSideLiftSounds.StartMoveLoop(carRoot);
            if (customLoop != null) return;
            var name = D.MoveLoopSound;
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (string.IsNullOrEmpty(name) || player == null) return;
            Audio.Manager.PlayInsidePlayerHead(name, player.entityId, 0f, true, true);
            loopPlaying = name;
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Warn("Move sound failed: " + e.Message);
        }
    }

    void StopMoveSound()
    {
        try
        {
            if (customLoop != null) { customLoop.Stop(); Destroy(customLoop); }
            customLoop = null;
            if (loopPlaying == null) return;
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player != null) Audio.Manager.StopLoopInsidePlayerHead(loopPlaying, player.entityId);
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Warn("Move sound stop failed: " + e.Message);
        }
        loopPlaying = null;
    }

    void PlayAt(string sound, int baseY)
    {
        try
        {
            var center = new Vector3(HSSideLiftCar.OriginX(baseY) + D.SizeX * 0.5f, D.ParkY + 1f, HSSideLiftCar.OriginZ(baseY) + D.SizeZ * 0.5f);
            if (HSSideLiftSounds.PlayDingAt(center) || string.IsNullOrEmpty(sound)) return;
            Audio.Manager.BroadcastPlay(center, sound);
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Warn("Sound " + sound + " failed: " + e.Message);
        }
    }

    void EnterError(string reason)
    {
        StopMoveSound();
        if (LiftState != HSSideLiftState.Error || errorReason != reason)
            HSSideLiftDebug.Error(reason + ". The car copy stays in place; retrying every " + RetryInterval + "s.");
        errorReason = reason;
        LiftState = HSSideLiftState.Error;
        nextRetry = Time.time + RetryInterval;
    }

    void FinishRelease()
    {
        if (releasing != null) Destroy(releasing);
        releasing = null;
    }

    // --- shutdown ---

    public static void BeginRemoteMove(string liftId, int parkedY, int targetY, float curY, List<HSSideLiftCell> captured)
    {
        var d = HSSideLiftConfiguration.ById(liftId);
        if (!Bind(d) || captured == null || captured.Count == 0) return;
        var c = Ensure(d);
        if (c == null) return;
        c.Push();
        c.FinishRelease();
        c.StopPreviewInternal();
        c.fromY = parkedY;
        c.cells = captured;
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var root = HSSideLiftCar.BuildVisual(world, captured, true, parkedY);
        c.move.Begin(root, parkedY, targetY);
        c.move.CurY = curY;
        c.move.Apply();
        c.LiftState = targetY > parkedY ? HSSideLiftState.MovingUp : HSSideLiftState.MovingDown;
        c.StartMoveSound(root);
        HSSideLiftDebug.Info("Client car " + liftId + " moving Y" + parkedY + " -> Y" + targetY);
    }

    public static void FinishRemoteMove(string liftId, int y)
    {
        var d = HSSideLiftConfiguration.ById(liftId);
        if (!Bind(d)) return;
        var c = Of(d);
        if (c == null) return;
        c.Push();
        d.CurrentY = y;
        c.StopMoveSound();
        c.PlayAt(d.ArriveSound, y);
        c.releasing = c.move.Root;
        c.releaseAt = Time.time + ReleaseDelay;
        c.move.Root = null;
        c.cells = null;
        c.errorReason = null;
        c.LiftState = HSSideLiftState.Idle;
        HSSideLiftDebug.Info("Client car " + liftId + " arrived Y" + y);
    }

    public static void SendActiveMoves(ClientInfo ci)
    {
        if (ci == null) return;
        foreach (var c in all)
        {
            if (c == null || c.Bound == null || !c.IsThisMoving || c.cells == null) continue;
            HSSideLiftNet.SendMoveStartTo(ci, c.Bound.ElevatorId, c.fromY, c.move.TargetY, c.move.CurY, c.cells);
        }
    }

    public static void OnWorldShuttingDown()
    {
        HSSideLiftCable.DisposeAll();
        foreach (var c in all.ToArray())
        {
            if (c == null) continue;
            c.Push();
            c.StopMoveSound();
            c.StopPreviewInternal();
            c.FinishRelease();
            if (!HSSideLiftNet.IsAuthority)
            {
                c.move.Destroy();
                c.cells = null;
                c.LiftState = HSSideLiftState.Idle;
                continue;
            }
            if (c.cells == null)
            {
                c.LiftState = HSSideLiftState.Idle;
                continue;
            }
            var world = GameManager.Instance.World;
            int settle = c.move.SettleY(c.fromY);
            var problem = world != null ? HSSideLiftCar.PlaceInWorld(world, settle, c.cells) : "no world";
            if (problem != null && world != null && settle != c.fromY)
            {
                settle = c.fromY;
                problem = HSSideLiftCar.PlaceInWorld(world, settle, c.cells);
            }
            if (problem == null)
            {
                D.CurrentY = settle;
                HSSideLiftConfiguration.Save();
                HSSideLiftCar.ClearJournal();
                HSSideLiftDebug.Info("World closing mid-move: " + D.ElevatorId + " put back at Y" + settle + ".");
            }
            else
            {
                HSSideLiftDebug.Error("World closing mid-move and " + D.ElevatorId + " could not be placed (" + problem + "). Journal kept for next load.");
            }
            c.move.Destroy();
            c.cells = null;
            c.LiftState = HSSideLiftState.Idle;
        }
    }

    // --- preview ---

    public static string StartPreview()
    {
        if (instance == null) return "HSSideLift not started";
        if (IsMoving) return "lift is moving";
        var world = GameManager.Instance.World;
        List<HSSideLiftCell> captured;
        int fromY;
        var problem = HSSideLiftCar.CaptureParked(world, out captured, out fromY);
        if (captured == null || captured.Count == 0) return "Nothing to preview: " + problem;
        instance.StopPreviewInternal();
        instance.preview = HSSideLiftCar.BuildVisual(world, captured, true, fromY);
        instance.preview.transform.position = HSSideLiftCar.UnityPos(D.CurrentY) + new Vector3(D.SizeX + 3, 0, 0);
        instance.previewUntil = Time.time + PreviewSeconds;
        return "Preview of " + captured.Count + " blocks shown " + (D.SizeX + 3) + " blocks east of the car for " + PreviewSeconds + "s. No world change."
            + (problem != null ? "\nNote: a real move would be refused: " + problem : "");
    }

    public static string StopPreview()
    {
        if (instance == null) return "HSSideLift not started";
        instance.StopPreviewInternal();
        return "Preview removed.";
    }

    void StopPreviewInternal()
    {
        if (preview != null) Destroy(preview);
        preview = null;
    }

    // --- status ---

    public static string StatusText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(HSSideLiftConfiguration.ListLifts());
        sb.AppendLine("Editing: " + HSSideLiftConfiguration.Summary());
        sb.AppendLine("State: " + State + (instance != null && instance.errorReason != null ? " (" + instance.errorReason + ")" : "")
            + (instance != null && instance.recovering ? " [restoring from journal]" : ""));
        foreach (var p in D.Panels)
            sb.AppendLine("  floor " + p.Stop + " panel " + p.Pos + " wired to power=" + HSSideLiftPower.IsPanelPowered(p.Pos));
        var missing = new List<string>();
        if (!D.HasCar) missing.Add("car (HSSideLift corner1 / corner2)");
        if (!D.HasUpper) missing.Add("at least two floors (HSSideLift floor add <name>)");
        if (D.HasCar && HSSideLiftConfiguration.FloorAt(D.CurrentY) == null) missing.Add("car is not at a floor (Y" + D.CurrentY + ")");
        string problem;
        if (!HSSideLiftPower.HasWorkingPower(out problem)) missing.Add(problem);
        if (D.HasCar && State == HSSideLiftState.Idle)
        {
            var carProblem = HSSideLiftCar.ValidateCarAt(GameManager.Instance.World, D.CurrentY);
            if (carProblem != null) missing.Add(carProblem);
        }
        sb.Append(missing.Count == 0 ? "Ready." : "Not ready: " + string.Join("; ", missing.ToArray()));
        return sb.ToString();
    }
}
