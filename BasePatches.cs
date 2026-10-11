using AlexBWFieldtrips.Wanted;
using AlexBWFieldtrips.DededeeCartRacing;
using HarmonyLib;
using MTM101BaldAPI;
using System.Reflection;
using TMPro;
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
#endif
        }

        [HarmonyPatch(typeof(FieldTripEntranceRoomFunction), nameof(FieldTripEntranceRoomFunction.Initialize)), HarmonyPostfix]
        static void IsThatOneClip(ref bool ___available, ref SoundObject ___audCampIntro)
        {
            if (___available)
                ___audCampIntro = CoreGameManager.Instance.currentFieldTrip.intro;
        }
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
            _minigame = AccessTools.DeclaredField(typeof(MinigameBase), "minigame");
        [HarmonyPatch(typeof(MinigameTutorial), nameof(MinigameTutorial.Initialize)), HarmonyPostfix]
        static void ResultSoundObject(MinigameTutorial __instance)
        {
            if (__instance.gameObject.name != "MinigameTutorial_SuperWanted64DS(Clone)" && __instance.gameObject.name != "MinigameTutorial_DededeCartRacing(Clone)") return; // Hardcoding because I don't want other mods to overlap.
            LoopingSoundObject musObj = (LoopingSoundObject)_musObj.GetValue(__instance.minigameBase);
            if (string.IsNullOrWhiteSpace(__instance.music) && musObj != null)
                MusicManager.Instance.QueueFile(musObj, true);
        }
        [HarmonyPatch(typeof(FieldTripEntranceRoomFunction), "Initialize"), HarmonyPostfix]
        private static void FieldTripLines(bool ___available, ref SoundObject ___audNoPass)
        {
            if (___available)
            {
                var trip = CoreGameManager.Instance.currentFieldTrip;
                if (trip.trip.ToStringExtended() == "AlexBWWorld")
                {
                    ___audNoPass = AlexBWFieldTripsPlugin.assetMan.Get<SoundObject>("ABW/BusPass");
                }
            }
        }
    }
}
