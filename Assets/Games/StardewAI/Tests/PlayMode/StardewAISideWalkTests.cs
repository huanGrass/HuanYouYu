using System;
using System.Collections;
using System.IO;
using System.Reflection;
using FarmPrototype;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public sealed class StardewAISideWalkTests
    {
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        [UnityTest]
        public IEnumerator BCharacterWalkPreview()
        {
            string output = Environment.GetEnvironmentVariable("STARDEW_WALK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(output)) { output = Path.Combine(output, "single"); Directory.CreateDirectory(output); }
            var stage = new GameObject("SingleCharacterStage");
            var prefab = Resources.Load<GameObject>("Art/ForestB/Character");
            Assert.IsNotNull(prefab);
            var rig = UnityEngine.Object.Instantiate(prefab).GetComponent<ForestBCharacterRig>();
            rig.transform.SetParent(stage.transform, true);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform);
            camera.orthographic = true; camera.orthographicSize = 1.15f; camera.aspect = 960f / 540;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.88f, .9f, .86f);
            var target = new RenderTexture(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var line = new GameObject("GroundLine").AddComponent<SpriteRenderer>();
            line.transform.SetParent(stage.transform); line.sprite = lineSprite;
            line.color = new Color(.53f, .6f, .52f); line.transform.position = new Vector3(0, -.012f, 0);
            line.transform.localScale = new Vector3(30, .012f, 1);
            int oldRate = Time.captureFramerate;
            RenderTexture oldActive = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                Time.captureFramerate = 30;
                var trouser = Array.Find(rig.GetComponentsInChildren<SkinnedMeshRenderer>(), part => part.name == "NearThigh");
                Assert.IsNotNull(trouser, "Trousers must use a deformable skin, not a rigid sprite.");
                rig.Pose(.22f, 1, false);
                yield return null;
                var baked = new Mesh();
                try
                {
                    trouser.BakeMesh(baked);
                    Vector3[] rest = trouser.sharedMesh.vertices, deformed = baked.vertices;
                    // 此骨架整体缩放为 1/64；BakeMesh 包含根骨缩放，先回到网格的像素坐标。
                    Vector3 scale = trouser.transform.lossyScale;
                    Vector3 bakeToRig = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    Matrix4x4 rigid = trouser.transform.worldToLocalMatrix * trouser.bones[0].localToWorldMatrix * trouser.sharedMesh.bindposes[0];
                    float nonRigidMovement = 0;
                    for (int v = 0; v < rest.Length; v++)
                        nonRigidMovement = Mathf.Max(nonRigidMovement, Vector3.Distance(Vector3.Scale(deformed[v], bakeToRig), rigid.MultiplyPoint3x4(rest[v])));
                    Assert.Greater(nonRigidMovement, .1f, "The trouser mesh must actually bend relative to the thigh bone.");
                    Assert.Less(nonRigidMovement, 5, "Skinning must not explode the short trouser mesh.");
                    rig.Pose(.25f, 0, false);
                    trouser.BakeMesh(baked);
                    Vector3[] neutral = baked.vertices;
                    for (int v = 0; v < rest.Length; v++)
                        Assert.Less(Vector3.Distance(rest[v], Vector3.Scale(neutral[v], bakeToRig)), .01f, "The neutral skin must preserve the approved standing silhouette.");
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
                float cycle = .25f, blend = 0;
                Vector3 previousFoot = Vector3.zero;
                float previousCycle = 0;
                bool previouslyPlanted = false;
                int plantedChecks = 0;
                for (int frame = 0; frame < 264; frame++)
                {
                    yield return null;
                    bool moving = frame >= 24 && frame < 120 || frame >= 144 && frame < 240;
                    bool left = frame >= 144;
                    Vector2 delta = moving ? (left ? Vector2.left : Vector2.right) * ForestBCharacterRig.CycleDistance / ForestBCharacterRig.CycleSeconds / 30 : Vector2.zero;
                    rig.transform.position += (Vector3)delta;
                    if (moving) cycle = Mathf.Repeat(cycle + 1f / 30 / ForestBCharacterRig.CycleSeconds, 1);
                    blend = Mathf.MoveTowards(blend, moving ? 1 : 0, 1f / 6);
                    if (blend == 0) cycle = .25f;
                    rig.Pose(cycle, blend, left);
                    camera.transform.position = new Vector3(rig.transform.position.x, .78f, -10);
                    Assert.Less(Vector3.Distance(rig.FrontFoot.position, rig.FrontTarget.position), .001f, "Short legs must reach each foot target without IK stretching.");
                    Assert.AreEqual(11f / 64, Vector3.Distance(rig.FrontHip.position, rig.FrontKnee.position), .0001f);
                    Assert.Less(Vector3.Angle(rig.FrontKnee.position - rig.FrontHip.position,
                        rig.FrontFoot.position - rig.FrontKnee.position), 45, "The short-legged walk must not collapse into a deep crouch.");
                    bool planted = moving && blend == 1 && cycle > .08f && cycle < .48f;
                    if (planted && previouslyPlanted && cycle > previousCycle)
                    {
                        Assert.Less(Vector3.Distance(rig.FrontFoot.position, previousFoot), .001f, "The supporting foot must not slide.");
                        plantedChecks++;
                    }
                    previouslyPlanted = planted; previousFoot = rig.FrontFoot.position; previousCycle = cycle;
                    Capture(camera, target, pixels, output, frame.ToString("D4"));
                }
                Assert.Greater(plantedChecks, 30);
                Assert.Less(rig.transform.position.magnitude, .001f, "Equal forward and return walks must end at the starting point.");
                if (!string.IsNullOrEmpty(output))
                {
                    string bonesOutput = Path.Combine(output, "bones"); Directory.CreateDirectory(bonesOutput);
                    string[][] chains = { new[] { "Pelvis", "Chest", "HeadBone", "Head" },
                        new[] { "NearShoulder", "NearElbow", "NearHand" }, new[] { "FarShoulder", "FarElbow", "FarHand" },
                        new[] { "NearHip", "NearKnee", "NearAnkle" }, new[] { "FarHip", "FarKnee", "FarAnkle" } };
                    Transform[] bones = rig.GetComponentsInChildren<Transform>();
                    var material = new Material(Shader.Find("Sprites/Default"));
                    var lines = new LineRenderer[chains.Length];
                    try
                    {
                        for (int c = 0; c < chains.Length; c++)
                        {
                            lines[c] = new GameObject("BoneOverlay").AddComponent<LineRenderer>();
                            lines[c].transform.SetParent(stage.transform); lines[c].sharedMaterial = material;
                            lines[c].positionCount = chains[c].Length; lines[c].startWidth = lines[c].endWidth = .012f;
                            lines[c].startColor = lines[c].endColor = new Color(1, .65f, .2f);
                            lines[c].sortingOrder = 100;
                        }
                        for (int frame = 0; frame < 60; frame++)
                        {
                            rig.Pose(frame / (30f * ForestBCharacterRig.CycleSeconds), 1, false);
                            for (int c = 0; c < chains.Length; c++) for (int b = 0; b < chains[c].Length; b++)
                                lines[c].SetPosition(b, Array.Find(bones, bone => bone.name == chains[c][b]).position);
                            Capture(camera, target, pixels, bonesOutput, frame.ToString("D4"));
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(material); }
                }
            }
            finally
            {
                Time.captureFramerate = oldRate; RenderTexture.active = oldActive;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(lineSprite);
            }
        }

        [UnityTest]
        public IEnumerator FullBodyWalkAndWardrobeUseActualGame()
        {
            var canvas = new GameObject("ForestReviewCanvas", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var host = new GameObject("ForestReviewHost", typeof(ReviewHost));
            GameStardewAIView view = null;
            FarmPrototypeController controller = null;
            RenderTexture target = null;
            Camera camera = null;
            Texture2D pixels = null;
            int oldRate = Time.captureFramerate;
            bool hadTopPreference = PlayerPrefs.HasKey("StardewAI.Wear.1");
            int oldTopPreference = PlayerPrefs.GetInt("StardewAI.Wear.1");
            RenderTexture oldActive = RenderTexture.active;
            string output = Environment.GetEnvironmentVariable("STARDEW_WALK_CAPTURE_DIR");
            try
            {
                view = new GameStardewAIView(host.GetComponent<ReviewHost>(), canvas.transform, delegate { }, delegate { });
                controller = UnityEngine.Object.FindObjectOfType<FarmPrototypeController>();
                controller.enabled = false;
                var rig = Get<ForestCharacterRig>(controller, "_playerRig");
                Assert.IsNotNull(rig);
                canvas.SetActive(false);
                for (int i = 0; i < 6; i++) rig.SetWear((ForestCharacterRig.WearSlot)i, 0, false);
                Set(controller, "_playerPosition", new Vector2(-4, -6.8f));
                Set(controller, "_facing", Vector2Int.right);
                camera = Get<Camera>(controller, "_mainCamera");
                target = new RenderTexture(960, 540, 24);
                pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                camera.targetTexture = target; camera.orthographicSize = 2.25f; camera.aspect = 960f / 540;
                Time.captureFramerate = 30;
                if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                Vector2 initial = Get<Vector2>(controller, "_playerPosition");
                Vector3 previousFoot = Vector3.zero;
                float previousCycle = 1, previousDirection = 0, furthest = initial.x;
                int plantedSamples = 0;
                bool previousPlanted = false;
                for (int frame = 0; frame < 165; frame++)
                {
                    yield return null;
                    Vector2 input = frame < 15 || frame >= 75 && frame < 90 || frame >= 150 ? Vector2.zero : frame < 75 ? Vector2.right : Vector2.left;
                    Vector2 before = Get<Vector2>(controller, "_playerPosition");
                    Call(controller, "UpdatePlayerMovement", input);
                    Vector2 position = Get<Vector2>(controller, "_playerPosition");
                    rig.Advance(position - before, Time.deltaTime);
                    Call(controller, "UpdatePlayerVisual", position - before);
                    furthest = Mathf.Max(furthest, position.x);
                    bool planted = rig.Cycle > .08f && rig.Cycle < .48f && input.x != 0 && rig.WalkBlend == 1;
                    if (planted && previousPlanted && rig.Cycle > previousCycle && input.x == previousDirection)
                    {
                        Assert.Less(Vector3.Distance(previousFoot, rig.FrontFoot.position), .003f, "Supporting ankle must remain planted in world space.");
                        plantedSamples++;
                    }
                    Assert.AreEqual(1, rig.FacingView);
                    Assert.Less(Vector3.Distance(rig.FrontFoot.position, rig.transform.position), 1f);
                    previousFoot = rig.FrontFoot.position; previousCycle = rig.Cycle; previousPlanted = planted; previousDirection = input.x;
                    if (frame == 80) { rig.SetWear(ForestCharacterRig.WearSlot.Top, 1, false); rig.SetWear(ForestCharacterRig.WearSlot.Shoes, 1, false); }
                    camera.transform.position = new Vector3(position.x, position.y + .5f, -10);
                    Capture(camera, target, pixels, output, frame.ToString("D4"));
                }
                Assert.Greater(furthest - initial.x, 1.7f);
                Assert.Less(Vector2.Distance(initial, Get<Vector2>(controller, "_playerPosition")), .1f);
                Assert.Greater(plantedSamples, 20);
                Assert.AreEqual(0, rig.WalkBlend);
                var directions = new[] { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left };
                // 混搭的 64 种组合在四个朝向共享同一套骨骼和资源，不依赖服装专属动画。
                foreach (Vector2Int direction in directions)
                {
                    for (int combination = 0; combination < 64; combination++)
                    {
                        for (int slot = 0; slot < 6; slot++) rig.SetWear((ForestCharacterRig.WearSlot)slot, (combination >> slot) & 1, false);
                        rig.Pose(direction, .45f, 100);
                        foreach (SpriteRenderer part in rig.GetComponentsInChildren<SpriteRenderer>()) Assert.IsNotNull(part.sprite, part.name);
                        Assert.AreEqual((combination >> 1) & 1, rig.GetWear(ForestCharacterRig.WearSlot.Top));
                    }
                    rig.Pose(direction, 0, 100);
                    Capture(camera, target, pixels, output, "outfit-" + direction.x + "-" + direction.y);
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => rig.SetWear(ForestCharacterRig.WearSlot.Top, 2, false));
                Assert.Throws<ArgumentOutOfRangeException>(() => rig.SetWear((ForestCharacterRig.WearSlot)99, 0, false));
                var topButton = Array.Find(canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true), button => button.name == "Wear1");
                Assert.IsNotNull(topButton);
                topButton.onClick.Invoke();
                Assert.AreEqual(0, rig.GetWear(ForestCharacterRig.WearSlot.Top));
                Assert.AreEqual(1, rig.GetWear(ForestCharacterRig.WearSlot.Shoes), "Changing the shirt must preserve independent shoe selection.");
                var reload = new GameObject("WardrobeReload").AddComponent<ForestCharacterRig>();
                try { Assert.AreEqual(0, reload.GetWear(ForestCharacterRig.WearSlot.Top), "A new actor must restore the saved choice."); }
                finally { UnityEngine.Object.DestroyImmediate(reload.gameObject); }
                Set(controller, "_facing", Vector2Int.right);
                Set(controller, "_toolActionTimer", .15f);
                Call(controller, "UpdatePlayerVisual", Vector2.zero);
                typeof(FarmPrototypeController).GetMethod("UpdateToolActionAnimation", PrivateInstance).Invoke(controller, null);
                var tool = Get<SpriteRenderer>(controller, "_playerToolRenderer");
                Assert.IsTrue(tool.enabled);
                Assert.Less(Vector3.Distance(tool.transform.position, rig.FrontHand.position), .2f);
                Capture(camera, target, pixels, output, "tool-action");
                camera.orthographicSize = 10;
                camera.transform.position = new Vector3(-2, 0, -10);
                Capture(camera, target, pixels, output, "world");
                if (!string.IsNullOrEmpty(output))
                {
                    canvas.SetActive(true);
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                        if (child.name == "MiniGamePopup") child.gameObject.SetActive(false);
                    Canvas reviewCanvas = canvas.GetComponent<Canvas>();
                    reviewCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    reviewCanvas.worldCamera = camera; reviewCanvas.planeDistance = 2;
                    camera.cullingMask = ~0; camera.orthographicSize = 2.25f;
                    Vector2 characterPosition = Get<Vector2>(controller, "_playerPosition");
                    camera.transform.position = new Vector3(characterPosition.x + 1.5f, characterPosition.y + .5f, -10);
                    Set(controller, "_toolActionTimer", 0f);
                    tool.enabled = false;
                    Set(controller, "_facing", Vector2Int.down);
                    Call(controller, "UpdatePlayerVisual", Vector2.zero);
                    object hud = Get<object>(controller, "_hudView");
                    for (int slot = 0; slot < 6; slot++) hud.GetType().GetMethod("ShowWearOption").Invoke(hud, new object[] { slot, rig.GetWear((ForestCharacterRig.WearSlot)slot) });
                    typeof(FarmPrototypeController).GetMethod("UpdateHud", PrivateInstance).Invoke(controller, null);
                    Array.Find(canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true), button => button.name == "WardrobeButton").onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    var wardrobe = Array.Find(canvas.GetComponentsInChildren<RectTransform>(true), rect => rect.name == "WardrobePanel");
                    Assert.AreEqual("PopupHost", wardrobe.parent.name, "The wardrobe must render above the bottom toolbar.");
                    Capture(camera, target, pixels, output, "wardrobe");
                }
                camera.targetTexture = null;
            }
            finally
            {
                if (hadTopPreference) PlayerPrefs.SetInt("StardewAI.Wear.1", oldTopPreference);
                else PlayerPrefs.DeleteKey("StardewAI.Wear.1");
                PlayerPrefs.Save();
                Time.captureFramerate = oldRate; RenderTexture.active = oldActive;
                if (camera != null) camera.targetTexture = null;
                view?.Dispose();
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller.gameObject);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(canvas); UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static void Capture(Camera camera, RenderTexture target, Texture2D pixels, string output, string name)
        {
            if (string.IsNullOrEmpty(output)) return;
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
        }
        static T Get<T>(FarmPrototypeController controller, string name) => (T)typeof(FarmPrototypeController).GetField(name, PrivateInstance).GetValue(controller);
        static void Set(FarmPrototypeController controller, string name, object value) => typeof(FarmPrototypeController).GetField(name, PrivateInstance).SetValue(controller, value);
        static void Call(FarmPrototypeController controller, string name, object value) => typeof(FarmPrototypeController).GetMethod(name, PrivateInstance).Invoke(controller, new[] { value });
        sealed class ReviewHost : MonoBehaviour { }
    }
}
