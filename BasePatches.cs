using AlexBWFieldtrips.DededeeCartRacing;
using AlexBWFieldtrips.Wanted;
using HarmonyLib;
using MTM101BaldAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ThumblysFieldTripsAPI.Extentions;
using ThumblysFieldTripsAPI.Registers;
using TMPro;
using UnityCipher;
using UnityEngine;

namespace AlexBWFieldtrips
{
    [HarmonyPatch]
    internal class BasePatches
    {
        [HarmonyPatch(typeof(EnvironmentController), nameof(EnvironmentController.NpcTimeScale), MethodType.Getter), HarmonyPostfix]
        private static void ScaledByStudents(ref float __result)
        {
            if (Minigame_Wanted.Instance != null)
            {
                var instance = Minigame_Wanted.Instance;
                if (instance.minigameBase.DeltaTime != 0f)
                    __result = 1f;
            }
        }
        [HarmonyPatch(typeof(EnvironmentController), nameof(EnvironmentController.LightLevel), [typeof(IntVector2)]), HarmonyPostfix]
        private static void MinigameLighting(ref float __result, IntVector2 position)
        {
            if (Minigame_Wanted.Instance != null)
            {
                __result = CoreGameManager.Instance.lightMapTexture.GetPixel(position.x, position.z).grayscale;
            }
        }
        internal static MinigameName dedede;
        [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.AwardPerfectBonus)), HarmonyPostfix]
        static void NoHitBonusText(ref MinigameName ___minigame, ref TMP_Text ___bonusTmp, ref int ___perfectBonus)
        {
            if (___minigame == dedede)
                ___bonusTmp.text = string.Format(LocalizationManager.Instance.GetLocalizedText("Mgm_DededeCartRacing_Perfect"), ___perfectBonus);
        }
        [HarmonyPatch(typeof(MinigameHUD), nameof(MinigameHUD.SetScore)), HarmonyPrefix]
        private static bool TimerElasped(int points, MinigameHUD __instance, TMP_Text ___scoreTmp)
        {
            var dedede = __instance.GetComponentInParent<Minigame_CartRacing>();
            if (dedede != null)
            {
                float gameTime = dedede.ElaspedTime,
                seconds = Mathf.Floor(gameTime % 60f),
                minutes = Mathf.Floor(gameTime / 60f),
                miliseconds = (gameTime % 1) * 1000;
                var localization = AlexBWFieldTripsPlugin.LocalizationStopwatch.GetValueSafe((Language)_currentSubLang.GetValue(LocalizationManager.Instance));
                if (localization == null)
                    localization = AlexBWFieldTripsPlugin.LocalizationStopwatch[Language.English];
                ___scoreTmp.text = string.Format(localization.Item1, minutes, seconds, miliseconds); // "{0:00}:{1:00}.{2:000}"
                ___scoreTmp.enableAutoSizing = localization.Item2;
                __instance.StopAllCoroutines();
                return false;
            }
            return true;
        }
#if DEBUG
        [HarmonyPatch(typeof(PitstopGameManager), "AwakeFunction"), HarmonyPostfix]
        static void InsertFieldTrip(ref WeightedFieldTrip[] ___tierOneTrips, ref FieldTripObject ___forcedTrip)
        {
            ___forcedTrip = AlexBWFieldTripsPlugin.assetMan.Get<FieldTripObject>("FieldTrip");
            ___tierOneTrips = [new WeightedFieldTrip()
            {
                selection = AlexBWFieldTripsPlugin.assetMan.Get<FieldTripObject>("FieldTrip"),
                weight = 150
            }];
        }
