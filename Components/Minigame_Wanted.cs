using HarmonyLib;
using MTM101BaldAPI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AlexBWFieldtrips.Wanted;

public class Minigame_Wanted : Minigame
{
    internal static Minigame_Wanted Instance { get; private set; }
    internal static readonly HashSet<Student> Students = new HashSet<Student>();
    [SerializeField] internal MinigameHUD minigameHud;
    [SerializeField] internal RoomAsset playroomPre;
    private RoomController playroom;
    private LevelBuilder assignedLL;
    private static FieldInfo
        _hub = AccessTools.DeclaredField(typeof(MinigameBase), "hub"),
        _audWhere = AccessTools.DeclaredField(typeof(Student), "audWhere"),
        _audYay = AccessTools.DeclaredField(typeof(Student), "audYay"),
        _studentaudioManager = AccessTools.DeclaredField(typeof(Student), "audioManager"),
        _studentanimator = AccessTools.DeclaredField(typeof(Student), "animator"),
        _origin = AccessTools.DeclaredField(typeof(Student), "origin"),
        _ec = AccessTools.DeclaredField(typeof(LanternMode), "ec"),
        _sources = AccessTools.DeclaredField(typeof(LanternMode), "sources"),
        _cameraStack = AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_Cameras"),
        _scoreIndicatorTmp = AccessTools.DeclaredField(typeof(MinigameHUD), "scoreIndicatorTmp");
    private Vector3 prePlayPos, camPos;
    private Camera camera;
    private bool studentFound = false;
    private float timer, actualSavedTime;
    private WantedState currentState;
    private Sprite studentSpr;
    [SerializeField] internal Image studentToFindUI;
    private Student targetStudent, targetStudentPre;
    private List<Student> spawnedStudents = new List<Student>();
    private LanternMode lightController;
    [SerializeField] internal SoundObject wrongClick, audPoints, tickingTime, timeBell;
    private List<LanternSource> LanternSources => (List<LanternSource>)_sources.GetValue(lightController);
    private Transform cursorRaycasted;
    private LayerMask triggerMask = LayerMask.GetMask("NPCs");
    private Ray pointRay;
    private GameCamera cameraToUse;
    [SerializeField] internal StandardDoor doorPre;

