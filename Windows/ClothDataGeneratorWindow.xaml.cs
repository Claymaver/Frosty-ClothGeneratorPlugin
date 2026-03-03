using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Managers;
using FrostySdk.IO;
using ClothDataPlugin.Core;
using ClothDataPlugin.Resources;

namespace ClothDataPlugin.Windows
{
    /// <summary>
    /// Cloth Data Generator Window - copies cloth data from template mesh to target mesh
    /// 
    /// WORKFLOW:
    /// 1. Target mesh is auto-loaded from selection (the mesh you want to add cloth to)
    /// 2. User selects a template mesh asset that has cloth (e.g., Leia's skirt)
    /// 3. Plugin auto-finds the template's ClothWrapping and EACloth resources
    /// 4. Plugin auto-finds target mesh's existing cloth resources (if any)
    /// 5. Generate copies the template cloth data to the target
    /// 
    /// BINARY FORMAT:
    /// - GetRes() returns data WITHOUT the 16-byte header (starts at BNRY)
    /// - ModifyRes() expects data WITH the 16-byte header
    /// - Header: [size (4 bytes, = content length)] [12 zeros] [BNRY content...]
    /// </summary>
    public partial class ClothDataGeneratorWindow : FrostyDockableWindow
    {
        // Target mesh (the one we want to add cloth to)
        private EbxAssetEntry _targetMeshAssetEntry;
        private MeshData _targetMeshData;
        
        // Template mesh (has existing cloth we want to copy from)
        private EbxAssetEntry _templateMeshAssetEntry;
        
        // Template cloth resource entries (auto-detected from template mesh)
        private ResAssetEntry _templateClothWrappingEntry;
        private ResAssetEntry _templateEAClothEntry;
        
        // Target cloth resource entries (auto-detected from target mesh, to replace)
        private ResAssetEntry _targetClothWrappingEntry;
        private ResAssetEntry _targetEAClothEntry;
        
        // Raw template bytes (from GetRes, WITHOUT 16-byte header)
        private byte[] _templateClothWrappingBytes;
        private byte[] _templateEAClothBytes;
        
        // Parsed cloth data for adaptation
        private ClothWrappingAssetParsed _templateClothWrappingParsed;
        
        // Target mesh MeshSet for vertex extraction
        private MeshSetPlugin.Resources.MeshSet _targetMeshSet;
        private MeshSetPlugin.Resources.MeshSet _templateMeshSet;
        
        // Bundle IDs for new resources
        private List<int> _meshBundles = new List<int>();

        public ClothDataGeneratorWindow() : this(null)
        {
        }

        public ClothDataGeneratorWindow(EbxAssetEntry selectedAsset)
        {
            InitializeComponent();
            
            if (selectedAsset != null)
            {
                _targetMeshAssetEntry = selectedAsset;
            }
        }

        private void FrostyDockableWindow_FrostyLoaded(object sender, EventArgs e)
        {
            if (_targetMeshAssetEntry != null)
            {
                LoadTargetMesh(_targetMeshAssetEntry);
            }
            else
            {
                // Try to get currently selected asset
                AssetEntry currentSelection = App.EditorWindow?.DataExplorer?.SelectedAsset;
                if (currentSelection != null && currentSelection.Type.Contains("MeshAsset"))
                {
                    EbxAssetEntry ebxEntry = currentSelection as EbxAssetEntry;
                    if (ebxEntry != null)
                    {
                        LoadTargetMesh(ebxEntry);
                    }
                }
            }
        }

        #region Target Mesh Loading

        private void LoadTargetMesh(EbxAssetEntry entry)
        {
            try
            {
                _targetMeshAssetEntry = entry;
                AssetPathText.Text = entry.Name;
                
                string baseName = System.IO.Path.GetFileName(entry.Name).ToLower();
                NewResourceNameText.Text = baseName;

                FrostyTaskWindow.Show("Loading Target Mesh", "", (task) =>
                {
                    task.Update("Loading mesh data...");
                    LoadTargetMeshData();
                    
                    task.Update("Finding existing cloth resources...");
                    AutoDetectTargetClothResources(entry.Name);
                });

                if (_targetMeshData != null)
                {
                    MeshInfoText.Text = $"Verts: {_targetMeshData.VertexCount}, Tris: {_targetMeshData.TriangleCount}";
                }

                UpdateTargetResourcesUI();

                StatusText.Text = _targetClothWrappingEntry != null || _targetEAClothEntry != null
                    ? "Target loaded with existing cloth. Select template mesh."
                    : "Target loaded (no cloth found). Select template mesh.";
                    
                UpdateGenerateButton();
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Error loading target: {ex.Message}", "Load Error", MessageBoxButton.OK);
                StatusText.Text = "Error loading target";
            }
        }

