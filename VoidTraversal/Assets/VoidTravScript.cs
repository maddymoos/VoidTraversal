using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using wawa.DDL;
using UnityEngine.XR.WSA;
using JetBrains.Annotations;
using System.Net.Configuration;

public class VoidTravScript : MonoBehaviour
{

    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Bomb;

    public KMSelectable ModSelectable;
    public KMSelectable LocationButton;
    public KMSelectable[] DirectionButtons;

    public GameObject StatusLightObj;

    public AudioSource ambience;

    public TextMesh DisplayColor;
    public TextMesh DisplayText;

    public Transform[] PortalLayers;
    public GameObject Portal;
    public GameObject LocationDisplay;

    public Material[] PortalMats;

    public Light PortalLight;

    public GameObject[] Backgrounds;
    public Transform Rotator;
    private float[] PortalSpin = new float[8];
    private float bgOffset;

    private float ambVol = 0;

    public Color[] Colors;

    static int moduleIdCounter = 1;
    int moduleId;

    private static int[] Xdir = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static int[] Ydir = { -1, -1, 0, 1, 1, 1, 0, -1 };

    public bool Started, Ready;

    private bool Selected = false, Opening = false, PortalClosed = false, PortalOpen = false, Focused = false, Struck = false, Solved = false, OpeningPortal = false, EnteringVoid = false;
    private string GoalLocation;
    private int GoalCol, GoalRow, CurrCol, CurrRow, CurrRot, MoveCount, AmbientTimer = 300;
    private float a = .05f, v, r;
    private static string[][] Map = {
        new string[]{"M7", "C5", "M4", "R6", "K7", "G0", "K3", "Y7"},
        new string[]{"R3", "K0", "W1", "M2", "R7", "Y5", "M6", "K2"},
        new string[]{"Y1","C3","R4","C1","Y2","W4","G3","W0"},
        new string[]{"Y6","K5","M0","W2","R5","M3","B6","G2"},
        new string[]{"B0","K4","M5","R1","B5","M1","Y4","B4"},
        new string[]{"G5","C6","G1","C2","R2","W7","B2","K6"},
        new string[]{"W3","K1","G6","B1","R0","W6","B7","G4"},
        new string[]{"W5","Y3","Y0","C0","C4","B3","C7","G7"},
    };
    private static string[] dirNames = { "forwards", "right", "backwards", "left" };
    private static string[] compNames = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
    private static string colors = "KRGYBMCW";
    private static string[] ColorNames = { "black", "red", "green", "yellow", "blue", "magenta", "cyan", "white" };

    private Vector3[] BackgroundScales = new Vector3[2]
    {
        new Vector3(1, 1, 1),
        new Vector3(0.2f, 0.05f, 0.2f)
    };