    public override void Initialize(MinigameBase minigameBase, bool scoringMode)
    {
        Instance = this;
        studentToFindUI.gameObject.SetActive(false);
        base.Initialize(minigameBase, scoringMode);
        var fieldtripRoom = (MinigameBaseRoomFunction)_hub.GetValue(minigameBase);
        fieldtripRoom.Room.ec.CullingManager.SetActive(false);
        assignedLL = GameObject.FindObjectsOfType<LevelBuilder>(true).First(x => x.Ec == fieldtripRoom.Room.ec);
        playroom = assignedLL.LoadRoom(playroomPre, new IntVector2(32, 40), playroomPre.potentialDoorPositions[0], Direction.North, false, playroomPre.florTex, playroomPre.wallTex, playroomPre.ceilTex);
        playroom.UpdatePosition();
        assignedLL.UpdateRoomObjectPosition(playroom); // This isn't called when the room is finished loading inside the function.
        foreach (var poster in playroomPre.posterDatas)
        {
            var posterData = poster.GetNew();
            assignedLL.Ec.BuildPoster(posterData.poster, assignedLL.Ec.CellFromPosition(posterData.position.Adjusted(playroomPre.potentialDoorPositions[0], Direction.North) + new IntVector2(32, 40)), posterData.direction.RotatedRelativeToNorth(Direction.North));
        }
        var doorPos = IntVector2.GetGridPosition(playroom.objectObject.transform.Find("Decor_ExitSign(Clone)").position);
        playroom.ec.CreateCell(0, doorPos + playroom.dir.ToIntVector2(), playroom);
        assignedLL.ManuallyBuildDoor(playroom, doorPre, doorPos, playroom.dir);
        //playroom.doors[1].Lock(true);
        playroom.ec.DestroyCell(playroom.ec.CellFromPosition(doorPos + playroom.dir.ToIntVector2()));
        prePlayPos = playroom.objectObject.transform.Find("Decor_PencilNotes(Clone)").position;
        camPos = playroom.objectObject.transform.Find("Decor_Notebooks(Clone)").position;
        Destroy(playroom.objectObject.transform.Find("Decor_PencilNotes(Clone)").gameObject);
        Destroy(playroom.objectObject.transform.Find("Decor_Notebooks(Clone)").gameObject);
        camera = new GameObject("CameraWanted", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        data.renderShadows = false;
        var gameCam = CoreGameManager.Instance.GetCamera(0);
        _cameraStack.SetValue(data, new List<Camera>() { gameCam.canvasCam, gameCam.mapCam, GlobalCam.Instance.RenderTexCam });
        ResetCamera();
        cameraToUse = CoreGameManager.Instance.GetCamera(0);
        cameraToUse.UpdateTargets(camera.transform, 0);
        lightController = gameObject.GetOrAddComponent<LanternMode>();
        if ((EnvironmentController)_ec.GetValue(lightController) == null) lightController.Initialize(assignedLL.Ec);
        if (!scoringMode)
        {
            minigameHud.HideScore();
            minigameBase.SetFinalRound(9);
        }
        else
        {
            minigameBase.SetFinalRound(999);
        }
    }

    private void OnDestroy()
    {
        cameraToUse.UpdateTargets(null, 0);
        Instance = null;
        ClearStudents();
        Destroy(camera.gameObject);
        assignedLL.Ec.rooms.Remove(playroom);
        assignedLL.DestroyRoom(playroom);
        assignedLL.Ec.lightingOverride = false;
        assignedLL.Ec.CullingManager.SetActive(true);
        foreach (var cell in assignedLL.Ec.cells)
            assignedLL.Ec.UpdateLightingAtCell(cell);
    }

    public override void VirtualReset()
    {
        base.VirtualReset();
        actualSavedTime = 20f;
        ResetCamera();
        LanternSources.Clear();
        studentToFindUI.gameObject.SetActive(false);
        TMP_Text timerText = (TMP_Text)_scoreIndicatorTmp.GetValue(minigameHud);
        timerText.gameObject.SetActive(false);
    }

    private void ResetCamera()
    {
        camera.transform.position = prePlayPos + (Vector3.back * 15f);
        camera.transform.rotation = Direction.North.ToRotation();
    }

    public override void VirtualStart()
    {
        base.VirtualStart();
        VirtualReset();
        minigameHud.Reinitialize();
        minigameBase.AdvanceRound(2f);
    }

    public override void VirtualUpdate()
    {
        base.VirtualUpdate();
        TMP_Text timerText = (TMP_Text)_scoreIndicatorTmp.GetValue(minigameHud);
        switch (currentState)
        {
            default:
                timerText.gameObject.SetActive(false);
                break;
            case WantedState.InSearch:
                timerText.gameObject.SetActive(true);
                var cursor = CursorController.Instance;
                if (cursorRaycasted == null)
                {
                    cursorRaycasted = new GameObject("Cursor Raycast").transform;
                    lightController.AddSource(cursorRaycasted, 2.5f, Color.white);
                }
                // The transform following cursor position took me longer because the results always point at the camera's position.
                var cursorPos = GlobalCam.Instance.CursorToRealScreenPosition(); //GlobalCam.Instance.UiToRealScreenPosition(new Vector2(cursor.position.x - (cursor.maxRange.x / 2f), (360f + cursor.position.y) + (cursor.minRange.y / 2f)));
                pointRay = camera.ScreenPointToRay(new Vector3(cursorPos.x, cursorPos.y));
                float squaredSize = camera.sensorSize.y > camera.sensorSize.x ? camera.sensorSize.y : camera.sensorSize.x;
                cursorRaycasted.transform.position = new(pointRay.GetPoint(squaredSize).x, 5f, pointRay.GetPoint((cursor.LocalPosition.y - cursor.minRange.y) / 5f).z);
                if (Physics.SphereCast(pointRay, 1f, out var hit, float.PositiveInfinity, triggerMask, QueryTriggerInteraction.Collide))
                {
                    var student = hit.transform.GetComponent<Student>();
                    if (student != null && spawnedStudents.Contains(student) && InputManager.Instance.GetDigitalInput("Interact", true))
                        ClickOnStudent(student);
                }

                var camPos = camera.transform.position;
                if (cursor.position.x > cursor.maxRange.x - 25f)
                    camPos += Vector3.right * 1f;
                else if (cursor.position.x < cursor.minRange.x + 25f)
                    camPos += Vector3.left * 1f;
                if (cursor.position.y > cursor.maxRange.y - 25f)
                    camPos += Vector3.forward * 1f;
                else if (cursor.position.y < cursor.minRange.y + 25f)
                    camPos += Vector3.back * 1f;
                Vector3
                    maxedge = playroom.ec.RealRoomMax(playroom),
                    minedge = playroom.ec.RealRoomMin(playroom);
                if (camPos.x > maxedge.x - 25f)
                    camPos.x = maxedge.x - 25f;
                else if (camPos.x < minedge.x + 25f)
                    camPos.x = minedge.x + 25f;
                if (camPos.z > maxedge.z - 40f)
                    camPos.z = maxedge.z - 40f;
                else if (camPos.z < minedge.z)
                    camPos.z = minedge.z;
                camera.transform.position = camPos;

                if (!studentFound)
                    timer -= minigameBase.DeltaTime;
                if (timer <= 10f)
                {
                    if (!minigameBase.AudioManager.QueuedAudioIsPlaying)
                    {
                        minigameBase.AudioManager.SetLoop(true);
                        minigameBase.AudioManager.QueueAudio(tickingTime, true);
                    }
                }
                else if (minigameBase.AudioManager.QueuedAudioIsPlaying)
                    minigameBase.AudioManager.FlushQueue(true);
                string text = Mathf.Floor(timer % 60f).ToString();
                if (Mathf.Floor(timer % 60f) < 10f)
                    text = "0" + Mathf.Floor(timer % 60f);
                timerText.text = Mathf.Floor(timer / 60f) + ":" + text;
                if (timer <= 0f)
                {
                    LanternSources.Clear();
                    lightController.AddSource(targetStudent.transform, 2.5f, Color.white);
                    timer = 2f + (Vector3.Distance(new Vector3(targetStudent.transform.position.x, camera.transform.position.y, targetStudent.transform.position.z), camera.transform.position) / 25f);
                    currentState = WantedState.OutOfTime;
                    minigameBase.AudioManager.FlushQueue(false);
                    minigameBase.AudioManager.PlaySingle(timeBell);
                }
                break;
            case WantedState.FoundStudent:
                timer -= minigameBase.DeltaTime;
                float actualTime = actualSavedTime - 10f;
                text = Mathf.Floor(actualTime % 60f).ToString();
                if (Mathf.Floor(actualTime % 60f) < 10f)
                    text = "0" + Mathf.Floor(actualTime % 60f);
                timerText.text = Mathf.Floor(actualTime / 60f) + ":" + text;
                if (timer <= 0f)
                {
                    LanternSources.Clear();
                    ClearStudents();
                    currentState = WantedState.StudentIntro;
                    if (minigameBase.CurrentRound >= (scoringMode ? 999 : 9))
                    {
                        if (!scoringMode && perfect)
                            minigameBase.AwardPerfectBonus();
                        minigameBase.Win();
                    }
                    else
                        minigameBase.AdvanceRound(0.5f);
                }
                break;
            case WantedState.OutOfTime:
                camPos = camera.transform.position;
                camPos.x = targetStudent.transform.position.x;
                camPos.z = targetStudent.transform.position.z - (camera.transform.eulerAngles.x / 2f);
                camera.transform.position = Vector3.MoveTowards(camera.transform.position, camPos, (65f + (Vector3.Distance(camPos, camera.transform.position) / 25f)) * minigameBase.DeltaTime);
                timer -= minigameBase.DeltaTime;
                if (timer <= 0f)
                {
                    LanternSources.Clear();
                    ClearStudents();
                    currentState = WantedState.StudentIntro;
                    if (scoringMode)
                        minigameBase.SetScore(score);
                    else
                        minigameBase.Lose();
                }
                timerText.text = "0:00";
                break;
        }
    }

    private Student GetRandomStudent() {
        var filteredList = Students.Where(x => x != targetStudentPre).ToList();
        return filteredList[UnityEngine.Random.Range(0, filteredList.Count)];
    }
    private void TargetRandomStudent()
    {
        targetStudentPre = GetRandomStudent();
        targetStudent = (Student)assignedLL.Ec.SpawnNPC(targetStudentPre, IntVector2.GetGridPosition(prePlayPos));
        targetStudent.SetupForItem(CoreGameManager.Instance.NoneItem);
        studentSpr = targetStudentPre.spriteRenderer[0].sprite;
        studentToFindUI.sprite = studentSpr;
    }

    internal void ClickOnStudent(Student student)
    {
        AudioManager audMan = (AudioManager)_studentaudioManager.GetValue(student);
        if (student != targetStudent)
        {
            timer -= 5f;
            perfect = false;
            student.StartCoroutine(FaceCamera(student));
            minigameBase.AudioManager.PlaySingle(wrongClick);
            minigameBase.AudioManager.PlaySingle((SoundObject)_audWhere.GetValue(student));
            return;
        }
        studentFound = true;
        targetStudent.behaviorStateMachine.ChangeState(new Student_Complete(targetStudent, targetStudent));
        targetStudent.Navigator.SetSpeed(0f);
        targetStudent.Navigator.maxSpeed = 0f;
        minigameBase.AudioManager.PlaySingle((SoundObject)_audYay.GetValue(targetStudent));
        Animator studentAnimator = (Animator)_studentanimator.GetValue(student);
        studentAnimator.Play("Student_Jump", -1, 0f);
        actualSavedTime = timer + 10f;
        timer = 2.5f;
        currentState = WantedState.FoundStudent;
        minigameBase.AudioManager.PlaySingle(audPoints);
        if (scoringMode)
        {
            AddScore(1);
            minigameHud.AddScore(score);
        }
    }

    private IEnumerator FaceCamera(Student student)
    {
        var prevState = student.behaviorStateMachine.CurrentState;
        student.behaviorStateMachine.ChangeState(new Student_WantedIntro(targetStudent));
        student.Navigator.SetSpeed(0f);
        student.Navigator.maxSpeed = 0f;
        float time = 2f;
        student.transform.rotation = Quaternion.LookRotation((camera.transform.position - student.transform.position).normalized, Vector3.up);
        yield return null;
        while (time > 0f)
        {
            time -= minigameBase.DeltaTime;
            student.transform.rotation = Quaternion.LookRotation((camera.transform.position - student.transform.position).normalized, Vector3.up);
            yield return null;
        }
        student.behaviorStateMachine.ChangeState(prevState);
    }

    public override void StartRound()
    {
        studentFound = false;
        studentToFindUI.gameObject.SetActive(false);
        Destroy(cursorRaycasted?.gameObject);
        ResetCamera();
        base.StartRound();
        if (((minigameBase.CurrentRound - 1) % 10) == 0)
            currentState = WantedState.StudentIntro;
        else
            currentState = WantedState.StudentIntroShort;
        TargetRandomStudent();
        switch (currentState)
        {
            case WantedState.StudentIntro:
                targetStudent.behaviorStateMachine.ChangeState(new Student_WantedIntro(targetStudent));
                targetStudent.transform.LookAt(camera.transform);
                StartCoroutine(PlayIntroCutscene());
                break;
            case WantedState.StudentIntroShort:
                StartSearch();
                break;
        }
    }

    private void ClearStudents()
    {
        foreach (var student in spawnedStudents)
            student.Despawn();
        spawnedStudents.Clear();
    }

    private IEnumerator PlayIntroCutscene()
    {
        Transform[] lights = new[]
        {
            new GameObject("LightLeft").transform,
            new GameObject("LightRight").transform
        };
        lights[0].transform.position = prePlayPos + (Vector3.left * 60f);
        lights[1].transform.position = prePlayPos + (Vector3.right * 60f);
        foreach (var light in lights)
            lightController.AddSource(light, 2.5f, Color.white);
        float timer = 3.5f, elaspedLightTime = 0.5f;
        bool whichSide = true;
        while (timer > 0f)
        {
            timer -= minigameBase.DeltaTime;
            elaspedLightTime -= minigameBase.DeltaTime;
            lights[0].transform.position = whichSide ? Vector3.Lerp(prePlayPos, prePlayPos + (Vector3.left * 60f), elaspedLightTime * 2f) : Vector3.Lerp(prePlayPos + (Vector3.left * 60f), prePlayPos, elaspedLightTime * 2f);
            lights[1].transform.position = whichSide ? Vector3.Lerp(prePlayPos, prePlayPos + (Vector3.right * 60f), elaspedLightTime * 2f) : Vector3.Lerp(prePlayPos + (Vector3.right * 60f), prePlayPos, elaspedLightTime * 2f);
            if (elaspedLightTime < 0f)
            {
                elaspedLightTime = 0.5f;
                whichSide = !whichSide;
            }
            yield return null;
        }
        LanternSources.Clear();
        lightController.AddSource(targetStudent.transform, 2.5f, Color.white);
        Destroy(lights[0].gameObject);
        Destroy(lights[1].gameObject);
        timer = 2f;
        yield return null;
        while (timer > 0f)
        {
            timer -= minigameBase.DeltaTime;
            yield return null;
        }
        LanternSources.Clear();
        timer = 1f;
        yield return null;
        while (timer > 0f)
        {
            timer -= minigameBase.DeltaTime;
            yield return null;
        }
        StartSearch();
    }

    private void StartSearch()
    {
        int students = UnityEngine.Random.Range(minigameBase.CurrentRound, minigameBase.CurrentRound + 8);
        for (int i = 0; i < students; i++)
        {
            var student = (Student)assignedLL.Ec.SpawnNPC(GetRandomStudent(), playroom.RandomEntitySafeCellNoGarbage().position);
            student.SetupForItem(CoreGameManager.Instance.NoneItem);
            spawnedStudents.Add(student);
        }
        targetStudent.transform.position = playroom.RandomEntitySafeCellNoGarbage().position.CenterWorldPosition();
        _origin.SetValue(targetStudent, targetStudent.ec.CellFromPosition(targetStudent.transform.position));
        spawnedStudents.Add(targetStudent);
        studentToFindUI.gameObject.SetActive(true);
        foreach (var student in spawnedStudents)
            student.behaviorStateMachine.ChangeState(new Student_WantedWander(student, minigameBase));
        camera.transform.position = camPos + (Vector3.up * 20f);
        camera.transform.eulerAngles = Vector3.right * 45f;
        timer = actualSavedTime;
        currentState = WantedState.InSearch;
    }
}

internal enum WantedState
{
    StudentIntro,
    StudentIntroShort,
    InSearch,
    FoundStudent,
    OutOfTime
}

internal class Student_WantedIntro(Student student) : Student_StateBase(student, student)
{
    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_DoNothing(student, 1));
    }
}

internal class Student_WantedWander(Student student, MinigameBase minigame) : Student_Wander(student, student)
{
    private static FieldInfo _reWanderTime = AccessTools.DeclaredField(typeof(Student_Wander), "reWanderTime");
    public override void Update()
    {
        var reWanderTime = (float)_reWanderTime.GetValue(this);
        reWanderTime -= minigame.DeltaTime * npc.TimeScale;
        if (reWanderTime <= 0f)
        {
            student.behaviorStateMachine.ChangeState(new Student_WantedWander(student, minigame));
        }
        _reWanderTime.SetValue(this, reWanderTime);
    }
}