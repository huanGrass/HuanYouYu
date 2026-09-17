using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D.IK;

namespace FarmPrototype
{
    // B 的独立审美样片：单一三分之二侧向，未替换游戏中的四向主角。
    public sealed class ForestBCharacterRig : MonoBehaviour
    {
        public const float CycleDistance = 20f / 64;
        public const float CycleSeconds = 1f;
        readonly Transform[] shoulders = new Transform[2], elbows = new Transform[2], hands = new Transform[2];
        readonly Transform[] hips = new Transform[2], knees = new Transform[2], ankles = new Transform[2], targets = new Transform[2];
        readonly LimbSolver2D[] solvers = new LimbSolver2D[2];
        Transform pelvis, chest, head, hairFollow, scarfFollow;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        public Transform FrontFoot => ankles[1];
        public Transform FrontKnee => knees[1];
        public Transform FrontHip => hips[1];
        public Transform FrontTarget => targets[1];
        // 每行是一个完整关键姿势：落脚、承重、经过、抬起，再换腿。
        // 列：髋高、重心偏移、骨盆转角、胸转角、头部补偿、摆臂、肘部。
        static readonly float[,] KeyPoses = {
            {27.1f, 0, .5f, -3, 2, -17, 7},
            {26.7f, -.45f, 1, -4.5f, 3.5f, -14, 5},
            {27.7f, -.65f, 0, -2, 1.5f, -2, 6},
            {27.8f, -.25f, -.5f, -1, 0, 12, 11},
            {27.1f, 0, -.5f, -3, 2, 17, 13},
            {26.7f, .45f, -1, -4.5f, 3.5f, 14, 12},
            {27.7f, .65f, 0, -2, 1.5f, 2, 9},
            {27.8f, .25f, .5f, -1, 0, -12, 7}
        };
        static readonly AnimationCurve[] Poses = BuildPoseCurves();
        static readonly AnimationCurve Return = new AnimationCurve(
            new Keyframe(.6f, -.3f, -1, -1), new Keyframe(.74f, -.12f, 2.3f, 2.3f),
            new Keyframe(.9f, .29f, .5f, .5f), new Keyframe(1, .3f, -1, -1));
        static readonly AnimationCurve Roll = new AnimationCurve(
            new Keyframe(0, 4), new Keyframe(.08f, 0), new Keyframe(.48f, 0),
            new Keyframe(.6f, -6), new Keyframe(.76f, -2), new Keyframe(.9f, 4), new Keyframe(1, 4));

