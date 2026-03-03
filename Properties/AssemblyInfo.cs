using Frosty.Core.Attributes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using ClothDataPlugin;
using ClothDataPlugin.Extensions;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("c8a7b3d1-5e2f-4a9b-8c1d-6f3e2a4b5c7d")]

// Plugin information
[assembly: PluginDisplayName("Cloth Data Generator")]
[assembly: PluginAuthor("Claymaver")]
[assembly: PluginVersion("1.0.0.0")]


// Register the Tools menu extension
[assembly: RegisterMenuExtension(typeof(ClothDataMenuExtension))]

// Register the context menu extension for mesh assets
[assembly: RegisterDataExplorerContextMenu(typeof(ClothDataContextMenuExtension))]
