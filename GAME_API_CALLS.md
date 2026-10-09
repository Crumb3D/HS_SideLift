# HS_SideLift — game calls (from Source)

Not extracted from the game DLL. This is `HS_SideLift/Source`. Unity / System / Harmony / our types skipped.

3.2 vs 3.3: `7DaysRef/HS_SideLift_3.2_vs_3.3.md`

## Inherits
- `Block`
- `BlockPowered`
- `ConsoleCmdAbstract`
- `IModApi`
- `ItemAction`
- `NetPackage`

## HarmonyPatch / typeof game types
- `BlockCompositeTileEntity`
- `BlockValue`
- `Entity`
- `EntityAlive`
- `EntityPlayerLocal`
- `NetPackageManager`
- `PlayerMoveController`
- `string`
- `TextureAtlasBlocks`
- `TextureFullArray`
- `TileEntityPowered`
- `Vector3i`
- `WorldBase`
- `XUiC_InteractionPrompt`

## Type.Method (static / nested)
- `Block.GetBlockName`
- `Block.GetBlockValue`
- `Block.GetLightValue`
- `Block.GetLocalizedBlockName`
- `Car.InverseTransformPoint`
- `Cells.Add`
- `ExcludedColumns.Add`
- `ExcludedColumns.Clear`
- `ExcludedColumns.RemoveAll`
- `FieldType.GetGenericArguments`
- `Floors.Add`
- `Floors.Clear`
- `Floors.Find`
- `Floors.FindIndex`
- `Floors.Remove`
- `Floors.RemoveAll`
- `Floors.Sort`
- `GameIO.GetSaveGameDir`
- `GameIO.GetUserGameDataDir`
- `GameManager.ShowTooltip`
- `GameManager.ShowTooltipMP`
- `GameStartDone.RegisterHandler`
- `Instance.GetPowerItemByWorldPos`
- `Instance.Output`
- `ItemClass.GetForId`
- `Lifts.Add`
- `Lifts.AddRange`
- `Lifts.Clear`
- `Lifts.Contains`
- `Lifts.Find`
- `Lifts.FindIndex`
- `Localization.Get`
- `Log.Error`
- `Log.Out`
- `Log.Warning`
- `Manager.BroadcastPlay`
- `Manager.BroadcastPlayByLocalPlayer`
- `Manager.PlayInsidePlayerHead`
- `Manager.StopLoopInsidePlayerHead`
- `Name.Trim`
- `Panels.Add`
- `Panels.Clear`
- `Panels.Exists`
- `Panels.Find`
- `Panels.FindAll`
- `Panels.RemoveAll`
- `PassThroughBlocks.Add`
- `PassThroughBlocks.Exists`
- `PassThroughBlocks.RemoveAll`
- `PlayerSpawnedInWorld.RegisterHandler`
- `Text.IndexOf`
- `Text.StringBuilder`
- `Versioning.TargetFrameworkAttribute`
- `World.GetPrimaryPlayer`
- `WorldShuttingDown.RegisterHandler`
- `XUiC_InteractionPrompt.SetText`
- `XUiC_Radial.RadialContextHoldingSlotIndex`

## new
- `new BlockActivationCommand`
- `new BlockChangeInfo`
- `new BlockValue`
- `new string`
- `new Vector3i`

## override (must still exist on 3.3 base)
- `CanInteract`
- `CreateTileEntity`
- `Execute`
- `ExecuteAction`
- `GetActivationText`
- `GetBlockActivationCommands`
- `getCommands`
- `getDescription`
- `getHelp`
- `GetLength`
- `HasBlockActivationCommands`
- `HasRadial`
- `OnBlockActivated`
- `OnBlockEntityTransformAfterActivated`
- `ProcessPackage`
- `read`
- `SetupRadial`
- `write`

## By file (line)
### HSGameVersion.cs
- `20: Log.Error`
- `25: Log.Error`
- `31: Log.Out`

### HSSideLiftCabinLights.cs
- `94: new Vector3i`

