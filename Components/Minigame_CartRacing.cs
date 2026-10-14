using HarmonyLib;
using MTM101BaldAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TarodevGhost;
using UnityCipher;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AlexBWFieldtrips.DededeeCartRacing;
public class Minigame_CartRacing : Minigame
{
    private static FieldInfo
        _cameraStack = AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_Cameras"),
        _winKey = AccessTools.DeclaredField(typeof(MinigameBase), "winKey"),
        _loseKey = AccessTools.DeclaredField(typeof(MinigameBase), "loseKey");

    [SerializeField] internal MinigameHUD minigameHud;
    [SerializeField] internal AnalogInputData movementAnalogData;

    internal static readonly Dictionary<Character, RacerData> possibleRacers = new Dictionary<Character, RacerData>();
    [SerializeField] internal CartRacerController cartPre;
    [SerializeField] internal CartGhostController ghostPre; 

    [SerializeField] internal List<GameObject> 
        stages = new List<GameObject>(),
        possibleStage1BGs = new List<GameObject>(),
        possibleStage2BGs = new List<GameObject>(),
        possibleStage3BGs = new List<GameObject>();
    private GameObject activeBG;
    private int curStage = 0;
    private bool raceFinished;
    private Camera camera;
    [SerializeField] internal RenderTexture texToUse;
    private Vector3 camPos;
    internal Vector3 CameraPosition => camPos;
    internal Rect stageBounds { get; private set; }
    [SerializeField] internal SoundObject audFinished;
    [SerializeField] internal CartBooster boostPre;
    [SerializeField] internal CartHurter hurtPre;
    [SerializeField] internal List<Sprite> boosterSprs = new List<Sprite>();
    [SerializeField] internal Sprite hurtSpr;
    [SerializeField] internal RectTransform trackerTransform, trackerPre;

    private const int RACERS_COUNT = 7;
    internal float ElaspedTime { get; private set; }
    public static void AddNewRacer(NPC character, RacerData data) => possibleRacers.Add(character.Character, data);
    internal int GetPlacement(CartRacerController racer)
    {
        var racersNonHash = CartRacerController.Racers.ToList();
        float[] placementPos = new float[racersNonHash.Count];
        foreach (var racerInstance in racersNonHash)
        {
            int
                racerIndex = racersNonHash.IndexOf(racerInstance),
                stageIndex = stages.IndexOf(racerInstance.transform.parent.gameObject);
            float posX = racerInstance.transform.localPosition.x + (stageIndex * stageBounds.xMax);
            placementPos[racerIndex] = posX;
        }
        float[] placementPre = new float[placementPos.Length];
        Array.Copy(placementPos, placementPre, placementPos.Length);
        Array.Sort(placementPos);
        placementPos = placementPos.Reverse().ToArray();
        int[] racersPos = new int[racersNonHash.Count];
        for (int i = 0; i < racersNonHash.Count; i++)
            racersPos[i] = Array.IndexOf(placementPos, placementPre[i]);
#if DEBUG
        if (raceFinished && racer.Player)
        {
            foreach (var i in racersPos)
                UnityEngine.Debug.Log($"Placement: {i}");
        }
#endif
        return racersPos[racersNonHash.IndexOf(racer)];
    }

    [Header("Ghost Stuff")]
    internal bool ShowGhosts = AlexBWFieldTripsPlugin.CartTimeTrialGhosts.Value;
    private GhostRacer[] ghostracers = new GhostRacer[0];
    private CartReplaySystem _system;
    [SerializeField, Range(1, 10)] private int _framesToNCapture = 2;
    private float SmoothTime { get; set; }
    internal float SmoothDeltaTime
    {
        get
        {
            if (minigameBase.DeltaTime == 0f || GlobalCam.Instance.TransitionActive)
                return 0f;

            return Time.smoothDeltaTime;
        }
    }
    internal void StopRecordingGhost(bool save)
    {
        _system.FinishRun();
        if (save)
        {
            bool useDateTime = false;
            _system.GetRun(RecordingType.Last, out var rundata);
            var data = new CartMinigameGhostRacerData();
            data.data = rundata.Serialize();
            var FileName = $"CartRaceGhost_{PlayerFileManager.Instance.fileName}_{(useDateTime ? DateTime.Now.ToString("yyyy-MM-dd_HH-mm-sss") : "Latest")}";
            data.Image = CartRacerController.PlayerInstance.render.sprite.texture.GetRawTextureData();

            var path = Application.persistentDataPath + $"/Modded/{AlexBWFieldTripsPlugin.PLUGIN_GUID}";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            path += $"/{FileName}.ttghost";
            if (File.Exists(path))
                File.Delete(path);
            File.WriteAllText(path, RijndaelEncryption.Encrypt(JsonUtility.ToJson(data), "RACERGHOSTDATA"));
        }
    }