        void Awake()
        {
            transform.localScale = Vector3.one / 64;
            gameObject.AddComponent<SortingGroup>();
            pelvis = Bone("Pelvis", transform, new Vector2(0, 28));
            chest = Bone("Chest", pelvis, new Vector2(0, 17));
            head = Bone("HeadBone", chest, new Vector2(0, 10));
            hairFollow = Bone("HairFollow", head, new Vector2(0, 30));
            scarfFollow = Bone("ScarfFollow", chest, new Vector2(3, 8));
            Part("Hips", pelvis, new Vector2(0, -1), new Vector2(25, 6), 5);
            SkinPart("Top", pelvis, new Vector2(0, 14), new Vector2(27, 28), 6, pelvis, chest, 4, 26);
            Part("Head", head, new Vector2(0, 18), new Vector2(38, 41), 12);
            SkinPart("Hair", head, new Vector2(-1, 30), new Vector2(51, 39), 13, head, hairFollow, 18, 45);
            SkinPart("Scarf", chest, new Vector2(3, 7), new Vector2(15, 12), 11, chest, scarfFollow, 11, 1);
            for (int i = 0; i < 2; i++)
            {
                string prefix = i == 0 ? "Far" : "Near";
                float depth = i == 0 ? 1 : -1;
                shoulders[i] = Bone(prefix + "Shoulder", chest, new Vector2(i == 0 ? 7 : -10, 6));
                elbows[i] = Bone(prefix + "Elbow", shoulders[i], new Vector2(0, -10));
                hands[i] = Bone(prefix + "Hand", elbows[i], new Vector2(0, -10));
                SkinPart(prefix + "UpperArm", shoulders[i], new Vector2(0, -5), new Vector2(12, 17), i == 0 ? 1 : 8,
                    shoulders[i], elbows[i], -5, -12);
                SkinPart(prefix + "Forearm", elbows[i], new Vector2(0, -5), new Vector2(9, 17), i == 0 ? 0 : 7,
                    elbows[i], hands[i], -5, -13);
                hips[i] = Bone(prefix + "Hip", pelvis, new Vector2(depth * 5.5f, 0));
                knees[i] = Bone(prefix + "Knee", hips[i], new Vector2(0, -11));
                ankles[i] = Bone(prefix + "Ankle", knees[i], new Vector2(0, -11));
                SkinPart(prefix + "Thigh", hips[i], new Vector2(0, -6), new Vector2(12, 16), i == 0 ? 2 : 4,
                    hips[i], knees[i], -4, -13);
                SkinPart(prefix + "Calf", knees[i], new Vector2(0, -5), new Vector2(7, 13), i == 0 ? 1 : 3,
                    knees[i], ankles[i], -6, -12);
                Part(prefix + "Shoe", ankles[i], new Vector2(2, -.5f), new Vector2(14, 12), i == 0 ? 2 : 4);
                targets[i] = Bone(prefix + "FootTarget", transform, new Vector2(depth * 5.5f, 6));
                solvers[i] = Bone(prefix + "IK", transform, Vector2.zero).gameObject.AddComponent<LimbSolver2D>();
                solvers[i].GetChain(0).effector = ankles[i];
                solvers[i].GetChain(0).target = targets[i];
                solvers[i].constrainRotation = false;
                solvers[i].Initialize();
            }
            Pose(.25f, 0, false);
        }

        public void Pose(float cycle, float blend, bool left)
        {
            cycle = Mathf.Repeat(cycle, 1); blend = Mathf.Clamp01(blend);
            transform.localScale = new Vector3(left ? -1 : 1, 1, 1) / 64;
            pelvis.localPosition = new Vector3(Poses[1].Evaluate(cycle) * blend, Mathf.Lerp(28, Poses[0].Evaluate(cycle), blend));
            pelvis.localRotation = Quaternion.Euler(0, 0, Poses[2].Evaluate(cycle) * blend);
            chest.localRotation = Quaternion.Euler(0, 0, Poses[3].Evaluate(cycle) * blend);
            head.localRotation = Quaternion.Euler(0, 0, Poses[4].Evaluate(cycle) * blend);
            hairFollow.localRotation = Quaternion.Euler(0, 0, Poses[4].Evaluate(Mathf.Repeat(cycle - .06f, 1)) * -.4f * blend);
            scarfFollow.localRotation = Quaternion.Euler(0, 0, Poses[3].Evaluate(Mathf.Repeat(cycle - .1f, 1)) * -.7f * blend);
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1 : 1;
                float c = Mathf.Repeat(cycle + (i == 0 ? .5f : 0), 1);
                shoulders[i].localRotation = Quaternion.Euler(0, 0, Poses[5].Evaluate(c) * blend);
                elbows[i].localRotation = Quaternion.Euler(0, 0, Poses[6].Evaluate(c) * blend);
                hands[i].localRotation = Quaternion.Euler(0, 0, (Poses[6].Evaluate(Mathf.Repeat(c - .07f, 1)) - 8) * .35f * blend);
                float step = (c < .6f ? .3f - c : Return.Evaluate(c)) * CycleDistance * 64 * blend;
                float lift = (c < .6f ? 0 : Mathf.Sin((c - .6f) / .4f * Mathf.PI) * .8f) * blend;
                float angle = Roll.Evaluate(c) * blend;
                Vector3 pivot = new Vector3(angle >= 0 ? -4 : 8, -6.5f);
                Vector3 offset = pivot - Quaternion.Euler(0, 0, angle) * pivot;
                targets[i].localPosition = new Vector3(-sign * 5.5f + step, 6 + lift) + offset;
                solvers[i].flip = left;
                solvers[i].UpdateIK(1);
                ankles[i].rotation = Quaternion.Euler(0, 0, left ? -angle : angle);
            }
        }