        private void LoadTargetMeshData()
        {
            var asset = App.AssetManager.GetEbx(_targetMeshAssetEntry);
            dynamic meshAsset = asset.RootObject;

            _meshBundles.Clear();
            _meshBundles.AddRange(_targetMeshAssetEntry.Bundles);
            if (_targetMeshAssetEntry.AddedBundles != null)
                _meshBundles.AddRange(_targetMeshAssetEntry.AddedBundles);

            ulong resRid = meshAsset.MeshSetResource;
            var resEntry = App.AssetManager.GetResEntry(resRid);

            if (resEntry == null)
                throw new Exception("Could not find mesh set resource");

            _targetMeshSet = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(resEntry);
            _targetMeshData = ExtractMeshData(_targetMeshSet);
        }

        private MeshData ExtractMeshData(MeshSetPlugin.Resources.MeshSet meshSet)
        {
            var meshData = new MeshData();

            if (meshSet.Lods == null || meshSet.Lods.Count == 0)
                throw new Exception("No LODs found");

            var lod = meshSet.Lods[0];
            int vertexCount = 0;
            int indexCount = 0;

            foreach (var section in lod.Sections)
            {
                vertexCount += (int)section.VertexCount;
                // PrimitiveCount is number of triangles, multiply by 3 for indices
                indexCount += (int)section.PrimitiveCount * 3;
            }

            meshData.Vertices = new Math.Vector3d[vertexCount];
            meshData.Indices = new int[indexCount]; // Initialize indices array for proper TriangleCount
            return meshData;
        }

        /// <summary>
        /// Auto-detect cloth resources for the TARGET mesh
        /// Uses strict matching: must end with "_clothwrappingasset" or "_eacloth"
        /// </summary>
        private void AutoDetectTargetClothResources(string meshAssetPath)
        {
            _targetClothWrappingEntry = null;
            _targetEAClothEntry = null;
            
            // Extract the mesh name pattern (e.g., "obiwan_03_skirt" from the path)
            string meshName = System.IO.Path.GetFileName(meshAssetPath).ToLower();
            
            // Get parent path for search scope
            int lastSlash = meshAssetPath.LastIndexOf('/');
            string parentPath = lastSlash > 0 ? meshAssetPath.Substring(0, lastSlash).ToLower() : "";
            
            foreach (var resEntry in App.AssetManager.EnumerateRes())
            {
                string resName = resEntry.Name.ToLower();
                
                // Must be in same directory tree
                if (!resName.StartsWith(parentPath)) continue;
                
                // Strict matching for ClothWrappingAsset (must end with it)
                if (resName.EndsWith("_clothwrappingasset") || resName.EndsWith("clothwrappingasset"))
                {
                    if (_targetClothWrappingEntry == null)
                    {
                        _targetClothWrappingEntry = resEntry;
                        App.Logger.Log($"Auto-detected target ClothWrapping: {resEntry.Name}");
                    }
                }
                // Strict matching for EACloth (must end with "_eacloth")
                else if (resName.EndsWith("_eacloth"))
                {
                    if (_targetEAClothEntry == null)
                    {
                        _targetEAClothEntry = resEntry;
                        App.Logger.Log($"Auto-detected target EACloth: {resEntry.Name}");
                    }
                }
            }
        }

        private void UpdateTargetResourcesUI()
        {
            TargetClothWrappingText.Text = _targetClothWrappingEntry?.Name ?? "(Will create new)";
            TargetEAClothText.Text = _targetEAClothEntry?.Name ?? "(Will create new)";
        }

        #endregion

        #region Template Selection

