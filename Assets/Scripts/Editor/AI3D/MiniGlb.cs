using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 极简 GLB 读取器：只取三角网格的顶点与索引，用来把 Marble 的碰撞网格变成 Unity Mesh。
    /// 不做任何坐标系转换（保持与泼溅文件相同的原始坐标，由上层 Transform 统一校正）。
    /// </summary>
    public static class MiniGlb
    {
        private const uint Magic = 0x46546C67;   // "glTF"
        private const uint ChunkJson = 0x4E4F534A;
        private const uint ChunkBin = 0x004E4942;

        public static Mesh LoadMesh(byte[] glb, string name = "GlbMesh")
        {
            if (glb == null || glb.Length < 20 || BitConverter.ToUInt32(glb, 0) != Magic) throw new Exception("不是 GLB 文件");
            int offset = 12;
            string jsonText = null;
            int binStart = -1, binLength = 0;
            while (offset + 8 <= glb.Length)
            {
                int len = (int)BitConverter.ToUInt32(glb, offset);
                uint type = BitConverter.ToUInt32(glb, offset + 4);
                offset += 8;
                if (type == ChunkJson) jsonText = Encoding.UTF8.GetString(glb, offset, len);
                else if (type == ChunkBin && binStart < 0) { binStart = offset; binLength = len; }
                offset += len;
            }
            if (jsonText == null) throw new Exception("GLB 里没有 JSON 块");
            var root = MiniJson.ParseObject(jsonText);
            if (root == null) throw new Exception("GLB JSON 解析失败");

            var ctx = new Context { bytes = glb, binStart = binStart, binLength = binLength, root = root };
            var vertices = new List<Vector3>();
            var indices = new List<int>();

            // 遍历场景节点（带层级变换）；没有 scenes 时直接遍历所有 mesh
            var scenes = root.TryGetValue("scenes", out var sc) ? sc as List<object> : null;
            var nodes = root.TryGetValue("nodes", out var nd) ? nd as List<object> : null;
            if (scenes != null && scenes.Count > 0 && nodes != null)
            {
                int sceneIndex = (int)MiniJson.GetNumber(root, "scene", 0);
                if (sceneIndex < 0 || sceneIndex >= scenes.Count) sceneIndex = 0;
                var sceneNodes = MiniJson.Get(scenes[sceneIndex], "nodes") as List<object>;
                if (sceneNodes != null)
                    foreach (var n in sceneNodes) VisitNode(ctx, nodes, Convert.ToInt32(n), Matrix4x4.identity, vertices, indices, 0);
            }
            else if (root.TryGetValue("meshes", out var ms) && ms is List<object> meshes)
            {
                for (int i = 0; i < meshes.Count; i++) AppendMesh(ctx, i, Matrix4x4.identity, vertices, indices);
            }
            if (vertices.Count == 0) throw new Exception("GLB 里没有三角网格");

            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private class Context
        {
            public byte[] bytes;
            public int binStart, binLength;
            public Dictionary<string, object> root;
        }

        private static void VisitNode(Context ctx, List<object> nodes, int index, Matrix4x4 parent, List<Vector3> v, List<int> idx, int depth)
        {
            if (depth > 64 || index < 0 || index >= nodes.Count) return;
            var node = nodes[index] as Dictionary<string, object>;
            if (node == null) return;
            Matrix4x4 local = NodeMatrix(node);
            Matrix4x4 world = parent * local;
            if (node.TryGetValue("mesh", out var m)) AppendMesh(ctx, Convert.ToInt32(m), world, v, idx);
            if (node.TryGetValue("children", out var ch) && ch is List<object> children)
                foreach (var c in children) VisitNode(ctx, nodes, Convert.ToInt32(c), world, v, idx, depth + 1);
        }

        private static Matrix4x4 NodeMatrix(Dictionary<string, object> node)
        {
            if (node.TryGetValue("matrix", out var mo) && mo is List<object> ml && ml.Count == 16)
            {
                var m = new Matrix4x4();
                // glTF 为列主序
                for (int c = 0; c < 4; c++)
                for (int r = 0; r < 4; r++)
                    m[r, c] = Convert.ToSingle(ml[c * 4 + r]);
                return m;
            }
            Vector3 t = Vector3.zero, s = Vector3.one;
            Quaternion q = Quaternion.identity;
            if (node.TryGetValue("translation", out var to) && to is List<object> tl && tl.Count == 3)
                t = new Vector3(Convert.ToSingle(tl[0]), Convert.ToSingle(tl[1]), Convert.ToSingle(tl[2]));
            if (node.TryGetValue("rotation", out var ro) && ro is List<object> rl && rl.Count == 4)
                q = new Quaternion(Convert.ToSingle(rl[0]), Convert.ToSingle(rl[1]), Convert.ToSingle(rl[2]), Convert.ToSingle(rl[3]));
            if (node.TryGetValue("scale", out var so) && so is List<object> sl && sl.Count == 3)
                s = new Vector3(Convert.ToSingle(sl[0]), Convert.ToSingle(sl[1]), Convert.ToSingle(sl[2]));
            return Matrix4x4.TRS(t, q, s);
        }

        private static void AppendMesh(Context ctx, int meshIndex, Matrix4x4 world, List<Vector3> v, List<int> idx)
        {
            var meshes = ctx.root["meshes"] as List<object>;
            if (meshes == null || meshIndex < 0 || meshIndex >= meshes.Count) return;
            var prims = MiniJson.Get(meshes[meshIndex], "primitives") as List<object>;
            if (prims == null) return;
            foreach (var p in prims)
            {
                int mode = (int)MiniJson.GetNumber(p, "mode", 4);
                if (mode != 4) continue; // 只要 TRIANGLES
                var posIdx = MiniJson.Get(p, "attributes.POSITION");
                if (posIdx == null) continue;
                var positions = ReadVec3(ctx, Convert.ToInt32(posIdx));
                int baseIndex = v.Count;
                foreach (var pos in positions) v.Add(world.MultiplyPoint3x4(pos));
                var indObj = MiniJson.Get(p, "indices");
                if (indObj != null)
                {
                    var ind = ReadIndices(ctx, Convert.ToInt32(indObj));
                    for (int i = 0; i + 2 < ind.Length; i += 3)
                    {
                        idx.Add(baseIndex + ind[i]);
                        idx.Add(baseIndex + ind[i + 1]);
                        idx.Add(baseIndex + ind[i + 2]);
                    }
                }
                else
                {
                    for (int i = 0; i + 2 < positions.Length; i += 3)
                    {
                        idx.Add(baseIndex + i); idx.Add(baseIndex + i + 1); idx.Add(baseIndex + i + 2);
                    }
                }
            }
        }

        private static (int offset, int stride, int count, int componentType) Accessor(Context ctx, int accessorIndex, int elementSize)
        {
            var accessors = ctx.root["accessors"] as List<object>;
            var acc = accessors[accessorIndex] as Dictionary<string, object>;
            int count = (int)MiniJson.GetNumber(acc, "count");
            int componentType = (int)MiniJson.GetNumber(acc, "componentType");
            int accOffset = (int)MiniJson.GetNumber(acc, "byteOffset", 0);
            if (!acc.TryGetValue("bufferView", out var bvo)) throw new Exception("GLB accessor 没有 bufferView（不支持稀疏存储）");
            var views = ctx.root["bufferViews"] as List<object>;
            var view = views[Convert.ToInt32(bvo)] as Dictionary<string, object>;
            int bufferIndex = (int)MiniJson.GetNumber(view, "buffer", 0);
            if (bufferIndex != 0 || ctx.binStart < 0) throw new Exception("GLB 只支持内嵌二进制缓冲");
            int viewOffset = (int)MiniJson.GetNumber(view, "byteOffset", 0);
            int stride = (int)MiniJson.GetNumber(view, "byteStride", 0);
            if (stride == 0) stride = elementSize;
            return (ctx.binStart + viewOffset + accOffset, stride, count, componentType);
        }

        private static Vector3[] ReadVec3(Context ctx, int accessorIndex)
        {
            var (offset, stride, count, componentType) = Accessor(ctx, accessorIndex, 12);
            if (componentType != 5126) throw new Exception("POSITION 只支持 float 类型");
            var result = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int o = offset + i * stride;
                result[i] = new Vector3(BitConverter.ToSingle(ctx.bytes, o), BitConverter.ToSingle(ctx.bytes, o + 4), BitConverter.ToSingle(ctx.bytes, o + 8));
            }
            return result;
        }

        private static int[] ReadIndices(Context ctx, int accessorIndex)
        {
            var accessors = ctx.root["accessors"] as List<object>;
            int componentType = (int)MiniJson.GetNumber(accessors[accessorIndex], "componentType");
            int size = componentType == 5125 ? 4 : componentType == 5123 ? 2 : 1;
            var (offset, stride, count, _) = Accessor(ctx, accessorIndex, size);
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int o = offset + i * stride;
                switch (componentType)
                {
                    case 5125: result[i] = (int)BitConverter.ToUInt32(ctx.bytes, o); break;
                    case 5123: result[i] = BitConverter.ToUInt16(ctx.bytes, o); break;
                    default: result[i] = ctx.bytes[o]; break;
                }
            }
            return result;
        }

        /// <summary>测试用：把一个 Mesh 写成最小 GLB（仅 POSITION + indices）。</summary>
        public static byte[] Write(Mesh mesh)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            int posBytes = verts.Length * 12;
            int idxBytes = tris.Length * 4;
            int binLen = posBytes + idxBytes;
            var bin = new byte[(binLen + 3) & ~3];
            for (int i = 0; i < verts.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(verts[i].x), 0, bin, i * 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(verts[i].y), 0, bin, i * 12 + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(verts[i].z), 0, bin, i * 12 + 8, 4);
            }
            for (int i = 0; i < tris.Length; i++) Buffer.BlockCopy(BitConverter.GetBytes((uint)tris[i]), 0, bin, posBytes + i * 4, 4);
            Bounds b = mesh.bounds;
            string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}],"
                + "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1,\"mode\":4}]}],"
                + "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":" + verts.Length + ",\"type\":\"VEC3\",\"min\":[" + F(b.min.x) + "," + F(b.min.y) + "," + F(b.min.z) + "],\"max\":[" + F(b.max.x) + "," + F(b.max.y) + "," + F(b.max.z) + "]},"
                + "{\"bufferView\":1,\"componentType\":5125,\"count\":" + tris.Length + ",\"type\":\"SCALAR\"}],"
                + "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + posBytes + "},{\"buffer\":0,\"byteOffset\":" + posBytes + ",\"byteLength\":" + idxBytes + "}],"
                + "\"buffers\":[{\"byteLength\":" + bin.Length + "}]}";
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            int jsonPadded = (jsonBytes.Length + 3) & ~3;
            var jb = new byte[jsonPadded];
            Buffer.BlockCopy(jsonBytes, 0, jb, 0, jsonBytes.Length);
            for (int i = jsonBytes.Length; i < jsonPadded; i++) jb[i] = 0x20;

            int total = 12 + 8 + jsonPadded + 8 + bin.Length;
            var outBytes = new byte[total];
            int o = 0;
            void U32(uint x) { Buffer.BlockCopy(BitConverter.GetBytes(x), 0, outBytes, o, 4); o += 4; }
            U32(Magic); U32(2); U32((uint)total);
            U32((uint)jsonPadded); U32(ChunkJson); Buffer.BlockCopy(jb, 0, outBytes, o, jsonPadded); o += jsonPadded;
            U32((uint)bin.Length); U32(ChunkBin); Buffer.BlockCopy(bin, 0, outBytes, o, bin.Length);
            return outBytes;
        }

        private static string F(float f) => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