    public override void Initialize(MinigameBase minigameBase, bool scoringMode)
    {
        base.Initialize(minigameBase, scoringMode);
        if (!scoringMode)
            minigameHud.HideScore();
        else
            minigameHud.SetScore(0);
        minigameBase.SetFinalRound(stages.Count);
        trackerTransform.sizeDelta = new(55f * stages.Count, 15f);
        stageBounds = new Rect(15f, -400f, 1380f + 15f, 800f);
        camera = new GameObject("CartCamera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
        var stackdata = camera.GetComponent<UniversalAdditionalCameraData>();
        stackdata.renderShadows = false;
        var gameCam = CoreGameManager.Instance.GetCamera(0);
        _cameraStack.SetValue(stackdata, new List<Camera>() { gameCam.canvasCam, gameCam.mapCam, GlobalCam.Instance.RenderTexCam });
        camera.orthographic = true;
        camera.orthographicSize = 25f;
        camera.depth = 1f;
        camera.cullingMask = LayerMask.GetMask("Overlay");
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.targetTexture = texToUse;
        camera.nearClipPlane = 0f;
        camera.cameraType = CameraType.Preview;
        ChangeStage(0, false);
        if (scoringMode && ShowGhosts)
        {
            _system = new CartReplaySystem(this);
            var path = Application.persistentDataPath + $"/Modded/{AlexBWFieldTripsPlugin.PLUGIN_GUID}";
            if (Directory.Exists(Path.Combine(path)))
            {
                var files = Directory.GetFiles(path, "*.ttghost");
                if (files.Length > 0)
                {
                    ghostracers = new GhostRacer[files.Length];
                    for (int i = 0;  i < files.Length; i++)
                    {
                        try
                        {
                            var data = JsonUtility.FromJson<CartMinigameGhostRacerData>(RijndaelEncryption.Decrypt(File.ReadAllText(files[i]), "RACERGHOSTDATA"));
                            Texture2D image = new Texture2D(200, 200, cartPre.render.sprite.texture.format, false);
                            image.LoadRawTextureData(data.Image);
                            image.Apply();
                            var racer = new GhostRacer(Sprite.Create(image, new Rect(0f, 0f, image.width, image.height), Vector2.one / 2f, 26f, 0u, SpriteMeshType.FullRect), new Recording(data.data));
                            ghostracers[i] = racer;
                        }
                        catch (Exception ex)
                        {
                            ghostracers = new GhostRacer[0];
                            throw ex;
                        }
                    }
                }
            }
        }
    }
    private void OnDestroy()
    {
        if (ShowGhosts && scoringMode)
        {
            StopRecordingGhost(false);
            for (int i = 0; i < ghostracers.Length; i++)
            {
                var data = ghostracers[i];
                DestroyImmediate(data.Image.texture);
                DestroyImmediate(data.Image);
            }
        }
        Destroy(camera.gameObject);
    }
    public override void Prepare()
    {
        base.Prepare();
        AlexBWFieldTripsPlugin.LoopMusic(MusicManager.Instance, 82.087f);
    }

    public override void VirtualReset()
    {
        base.VirtualReset();
        ElaspedTime = 0f;
        SmoothTime = 0f;
        minigameHud.Reinitialize();
        GameObject.FindObjectsOfType<CartObject>(true).DoIf(x => stages.Contains(x.transform.parent?.gameObject), x => Destroy(x.gameObject));
        CartObject.ToCallVirtually.Clear();
        while (CartRacerController.Racers.Count > 0)
        {
            var racer = CartRacerController.Racers.First();
            CartRacerController.Racers.Remove(racer); // While loops will not let `OnDestroy()` be called until the loop ends.
            Destroy(racer.gameObject);
        }
        while (CartGhostController.Ghosts.Count > 0)
        {
            var ghost = CartGhostController.Ghosts.First();
            CartGhostController.Ghosts.Remove(ghost);
            Destroy(ghost.gameObject);
        }
        curStage = 0;
        ChangeStage(0, false);
        raceFinished = false;
        camera.transform.position = Vector3.zero;
    }

    public override void VirtualStart()
    {
        base.VirtualStart();
        AssignRacers(scoringMode ? 0 : RACERS_COUNT);
        camera.transform.position = CartRacerController.PlayerInstance.transform.position + (Vector3.right * (15f + stageBounds.xMin));
        camPos = camera.transform.position;
        foreach (var stage in stages)
        {
            StartCoroutine(BoosterSpawner(stage));
            StartCoroutine(HurterSpawner(stage));
        }
    }

    public override void VirtualUpdate()
    {
        base.VirtualUpdate();
        if (!raceFinished)
            ElaspedTime += minigameBase.DeltaTime;
        minigameHud.SetScore(0);
        var player = CartRacerController.PlayerInstance;
        if (player != null && player.transform.localPosition.x >= stageBounds.xMin && player.transform.localPosition.x < (stageBounds.width - 150f))
        {
            camPos.y = player.transform.position.y;
            camPos.x = player.transform.position.x + 15f;
        }
        camPos.y = Mathf.Clamp(camPos.y, stageBounds.yMin + (camera.orthographicSize / 2f), stageBounds.yMax - (camera.orthographicSize / 2f));
        camera.transform.position = camPos;
        foreach (var racer in CartRacerController.Racers)
            racer.VirtualUpdate();
        SmoothTime += SmoothDeltaTime;
        foreach (var ghost in CartGhostController.Ghosts)
        {
            var _replay = ghost.replayData;
            Pose pose = _replay.EvaluatePoint(SmoothTime);
            ghost.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (SmoothTime > _replay.Duration)
                ghost.Dissolve();
            else if ((pose.position.x + 5f) >= (stageBounds.xMax - 150f))
            {
                if (!ghost.transitioningToNext)
                {
                    ghost.transitioningToNext = true;
                    var currentStage = stages.IndexOf(ghost.transform.parent.gameObject);
                    if (currentStage == -1)
                    {
                        MTM101BaldiDevAPI.CauseCrash(AlexBWFieldTripsPlugin.Instance.Info, new Exception("The ghost is outside the stages!"));
                        return;
                    }
                    var nextStage = stages[Mathf.Min(currentStage + 1, stages.Count - 1)];
                    if ((currentStage + 1) > (stages.Count - 1))
                        return;
                    ghost.transform.SetParent(nextStage.transform, false);
                }
            }
            else if (ghost.transitioningToNext && stageBounds.Contains(pose.position))
                ghost.transitioningToNext = false;
        }
        for (int i = 0; i < CartObject.ToCallVirtually.Count; i++)
        {
            if (i >= CartObject.ToCallVirtually.Count) break;
            CartObject.ToCallVirtually[i]?.VirtualUpdate();
        }
        foreach (var track in trackers)
        {
            int stageIndex = stages.IndexOf(track.Key.parent.gameObject);
            var pos = track.Value.anchoredPosition;
            float max = 27.5f * stages.Count;
            pos.x = ((track.Key.transform.localPosition.x / 27.5f) + (27.5f * stageIndex)) - (max - (27.5f * stageIndex));
            track.Value.anchoredPosition = pos;
        }
    }
    private void LateUpdate()
    {
        foreach (var racer in CartRacerController.Racers)
            racer.VirtualLateUpdate();
    }

    private void FixedUpdate()
    {
        foreach (var racer in CartRacerController.Racers)
            racer.VirtualFixedUpdate();
    }

    private IEnumerator BoosterSpawner(GameObject stage)
    {
        while (!raceFinished)
        {
            float timer = UnityEngine.Random.Range(0.05f, 0.2f);
            while (timer > 0f && !raceFinished)
            {
                timer -= minigameBase.DeltaTime;
                yield return null;
            }
            if (raceFinished)
                yield break;
            for (int i = 0; i < UnityEngine.Random.Range(1, 10); i++)
                CreateBooster(stage);
            yield return null;
        }
    }
    private IEnumerator HurterSpawner(GameObject stage)
    {
        while (!raceFinished)
        {
            float timer = UnityEngine.Random.Range(0.075f, 0.2f);
            while (timer > 0f && !raceFinished)
            {
                timer -= minigameBase.DeltaTime;
                yield return null;
            }
            if (raceFinished)
                yield break;
            for (int i = 0; i < UnityEngine.Random.Range(1, 3); i++)
                CreateHurter(stage);
            yield return null;
        }
    }

    internal void CartMadeItToFinish(CartRacerController cart)
    {
        if (raceFinished) return;
        var currentStage = stages.IndexOf(cart.transform.parent.gameObject);
        if (currentStage == -1)
        {
            MTM101BaldiDevAPI.CauseCrash(AlexBWFieldTripsPlugin.Instance.Info, new Exception("The cart is outside the stages!"));
            return;
        }
        var nextStage = stages[Mathf.Min(currentStage + 1, stages.Count - 1)];
        if ((currentStage + 1) > (stages.Count - 1))
        {
            End(cart.Player);
            return;
        }
        else if (cart.Player)
            ChangeStage(currentStage + 1);
        var yPos = cart.transform.localPosition.y;
        cart.transform.SetParent(nextStage.transform, false);
        cart.transform.localPosition = new(0f, yPos, 0f);
        if (cart == CartRacerController.PlayerInstance)
        {
            camera.transform.position = cart.transform.position + (Vector3.right * (15f + stageBounds.xMin));
            camPos = camera.transform.position;
        }
    }
    private void End(bool win)
    {
        raceFinished = true;
        minigameBase.AudioManager.PlaySingle(audFinished);
        if (ShowGhosts && scoringMode)
            StopRecordingGhost(true);
        MusicManager.Instance.StopFile();
        MusicManager.Instance.StopMidi();
        int placement = GetPlacement(CartRacerController.PlayerInstance);
        string placementText = placement switch
        {
            0 => "1st",
            1 => "2nd",
            2 => "3rd",
            _ => $"{placement + 1}th"
        };
        _winKey.SetValue(minigameBase, string.Format(LocalizationManager.Instance.GetLocalizedText("Mgm_DededeCartRacing_Win"), placementText));
        _loseKey.SetValue(minigameBase, string.Format(LocalizationManager.Instance.GetLocalizedText("Mgm_DededeCartRacing_Lose"), placementText));
        IEnumerator Ending()
        {
            float timer = 3f;
            while (timer > 0f)
            {
                timer -= minigameBase.DeltaTime;
                yield return null;
            }
            if (scoringMode)
                minigameBase.SetScore(0);
            else if (win)
            {
                if (perfect && !scoringMode)
                    minigameBase.AwardPerfectBonus();
                minigameBase.Win();
                yield break;
            }
            else if (placement <= (Mathf.RoundToInt(CartRacerController.Racers.Count / 2f) - 1))
                minigameBase.Win();
            else
                minigameBase.Lose();
            yield break;
        }
        StartCoroutine(Ending());
    }
    private void ChangeStage(int stage, bool transition = true)
    {
        if (transition)
            GlobalCam.Instance.Transition(UiTransition.Dither, 0.01666667f);
        curStage = stage;
        for (int i = 0; i < stages.Count; i++)
            stages[i].gameObject.SetActive(i == stage); //transform.localPosition = (i == stage) ? Vector3.zero : (Vector3.up * ((stageBounds.height * 3f) * (i + 1)));
        ChangeBG(stage);
    }
    private void ChangeBG(int stage = 0)
    {
        if (activeBG != null)
            activeBG.SetActive(false);
        List<GameObject> stageBGs = stage switch
        {
            0 => possibleStage1BGs,
            1 => possibleStage2BGs,
            2 => possibleStage3BGs,
            _ => null,
        };
        if (stageBGs == null) return;
        activeBG = stageBGs[Mathf.RoundToInt(UnityEngine.Random.Range(0f, stageBGs.Count - 1f))];
        activeBG.SetActive(true);
    }

    private void AssignRacers(int amount = RACERS_COUNT)
    {
        int hardAmount = Mathf.RoundToInt(amount / 4f);
        int halfAmount = Mathf.RoundToInt(amount / 2f);
        List<RacerData> datas = new(possibleRacers.Values);
        datas.RemoveAll(x => x.Difficulty == RacerDifficulty.NotParticipating);
        datas.Shuffle();
        float halfplacement = 0f, placement = 10f;
        for (int i = 0; i < amount; i++)
        {
            if (i == halfAmount)
            {
                halfplacement = placement;
                placement += 10f;
            }
            if (i < hardAmount)
            {
                foreach (RacerData item in datas)
                {
                    if (item.Difficulty != RacerDifficulty.Hard) continue;
                    Instantiate(cartPre, stages[0].transform, false).Initialize(this, item, initialplacement: placement);
                    datas.Remove(item);
                    break;
                }
                placement += 10f;
                continue;
            }
            foreach (RacerData item in datas)
            {
                if (item.Difficulty == RacerDifficulty.Hard) continue;
                Instantiate(cartPre, stages[0].transform, false).Initialize(this, item, initialplacement: placement);
                datas.Remove(item);
                break;
            }
            placement += 10f;
        }
        var player = Instantiate(cartPre, stages[0].transform, false);
        player.Initialize(this, possibleRacers[Character.Null], true, halfplacement);
        if (ShowGhosts && scoringMode)
        {
            _system.StartRun(player.transform, _framesToNCapture, float.PositiveInfinity);
            for (int i = 0; i < ghostracers.Length; i++)
                Instantiate(ghostPre, stages[0].transform, false).Initialize(this, ghostracers[i]);
        }
    }
    private void CreateBooster(GameObject stage)
    {
        var booster = Instantiate(boostPre, stage.transform, false);
        while (booster.transform.localPosition == Vector3.zero || CheckBounds(booster.transform))
            booster.transform.localPosition = new Vector2(UnityEngine.Random.Range(stageBounds.xMin * 5f, stageBounds.xMax), UnityEngine.Random.Range(stageBounds.yMin, stageBounds.yMax));
        var speed = UnityEngine.Random.Range(10f, 20f);
        booster.Initialize(this, boosterSprs[UnityEngine.Random.Range(0, boosterSprs.Count)], speed);
        if (UnityEngine.Random.Range(0, 10) < 3)
        {
            Vector2 otherPos = booster.transform.localPosition;
            booster = Instantiate(boostPre, stage.transform, false);
            booster.transform.localPosition = otherPos + ((UnityEngine.Random.Range(0, 2) < 1 ? Vector2.down : Vector2.up) * 2.5f);
            booster.Initialize(this, boosterSprs[UnityEngine.Random.Range(0, boosterSprs.Count)], speed);
        }
    }
    private void CreateHurter(GameObject stage)
    {
        var hurter = Instantiate(hurtPre, stage.transform, false);
        while (hurter.transform.localPosition == Vector3.zero || CheckBounds(hurter.transform))
            hurter.transform.localPosition = new Vector2(UnityEngine.Random.Range(stageBounds.xMin * 5f, stageBounds.xMax), UnityEngine.Random.Range(stageBounds.yMin, stageBounds.yMax));
        hurter.Initialize(this, hurtSpr, UnityEngine.Random.Range(10f, 20f));
    }
    private bool CheckBounds(Transform cartobject)
    {
        foreach (var racer in CartRacerController.Racers)
        {
            if (racer.transform.parent != cartobject.parent) continue;
            if (racer.CamBounds.Contains(cartobject.localPosition))
                return true;
        }
        return false;
    }

    internal void PerfectFailure() => perfect = false;

    private Dictionary<Transform, RectTransform> trackers = new Dictionary<Transform, RectTransform>();
    internal void TrackController(CartRacerController controller, Color color)
    {
        var track = Instantiate(trackerPre, trackerTransform);
        track.GetComponent<RawImage>().color = color;
        track.gameObject.SetActive(true);
        if (controller.Player)
        {
            track.sizeDelta += Vector2.one * 3.5f;
            track.SetAsFirstSibling();
        }
        trackers.Add(controller.transform, track);
    }
    internal void TrackController(CartGhostController controller)
    {
        var track = Instantiate(trackerPre, trackerTransform);
        track.GetComponent<RawImage>().color = new Color(1f, 1f, 1f, 0.5f);
        track.gameObject.SetActive(true);
        trackers.Add(controller.transform, track);
    }
    internal void RemoveTracker(Transform controller)
    {
        Destroy(trackers[controller.transform].gameObject);
        trackers.Remove(controller.transform);
    }
}

public enum RacerDifficulty
{
    NotParticipating = 0,
    Easy,
    Normal,
    Hard
}

internal class CartRacerController : MonoBehaviour
{
    private const float
        REG_SPEED = 25f,
        BOOST_SPEED = 10f,
        SLOW_SPEED = 5f;
    public static HashSet<CartRacerController> Racers = new HashSet<CartRacerController>();
    [SerializeField] internal SpriteRenderer render;
    [SerializeField] internal LineRenderer liner;
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal SoundObject boost, boostDouble, boostTriple, hurted;
    private Minigame_CartRacing minigame;
    private Vector2 _absoluteVector, _deltaVector;
    private Vector2 _movement;
    private bool isPlayer = false, transitioningToNext = false;
    private int boostingThisFrame = 0;
    public bool Player => isPlayer;
    public static CartRacerController PlayerInstance { get; private set; }
    private float speed = REG_SPEED;

