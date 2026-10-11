using AlexBWFieldtrips.Wanted;
using AlexBWFieldtrips.DededeeCartRacing;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Registers;
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;

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
                ___scoreTmp.text = string.Format("{0:00}:{1:00}.{2:000}", minutes, seconds, miliseconds);
                __instance.StopAllCoroutines();
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(PitstopGameManager), "AwakeFunction"), HarmonyPostfix]
        static void InsertFieldTrip(ref WeightedFieldTrip[] ___tierOneTrips, ref FieldTripObject ___forcedTrip)
        {
#if DEBUG
            ___forcedTrip = AlexBWFieldTripsPlugin.assetMan.Get<FieldTripObject>("FieldTrip");
            ___tierOneTrips = [new WeightedFieldTrip()
            {
                selection = AlexBWFieldTripsPlugin.assetMan.Get<FieldTripObject>("FieldTrip"),
                weight = 150
            }];
#else
            ___tierOneTrips = ___tierOneTrips.AddToArray(new WeightedFieldTrip()
            {
                selection = AlexBWFieldTripsPlugin.assetMan.Get<FieldTripObject>("FieldTrip"),
                weight = 150
            });
#endif
        }

        [HarmonyPatch(typeof(FieldTripEntranceRoomFunction), nameof(FieldTripEntranceRoomFunction.Initialize)), HarmonyPostfix]
        static void IsThatOneClip(ref bool ___available, ref SoundObject ___audCampIntro)
        {
            if (___available)
                ___audCampIntro = CoreGameManager.Instance.currentFieldTrip.intro;
        }
        [HarmonyPatch(typeof(MinigameBase), "Pause"), HarmonyPostfix]
        private static void PauseSoundObject(bool ___paused, string ___midiSong)
        {
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
            _audioSource = AccessTools.Field(typeof(MusicManager), "fileSoure");
        [HarmonyPatch(typeof(MinigameTutorial), nameof(MinigameTutorial.Initialize)), HarmonyPostfix]
        static void ResultSoundObject(MinigameTutorial __instance)
        {
            LoopingSoundObject musObj = (LoopingSoundObject)_musObj.GetValue(__instance.minigameBase);
            if (string.IsNullOrWhiteSpace(__instance.music) && musObj != null)
                MusicManager.Instance.QueueFile(musObj, true);
        }

        private static bool ChangedNow = false;
        //[HarmonyPatch(typeof(ModdedHighscoreManager), nameof(ModdedHighscoreManager.Load)), HarmonyPrefix]
        internal static void NotImplementedWhyDidYou() // This had to fix the high score crash...
        {
            if (ChangedNow) return;
            Tuple<int[,], string[,]> prevhighscores = new Tuple<int[,], string[,]>(ModdedHighscoreManager.tripScores, ModdedHighscoreManager.tripNames);
            var count = 16 + 256 + (EnumExtensions.GetValues<MinigameName>().Length - 6);
            ModdedHighscoreManager.tripScores = new int[count, 5];
            ModdedHighscoreManager.tripNames = new string[count, 5];
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    ModdedHighscoreManager.tripScores[j, i] = 0;
                    ModdedHighscoreManager.tripNames[j, i] = "Baldi";
                }
            }
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    ModdedHighscoreManager.tripScores[j, i] = prevhighscores.Item1[j, i];
                    ModdedHighscoreManager.tripNames[j, i] = prevhighscores.Item2[j, i];
                }
            }
            HighScoreManager.Instance.tripScores = ModdedHighscoreManager.tripScores;
            HighScoreManager.Instance.tripNames = ModdedHighscoreManager.tripNames;
            ChangedNow = true;
        }
        [HarmonyPatch(typeof(HighScoreManager), "AddTripScore"), HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static bool LastResort(MinigameName minigame) => minigame <= MinigameName.Minigame6;
    }
}
