using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Hnl.CadBridge.BridgeCommands))]
[assembly: CommandClass(typeof(Hnl.CadBridge.BridgeCommands))]
[assembly: CommandClass(typeof(Hnl.CadBridge.NativePaletteCommands))]