### HSSideLiftCar.cs
- `56: new Vector3i`
- `130: ExcludedColumns.RemoveAll`
- `151: ExcludedColumns.Add`
- `225: new BlockChangeInfo`
- `266: new Vector3i`
- `271: new BlockChangeInfo`
- `319: new Vector3i`
- `332: new Vector3i`
- `333: new Vector3i`
- `337: new Vector3i`
- `338: new Vector3i`
- `354: Block.GetBlockName`
- `360: Block.GetBlockValue`
- `361: Block.GetBlockValue`
- `362: Block.GetBlockValue`
- `372: new Vector3i`
- `376: new BlockChangeInfo`
- `389: new Vector3i`
- `427: new BlockChangeInfo`
- `427: new Vector3i`
- `433: new Vector3i`
- `447: new BlockChangeInfo`
- `459: new Vector3i`
- `678: new Vector3i`
- `689: Block.GetBlockName`
- `704: Block.GetLocalizedBlockName`
- `708: Block.GetBlockName`
- `744: new BlockChangeInfo`
- `764: Block.GetBlockName`
- `772: Block.GetLocalizedBlockName`
- `801: Block.GetBlockName`
- `809: Block.GetLocalizedBlockName`
- `835: new string`
- `867: new BlockChangeInfo`
- `868: new BlockChangeInfo`
- `892: new Vector3i`
- `922: new BlockChangeInfo`
- `945: new Vector3i`
- `951: new Vector3i`
- `1009: new Vector3i`
- `1014: Block.GetBlockName`
- `1021: new Vector3i`
- `1092: ItemClass.GetForId`
- `1129: Block.GetBlockName`
- `1134: Block.GetLightValue`
- `1244: Cells.Add`
- `1302: new Vector3i`
- `1304: new BlockValue`
- `1309: Block.GetBlockName`
- `1339: new BlockChangeInfo`
- `1366: new Vector3i`
- `1375: new BlockChangeInfo`
- `1376: Block.GetBlockName`
- `1393: Block.GetBlockName`
- `1408: new Vector3i`
- `1410: new BlockValue`

### HSSideLiftCommands.cs
- `4: inherits ConsoleCmdAbstract`
- `6: override getCommands`
- `11: override getDescription`
- `19: override getHelp`
- `52: override Execute`
- `81: Instance.Output`

### HSSideLiftConfiguration.cs
- `12: new Vector3i`
- `169: GameIO.GetSaveGameDir`
- `257: Lifts.Add`
- `280: Lifts.Add`
- `298: Lifts.AddRange`
- `309: Lifts.Add`
- `318: Lifts.Clear`
- `320: Lifts.Add`
- `338: PassThroughBlocks.RemoveAll`
- `340: PassThroughBlocks.Exists`
- `341: PassThroughBlocks.Add`
- `342: PassThroughBlocks.Exists`
- `343: PassThroughBlocks.Add`
- `344: PassThroughBlocks.Exists`
- `345: PassThroughBlocks.Add`
- `346: PassThroughBlocks.Exists`
- `347: PassThroughBlocks.Add`
- `348: PassThroughBlocks.Exists`
- `349: PassThroughBlocks.Add`
- `350: PassThroughBlocks.Exists`
- `351: PassThroughBlocks.Add`
- `352: PassThroughBlocks.Exists`
- `353: PassThroughBlocks.Add`
- `354: PassThroughBlocks.Exists`
- `355: PassThroughBlocks.Add`
- `356: PassThroughBlocks.Exists`
- `357: PassThroughBlocks.Add`
- `358: PassThroughBlocks.Exists`
- `359: PassThroughBlocks.Add`
- `364: Floors.Add`
- `365: Floors.Add`
- `383: Lifts.FindIndex`
- `385: Lifts.Contains`
- `385: Lifts.Add`
- `403: Lifts.Find`
- `427: Lifts.Add`
- `448: Panels.Exists`
- `537: new Vector3i`
- `556: Floors.RemoveAll`
- `557: Floors.Sort`
- `573: Floors.Find`
- `578: Floors.Find`
- `583: Floors.FindIndex`
- `595: Floors.Find`

### HSSideLiftController.cs
- `216: Panels.Find`
- `218: Floors.Find`
- `294: Localization.Get`
- `302: new Vector3i`
- `414: Localization.Get`
- `759: World.GetPrimaryPlayer`
- `761: Manager.PlayInsidePlayerHead`
- `783: World.GetPrimaryPlayer`
- `784: Manager.StopLoopInsidePlayerHead`
- `799: Manager.BroadcastPlay`

### HSSideLiftDebug.cs
- `11: Log.Out`
- `16: Log.Out`
- `21: Log.Warning`
- `26: Log.Error`

### HSSideLiftDoors.cs
- `106: new Vector3i`
- `168: new Vector3i`
- `196: new Vector3i`
- `197: new Vector3i`
- `213: new Vector3i`
- `394: new Vector3i`
- `401: new Vector3i`
- `421: Block.GetBlockName`

### HSSideLiftFloorMenu.cs
- `27: new BlockActivationCommand`
- `43: Localization.Get`

### HSSideLiftFloorSigns.cs
- `88: new Vector3i`
- `93: Block.GetBlockName`
- `98: Block.GetBlockValue`
- `105: new BlockChangeInfo`
- `114: Name.Trim`

### HSSideLiftMod.cs
- `6: inherits IModApi`
- `18: GameIO.GetUserGameDataDir`
- `28: GameStartDone.RegisterHandler`
- `29: WorldShuttingDown.RegisterHandler`
- `30: PlayerSpawnedInWorld.RegisterHandler`