    [Header("Bot Stuff")]
    private RacerDifficulty curDifficulty;
    private Vector2 destination;
    private List<Vector2> destinationPoints = new List<Vector2>();
    private bool rubber;
    private float moveCool;
    private Vector2 FlatPosition => new Vector2(transform.localPosition.x, transform.localPosition.y);
    private Rect camBounds = new Rect(-45f, -150f, 250f, 300f);
    internal Rect CamBounds => camBounds;

    public void Initialize(Minigame_CartRacing minigame, RacerData data, bool isPlayer = false, float initialplacement = 0f)
    {
        this.minigame = minigame;
        render.sprite = data.Image;
        curDifficulty = data.Difficulty;
        this.isPlayer = isPlayer;
        _movement = new Vector2(0f, initialplacement);
        transform.localPosition = _movement;
        if (isPlayer)
        {
            gameObject.layer = LayerMask.NameToLayer("Player");
            gameObject.tag = "Player";
            if (PlayerInstance == null)
            {
                PlayerInstance = this;
                gameObject.AddComponent<AudioListener>();
            }
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer("NPCs");
            gameObject.tag = "NPC";
        }
        Racers.Add(this);
        var color = data.Color;
        liner.startColor = color - new Color(0f, 0f, 0f, 0.75f);
        liner.endColor = color;
        minigame.TrackController(this, color);
    }
    
