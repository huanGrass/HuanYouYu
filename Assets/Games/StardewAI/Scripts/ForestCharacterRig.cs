using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D.IK;

namespace FarmPrototype
{
    // 同一体型的服装共用骨架。袖子、裤腿和鞋分别跟随肩、髋、踝，避免换装重做动作。
    public sealed class ForestCharacterRig : MonoBehaviour
    {
        public const float PixelsPerUnit = 64f;
        public const float Stride = .42f;
        public const float WalkSpeed = .9f;
        // 落脚、承重、经过、抬起；半周期后换另一条腿承重。
        static readonly AnimationCurve WeightHeight = new AnimationCurve(
            new Keyframe(0, 39, -8, -8), new Keyframe(.1f, 38, 0, 0),
            new Keyframe(.28f, 41, 0, 0), new Keyframe(.4f, 40, -8, -8), new Keyframe(.5f, 39, -8, -8));
        static readonly AnimationCurve Recovery = new AnimationCurve(
            new Keyframe(.6f, -.3f, -1, -1), new Keyframe(.73f, -.15f, 2.4f, 2.4f),
            new Keyframe(.9f, .29f, .6f, .6f), new Keyframe(1, .3f, -1, -1));
        static readonly AnimationCurve FootRoll = new AnimationCurve(
            new Keyframe(0, 12), new Keyframe(.08f, 0), new Keyframe(.48f, 0),
            new Keyframe(.6f, -22), new Keyframe(.75f, -8), new Keyframe(.9f, 12), new Keyframe(1, 12));
        public enum WearSlot { Hair, Top, Bottom, Shoes, Scarf, Hat }
        readonly int[] outfit = new int[6];
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        Transform pelvis, torso, head;
        SortingGroup sortingGroup;
        readonly Transform[] shoulders = new Transform[2], elbows = new Transform[2], hands = new Transform[2];
        readonly Transform[] thighs = new Transform[2], knees = new Transform[2], feet = new Transform[2], targets = new Transform[2];
        readonly LimbSolver2D[] solvers = new LimbSolver2D[2];
        SpriteRenderer face, hair, body, top, hips, scarf, hat;
        readonly SpriteRenderer[] upperArms = new SpriteRenderer[2], forearms = new SpriteRenderer[2], sleeves = new SpriteRenderer[2];
        readonly SpriteRenderer[] legs = new SpriteRenderer[2], calves = new SpriteRenderer[2], shoes = new SpriteRenderer[2];
        int view = -1;
        public Transform FrontFoot => feet[1];
        public Transform FrontHand => hands[1];
        public Transform BackHand => hands[0];
        public float Cycle { get; private set; } = .25f;
        public float WalkBlend { get; private set; }
        public int FacingView => view;

        void Awake()
        {
            sortingGroup = gameObject.AddComponent<SortingGroup>();
            transform.localScale = Vector3.one / PixelsPerUnit;
            pelvis = Bone("Pelvis", transform, new Vector2(0, 40));
            torso = Bone("Torso", pelvis, Vector2.zero);
            head = Bone("Head", torso, new Vector2(0, 25));
            face = Part("Face", head); hair = Part("Hair", head); hat = Part("Hat", head);
            body = Part("Body", torso); top = Part("Top", torso); hips = Part("Hips", pelvis); scarf = Part("Scarf", torso);
            for (int i = 0; i < 2; i++)
            {
                shoulders[i] = Bone("Shoulder" + i, torso, new Vector2(i == 0 ? -11 : 11, 24));
                elbows[i] = Bone("Elbow" + i, shoulders[i], new Vector2(0, -13));
                hands[i] = Bone("Hand" + i, elbows[i], new Vector2(0, -13));
                upperArms[i] = Part("UpperArm" + i, shoulders[i]);
                sleeves[i] = Part("Sleeve" + i, shoulders[i]);
                forearms[i] = Part("Forearm" + i, elbows[i]);
                thighs[i] = Bone("Hip" + i, pelvis, new Vector2(i == 0 ? -6 : 6, 0));
                knees[i] = Bone("Knee" + i, thighs[i], new Vector2(0, -20));
                feet[i] = Bone("Ankle" + i, knees[i], new Vector2(0, -20));
                legs[i] = Part("TrouserLeg" + i, thighs[i]); calves[i] = Part("Calf" + i, knees[i]); shoes[i] = Part("Shoe" + i, feet[i]);
                targets[i] = Bone("FootTarget" + i, transform, new Vector2(i == 0 ? -6 : 6, 3));
                solvers[i] = new GameObject("LegIK" + i).AddComponent<LimbSolver2D>();
                solvers[i].transform.SetParent(transform, false);
                solvers[i].GetChain(0).effector = feet[i];
                solvers[i].GetChain(0).target = targets[i];
                solvers[i].constrainRotation = false;
                solvers[i].Initialize();
            }
            for (int i = 0; i < outfit.Length; i++) outfit[i] = Mathf.Clamp(PlayerPrefs.GetInt("StardewAI.Wear." + i, 0), 0, 1);
            Pose(Vector2Int.down, 0, 0);
        }

