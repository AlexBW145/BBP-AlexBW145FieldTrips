using AlexBWFieldtrips.DededeeCartRacing;
using AlexBWFieldtrips.Wanted;
using BepInEx;
using CampfireFrenzy;
using HarmonyLib;
using MonoMod.Utils;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Reflection;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI.SaveSystem;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AlexBWFieldtrips
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency("mtm101.rulerp.bbplus.baldidevapi", "11.0.0.0")]
    [BepInDependency("mtm101.rulerp.baldiplus.levelstudioloader", BepInDependency.DependencyFlags.HardDependency)]
    [BepInProcess("BALDI.exe")]
    public class AlexBWFieldTripsPlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "alexbw145.bbplus.thegamerfieldtrips", PLUGIN_NAME = "AlexBW145's Custom Field Trips", PLUGIN_VERSION = "0.0.0.0";
        public static AlexBWFieldTripsPlugin Instance { get; private set; }
        public static readonly AssetManager assetMan = new AssetManager();

        private void Awake()
        {
            Harmony harmony = new Harmony(PLUGIN_GUID);
            Instance = this;
            harmony.PatchAllConditionals();

            LoadingEvents.RegisterOnAssetsLoaded(Info, PreLoad(), LoadingEventOrder.Pre);
            LoadingEvents.RegisterOnAssetsLoaded(Info, PostLoad(), LoadingEventOrder.Post);
            LoadingEvents.RegisterOnAssetsLoaded(Info, BasePatches.NotImplementedWhyDidYou, LoadingEventOrder.Final);

            ModdedSaveGame.AddSaveHandler(Info);
        }

        private IEnumerator PreLoad()
        {
            yield return 5;
            yield return "Templating Templates";
            AssetLoader.LocalizationFromFunction((lang) => new System.Collections.Generic.Dictionary<string, string>()
            {
                { "Level_Trip_AlexBWWorld", "AlexBW145's Custom Field Trips" },

                { "Mgm_SuperWanted64DS_Title", "WANTED!" },
                { "Mgm_SuperWanted64DS_Desc", "Looks like the students are scattered around, but only one is different from the rest!" },
                { "Mgm_SuperWanted64DS_Win", "You've managed to reach to that required score! Way to go!" },
                { "Mgm_SuperWanted64DS_Lose", "School's out! Looks like you did not make it to the required score." },

                { "Mgm_DededeCartRacing_Title", "Coasting Cart Racing" },
                { "Mgm_DededeCartRacing_Desc", "Race against 7 other participants for the top 4! Watch out for incoming obstacles!" },
                { "Mgm_DededeCartRacing_Win", "You've placed {0}, way to go! Now that was a coasting experience!" },
                { "Mgm_DededeCartRacing_Lose", "You've placed {0}, but the race was already over..." },
                { "Mgm_DededeCartRacing_Perfect", "No Hit Bonus! +{0} YTPs" }
            });
            var template = Instantiate(Resources.FindObjectsOfTypeAll<Minigame_Campfire>().Last().GetComponent<MinigameBase>(), MTM101BaldiDevAPI.prefabTransform, true);
            DestroyImmediate(template.GetComponent<Minigame_Campfire>());
            template.gameObject.name = "MinigameTemplate";
            var game = template.transform.Find("GameCanvas").Find("Game");
            DestroyImmediate(game.Find("FrenzyBaldi").gameObject);
            DestroyImmediate(game.Find("Fire").gameObject);
            DestroyImmediate(game.Find("Fuel").gameObject);
            DestroyImmediate(game.Find("Launcher").gameObject);
            assetMan.Add<MinigameBase>("TemplateMinigame", template);
            var tutorialTemplate = Instantiate(Resources.FindObjectsOfTypeAll<MinigameTutorial>().Last(), MTM101BaldiDevAPI.prefabTransform, false);
            tutorialTemplate.gameObject.name = "MinigameTutorialTemplate";
            var tutorialCanvas = tutorialTemplate.transform.Find("GameCanvas").Find("Game");
            for (int i = 0; i < tutorialCanvas.childCount; i++)
            {
                var child = tutorialCanvas.GetChild(i);
                if (child.name.StartsWith("Page_") && child.name != "Page_0")
                    DestroyImmediate(child.gameObject);
                else if (child.name == "Page_0")
                {
                    child.name = "TutorialPage_Template";
                    assetMan.Add("TemplateTutorial_Page", child.gameObject);
                }
            }
            assetMan.Add<MinigameTutorial>("TemplateTutorial", tutorialTemplate);
            yield return "WANTED!"; // SM64DS Wanted!
            var findMusic = new LoopingSoundObject()
            {
                clips = [AssetLoader.AudioClipFromMod(this, "Wanted.mp3")],
                name = "WANTED_Minigame",
                mixer = Resources.FindObjectsOfTypeAll<AudioMixerGroup>().Last(x => x.audioMixer.name == "Music" && x.name == "Wav")
            };
            var studentfind = CreateMinigame<Minigame_Wanted>("SuperWanted64DS", "SuperWanted64DS", clipMusic: findMusic, usesCursor: true);
            studentfind.minigameHud = studentfind.gameObject.GetComponentInChildren<MinigameHUD>(true);
            studentfind.wrongClick = AssetFinder.FindOfTypeWithName<SoundObject>("Activity_Incorrect", true);
            studentfind.audPoints = AssetFinder.FindOfTypeWithName<SoundObject>("YTPPickup_0", true);
            studentfind.tickingTime = AssetFinder.FindOfTypeWithName<SoundObject>("TimeLimitTicking", true);
            studentfind.timeBell = AssetFinder.FindOfTypeWithName<SoundObject>("TimeLimitBell", true);
            studentfind.transform.GetChild(0).GetChild(0).GetComponentInChildren<Image>(true).gameObject.SetActive(false);
            studentfind.studentToFindUI = new GameObject("StudentUI", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            studentfind.studentToFindUI.raycastTarget = false;
            studentfind.studentToFindUI.rectTransform.SetParent(studentfind.transform.GetChild(0).GetChild(0), false);
            studentfind.studentToFindUI.rectTransform.sizeDelta = new(90f, 90f * 1.15f);
            studentfind.studentToFindUI.rectTransform.anchorMin = studentfind.studentToFindUI.rectTransform.anchorMax = studentfind.studentToFindUI.rectTransform.pivot = Vector2.one;
            studentfind.studentToFindUI.rectTransform.anchoredPosition = new(450f, 310f);
            studentfind.studentToFindUI.gameObject.layer = LayerMask.NameToLayer("UI");
            var indicatorFind = (TMP_Text)studentfind.minigameHud.ReflectionGetVariable("scoreIndicatorTmp");
            indicatorFind.gameObject.GetComponentInParent<Animator>(true).enabled = false;
            var indicatorBase = (RectTransform)indicatorFind.rectTransform.parent;
            indicatorBase.anchorMin = indicatorBase.anchorMax = indicatorBase.pivot = new(0.5f, 1f);
            indicatorBase.anchoredPosition = new(230f, 345f);
            indicatorFind.color = Color.white;
            studentfind.doorPre = AssetFinder.FindOfTypeWithName<StandardDoor>("ClassDoor_Standard", true);
            var findTutorialMusic = new LoopingSoundObject()
            {
                clips = [AssetLoader.AudioClipFromMod(this, "Explaination-Wanted.mp3")],
                name = "WANTED_MinigameTutorial",
                mixer = Resources.FindObjectsOfTypeAll<AudioMixerGroup>().Last(x => x.audioMixer.name == "Music" && x.name == "Wav")
            };
            var studentfindTutorial = CreateTutorial("SuperWanted64DS", "SuperWanted64DS", [
                CreateTutorialPage(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "1.jpg"), "Are you ready to search for some unique students?\nIt's time for WANTED!", new(130f, -212f, 1.5f)),
                CreateTutorialPage(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "2.jpg"), "Your job is to search for a specific student\nthat will be different from the rest!", new(-120f, 0f, 1f)),
                CreateTutorialPage(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "3.jpg"), "Once you click on the target, you will get a time bonus\nof 10 extra seconds!", new(0f, -130f, 1f)),
                CreateTutorialPage(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "4.jpg"), "There is a time limit to how long you are searching for.\nSo don't stay searching for too long!", new(0f, -130f, 2f)),
                CreateTutorialPage(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "5.jpg"), "In every next rounds, more students will appear\nso be precise on where you are searching.", new(90f, -50f, 1f)),
                CreateTutorialPage(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "6.jpg"), "In scoring mode, future rounds are made to be cramped!", new(30f, 110f, 0.5f))
                ], clipMusic: findTutorialMusic);
            yield return "Canvas Cursed"; // Canvas Curse Dedede's Cart Racing
            var kartracing = CreateMinigame<Minigame_CartRacing>("DededeCartRacing", "DededeCartRacing", perfectBonus: 1500);
            kartracing.audFinished = studentfind.timeBell;
            BasePatches.dedede = EnumExtensions.GetFromExtendedName<MinigameName>("DededeCartRacing");
            var cartPre = new GameObject("CartRacerPre", typeof(CircleCollider2D)).transform;
            cartPre.SetParent(MTM101BaldiDevAPI.prefabTransform, false);
            cartPre.GetComponent<CircleCollider2D>().radius = 2.5f;
            cartPre.GetComponent<CircleCollider2D>().isTrigger = true;
            cartPre.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            kartracing.cartPre = cartPre.gameObject.AddComponent<CartRacerController>();
            FieldInfo
                _disableSubtitles = AccessTools.DeclaredField(typeof(AudioManager), "disableSubtitles");
            var audManCart = cartPre.gameObject.AddComponent<AudioManager>();
            audManCart.audioDevice = audManCart.gameObject.AddComponent<AudioSource>();
            audManCart.audioDevice.playOnAwake = false;
            audManCart.audioDevice.dopplerLevel = 0f;
            audManCart.audioDevice.spread = 0f;
            audManCart.audioDevice.spatialBlend = 1f;
            audManCart.audioDevice.minDistance = 25f;
            audManCart.audioDevice.maxDistance = 50f;
            audManCart.ignoreListenerPause = true;
            audManCart.volumeModifier = 0.35f;
            _disableSubtitles.SetValue(audManCart, true);
            kartracing.cartPre.audMan = audManCart;
            audManCart = cartPre.gameObject.AddComponent<AudioManager>();
            audManCart.audioDevice = audManCart.gameObject.AddComponent<AudioSource>();
            audManCart.audioDevice.playOnAwake = false;
            audManCart.audioDevice.dopplerLevel = 4f;
            audManCart.audioDevice.spread = 0f;
            audManCart.audioDevice.spatialBlend = 1f;
            audManCart.audioDevice.minDistance = 25f;
            audManCart.audioDevice.maxDistance = 50f;
            audManCart.useUnscaledPitch = true;
            audManCart.ignoreListenerPause = true;
            audManCart.volumeModifier = 0.1f;
            _disableSubtitles.SetValue(audManCart, true);
            audManCart.ReflectionSetVariable("loopOnStart", true);
            audManCart.ReflectionSetVariable("soundOnStart", new SoundObject[] { ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "CART_engine.wav"), "CART_engine", SoundType.Effect, Color.white) });
            var lineren = new GameObject("Liner", typeof(LineRenderer)).GetComponent<LineRenderer>();
            lineren.transform.SetParent(cartPre.transform, false);
            lineren.transform.localPosition = Vector3.down * 2.5f;
            lineren.useWorldSpace = false;
            lineren.material = AssetFinder.FindOfTypeWithName<Material>("Lit_SpriteStandard_NoBillboard", true);
            lineren.numCapVertices = 2;
            lineren.alignment = LineAlignment.TransformZ;
            lineren.positionCount = 4;
            lineren.SetPositions([Vector3.left * 60f, Vector3.left * 40f, Vector3.left * 20f, Vector3.zero]);
            lineren.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
            lineren.gameObject.layer = LayerMask.NameToLayer("Overlay");
            kartracing.cartPre.liner = lineren;
            var render = new GameObject("Render", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(cartPre, false);
            render.material = AssetFinder.FindOfTypeWithName<Material>("Lit_SpriteStandard_NoBillboard", true);
            render.gameObject.layer = LayerMask.NameToLayer("Overlay");
            kartracing.cartPre.render = render;
            //kartracing.gameGOBJ = kartracing.transform.Find("GameCanvas").Find("Game");
            var placehold = Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "1PrizeSprites_12" && x.IsPrefab());
            render.sprite = placehold;
            Minigame_CartRacing.possibleRacers.AddRange(new Dictionary<Character, RacerData>()
            {
                { Character.Null, new RacerData(placehold, RacerDifficulty.NotParticipating) },
                { Character.Baldi, new RacerData(placehold, RacerDifficulty.Hard) },
                { Character.Principal, new RacerData(placehold, RacerDifficulty.Normal) },
                { Character.Crafters, new RacerData(placehold, RacerDifficulty.NotParticipating) },
                { Character.Prize, new RacerData(placehold, RacerDifficulty.Easy) },
                { Character.Sweep, new RacerData(placehold, RacerDifficulty.Normal) },
                { Character.Bully, new RacerData(placehold, RacerDifficulty.Hard) },
                { Character.Beans, new RacerData(placehold, RacerDifficulty.Normal) },
                { Character.Cumulo, new RacerData(placehold, RacerDifficulty.Normal) },
                { Character.Chalkles, new RacerData(placehold, RacerDifficulty.NotParticipating) },
                { Character.Playtime, new RacerData(placehold, RacerDifficulty.Easy) },
                { Character.Pomp, new RacerData(placehold, RacerDifficulty.Normal) },
                { Character.DrReflex, new RacerData(placehold, RacerDifficulty.Normal) },
                { Character.LookAt, new RacerData(placehold, RacerDifficulty.Hard) },
            });
            kartracing.movementAnalogData = Resources.FindObjectsOfTypeAll<PlayerMovement>().Last(x => x.IsPrefab()).movementAnalogData;
            RectTransform bg = (RectTransform)kartracing.transform.Find("GameCanvas").Find("Game").Find("BG");
            var bgSet = new GameObject("Stage_1");
            bgSet.transform.SetParent(bg.parent, false);
            bg.SetParent(bgSet.transform, true);
            DestroyImmediate(bg.GetComponent<Image>());
            bg.gameObject.AddComponent<RawImage>().texture = AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_1", "Background.png");
            bg.sizeDelta = new(1536f, 384f);
            bg.anchoredPosition = Vector2.zero;
            var fg = Instantiate(bg, bgSet.transform, true);
            fg.GetComponent<RawImage>().texture = AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_1", "Foreground.png");
            fg.anchoredPosition = Vector2.up * 50f;
            fg.sizeDelta = new(480f, 360f);
            fg.gameObject.name = "FG";
            var fgbottom = Instantiate(fg, fg, true);
            fgbottom.GetComponent<RawImage>().texture = AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_1", "ForegroundUnderneath.png");
            fgbottom.anchorMin = fgbottom.anchorMax = fgbottom.pivot = Vector2.right * 0.5f;
            fgbottom.anchoredPosition = Vector2.zero;
            fgbottom.gameObject.name = "Beneath";
            bg.localScale = Vector3.one;
            fg.localScale = Vector3.one;
            fgbottom.localScale = Vector3.one;
            fgbottom.localScale += Vector3.down * 2f;
            var scorller = bgSet.gameObject.AddComponent<HorizontalScroller>();
            scorller.minigame = kartracing.GetComponent<MinigameBase>();
            scorller.imgs = [fg.GetComponent<RawImage>(), fgbottom.GetComponent<RawImage>()];
            scorller = bgSet.gameObject.AddComponent<HorizontalScroller>();
            scorller.speed = 0.05f;
            scorller.minigame = kartracing.GetComponent<MinigameBase>();
            scorller.imgs = [bg.GetComponent<RawImage>()];
            kartracing.possibleStage1BGs.Add(bgSet);
            bgSet.transform.SetSiblingIndex(0);
            fg.gameObject.AddComponent<StageCamFollower>().minigame = kartracing;
            kartracing.possibleStage2BGs.AddRange([
                CreateStageBackground(bgSet, "Stage_2_School", AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_2_School", "Foreground.png"), AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_2_School", "ForegroundUnderneath.png"), AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_2_School", "Background.png")),

                ]);
            kartracing.possibleStage3BGs.AddRange([
                CreateStageBackground(bgSet, "Stage_3_Camping", AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Camp", "Foreground.png"), (Texture2D)fgbottom.GetComponent<RawImage>().texture, AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Camp", "Background.png")),

                ]);
            var ingamestage = new GameObject("Stage1");
            ingamestage.transform.SetParent(kartracing.transform, false);
            kartracing.stages.Add(ingamestage);
            ingamestage = new GameObject("Stage2");
            ingamestage.transform.SetParent(kartracing.transform, false);
            kartracing.stages.Add(ingamestage);
            ingamestage = new GameObject("Stage3");
            ingamestage.transform.SetParent(kartracing.transform, false);
            kartracing.stages.Add(ingamestage);
            /*var ending = new GameObject("Ending", typeof(BoxCollider2D)).GetComponent<BoxCollider2D>();
            ending.isTrigger = true;
            ending.transform.SetParent(ingamestage.transform, false);
            ending.transform.localScale = new(9f, 400f, 1f);
            ending.transform.localPosition = Vector3.right * 460f;
            ending.gameObject.AddComponent<StageChanger>().minigame = kartracing;*/
            var renderertexview = new GameObject("Gameplay", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            renderertexview.raycastTarget = false;
            renderertexview.rectTransform.SetParent(bgSet.transform.parent, false);
            renderertexview.rectTransform.SetSiblingIndex(renderertexview.transform.parent.childCount - 2);
            renderertexview.rectTransform.anchoredPosition = new Vector2(240f, 180f);
            renderertexview.rectTransform.sizeDelta = new Vector2(840f, 360f);
            renderertexview.rectTransform.anchorMin = Vector2.zero;
            renderertexview.rectTransform.anchorMax = Vector2.one;
            renderertexview.rectTransform.pivot = Vector2.one / 2f;
            var cartrendertex = new RenderTexture(840, 360, 0, RenderTextureFormat.ARGB32, -1);
            cartrendertex.name = "CartRenderTex";
            cartrendertex.filterMode = FilterMode.Point;
            cartrendertex.wrapMode = TextureWrapMode.Clamp;
            renderertexview.texture = cartrendertex;
            kartracing.texToUse = cartrendertex;
            yield return "Midi Guitar"; // DELTARUNE Rock Concert

            yield return "Creating Field Trip Location...";
            var reader = new BinaryReader(File.OpenRead(Path.Combine(AssetLoader.GetModPath(this), "abwfieldtripland.bpl")));
            var sceneObjectOnAccident = LevelImporter.CreateSceneObject(BaldiLevel.Read(reader));
            reader.Close();
            sceneObjectOnAccident.skyboxColor = new Color(0f / 255f, 47f / 255f, 204f / 255f);
            var fieldtripHub = new RoomAsset();
            RoomData 
                mainlobbydata = sceneObjectOnAccident.levelAsset.rooms.First(x => x.category == RoomCategory.Special),
                playroom = sceneObjectOnAccident.levelAsset.rooms.First(x => x.name == "StudentWANTEDArea");
            mainlobbydata.ConvertToAsset(fieldtripHub, default);
            studentfind.playroomPre = new RoomAsset();
            playroom.ConvertToAsset(studentfind.playroomPre, new IntVector2(4, 22));
            foreach (var tile in sceneObjectOnAccident.levelAsset.tile)
            {
                if (tile.roomId == sceneObjectOnAccident.levelAsset.rooms.IndexOf(mainlobbydata))
                {
                    fieldtripHub.cells.Add(new()
                    {
                        pos = tile.pos.Adjusted(default, Direction.North),
                        roomId = 0,
                        type = tile.type
                    });
                }
                else if (tile.roomId == sceneObjectOnAccident.levelAsset.rooms.IndexOf(playroom))
                {
                    studentfind.playroomPre.cells.Add(new()
                    {
                        pos = tile.pos.Adjusted(new IntVector2(4, 22), Direction.North),
                        roomId = 1,
                        type = tile.type
                    });
                    if (sceneObjectOnAccident.levelAsset.posters.Exists(p => p.position == tile.pos))
                    {
                        var posterData = sceneObjectOnAccident.levelAsset.posters.Find(p => p.position == tile.pos).GetNew();
                        posterData.position = posterData.position.Adjusted(new IntVector2(4, 22), Direction.North);
                        studentfind.playroomPre.posterDatas.Add(posterData);
                    }
                }
            }
            fieldtripHub.category = RoomCategory.Null;
            fieldtripHub.type = RoomType.Null;
            studentfind.playroomPre.category = RoomCategory.Class;
            studentfind.playroomPre.type = RoomType.Room;
            fieldtripHub.potentialDoorPositions.Add(default);
            studentfind.playroomPre.potentialDoorPositions.Add(new IntVector2(4, 22));
            /*foreach (var door in sceneObjectOnAccident.levelAsset.doors)
            {
                if (door.roomId == sceneObjectOnAccident.levelAsset.rooms.IndexOf(mainlobbydata))
                {
                    fieldtripHub.potentialDoorPositions.Add(door.position);
                }
                else if (door.roomId == sceneObjectOnAccident.levelAsset.rooms.IndexOf(playroom))
                {
                    studentfind.playroomPre.potentialDoorPositions.Add(door.position);
                }
            }*/
            var fieldtripLocation = Instantiate(Resources.FindObjectsOfTypeAll<FieldTripObject>().Last());
            fieldtripLocation.name = "SelfInsertsSelfMadeFieldTrips";
            ((UnityEngine.Object)fieldtripHub).name = "Room_AlexBWWorld";
            fieldtripHub.name = "AlexBWWorld";
            ((UnityEngine.Object)studentfind.playroomPre).name = "Room_AlexBWPlayroom";
            studentfind.playroomPre.name = "AlexBWPlayroom";
            fieldtripLocation.skybox = sceneObjectOnAccident.skybox;
            Vector3 placedPos = fieldtripLocation.tripHub.position.CenterWorldPosition();
            fieldtripLocation.spawnPoint = placedPos + sceneObjectOnAccident.levelAsset.spawnPoint;
            var function = Instantiate(fieldtripLocation.tripHub.room.roomFunctionContainer, MTM101BaldiDevAPI.prefabTransform);
            function.gameObject.name = "AlexBWWorldHubRoomFunction";
            var assigner = function.gameObject.AddComponent<TripAssigner>();
            function.AddFunction(assigner);
            function.GetComponent<SunlightRoomFunction>().color = sceneObjectOnAccident.skyboxColor;
            assigner.fieldTripFunc = function.GetComponent<FieldTripBaseRoomFunction>();
            assigner.fieldTripFunc.ReflectionSetVariable("pickupsToPitOffset", new Vector3(170f, 0f, -170f));
            FieldInfo _objectsToToggle = AccessTools.DeclaredField(typeof(FieldTripBaseRoomFunction), "objectToToggleAfterMinigame");
            var objectsToToggle = ((GameObject[])_objectsToToggle.GetValue(assigner.fieldTripFunc)).ToList();
            var objectBase = function.transform.Find("ObjectBase");
            List<BasicObjectData> 
                gamelocations = fieldtripHub.basicObjects.FindAll(x => x.prefab.name == "RoundTable"),
                tutorialLocations = fieldtripHub.basicObjects.FindAll(x => x.prefab.name == "Decor_Banana");
            assigner.events.Add(new TripTuple(objectBase.Find("Minigame1Trigger").GetComponent<ClickableEventTrigger>(), studentfind.GetComponent<MinigameBase>()));
            assigner.events.Add(new TripTuple(objectBase.Find("Minigame1Tutorial").GetComponent<ClickableEventTrigger>(), studentfindTutorial.GetComponent<MinigameBase>()));
            assigner.events.Add(new TripTuple(objectBase.Find("Minigame2Trigger").GetComponent<ClickableEventTrigger>(), kartracing.GetComponent<MinigameBase>()));
            objectBase.Find("Minigame1Trigger").localPosition = gamelocations[0].position + (Vector3.up * 5f);
            objectBase.Find("Minigame1Tutorial").localPosition = new(tutorialLocations[0].position.x, 5f, tutorialLocations[0].position.z);
            objectBase.Find("Minigame2Trigger").localPosition = gamelocations[2].position + (Vector3.up * 5f);
            objectBase.Find("Minigame2Tutorial").localPosition = new(tutorialLocations[2].position.x, 5f, tutorialLocations[2].position.z);
            fieldtripHub.roomFunctionContainer = function;
            fieldtripLocation.tripHub = new RoomAssetPlacementData()
            {
                direction = Direction.North,
                position = new IntVector2(0, 16),
                room = fieldtripHub,
                doorSpawnId = 0
            };
            fieldtripLocation.trip = EnumExtensions.ExtendEnum<FieldTrips>("AlexBWWorld");
            fieldtripLocation.nameKey = "Level_Trip_AlexBWWorld";
            fieldtripLocation.intro = Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "Jon_Expel3");
            fieldtripHub.basicObjects.RemoveAll(gamelocations.Contains);
            fieldtripHub.basicObjects.RemoveAll(tutorialLocations.Contains);
            var buspos = fieldtripHub.basicObjects.First(x => x.prefab.name == "FactoryShelf_Object");
            objectBase.Find("Bus").localPosition = buspos.position;
            objectBase.Find("Bus_Occupied").localPosition = buspos.position;
            objectBase.Find("ExitTrigger").localPosition = objectBase.Find("Bus_Occupied").localPosition + new Vector3(-6f, 5f, 0f);
            objectBase.Find("AnimatedTripBaldi").localPosition = new(buspos.position.x + 14f, 3.66f, buspos.position.z + 6f);
            fieldtripHub.basicObjects.Remove(buspos);
            fieldtripHub.florTex = AssetLoader.TextureFromMod(this, "ABWLandFloor.png");
            var theMoon = Instantiate(objectBase.transform.Find("PineTree_Bully").gameObject, objectBase.transform, false);
            theMoon.name = "TheMoon";
            theMoon.GetComponentInChildren<SpriteRenderer>(true).sprite = AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 2f, "ABWLandMoon.png");
            theMoon.GetComponentInChildren<SpriteRenderer>(true).transform.localPosition = Vector3.zero;
            theMoon.GetComponentInChildren<SpriteRenderer>(true).material = AssetFinder.FindOfTypeWithName<Material>("Lit_SpriteStandard_Billboard", true);
            theMoon.transform.localScale = Vector3.one * 3f;
            theMoon.transform.localPosition = new(930f, -250f, 696f);
            Destroy(objectBase.transform.Find("PineTree_Bully").gameObject);
            Destroy(objectBase.transform.Find("FirstPrize_SpriteBase").gameObject);
            objectsToToggle.RemoveAll(x => x == null);
            _objectsToToggle.SetValue(assigner.fieldTripFunc, objectsToToggle.ToArray());

            assetMan.Add("FieldTrip", fieldtripLocation);
        }

        private GameObject CreateStageBackground(GameObject bgSetTemplate, string name, Texture2D foreground, Texture2D foregroundBottom, Texture2D sky)
        {
            var bgSet = Instantiate(bgSetTemplate, bgSetTemplate.transform.parent, true);
            bgSet.name = name;
            bgSet.transform.localScale = Vector3.one;
            bgSet.transform.localPosition = Vector3.zero;
            bgSet.transform.Find("BG").GetComponent<RawImage>().texture = sky;
            bgSet.transform.Find("FG").GetComponent<RawImage>().texture = foreground;
            bgSet.transform.Find("FG").GetChild(0).GetComponent<RawImage>().texture = foregroundBottom;
            bgSet.SetActive(false);
            bgSet.transform.SetSiblingIndex(0);
            return bgSet;
        }

        private IEnumerator PostLoad()
        {
            yield return 1;
            yield return "Invoking mod changes...";
            GeneratorManagement.InvokeFieldTripLootChange(assetMan.Get<FieldTripObject>("FieldTrip"));
            foreach (Student student in Resources.FindObjectsOfTypeAll<Student>().Where(npc => npc.gameObject.GetInstanceID() >= 0 || npc.transform.parent == MTM101BaldiDevAPI.prefabTransform))
                Minigame_Wanted.Students.Add(student);
        }
        private static FieldInfo
            _minigameController = AccessTools.DeclaredField(typeof(MinigameBase), "minigameController"),
            _minigame = AccessTools.DeclaredField(typeof(MinigameBase), "minigame"),
            _minigameNameKey = AccessTools.DeclaredField(typeof(MinigameBase), "minigameNameKey"),
            _winKey = AccessTools.DeclaredField(typeof(MinigameBase), "winKey"),
            _loseKey = AccessTools.DeclaredField(typeof(MinigameBase), "loseKey"),
            _perfectBonus = AccessTools.DeclaredField(typeof(MinigameBase), "perfectBonus"),
            _midiSong = AccessTools.DeclaredField(typeof(MinigameBase), "midiSong"),
            _musicObject = AccessTools.DeclaredField(typeof(MinigameBase), "musicObject"),
            _useCursor = AccessTools.DeclaredField(typeof(MinigameBase), "useCursor"),
            _titleObject = AccessTools.DeclaredField(typeof(MinigameBase), "titleObject"),


            _screen = AccessTools.DeclaredField(typeof(MinigameTutorial), "screen"),
            _tooltipKey = AccessTools.DeclaredField(typeof(MinigameTutorial), "tooltipKey"),
            _positionAndScale = AccessTools.DeclaredField(typeof(MinigameTutorial), "positionAndScale"),
            _audVoice = AccessTools.DeclaredField(typeof(MinigameTutorial), "audVoice");
        private T CreateMinigame<T>(string minigameLocal, string minigameEnum, LoopingSoundObject clipMusic = null, string midiMusic = "", int perfectBonus = 500, bool usesCursor = false) where T : Minigame
        {
            var newMinigame = Instantiate(assetMan.Get<MinigameBase>("TemplateMinigame"), MTM101BaldiDevAPI.prefabTransform, false).gameObject.AddComponent<T>();
            newMinigame.gameObject.name = "Minigame_" + minigameEnum;
            var minigameBase = newMinigame.GetComponent<MinigameBase>();
            _minigameController.SetValue(minigameBase, newMinigame);
            _minigame.SetValue(minigameBase, EnumExtensions.ExtendEnum<MinigameName>(minigameEnum));
            _minigameNameKey.SetValue(minigameBase, $"Mgm_{minigameLocal}_Title");
            _winKey.SetValue(minigameBase, $"Mgm_{minigameLocal}_Win");
            _loseKey.SetValue(minigameBase, $"Mgm_{minigameLocal}_Lose");
            _perfectBonus.SetValue(minigameBase, perfectBonus);
            _midiSong.SetValue(minigameBase, midiMusic); // This overrides the sound object music
            if (clipMusic != null) // musicObject will not do anything if midi music is used
                _musicObject.SetValue(minigameBase, clipMusic);
            _useCursor.SetValue(minigameBase, usesCursor);
            GameObject titleObject = _titleObject.GetValue(minigameBase) as GameObject;
            titleObject.transform.Find("Title").GetComponent<TextLocalizer>().key = $"Mgm_{minigameLocal}_Title";
            titleObject.transform.Find("Description").GetComponent<TextLocalizer>().key = $"Mgm_{minigameLocal}_Desc";

            return newMinigame;
        }

        private MinigameTutorial CreateTutorial(string minigameLocal, string minigameEnum, Tuple<GameObject, string, Vector3>[] pages, LoopingSoundObject clipMusic = null, string midiMusic = "", SoundObject[] voicePages = null)
        {
            if (voicePages == null)
                voicePages = new SoundObject[pages.Length];
            else if (voicePages.Length > pages.Length)
                Array.Resize(ref voicePages, pages.Length);
            var newTutorial = Instantiate(assetMan.Get<MinigameTutorial>("TemplateTutorial"), MTM101BaldiDevAPI.prefabTransform, false);
            newTutorial.gameObject.name = "MinigameTutorial_" + minigameEnum;
            SoundObject silence = ((SoundObject[])_audVoice.GetValue(newTutorial))[0];
            var minigameBase = newTutorial.GetComponent<MinigameBase>();
            _minigameController.SetValue(minigameBase, newTutorial);
            _minigame.SetValue(minigameBase, EnumExtensions.ExtendEnum<MinigameName>(minigameEnum));
            _minigameNameKey.SetValue(minigameBase, $"Mgm_{minigameLocal}_Title");
            _midiSong.SetValue(minigameBase, midiMusic); // This overrides the sound object music
            newTutorial.music = midiMusic;
            if (clipMusic != null) // musicObject will not do anything if midi music is used
                _musicObject.SetValue(minigameBase, clipMusic);

            GameObject[] screens = new GameObject[voicePages.Length];
            string[] tooltipKeys = new string[voicePages.Length];
            Vector3[] positionsAndScales = new Vector3[voicePages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Item1.name = $"Page_{i}";
                pages[i].Item1.transform.SetParent(newTutorial.transform.Find("GameCanvas").Find("Game"), true);
                pages[i].Item1.GetComponent<RectTransform>().sizeDelta = newTutorial.transform.Find("GameCanvas").Find("Game").GetChild(0).GetComponent<RectTransform>().sizeDelta;
                pages[i].Item1.transform.localScale = Vector3.one;
                screens[i] = pages[i].Item1;
                tooltipKeys[i] = pages[i].Item2;
                positionsAndScales[i] = pages[i].Item3;
                if (voicePages[i] == null)
                    voicePages[i] = silence;
            }
            _screen.SetValue(newTutorial, screens);
            _tooltipKey.SetValue(newTutorial, tooltipKeys);
            _positionAndScale.SetValue(newTutorial, positionsAndScales);
            _audVoice.SetValue(newTutorial, voicePages);

            return newTutorial;
        }

        private Tuple<GameObject, string, Vector3> CreateTutorialPage(Sprite image, string subKey, Vector3 baldiPosScale)
        {
            var page = Instantiate(assetMan.Get<GameObject>("TemplateTutorial_Page"), assetMan.Get<GameObject>("TemplateTutorial_Page").transform.parent, true);
            page.GetComponent<Image>().sprite = image;
            return new(page, subKey, baldiPosScale);
        }
    }

    [Serializable]
    public class TripTuple
    {
        public ClickableEventTrigger trigger;
        public MinigameBase minigame;
        public TripTuple(ClickableEventTrigger clickableTrigger, MinigameBase minigame)
        {
            trigger = clickableTrigger;
            this.minigame = minigame;
        }
            
    }

    internal class TripAssigner : RoomFunction, ISerializationCallbackReceiver
    {
        [SerializeField] internal List<TripTuple> events = new List<TripTuple>();
        [SerializeField] internal FieldTripBaseRoomFunction fieldTripFunc;

        [SerializeField, HideInInspector] private List<ClickableEventTrigger> triggers = new List<ClickableEventTrigger>();
        [SerializeField, HideInInspector] private List<MinigameBase> minigames = new List<MinigameBase>();

        private static FieldInfo _OnPress = AccessTools.Field(typeof(ClickableEventTrigger), "OnPress");

        public void OnAfterDeserialize()
        {
            for (int i = 0; i != Math.Min(triggers.Count, minigames.Count); i++)
                events.Add(new TripTuple(triggers[i], minigames[i]));
        }

        public void OnBeforeSerialize()
        {
            triggers.Clear();
            minigames.Clear();
            foreach (var kvp in events)
            {
                triggers.Add(kvp.trigger);
                minigames.Add(kvp.minigame);
            }
        }

        public override void OnGenerationFinished()
        {
            base.OnGenerationFinished();
            foreach (var trigger in events)
            {
                var uevent = new UnityEvent();
                uevent.AddListener(() => fieldTripFunc.StartMinigame(trigger.minigame));
                _OnPress.SetValue(trigger.trigger, uevent);
            }
        }
    }
}