    private void UpdateLineRenderer(float absoluteVector)
    {
        Vector3 linePos = liner.GetPosition(2);
        liner.SetPosition(2, Vector3.MoveTowards(linePos, (Vector3.right * linePos.x) + (Vector3.down * (10f * absoluteVector)), speed * minigame.minigameBase.DeltaTime));
        linePos = liner.GetPosition(1);
        liner.SetPosition(1, Vector3.MoveTowards(linePos, (Vector3.right * linePos.x) + (Vector3.down * (20f * absoluteVector)), speed * minigame.minigameBase.DeltaTime));
        linePos = liner.GetPosition(0);
        liner.SetPosition(0, Vector3.MoveTowards(linePos, (Vector3.right * linePos.x) + (Vector3.down * (40f * absoluteVector)), speed * minigame.minigameBase.DeltaTime));
    }

    private bool pausedAudMans = false;
    internal void VirtualLateUpdate()
    {
        camBounds.x = -45f + transform.localPosition.x;
        camBounds.y = -150f + transform.localPosition.y;
        if (!(minigame.minigameBase.DeltaTime > 0f))
        {
            if (!pausedAudMans)
            {
                pausedAudMans = true;
                foreach (AudioManager audMan in GetComponents<AudioManager>())
                    audMan.Pause(true);
            }
        }
        else if (pausedAudMans)
        {
            pausedAudMans = false;
            foreach (AudioManager audMan in GetComponents<AudioManager>())
                audMan.Pause(false);
        }
    }

