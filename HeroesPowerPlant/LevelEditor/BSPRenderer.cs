﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using SharpDX;
using HeroesONE_R.Structures;
using HeroesONE_R.Structures.Subsctructures;
using RenderWareFile;

namespace HeroesPowerPlant
{
    public class BSPRenderer
    {
        public void Dispose()
        {
            foreach (RenderWareModelFile r in BSPList)
                foreach (SharpMesh mesh in r.meshList)
                    mesh.Dispose();

            foreach (RenderWareModelFile r in ShadowColBSPList)
                foreach (SharpMesh mesh in r.meshList)
                    mesh.Dispose();
        }

        public string currentFileNamePrefix = "default";
        public List<RenderWareModelFile> BSPList = new List<RenderWareModelFile>();

        public void SetHeroesBSPList(SharpDevice device, Archive heroesONEfile)
        {
            Dispose();
            ReadFileMethods.isShadow = false;

            BSPList = new List<RenderWareModelFile>(heroesONEfile.Files.Count);
            ShadowColBSPList = new List<RenderWareModelFile>();

            foreach (ArchiveFile file in heroesONEfile.Files)
            {
                if (!(new string[] { ".bsp", ".rg1", ".rx1" }.Contains(Path.GetExtension(file.Name).ToLower())))
                    continue;
                
                RenderWareModelFile TempBSPFile = new RenderWareModelFile(file.Name);
                TempBSPFile.SetChunkNumberAndName();
                byte[] uncompressedData = file.DecompressThis();
                TempBSPFile.SetForRendering(device, ReadFileMethods.ReadRenderWareFile(uncompressedData), uncompressedData);
                BSPList.Add(TempBSPFile);
            }
        }
        
        // Visibility functions

        private HashSet<int> VisibleChunks = new HashSet<int>();

        public void DetermineVisibleChunks(SharpRenderer renderer, List<LevelEditor.Chunk> chunkList)
        {
            VisibleChunks.Clear();
            VisibleChunks.Add(-1);
            Vector3 cameraPos = renderer.Camera.GetPosition();

            foreach (var c in chunkList)
            {
                if ((cameraPos.X > c.Min.X) && (cameraPos.Y > c.Min.Y) && (cameraPos.Z > c.Min.Z) &
                    (cameraPos.X < c.Max.X) && (cameraPos.Y < c.Max.Y) && (cameraPos.Z < c.Max.Z))
                {
                    VisibleChunks.Add(c.number);
                }
            }
        }

        public static bool renderByChunk = true;
                
        // Rendering functions
        
        public void RenderLevelModel(SharpRenderer renderer, List<LevelEditor.Chunk> chunkList)
        {
            if (renderByChunk)
                DetermineVisibleChunks(renderer, chunkList);

            renderer.Device.SetFillModeDefault();
            renderer.defaultShader.Apply();

            RenderOpaque(renderer);
            RenderAlpha(renderer);
        }

        private void RenderOpaque(SharpRenderer renderer)
        {
            renderer.Device.SetDefaultBlendState();
            renderer.Device.SetDefaultDepthState();
            renderer.Device.SetCullModeDefault();

            renderer.Device.UpdateData(renderer.defaultBuffer, renderer.viewProjection);
            renderer.Device.DeviceContext.VertexShader.SetConstantBuffer(0, renderer.defaultBuffer);

            for (int j = 0; j < BSPList.Count; j++)
            {
                if ((renderByChunk && !VisibleChunks.Contains(BSPList[j].ChunkNumber)) ||
                    (BSPList[j].ChunkName == "A" || BSPList[j].ChunkName == "P" || BSPList[j].ChunkName == "K"))
                    continue;

                if (BSPList[j].isNoCulling) renderer.Device.SetCullModeNone();
                else renderer.Device.SetCullModeDefault();

                renderer.Device.ApplyRasterState();
                renderer.Device.UpdateAllStates();

                BSPList[j].Render(renderer.Device);
            }
        }