        /// <summary>
        /// Browse for a mesh asset to use as template
        /// </summary>
        private void BrowseTemplateMesh_Click(object sender, RoutedEventArgs e)
        {
            List<EbxAssetEntry> meshAssets = null;
            
            FrostyTaskWindow.Show("Finding Meshes with Cloth", "", (task) =>
            {
                task.Update("Scanning cloth resources...");
                
                // First pass: collect all parent paths that have cloth resources
                var pathsWithCloth = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var resEntry in App.AssetManager.EnumerateRes())
                {
                    string resName = resEntry.Name.ToLower();
                    if (resName.EndsWith("_clothwrappingasset") || resName.EndsWith("_eacloth"))
                    {
                        // Extract parent path
                        int lastSlash = resEntry.Name.LastIndexOf('/');
                        if (lastSlash > 0)
                        {
                            // Go up one more level for cloth resources in subdirs like /cloth/
                            string parentPath = resEntry.Name.Substring(0, lastSlash);
                            pathsWithCloth.Add(parentPath.ToLower());
                            
                            // Also add grandparent for cases like mesh/cloth/eacloth
                            int secondLastSlash = parentPath.LastIndexOf('/');
                            if (secondLastSlash > 0)
                            {
                                pathsWithCloth.Add(parentPath.Substring(0, secondLastSlash).ToLower());
                            }
                        }
                    }
                }
                
                task.Update($"Found {pathsWithCloth.Count} paths with cloth. Scanning meshes...");
                
                // Second pass: find mesh assets in those paths
                meshAssets = new List<EbxAssetEntry>();
                
                foreach (var ebxEntry in App.AssetManager.EnumerateEbx())
                {
                    if (ebxEntry.Type != null && ebxEntry.Type.Contains("SkinnedMeshAsset"))
                    {
                        string meshPath = ebxEntry.Name.ToLower();
                        int lastSlash = meshPath.LastIndexOf('/');
                        string parentPath = lastSlash > 0 ? meshPath.Substring(0, lastSlash) : meshPath;
                        
                        if (pathsWithCloth.Contains(parentPath))
                        {
                            meshAssets.Add(ebxEntry);
                        }
                    }
                }
                
                meshAssets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            });
            
            if (meshAssets == null || meshAssets.Count == 0)
            {
                FrostyMessageBox.Show("No mesh assets with cloth data found.", "No Templates", MessageBoxButton.OK);
                return;
            }
            
            var dialog = new MeshBrowserDialog(meshAssets, "Select Template Mesh (with cloth)");
            if (dialog.ShowDialog() == true && dialog.SelectedMesh != null)
            {
                LoadTemplateMesh(dialog.SelectedMesh);
            }
        }

        private void LoadTemplateMesh(EbxAssetEntry meshEntry)
        {
            _templateMeshAssetEntry = meshEntry;
            _templateClothWrappingEntry = null;
            _templateEAClothEntry = null;
            _templateClothWrappingBytes = null;
            _templateEAClothBytes = null;
            _templateClothWrappingParsed = null;
            _templateMeshSet = null;
            
            try
            {
                FrostyTaskWindow.Show("Loading Template", "", (task) =>
                {
                    task.Update("Loading template mesh...");
                    LoadTemplateMeshSet(meshEntry);
                    
                    task.Update("Finding cloth resources...");
                    AutoDetectTemplateClothResources(meshEntry.Name);
                    
                    if (_templateClothWrappingEntry != null)
                    {
                        task.Update("Loading ClothWrapping...");
                        LoadTemplateClothWrapping();
                    }
                    
                    if (_templateEAClothEntry != null)
                    {
                        task.Update("Loading EACloth...");
                        LoadTemplateEACloth();
                    }
                });
                
                // Update UI
                TemplateClothWrappingText.Text = _templateClothWrappingEntry?.Name ?? "(Not found)";
                TemplateEAClothText.Text = _templateEAClothEntry?.Name ?? "(Not found)";
                
                if (_templateClothWrappingBytes != null && _templateEAClothBytes != null)
                {
                    StatusText.Text = $"Template loaded: {meshEntry.Name}";
                }
                else
                {
                    StatusText.Text = "Warning: Could not load all template cloth data";
                }
                
                UpdateGenerateButton();
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Error loading template: {ex.Message}", "Error", MessageBoxButton.OK);
                StatusText.Text = "Error loading template";
            }
        }