        public int GetWear(WearSlot slot) => outfit[Validate(slot)];
        public void SetWear(WearSlot slot, int option, bool save = true)
        {
            int index = Validate(slot);
            if (option < 0 || option > 1) throw new ArgumentOutOfRangeException(nameof(option));
            outfit[index] = option;
            if (save) { PlayerPrefs.SetInt("StardewAI.Wear." + index, option); PlayerPrefs.Save(); }
            if (view >= 0) ApplySprites();
        }
        static int Validate(WearSlot slot)
        {
            int index = (int)slot;
            if (index < 0 || index >= 6) throw new ArgumentOutOfRangeException(nameof(slot));
            return index;
        }

        public void Advance(Vector2 delta, float dt)
        {
            bool moving = delta.sqrMagnitude > .00000001f;
            if (moving) Cycle = Mathf.Repeat(Cycle + delta.magnitude / (2 * Stride), 1);
            WalkBlend = Mathf.MoveTowards(WalkBlend, moving ? 1 : 0, dt / .12f);
            if (WalkBlend == 0) Cycle = .25f;
        }

        public void Pose(Vector2Int facing, float action, int sortingOrder)
        {
            sortingGroup.sortingOrder = sortingOrder;
            sortingOrder = 0;
            int nextView = facing.x != 0 ? 1 : facing.y > 0 ? 2 : 0;
            transform.localScale = new Vector3(facing.x < 0 ? -1 : 1, 1, 1) / PixelsPerUnit;
            if (view != nextView) { view = nextView; ApplySprites(); }
            bool side = view == 1;
            float phase = Cycle * Mathf.PI * 2;
            float swing = Mathf.Cos(phase) * WalkBlend;
            pelvis.localPosition = new Vector3(0, Mathf.Lerp(43, WeightHeight.Evaluate(Mathf.Repeat(Cycle, .5f)), WalkBlend));
            torso.localRotation = Quaternion.Euler(0, 0, side ? (-2 + Mathf.Sin(phase * 2)) * WalkBlend - Mathf.Sin(action * Mathf.PI) * 7 : 0);
            head.localRotation = Quaternion.Euler(0, 0, -Mathf.DeltaAngle(0, torso.localEulerAngles.z) * .25f);
            for (int i = 0; i < renderers.Count; i++) renderers[i].sortingOrder = sortingOrder + 8;
            body.sortingOrder = sortingOrder + 6; top.sortingOrder = sortingOrder + 7; hips.sortingOrder = sortingOrder + 5;
            face.sortingOrder = sortingOrder + 10; hair.sortingOrder = sortingOrder + 11; scarf.sortingOrder = sortingOrder + 12; hat.sortingOrder = sortingOrder + 13;
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1 : 1;
                shoulders[i].localPosition = new Vector3(side ? sign * 3 : sign * 11, 24);
                shoulders[i].localRotation = Quaternion.Euler(0, 0, side ? -sign * swing * 23 : sign * (3 + swing * 5));
                elbows[i].localRotation = Quaternion.Euler(0, 0, side ? (7 + Mathf.Max(0, -sign * Mathf.Cos(phase - .4f)) * 12) * WalkBlend : 0);
                if (action > 0)
                {
                    shoulders[i].localRotation = Quaternion.Euler(0, 0, side ? Mathf.Lerp(105, 30, action) : sign * Mathf.Sin(action * Mathf.PI) * 28);
                    elbows[i].localRotation = Quaternion.Euler(0, 0, side ? -35 : -sign * 12);
                }
                float cycle = Mathf.Repeat(Cycle + (i == 0 ? .5f : 0), 1);
                bool planted = cycle < .6f;
                float p = planted ? 0 : (cycle - .6f) / .4f;
                float step = (planted ? .3f - cycle : Recovery.Evaluate(cycle)) * 2 * Stride * PixelsPerUnit * WalkBlend;
                float lift = (planted ? 0 : Mathf.Sin(p * Mathf.PI) * 3.5f) * WalkBlend;
                thighs[i].localPosition = new Vector3(side ? sign * 1 : sign * 6, 0);
                if (side)
                {
                    knees[i].localPosition = new Vector3(0, -20); feet[i].localPosition = new Vector3(0, -20);
                    float roll = FootRoll.Evaluate(cycle) * WalkBlend;
                    // 以鞋跟或鞋尖为支点滚动，避免旋转鞋子时鞋底插进地面。
                    Vector3 pivot = new Vector3(roll >= 0 ? -5 : 8, -6.5f);
                    Vector3 rollOffset = pivot - Quaternion.Euler(0, 0, roll) * pivot;
                    targets[i].localPosition = new Vector3(sign + step, 3 + lift) + rollOffset;
                    solvers[i].flip = facing.x < 0;
                    solvers[i].UpdateIK(1);
                    feet[i].rotation = Quaternion.Euler(0, 0, facing.x < 0 ? -roll : roll);
                }
                else
                {
                    // 正背面展示纵深投影：膝踝上提，避免把侧面弯膝画成左右劈腿。
                    thighs[i].localRotation = Quaternion.identity; knees[i].localRotation = Quaternion.identity; feet[i].localRotation = Quaternion.identity;
                    knees[i].localPosition = new Vector3(0, -21 + lift * .35f);
                    feet[i].localPosition = new Vector3(0, -19 + lift * .65f + step * .12f);
                }
                int layer = sortingOrder + (side && i == 0 ? 1 : 8);
                upperArms[i].sortingOrder = layer; sleeves[i].sortingOrder = layer + 1; forearms[i].sortingOrder = layer;
                legs[i].sortingOrder = sortingOrder + (side && i == 0 ? 2 : 4);
                calves[i].sortingOrder = legs[i].sortingOrder - 1; shoes[i].sortingOrder = legs[i].sortingOrder;
                Color shade = side && i == 0 ? new Color(.78f, .84f, .9f) : Color.white;
                upperArms[i].color = sleeves[i].color = forearms[i].color = legs[i].color = calves[i].color = shoes[i].color = shade;
            }
        }