        private void RenderAlpha(SharpRenderer renderer)
        {
            for (int j = 0; j < BSPList.Count; j++)
            {
                if ((renderByChunk && !VisibleChunks.Contains(BSPList[j].ChunkNumber)) ||
                    (BSPList[j].ChunkName == "O"))
                    continue;

                if (BSPList[j].isNoCulling) renderer.Device.SetCullModeNone();
                else renderer.Device.SetCullModeDefault();

                if (BSPList[j].ChunkName == "A" || BSPList[j].ChunkName == "P")
                {
                    renderer.Device.SetBlendStateAlphaBlend();
                }
                else if (BSPList[j].ChunkName == "K")
                {
                    renderer.Device.SetBlendStateAdditive();
                }

                renderer.Device.ApplyRasterState();
                renderer.Device.UpdateAllStates();

                renderer.Device.UpdateData(renderer.defaultBuffer, renderer.viewProjection);
                renderer.Device.DeviceContext.VertexShader.SetConstantBuffer(0, renderer.defaultBuffer);

                BSPList[j].Render(renderer.Device);
            }
        }

        // Shadow functions
        public string currentShadowFolderNamePrefix = "default";

        public void LoadShadowLevelFolder(SharpRenderer renderer, string Folder, LevelEditor.LevelEditor levelEditor)
        {
            List<Archive> ShadowONEFiles = new List<Archive>();
            currentShadowFolderNamePrefix = Path.GetFileNameWithoutExtension(Folder);

            foreach (string fileName in Directory.GetFiles(Folder))
            {
                if (Path.GetExtension(fileName).ToLower() == ".one")
                    if (!(fileName.Contains("dat") ||
                        fileName.Contains("fx") ||
                        fileName.Contains("gdt") ||
                        fileName.Contains("tex")))
                    {
                        byte[] oneDataBytes = File.ReadAllBytes(fileName);
                        ShadowONEFiles.Add(Archive.FromONEFile(ref oneDataBytes));
                    }
                    else if (fileName.Contains("dat"))
                    {
                        levelEditor.initVisibilityEditor(true, fileName);
                        levelEditor.shadowSplineEditor.Init(fileName);
                    }
                    else if (fileName.Contains("fx"))
                    {
                        //  OpenShadowFXONE = new HeroesONEFile(fileName);
                    }
                    else if (fileName.Contains("gdt"))
                    {
                        Program.MainForm.renderer.dffRenderer.AddDFFFiles(new string[] { fileName });
                    }
                    else if (fileName.Contains("tex"))
                    {
                        TextureManager.LoadTexturesFromTXD(fileName, renderer, this);
                    }
            }

            SetShadowBSPList(renderer, ShadowONEFiles);
        }

        public List<RenderWareModelFile> ShadowColBSPList = new List<RenderWareModelFile>();

        private void SetShadowBSPList(SharpRenderer renderer, List<Archive> OpenShadowONEFiles)
        {
            Dispose();
            
            BSPList = new List<RenderWareModelFile>();
            ShadowColBSPList = new List<RenderWareModelFile>();

            ReadFileMethods.isShadow = true;

            foreach (Archive f in OpenShadowONEFiles)
                foreach (ArchiveFile file in f.Files)
                {
                    string ChunkName = Path.GetFileNameWithoutExtension(file.Name);

                    if (ChunkName.Contains("COLI"))
                    {
                        ReadFileMethods.isCollision = true;

                        RenderWareModelFile TempBSPFile = new RenderWareModelFile(file.Name);

                        foreach (string s in ChunkName.Split('_'))
                            try
                            {
                                TempBSPFile.ChunkNumber = Convert.ToByte(s);
                                break;
                            }
                            catch { TempBSPFile.ChunkNumber = -1; }
                        
                        TempBSPFile.isShadowCollision = true;
                        try
                        {
                            byte[] data = file.DecompressThis();
                            TempBSPFile.SetForRendering(renderer.Device, ReadFileMethods.ReadRenderWareFile(data), data);
                        }
                        catch (Exception e)
                        {
                            System.Windows.Forms.MessageBox.Show("Error on opening " + file.Name + ": " + e.Message);
                        }
                        ShadowColBSPList.Add(TempBSPFile);

                        ReadFileMethods.isCollision = false;
                    }
                    else
                    {
                        RenderWareModelFile TempBSPFile = new RenderWareModelFile(file.Name);
                        TempBSPFile.SetChunkNumberAndName();
                        byte[] data = file.DecompressThis();
                        TempBSPFile.SetForRendering(renderer.Device, ReadFileMethods.ReadRenderWareFile(data), data);
                        BSPList.Add(TempBSPFile);
                    }
                }
        }

