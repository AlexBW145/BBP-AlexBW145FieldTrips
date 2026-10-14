using AlexBWFieldtrips.DededeeCartRacing;
using AlexBWFieldtrips.Wanted;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using MonoMod.Utils;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Components.Animation;
using MTM101BaldAPI.OptionsAPI;
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
using ThumblysFieldTripsAPI;
using ThumblysFieldTripsAPI.Builders;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AlexBWFieldtrips
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency("mtm101.rulerp.bbplus.baldidevapi", "11.2.0.1")]
    [BepInDependency("mtm101.rulerp.baldiplus.levelstudioloader", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("thumbly.baldiplus.fieldtripAPI", BepInDependency.DependencyFlags.HardDependency)]
    [BepInProcess("BALDI.exe")]
    public class AlexBWFieldTripsPlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "alexbw145.bbplus.thegamerfieldtrips", PLUGIN_NAME = "AlexBW145's Custom Field Trips", PLUGIN_VERSION = "1.0.0.0";
        public static AlexBWFieldTripsPlugin Instance { get; private set; }
        public static readonly AssetManager assetMan = new AssetManager();
        internal static ConfigEntry<bool> CartTimeTrialGhosts;

        private void Awake()
        {
            Harmony harmony = new Harmony(PLUGIN_GUID);
            Instance = this;
            harmony.PatchAllConditionals();

            LoadingEvents.RegisterOnAssetsLoaded(Info, AssetsLoad, LoadingEventOrder.Start);
            LoadingEvents.RegisterOnAssetsLoaded(Info, PreLoad(), LoadingEventOrder.Pre);
            LoadingEvents.RegisterOnAssetsLoaded(Info, PostLoad(), LoadingEventOrder.Post);

            ModdedSaveGame.AddSaveHandler(Info);
        }

        private class ModOptions : CustomOptionsCategory
        {
            public override void Build()
            {
                var toggle = CreateToggle("CartTimeTrialGhosts", "Time Trial Ghosts", CartTimeTrialGhosts.Value, new Vector2(75f, 40f), 198f);
                AddTooltip(toggle, "If enabled, ghosts can appear\nfor Coasting Cart Racing's time trials\nand you are able to record your own attempted time!");
                toggle.GetComponentInChildren<StandardMenuButton>().OnPress.AddListener(() => CartTimeTrialGhosts.Value = toggle.Value);
            }
        }

        private void AssetsLoad()
        {
            CartTimeTrialGhosts = Config.Bind("Sharing Settings", "Time Attack Ghosts", true, "If enabled, ghosts can appear for Coasting Cart Racing's time trials and you are able to record your own attempted time!");
            CustomOptionsCore.OnMenuInitialize += (__instance, handler) =>
            {
                handler.AddCategory<ModOptions>("ABW's Funland");
            };
            AssetLoader.LocalizationFromFunction((lang) => new System.Collections.Generic.Dictionary<string, string>()
            {
                { "Level_Trip_AlexBWWorld", "AlexBW145's Funland" },

                { "Mgm_SuperWanted64DS_Title", "WANTED!" },
                { "Mgm_SuperWanted64DS_Desc", "Looks like the students are scattered around, but only one is different from the rest!" },
                { "Mgm_SuperWanted64DS_Win", "You've managed to reach to that required score! Way to go!" },
                { "Mgm_SuperWanted64DS_Lose", "School's out! Looks like you did not make it to the required score." },

                { "Mgm_DededeCartRacing_Title", "Coasting Cart Racing" },
                { "Mgm_DededeCartRacing_Desc", "Race against 7 other participants for the top 4! Watch out for incoming obstacles!" },
                { "Mgm_DededeCartRacing_Win", "You've placed {0}, way to go! Now that was a coasting experience!" },
                { "Mgm_DededeCartRacing_Lose", "You've placed {0}, but the race was already over..." },
                { "Mgm_DededeCartRacing_Perfect", "No Hit Bonus! +{0} YTPs" },
                { "Mgm_DededeCartRacing_FinalScore", "Final score: {0}\n\n" }
            });
            assetMan.Add<Sprite[]>("Tutorial/WANTED", [
                AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "1.jpg"), AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "2.jpg"), AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "3.jpg"), AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "4.jpg"), AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "5.jpg"), AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 1f, "Tutorial", "WANTED", "6.jpg")
                ]);
            Color him = new Color(0.1921568627f, 0.5411764706f, 1f);
            assetMan.AddRange<SoundObject>([
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "WANTED_DrumRoll.wav"), "WANTED_DrumRoll", SoundType.Effect, Color.white),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "WANTED_DrumRollEnd.wav"), "WANTED_DrumRollEnd", SoundType.Effect, Color.white),

                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "CART_engine.wav"), "CART_engine", SoundType.Effect, Color.white),

                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "ABW_FunlandNotice.mp3"), "Vfx_ABW_FunlandTrip", SoundType.Voice, him),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "ABW_FunlandPass.mp3"), "Vfx_ABW_BusPass", SoundType.Voice, him),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "ABW_FunlandIntro.mp3"), "Vfx_ABW_FunlandIntro", SoundType.Voice, him),
                ],
                [
                    "WANTED/Drumroll",
                    "WANTED/DrumrollEnd",

                    "CartRace/Engine",

                    "ABW/FieldTrip",
                    "ABW/BusPass",
                    "ABW/TripIntro",
                    ]);
            var tracktex = AssetLoader.TextureFromMod(this, "cartdash_track.png");
            var track = Sprite.Create(tracktex, new Rect(0f, 0f, tracktex.width, tracktex.height), Vector2.one / 2f, 1f, 0, SpriteMeshType.FullRect, new Vector4(0f, 4f, 0f, 4f));
            assetMan.Add("CartRace/PositionUI", track);
            var mixer = Resources.FindObjectsOfTypeAll<AudioMixerGroup>().Last(x => x.audioMixer.name == "Music" && x.name == "Wav");
            assetMan.Add<LoopingSoundObject>("Music/WANTED", new LoopingSoundObject()
            {
                clips = [AssetLoader.AudioClipFromMod(this, "Wanted.mp3")],
                name = "WANTED_Minigame",
                mixer = mixer
            });
            assetMan.Add<LoopingSoundObject>("TutorialMusic/WANTED", new LoopingSoundObject()
            {
                clips = [AssetLoader.AudioClipFromMod(this, "Explaination-Wanted.mp3")],
                name = "WANTED_MinigameTutorial",
                mixer = mixer
            });
            assetMan.Add<LoopingSoundObject>("Music/CartRace", new LoopingSoundObject()
            {
                clips = [AssetLoader.AudioClipFromMod(this, "CoastingCartRacing.mp3")],
                name = "CoastingCartRace_Minigame",
                mixer = mixer
            });
            assetMan.Add<LoopingSoundObject>("TutorialMusic/CartRace", new LoopingSoundObject()
            {
                clips = [AssetLoader.AudioClipFromMod(this, "Explaination-CoastingCartRacing.mp3")],
                name = "CoastingCartRace_MinigameTutorial",
                mixer = mixer

            });
            assetMan.Add("ABWLand/FloorTex", AssetLoader.TextureFromMod(this, "ABWLandFloor.png"));
            assetMan.Add("ABWLand/Moon", AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 2f, "ABWLandMoon.png"));
            assetMan.Add("ABWLandAmbience", AssetLoader.AudioClipFromMod(this, "ABWLand.wav"));
            var himpivot = new Vector2(0.5f, 0.325f);
            assetMan.Add("ABW/Idle", AssetLoader.SpriteFromMod(this, himpivot, 100f, "ABW_Idle.png"));
            assetMan.Add("ABW/Talk", AssetLoader.SpriteFromMod(this, himpivot, 100f, "ABW_Speak.png"));
            assetMan.Add("ABW/Tutorial1", AssetLoader.SpritesFromSpritesheet(19, 1, 1f, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "ABW_Tutorial1.png")));
            assetMan.Add("ABW/Tutorial2", AssetLoader.SpritesFromSpritesheet(19, 1, 1f, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "ABW_Tutorial2.png")));
            assetMan.Add("ABWLand/Cart", AssetLoader.SpritesFromSpritesheet(8, 1, 21f, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "CartRideIntoDrumDash.png")));
            assetMan.Add("ABWLand/Telescope", AssetLoader.SpritesFromSpritesheet(8, 1, 21f, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "ABWLandTelescope.png")));
        }

        private IEnumerator PreLoad()
        {
            yield return 4;
            yield return "Loading Field Trip Location...";
            var reader = new BinaryReader(File.OpenRead(Path.Combine(AssetLoader.GetModPath(this), "abwfieldtripland.bpl")));
            var sceneObjectOnAccident = LevelImporter.CreateSceneObject(BaldiLevel.Read(reader));
            reader.Close();
            sceneObjectOnAccident.skyboxColor = new Color(0f / 255f, 47f / 255f, 204f / 255f);
            var fieldtripHub = new RoomAsset();
            RoomData
                mainlobbydata = sceneObjectOnAccident.levelAsset.rooms.First(x => x.category == RoomCategory.Special),
                playroom = sceneObjectOnAccident.levelAsset.rooms.First(x => x.name == "StudentWANTEDArea");
            mainlobbydata.ConvertToAsset(fieldtripHub, default);
            var playroomPre = new RoomAsset();
            playroom.ConvertToAsset(playroomPre, new IntVector2(4, 22));
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
                    playroomPre.cells.Add(new()
                    {
                        pos = tile.pos.Adjusted(new IntVector2(4, 22), Direction.North),
                        roomId = 1,
                        type = tile.type
                    });
                    if (sceneObjectOnAccident.levelAsset.posters.Exists(p => p.position == tile.pos))
                    {
                        var posterData = sceneObjectOnAccident.levelAsset.posters.Find(p => p.position == tile.pos).GetNew();
                        posterData.position = posterData.position.Adjusted(new IntVector2(4, 22), Direction.North);
                        playroomPre.posterDatas.Add(posterData);
                    }
                }
            }
            fieldtripHub.category = RoomCategory.FieldTrip;
            fieldtripHub.type = RoomType.Null;
            playroomPre.category = RoomCategory.Class;
            playroomPre.type = RoomType.Room;
            fieldtripHub.potentialDoorPositions.Add(default);
            playroomPre.potentialDoorPositions.Add(new IntVector2(4, 22));

            yield return "WANTED!"; // SM64DS Wanted!
            var findMusic = assetMan.Get<LoopingSoundObject>("Music/WANTED");
            var studentfind = new MinigameBaseBuilder<Minigame_Wanted>()
                .SetName("Minigame_SuperWanted64DS")
                .SetEnum("SuperWanted64DS")
                .SetTitle("Mgm_SuperWanted64DS_Title", "Mgm_SuperWanted64DS_Desc")
                .SetEndText("Mgm_SuperWanted64DS_Win", "Mgm_SuperWanted64DS_Lose")
                .SetMusic(findMusic)
                .NeedsCursor()
                .SetWinBonus(700)
                .Build().GetComponent<Minigame_Wanted>();
            studentfind.playroomPre = playroomPre;
            studentfind.minigameHud = Instantiate(FieldTripsAPI.minigameHudPref, studentfind.transform.Find("GameCanvas").Find("Game"));
            studentfind.wrongClick = AssetFinder.FindOfTypeWithName<SoundObject>("Activity_Incorrect", true);
            studentfind.audPoints = AssetFinder.FindOfTypeWithName<SoundObject>("YTPPickup_0", true);
            studentfind.tickingTime = AssetFinder.FindOfTypeWithName<SoundObject>("TimeLimitTicking", true);
            studentfind.timeBell = AssetFinder.FindOfTypeWithName<SoundObject>("TimeLimitBell", true);
            studentfind.drumRoll = assetMan.Get<SoundObject>("WANTED/Drumroll");
            studentfind.drumRollEnd = assetMan.Get<SoundObject>("WANTED/DrumrollEnd");
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
            var findTutorialMusic = assetMan.Get<LoopingSoundObject>("TutorialMusic/WANTED");
            Sprite[] tutorialScreens = assetMan.Get<Sprite[]>("Tutorial/WANTED");
            Sprite[] me1 = assetMan.Get<Sprite[]>("ABW/Tutorial1"), me2 = assetMan.Get<Sprite[]>("ABW/Tutorial2");
            var studentfindTutorial = new MinigameTutorialBuilder()
                .SetName("MinigameTutorial_SuperWanted64DS")
                .AddScreen(new TutorialScreenInfo(tutorialScreens[0], new(130f, -212f, 1.5f), "Are you ready to search for some unique students?\nIt's time for WANTED!"))
                .AddScreen(new(tutorialScreens[1], new(-120f, 0f, 1f), "Your job is to search for a specific student\nthat will be different from the rest!"))
                .AddScreen(new(tutorialScreens[2], new(0f, -130f, 1f), "Once you click on the target, you will get a time bonus\nof 10 extra seconds!"))
                .AddScreen(new(tutorialScreens[3], new(0f, -130f, 2f), "There is a time limit to how long you are searching for.\nSo don't stay searching for too long!"))
                .AddScreen(new(tutorialScreens[4], new(90f, -50f, 1f), "In every next rounds, more students will appear\nso be precise on where you are searching."))
                .AddScreen(new TutorialScreenInfo(tutorialScreens[5], new(30f, 110f, 0.5f), "In scoring mode, future rounds are made to be cramped!"))
                .SetMidi("")
                .AddTutorAnimation("ABW_Talk1", new SpriteAnimation(17, me1))
                .AddTutorAnimation("ABW_Talk2", new SpriteAnimation(15, me2))
                .Build();
            _musicObject.SetValue(studentfindTutorial.GetComponent<MinigameBase>(), findTutorialMusic);
            //_minigame.SetValue(studentfindTutorial.GetComponent<MinigameBase>(), (MinigameName)_minigame.GetValue(studentfind.GetComponent<MinigameBase>())); // See the patches
            yield return "Canvas Cursed"; // Canvas Curse Dedede's Cart Racing
            var kartracing = new MinigameBaseBuilder<Minigame_CartRacing>()
                .SetName("Minigame_DededeCartRacing")
                .SetEnum("DededeCartRacing")
                .SetTitle("Mgm_DededeCartRacing_Title", "Mgm_DededeCartRacing_Desc")
                .SetEndText("Mgm_DededeCartRacing_Win", "Mgm_DededeCartRacing_Lose")
                .SetMusic(assetMan.Get<LoopingSoundObject>("Music/CartRace"))
                .SetWinBonus(1500)
                .Build().GetComponent<Minigame_CartRacing>();
            kartracing.minigameHud = Instantiate(FieldTripsAPI.minigameHudPref, kartracing.transform.Find("GameCanvas").Find("Game"));
            kartracing.audFinished = studentfind.timeBell;
            BasePatches.dedede = EnumExtensions.GetFromExtendedName<MinigameName>("DededeCartRacing");
            var cartPre = new GameObject("CartRacerPre", typeof(CircleCollider2D)).transform;
            cartPre.SetParent(MTM101BaldiDevAPI.prefabTransform, false);
            cartPre.GetComponent<CircleCollider2D>().radius = 3f;
            cartPre.GetComponent<CircleCollider2D>().isTrigger = true;
            cartPre.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            kartracing.cartPre = cartPre.gameObject.AddComponent<CartRacerController>();
            var lit_nobillboard = AssetFinder.FindOfTypeWithName<Material>("Lit_SpriteStandard_NoBillboard", true);
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
            audManCart.volumeModifier = 0.75f;
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
            audManCart.ReflectionSetVariable("soundOnStart", new SoundObject[] { assetMan.Get<SoundObject>("CartRace/Engine") });
            var lineren = new GameObject("Liner", typeof(LineRenderer)).GetComponent<LineRenderer>();
            lineren.transform.SetParent(cartPre.transform, false);
            lineren.transform.localPosition = new Vector3(2f, -4.25f, 0f);
            lineren.useWorldSpace = false;
            lineren.material = lit_nobillboard;
            lineren.numCapVertices = 2;
            lineren.alignment = LineAlignment.TransformZ;
            lineren.positionCount = 5;
            lineren.SetPositions([Vector3.left * 40f, Vector3.left * 20f, Vector3.left * 10f, Vector3.left * 5f, Vector3.zero]);
            lineren.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
            lineren.gameObject.layer = LayerMask.NameToLayer("Overlay");
            kartracing.cartPre.liner = lineren;
            var render = new GameObject("Render", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(cartPre, false);
            render.material = lit_nobillboard;
            render.gameObject.layer = LayerMask.NameToLayer("Overlay");
            kartracing.cartPre.render = render;
            kartracing.cartPre.hurted = AssetFinder.FindOfTypeWithName<SoundObject>("Explosion", true);
            kartracing.cartPre.boost = AssetFinder.FindOfTypeWithName<SoundObject>("ChipCrunch", true);
            kartracing.cartPre.boostDouble = AssetFinder.FindOfTypeWithName<SoundObject>("DrinkElixir", true);
            kartracing.cartPre.boostTriple = AssetFinder.FindOfTypeWithName<SoundObject>("YTPPickup_2", true);
            //kartracing.gameGOBJ = kartracing.transform.Find("GameCanvas").Find("Game");
            var placehold = AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Template.png");
            render.sprite = placehold;
            Minigame_CartRacing.possibleRacers.AddRange(new Dictionary<Character, RacerData>()
            {
                { Character.Null, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Player.png"), RacerDifficulty.NotParticipating, new Color(225f / 255f, 58f/ 255f, 46f / 255f)) },
                { Character.Baldi, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Baldi.png"), RacerDifficulty.Hard, Color.green) },
                { Character.Principal, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Principal.png"), RacerDifficulty.Normal, new Color(0.5f, 0.5f, 0.5f)) },
                { Character.Crafters, new RacerData(placehold, RacerDifficulty.NotParticipating, Color.white) },
                { Character.Prize, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_FirstPrize.png"), RacerDifficulty.Easy, new Color(0.05f, 0.66f, 0.58f)) },
                { Character.Sweep, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Sweep.png"), RacerDifficulty.Normal, new Color(0f, 0.56f, 0.15f)) },
                { Character.Bully, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Bully.png"), RacerDifficulty.Hard, new Color(0.8f, 0.4f, 0.01f)) },
                { Character.Beans, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Beans.png"), RacerDifficulty.Normal, new Color(1f, 0f, 1f)) },
                { Character.Cumulo, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Cumulo.png"), RacerDifficulty.Normal, new Color(0.4f, 0.3f, 0.4f)) },
                { Character.Chalkles, new RacerData(placehold, RacerDifficulty.NotParticipating, Color.white) },
                { Character.Playtime, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_Playtime.png"), RacerDifficulty.Easy, Color.red) },
                { Character.Pomp, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_NoLateTeacher.png"), RacerDifficulty.Normal, new Color(0.97f, 1f, 0.4f)) },
                { Character.DrReflex, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_DrReflex.png"), RacerDifficulty.Normal, new Color(0f, 0.29f, 0.5f)) },
                { Character.LookAt, new RacerData(AssetLoader.SpriteFromMod(this, Vector2.one / 2f, 26f, "CartDash_NPCs", "CartRider_LookAt.png"), RacerDifficulty.Hard, new Color(0.2f, 0.2f, 0.18f)) },
            });
            kartracing.movementAnalogData = Resources.FindObjectsOfTypeAll<PlayerMovement>().Last(x => x.IsPrefab()).movementAnalogData;
            cartPre = new GameObject("GhostCartRacerPre").transform;
            cartPre.SetParent(MTM101BaldiDevAPI.prefabTransform, false);
            kartracing.ghostPre = cartPre.gameObject.AddComponent<CartGhostController>();
            render = new GameObject("Render", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(cartPre, false);
            render.material = lit_nobillboard;
            render.gameObject.layer = LayerMask.NameToLayer("Overlay");
            render.color = new Color(1f, 1f, 1f, 0.5f);
            render.sprite = placehold;
            kartracing.ghostPre.render = render;
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
                CreateStageBackground(bgSet, "Stage_2_Faculty", AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_2_Faculty", "Foreground.png"), AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_2_Faculty", "ForegroundUnderneath.png"), AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_2_Faculty", "Background.png")),

                ]);
            kartracing.possibleStage3BGs.AddRange([
                CreateStageBackground(bgSet, "Stage_3_Camping", AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Camp", "Foreground.png"), (Texture2D)fgbottom.GetComponent<RawImage>().texture, AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Camp", "Background.png")),
                CreateStageBackground(bgSet, "Stage_3_Laboratory", AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Laboratory", "Foreground.png"), AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Laboratory", "ForegroundUnderneath.png"), AssetLoader.TextureFromMod(this, "CartDash_BGs", "Stage_3_Laboratory", "Background.png")),

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
            var booster = new GameObject("CartBooster", typeof(CircleCollider2D));
            booster.transform.SetParent(MTM101BaldiDevAPI.prefabTransform, false);
            kartracing.boostPre = booster.AddComponent<CartBooster>();
            booster.GetComponent<CircleCollider2D>().isTrigger = true;
            booster.GetComponent<CircleCollider2D>().radius = 1.5f;
            render = new GameObject("Render", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(booster.transform, false);
            render.material = lit_nobillboard;
            render.gameObject.layer = LayerMask.NameToLayer("Overlay");
            render.drawMode = SpriteDrawMode.Sliced;
            kartracing.boostPre.spriteRenderer = render;
            kartracing.boosterSprs.AddRange(Resources.FindObjectsOfTypeAll<Sprite>().Where(x => x.name.StartsWith("PicnicFood_Sheet_") && x.IsPrefab()));
            var hurter = new GameObject("CartHurter", typeof(CircleCollider2D));
            hurter.transform.SetParent(MTM101BaldiDevAPI.prefabTransform, false);
            kartracing.hurtPre = hurter.AddComponent<CartHurter>();
            hurter.GetComponent<CircleCollider2D>().isTrigger = true;
            hurter.GetComponent<CircleCollider2D>().radius = 1.5f;
            render = new GameObject("Render", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(hurter.transform, false);
            render.material = lit_nobillboard;
            render.gameObject.layer = LayerMask.NameToLayer("Overlay");
            render.drawMode = SpriteDrawMode.Sliced;
            render.transform.localPosition = Vector3.down * 1.25f;
            kartracing.hurtPre.spriteRenderer = render;
            kartracing.hurtSpr = AssetFinder.FindOfTypeWithName<Sprite>("Campfire_Sheet_18", true);
            var gameCanvas = kartracing.minigameHud.transform.parent;
            var trackPos = new GameObject("PositionTracker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            trackPos.sprite = assetMan.Get<Sprite>("CartRace/PositionUI");
            trackPos.type = Image.Type.Tiled;
            trackPos.pixelsPerUnitMultiplier = 20f;
            trackPos.rectTransform.SetParent(gameCanvas, false);
            trackPos.rectTransform.anchorMin = trackPos.rectTransform.anchorMax = trackPos.rectTransform.pivot = Vector2.one;
            trackPos.rectTransform.sizeDelta = new(55f, 15f);
            trackPos.rectTransform.anchoredPosition = new(425f, 300f);
            kartracing.trackerTransform = trackPos.rectTransform;
            var trackPlayer = new GameObject("TrackedController", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            trackPlayer.SetParent(trackPos.rectTransform, false);
            trackPlayer.sizeDelta = Vector2.one * 5f;
            trackPlayer.anchoredPosition = Vector2.zero;
            kartracing.trackerPre = trackPlayer;
            trackPlayer.gameObject.SetActive(false);
            var kartracingTutorial = new MinigameTutorialBuilder()
                .SetName("MinigameTutorial_DededeCartRacing")
                .AddScreen(new TutorialScreenInfo(placehold, default, "It's time to race in Coasting Cart Racing!"))
                .AddScreen(new TutorialScreenInfo(placehold, default, "To push foward, eat foods that are randomly appearing accross the stages!"))
                .AddScreen(new TutorialScreenInfo(placehold, default, "Eat more at once and you will get a multiplied boost!"))
                .AddScreen(new TutorialScreenInfo(placehold, default, "Be aware of fire! Landing on them will push you down to the lowest speed,\nforcing you to go back to regular speed!"))
                .AddScreen(new TutorialScreenInfo(placehold, default, "There are three stages in total!\nWhen someone reaches to the end, the race ends!"))
                .AddScreen(new TutorialScreenInfo(placehold, default, "In scoring mode, you are after the clock for the best record!"))
                .SetMidi("")
                .AddTutorAnimation("ABW_Talk1", new SpriteAnimation(17, me1))
                .AddTutorAnimation("ABW_Talk2", new SpriteAnimation(15, me2))
                .Build();
            _musicObject.SetValue(kartracingTutorial.GetComponent<MinigameBase>(), assetMan.Get<LoopingSoundObject>("TutorialMusic/CartRace"));
            //_minigame.SetValue(studentfindTutorial.GetComponent<MinigameBase>(), (MinigameName)_minigame.GetValue(kartracing.GetComponent<MinigameBase>()));

            yield return "Creating Field Trip Location...";
            List<BasicObjectData>
                gamelocations = fieldtripHub.basicObjects.FindAll(x => x.prefab.name == "RoundTable"),
                tutorialLocations = fieldtripHub.basicObjects.FindAll(x => x.prefab.name == "Decor_Banana");
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
            var buspos = fieldtripHub.basicObjects.First(x => x.prefab.name == "FactoryShelf_Object");
            var function = Instantiate(AssetFinder.FindOfTypeWithName<SkyboxRoomFunction>("PlaygroundRoomFunction", true), MTM101BaldiDevAPI.prefabTransform).GetComponent<RoomFunctionContainer>();
            function.gameObject.name = "AlexBWWorldHubRoomFunction";
            List<RoomFunction> functions = (List<RoomFunction>)function.ReflectionGetVariable("functions");
            functions.RemoveAll(x => x is SpecialRoomSwingingDoorsBuilder or RuleFreeZone or CoverRoomFunction);
            DestroyImmediate(function.GetComponent<SpecialRoomSwingingDoorsBuilder>());
            DestroyImmediate(function.GetComponent<RuleFreeZone>());
            DestroyImmediate(function.GetComponent<CoverRoomFunction>());
            function.GetComponent<SunlightRoomFunction>().color = sceneObjectOnAccident.skyboxColor;
            function.GetComponent<AudioSource>().clip = assetMan.Get<AudioClip>("ABWLandAmbience");
            function.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3000f;
            function.AddFunction(function.gameObject.AddComponent<SpaceWorldRoomFunction>());
            var diamond = ItemMetaStorage.Instance.GetPointsObject(250, true);
            var fieldtripRoomFunction = new FieldTripRoomFunctionBuilder()
                .SetBusTransform(buspos.position, Direction.South)
                .SetPitstopItemOffset(new Vector3(170f, 0f, -170f))
                .SetFunctionGameobject(function.gameObject)
                .SetTutorialBaldiSprite(assetMan.Get<Sprite>("ABW/Idle"))
                .SetIntroTutor(new(buspos.position.x + 14f, 3.66f, buspos.position.z + 6f), null)
                .AddMinigameTrigger(new MinigameSettings(studentfind.GetComponent<MinigameBase>(), gamelocations[0].position + (Vector3.up * 5f)))
                .AddMinigameTutorialBaldi(studentfindTutorial, new(tutorialLocations[0].position.x, 5f, tutorialLocations[0].position.z))
                .AddMinigameTrigger(new MinigameSettings(kartracing.GetComponent<MinigameBase>(), gamelocations[2].position + (Vector3.up * 5f)))
                .AddMinigameTutorialBaldi(kartracingTutorial, new(tutorialLocations[2].position.x, 5f, tutorialLocations[2].position.z))
                .SetIntroTutor(new(buspos.position.x + 14f, 3.66f, buspos.position.z + 6f), assetMan.Get<SoundObject>("ABW/TripIntro"))
                .SetLootTable(new FieldTripLoot()
                {
                    guaranteedItems = [diamond, ItemMetaStorage.Instance.FindByEnum(Items.StickerPack).itemObjects.First(x => x.name == "StickerPack_Large"), diamond],
                    potentialItems = [
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.ZestyBar).value,
                            weight = 100
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.NanaPeel).value,
                            weight = 100
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.DoorLock).value,
                            weight = 100
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.Tape).value,
                            weight = 150
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.Wd40).value,
                            weight = 150
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.ReachExtender).value,
                            weight = 150
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.InvisibilityElixir).value,
                            weight = 100
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.Bsoda).value,
                            weight = 250
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.Apple).value,
                            weight = 300
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.Teleporter).value,
                            weight = 250
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.GrapplingHook).value,
                            weight = 250
                        },
                        new(){
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.PortalPoster).value,
                            weight = 50
                        },
                        new()
                        {
                            selection = ItemMetaStorage.Instance.FindByEnum(Items.StickerPack).itemObjects.First(x => x.name == "StickerPack_Fresh"),
                            weight = 15
                        }
                        ]
                })
                .Build();
            function.AddFunction(fieldtripRoomFunction);
            var tripHub = new RoomAssetPlacementData()
            {
                direction = Direction.North,
                position = new IntVector2(0, 16),
                room = fieldtripHub,
                doorSpawnId = 0
            };
            Vector3 placedPos = tripHub.position.CenterWorldPosition();
            var fieldtripLocation = new FieldTripBuilder()
                .SetName("SelfInsertsSelfMadeFieldTrips")
                .SetEnum("AlexBWWorld")
                .SetBaldiSprites(assetMan.Get<Sprite>("ABW/Idle"), assetMan.Get<Sprite>("ABW/Talk"))
                .SetSound(assetMan.Get<SoundObject>("ABW/FieldTrip"))
                .SetSkybox(sceneObjectOnAccident.skybox)
                .SetHubRoom(tripHub)
                .SetSpawn(placedPos + sceneObjectOnAccident.levelAsset.spawnPoint, Direction.North)
                .Build();
            fieldtripHub.roomFunctionContainer = function;
            ((UnityEngine.Object)fieldtripHub).name = "Room_AlexBWWorld";
            fieldtripHub.name = "AlexBWWorld";
            ((UnityEngine.Object)playroomPre).name = "Room_AlexBWPlayroom";
            playroomPre.name = "AlexBWPlayroom";
            var objectBase = function.transform.Find("ObjectBase");

            Sprite[] cartSprites = assetMan.Get<Sprite[]>("ABWLand/Cart"), teleSprites = assetMan.Get<Sprite[]>("ABWLand/Telescope");
            var cartDisplay = new GameObject("Cart");
            cartDisplay.transform.SetParent(objectBase, false);
            render = new GameObject("Renderer", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(cartDisplay.transform, false);
            render.sprite = cartSprites[0];
            render.material = AssetFinder.FindOfTypeWithName<Material>("SpriteStandard_Billboard", true);
            var rotator = render.gameObject.AddComponent<AnimatedSpriteRotator>();
            FieldInfo
                _renderer = AccessTools.DeclaredField(typeof(AnimatedSpriteRotator), "renderer"),
                _spriteSheet = AccessTools.DeclaredField(typeof(SpriteRotationMap), "spriteSheet"),
                _spriteMap = AccessTools.DeclaredField(typeof(AnimatedSpriteRotator), "spriteMap");
            _renderer.SetValue(rotator, render);
            SpriteRotationMap map = new SpriteRotationMap();
            map.angleCount = 8;
            _spriteSheet.SetValue(map, cartSprites);
            _spriteMap.SetValue(rotator, new SpriteRotationMap[] { map });
            cartDisplay.transform.localPosition = gamelocations[2].position;
            render.transform.localPosition = Vector3.up * 2.5f;
            render.gameObject.layer = LayerMask.NameToLayer("Billboard");
            cartDisplay.transform.localRotation = Direction.West.ToRotation();

            var telescope = new GameObject("Telescope");
            telescope.transform.SetParent(objectBase, false);
            render = new GameObject("Renderer", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(telescope.transform, false);
            render.sprite = teleSprites[0];
            render.material = AssetFinder.FindOfTypeWithName<Material>("SpriteStandard_Billboard", true);
            rotator = render.gameObject.AddComponent<AnimatedSpriteRotator>();
            _renderer.SetValue(rotator, render);
            map = new SpriteRotationMap();
            map.angleCount = 8;
            _spriteSheet.SetValue(map, teleSprites);
            _spriteMap.SetValue(rotator, new SpriteRotationMap[] { map });
            telescope.transform.localPosition = gamelocations[0].position;
            render.transform.localPosition = Vector3.up * 4.75f;
            render.gameObject.layer = LayerMask.NameToLayer("Billboard");
            var capsulecollider = telescope.AddComponent<CapsuleCollider>();
            capsulecollider.height = 8f;
            capsulecollider.radius = 1.5f;

            fieldtripHub.basicObjects.RemoveAll(gamelocations.Contains);
            fieldtripHub.basicObjects.RemoveAll(tutorialLocations.Contains);

            fieldtripHub.basicObjects.Remove(buspos);
            fieldtripHub.florTex = assetMan.Get<Texture2D>("ABWLand/FloorTex");
            var theMoon = new GameObject("TheMoon");
            theMoon.transform.SetParent(objectBase, false);
            render = new GameObject("Renderer", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            render.transform.SetParent(theMoon.transform, false);
            render.sprite = assetMan.Get<Sprite>("ABWLand/Moon");
            render.transform.localPosition = Vector3.zero;
            render.material = AssetFinder.FindOfTypeWithName<Material>("Lit_SpriteStandard_Billboard", true);
            render.gameObject.layer = LayerMask.NameToLayer("Billboard");
            theMoon.transform.localScale = Vector3.one * 3f;
            theMoon.transform.localPosition = new(930f, -250f, 696f);
            assetMan.Add("FieldTrip", fieldtripLocation);
            FieldTripsAPI.AddFieldTrip(fieldtripLocation, 50, TripTiers.TierOne);
            //FieldTripsAPI.AddFieldTrip(fieldtripLocation, 50, TripTiers.TierTwo);
            //FieldTripsAPI.AddFieldTrip(fieldtripLocation, 50, TripTiers.TierThree);
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

        private static FieldInfo
            _currentSource = AccessTools.Field(typeof(MusicManager), "currentSource"),
            _audioSource = AccessTools.Field(typeof(MusicManager), "fileSoure"),
            _filePlaying = AccessTools.Field(typeof(MusicManager), "filePlaying");

        internal static IEnumerator loopEnumerator;
        internal static void LoopMusic(MusicManager instance, float loopStart = 0f)
        {
            AudioSource[] sources = (AudioSource[])_audioSource.GetValue(instance);
            int source = (int)_currentSource.GetValue(instance);
            IEnumerator Loop()
            {
                float elasped = sources[source].timeSamples - 1f;
                bool actuallyPlaying = (bool)_filePlaying.GetValue(instance);
                while (actuallyPlaying)
                {
                    while (elasped <= sources[source].timeSamples)
                    {
                        elasped = sources[source].timeSamples - 1f;
                        yield return null;
                    }
                    sources[source].time = loopStart;
                    elasped = sources[source].timeSamples - 1f;
                    actuallyPlaying = (bool)_filePlaying.GetValue(instance);
                    yield return null;
                }
                yield break;
            }
            loopEnumerator = Loop();
            Instance.StartCoroutine(loopEnumerator);
        }
    }

    internal class SpaceWorldRoomFunction : RoomFunction
    {
        [SerializeField] internal float frequency = 1500f;
        private HashSet<PlayerManager> players = new HashSet<PlayerManager>();
        public override void OnPlayerEnter(PlayerManager player)
        {
            base.OnPlayerEnter(player);
            CoreGameManager.Instance.GetCamera(player.playerNumber).listenerTra.gameObject.GetOrAddComponent<AudioLowPassFilter>().cutoffFrequency = frequency;
            players.Add(player);
        }
        public override void OnPlayerExit(PlayerManager player)
        {
            base.OnPlayerExit(player);
            players.Remove(player);
            Destroy(CoreGameManager.Instance.GetCamera(player.playerNumber).listenerTra.GetComponent<AudioLowPassFilter>());
        }
        private void LateUpdate()
        {
            if (room.ec.PlayerTimeScale == 0f || Time.timeScale == 0f)
            {
                for (int i = 0; i < CoreGameManager.Instance.setPlayers; i++)
                {
                    if (!players.Contains(CoreGameManager.Instance.GetPlayer(i))) continue;
                    var listenerTra = CoreGameManager.Instance.GetCamera(i).listenerTra;
                    var component = listenerTra.GetComponent<AudioLowPassFilter>();
                    if (component != null) component.enabled = false;
                }
            }
            else
            {
                for (int i = 0; i < CoreGameManager.Instance.setPlayers; i++)
                {
                    if (!players.Contains(CoreGameManager.Instance.GetPlayer(i))) continue;
                    var listenerTra = CoreGameManager.Instance.GetCamera(i).listenerTra;
                    var component = listenerTra.GetComponent<AudioLowPassFilter>();
                    if (component != null) component.enabled = true;
                }
            }
        }
    }

    [Serializable, Obsolete("Unavailable in FieldTripsAPI Port", true)]
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
    [Obsolete("Unavailable in FieldTripsAPI Port", true)]
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