    internal void VirtualUpdate()
    {
        boostingThisFrame = 0;
        if (!rubber)
            speed = Mathf.MoveTowards(speed, REG_SPEED, speed * minigame.minigameBase.DeltaTime);
        _movement = transform.localPosition;
        _movement.x += (speed * minigame.minigameBase.DeltaTime);
        if (isPlayer)
        {
            InputManager.Instance.GetAnalogInput(minigame.movementAnalogData, out _absoluteVector, out _deltaVector, 0.1f);
            _deltaVector.x = 0f;
            _deltaVector.y = 0f;

            if (transform.localPosition.x >= minigame.stageBounds.xMin && transform.localPosition.x < (minigame.stageBounds.width - 150f))
                _movement.y += _absoluteVector.y * (speed * minigame.minigameBase.DeltaTime);
            if (!minigame.stageBounds.Contains(_movement))
                _movement.y = Mathf.Clamp(_movement.y, minigame.stageBounds.yMin, minigame.stageBounds.yMax);
            transform.localPosition = _movement;
            StateUpdate();
            return;
        }
        BotUpdate();
        if (!minigame.stageBounds.Contains(_movement))
            _movement.y = Mathf.Clamp(_movement.y, minigame.stageBounds.yMin, minigame.stageBounds.yMax);
        transform.localPosition = _movement;
        StateUpdate();
    }