        private void LoadTemplateMeshSet(EbxAssetEntry meshEntry)
        {
            var asset = App.AssetManager.GetEbx(meshEntry);
            dynamic meshAsset = asset.RootObject;

            ulong resRid = meshAsset.MeshSetResource;
            var resEntry = App.AssetManager.GetResEntry(resRid);

            if (resEntry != null)
            {
                _templateMeshSet = App.AssetManager.GetResAs<MeshSetPlugin.Resources.MeshSet>(resEntry);
                App.Logger.Log($"Loaded template MeshSet: {meshEntry.Name}");
            }
        }

        /// <summary>
        /// Auto-detect cloth resources for the TEMPLATE mesh
        /// Finds matching pairs (ClothWrapping and EACloth with same base name)
        /// </summary>
        private void AutoDetectTemplateClothResources(string meshAssetPath)
        {
            string meshPathLower = meshAssetPath.ToLower();
            int lastSlash = meshPathLower.LastIndexOf('/');
            string parentPath = lastSlash > 0 ? meshPathLower.Substring(0, lastSlash) : "";
            
            // Collect all cloth resources for this mesh
            var clothWrappings = new List<ResAssetEntry>();
            var eaCloths = new List<ResAssetEntry>();
            
            foreach (var resEntry in App.AssetManager.EnumerateRes())
            {
                string resName = resEntry.Name.ToLower();
                
                if (!resName.StartsWith(parentPath)) continue;
                
                if (resName.EndsWith("_clothwrappingasset") || resName.EndsWith("clothwrappingasset"))
                {
                    clothWrappings.Add(resEntry);
                }
                else if (resName.EndsWith("_eacloth"))
                {
                    eaCloths.Add(resEntry);
                }
            }
            
            // Try to find matching pairs by base name
            // e.g., "leia_princess_01_skirt" should match both "_skirt_clothwrappingasset" and "_skirt_eacloth"
            foreach (var cw in clothWrappings)
            {
                // Extract base name from clothwrapping (remove suffix)
                string cwName = cw.Name.ToLower();
                string baseName = cwName.Replace("_clothwrappingasset", "").Replace("clothwrappingasset", "");
                
                // Extract the identifying part (e.g., "skirt" from "leia_princess_01_skirt")
                int cwLastSlash = baseName.LastIndexOf('/');
                string cwFileName = cwLastSlash >= 0 ? baseName.Substring(cwLastSlash + 1) : baseName;
                
                // Find matching EACloth - look for one that contains the same identifier
                foreach (var ec in eaCloths)
                {
                    string ecName = ec.Name.ToLower();
                    string ecBaseName = ecName.Replace("_eacloth", "");
                    int ecLastSlash = ecBaseName.LastIndexOf('/');
                    string ecFileName = ecLastSlash >= 0 ? ecBaseName.Substring(ecLastSlash + 1) : ecBaseName;
                    
                    // Check if they share the same base identifier
                    // e.g., both contain "skirt" or both end with same pattern
                    if (cwFileName == ecFileName || cwFileName.EndsWith(ecFileName) || ecFileName.EndsWith(cwFileName))
                    {
                        _templateClothWrappingEntry = cw;
                        _templateEAClothEntry = ec;
                        App.Logger.Log($"Found matching template pair:");
                        App.Logger.Log($"  ClothWrapping: {cw.Name}");
                        App.Logger.Log($"  EACloth: {ec.Name}");
                        return;
                    }
                }
            }
            
            // Fallback: just use first of each if no matching pair found
            if (clothWrappings.Count > 0)
            {
                _templateClothWrappingEntry = clothWrappings[0];
                App.Logger.Log($"Found template ClothWrapping (no match): {clothWrappings[0].Name}");
            }
            if (eaCloths.Count > 0)
            {
                _templateEAClothEntry = eaCloths[0];
                App.Logger.Log($"Found template EACloth (no match): {eaCloths[0].Name}");
            }
        }