        public void RenderShadowCollisionModel(SharpRenderer renderer, List<LevelEditor.Chunk> chunkList)
        {
            if (renderByChunk)
                DetermineVisibleChunks(renderer, chunkList);

            renderer.Device.SetDefaultBlendState();
            renderer.Device.SetFillModeDefault();
            renderer.Device.SetCullModeDefault();
            renderer.Device.ApplyRasterState();
            renderer.Device.UpdateAllStates();

            renderer.Device.UpdateData(renderer.defaultBuffer, renderer.viewProjection);
            renderer.Device.DeviceContext.VertexShader.SetConstantBuffer(0, renderer.defaultBuffer);
            renderer.defaultShader.Apply();
                        
            for (int j = 0; j < ShadowColBSPList.Count; j++)
            {
                if (renderByChunk & !VisibleChunks.Contains(ShadowColBSPList[j].ChunkNumber))
                    continue;

                ShadowColBSPList[j].Render(renderer.Device);
            }
        }

        public Vector3 GetDroppedPosition(Vector3 InitialPosition)
        {
            Ray ray = new Ray(InitialPosition, Vector3.Down);
            float smallerDistance = 10000f;
            bool change = false;

            List<RenderWareModelFile> bsps = Program.MainForm.renderer.ShowCollision ? ShadowColBSPList : BSPList;

            foreach (RenderWareModelFile rwmf in bsps)
            {
                foreach (RWSection rw in rwmf.GetAsRWSectionArray())
                {
                    if (rw is RenderWareFile.Sections.World_000B world)
                    {
                        if (InitialPosition.X < world.worldStruct.boxMinimum.X ||
                            InitialPosition.Y < world.worldStruct.boxMinimum.Y ||
                            InitialPosition.Z < world.worldStruct.boxMinimum.Z ||
                            InitialPosition.X > world.worldStruct.boxMaximum.X ||
                            InitialPosition.Y > world.worldStruct.boxMaximum.Y ||
                            InitialPosition.Z > world.worldStruct.boxMaximum.Z) continue;
                    }
                }

                foreach (Triangle t in rwmf.triangleList)
                {
                    Vector3 v1 = rwmf.vertexListG[t.vertex1];
                    Vector3 v2 = rwmf.vertexListG[t.vertex2];
                    Vector3 v3 = rwmf.vertexListG[t.vertex3];

                    if (ray.Intersects(ref v1, ref v2, ref v3, out float distance))
                        if (distance < smallerDistance)
                        {
                            smallerDistance = distance;
                            change = true;
                        }
                }
            }

            if (Program.MainForm.renderer.ShowCollision)
                foreach (CollisionEditor.CollisionEditor collisionEditor in Program.MainForm.CollisionEditorDict.Values)
                {
                    collisionEditor.GetClickedModelPosition(ray, out bool hasIntersected, out float smallColDistance);
                    if (hasIntersected && smallColDistance < smallerDistance)
                    {
                        smallerDistance = smallColDistance;
                        change = true;
                    }
                }
            
            if (change)
                InitialPosition.Y -= smallerDistance;

            return InitialPosition;
        }

        public void GetClickedModelPosition(bool isShadowCollision, Ray ray, out bool hasIntersected, out float smallestDistance)
        {
            hasIntersected = false;
            smallestDistance = 40000f;

            foreach (RenderWareModelFile bsp in isShadowCollision ? ShadowColBSPList : BSPList)
            {
                if (renderByChunk && !VisibleChunks.Contains(bsp.ChunkNumber))
                    continue;

                foreach (Triangle t in bsp.triangleList)
                {
                    Vector3 v1 = bsp.vertexListG[t.vertex1];
                    Vector3 v2 = bsp.vertexListG[t.vertex2];
                    Vector3 v3 = bsp.vertexListG[t.vertex3];

                    if (ray.Intersects(ref v1, ref v2, ref v3, out float distance))
                    {
                        hasIntersected = true;

                        if (distance < smallestDistance)
                            smallestDistance = distance;
                    }
                }
            }
        }
    }
}
