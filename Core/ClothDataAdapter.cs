using System;
using System.Collections.Generic;
using System.IO;
using ClothDataPlugin.Resources;
using Frosty.Core;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using MeshSetPlugin.Resources;

namespace ClothDataPlugin.Core
{
    /// <summary>
    /// Adapts cloth data from a template mesh to a target mesh by finding
    /// the closest template vertex for each target vertex and copying its cloth parameters.
    /// Based on FrostMeshy's ClothConverter logic.
    /// </summary>
    public static class ClothDataAdapter
    {
        /// <summary>
        /// Adapts cloth wrapping data from template to target mesh.
        /// LOD0 is adapted (nearest-vertex matching), LOD1+ are copied from template.
        /// All sections are preserved so the output LodCount matches the template.
        /// </summary>
        public static ClothWrappingAssetParsed AdaptClothWrapping(
            ClothVector3[] targetVertices,
            ClothWrappingAssetParsed templateClothData)
        {
            if (targetVertices == null || targetVertices.Length == 0)
                throw new ArgumentException("Target vertices cannot be empty");

            if (templateClothData?.MeshSections == null || templateClothData.MeshSections.Length == 0)
                throw new ArgumentException("Template cloth data has no mesh sections");

            int sectionCount = templateClothData.MeshSections.Length;
            var templateSection = templateClothData.MeshSections[0]; // LOD0
            
            App.Logger.Log($"Adapting cloth: {targetVertices.Length} target verts, {templateSection.VertexCount} template LOD0 verts, {sectionCount} total LODs");

            // Create new cloth wrapping asset preserving ALL template structure
            var result = new ClothWrappingAssetParsed
            {
                BnryHeader = (byte[])templateClothData.BnryHeader.Clone(),
                UnknownField = templateClothData.UnknownField,
                LodCount = (uint)sectionCount,
                MeshSections = new ClothWrappingAssetParsed.MeshSection[sectionCount]
            };

            // --- LOD0: Adapt to target mesh ---
            var newSection = new ClothWrappingAssetParsed.MeshSection
            {
                UnknownId = templateSection.UnknownId,
                VertexCount = (uint)targetVertices.Length,
                UnmappedBytes = (byte[])templateSection.UnmappedBytes.Clone(),
                Vertices = new ClothVertex[targetVertices.Length]
            };

            for (int i = 0; i < targetVertices.Length; i++)
            {
                int closestIdx = FindClosestVertexIndex(targetVertices[i], templateSection.Vertices);
                var tv = templateSection.Vertices[closestIdx];

                newSection.Vertices[i] = new ClothVertex
                {
                    Position = targetVertices[i],
                    NormalX = tv.NormalX,
                    NormalY = tv.NormalY,
                    TangentX = tv.TangentX,
                    TangentY = tv.TangentY,
                    Weight0 = tv.Weight0,
                    Weight1 = tv.Weight1,
                    Weight2 = tv.Weight2,
                    Weight3 = tv.Weight3,
                    Index0 = tv.Index0,
                    Index1 = tv.Index1,
                    Index2 = tv.Index2,
                    Index3 = tv.Index3
                };

                if (i > 0 && i % 1000 == 0)
                {
                    App.Logger.Log($"  Processed {i}/{targetVertices.Length} vertices...");
                }
            }

            result.MeshSections[0] = newSection;

            // --- LOD1+: Deep copy from template ---
            for (int lodIdx = 1; lodIdx < sectionCount; lodIdx++)
            {
                var srcSection = templateClothData.MeshSections[lodIdx];
                var copySection = new ClothWrappingAssetParsed.MeshSection
                {
                    UnknownId = srcSection.UnknownId,
                    VertexCount = srcSection.VertexCount,
                    UnmappedBytes = (byte[])srcSection.UnmappedBytes.Clone(),
                    Vertices = new ClothVertex[srcSection.VertexCount]
                };

                for (int i = 0; i < srcSection.VertexCount; i++)
                {
                    var sv = srcSection.Vertices[i];
                    copySection.Vertices[i] = new ClothVertex
                    {
                        Position = sv.Position,
                        NormalX = sv.NormalX,
                        NormalY = sv.NormalY,
                        TangentX = sv.TangentX,
                        TangentY = sv.TangentY,
                        Weight0 = sv.Weight0,
                        Weight1 = sv.Weight1,
                        Weight2 = sv.Weight2,
                        Weight3 = sv.Weight3,
                        Index0 = sv.Index0,
                        Index1 = sv.Index1,
                        Index2 = sv.Index2,
                        Index3 = sv.Index3
                    };
                }

                result.MeshSections[lodIdx] = copySection;
                App.Logger.Log($"  Copied LOD{lodIdx}: {srcSection.VertexCount} vertices");
            }

            App.Logger.Log($"Cloth adaptation complete: LOD0={targetVertices.Length} adapted, {sectionCount - 1} LODs copied from template");
            return result;
        }

        private static int FindClosestVertexIndex(ClothVector3 position, ClothVertex[] vertices)
        {
            int closestIdx = 0;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < vertices.Length; i++)
            {
                float distSq = position.DistanceSquared(vertices[i].Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestIdx = i;
                }
            }

            return closestIdx;
        }