        void ApplySprites()
        {
            bool side = view == 1;
            Set(face, "Head", new Vector2(0, 10), new Vector2(side ? 23 : 25, 28));
            Set(hair, "Hair" + outfit[0], new Vector2(side ? -3 : 0, 18), new Vector2(side ? 31 : 34, view == 2 ? 29 : 26));
            Set(hat, "Hat", new Vector2(0, 27), new Vector2(39, 19)); hat.enabled = outfit[5] != 0;
            Set(body, "Body", new Vector2(0, 13), new Vector2(side ? 15 : 21, 29));
            Set(top, "Top" + outfit[1], new Vector2(0, 12), new Vector2(side ? 19 : 25, 31));
            Set(hips, "Hips" + outfit[2], new Vector2(0, -1), new Vector2(side ? 17 : 25, 15));
            Set(scarf, "Scarf" + outfit[4], new Vector2(side ? 5 : 0, 20), new Vector2(side ? 12 : 19, 12));
            for (int i = 0; i < 2; i++)
            {
                Set(upperArms[i], "UpperArm", new Vector2(0, -6), new Vector2(7, 16));
                Set(sleeves[i], "Sleeve" + outfit[1], new Vector2(0, -3), new Vector2(10, 11));
                Set(forearms[i], "Forearm", new Vector2(0, -6), new Vector2(6.5f, 17));
                Set(legs[i], "Leg" + outfit[2], new Vector2(0, -10), new Vector2(side ? 12 : 12, 27));
                Set(calves[i], "Calf", new Vector2(0, -10), new Vector2(7, 24));
                Set(shoes[i], "Shoe" + outfit[3], new Vector2(side ? 2 : 0, -1), new Vector2(side ? 15 : 11, 11));
                // 前后面左右肢体使用对称分件，侧面保持一致的朝向。
                upperArms[i].flipX = forearms[i].flipX = sleeves[i].flipX = !side && i == 0;
            }
        }

        void Set(SpriteRenderer renderer, string name, Vector2 position, Vector2 size)
        {
            string path = "Art/ForestCharacter/" + (view == 0 ? "Front" : view == 1 ? "Side" : "Back") + "/" + name;
            if (!sprites.TryGetValue(path, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing character part: " + path);
                sprites.Add(path, sprite);
            }
            renderer.sprite = sprite;
            renderer.transform.localPosition = position;
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        }

        static Transform Bone(string name, Transform parent, Vector2 position)
        {
            var bone = new GameObject(name).transform; bone.SetParent(parent, false); bone.localPosition = position; return bone;
        }
        SpriteRenderer Part(string name, Transform parent)
        {
            var renderer = Bone(name, parent, Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
            renderers.Add(renderer); return renderer;
        }
    }
}
