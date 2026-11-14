using System;
using System.Collections.Generic;

// Hold-E floor list. The radial labels each entry with Localization.Get("blockcommand_" + text), so the
// label for each numbered slot is written into the live localization table when the menu is built.
public static class HSSideLiftFloorMenu
{
    const string Prefix = "HSSideLiftFloor";

    public static List<BlockActivationCommand> Commands()
    {
        return Commands(HSSideLiftConfiguration.Data);
    }

    public static List<BlockActivationCommand> Commands(HSSideLiftConfigData d)
    {
        var list = new List<BlockActivationCommand>();
        if (d == null || d.Floors == null) return list;
        var ctrl = HSSideLiftController.Of(d);
        var moving = ctrl != null && ctrl.IsThisMoving;
        // Two stops: the only enabled entry is the other stop, so a tap of E goes there.
        // More than two: this list is the hold-E wheel. A tap is thrown away by the radial patch.
        if (d.Floors.Count == 2)
        {
            int other = OtherFloorIndex(d);
            if (other >= 0)
            {
                var f = d.Floors[other];
                var text = Prefix + other;
                SetLabel("blockcommand_" + text, HSSideLiftConfiguration.FloorDisplayName(f.Name));
                list.Add(new BlockActivationCommand(text, "electric_switch", !moving, false, null));
            }
            return list;
        }
        int hereIndex = -1;
        for (int i = 0; i < d.Floors.Count; i++)
            if (d.Floors[i].Y == d.CurrentY) hereIndex = i;
        for (int i = d.Floors.Count - 1; i >= 0; i--)
        {
            if (i == hereIndex) continue;
            var f = d.Floors[i];
            var text = Prefix + i;
            SetLabel("blockcommand_" + text, HSSideLiftConfiguration.FloorDisplayName(f.Name));
            list.Add(new BlockActivationCommand(text, "electric_switch", !moving, false, null));
        }
        return list;
    }

    public static int OtherFloorIndex(HSSideLiftConfigData d)
    {
        if (d == null || d.Floors == null || d.Floors.Count != 2) return -1;
        if (d.Floors[0].Y == d.CurrentY) return 1;
        if (d.Floors[1].Y == d.CurrentY) return 0;
        return d.CurrentY < d.Floors[1].Y ? 1 : 0;
    }

    public static string Prompt(HSSideLiftConfigData d)
    {
        if (d != null && d.Floors != null && d.Floors.Count == 2)
            return Localization.Get("HSSideLiftNextStop");
        return Localization.Get("HSSideLiftChooseFloor");
    }

    // Floor index for a menu command, or -1 when it is not one of ours.
    public static int Parse(string commandName)
    {
        return Parse(commandName, HSSideLiftConfiguration.Data);
    }

    public static int Parse(string commandName, HSSideLiftConfigData d)
    {
        if (d == null || d.Floors == null) return -1;
        if (commandName == null || !commandName.StartsWith(Prefix, StringComparison.Ordinal)) return -1;
        int i;
        if (!int.TryParse(commandName.Substring(Prefix.Length), out i) || i < 0 || i >= d.Floors.Count) return -1;
        return i;
    }

    static void SetLabel(string key, string label)
    {
        try
        {
            var dict = Localization.Dictionary;
            string[] row;
            if (dict.TryGetValue(key, out row) && row != null && row.Length > 0 && row[row.Length - 1] == label) return;
            int columns = 0;
            string[] sample;
            if (dict.TryGetValue("blockcommand_open", out sample) && sample != null) columns = sample.Length;
            if (columns == 0) foreach (var v in dict.Values) { if (v != null) { columns = v.Length; break; } }
            if (columns == 0) return;
            row = new string[columns];
            for (int c = 0; c < columns; c++) row[c] = label;
            dict[key] = row;
        }
        catch (Exception e)
        {
            HSSideLiftDebug.Warn("Floor label failed for " + key + ": " + e.Message);
        }
    }
}