        private void LoadTemplateClothWrapping()
        {
            using (var stream = App.AssetManager.GetRes(_templateClothWrappingEntry))
            {
                _templateClothWrappingBytes = ReadAllBytes(stream);
            }
            App.Logger.Log($"Loaded ClothWrapping: {_templateClothWrappingBytes.Length} bytes");
            
            // Parse the cloth wrapping data for adaptation
            try
            {
                _templateClothWrappingParsed = new ClothWrappingAssetParsed();
                _templateClothWrappingParsed.Read(_templateClothWrappingBytes);
                App.Logger.Log($"Parsed ClothWrapping: {_templateClothWrappingParsed.LodCount} LODs, {_templateClothWrappingParsed.MeshSections.Length} sections, {_templateClothWrappingParsed.MeshSections[0].VertexCount} vertices in LOD0");
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"Could not parse ClothWrapping: {ex.Message}");
                _templateClothWrappingParsed = null;
            }
        }

        private void LoadTemplateEACloth()
        {
            using (var stream = App.AssetManager.GetRes(_templateEAClothEntry))
            {
                _templateEAClothBytes = ReadAllBytes(stream);
            }
            App.Logger.Log($"Loaded EACloth: {_templateEAClothBytes.Length} bytes (content only, no header)");
        }

        private byte[] ReadAllBytes(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        #endregion

        #region Manual Target Browsing

        private void BrowseTargetClothWrapping_Click(object sender, RoutedEventArgs e)
        {
            var entry = BrowseForClothResource("Select ClothWrapping to Replace", "_clothwrappingasset");
            if (entry != null)
            {
                _targetClothWrappingEntry = entry;
                TargetClothWrappingText.Text = entry.Name;
                StatusText.Text = $"Will replace: {entry.Name}";
            }
        }

        private void BrowseTargetEACloth_Click(object sender, RoutedEventArgs e)
        {
            var entry = BrowseForClothResource("Select EACloth to Replace", "_eacloth");
            if (entry != null)
            {
                _targetEAClothEntry = entry;
                TargetEAClothText.Text = entry.Name;
                StatusText.Text = $"Will replace: {entry.Name}";
            }
        }

        private void ClearTargetClothWrapping_Click(object sender, RoutedEventArgs e)
        {
            _targetClothWrappingEntry = null;
            TargetClothWrappingText.Text = "(Will create new)";
            StatusText.Text = "Cleared target ClothWrapping";
        }

        private void ClearTargetEACloth_Click(object sender, RoutedEventArgs e)
        {
            _targetEAClothEntry = null;
            TargetEAClothText.Text = "(Will create new)";
            StatusText.Text = "Cleared target EACloth";
        }

        private ResAssetEntry BrowseForClothResource(string title, string suffix)
        {
            var resources = new List<ResAssetEntry>();
            
            foreach (var resEntry in App.AssetManager.EnumerateRes())
            {
                if (resEntry.Name.ToLower().EndsWith(suffix))
                {
                    resources.Add(resEntry);
                }
            }
            
            if (resources.Count == 0)
            {
                FrostyMessageBox.Show($"No {suffix} resources found.", "No Resources", MessageBoxButton.OK);
                return null;
            }

            resources.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var dialog = new ResourceBrowserDialog(resources, title);
            if (dialog.ShowDialog() == true)
            {
                return dialog.SelectedResource;
            }

            return null;
        }

        #endregion

        private void UpdateGenerateButton()
        {
            generateButton.IsEnabled = _templateClothWrappingBytes != null && _templateEAClothBytes != null;
        }

        /// <summary>
        /// Prepends the 16-byte header to content bytes
        /// Header: [size (4 bytes)] [12 zeros]
        /// This is required because GetRes() strips the header but ModifyRes() expects it
        /// </summary>
        private byte[] PrependHeader(byte[] contentBytes)
        {
            byte[] result = new byte[contentBytes.Length + 16];
            
            // Size field = content length (little-endian)
            uint size = (uint)contentBytes.Length;
            result[0] = (byte)(size & 0xFF);
            result[1] = (byte)((size >> 8) & 0xFF);
            result[2] = (byte)((size >> 16) & 0xFF);
            result[3] = (byte)((size >> 24) & 0xFF);
            
            // Bytes 4-15 are zeros (already initialized)
            
            // Copy content at offset 16
            Array.Copy(contentBytes, 0, result, 16, contentBytes.Length);
            
            return result;
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_templateClothWrappingBytes == null || _templateEAClothBytes == null)
            {
                FrostyMessageBox.Show("Please select a template mesh first.", "Template Required", MessageBoxButton.OK);
                return;
            }

            if (_targetMeshSet == null)
            {
                FrostyMessageBox.Show("Target mesh not loaded.", "Error", MessageBoxButton.OK);
                return;
            }

            string newResourceName = NewResourceNameText.Text.Trim().ToLower();
            var targetClothWrappingEntry = _targetClothWrappingEntry;
            var targetEAClothEntry = _targetEAClothEntry;
            var meshBundles = _meshBundles.ToArray();
            
            byte[] adaptedClothWrappingBytes = null;
            byte[] eaClothBytes = null;

            try
            {
                FrostyTaskWindow.Show("Generating Cloth Data", "", (task) =>
                {
                    // Step 1: Extract target mesh vertices
                    task.Update("Extracting target mesh vertices...");
                    ClothVector3[] targetVertices = ClothDataAdapter.ExtractMeshVertices(_targetMeshSet);
                    App.Logger.Log($"Target mesh: {targetVertices.Length} vertices");
                    
                    // Step 2: Adapt cloth wrapping data
                    task.Update("Adapting cloth data to target mesh...");
                    var adaptedClothWrapping = ClothDataAdapter.AdaptClothWrapping(targetVertices, _templateClothWrappingParsed);
                    
                    // Step 3: Write adapted cloth wrapping to bytes (content only, starts at BNRY)
                    task.Update("Writing ClothWrappingAsset...");
                    adaptedClothWrappingBytes = adaptedClothWrapping.Write();
                    App.Logger.Log($"Generated ClothWrapping: {adaptedClothWrappingBytes.Length} bytes");
                    
                    // Step 4: EACloth copied from template
                    eaClothBytes = _templateEAClothBytes;
                    App.Logger.Log($"EACloth: {eaClothBytes.Length} bytes (copied from template)");
                    
                    // Step 5: Write to Frosty project
                    task.Update("Saving to project...");
                    
                    if (targetClothWrappingEntry != null)
                    {
                        // Update ResMeta with correct content size (first 4 bytes = ByteSize)
                        byte[] cwMeta = new byte[16];
                        BitConverter.GetBytes((uint)adaptedClothWrappingBytes.Length).CopyTo(cwMeta, 0);
                        App.AssetManager.ModifyRes(targetClothWrappingEntry.ResRid, adaptedClothWrappingBytes, cwMeta);
                        App.Logger.Log($"Replaced ClothWrapping: {targetClothWrappingEntry.Name} ({adaptedClothWrappingBytes.Length} bytes, ResMeta updated)");
                    }
                    else
                    {
                        string resourceName = newResourceName + "_clothwrappingasset";
                        App.AssetManager.AddRes(resourceName, ResourceType.EAClothAssetData, null, adaptedClothWrappingBytes, meshBundles);
                        App.Logger.Log($"Created ClothWrapping: {resourceName}");
                    }
                    
                    if (targetEAClothEntry != null)
                    {
                        // Update ResMeta with correct content size (first 4 bytes = ByteSize)
                        byte[] eaMeta = new byte[16];
                        BitConverter.GetBytes((uint)eaClothBytes.Length).CopyTo(eaMeta, 0);
                        App.AssetManager.ModifyRes(targetEAClothEntry.ResRid, eaClothBytes, eaMeta);
                        App.Logger.Log($"Replaced EACloth: {targetEAClothEntry.Name} ({eaClothBytes.Length} bytes, ResMeta updated)");
                    }
                    else
                    {
                        string resourceName = newResourceName + "_eacloth";
                        App.AssetManager.AddRes(resourceName, ResourceType.EAClothData, null, eaClothBytes, meshBundles);
                        App.Logger.Log($"Created EACloth: {resourceName}");
                    }

                    task.Update("Complete!");
                });

                StatusText.Text = "Complete! Save your project.";
                FrostyMessageBox.Show(
                    "Cloth data generated!\n\n" +
                    $"ClothWrapping: {adaptedClothWrappingBytes?.Length ?? 0} bytes\n" +
                    $"EACloth: {eaClothBytes?.Length ?? 0} bytes\n\n" +
                    "Save your project to apply changes.",
                    "Complete", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                FrostyMessageBox.Show($"Error: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK);
                StatusText.Text = "Error during generation";
                App.Logger.LogError($"Generation error: {ex}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
