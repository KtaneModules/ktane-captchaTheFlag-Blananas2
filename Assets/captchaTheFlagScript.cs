using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class captchaTheFlagScript : MonoBehaviour
{

    public KMBombInfo Bomb;
    public KMAudio Audio;

    public KMSelectable[] FlagButtons;
    public KMSelectable SubmitButton;
    public GameObject[] Flags;
    public SpriteRenderer[] ForFade;
    public TextMesh StageCounter;

    public TextMesh PLACEHOLDER;
    public Sprite[] Cares;
    public SpriteRenderer[] Who;
    public Sprite Empty;

    string charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    int[] sinis = { 0, 3, 2, 1, 0, 4, 4, 4, 2, 3, 3, 2, 1, 0, 4, 4, 4, 2, 3, 0, 3, 3, 3, 3, 2, 2, 2, 2, 2, 1, 1, 0, 7, 7, 1, 5 };
    int[] dextr = { 2, 4, 4, 4, 4, 1, 2, 3, 5, 7, 4, 4, 4, 4, 1, 2, 3, 5, 7, 2, 0, 1, 2, 3, 7, 0, 1, 2, 3, 0, 1, 3, 2, 3, 2, 2 };
    int[] funny = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 };
    int[] flagPos = { 0, 0 };
    string[] words = { "F1AG", "P0LE" };
    string cap = "";
    string ser = "";
    int stage = 0;
    int[] vertiP = { 4, 3, 2, 1, 0, 7, 6, 5 };
    int[] horizP = { 0, 7, 6, 5, 4, 3, 2, 1 };
    int[] desired = { -3, -3, -3, -3, -3, -3, -3, -3, -3, -3, -3, -3 };
    int held = -1;
    bool hidden = false;
    string[] dirNames = { "North", "North-West", "West", "South-West", "South", "South-East", "East", "North-East",
                          "North", "North-East", "East", "South-East", "South", "South-West", "West", "North-West" };

    private Coroutine buttonHold;
    private bool holding = false;

    private Coroutine fading;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;

        for (int i = 0; i < FlagButtons.Length; i++)
        {
            FlagButtons[i].OnInteract += FlagPress(i);
            FlagButtons[i].OnInteractEnded += FlagRelease(i);
        }

        SubmitButton.OnInteract += delegate () { Submit(); return false; };

    }

    // Use this for initialization
    void Start()
    {
        for (int c = 0; c < 6; c++)
        {
            cap += charset.PickRandom();
        }
        ser = Bomb.GetSerialNumber();
        GenerateCAPTCHA(cap);

        Debug.LogFormat("[Captcha the Flag #{0}] CAPTCHA is {1}, Serial Number is {2}.", moduleId, cap, ser);

        for (int h = 0; h < 6; h++)
        {
            string log = "";

            int ix = charset.IndexOf(cap[h]);
            int xi = charset.IndexOf(ser[h]);
            log = string.Format("Stage {0}: CAPTCHA character is {1}, Serial Number character is {2}. ", h + 1, charset[ix], charset[xi]);

            int leftFlag = sinis[ix];
            int rightFlag = dextr[ix];
            log += string.Format("CAPTCHA character in Semaphore is {0} by {1}. ", dirNames[leftFlag], dirNames[rightFlag + 8]);

            bool verti = words[0].Contains(cap[h]);
            bool horiz = words[1].Contains(ser[h]);

            if (verti && horiz)
            {
                int funniest = funny[ix] * funny[xi];
                log += "Both F1AG and P0LE rules applied. ";
                if (funniest == 0 || funniest % 2 == 1)
                {
                    leftFlag = -1; rightFlag = -1;
                    log += "Left button must be held.";
                }
                else
                {
                    leftFlag = -2; rightFlag = -2;
                    log += "Right button must be held.";
                }
            }
            else
            {
                if (verti)
                {
                    leftFlag = vertiP[leftFlag];
                    rightFlag = vertiP[rightFlag];
                    log += string.Format("F1AG rule applied. New Semaphore orientations are {0} by {1}.", dirNames[leftFlag], dirNames[rightFlag + 8]);
                }
                if (horiz)
                {
                    leftFlag = horizP[leftFlag];
                    rightFlag = horizP[rightFlag];
                    log += string.Format("P0LE rule applied. New Semaphore orientations are {0} by {1}.", dirNames[leftFlag], dirNames[rightFlag + 8]);
                }
            }
            desired[2 * h] = leftFlag;
            desired[2 * h + 1] = rightFlag;

            Debug.LogFormat("[Captcha the Flag #{0}] {1}", moduleId, log);
        }
    }

    void GenerateCAPTCHA(string input)
    {
        string morelog = "Chosen sprites: ";
        for (int c = 0; c < 6; c++)
        {
            int b = charset.IndexOf(input[c]);
            int d = b * 36 + UnityEngine.Random.Range(0, 36);
            Who[c].sprite = Cares[d];
            morelog = morelog + d.ToString() + " ";
        }
        Debug.LogFormat("<Captcha the Flag #{0}> {1}", moduleId, morelog);
    }

    KMSelectable.OnInteractHandler FlagPress(int f)
    {
        return delegate ()
        {
            if (moduleSolved) return false;
            StartCoroutine(TurnFlag(f));

            if (buttonHold != null)
            {
                holding = false;
                StopCoroutine(buttonHold);
                buttonHold = null;
            }

            if (held == -1)
            {
                buttonHold = StartCoroutine(HoldChecker(f));
            }
            return false;
        };
    }

    Action FlagRelease(int f)
    {
        return delegate ()
        {
            if (buttonHold != null)
            {
                StopCoroutine(buttonHold);
            }
            if (fading != null)
            {
                StopCoroutine(fading);
            }
            if (!hidden) {
                for (int z = 0; z < 3; z++)
                {
                    ForFade[z].color = Color.Lerp(Color.white, Color.clear, 0f);
                }
                held = -1;
            }
        };
    }

    void Submit()
    {
        if (moduleSolved) { return; }
        bool valid = false;
        if (desired[2 * stage] == -1)
        {
            if (hidden && held == 0)
            {
                valid = true;
                Debug.LogFormat("[Captcha the Flag #{0}] Left button held correctly.", moduleId);
            } else {
                Debug.LogFormat("[Captcha the Flag #{0}] Left button was not held correctly. Strike!", moduleId);
            }
        }
        else if (desired[2 * stage] == -2)
        {
            if (hidden && held == 1)
            {
                valid = true;
                Debug.LogFormat("[Captcha the Flag #{0}] Right button held correctly.", moduleId);
            } else {
                Debug.LogFormat("[Captcha the Flag #{0}] Right button was not held correctly. Strike!", moduleId);
            }
        }
        else if ((flagPos[0] == desired[2 * stage]) && (flagPos[1] == desired[2 * stage + 1]))
        {
            if (!hidden)
            {
                valid = true;
                Debug.LogFormat("[Captcha the Flag #{0}] Flags set to {0} by {1}, that is correct.", dirNames[flagPos[0]], dirNames[flagPos[1]]);
            } else {
                Debug.LogFormat("[Captcha the Flag #{0}] Flags set to {0} by {1}, that is incorrect. Strike!", dirNames[flagPos[0]], dirNames[flagPos[1]]);
            }
        }
        held = -1;
        hidden = false;
        for (int z = 0; z < 3; z++)
        {
            ForFade[z].color = Color.Lerp(Color.white, Color.clear, 0f);
        }

        if (valid)
        {
            stage += 1;
            StageCounter.text = stage.ToString();
            if (stage == 6)
            {
                Debug.LogFormat("[Captcha the Flag #{0}] All 6 stages complete, module solved.", moduleId);
                GetComponent<KMBombModule>().HandlePass();
                moduleSolved = true;
                Audio.PlaySoundAtTransform("solve", transform);
            } else {
                Audio.PlaySoundAtTransform("blip", transform);
            }
        }
        else
        {
            GetComponent<KMBombModule>().HandleStrike();
            /*
            PLACEHOLDER.text = cap;
            for (int a = 0; a < 6; a++) {
                Who[a].sprite = Empty;
            }
            */
        }

        if (hidden)
        {
            for (int z = 0; z < 3; z++)
            {
                ForFade[z].color = Color.white;
            }
        }
    }

    IEnumerator TurnFlag(int which)
    {
        var elapsed = 0f;
        var duration = .25f;
        var startRotation = Flags[which].transform.localRotation;
        flagPos[which] = (flagPos[which] + 1) % 8;
        var endRotation = Quaternion.Euler(90f, 0f, flagPos[which] * (45f + (-90f * which)));
        while (elapsed < duration)
        {
            Flags[which].transform.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        Flags[which].transform.localRotation = endRotation;
    }

    IEnumerator HoldChecker(int q)
    {
        yield return new WaitForSeconds(.4f);
        fading = StartCoroutine(HideStuff());
        holding = true;
        held = q;
    }

    IEnumerator HideStuff()
    {
        float delta = 0f;
        while (delta < 1)
        {
            delta += 0.33f * Time.deltaTime;
            for (int z = 0; z < 3; z++)
            {
                ForFade[z].color = Color.Lerp(Color.white, Color.clear, delta);
            }
            yield return null;
        }
        hidden = true;
    }
	
	//twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"To press the blue/red flag button in a certain amount, use the command !{0} press red/blue [1-7] | To hold the blue/red flag button, use the command !{0} hold red/blue | To submit your answer, use !{0} submit";
    #pragma warning restore 414
    
    IEnumerator ProcessTwitchCommand(string command)
    {
		string[] parameters = command.Split(' ');
        if (Regex.IsMatch(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
			yield return null;
			SubmitButton.OnInteract();
		}
		
		if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && Regex.IsMatch(parameters[1], @"^\s*red\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
			yield return null;
			if (parameters.Length != 3)
			{
				yield return "sendtochaterror Parameter length invalid. Command ignored.";
				yield break;
			}
			
			if (hidden)
			{
				yield return "sendtochaterror The semaphore is hidden. Command ignored.";
				yield break;
			}
			
			int Out;
			if (!int.TryParse(parameters[2], out Out))
			{
				yield return "sendtochaterror The number given is not valid. Command ignored.";
				yield break;
			}
			
			if (Out < 1 || Out > 7)
			{
				yield return "sendtochaterror The number given is not 1-7. Command ignored.";
				yield break;
			}
			
			for (int x = 0; x < Out; x++)
			{
				FlagButtons[0].OnInteract();
				yield return new WaitForSecondsRealtime(0.1f);
				FlagButtons[0].OnInteractEnded();
				yield return new WaitForSecondsRealtime(0.1f);
			}
			
		}
		
		if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && Regex.IsMatch(parameters[1], @"^\s*blue\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
			yield return null;
			if (parameters.Length != 3)
			{
				yield return "sendtochaterror Parameter length invalid. Command ignored.";
				yield break;
			}
			
			if (hidden)
			{
				yield return "sendtochaterror The semaphore is hidden. Command ignored.";
				yield break;
			}
			
			int Out;
			if (!int.TryParse(parameters[2], out Out))
			{
				yield return "sendtochaterror The number given is not valid. Command ignored.";
				yield break;
			}
			
			if (Out < 1 || Out > 7)
			{
				yield return "sendtochaterror The number given is not 1-7. Command ignored.";
				yield break;
			}
			
			for (int x = 0; x < Out; x++)
			{
				FlagButtons[1].OnInteract();
				yield return new WaitForSecondsRealtime(0.1f);
				FlagButtons[1].OnInteractEnded();
				yield return new WaitForSecondsRealtime(0.1f);
			}
			
		}
		
		if (Regex.IsMatch(command, @"^\s*hold red\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
			yield return null;
			if (hidden)
			{
				yield return "sendtochaterror The semaphore is already hidden. Command ignored.";
				yield break;
			}
			
			FlagButtons[0].OnInteract();
			yield return new WaitForSecondsRealtime(5f);
			FlagButtons[0].OnInteractEnded();
		}
			
		
		if (Regex.IsMatch(command, @"^\s*hold blue\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
			yield return null;
			if (hidden)
			{
				yield return "sendtochaterror The semaphore is already hidden. Command ignored.";
				yield break;
			}
			
			FlagButtons[1].OnInteract();
			yield return new WaitForSecondsRealtime(5f);
			FlagButtons[1].OnInteractEnded();
		}
	}
}