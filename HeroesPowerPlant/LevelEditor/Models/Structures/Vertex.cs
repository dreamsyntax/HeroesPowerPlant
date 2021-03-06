﻿using SharpDX;

namespace HeroesPowerPlant.LevelEditor
{
    public struct Vertex
    {
        public Vector3 Position;
        public Color Color;
        public Vector2 TexCoord;

        public bool HasUV;
        public bool HasColor;
    }
}