    private void BotUpdate()
    {
        if (!(minigame.minigameBase.DeltaTime > 0f)) return;
        if (!minigame.stageBounds.Contains(_movement)) return;
        moveCool -= minigame.minigameBase.DeltaTime;

        if (destinationPoints.Count > 0)
        {
            MoveTowardsDestination();
        }
        else
        {
            DestinationEmpty();
        }
        if (moveCool <= 0f)
            moveCool += 0.7f;
    }

    public void MoveTowardsDestination()
    {
        float _distanceToTravel = speed * minigame.minigameBase.DeltaTime;
        destination = _movement;
        destination.y = destinationPoints[0].y;

        while ((_movement.x > destinationPoints[0].x || (destination - _movement).magnitude <= _distanceToTravel) && destinationPoints.Count > 0 && _distanceToTravel > 0f)
        {
            if (!(_movement.x > destinationPoints[0].x))
            {
                _distanceToTravel -= (destination - _movement).magnitude;
                _movement.y = destination.y;
            }
            destinationPoints.RemoveAt(0);
            if (destinationPoints.Count > 0)
            {
                destination = _movement;
                destination.y = destinationPoints[0].y;
                continue;
            }
            DestinationEmpty();

            bool flag = false;
            foreach (var destinationPoint in destinationPoints)
            {
                if (destinationPoint != FlatPosition)
                    flag = true;
            }

            if (!flag)
                destinationPoints.Clear();
            else
            {
                destination = _movement;
                destination.y = destinationPoints[0].y;
            }
        }

        _absoluteVector = (destination - _movement).normalized * _distanceToTravel;
        if (transform.localPosition.x >= minigame.stageBounds.xMin && transform.localPosition.x < (minigame.stageBounds.width - 150f))
            _movement.y += _absoluteVector.y;
    }

    private RaycastHit2D[] _hits = new RaycastHit2D[32];
    private int hitCount;
    private LayerMask touchLayers = LayerMask.GetMask("Default");
    internal void VirtualFixedUpdate()
    {
        if (isPlayer) return;
        hitCount = Physics2D.RaycastNonAlloc(transform.position, transform.right, _hits, 140f + speed, touchLayers);
        if ((minigame?.minigameBase.DeltaTime ?? 0f) != 0f && curDifficulty != RacerDifficulty.Easy)
        {
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hits[i];
                var hurter = hit.transform.GetComponent<CartHurter>();
                if (hurter != null && hit.distance > (90f + speed))
                {
                    bool isBelow = transform.localPosition.y < hit.transform.localPosition.y;
                    FindPath(hit.transform.localPosition + ((isBelow ? Vector3.down : Vector3.up) * Mathf.Max(10f + speed, Mathf.Abs(hit.transform.localPosition.y))));
                    if (curDifficulty == RacerDifficulty.Hard && hit.distance < (180f + speed))
                        moveCool--;
                    continue;
                }
                var booster = hit.transform.GetComponent<CartBooster>();
                if (booster != null && curDifficulty == RacerDifficulty.Hard && (destinationPoints.Count <= 0 || hit.distance < destinationPoints.Last().x))
                {
                    FindPath(hit.transform.localPosition, false);
                    ResetCooldown();
                }
            }
        }
    }
    private void DestinationEmpty()
    {
        float distance = 120f;
        bool targetFood = false;
        if (moveCool <= 0f) // If `DestinationEmpty()` keeps getting called on every frame, the game freezes.
        {
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hits[i];
                var hurter = hit.transform?.GetComponent<CartHurter>();
                if (hurter != null)
                {
                    bool isBelow = transform.localPosition.y < hit.transform.localPosition.y;
                    FindPath(hit.transform.localPosition + ((isBelow ? Vector3.down : Vector3.up) * Mathf.Max(10f + speed, Mathf.Abs(hit.transform.localPosition.y))));
                    continue;
                }
                if (targetFood) continue;
                var booster = hit.transform?.GetComponent<CartBooster>();
                if (booster != null && hit.distance < (180f + speed))
                {
                    FindPath(hit.transform.localPosition, false);
                    targetFood = true;
                }
            }
        }
        if (!targetFood)
            FindPath(FlatPosition + (Vector2.right * speed) + (Vector2.up * UnityEngine.Random.Range(-distance, distance)));
        if (moveCool <= 0f)
            ResetCooldown();
    }
    private void ResetCooldown()
    {
        switch (curDifficulty)
        {
            case RacerDifficulty.Easy:
                moveCool = UnityEngine.Random.Range(0.7f, 1f);
                break;
            case RacerDifficulty.Normal:
                moveCool = UnityEngine.Random.Range(0.5f, 0.65f);
                break;
            case RacerDifficulty.Hard:
                moveCool = UnityEngine.Random.Range(0.2f, 0.5f);
                break;
            default:
                moveCool = 1f;
                break;
        }
    }

    public void FindPath(Vector2 targetPos, bool targeting)
    {
        if (!minigame.stageBounds.Contains(targetPos))
            targetPos = targetPos.y < minigame.stageBounds.yMin ? (targetPos + (Vector2.down * 90f)) : (targetPos + (Vector2.up * 90f));
        if (targeting && destinationPoints.Count > 0)
            destinationPoints[0] = targetPos;
        else
            destinationPoints.Add(targetPos);
    }
    public void FindPath(Vector2 targetPos) => FindPath(targetPos, targeting: true);

    private void StateUpdate()
    {
        UpdateLineRenderer(transitioningToNext ? 0f : _absoluteVector.y);
        if (transform.localPosition.x >= (minigame.stageBounds.xMax - 150f))
        {
            if (!transitioningToNext)
            {
                transitioningToNext = true;
                minigame.CartMadeItToFinish(this);
            }
        }
        else if (transitioningToNext && minigame.stageBounds.Contains(transform.localPosition))
            transitioningToNext = false;
        if (!isPlayer && curDifficulty == RacerDifficulty.Hard && !transitioningToNext)
        {
            int 
                stageIndex = minigame.stages.IndexOf(transform.parent.gameObject),
                plrstageIndex = minigame.stages.IndexOf(PlayerInstance.transform.parent.gameObject);
            float distance = (PlayerInstance.transform.localPosition.x + (plrstageIndex * minigame.stageBounds.xMax)) - (transform.localPosition.x + (stageIndex * minigame.stageBounds.xMax));
            if (distance > (minigame.stageBounds.xMax / 12f)
                && minigame.GetPlacement(this) > minigame.GetPlacement(PlayerInstance)) // Rubberbanding
            {
                rubber = true;
                speed += BOOST_SPEED * (SLOW_SPEED * minigame.minigameBase.DeltaTime);
                speed = Mathf.Clamp(speed, SLOW_SPEED, REG_SPEED * 10f);
            }
            else if (distance <= (minigame.stageBounds.xMin * 3f) && rubber)
                rubber = false;
        }
    }

    private void OnDestroy()
    {
        Racers.Remove(this);
        minigame.RemoveTracker(transform);
        if (PlayerInstance == this)
            PlayerInstance = null;
    }

    public void Boost()
    {
        boostingThisFrame++;
        speed += BOOST_SPEED * boostingThisFrame;
        audMan.PlaySingle(boostingThisFrame switch {
            0 or 1 => boost,
            2 => boostDouble,
            _ => boostTriple,
        });
    }
    public void Hurt()
    {
        boostingThisFrame = 0;
        speed = SLOW_SPEED;
        audMan.PlaySingle(hurted);
        if (isPlayer)
            minigame.PerfectFailure();
    }
}

