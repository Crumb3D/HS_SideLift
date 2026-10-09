using System;
using System.Collections.Generic;

public class ConsoleCmdHSSideLift : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "hssidelift" };
    }

    public override string getDescription()
    {
        return "HSSideLift admin: leftover setup and debug. Players use the Side Lift Setup Tool (hold E).";
    }

    // Same level as help/give-style admin commands; F1 console is locked to admins.
    public override int DefaultPermissionLevel { get { return 1000; } }

    public override string getHelp()
    {
        return
            "Admin only. Players: craft/hold the Side Lift Setup Tool and hold E.\n" +
            "Aim at blocks while typing the setup commands.\n" +
            "HSSideLift corner1 | corner2      - mark opposite corners of the car\n" +
            "HSSideLift type ped | vehicle     - START a new lift. ped = 3D cabin + elevator doors. vehicle = floor platform only + garage/roll-up doors. Never overwrites another lift.\n" +
            "HSSideLift list                   - list every lift (* = the one you are editing)\n" +
            "HSSideLift select                 - aim at a lift: make that one the one you are editing\n" +
            "HSSideLift floor add <name>       - aim at a landing floor block: adds a floor (G is set with the car)\n" +
            "HSSideLift floor ground           - aim at a landing: that height becomes G. Floors above become 1, 2, …\n" +
            "HSSideLift floor remove <name>    - forget a floor\n" +
            "HSSideLift floor list             - show floors\n" +
            "HSSideLift scheme gb | us         - floor labels (also FloorScheme in the world-save HSSideLift.json: gb or us). GB default: G, 1, 2. US: G becomes 1\n" +
            "HSSideLift music on | off         - host: cabin MP3s on/off for every client. Empty assets/music is silent\n" +
            "HSSideLift flicker on | off       - host: cabin bulb flicker (JSON FlickerLights, default on)\n" +
            "HSSideLift panel                  - register the aimed outside button panel (floor found from its height)\n" +
            "HSSideLift panel clear            - forget all registered panels\n" +
            "HSSideLift go <floor>             - send the car to a floor\n" +
            "HSSideLift up | down              - move the car one floor\n" +
            "HSSideLift speed <blocks/sec>     - travel speed\n" +
            "HSSideLift preview [off]          - show the moving copy beside the car, no world change\n" +
            "HSSideLift status                 - show setup and what is missing\n" +
            "HSSideLift reset                  - forget the car selection\n" +
            "HSSideLift grow <n>               - widen the car box by n blocks on every side (to include outer walls)\n" +
            "HSSideLift exclude [rows]         - aim at the doorway platform (any block): it stays put. Setup tool: Exclude landing\n" +
            "HSSideLift exclude list | clear   - show / remove stationary landing columns\n" +
            "HSSideLift cleanup <y>            - remove a leftover copy of the car whose floor is at Y (only blocks matching the car)\n" +
            "HSSideLift debris                 - delete dirt/terrain/rubble in this lift's shaft (not the car, doors, panels, or pass-through)\n" +
            "HSSideLift debug                  - toggle verbose logging";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            var sub = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            var arg = _params != null && _params.Count > 1 ? _params[1].ToLowerInvariant() : "";
            var extra = _params != null && _params.Count > 2 ? _params[2] : "";
            var world = GameManager.Instance.World;
            EntityPlayerLocal player = null;
            if (world != null)
            {
                player = world.GetPrimaryPlayer();
                if (player == null)
                {
                    var locals = world.GetLocalPlayers();
                    if (locals != null && locals.Count > 0) player = locals[0] as EntityPlayerLocal;
                }
            }
            Out(HSSideLiftSetup.Execute(sub, arg, extra, player));
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Error("Command failed", e);
            Out("HSSideLift command failed: " + e.Message);
        }
    }

    static void Out(string s)
    {
        SdtdConsole.Instance.Output(s);
    }
}
