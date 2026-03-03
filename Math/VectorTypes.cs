using System;

namespace ClothDataPlugin.Math
{
    /// <summary>
    /// 2D Vector structure for cloth data calculations
    /// </summary>
    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public float Distance(ref Vector2 other)
        {
            float dx = x - other.x;
            float dy = y - other.y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }

        public string ToString(string format)
        {
            return $"({x.ToString(format)}, {y.ToString(format)})";
        }
    }

    /// <summary>
    /// 3D Vector structure for cloth data calculations
    /// </summary>
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float Distance(ref Vector3 other)
        {
            float dx = x - other.x;
            float dy = y - other.y;
            float dz = z - other.z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }

        public string ToString(string format)
        {
            return $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
        }
    }

    /// <summary>
    /// 3D Vector with double precision
    /// </summary>
    public struct Vector3d
    {
        public double x;
        public double y;
        public double z;

        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float Distance(ref Vector3d other)
        {
            double dx = x - other.x;
            double dy = y - other.y;
            double dz = z - other.z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }

        public string ToString(string format)
        {
            return $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
        }
    }

    /// <summary>
    /// 4D Vector structure for cloth data calculations
    /// </summary>
    public struct Vector4
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public float Distance(ref Vector4 other)
        {
            float dx = x - other.x;
            float dy = y - other.y;
            float dz = z - other.z;
            float dw = w - other.w;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz + dw * dw);
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z}, {w})";
        }

        public string ToString(string format)
        {
            return $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)}, {w.ToString(format)})";
        }
    }

    /// <summary>
    /// Bone weight structure for skinned mesh data
    /// </summary>
    public struct BoneWeight
    {
        public int boneIndex0;
        public int boneIndex1;
        public int boneIndex2;
        public int boneIndex3;
        public int boneIndex4;
        public int boneIndex5;
        public int boneIndex6;
        public int boneIndex7;
        public float boneWeight0;
        public float boneWeight1;
        public float boneWeight2;
        public float boneWeight3;
        public float boneWeight4;
        public float boneWeight5;
        public float boneWeight6;
        public float boneWeight7;

        public BoneWeight(int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7,
                         float w0, float w1, float w2, float w3, float w4, float w5, float w6, float w7)
        {
            boneIndex0 = i0; boneIndex1 = i1; boneIndex2 = i2; boneIndex3 = i3;
            boneIndex4 = i4; boneIndex5 = i5; boneIndex6 = i6; boneIndex7 = i7;
            boneWeight0 = w0; boneWeight1 = w1; boneWeight2 = w2; boneWeight3 = w3;
            boneWeight4 = w4; boneWeight5 = w5; boneWeight6 = w6; boneWeight7 = w7;
        }

        public int BoneCount()
        {
            int count = 0;
            if (boneWeight0 > 0) count++;
            if (boneWeight1 > 0) count++;
            if (boneWeight2 > 0) count++;
            if (boneWeight3 > 0) count++;
            if (boneWeight4 > 0) count++;
            if (boneWeight5 > 0) count++;
            if (boneWeight6 > 0) count++;
            if (boneWeight7 > 0) count++;
            return count;
        }

        public void RemoveBones(int count)
        {
            // Remove the smallest weighted bones
            for (int i = 0; i < count; i++)
            {
                int minIdx = -1;
                float minWeight = float.MaxValue;
                
                float[] weights = { boneWeight0, boneWeight1, boneWeight2, boneWeight3,
                                   boneWeight4, boneWeight5, boneWeight6, boneWeight7 };
                
                for (int j = 0; j < 8; j++)
                {
                    if (weights[j] > 0 && weights[j] < minWeight)
                    {
                        minWeight = weights[j];
                        minIdx = j;
                    }
                }

                if (minIdx >= 0)
                {
                    SetWeight(minIdx, 0);
                }
            }
        }

        public void Align()
        {
            // Normalize weights to sum to 1.0
            float total = boneWeight0 + boneWeight1 + boneWeight2 + boneWeight3 +
                         boneWeight4 + boneWeight5 + boneWeight6 + boneWeight7;
            
            if (total > 0)
            {
                boneWeight0 /= total;
                boneWeight1 /= total;
                boneWeight2 /= total;
                boneWeight3 /= total;
                boneWeight4 /= total;
                boneWeight5 /= total;
                boneWeight6 /= total;
                boneWeight7 /= total;
            }
        }

        private void SetWeight(int index, float value)
        {
            switch (index)
            {
                case 0: boneWeight0 = value; break;
                case 1: boneWeight1 = value; break;
                case 2: boneWeight2 = value; break;
                case 3: boneWeight3 = value; break;
                case 4: boneWeight4 = value; break;
                case 5: boneWeight5 = value; break;
                case 6: boneWeight6 = value; break;
                case 7: boneWeight7 = value; break;
            }
        }
    }
}