### HSSideLiftNet.cs
- `60: FieldType.GetGenericArguments`
- `167: new Vector3i`
- `173: new Vector3i`
- `196: Localization.Get`
- `210: GameManager.ShowTooltip`
- `225: GameManager.ShowTooltip`
- `228: GameManager.ShowTooltipMP`
- `246: inherits NetPackage`
- `280: override read`
- `290: new Vector3i`
- `306: new BlockValue`
- `319: override write`
- `354: override ProcessPackage`
- `427: override GetLength`
- `435: HarmonyPatch NetPackageManager`

### HSSideLiftPaint.cs
- `85: Text.StringBuilder`
- `248: new Vector3i`

### HSSideLiftPower.cs
- `17: Instance.GetPowerItemByWorldPos`

### HSSideLiftSettings.cs
- `35: GameIO.GetSaveGameDir`

### HSSideLiftSetup.cs
- `13: GameManager.ShowTooltip`
- `14: Instance.Output`
- `149: Panels.FindAll`
- `184: ExcludedColumns.Clear`
- `223: ExcludedColumns.RemoveAll`
- `224: ExcludedColumns.Add`
- `227: Block.GetBlockName`
- `229: Block.GetBlockName`
- `257: Floors.Add`
- `283: Floors.Add`
- `305: Floors.Remove`
- `327: Floors.RemoveAll`
- `328: Floors.Sort`
- `356: Floors.Sort`
- `445: Floors.Clear`
- `446: Panels.Clear`
- `447: ExcludedColumns.Clear`
- `460: Floors.Add`
- `479: Panels.Clear`
- `501: Block.GetBlockName`
- `505: Panels.RemoveAll`
- `506: Panels.Add`
- `521: new Vector3i`

### HSSideLiftSounds.cs
- `236: Car.InverseTransformPoint`

### ItemActionHSSideLiftTool.cs
- `7: inherits ItemAction`
- `48: override HasRadial`
- `53: override ExecuteAction`
- `59: GameManager.ShowTooltip`
- `59: Localization.Get`
- `67: override CanInteract`
- `69: Localization.Get`
- `72: override SetupRadial`
- `85: Localization.Get`
- `86: Localization.Get`
- `87: Localization.Get`
- `88: Localization.Get`
- `89: Localization.Get`
- `90: Localization.Get`
- `91: Localization.Get`
- `92: Localization.Get`
- `93: Localization.Get`
- `97: XUiC_Radial.RadialContextHoldingSlotIndex`

### Blocks\BlockHSSideLiftInsidePanel.cs
- `4: inherits Block`
- `6: override HasBlockActivationCommands`
- `11: override GetBlockActivationCommands`
- `26: override GetActivationText`
- `33: Localization.Get`
- `35: Localization.Get`
- `47: override OnBlockEntityTransformAfterActivated`
- `54: override OnBlockActivated`
- `59: override OnBlockActivated`
- `70: GameManager.ShowTooltip`
- `70: Localization.Get`
- `78: GameManager.ShowTooltip`
- `78: Localization.Get`

### Blocks\BlockHSSideLiftOutsidePanel.cs
- `4: inherits BlockPowered`
- `6: override CreateTileEntity`
- `11: override HasBlockActivationCommands`
- `16: override GetBlockActivationCommands`
- `24: new BlockActivationCommand`
- `33: override GetActivationText`
- `40: Localization.Get`
- `42: Localization.Get`
- `45: Localization.Get`
- `58: Localization.Get`
- `62: override OnBlockEntityTransformAfterActivated`
- `68: override OnBlockActivated`
- `79: GameManager.ShowTooltip`
- `79: Localization.Get`

### obj\Release\.NETFramework,Version=v4.8.AssemblyAttributes.cs
- `4: Versioning.TargetFrameworkAttribute`

### Patches\HSSideLiftDoorPatch.cs
- `7: HarmonyPatch BlockCompositeTileEntity`
- `32: GameManager.ShowTooltip`
- `32: Localization.Get`
- `69: GameManager.ShowTooltip`
- `69: Localization.Get`
- `70: Manager.BroadcastPlayByLocalPlayer`
- `83: Localization.Get`
- `87: Localization.Get`
- `87: Localization.Get`
- `89: GameManager.ShowTooltip`
- `91: Manager.BroadcastPlayByLocalPlayer`
- `133: HarmonyPatch BlockCompositeTileEntity`
- `173: HarmonyPatch BlockCompositeTileEntity`
- `186: Localization.Get`
- `186: Localization.Get`

### Patches\HSSideLiftLightPatch.cs
- `3: HarmonyPatch TileEntityPowered`

### Patches\HSSideLiftToolPatch.cs
- `6: HarmonyPatch PlayerMoveController`
- `25: XUiC_InteractionPrompt.SetText`
- `25: Localization.Get`
- `55: XUiC_InteractionPrompt.SetText`
- `64: Localization.Get`
- `81: HarmonyPatch XUiC_InteractionPrompt`
- `88: Localization.Get`
- `90: Text.IndexOf`
- `96: XUiC_InteractionPrompt.SetText`


