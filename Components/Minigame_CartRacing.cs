using CampfireFrenzy;
using MTM101BaldAPI.AssetTools;
using PicnicPanic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityCipher;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace AlexBWFieldtrips.DededeeCartRacing;
public class Minigame_CartRacing : Minigame
{
    [SerializeField] internal AnalogInputData movementAnalogData;
    [SerializeField] internal Transform gameGOBJ;

    internal bool ShowGhosts = false;
    private GhostRacer[] ghostracers;

    [SerializeField] internal List<Sprite> possibleStage1BGs = new List<Sprite>();
    [SerializeField] internal List<Sprite> possibleStage2BGs = new List<Sprite>();
    [SerializeField] internal List<Sprite> possibleStage3BGs = new List<Sprite>();

    internal static readonly Dictionary<Character, RacerData> possibleRacers = new Dictionary<Character, RacerData>();
    [SerializeField] internal CartRacerController cartPre;

    [SerializeField] internal readonly Dictionary<int, GameObject> stages = new Dictionary<int, GameObject>();

    public static void AddNewRacer(NPC character, RacerData data) => possibleRacers.Add(character.Character, data);

    public override void Initialize(MinigameBase minigameBase, bool scoringMode)
    {
        base.Initialize(minigameBase, scoringMode);
        minigameBase.SetFinalRound(3);
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
                            var racer = new GhostRacer(Sprite.Create(image, new Rect(0f, 0f, image.width, image.height), Vector2.one / 2f, 1f, 0u, SpriteMeshType.FullRect), data.FileName, racerRun);
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
                        Instantiate(cartPre, gameGOBJ, false).Initialize(this, item, initialplacement: placement);
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
                    Instantiate(cartPre, gameGOBJ, false).Initialize(this, item);
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
        Instantiate(cartPre, gameGOBJ, false).Initialize(this, possibleRacers[Character.Null], true, halfplacement);
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
    [SerializeField] internal Image render;
    private RacerDifficulty curDifficulty;

    public void Initialize(Minigame_CartRacing minigame, RacerData data, bool isPlayer = false, float initialplacement = 0f)
    {
        this.minigame = minigame;
        render.sprite = data.Image;
        curDifficulty = data.Difficulty;
        this.isPlayer = isPlayer;
        _movement = new Vector2(0f, initialplacement);
        if (isPlayer)
            gameObject.layer = LayerMask.NameToLayer("Player");
        else
            gameObject.layer = LayerMask.NameToLayer("NPCs");
    }

    private void Update()
    {
        _movement.x += minigame.minigameBase.DeltaTime;
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