        static AnimationCurve[] BuildPoseCurves()
        {
            var curves = new AnimationCurve[7];
            for (int column = 0; column < curves.Length; column++)
            {
                var keys = new Keyframe[9];
                for (int i = 0; i < keys.Length; i++)
                {
                    float tangent = (KeyPoses[(i + 1) % 8, column] - KeyPoses[(i + 7) % 8, column]) * 4;
                    keys[i] = new Keyframe(i / 8f, KeyPoses[i % 8, column], tangent, tangent);
                }
                curves[column] = new AnimationCurve(keys);
            }
            return curves;
        }

        void SkinPart(string name, Transform anchor, Vector2 at, Vector2 size, int order,
            Transform first, Transform second, float blendStart, float blendEnd)
        {
            Sprite sprite = Resources.Load<Sprite>("Art/ForestB/" + name);
            if (sprite == null) throw new InvalidOperationException("Missing B character part: " + name);
            var renderer = Bone(name, transform, Vector2.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            const int columns = 4, rows = 12;
            int count = (columns + 1) * (rows + 1);
            var vertices = new Vector3[count]; var uv = new Vector2[count];
            var weights = new BoneWeight[count]; var colors = new Color[count];
            var triangles = new int[columns * rows * 6];
            Rect rect = sprite.rect;
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                int index = y * (columns + 1) + x;
                Vector2 unit = new Vector2(x / (float)columns, y / (float)rows);
                Vector2 point = at + Vector2.Scale(unit - Vector2.one * .5f, size);
                vertices[index] = renderer.transform.InverseTransformPoint(anchor.TransformPoint(point));
                uv[index] = new Vector2((rect.x + unit.x * rect.width) / sprite.texture.width,
                    (rect.y + unit.y * rect.height) / sprite.texture.height);
                float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(blendStart, blendEnd, point.y));
                weights[index] = new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = 1 - weight, weight1 = weight };
                colors[index] = Color.white;
                if (x == columns || y == rows) continue;
                int t = (y * columns + x) * 6;
                triangles[t] = index; triangles[t + 1] = index + columns + 1; triangles[t + 2] = index + 1;
                triangles[t + 3] = index + 1; triangles[t + 4] = index + columns + 1; triangles[t + 5] = index + columns + 2;
            }
            var mesh = new Mesh { name = name + "Skin", vertices = vertices, uv = uv, colors = colors,
                triangles = triangles, boneWeights = weights,
                bindposes = new[] { first.worldToLocalMatrix * renderer.transform.localToWorldMatrix,
                    second.worldToLocalMatrix * renderer.transform.localToWorldMatrix } };
            mesh.RecalculateBounds(); meshes.Add(mesh);
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = sprite.texture };
            materials.Add(material);
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
            renderer.bones = new[] { first, second }; renderer.rootBone = transform;
            renderer.quality = SkinQuality.Bone2; renderer.updateWhenOffscreen = true;
            renderer.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 12);
            renderer.sortingOrder = order; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in meshes) Destroy(mesh);
            foreach (Material material in materials) Destroy(material);
        }

        static Transform Bone(string name, Transform parent, Vector2 at)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false); bone.localPosition = at; return bone;
        }
        static void Part(string name, Transform parent, Vector2 at, Vector2 size, int order)
        {
            Sprite sprite = Resources.Load<Sprite>("Art/ForestB/" + name);
            if (sprite == null) throw new InvalidOperationException("Missing B character part: " + name);
            var renderer = Bone(name, parent, at).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = order;
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        }
    }
}