        /// <summary>
        /// Extracts vertex positions from a Frosty MeshSet's LOD0.
        /// Based on AutoLodPlugin.PostImportLodDecimator.ExtractSection pattern.
        /// 
        /// Chunk layout: [prefix_data] [vertex_buffer] [index_buffer]
        /// vertexBufferStart = chunkLength - IndexBufferSize - VertexBufferSize
        /// vertex position = vertexBufferStart + section.VertexOffset + (v * stride) + posOffset
        /// </summary>
        public static ClothVector3[] ExtractMeshVertices(MeshSet meshSet)
        {
            if (meshSet?.Lods == null || meshSet.Lods.Count == 0)
                throw new ArgumentException("MeshSet has no LODs");

            var lod = meshSet.Lods[0];
            var vertices = new List<ClothVector3>();

            // Get chunk data stream
            Stream chunkStream = null;
            
            if (lod.ChunkId != Guid.Empty)
            {
                var chunkEntry = App.AssetManager.GetChunkEntry(lod.ChunkId);
                if (chunkEntry != null)
                {
                    chunkStream = App.AssetManager.GetChunk(chunkEntry);
                }
            }
            
            if (chunkStream == null && lod.InlineData != null && lod.InlineData.Length > 0)
            {
                chunkStream = new MemoryStream(lod.InlineData);
            }

            if (chunkStream == null)
                throw new InvalidOperationException("Could not get mesh chunk data");

            // Calculate vertex buffer start offset (critical!)
            // Chunk layout: [prefix_data] [vertex_buffer] [index_buffer]
            long chunkLength = chunkStream.Length;
            long vertexBufferStart = chunkLength - lod.IndexBufferSize - lod.VertexBufferSize;

            App.Logger.Log($"  Chunk: {chunkLength} bytes, VertexBufferSize={lod.VertexBufferSize}, IndexBufferSize={lod.IndexBufferSize}, vertexBufferStart={vertexBufferStart}");
            App.Logger.Log($"  LOD0 sections: {lod.Sections.Count}, IndexUnitSize={lod.IndexUnitSize}");

            using (var reader = new NativeReader(chunkStream))
            {
                foreach (var section in lod.Sections)
                {
                    // Skip non-renderable sections (depth/shadow with no name)
                    if (string.IsNullOrEmpty(section.Name))
                        continue;

                    if (section.VertexCount == 0)
                        continue;

                    var geomDecl = section.GeometryDeclDesc[0];

                    // Find position element
                    int posOffset = -1;
                    int posStreamIdx = -1;
                    VertexElementFormat posFormat = VertexElementFormat.None;

                    for (int e = 0; e < geomDecl.ElementCount; e++)
                    {
                        var elem = geomDecl.Elements[e];
                        if (elem.Usage == VertexElementUsage.Pos)
                        {
                            posOffset = elem.Offset;
                            posStreamIdx = elem.StreamIndex;
                            posFormat = elem.Format;
                            break;
                        }
                    }

                    if (posOffset < 0)
                    {
                        App.Logger.LogWarning($"  Section '{section.Name}': no position element, skipping");
                        continue;
                    }

                    int vertexStride = geomDecl.Streams[posStreamIdx].VertexStride;

                    App.Logger.Log($"  Section '{section.Name}': {section.VertexCount} verts, stride={vertexStride}, posOffset={posOffset}, posFormat={posFormat}, sectionVertexOffset={section.VertexOffset}");

                    // Read vertex positions
                    for (uint v = 0; v < section.VertexCount; v++)
                    {
                        reader.Position = vertexBufferStart + section.VertexOffset + (v * vertexStride) + posOffset;
                        
                        float x, y, z;
                        switch (posFormat)
                        {
                            case VertexElementFormat.Float3:
                                x = reader.ReadFloat();
                                y = reader.ReadFloat();
                                z = reader.ReadFloat();
                                break;
                            case VertexElementFormat.Float4:
                                x = reader.ReadFloat();
                                y = reader.ReadFloat();
                                z = reader.ReadFloat();
                                reader.ReadFloat(); // w
                                break;
                            case VertexElementFormat.Half3:
                                x = HalfUtils.Unpack(reader.ReadUShort());
                                y = HalfUtils.Unpack(reader.ReadUShort());
                                z = HalfUtils.Unpack(reader.ReadUShort());
                                break;
                            case VertexElementFormat.Half4:
                                x = HalfUtils.Unpack(reader.ReadUShort());
                                y = HalfUtils.Unpack(reader.ReadUShort());
                                z = HalfUtils.Unpack(reader.ReadUShort());
                                reader.ReadUShort(); // w
                                break;
                            default:
                                x = reader.ReadFloat();
                                y = reader.ReadFloat();
                                z = reader.ReadFloat();
                                break;
                        }

                        vertices.Add(new ClothVector3(x, y, z));
                    }

                    // Log first 3 vertex positions for debugging
                    int startIdx = vertices.Count - (int)section.VertexCount;
                    for (int d = 0; d < System.Math.Min(3, (int)section.VertexCount); d++)
                    {
                        var vd = vertices[startIdx + d];
                        App.Logger.Log($"    v[{d}]: ({vd.X:F6}, {vd.Y:F6}, {vd.Z:F6})");
                    }
                }
            }

            chunkStream.Dispose();

            App.Logger.Log($"Extracted {vertices.Count} vertices from MeshSet (renderable sections only)");
            return vertices.ToArray();
        }
    }
}
