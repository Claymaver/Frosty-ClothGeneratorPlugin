using System;
using System.Windows;
using System.Windows.Media;
using Frosty.Core;
using Frosty.Core.Windows;
using Frosty.Controls;
using FrostySdk.Managers;
using ClothDataPlugin.Windows;

namespace ClothDataPlugin.Extensions
{
    /// <summary>
    /// Context menu extension for generating cloth data from mesh assets
    /// Right-click on a SkinnedMeshAsset to open the Cloth Data Generator window
    /// </summary>
    public class ClothDataContextMenuExtension : DataExplorerContextMenuExtension
    {
        public override string ContextItemName => "Generate Cloth Data";

        public override ImageSource Icon => null;

        public override RelayCommand ContextItemClicked => new RelayCommand(
            (o) =>
            {
                // Get selected asset
                AssetEntry selectedEntry = App.EditorWindow.DataExplorer.SelectedAsset;
                if (selectedEntry == null)
                {
                    FrostyMessageBox.Show("No asset selected", "Cloth Data Generator", MessageBoxButton.OK);
                    return;
                }

                // Verify it's a mesh asset
                if (!selectedEntry.Type.Contains("MeshAsset"))
                {
                    FrostyMessageBox.Show("Please select a Skinned Mesh Asset to generate cloth data.", 
                        "Cloth Data Generator", MessageBoxButton.OK);
                    return;
                }

                // Cast to EbxAssetEntry
                EbxAssetEntry ebxEntry = selectedEntry as EbxAssetEntry;
                if (ebxEntry == null)
                {
                    FrostyMessageBox.Show("Selected asset is not an EBX asset.", "Cloth Data Generator", MessageBoxButton.OK);
                    return;
                }

                try
                {
                    // Open the generator window with the selected asset
                    var window = new ClothDataGeneratorWindow(ebxEntry);
                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    FrostyMessageBox.Show($"Error opening cloth data generator: {ex.Message}", 
                        "Cloth Data Generator", MessageBoxButton.OK);
                }
            },
            (o) =>
            {
                // Only enable for mesh assets
                AssetEntry selectedEntry = App.EditorWindow?.DataExplorer?.SelectedAsset;
                return selectedEntry != null && selectedEntry.Type.Contains("MeshAsset");
            }
        );
    }
}