internal class CartGhostController : MonoBehaviour // From Tarodev's example ghost script
{
    [SerializeField] internal SpriteRenderer render;
    private Minigame_CartRacing minigame;
    internal Recording replayData;
    public static HashSet<CartGhostController> Ghosts = new HashSet<CartGhostController>();
    internal bool transitioningToNext = false;

    private void OnDestroy()
    {
        Ghosts.Remove(this);
        minigame.RemoveTracker(transform);
    }

    public void Initialize(Minigame_CartRacing minigame, GhostRacer data)
    {
        this.minigame = minigame;
        render.sprite = data.Image;
        replayData = data.data;
        Ghosts.Add(this);
        minigame.TrackController(this);
    }

    public void Dissolve()
    {
        IEnumerator DissolveAnim()
        {
            Color alpha = render.color;
            while (alpha.a > 0f)
            {
                alpha.a -= Time.unscaledDeltaTime;
                render.color = alpha;
                yield return null;
            }
            Destroy(gameObject);
        }
        OnDestroy();
        Destroy(gameObject);
    }
}

public class RacerData(Sprite image, RacerDifficulty difficulty)
{
    internal readonly Sprite Image = image;
    internal readonly RacerDifficulty Difficulty = difficulty;
    internal readonly Color Color = new(UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(0.5f, 1f), UnityEngine.Random.Range(0.5f, 1f));

    public RacerData(Sprite image, RacerDifficulty difficulty, Color color) : this (image, difficulty)
    {
        Color = color;
    }
}

internal class GhostRacer(Sprite image, Recording data)
{
    public Sprite Image = image;
    public Recording data = data;
}

internal class HorizontalScroller : MonoBehaviour
{
    [SerializeField] internal float speed = 1f;
    [SerializeField] internal MinigameBase minigame;
    [SerializeField] internal List<RawImage> imgs = new List<RawImage>();

    private void LateUpdate()
    {
        foreach (var img in imgs)
            img.uvRect = new Rect(img.uvRect.position + new Vector2(speed, 0f) * minigame.DeltaTime, img.uvRect.size);
    }
}

internal class StageCamFollower : MonoBehaviour
{
    private RectTransform rectTransform;
    [SerializeField] internal Minigame_CartRacing minigame;
    private Vector2 pos;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    private void Start()
    {
        LateUpdate();
    }
    private void LateUpdate()
    {
        pos.y = minigame.CameraPosition.y * rectTransform.anchorMax.y;
        rectTransform.localPosition = -pos;
    }
}

public class CartObject : MonoBehaviour
{
    protected Minigame_CartRacing minigame;
    [SerializeField] internal SpriteRenderer spriteRenderer;
    private float speed;
    internal static readonly List<CartObject> ToCallVirtually = new List<CartObject>();
    public virtual void Initialize(Minigame_CartRacing game, Sprite sprite, float speed)
    {
        minigame = game;
        spriteRenderer.sprite = sprite;
        spriteRenderer.size = Vector2.one * 3f;
        this.speed = speed;
        ToCallVirtually.Add(this);
    }
    private void OnDestroy()
    {
        ToCallVirtually.Remove(this);
    }

