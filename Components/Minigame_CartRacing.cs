using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

    [SerializeField] internal AnalogInputData movementAnalogData;

    internal bool ShowGhosts = false;
    private GhostRacer[] ghostracers;

    internal static readonly Dictionary<Character, RacerData> possibleRacers = new Dictionary<Character, RacerData>();
    [SerializeField] internal CartRacerController cartPre;

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

    private const int RACERS_COUNT = 7;
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
        foreach (var i in racersPos)
            Debug.Log($"Placement: {i}");
        return racersPos[racersNonHash.IndexOf(racer)];
    }

    public override void Initialize(MinigameBase minigameBase, bool scoringMode)
    {
        base.Initialize(minigameBase, scoringMode);
        minigameBase.SetFinalRound(stages.Count);
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
            var path = Path.Combine(AssetLoader.GetModPath(AlexBWFieldTripsPlugin.Instance), "GhostDatas");
            if (Directory.Exists(Path.Combine(path)))
            {
                var files = Directory.GetFiles(path);
                if (files.Length > 0)
                {
                    ghostracers = new GhostRacer[files.Length];
                    for (int i = 0;  i < files.Length; i++)
                    {
                        try
                        {
                            var data = JsonUtility.FromJson<CartMinigameGhostRacerData>(RijndaelEncryption.Decrypt(File.ReadAllText(files[i]), "RACERGHOSTDATA"));
                            Texture2D image = new Texture2D(16, 16);
                            image.LoadRawTextureData(data.Image);
                            image.Apply();
                            GCHandle gcHandle = GCHandle.Alloc(data.data, GCHandleType.Pinned);
                            Dictionary<float, Vector2> racerRun = (Dictionary<float, Vector2>)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(Dictionary<float, Vector2>));
                            var racer = new GhostRacer(Sprite.Create(image, new Rect(0f, 0f, image.width, image.height), Vector2.one / 2f, 26f, 0u, SpriteMeshType.FullRect), data.FileName, racerRun);
                            ghostracers[i] = racer;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                            continue;
                        }
                    }
                }
            }
        }
    }
    private void OnDestroy()
    {
        Destroy(camera.gameObject);
    }

    public override void VirtualReset()
    {
        base.VirtualReset();
        while (CartRacerController.Racers.Count > 0)
        {
            var racer = CartRacerController.Racers.First();
            CartRacerController.Racers.Remove(racer); // While loops will not let `OnDestroy()` be called until the loop ends.
            Destroy(racer.gameObject);
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
    }

    public override void VirtualUpdate()
    {
        base.VirtualUpdate();
        camPos.y = Mathf.Clamp(camPos.y, stageBounds.yMin + camera.orthographicSize, stageBounds.yMax - camera.orthographicSize);
        var player = CartRacerController.PlayerInstance;
        if (player != null && player.transform.localPosition.x >= stageBounds.xMin && player.transform.localPosition.x < (stageBounds.width - 150f))
        {
            camPos.y = Mathf.MoveTowards(camPos.y, player.transform.position.y, (Vector3.Distance(camPos, player.transform.position) * 15f) * minigameBase.DeltaTime);
            camPos.x = player.transform.position.x + 15f;
        }
        camera.transform.position = camPos;
        foreach (var racer in CartRacerController.Racers)
            racer.VirtualUpdate();
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
            if (win)
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
            stages[i].transform.localPosition = (i == stage) ? Vector3.zero : Vector3.up * ((stageBounds.height * 3f) * (i + 1));
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
        Instantiate(cartPre, stages[0].transform, false).Initialize(this, possibleRacers[Character.Null], true, halfplacement);
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
        SLOW_SPEED = 10f;
    public static HashSet<CartRacerController> Racers = new HashSet<CartRacerController>();
    [SerializeField] internal SpriteRenderer render;
    [SerializeField] internal LineRenderer liner;
    [SerializeField] internal AudioManager audMan;
    private Minigame_CartRacing minigame;
    private Vector2 _absoluteVector, _deltaVector;
    private Vector2 _movement;
    private bool isPlayer = false, transitioningToNext = false;
    public bool Player => isPlayer;
    public static CartRacerController PlayerInstance { get; private set; }
    private float speed = REG_SPEED;

    [Header("Bot Stuff")]
    private RacerDifficulty curDifficulty;
    private Vector2 _startPos, destination, expectedPosition, previousPosition;
    private List<Vector2> destinationPoints = new List<Vector2>();
    private bool beenMoved;
    private Vector2 FlatPosition => new Vector2(transform.localPosition.x, transform.localPosition.y);

    public void Initialize(Minigame_CartRacing minigame, RacerData data, bool isPlayer = false, float initialplacement = 0f)
    {
        this.minigame = minigame;
        render.sprite = data.Image;
        curDifficulty = data.Difficulty;
        this.isPlayer = isPlayer;
        _movement = new Vector2(0f, initialplacement);
        transform.localPosition = _movement;
        expectedPosition = transform.localPosition;
        _startPos = transform.localPosition;
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
    }
    
    private void UpdateLineRenderer(float absoluteVector)
    {
        Vector3 linePos = liner.GetPosition(2);
        liner.SetPosition(2, Vector3.MoveTowards(linePos, (Vector3.right * linePos.x) + (Vector3.down * (5f * absoluteVector)), speed * minigame.minigameBase.DeltaTime));
        linePos = liner.GetPosition(1);
        liner.SetPosition(1, Vector3.MoveTowards(linePos, (Vector3.right * linePos.x) + (Vector3.down * (10f * absoluteVector)), speed * minigame.minigameBase.DeltaTime));
        linePos = liner.GetPosition(0);
        liner.SetPosition(0, Vector3.MoveTowards(linePos, (Vector3.right * linePos.x) + (Vector3.down * (15f * absoluteVector)), speed * minigame.minigameBase.DeltaTime));
    }

    private bool pausedAudMans = false;
    private void LateUpdate()
    {
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
        _movement = transform.localPosition;
        _movement.x += (speed * minigame.minigameBase.DeltaTime);
        if (isPlayer)
        {
            InputManager.Instance.GetAnalogInput(minigame.movementAnalogData, out _absoluteVector, out _deltaVector, 0.1f);
            _deltaVector.x = 0f;
            _deltaVector.y = 0f;

            if (transform.localPosition.x >= minigame.stageBounds.xMin && transform.localPosition.x < (minigame.stageBounds.width - 150f))
                _movement.y += _absoluteVector.y;
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
        _startPos = transform.localPosition;
        destination.y = transform.localPosition.y;
        if (expectedPosition != FlatPosition)
            beenMoved = true;
        else
            beenMoved = false;
        if (!minigame.stageBounds.Contains(_movement)) return;

        if (previousPosition != FlatPosition)
        {
            if (beenMoved)
            {
                if (destination != Vector2.zero)
                {
                    FindPath(transform.localPosition, destination);
                }
                else
                {
                    ClearDestination();
                    DestinationEmpty();
                }
            }

            previousPosition = FlatPosition;
        }

        if (destinationPoints.Count > 0)
        {
            MoveTowardsDestination();
            expectedPosition = transform.localPosition;
        }
        else
        {
            expectedPosition = transform.localPosition;
            DestinationEmpty();
        }
    }

    public void MoveTowardsDestination()
    {
        destination = new(_movement.x, destinationPoints[0].y);

        while ((destination - _movement).magnitude <= minigame.minigameBase.DeltaTime && destinationPoints.Count > 0)
        {
            _movement = destination;
            destinationPoints.RemoveAt(0);
            if (destinationPoints.Count > 0)
            {
                destination = new(_movement.x, destinationPoints[0].y);
                continue;
            }
            DestinationEmpty();

            bool flag = false;
            foreach (Vector2 destinationPoint in destinationPoints)
            {
                if (destinationPoint != FlatPosition)
                {
                    flag = true;
                }
            }

            if (!flag)
            {
                destinationPoints.Clear();
            }
            else
            {
                destination = new(_movement.x, destinationPoints[0].y);
            }
        }

        _absoluteVector = (destination - _movement).normalized;
        _movement.y += _absoluteVector.y;
    }

    private void DestinationEmpty()
    {
        FindPath(FlatPosition + (Vector2.up * UnityEngine.Random.Range(-20f, 20f)));
    }

    public void FindPath(Vector2 startPos, Vector2 targetPos, bool targeting)
    {
        if (destinationPoints.Count > 0)
            destinationPoints[destinationPoints.Count - 1] = targetPos;
        else
            destinationPoints.Add(targetPos);
    }
    public void FindPath(Vector2 targetPosition) => FindPath(transform.localPosition, targetPosition);
    public void FindPath(Vector2 startPos, Vector2 targetPos) => FindPath(startPos, targetPos, targeting: true);
    public void CheckPath()
    {
        if (destinationPoints.Count > 0)
        {
            FindPath(transform.localPosition, destinationPoints[destinationPoints.Count - 1], false);
        }
    }

    public void ClearDestination()
    {
        destinationPoints.Clear();
    }

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
    }

    private void OnDestroy()
    {
        Racers.Remove(this);
        if (PlayerInstance == this)
            PlayerInstance = null;
    }
}

public class RacerData
{
    internal readonly Sprite Image;
    internal readonly RacerDifficulty Difficulty;

    public RacerData(Sprite image, RacerDifficulty difficulty)
    {
        Image = image;
        Difficulty = difficulty;
    }
}

internal class GhostRacer
{
    public Sprite Image;
    public string FileName;
    public Dictionary<float, Vector2> data;

    public GhostRacer(Sprite image, string fileName, Dictionary<float, Vector2> data)
    {
        Image = image;
        FileName = fileName;
        this.data = data;
    }
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

internal class StageChanger : MonoBehaviour
{
    [SerializeField] internal Minigame_CartRacing minigame;
    private void OnTriggerEnter2D(Collider2D other)
    {
        var cart = other.GetComponent<CartRacerController>();
        if (cart != null)
            minigame.CartMadeItToFinish(cart);
    }
}