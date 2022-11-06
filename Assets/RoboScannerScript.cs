using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using System;

public class RoboScannerScript : MonoBehaviour {

    public KMAudio audio;
    public KMAudio.KMAudioRef audioRef;
    public KMBombInfo bomb;
    public KMSelectable[] buttons;
    public Transform grid;
    public Transform[] gridPositions;
    public TextMesh speedText;
    public TextMesh submitText;
    public Sprite[] robotSprites;
    public GameObject spriteObjTemplate;

    private List<GameObject> spriteObjs = new List<GameObject>();
    private List<List<int>> robotPaths = new List<List<int>>();
    private List<int> chosenBotSprites = new List<int>();
    private Coroutine holdingCo = null;
    private bool activated;
    private bool submitMode;
    private string[] allInputs = { "RLDU", "URLD", "RDUL", "UDRL", "DURL", "DRUL", "LURD", "DLRU", "ULRD", "LDRU", "RULD", "RLUD", "-", "URDL", "LRDU", "ULDR", "UDLR", "DRLU", "LDUR", "DLUR", "RUDL", "DULR", "LRUD", "RDLU", "LUDR" };
    private string correctInputs;
    private int emptyCell;
    private int heldBtn;
    private int speedSetting = 1;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable btn = obj;
            btn.OnInteract += delegate () { PressButton(btn); return false; };
            int index = Array.IndexOf(buttons, btn);
            if (index != 4 && index != 5)
                btn.OnInteractEnded += delegate () { ReleaseButton(btn); };
        }
        speedText.text = "";
        GetComponent<KMBombModule>().OnActivate += Activate;
    }

    void Start()
    {
        regen:
        int botCt = 0;
        int[][] takenCells = new int[5][];
        for (int i = 0; i < 5; i++)
            takenCells[i] = new int[5];
        while (!HasOneEmptyCell(takenCells))
        {
            List<int> path = new List<int>();
            int rndx = UnityEngine.Random.Range(0, takenCells.Length);
            int rndy = UnityEngine.Random.Range(0, takenCells[rndx].Length);
            while (takenCells[rndx][rndy] != 0)
            {
                rndx = UnityEngine.Random.Range(0, takenCells.Length);
                rndy = UnityEngine.Random.Range(0, takenCells[rndx].Length);
            }
            takenCells[rndx][rndy] = botCt + 1;
            path.Add(rndx * 5 + rndy);
            while (path.Count != 5 && !HasOneEmptyCell(takenCells))
            {
                List<int[]> choices = GetValidCells(takenCells, botCt + 1);
                if (choices.Count > 0)
                {
                    int[] choice = choices.PickRandom();
                    takenCells[choice[0]][choice[1]] = botCt + 1;
                    path.Add(choice[0] * 5 + choice[1]);
                    if (path.Count != 1 && UnityEngine.Random.Range(0, 4) == 0)
                        break;
                }
                else
                    break;
            }
            robotPaths.Add(path);
            botCt++;
        }
        for (int i = 0; i < takenCells.Length; i++)
        {
            bool die = false;
            for (int j = 0; j < takenCells[i].Length; j++)
            {
                if (takenCells[i][j] == 0)
                {
                    emptyCell = i * 5 + j;
                    j = takenCells[i].Length;
                    die = true;
                }
            }
            if (die)
                break;
        }
        if (emptyCell == 12)
        {
            robotPaths.Clear();
            goto regen;
        }
        correctInputs = allInputs[emptyCell];
        for (int i = 0; i < botCt; i++)
        {
            int color = UnityEngine.Random.Range(0, robotSprites.Length);
            while (chosenBotSprites.Contains(color))
                color = UnityEngine.Random.Range(0, robotSprites.Length);
            chosenBotSprites.Add(color);
            GameObject robot = Instantiate(spriteObjTemplate, grid);
            robot.transform.localPosition = gridPositions[robotPaths[i][0]].localPosition;
            robot.GetComponent<SpriteRenderer>().sprite = robotSprites[chosenBotSprites[i]];
            spriteObjs.Add(robot);
            if (robotPaths[i].Count != 1)
                StartCoroutine(FollowPath(i));
        }
        for (int i = 0; i < robotPaths.Count; i++)
        {
            string log = "The " + robotSprites[chosenBotSprites[i]].name + " robot is traversing: ";
            for (int j = 0; j < robotPaths[i].Count; j++)
            {
                log += "ABCDE"[robotPaths[i][j] % 5].ToString() + (robotPaths[i][j] / 5 + 1);
                if (j != robotPaths[i].Count - 1)
                    log += ", ";
            }
            Debug.LogFormat("[Robo-Scanner #{0}] {1}", moduleId, log);
        }
        Debug.LogFormat("[Robo-Scanner #{0}] The cell not traversed by any robot is: {1}", moduleId, "ABCDE"[emptyCell % 5].ToString() + (emptyCell / 5 + 1));
        Debug.LogFormat("[Robo-Scanner #{0}] The correct sequence of inputs is: {1}", moduleId, correctInputs);
    }

    void Activate()
    {
        speedText.text = "1";
        grid.transform.localPosition = new Vector3(UnityEngine.Random.Range(-.3f, .3f), 0, UnityEngine.Random.Range(-.3f, .3f));
        for (int i = 0; i < spriteObjs.Count; i++)
            spriteObjs[i].SetActive(true);
        activated = true;
    }

    void PressButton(KMSelectable pressed)
    {
        if (moduleSolved != true && activated != false)
        {
            int index = Array.IndexOf(buttons, pressed);
            if (index != 4 && index != 5)
            {
                pressed.AddInteractionPunch(.75f);
                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
                if (!submitMode)
                {
                    heldBtn = index;
                    holdingCo = StartCoroutine(MoveScreen(index));
                    audioRef = audio.PlaySoundAtTransformWithRef("scanning", transform);
                }
                else
                {
                    submitText.text += "UDLR"[index];
                    if (submitText.text.Length == 4 && submitText.text == correctInputs)
                    {
                        moduleSolved = true;
                        Debug.LogFormat("[Robo-Scanner #{0}] Submitted the sequence {1}, which is correct. Module solved!", moduleId, submitText.text);
                        GetComponent<KMBombModule>().HandlePass();
                        audio.PlaySoundAtTransform("solve", transform);
                    }
                    else if (submitText.text.Length == 4 && submitText.text != correctInputs)
                    {
                        Debug.LogFormat("[Robo-Scanner #{0}] Submitted the sequence {1}, which is incorrect. Strike!", moduleId, submitText.text);
                        GetComponent<KMBombModule>().HandleStrike();
                        submitMode = false;
                        speedText.text = speedSetting.ToString();
                        grid.gameObject.SetActive(true);
                        submitText.text = "";
                    }
                }
            }
            else if (index == 4 && !submitMode)
            {
                pressed.AddInteractionPunch(.75f);
                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
                speedSetting++;
                if (speedSetting > 3)
                    speedSetting = 1;
                speedText.text = speedSetting.ToString();
            }
            else
            {
                pressed.AddInteractionPunch(.75f);
                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
                submitMode = !submitMode;
                if (submitMode)
                {
                    speedText.text = "";
                    grid.gameObject.SetActive(false);
                }
                else
                {
                    speedText.text = speedSetting.ToString();
                    grid.gameObject.SetActive(true);
                    submitText.text = "";
                }
            }
        }
    }

    void ReleaseButton(KMSelectable released)
    {
        if (moduleSolved != true && holdingCo != null)
        {
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, released.transform);
            audioRef.StopSound();
            StopCoroutine(holdingCo);
            holdingCo = null;
        }
    }

    List<int[]> GetValidCells(int[][] grid, int num)
    {
        List<int[]> cells = new List<int[]>();
        for (int i = 0; i < grid.Length; i++)
        {
            for (int j = 0; j < grid[i].Length; j++)
            {
                if (grid[i][j] == num)
                {
                    if (i + 1 != grid.Length)
                    {
                        if (grid[i + 1][j] == 0)
                            cells.Add(new int[] { i + 1, j });
                    }
                    if (i - 1 != -1)
                    {
                        if (grid[i - 1][j] == 0)
                            cells.Add(new int[] { i - 1, j });
                    }
                    if (j + 1 != grid[i].Length)
                    {
                        if (grid[i][j + 1] == 0)
                            cells.Add(new int[] { i, j + 1 });
                    }
                    if (j - 1 != -1)
                    {
                        if (grid[i][j - 1] == 0)
                            cells.Add(new int[] { i, j - 1 });
                    }
                }
            }
        }
        return cells;
    }

    bool HasOneEmptyCell(int[][] grid)
    {
        int zeroCt = 0;
        for (int i = 0; i < grid.Length; i++)
            for (int j = 0; j < grid[i].Length; j++)
                if (grid[i][j] == 0)
                    zeroCt++;
        if (zeroCt == 1)
            return true;
        return false;
    }

    IEnumerator MoveScreen(int dir)
    {
        while (true)
        {
            yield return null;
            if (dir == 0 && grid.localPosition.z > -.3f)
                grid.localPosition += new Vector3(0, 0, -0.0008f * speedSetting);
            else if (dir == 1 && grid.localPosition.z < .3f)
                grid.localPosition += new Vector3(0, 0, 0.0008f * speedSetting);
            else if (dir == 2 && grid.localPosition.x < .3f)
                grid.localPosition += new Vector3(0.0008f * speedSetting, 0, 0);
            else if (dir == 3 && grid.localPosition.x > -.3f)
                grid.localPosition += new Vector3(-0.0008f * speedSetting, 0, 0);
        }
    }

    IEnumerator FollowPath(int robot)
    {
        int curPos = 0;
        bool posDir = true;
        while (true)
        {
            Vector3 origPos = gridPositions[robotPaths[robot][curPos]].localPosition;
            Vector3 newPos = gridPositions[robotPaths[robot][posDir ? (curPos + 1) : (curPos - 1)]].localPosition;
            float t = 0f;
            while (t < 2f)
            {
                yield return null;
                t += Time.deltaTime;
                spriteObjs[robot].transform.localPosition = Vector3.Lerp(origPos, newPos, t);
            }
            if (posDir)
                curPos++;
            else
                curPos--;
            if (curPos == robotPaths[robot].Count - 1)
                posDir = false;
            else if (curPos == 0)
                posDir = true;
        }
    }

    //twitch plays
    bool TwitchShouldCancelCommand;
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} move <up/left/right/down> <#> [Moves the scanner in the specified direction for '#' seconds] | !{0} speed [Changes the scanner speed setting] | !{0} submit [Presses the submission toggle button] | !{0} press <up/left/right/down> [Presses the arrow button in the specified direction] | Arrow presses can be chained and directions can be substituted with their first letter";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[5].OnInteract();
            yield break;
        }
        if (Regex.IsMatch(command, @"^\s*speed\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (submitMode)
            {
                yield return "sendtochaterror The speed setting cannot be changed when in submission mode!";
                yield break;
            }
            yield return null;
            buttons[4].OnInteract();
            yield break;
        }
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*move\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify a direction and a time!";
            else if (parameters.Length == 2 && parameters[1].ToUpperInvariant().EqualsAny("UP", "DOWN", "LEFT", "RIGHT", "U", "D", "L", "R"))
                yield return "sendtochaterror Please specify a time!";
            else if (parameters.Length == 2)
                yield return "sendtochaterror!f The specified direction '" + parameters[1] + "' is invalid!";
            else if (parameters.Length > 3)
                yield return "sendtochaterror Too many parameters!";
            else
            {
                if (!parameters[1].ToUpperInvariant().EqualsAny("UP", "DOWN", "LEFT", "RIGHT", "U", "D", "L", "R"))
                {
                    yield return "sendtochaterror!f The specified direction '" + parameters[1] + "' is invalid!";
                    yield break;
                }
                float temp = -1;
                if (!float.TryParse(parameters[2], out temp))
                {
                    yield return "sendtochaterror!f The specified time '" + parameters[2] + "' is invalid!";
                    yield break;
                }
                if (temp <= 0)
                {
                    yield return "sendtochaterror The specified time '" + parameters[2] + "' is invalid!";
                    yield break;
                }
                if (submitMode)
                {
                    yield return "sendtochaterror You cannot move the scanner when in submission mode!";
                    yield break;
                }
                yield return null;
                switch (parameters[1].ToUpperInvariant())
                {
                    case "UP":
                    case "U":
                        buttons[0].OnInteract();
                        break;
                    case "DOWN":
                    case "D":
                        buttons[1].OnInteract();
                        break;
                    case "LEFT":
                    case "L":
                        buttons[2].OnInteract();
                        break;
                    case "RIGHT":
                    default:
                        buttons[3].OnInteract();
                        break;
                }
                float t = 0f;
                while (t < temp)
                {
                    t += Time.deltaTime;
                    yield return null;
                    if (TwitchShouldCancelCommand)
                        break;
                }
                buttons[heldBtn].OnInteractEnded();
                if (TwitchShouldCancelCommand)
                    yield return "cancelled";
            }
            yield break;
        }
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 1)
                yield return "sendtochaterror Please specify at least one direction!";
            else
            {
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (!parameters[i].ToUpperInvariant().EqualsAny("UP", "DOWN", "LEFT", "RIGHT", "U", "D", "L", "R"))
                    {
                        yield return "sendtochaterror!f The specified direction '" + parameters[i] + "' is invalid!";
                        yield break;
                    }
                }
                if (!submitMode)
                {
                    yield return "sendtochaterror Arrow buttons cannot be pressed when not in submission mode!";
                    yield break;
                }
                yield return null;
                for (int i = 1; i < parameters.Length; i++)
                {
                    switch (parameters[i].ToUpperInvariant())
                    {
                        case "UP":
                        case "U":
                            buttons[0].OnInteract();
                            break;
                        case "DOWN":
                        case "D":
                            buttons[1].OnInteract();
                            break;
                        case "LEFT":
                        case "L":
                            buttons[2].OnInteract();
                            break;
                        case "RIGHT":
                        default:
                            buttons[3].OnInteract();
                            break;
                    }
                    yield return new WaitForSeconds(.1f);
                }
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!activated) yield return true;
        if (holdingCo != null)
        {
            buttons[heldBtn].OnInteractEnded();
            yield return new WaitForSeconds(.1f);
        }
        if (!submitMode)
        {
            buttons[5].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
        else
        {
            for (int i = 0; i < submitText.text.Length; i++)
            {
                if (submitText.text[i] != correctInputs[i])
                {
                    buttons[5].OnInteract();
                    yield return new WaitForSeconds(.1f);
                    buttons[5].OnInteract();
                    yield return new WaitForSeconds(.1f);
                    break;
                }
            }
        }
        int start = submitText.text.Length;
        for (int i = start; i < 4; i++)
        {
            buttons["UDLR".IndexOf(correctInputs[i])].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }
}