    internal void VirtualUpdate() // Alternative doings if I want to disable stage's game object and reduce lag.
    {
        if (minigame == null) return;
        transform.localPosition += Vector3.left * (speed * minigame.minigameBase.DeltaTime);
        if (!minigame.stageBounds.Contains(transform.localPosition))
        {
            Destroy(gameObject);
            OnDestroy();
        }
    }

    private void OnBecameInvisible() => spriteRenderer.enabled = false;
    private void OnBecameVisible() => spriteRenderer.enabled = true;
}

internal class CartBooster : CartObject
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var cart = other.GetComponent<CartRacerController>();
        if (cart != null)
        {
            cart.Boost();
            Destroy(gameObject);
        }
    }
}
internal class CartHurter : CartObject
{
    public override void Initialize(Minigame_CartRacing game, Sprite sprite, float speed)
    {
        base.Initialize(game, sprite, speed);
        spriteRenderer.size = Vector2.one * 4.5f;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        var cart = other.GetComponent<CartRacerController>();
        if (cart != null)
        {
            cart.Hurt();
            Destroy(gameObject);
        }
    }
}

internal class CartReplaySystem // From Tarodev's replay system script, but this variant is to account timescale fixes.
{
    private readonly WaitForFixedUpdate _wait = new WaitForFixedUpdate();

    private readonly Dictionary<RecordingType, Recording> _runs = new Dictionary<RecordingType, Recording>();

    private Recording _currentRun;

    private float _elapsedRecordingTime;

    private int _snapshotEveryNFrames;

    private int _frameCount;

    private float _maxRecordingTimeLimit;

    private Recording _currentReplay;

    private GameObject _ghostObj;

    private bool _destroyOnComplete;

    private float _replaySmoothedTime;

    public CartReplaySystem(Minigame_CartRacing runner)
    {
        runner.StartCoroutine(FixedUpdate(runner));
        runner.StartCoroutine(Update(runner));
    }

    private IEnumerator FixedUpdate(Minigame_CartRacing minigame)
    {
        while (true)
        {
            yield return _wait;
            AddSnapshot();
            _elapsedRecordingTime += minigame.SmoothDeltaTime;
        }
    }

    private IEnumerator Update(Minigame_CartRacing minigame)
    {
        while (true)
        {
            yield return null;
            _replaySmoothedTime += minigame.SmoothDeltaTime;
            UpdateReplay();
        }
    }

    public void StartRun(Transform target, int snapshotEveryNFrames = 2, float maxRecordingTimeLimit = 60f)
    {
        _currentRun = new Recording(target);
        _elapsedRecordingTime = 0f;
        _snapshotEveryNFrames = Mathf.Max(1, snapshotEveryNFrames);
        _frameCount = 0;
        _maxRecordingTimeLimit = maxRecordingTimeLimit;
    }

    private void AddSnapshot()
    {
        if (_currentRun != null)
        {
            if (_frameCount++ % _snapshotEveryNFrames == 0)
            {
                _currentRun.AddSnapshot(_elapsedRecordingTime);
            }

            if (_currentRun.Duration >= _maxRecordingTimeLimit)
            {
                FinishRun();
            }
        }
    }

    public bool FinishRun(bool save = true)
    {
        if (_currentRun == null)
        {
            return false;
        }

        if (!save)
        {
            _currentRun = null;
            return false;
        }

        _runs[RecordingType.Last] = _currentRun;
        _currentRun = null;
        if (!GetRun(RecordingType.Best, out var run) || _runs[RecordingType.Last].Duration <= run.Duration)
        {
            _runs[RecordingType.Best] = _runs[RecordingType.Last];
            return true;
        }

        return false;
    }

    public void SetSavedRun(Recording run)
    {
        _runs[RecordingType.Saved] = run;
    }

    public bool GetRun(RecordingType type, out Recording run)
    {
        return _runs.TryGetValue(type, out run);
    }

    public void PlayRecording(RecordingType type, GameObject ghostObj, bool destroyOnCompletion = true)
    {
        if (_ghostObj != null)
        {
            GameObject.Destroy(_ghostObj);
        }

        if (!GetRun(type, out _currentReplay))
        {
            GameObject.Destroy(ghostObj);
            return;
        }

        _replaySmoothedTime = 0f;
        _destroyOnComplete = destroyOnCompletion;
        if (_currentReplay != null)
        {
            _ghostObj = ghostObj;
        }
        else if (_destroyOnComplete)
        {
            GameObject.Destroy(_ghostObj);
        }
    }

    private void UpdateReplay()
    {
        if (_currentReplay == null)
        {
            return;
        }

        Pose pose = _currentReplay.EvaluatePoint(_replaySmoothedTime);
        _ghostObj.transform.SetPositionAndRotation(pose.position, pose.rotation);
        if (_replaySmoothedTime > _currentReplay.Duration)
        {
            _currentReplay = null;
            if (_destroyOnComplete)
            {
                GameObject.Destroy(_ghostObj);
            }
        }
    }

    public void StopReplay()
    {
        if (_ghostObj != null)
        {
            GameObject.Destroy(_ghostObj);
        }

        _currentReplay = null;
    }
}