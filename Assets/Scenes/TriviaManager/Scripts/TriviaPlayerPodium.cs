
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TriviaPlayerPodium : UdonSharpBehaviour
{
    [SerializeField] private TriviaManager triviaManager;
    [SerializeField] private TMP_Text TxtNameplate;
    [SerializeField] private TMP_Text[] TxtAnswer;
    [SerializeField] private Camera photoCamera;
    [SerializeField] internal UdonBehaviour[] buttonClaim;
    [SerializeField] internal UdonBehaviour[] buttonAnswer;
    [SerializeField] private Renderer[] renderersButtonAnswer;
    [SerializeField] internal Animator podiumAnimator;
    [SerializeField] private Slider sliderPodiumStairs;
    [SerializeField] private Transform podiumStairs;

    // Which player currently claimed this podium?
    [UdonSynced] private int registeredPlayer = -1;
    internal int _registeredPlayer = -1;
    [UdonSynced] private int selectedAnswer = -1;
    internal int _selectedAnswer = -1;
    private VRCPlayerApi _cachedRegisteredPlayer = null;

    internal RenderTexture photoRT;
    internal VRCTweenHandle[] tweenInteractAnswer = new VRCTweenHandle[0];

    private const string CAPTION_ENTER = "Register";
    private const string CAPTION_EXIT = "Quit";
    internal Vector3 INTERACTABLE_TWEEN_RESET_DUO = new Vector3(0, 0.00455f, -0.000854f);
    internal Vector3 INTERACTABLE_TWEEN_LOCALPOS = new Vector3(0f, 0.0009f, 0.00046f);
    private Vector3 STAIR_HEIGHT_HIGHEST = new Vector3(0, 1.75f, -2.695f);
    private Vector3 STAIR_HEIGHT_LOWEST = new Vector3(0, 0.675f, -2.695f);

    private void Start()
    {
        if (triviaManager == null) 
        {
            GameObject triviaManagerObj = GameObject.Find("TriviaManager");
            if (triviaManagerObj != null) { triviaManager = triviaManagerObj.GetComponent<TriviaManager>(); }
        }
        if (!Utilities.IsValid(triviaManager)) { gameObject.SetActive(false); }
        podiumAnimator.SetInteger("answerCnt", 0);
        tweenInteractAnswer = new VRCTweenHandle[buttonAnswer.Length];
        for (int i = 0; i < buttonAnswer.Length; i++) 
        { 
            renderersButtonAnswer[i].materials[1].DisableKeyword("_EMISSION");
            tweenInteractAnswer[i] = buttonAnswer[i].transform.TweenLocalPosition(INTERACTABLE_TWEEN_LOCALPOS, 1f, VRCTweenEase.Linear).Pause();
        }  
        CreatePhotoRenderTexture();
        triviaManager.RegisterPodium(this);
        for (int b = 0; b < buttonClaim.Length; b++) 
        { 
            buttonClaim[b].gameObject.SetActive(b == 0); 
            buttonClaim[b].InteractionText = CAPTION_ENTER; 
        }
        TxtNameplate.text = "";
        OnSliderStairChanged();
    }

    public override void OnDeserialization()
    {
        if (!Utilities.IsValid(triviaManager)) { return; }
        base.OnDeserialization();

        if (_registeredPlayer != registeredPlayer)
        {
            if (registeredPlayer < 0) 
            { 
                _cachedRegisteredPlayer = null;
                TxtNameplate.text = "";
                for (int b = 0; b < buttonClaim.Length; b++)
                {
                    buttonClaim[b].gameObject.SetActive(b == 0);
                    buttonClaim[b].InteractionText = CAPTION_ENTER;
                }
                for (int i = 0; i < buttonAnswer.Length; i++) { buttonAnswer[i].DisableInteractive = true; }
            }
            else 
            {
                _cachedRegisteredPlayer = VRCPlayerApi.GetPlayerById(registeredPlayer);
                TxtNameplate.text = Utilities.IsValid(_cachedRegisteredPlayer) ? _cachedRegisteredPlayer.displayName : null;
                
                if (_cachedRegisteredPlayer.isLocal)
                {
                    triviaManager._localIsPlaying = true;
                    triviaManager.PlayAudioAtPoint(transform.position, TriviaManagerAudioClips.EnterGame);
                    for (int i = 0; i < triviaManager._registeredPodiums.Count; i++)
                    {
                        if (triviaManager._registeredPodiums[i].Error != DataError.None) { return; }
                        TriviaPlayerPodium podium = (TriviaPlayerPodium)triviaManager._registeredPodiums[i].Reference;
                        for (int b = 0; b < podium.buttonClaim.Length; b++) 
                        { 
                            podium.buttonClaim[b].gameObject.SetActive(podium == this && b == 1);
                            if (podium == this) { podium.buttonClaim[b].InteractionText = CAPTION_EXIT; }
                        }

                    }
                }
                for (int i = 0; i < buttonAnswer.Length; i++) { buttonAnswer[i].DisableInteractive = !_cachedRegisteredPlayer.isLocal; }
            }
            _registeredPlayer = registeredPlayer;
            OnSliderStairChanged();
        }

        if (_selectedAnswer != selectedAnswer)
        {
            DataList answerList = triviaManager.GetCurrentAnswers();
            int answerCnt = answerList.Count;
            for (int i = 0; i < TxtAnswer.Length; i++)
            {
                if (selectedAnswer < 0) { TxtAnswer[i].text = "-"; TxtAnswer[i].color = Color.white; }
                else
                {
                    if (answerCnt == 2)
                    {
                        TxtAnswer[i].text = selectedAnswer == 0 ? "T" : "F";
                    }
                    else
                    {
                        TxtAnswer[i].text = ((char)('A' + selectedAnswer)).ToString();
                    }
                    TxtAnswer[i].color = Color.white;
                }
            }
            triviaManager.SetPodiumPlyCircleParent(this, selectedAnswer);
            for (int i = 0; i < buttonAnswer.Length; i++)
            {
                if (selectedAnswer == i) { renderersButtonAnswer[i].materials[1].EnableKeyword("_EMISSION"); }
                else { renderersButtonAnswer[i].materials[1].DisableKeyword("_EMISSION"); }
            }
            _selectedAnswer = selectedAnswer;
        }
    }

    // -- Head Photos --
    private bool CreatePhotoRenderTexture()
    {
        if (photoRT != null) return true;
        RenderTexture newRenderTexture = new RenderTexture(1280, 720, 1, RenderTextureFormat.ARGB32);
        if (newRenderTexture == null) return false;
        if (!newRenderTexture.Create())
        {
            Destroy(newRenderTexture);
            return false;
        }
        photoRT = newRenderTexture;
        photoCamera.targetTexture = photoRT;
        return true;
    }

    internal void QueuePhoto()
    {
        if (_registeredPlayer < 0 || !Utilities.IsValid(_cachedRegisteredPlayer)) { return; }
        VRCPlayerApi.TrackingData trackingData = _cachedRegisteredPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        photoCamera.gameObject.SetActive(true);
        photoCamera.transform.SetPositionAndRotation(trackingData.position + (trackingData.rotation * Vector3.forward * 4f), trackingData.rotation * Quaternion.Euler(0, 180, 0));
        SendCustomEventDelayedSeconds(nameof(SnapPhoto), 0.2f);
    }

    public void SnapPhoto()
    {
        photoCamera.gameObject.SetActive(false);
    }

    // -- Ownership Management --

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);
        if (player.playerId == registeredPlayer) 
        {
            _cachedRegisteredPlayer = null;
            SetPodiumOwner(-1); 
        }
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        base.OnPlayerRespawn(player);
        if (player.playerId == registeredPlayer)
        {
            if (player.isLocal) { LocalExitGame(); }
            SetPodiumOwner(-1);
        }
    }
    private void SetPodiumOwner(int playerId)
    {
        if (!Networking.IsOwner(gameObject) || playerId == registeredPlayer) { return; }
        if (playerId >= 0) { 
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
            if (!Utilities.IsValid(player)) { playerId = -1; }
        }
        else { playerId = -1; }
        registeredPlayer = playerId;
        RequestSerialization();
        OnDeserialization();
    }

    public void SendPodiumClaimRequest()
    {
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(ReceivePodiumClaimRequest), Networking.LocalPlayer.playerId);
    }

    [NetworkCallable]
    public void ReceivePodiumClaimRequest(int playerId)
    {
        if (!Networking.IsOwner(gameObject)) { return; }
        VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
        if (!Utilities.IsValid(player)) { return; }
        SetPodiumOwner(Networking.LocalPlayer.playerId);
        Networking.SetOwner(player, gameObject);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject) && registeredPlayer >= 0 || registeredPlayer != Networking.LocalPlayer.playerId) { SetPodiumOwner(Networking.LocalPlayer.playerId); }
    }

    private void LocalExitGame()
    {
        if (_cachedRegisteredPlayer == null || !_cachedRegisteredPlayer.isLocal) { return; }
        _cachedRegisteredPlayer = null;
        triviaManager._localIsPlaying = false;
        triviaManager.PlayAudioAtPoint(transform.position, TriviaManagerAudioClips.ExitGame);
        SetPodiumOwner(-1);
        for (int i = 0; i < triviaManager._registeredPodiums.Count; i++)
        {
            if (triviaManager._registeredPodiums[i].Error != DataError.None) { return; }
            TriviaPlayerPodium podium = (TriviaPlayerPodium)triviaManager._registeredPodiums[i].Reference;
            for (int b = 0; b < podium.buttonClaim.Length; b++)
            {
                podium.buttonClaim[b].gameObject.SetActive(b == 0);
                podium.buttonClaim[b].InteractionText = CAPTION_ENTER;
            }
        }
    }

    public void OnButtonPodiumClaim()
    {
        if (_registeredPlayer < 0 || _cachedRegisteredPlayer == null)
        {
            SendPodiumClaimRequest();
        }
        else if (_cachedRegisteredPlayer != null && _cachedRegisteredPlayer.isLocal)
        {
            LocalExitGame();
        }
    }

    internal void SendAnswerStatus(bool isCorrect)
    {
        if (isCorrect)
        {
            if (Utilities.IsValid(_cachedRegisteredPlayer) && _cachedRegisteredPlayer.isLocal) { triviaManager.PlayAudioAtPoint(transform.position, TriviaManagerAudioClips.AnswerCorrect); }
        }
        else
        {
            if (Utilities.IsValid(_cachedRegisteredPlayer) && _cachedRegisteredPlayer.isLocal) { triviaManager.PlayAudioAtPoint(transform.position, TriviaManagerAudioClips.AnswerIncorrect); }
        }
        for (int i = 0; i < TxtAnswer.Length; i++)
        {
            if (_registeredPlayer >= 0)
            {
                TxtAnswer[i].text = isCorrect ? "O" : "X";
                TxtAnswer[i].color = isCorrect ? Color.green : Color.red;
            }
            else
            {
                TxtAnswer[i].text = "-";
                TxtAnswer[i].color = Color.white;
            }
        }
    }
    
    internal void SetAnswer(int answerIndex)
    {
        if (((registeredPlayer < 0 || _cachedRegisteredPlayer == null) && !Networking.IsOwner(gameObject)) 
            || (_cachedRegisteredPlayer != null && !_cachedRegisteredPlayer.isLocal)) { return; }
        selectedAnswer = answerIndex;
        RequestSerialization();
        OnDeserialization();
    }

    public void SelectAnswerA()
    {
        SetAnswer(0);
    }
    public void SelectAnswerB()
    {
        SetAnswer(1);
    }
    public void SelectAnswerC()
    {
        SetAnswer(2);
    }
    public void SelectAnswerD()
    {
        SetAnswer(3);
    }

    public void OnSliderStairChanged()
    {
        if (registeredPlayer < 0 || _cachedRegisteredPlayer == null || !_cachedRegisteredPlayer.isLocal) 
        {
            if (sliderPodiumStairs.gameObject.activeSelf) { sliderPodiumStairs.gameObject.SetActive(false); }
            if (podiumStairs.gameObject.activeSelf) { podiumStairs.gameObject.SetActive(false); }
            return; 
        }
        if (!sliderPodiumStairs.gameObject.activeSelf) { sliderPodiumStairs.gameObject.SetActive(true); }
        if (!podiumStairs.gameObject.activeSelf) { podiumStairs.gameObject.SetActive(true); }
        podiumStairs.transform.localPosition = new Vector3(
            Mathf.Lerp(STAIR_HEIGHT_LOWEST.x, STAIR_HEIGHT_HIGHEST.x, sliderPodiumStairs.value),
            Mathf.Lerp(STAIR_HEIGHT_LOWEST.y, STAIR_HEIGHT_HIGHEST.y, sliderPodiumStairs.value),
            Mathf.Lerp(STAIR_HEIGHT_LOWEST.z, STAIR_HEIGHT_HIGHEST.z, sliderPodiumStairs.value)
            );
    }
}
