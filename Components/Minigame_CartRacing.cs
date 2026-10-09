using HarmonyLib;
using MTM101BaldAPI.AssetTools;
using System;
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
        _cameraStack = AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_Cameras");

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

    public static void AddNewRacer(NPC character, RacerData data) => possibleRacers.Add(character.Character, data);

    public override void Initialize(MinigameBase minigameBase, bool scoringMode)
    {
        base.Initialize(minigameBase, scoringMode);
        minigameBase.SetFinalRound(3);
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
        activeBG = stages[0];
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
        for (int i = CartRacerController.Racers.Count - 1; i >= 0; i--)
        {
            var racer = CartRacerController.Racers.Last();
            Destroy(racer.gameObject);
        }
        curStage = 0;
        ChangeStage(0, false);
        raceFinished = false;
    }

    public override void VirtualStart()
    {
        base.VirtualStart();
        AssignRacers(scoringMode ? 0 : 7);
        camera.transform.position = CartRacerController.PlayerInstance.transform.position + (Vector3.right * 25f);
    }

    public override void VirtualUpdate()
    {
        base.VirtualUpdate();
        var player = CartRacerController.PlayerInstance;
        if (player != null && player.transform.localPosition.x >= 5f && player.transform.localPosition.x < 460f - 5f)
            camera.transform.position = Vector3.MoveTowards(camera.transform.position, player.transform.position, 50f * minigameBase.DeltaTime);
    }

    internal void CartMadeItToFinish(CartRacerController cart)
    {
        if (raceFinished) return;
        if (cart.Player)
            ChangeStage(curStage);
        if ((curStage + 1) > (stages.Count - 1))
        {
            End(cart.Player);
            return;
        }
        cart.transform.SetParent(stages[curStage + 1].transform);
        cart.transform.localPosition = new(0f, cart.transform.localPosition.y, 0f);
    }
    private void End(bool win)
    {
        raceFinished = true;
        if (win)
        {
            if (perfect && !scoringMode)
                minigameBase.AwardPerfectBonus();
            minigameBase.Win();
            return;
        }
        minigameBase.Lose();
    }
    private void ChangeStage(int stage, bool transition = true)
    {
        if (transition)
            GlobalCam.Instance.Transition(UiTransition.Dither, 0.01666667f);
        curStage = stage;
        for (int i = 0; i < stages.Count; i++)
            stages[i].SetActive(i == stage);
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

    private void AssignRacers(int amount = 7)
    {
        var rng = new System.Random(CoreGameManager.Instance.Seed());
        int hardAmount = Mathf.RoundToInt(amount / 4);
        int halfAmount = Mathf.RoundToInt(amount / 2);
        List<RacerData> datas = possibleRacers.Values.ToList();
        datas.RemoveAll(x => x.Difficulty == RacerDifficulty.NotParticipating);
        float halfplacement = 0f;
        for (int i = 0; i < amount; i++)
        {
            float placement = 10f;
            if (i <= hardAmount)
            {
                foreach (RacerData item in datas.Where(x => x.Difficulty == RacerDifficulty.Hard))
                {
                    if (rng.Next(0, 100) >= 10)
                    {
                        Instantiate(cartPre, stages[0].transform, false).Initialize(this, item, initialplacement: placement);
                        datas.Remove(item);
                        break;
                    }
                }
                placement += 10f;
                continue;
            }
            foreach (RacerData item in datas.Where(x => x.Difficulty != RacerDifficulty.Hard))
            {
                if (rng.Next(0, 100) >= 10)
                {
                    Instantiate(cartPre, stages[0].transform, false).Initialize(this, item);
                    datas.Remove(item);
                    break;
                }
            }
            if (i == halfAmount)
            {
                halfplacement = placement;
                placement += 10f;
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
    private Minigame_CartRacing minigame;
    private Vector2 _absoluteVector;
    private Vector2 _deltaVector;
    private Vector2 _movement;
    private bool isPlayer = false;
    public bool Player => isPlayer;
    [SerializeField] internal SpriteRenderer render;
    private RacerDifficulty curDifficulty;
    public static HashSet<CartRacerController> Racers = new HashSet<CartRacerController>();
    public static CartRacerController PlayerInstance { get; private set; }
    private float speed = 5f;

    public void Initialize(Minigame_CartRacing minigame, RacerData data, bool isPlayer = false, float initialplacement = 0f)
    {
        this.minigame = minigame;
        render.sprite = data.Image;
        curDifficulty = data.Difficulty;
        this.isPlayer = isPlayer;
        _movement = new Vector2(0f, initialplacement);
        if (isPlayer)
        {
            gameObject.layer = LayerMask.NameToLayer("Player");
            PlayerInstance = this;
        }
        else
            gameObject.layer = LayerMask.NameToLayer("NPCs");
        Racers.Add(this);
    }

    private void Update()
    {
        _movement = transform.localPosition;
        _movement.x += (speed * minigame.minigameBase.DeltaTime);
        if (isPlayer)
        {
            InputManager.Instance.GetAnalogInput(minigame.movementAnalogData, out _absoluteVector, out _deltaVector, 0.1f);
            _deltaVector.x = 0f;
            _deltaVector.y = 0f;

            _movement.y += _absoluteVector.y;
            transform.localPosition = _movement;
            return;
        }
        transform.localPosition = _movement;
    }

    private void OnDestroy()
    {
        Racers.Remove(this);
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