#endif
        [HarmonyPatch(typeof(MinigameBase), "Pause"), HarmonyPostfix]
        private static void PauseSoundObject(bool ___paused, string ___midiSong, MinigameName ___minigame)
        {
            if (___minigame.ToStringExtended() != "SuperWanted64DS" && ___minigame.ToStringExtended() != "DededeCartRacing") return;
            if (___midiSong.Length <= 0)
            {
                AudioSource[] sources = (AudioSource[])_audioSource.GetValue(MusicManager.Instance);
                int source = (int)_currentSource.GetValue(MusicManager.Instance);
                if (___paused)
                    sources[source].Pause();
                else
                    sources[source].UnPause();
            }
        }

        private static FieldInfo
            _musObj = AccessTools.Field(typeof(MinigameBase), "musicObject"),
            _currentSource = AccessTools.Field(typeof(MusicManager), "currentSource"),
            _audioSource = AccessTools.Field(typeof(MusicManager), "fileSoure"),
            _minigame = AccessTools.DeclaredField(typeof(MinigameBase), "minigame"),
            _soundOnStart = AccessTools.DeclaredField(typeof(AudioManager), "soundOnStart"),
            _subtitleColor = AccessTools.DeclaredField(typeof(AudioManager), "subtitleColor"),
            _currentSubLang = AccessTools.DeclaredField(typeof(LocalizationManager), "currentSubLang");
        [HarmonyPatch(typeof(MinigameTutorial), nameof(MinigameTutorial.Initialize)), HarmonyPostfix]
        static void ResultSoundObject(MinigameTutorial __instance)
        {
            if (__instance.gameObject.name != "MinigameTutorial_SuperWanted64DS(Clone)" && __instance.gameObject.name != "MinigameTutorial_DededeCartRacing(Clone)") return; // Hardcoding because I don't want other mods to overlap.
            LoopingSoundObject musObj = (LoopingSoundObject)_musObj.GetValue(__instance.minigameBase);
            if (string.IsNullOrWhiteSpace(__instance.music) && musObj != null)
            {
                MusicManager.Instance.QueueFile(musObj, true);
                if (musObj == AlexBWFieldTripsPlugin.assetMan.Get<LoopingSoundObject>("TutorialMusic/CartRace"))
                    AlexBWFieldTripsPlugin.LoopMusic(MusicManager.Instance, 0.324f);
            }
        }
        [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.StopFile)), HarmonyPostfix]
        private static void StopLoop(AudioSource[] ___fileSoure) // Those are a bummer to deal with.
        {
            if (AlexBWFieldTripsPlugin.loopEnumerator != null)
            {
                AlexBWFieldTripsPlugin.Instance.StopCoroutine(AlexBWFieldTripsPlugin.loopEnumerator);
                ___fileSoure[0].time = 0f;
                ___fileSoure[1].time = 0f;
                AlexBWFieldTripsPlugin.loopEnumerator = null;
            }
        }
        [HarmonyPatch(typeof(FieldTripEntranceRoomFunction), "Initialize"), HarmonyPostfix]
        private static void FieldTripLines(bool ___available, ref SoundObject ___audNoPass, Transform ___busObjects)
        {
            if (___available)
            {
                var trip = CoreGameManager.Instance.currentFieldTrip;
                if (trip.trip.ToStringExtended() == "AlexBWWorld")
                {
                    ___audNoPass = AlexBWFieldTripsPlugin.assetMan.Get<SoundObject>("ABW/BusPass");
                    var bus = ___busObjects.Find("Bus");
                    foreach (var _meshrender in bus.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        _meshrender.transform.localPosition = new(_meshrender.transform.localPosition.x, 6.75f, _meshrender.transform.localPosition.z);
                        _meshrender.transform.localScale = Vector3.one * 15f;
                        _meshrender.material = AlexBWFieldTripsPlugin.assetMan.Get<Material>("PortalEntranceMat");
                    }
                    _soundOnStart.SetValue(bus.GetComponent<PropagatedAudioManager>(), new SoundObject[] { AlexBWFieldTripsPlugin.assetMan.Get<SoundObject>("PortalAmbience") });
                    var clicktrigger = ___busObjects.Find("TripEntranceTrigger");
                    clicktrigger.localPosition = new(bus.localPosition.x, clicktrigger.localPosition.y, clicktrigger.localPosition.z);
                    _subtitleColor.SetValue(___busObjects.Find("AnimatedTripBaldi").GetComponent<PropagatedAudioManager>(), trip.intro.color);
                }
            }
        }
        #region HIGH SCORE STUFF
        private static MethodInfo
            _SortMinigameScores = AccessTools.Method(typeof(ModdedFieldTripScoreManager), "SortMinigameScores"),
            _ShowResults = AccessTools.Method(typeof(MinigameBase), "ShowResults", [typeof(bool), typeof(int), typeof(int)]);
        [HarmonyPatch(typeof(ModdedFieldTripScoreManager), nameof(ModdedFieldTripScoreManager.GetSortedScoresForMinigame)), HarmonyPrefix]
        private static bool TimeAttackScoreGetSorted(string minigameName, ref List<FieldTripScore> __result, ModdedFieldTripScoreManager __instance)
        {
            if (minigameName == dedede.ToStringExtended())
            {
                List<CartMinigameScore> list = new List<CartMinigameScore>();
                for (int i = 0; i < __instance.tripScores.Count; i++)
                {
                    if (string.Equals(__instance.tripScores[i].minigame, minigameName))
                        list.Add((CartMinigameScore)__instance.tripScores[i]);
                }
                var castedList = new List<FieldTripScore>(list);
                _SortMinigameScores.Invoke(__instance, [castedList, false]);
                __result = castedList;
                return false;
            }
            return true;
        }
        [HarmonyPatch(typeof(ModdedFieldTripScoreManager), "SortMinigameScores"), HarmonyPrefix]
        private static bool TimeAttackScoreSort(List<FieldTripScore> scores)
        {
            if (scores.Exists(x => x is CartMinigameScore))
            {
                List<CartMinigameScore> list = new List<CartMinigameScore>();
                int count = scores.Count;
                while (list.Count < count)
                {
                    int index = 0;
                    float num = float.PositiveInfinity;
                    for (int i = 0; i < scores.Count; i++)
                    {
                        if ((float)((CartMinigameScore)scores[i]).time < num)
                        {
                            num = Convert.ToSingle(((CartMinigameScore)scores[i]).time);
                            index = i;
                        }
                    }

                    list.Add((CartMinigameScore)scores[index]);
                    scores.RemoveAt(index);
                }

                scores.Clear();
                scores.AddRange(list);
                return false;
            }
            return true;
        }
        [HarmonyPatch(typeof(ModdedFieldTripScoreManager), nameof(ModdedFieldTripScoreManager.AddScore)), HarmonyPrefix]
        private static bool DisableInvalid(string minigame, int score, string name, out int rank, ModdedFieldTripScoreManager __instance)
        {
            rank = -1;
            if (minigame == dedede.ToStringExtended())
                return false;
            return true;
        }
        [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.SetScore)), HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static bool TimeAttackScore(MinigameName ___minigame, MinigameBase __instance, ref bool ___minigameActive, ref bool ___result, string ___minigameNameKey)
        {
            if (___minigame == dedede)
            {
                float time = __instance.GetComponent<Minigame_CartRacing>().ElaspedTime;
                ModdedFieldTripScoreManager manager = ModdedFieldTripScoreManager.Instance;
                int rank = -1;
                bool flag = false;
                CartMinigameScore tripScore = new CartMinigameScore(___minigame.ToStringExtended(), time, PlayerFileManager.Instance.fileName);
                manager.tripScores.Add(tripScore);
                List<CartMinigameScore> list = new List<CartMinigameScore>(manager.GetSortedScoresForMinigame(___minigame.ToStringExtended(), false).Cast<CartMinigameScore>());
                for (int i = 0; i < list.Count && i < 5; i++)
                {
                    if (list[i] == tripScore)
                    {
                        rank = i;
                        flag = true;
                        break;
                    }
                }

                for (int i = 5; i < list.Count; i++)
                    manager.tripScores.Remove(list[i]);

                if (flag)
                    manager.Save();
                else
                    manager.tripScores.Remove(tripScore);

                if (rank >= 0)
                    HighlightManager.Instance.Highlight("steam_trophy", string.Format(LocalizationManager.Instance.GetLocalizedText("Steam_Highlight_MinigameHighScore"), LocalizationManager.Instance.GetLocalizedText(___minigameNameKey)), string.Format(Singleton<LocalizationManager>.Instance.GetLocalizedText("Steam_Highlight_MinigameHighScore_Desc"), (rank + 1).ToString(), time, LocalizationManager.Instance.GetLocalizedText(___minigameNameKey)), 3u, 0f - __instance.RealTime, __instance.RealTime + 5f, TimelineEventClipPriority.Featured);

                ___minigameActive = false;
                _ShowResults.Invoke(__instance, [rank >= 0, rank, 0]);
                ___result = rank >= 0;
                MusicManager.Instance.SetFileLoop(false);
                MusicManager.Instance.SetLoop(false);
                return false;
            }
            return true;
        }
        [HarmonyPatch(typeof(ModdedFieldTripScoreManager), "Save"), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void PreSaveScore(ModdedFieldTripScoreManager __instance, out List<CartMinigameScore> __state)
        {
            List<CartMinigameScore> cartScores = __instance.tripScores.Where(x => x is CartMinigameScore).Select(x => x as CartMinigameScore).ToList();
            __instance.tripScores.RemoveAll(x => x is CartMinigameScore);

            if (!Directory.Exists(Application.persistentDataPath + "/Modded"))
                Directory.CreateDirectory(Application.persistentDataPath + "/Modded");

            string path = Application.persistentDataPath + "/Modded/CoastingCartRacingFastestTimes.hgh";
            if (File.Exists(path))
                File.Delete(path);

            CartScoresData moddedFieldTripData = new CartScoresData();
            moddedFieldTripData.cartScores = cartScores;
            File.WriteAllText(path, RijndaelEncryption.Encrypt(JsonConvert.SerializeObject((object)moddedFieldTripData), "99"));
            __state = cartScores;
        }
        [HarmonyPatch(typeof(ModdedFieldTripScoreManager), "Save"), HarmonyPostfix, HarmonyPriority(Priority.First)]
        private static void PostSaveScore(ModdedFieldTripScoreManager __instance, List<CartMinigameScore> __state)
        {
            __instance.tripScores.AddRange(__state);
        }
        [HarmonyPatch(typeof(ModdedFieldTripScoreManager), "Load"), HarmonyPostfix]
        private static void LoadScore(ModdedFieldTripScoreManager __instance)
        {
            string path = Application.persistentDataPath + "/Modded/CoastingCartRacingFastestTimes.hgh";
            if (File.Exists(path))
            {
                bool flag = true;
                CartScoresData moddedFieldTripData = new CartScoresData();
                try
                {
                    moddedFieldTripData = JsonConvert.DeserializeObject<CartScoresData>(RijndaelEncryption.Decrypt(File.ReadAllText(path), "99"));
                }
                catch
                {
                    flag = false;
                }

                if (flag)
                {
                    __instance.tripScores.AddRange(moddedFieldTripData.cartScores);
                }
                else
                {
                    Debug.LogWarning("Coasting Cart Racing scores file is corrupted. Setting defaults.");
                    PreSaveScore(__instance, out _);
                }
            }
            else
            {
                Debug.Log("Coasting Cart Racing scores file do not exist. Setting defaults.");
                PreSaveScore(__instance, out _);
            }
        }
        [HarmonyPatch(typeof(MinigameBase), "ShowResults", [typeof(bool), typeof(int), typeof(int)]), HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static bool ShowProperResults(bool result, int rank, int score, bool ___scoringMode, TMP_Text ___resultTmp, MinigameName ___minigame, GameObject ___resultsObject, MinigameBase __instance)
        {
            if (___minigame == dedede && ___scoringMode)
            {
                ___resultsObject.SetActive(true);
                float gameTime = __instance.GetComponent<Minigame_CartRacing>().ElaspedTime,
                seconds = Mathf.Floor(gameTime % 60f),
                minutes = Mathf.Floor(gameTime / 60f),
                miliseconds = (gameTime % 1) * 1000;
                var localization = AlexBWFieldTripsPlugin.LocalizationStopwatch.GetValueSafe((Language)_currentSubLang.GetValue(LocalizationManager.Instance));
                if (localization == null)
                    localization = AlexBWFieldTripsPlugin.LocalizationStopwatch[Language.English];
                if (result)
                    ___resultTmp.text = string.Format(LocalizationManager.Instance.GetLocalizedText("Mgm_DededeCartRacing_FinalScore"), string.Format(localization.Item1, minutes, seconds, miliseconds)) + string.Format(LocalizationManager.Instance.GetLocalizedText("Mgm_HighScore"), rank + 1);
                else
                    ___resultTmp.text = string.Format(LocalizationManager.Instance.GetLocalizedText("Mgm_DededeCartRacing_FinalScore"), string.Format(localization.Item1, minutes, seconds, miliseconds)) + LocalizationManager.Instance.GetLocalizedText("Mgm_LoseEndless");
                return false;
            }
            return true;
        }
        [HarmonyPatch(typeof(ModdedMinigameHighScoreTable), "UpdateScores"), HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static bool TimeAttackScoreUpdate(MinigameName minigame, string nameKey, TMP_Text ___titleTmp, TMP_Text[] ___nameTmp, TMP_Text[] ___valueTmp)
        {
            if (minigame == dedede)
            {
                string enumName = EnumExtensions.GetExtendedName<MinigameName>((int)minigame);

                ___titleTmp.text = LocalizationManager.Instance.GetLocalizedText(nameKey) + " " + LocalizationManager.Instance.GetLocalizedText("Mgm_Leaderboard");
                List<CartMinigameScore> sortedScores = ModdedFieldTripScoreManager.Instance.GetSortedScoresForMinigame(enumName, false).Cast<CartMinigameScore>().ToList();
                var localization = AlexBWFieldTripsPlugin.LocalizationStopwatch.GetValueSafe((Language)_currentSubLang.GetValue(LocalizationManager.Instance));
                if (localization == null)
                    localization = AlexBWFieldTripsPlugin.LocalizationStopwatch[Language.English];
                for (int i = 0; i < ___nameTmp.Length; i++)
                {
                    if (sortedScores != null && i < sortedScores.Count)
                    {
                        ___nameTmp[i].text = sortedScores[i].name;
                        float gameTime = Convert.ToSingle(sortedScores[i].time),
                        seconds = Mathf.Floor(gameTime % 60f),
                        minutes = Mathf.Floor(gameTime / 60f),
                        miliseconds = (gameTime % 1) * 1000;
                        ___valueTmp[i].text = string.Format(localization.Item1, minutes, seconds, miliseconds);
                    }
                    else
                    {
                        ___nameTmp[i].text = LocalizationManager.Instance.GetLocalizedText("Name_Baldi");
                        ___valueTmp[i].text = string.Format(localization.Item1, "99", "99", "999");
                    }
                    ___valueTmp[i].rectTransform.sizeDelta = new Vector2(132f, ___valueTmp[i].rectTransform.sizeDelta.y);
                    ___valueTmp[i].enableAutoSizing = localization.Item2;
                }
                return false;
            }
            return true;
        }
        #endregion
    }
}