    void Awake()
    {
        float scalar = transform.lossyScale.x;
        PortalLight.range *= scalar;

        PortalLayers[4].localScale = new Vector3(0f, .0001f, .125f);
        LocationDisplay.SetActive(false);
        Portal.SetActive(false);
        moduleId = moduleIdCounter++;
        ModSelectable.OnFocus += delegate ()
        {
            if (!Selected && !OpeningPortal)
            {
                OpeningPortal = true;
                StartCoroutine(OpenPortal());
            }
            Focused = true;
        };
        ModSelectable.OnDefocus += delegate ()
        {
            Focused = false;
        };
        LocationButton.OnInteract += delegate ()
        {
            if (Started && Ready && !Solved)
            {
                Ready = false;
                SubmitCoord();
            }
            else if (Ready && !Solved)
            {
                EnteringVoid = true;
                Ready = false;
                StartCoroutine(EnterTheVoid());
            }
            return false;
        };
        for (byte i = 0; i < 4; i++)
        {
            byte j = i;
            DirectionButtons[j].OnInteract += delegate ()
            {
                if (Started && Ready && !Solved)
                {
                    PlayerMove(j);
                }
                return false;
            };
        }
    }
    // Use this for initialization
    void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            PortalSpin[i] = Rnd.Range(0.05f, .5f);
        }
        StartCoroutine(PortalSpinner());
        InitModule();
    }

    // Update is called once per frame


    void InitModule()
    {
        Backgrounds[0].transform.localScale = BackgroundScales[0];
        Backgrounds[1].transform.localScale = new Vector3(0, 0, 0);
        ambVol = 0;
        bgOffset = Rnd.Range(0f, 1f);
        Selected = false;
        Ready = false;
        GoalCol = Rnd.Range(0, 8);
        GoalRow = Rnd.Range(0, 8);
        GoalLocation = Map[GoalRow][GoalCol];

        CurrCol = Rnd.Range(0, 8);
        CurrRow = Rnd.Range(0, 8);
        CurrRot = Rnd.Range(0, 8);
        MoveCount = 0;
        Rotator.transform.localEulerAngles = new Vector3();
        Rotator.transform.localPosition = new Vector3(0, .12f, 0);
        LocationDisplay.transform.localPosition = new Vector3(0, .009f, 0);

        Debug.LogFormat("[Void Traversal #{0}]: It's time to pay back your bet. Come visit me at {1}.", moduleId, GoalLocation);
        DisplayColor.color = Colors[Array.IndexOf(colors.ToArray(), GoalLocation[0])];
        DisplayText.text = GoalLocation[1].ToString();
        if (GoalLocation[0] == 'K')
            DisplayText.color = Color.white;
        else
            DisplayText.color = Color.black;

    }

    void SubmitCoord()
    {
        if (GoalLocation == Map[CurrRow][CurrCol])
        {
            if (!Struck)
            {
                Debug.LogFormat("[Void Traversal #{0}]: Wow, you actually managed to find me. Congrats!", moduleId);
            }
            else
            {
                Debug.LogFormat("[Void Traversal #{0}]: Took you a couple tries, hehe. Congrats on finding me... *eventually*.", moduleId);
            }
            Solved = true;
            StartCoroutine(ReturnToMortalPlane());
        }
        else
        {
            Debug.LogFormat("[Void Traversal #{0}]: No, doofus! I'm not at {1}! I'm at {2}!", moduleId, Map[CurrRow][CurrCol], GoalLocation);
            // play laugh
            Struck = true;
            StartCoroutine(ReturnToMortalPlane());
        }
    }

    void PlayerMove(int Direction)
    {
        Audio.PlaySoundAtTransform("walk0" + Rnd.Range(1, 10), Portal.transform);
        Ready = false;
        bool Num = Rnd.Range(0, 2) == 0;
        CurrRot += Direction * 2;
        CurrRot %= 8;
        MoveCount++;
        string[] str = new string[MoveCount];
        Debug.LogFormat("[Void Traversal #{0}]: You're choosing to move {1} from {2}. That'll take you {3}.", moduleId, dirNames[Direction], Map[CurrRow][CurrCol], compNames[CurrRot]);
        int x = CurrCol;
        int y = CurrRow;
        int Dir = CurrRot;
        int sum = 0;
        for (int a = 0; a < MoveCount; a++)
        {
            x = (x + Xdir[Dir]);
            y = (y + Ydir[Dir]);
            if (x == 8 || y == 8 || x == -1 || y == -1)
            {
                if (x == y || (x == 8 && y == -1) || (x == -1 && y == -8))
                {
                    if (x == -1) x = 0;
                    if (y == -1) y = 0;
                    if (x == 8) x = 7;
                    if (y == 8) y = 7;

                    Dir = (Dir + 4) % 8;
                }
                if (x == 8 || x == -1) Dir = (8 - Dir) % 8;
                if (y == 8 || y == -1) Dir = (12 - Dir) % 8;
                if (x == -1) x = 0;
                if (y == -1) y = 0;
                if (x == 8) x = 7;
                if (y == 8) y = 7;
            }
            if (Num)
            {
                sum += int.Parse(Map[y][x][1].ToString());
                sum %= 8;
            }
            else
            {
                sum ^= Array.IndexOf(colors.ToArray(), Map[y][x][0]);
            }
            if (a != 0)
                str[a] = Map[y][x];

            if (a == 0)
            {
                Dir = (Dir + 1) % 8;
                Debug.LogFormat("[Void Traversal #{0}]: You step onto {1}, before The Void spins you, making you face {2}!", moduleId, Map[y][x], compNames[Dir]);
            }
        }
        if (FixMyFormattingPlease(str) != "")
            Debug.LogFormat("[Void Traversal #{0}]: The Void whisks you past {1}.", moduleId, FixMyFormattingPlease(str));

        CurrCol = x;
        CurrRow = y;
        CurrRot = Dir;
        Debug.LogFormat("[Void Traversal #{0}]: You end up on {1} (It's showing {3}). You're facing {2}.", moduleId, Map[y][x], compNames[CurrRot], Num ? sum.ToString() : ColorNames[sum]);
        StartCoroutine(SwitchLocation(Num, sum));
    }

    string FixMyFormattingPlease(string[] str)
    {
        string fix = "";
        if (str.Length == 2)
        {
            return str[1];
        }
        for (int i = 1; i < str.Length; i++)
        {
            fix += str[i];
            if (i < str.Length - 2)
            {
                fix += ", ";
            }
            else
            {
                fix += " and ";
                fix += str[i + 1];
                break;
            }
        }
        return fix;
    }

    void FixedUpdate()
    {
        bgOffset -= .0001f;
        if (PortalOpen)
        {
            Backgrounds[1].GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(bgOffset + .4f, 0);
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(bgOffset, 0);
        }
        if (Started && Focused)
        {
            AmbientTimer--;
            if (AmbientTimer == 0)
            {
                AmbientTimer = Rnd.Range(300, 600);
                Audio.PlaySoundAtTransform("ambience0" + Rnd.Range(1, 9), Portal.transform);
            }

        }
        if (!Focused || !Started)
        {
            ambVol -= .1f;
            if (ambVol < 0)
                ambVol = 0;
            AmbientTimer = 150;
        }
        else
        {
            ambVol += .01f;
            if (ambVol > 1)
                ambVol = 1;
        }
        ambience.volume = (wawa.DDL.Preferences.Sound / 500f) * ambVol;
    }

    IEnumerator PortalSpinner()
    {
        r = Rnd.Range(-25f, 25f);
        while (true)
        {
            if (!PortalClosed)
            {
                if (Application.isEditor)
                    Rotator.transform.rotation = new Quaternion(0, 0, 0, 1);
                else
                    Rotator.transform.rotation = new Quaternion(-0.6f, 0.0f, 0.0f, 0.8f);
            }
            for (int i = 0; i < 4; i++)
            {
                PortalSpin[i + 4] += Opening ? 5 * PortalSpin[i] : PortalSpin[i];
                PortalLayers[i].localEulerAngles = new Vector3(90, PortalSpin[i + 4], 0);
            }
            if (r * a > 1)
            {
                a = -a;
            }
            v += a / 5;
            if (v > 1f)
            {
                v = 1f;
            }
            if (v < -1f)
            {
                v = -1f;
            }
            r += v / 5;
            LocationDisplay.transform.localEulerAngles = new Vector3(0, r, 0);
            yield return null;
        }
    }
    IEnumerator OpenPortal()
    {
        Selected = true;
        yield return new WaitForSeconds(.15f);

        yield return new WaitForSeconds(.5f);
        Audio.PlaySoundAtTransform("PortalOpen", Portal.transform);
        Audio.PlaySoundAtTransform("VoidAmbi", Portal.transform);
        float i = 0;
        Portal.SetActive(true);
        Opening = true;
        PortalClosed = false;

        //LERP 1 - PORTAL OPEN
        while (i < 1)
        {
            PortalLayers[4].localScale = Vector3.Lerp(new Vector3(0f, .001f, .125f), new Vector3(.125f, .001f, .125f), i * i);
            Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(-(i * i) / 2.5f, -1);
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2((-1 + i * i) / 5f + bgOffset, 0);
            PortalLight.intensity = i;
            yield return null;
            i += Time.deltaTime;
        }
        PortalOpen = true;
        Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(-1 / 2.5f, -1);
        Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(bgOffset, 0);
        PortalLayers[4].localScale = new Vector3(.125f, .001f, .125f);
        Opening = false;
        yield return new WaitForSeconds(.25f);

        i = 0;
        LocationDisplay.transform.localScale = new Vector3(0, .0001f, 0);
        LocationDisplay.SetActive(true);

        // LERP 2 - Spawn Location
        while (i < 1)
        {
            LocationDisplay.transform.localScale = Vector3.Lerp(new Vector3(0f, .0001f, 0), new Vector3(.05f, .0001f, .05f), Easing.InOutSine(i, 0, 1, 1));
            yield return null;
            i += Time.deltaTime;
        }
        LocationDisplay.transform.localScale = new Vector3(.05f, .0001f, .05f);
        yield return new WaitForSeconds(1f);
        Audio.PlaySoundAtTransform("PortalClose", Portal.transform);

        i = 0;
        Opening = true;
        PortalOpen = false;
        // LERP 3 - Close Portal
        while (i < 1)
        {
            PortalLayers[4].localScale = Vector3.Lerp(new Vector3(.125f, .001f, .125f), new Vector3(0f, .001f, .125f), i * i);
            Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2((-1 + (i * i)) / 2.5f, -1);
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(-(i * i) / 5f + bgOffset, 0);
            PortalLight.intensity = 1 - i;
            yield return null;
            i += Time.deltaTime;
        }
        PortalLight.intensity = 0;
        Opening = false;
        Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(0, -1);
        Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(bgOffset, 0);
        PortalLayers[4].localScale = new Vector3(0f, .001f, .125f);
        Portal.SetActive(false);
        PortalClosed = true;
        PortalOpen = true;
        yield return new WaitForSeconds(.5f);
        // LERP 4 - Location stop floating dummy
        i = 0;
        while (i < 1)
        {
            LocationDisplay.transform.localPosition = Vector3.Lerp(LocationDisplay.transform.localPosition, new Vector3(0, -0.1025f, 0), i * i * i);
            Rotator.transform.localRotation = Quaternion.Lerp(Rotator.transform.localRotation, Quaternion.Euler(new Vector3(0, 0, 0)), i * i * i);
            yield return null;
            i += Time.deltaTime / 2;
        }
        Ready = true;
        OpeningPortal = false;
    }

    IEnumerator EnterTheVoid()
    {
        EnteringVoid = true;
        Ready = false;
        StartCoroutine(ScaleStatusLight(true));
        Debug.LogFormat("[Void Traversal #{0}]: You land in The Void at {1}. You're facing {2}.", moduleId, Map[CurrRow][CurrCol], compNames[CurrRot]);
        if (Map[CurrRow][CurrCol] == GoalLocation)
        {
            Debug.LogFormat("[Void Traversal #{0}]: And, unfortunately for you, that's exactly where I am, and you'd never know.", moduleId);
        }

        float i = 0;
        //lerp 0 hide locator
        while (i < 1)
        {
            LocationDisplay.transform.localScale = Vector3.Lerp(new Vector3(.05f, .0001f, .05f), Vector3.zero, i * i * i);
            i += Time.deltaTime * 2f;
            yield return null;
        }
        Portal.SetActive(true);
        Opening = true;
        Rotator.transform.localEulerAngles = new Vector3();
        Rotator.transform.localPosition = new Vector3(0, 0.025f, 0);
        Audio.PlaySoundAtTransform("PortalTravel", Portal.transform);
        //LERP 1 - PORTAL OPEN
        i = 0;
        while (i < 1)
        {
            PortalLayers[4].localScale = Vector3.Lerp(new Vector3(0f, .001f, 0f), new Vector3(.25f, .001f, .25f), i * i);
            Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(-(i * i) / 2.5f, -(i * i));
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2((-1 + i * i) / 5f + bgOffset, (-1 + i * i) / 2f);
            PortalLight.intensity = 5 * i;
            yield return null;
            i += Time.deltaTime * 2;
        }
        PortalOpen = true;
        Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(-1 / 2.5f, -1);
        Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(bgOffset, 0);
        PortalLayers[4].localScale = new Vector3(.25f, .001f, .25f);

        Backgrounds[0].transform.localScale = new Vector3(0, 0, 0);
        Backgrounds[1].transform.localScale = BackgroundScales[1];
        yield return new WaitForSeconds(.75f);
        i = 0;
        Started = true;
        PortalOpen = false;
        //LERP 2 portal close
        while (i < 1)
        {
            PortalLayers[4].localScale = Vector3.Lerp(new Vector3(.25f, .001f, .25f), new Vector3(0f, .001f, 0f), i * i);
            Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2((-1 + (i * i)) / 2.5f, -1 + (i * i));
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(-(i * i) / 5f + bgOffset, -(i * i) / 2f);
            PortalLight.intensity = 5 - 5 * i;
            yield return null;
            i += Time.deltaTime * 2;
        }
        PortalOpen = true;
        PortalLight.intensity = 0;
        LocationDisplay.SetActive(false);
        Portal.SetActive(false);
        Rotator.transform.localPosition = new Vector3(0, 0.12f, 0);
        PortalLayers[4].localScale = new Vector3(0, .001f, 0);
        Opening = false;
        //Intermission Change The Locator To Player Pos NOW
        if (Rnd.Range(0, 2) == 0)
        {

            DisplayColor.color = Colors[Array.IndexOf(colors.ToArray(), Map[CurrRow][CurrCol][0])];
            DisplayText.text = "";
            DisplayText.color = Color.white;
        }
        else
        {
            DisplayText.text = Map[CurrRow][CurrCol][1].ToString();
            DisplayColor.color = Color.black;
            DisplayText.color = Color.white;
        }

        yield return new WaitForSeconds(.5f);
        //lerp 3 locator back
        LocationDisplay.SetActive(true);
        i = 0;
        while (i < 1)
        {
            LocationDisplay.transform.localScale = Vector3.Lerp(Vector3.zero, new Vector3(.05f, .0001f, .05f), i * i * i);
            i += Time.deltaTime * 2f;
            yield return null;
        }
        LocationDisplay.transform.localScale = new Vector3(.05f, .0001f, .05f);
        Ready = true;
        EnteringVoid = false;
    }

    IEnumerator SwitchLocation(bool Num, int thing)
    {
        Ready = false;
        yield return null;
        float i = 0;
        while (i < 1)
        {
            LocationDisplay.transform.localScale = Vector3.Lerp(new Vector3(.05f, .0001f, .05f), Vector3.zero, i * i * i);
            i += Time.deltaTime * 2f;
            bgOffset -= Time.deltaTime / 69f;
            yield return null;
        }
        LocationDisplay.SetActive(false);
        if (!Num)
        {
            DisplayColor.color = Colors[thing];
            DisplayText.text = "";
            DisplayText.color = Color.white;
        }
        else
        {
            DisplayText.text = thing.ToString();
            DisplayColor.color = Color.black;
            DisplayText.color = Color.white;
        }
        LocationDisplay.SetActive(true);
        i = 0;
        while (i < 1)
        {
            LocationDisplay.transform.localScale = Vector3.Lerp(Vector3.zero, new Vector3(.05f, .0001f, .05f), i * i * i);
            i += Time.deltaTime * 2f;
            bgOffset -= Time.deltaTime / 69f;
            yield return null;
        }
        Ready = true;
        LocationDisplay.transform.localScale = new Vector3(.05f, .0001f, .05f);
    }

    IEnumerator ReturnToMortalPlane()
    {
        Ready = false;
        Portal.GetComponent<MeshRenderer>().material = PortalMats[1];
        //Knock wait
        float i = 0;
        while (i < 1)
        {
            LocationDisplay.transform.localScale = Vector3.Lerp(new Vector3(.05f, .0001f, .05f), Vector3.zero, i * i * i);
            i += Time.deltaTime * 2f;
            yield return null;
        }
        LocationDisplay.SetActive(false);

        // DIALOGUE
        yield return new WaitForSeconds(.25f);
        if (Solved)
        {
            if (!Struck)
            {
                Audio.PlaySoundAtTransform("supersolve", Portal.transform);
                yield return new WaitForSeconds(.75f);

            }
            Audio.PlaySoundAtTransform("solve0", Portal.transform);
            Module.HandlePass();
            yield return new WaitForSeconds(.75f);
        }
        else
        {
            Audio.PlaySoundAtTransform("strike0", Portal.transform);
            Module.HandleStrike();
            yield return new WaitForSeconds(.5f);
            Audio.PlaySoundAtTransform("strikelaugh", Portal.transform);
            yield return new WaitForSeconds(.25f);

        }

        //Wait for dialogue


        Portal.SetActive(true);
        Opening = true;
        Rotator.transform.localEulerAngles = new Vector3();
        Rotator.transform.localPosition = new Vector3(0, 0.025f, 0);
        Audio.PlaySoundAtTransform("PortalExit", Portal.transform);
        yield return new WaitForSeconds(.25f);
        // LERP 1: Open Portal Big
        i = 0;
        while (i < 1)
        {
            PortalLayers[4].localScale = Vector3.Lerp(new Vector3(0f, .001f, 0f), new Vector3(.25f, .001f, .25f), i * i);
            Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(-(i * i), -(i * i));
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2((-1 + i * i) / 2f, (-1 + i * i) / 2f);
            PortalLight.intensity = i;
            if (i > .875f)
            {
                Backgrounds[0].transform.localScale = BackgroundScales[0];
                Backgrounds[1].transform.localScale = new Vector3(0, 0, 0);
                PortalOpen = false;
            }
            yield return null;
            i += Time.deltaTime * .75f;
        }
        Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(-1, -1);
        Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(0, 0);
        PortalLayers[4].localScale = new Vector3(.25f, .001f, .25f);
        Started = false;
        yield return new WaitForSeconds(.75f);
        i = 0;
        //LERP 2 portal close
        while (i < 1)
        {
            PortalLayers[4].localScale = Vector3.Lerp(new Vector3(.25f, .001f, .25f), new Vector3(0f, .001f, 0f), i * i);
            Portal.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2((-1 + (i * i)), -1 + (i * i));
            Portal.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(-(i * i) / 2f, -(i * i) / 2f);
            PortalLight.intensity = 1 - i;
            yield return null;
            i += Time.deltaTime;
        }
        PortalOpen = true;
        Portal.SetActive(false);
        StartCoroutine(ScaleStatusLight(false));
        yield return new WaitForSeconds(.25f);
        Portal.GetComponent<MeshRenderer>().material = PortalMats[0];
        if (!Solved)
        {
            InitModule();
        }
    }

    private IEnumerator ScaleStatusLight(bool enteringVoid)
    {
        var duration = 0.5f;
        var elapsed = 0f;
        var startY = enteringVoid ? 1f : 0.1f;
        var goalY = enteringVoid ? 0.1f : 1f;
        while (elapsed < duration)
        {
            StatusLightObj.transform.localScale = new Vector3(1f, Mathf.Lerp(startY, goalY, elapsed / duration), 1f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        StatusLightObj.transform.localScale = new Vector3(1f, goalY, 1f);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} select (selects the module) !{0} urld (move up/right/left/backwards) !{0} enter (enter the void) !{0} submit (submit the current location)";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if ((m = Regex.Match(command, @"^\s*((select)|(submit)|(enter)|([urld]+))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            yield return null;
            var input = m.Groups[1].Value.ToLowerInvariant();
            string s = "urdl";
            switch (input)
            {
                case "select":
                    ModSelectable.OnFocus();
                    yield return new WaitForSeconds(.5f);
                    ModSelectable.OnDefocus();
                    break;
                case "submit":
                    if (!Started)
                    {
                        yield return "sendtochaterror Silly player... you can't knock on my door! You aren't even in The Void!";
                        break;
                    }
                    if (!Ready)
                    {
                        yield return "sendtochaterror Slow down! You're in too much of a rush.";
                        break;
                    }
                    else
                    {
                        LocationButton.OnInteract();
                    }
                    break;
                case "enter":
                    if (Started)
                    {
                        yield return "sendtochaterror Silly player... you can't enter The Void! You're already here!";
                        break;
                    }
                    if (!Ready)
                    {
                        yield return "sendtochaterror Slow down! You're in too much of a rush.";
                        break;
                    }
                    else
                    {
                        LocationButton.OnInteract();
                    }
                    break;
                default:
                    if (!Ready || !Started)
                    {
                        yield return "sendtochaterror Slow down! You're in too much of a rush.";
                        break;
                    }
                    for (int i = 0; i < input.Length; i++)
                    {

                        DirectionButtons[Array.IndexOf(s.ToArray(), input[i])].OnInteract();
                        yield return new WaitForSeconds(1.25f);
                    }
                    break;
            }
        }
        else
        {
            yield return "sendtochaterror Unknown command. Use either select, submit, enter, or u/r/l/d.";
            yield break;
        }
    }

    public class PositionItem
    {
        public int XCol;
        public int YRow;
        public int StepCount;
        public int Rotation;

        public PositionItem(int x, int y, int sc, int r)
        {
            XCol = x;
            YRow = y;
            StepCount = sc;
            Rotation = r;
        }
    }

    public struct QueueItem
    {
        public PositionItem Item;
        public PositionItem Parent;
        public int Action;

        public QueueItem(PositionItem i, PositionItem p, int r)
        {
            Item = i;
            Parent = p;
            Action = r;
        }
    }

    public struct StateKey
    {
        public int X, Y, Rotation, Step;

        public StateKey(int x, int y, int rot, int step)
        {
            X = x;
            Y = y;
            Rotation = rot;
            Step = step;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (!Selected && !OpeningPortal)
        {
            ModSelectable.OnFocus();
            yield return null;
            ModSelectable.OnDefocus();

            while (!Ready)
                yield return true;
        }
        if (!Started && !EnteringVoid)
        {
            while (!Ready)
                yield return true;

            LocationButton.OnInteract();
            yield return null;
        }
        while (!Started || !Ready || EnteringVoid)
            yield return true;

        var currentItem = new PositionItem(CurrCol, CurrRow, MoveCount, CurrRot);

        var visited = new Dictionary<StateKey, QueueItem>();
        var q = new Queue<QueueItem>();

        q.Enqueue(new QueueItem(currentItem, null, 0));
        PositionItem found = null;
        while (q.Count > 0)
        {
            var qi = q.Dequeue();
            var key = new StateKey(qi.Item.XCol, qi.Item.YRow, qi.Item.Rotation, qi.Item.StepCount);
            if (visited.ContainsKey(key))
                continue;
            visited[key] = qi;
            if (qi.Item.XCol == GoalCol && qi.Item.YRow == GoalRow)
            {
                found = qi.Item;
                break;
            }

            for (int i = 0; i < 4; i++)
                q.Enqueue(new QueueItem(GetPositionItem(qi.Item, i), qi.Item, i));
        }
        if (found == null)
        {
            Debug.LogError("Autosolver failed to find a path.");
            yield break;
        }
        var r = found;
        var path = new List<int>();
        while (true)
        {
            var key = new StateKey(r.XCol, r.YRow, r.Rotation, r.StepCount);
            var nr = visited[key];
            if (nr.Parent == null)
                break;
            path.Add(nr.Action);
            r = nr.Parent;
        }

        for (int i = path.Count - 1; i >= 0; i--)
        {
            DirectionButtons[path[i]].OnInteract();
            while (!Ready)
                yield return null;
        }
        LocationButton.OnInteract();
        while (!Solved)
            yield return true;
    }

    PositionItem GetPositionItem(PositionItem item, int Direction)
    {
        PositionItem newItem = new PositionItem(item.XCol, item.YRow, item.StepCount, item.Rotation);

        newItem.Rotation += Direction * 2;
        newItem.Rotation %= 8;

        newItem.StepCount++;

        int x = newItem.XCol;
        int y = newItem.YRow;
        int dir = newItem.Rotation;

        for (int a = 0; a < newItem.StepCount; a++)
        {
            x = (x + Xdir[dir]);
            y = (y + Ydir[dir]);
            if (x == 8 || y == 8 || x == -1 || y == -1)
            {
                if (x == y || (x == 8 && y == -1) || (x == -1 && y == -8))
                {
                    if (x == -1) x = 0;
                    if (y == -1) y = 0;
                    if (x == 8) x = 7;
                    if (y == 8) y = 7;

                    dir = (dir + 4) % 8;
                }
                if (x == 8 || x == -1) dir = (8 - dir) % 8;
                if (y == 8 || y == -1) dir = (12 - dir) % 8;
                if (x == -1) x = 0;
                if (y == -1) y = 0;
                if (x == 8) x = 7;
                if (y == 8) y = 7;
            }

            if (a == 0)
                dir = (dir + 1) % 8;
        }

        newItem.XCol = x;
        newItem.YRow = y;
        newItem.Rotation = dir;

        return newItem;
    }
}
