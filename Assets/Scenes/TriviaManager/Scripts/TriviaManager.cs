
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon;

public enum TriviaManagerAudioClips
{
    EnterGame, ExitGame, ShowQuestion, AnswerCorrect, AnswerIncorrect
}

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TriviaManager : UdonSharpBehaviour
{

    [Header("Configurables")]
    [SerializeField] private VRCUrl QuestionsURL;
    [SerializeField] private Color colorRevealAnswer = Color.red;
    [SerializeField] private Color colorNextQuestion = Color.green;
    [Header("References")]
    [SerializeField] private TMP_Text TxtCategory;
    [SerializeField] private TMP_Text TxtQuestion;
    [SerializeField] private Transform[] PanelParents;
    [SerializeField] private Transform[] PanelAnswers;
    [SerializeField] private Transform[] PanelAnswerPlyList;
    private TMP_Text[] TxtAnswers;
    [SerializeField] private Toggle ToggleTemplateCategory;
    [SerializeField] private Transform PanelTemplateCategory;
    [SerializeField] private Transform PanelTemplateAnswerPlyCircle;
    [SerializeField] private Toggle ToggleRepeats;
    [SerializeField] private Toggle ToggleOtherPly;
    [SerializeField] private Toggle ToggleSFX;
    [SerializeField] private Button ButtonNext;
    [SerializeField] private Button ButtonRefresh;
    [SerializeField] private Image ImageNext;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] audioClips; // Corresponds to TriviaManagerAudioClips enum

    [UdonSynced] private double networkShuffleSeed = 0;
    private double _networkShuffleSeed = 0;
    [UdonSynced] private bool allowRepeatQuestions = false;
    private bool _allowRepeatQuestions = false;
    [UdonSynced] private bool otherPlayersVisible = true;
    internal bool _otherPlayersVisible = true;
    
    private DataList _questionsAnswered = new DataList();
    [UdonSynced] private int questionID = 0;
    private int _questionID = 0;
    [UdonSynced] private bool[] selectedCategories = new bool[0];
    private bool[] _selectedCategories = new bool[0];
    [UdonSynced] private bool revealAnswer = false;
    private bool _revealAnswer = false;

    private bool _loadingNetworkedValues = false;

    private DataDictionary questionsByCategory = new DataDictionary();
    private DataList categoryNames = new DataList();
    private DataList questionsByID = new DataList();
    // key = category name, value = DataList(Question), and Question = DataList(), where index 0 is question, index 1 is correct answer, 2+ are wrong answers))

    private VRCPlayerApi owner;
    private bool localIsJoining = true;
    private bool questionsLoaded = false;
    private int totalQuestionCount = 0;
    private int questionLoadAttempts = 0;
    private const int QUESTION_MAX_LOAD_ATTEMPTS = 4;

    private Toggle[] ToggleCategories = new Toggle[0];
    private Transform[] PanelCategories = new Transform[0];
    private TMP_Text[] TxtCategories = new TMP_Text[0];
    private Toggle[] ToggleAnswers = new Toggle[0];
    private VRCTweenHandle[] _TweenAnswers = new VRCTweenHandle[0];
    private const int MAX_CATEGORIES_PER_PANEL = 6;
    private Transform ImageArrowRefresh;
    private VRCTweenHandle _refreshingArrowSpin;

    // Answers
    internal DataList _registeredPodiums = new DataList();
    private DataList _registeredAnswerPlyCircle = new DataList();
    internal bool _localIsPlaying = false;
    internal bool _localRequestedPlay = false;

    private void Start()
    {
        localIsJoining = true;
        owner = Networking.GetOwner(gameObject);
        questionsLoaded = false;
        ToggleTemplateCategory.gameObject.SetActive(false);
        PanelTemplateCategory.gameObject.SetActive(false);
        PanelTemplateAnswerPlyCircle.gameObject.SetActive(false);
        TxtAnswers = new TMP_Text[PanelAnswers.Length];
        ToggleAnswers = new Toggle[PanelAnswers.Length];
        _TweenAnswers = new VRCTweenHandle[PanelAnswers.Length];
        for (int i = 0; i < PanelAnswers.Length; i++)
        {
            TxtAnswers[i] = PanelAnswers[i].GetComponentInChildren<TMP_Text>(true);
            ToggleAnswers[i] = PanelAnswers[i].GetComponent<Toggle>();
            _TweenAnswers[i] = PanelAnswers[i].TweenScale(Vector3.one * 1.5f, 0.1f, VRCTweenEase.Linear).SetLoops(2, VRCTweenLoopType.Yoyo).Pause();
        }
        ImageArrowRefresh = ButtonRefresh.transform.GetChild(0).GetChild(0).GetChild(0);
        _refreshingArrowSpin = ImageArrowRefresh.TweenLocalRotation(new Vector3(0, 0, 180f), 1.0f, VRCTweenEase.Linear).SetLoops(-1, VRCTweenLoopType.Incremental).Pause();
        ImageNext.color = _revealAnswer ? colorNextQuestion : colorRevealAnswer;
        OnPushRefresh();
    }

    public override void OnDeserialization()
    {
        base.OnDeserialization();

        _loadingNetworkedValues = true;
        if (owner == null) { owner = Networking.GetOwner(gameObject); }

        // Refresh category list (size)
        if (selectedCategories.Length != _selectedCategories.Length)
        {
            _selectedCategories = new bool[selectedCategories.Length];
        }
        // Refresh category toggles
        for (int i = 0; i < selectedCategories.Length; i++)
        {
            _selectedCategories[i] = selectedCategories[i];
            if (questionsLoaded && i < ToggleCategories.Length && Utilities.IsValid(ToggleCategories[i])) { ToggleCategories[i].isOn = _selectedCategories[i]; }
        }
        // Refresh question repeats
        if (allowRepeatQuestions != _allowRepeatQuestions)
        {
            _allowRepeatQuestions = allowRepeatQuestions;
            ToggleRepeats.isOn = _allowRepeatQuestions;
        }
        // Refresh other player answer reveals
        if (otherPlayersVisible != _otherPlayersVisible)
        {
            _otherPlayersVisible = otherPlayersVisible;
            ToggleOtherPly.isOn = _otherPlayersVisible;
        }
        // Refresh current question
        if (questionID != _questionID || networkShuffleSeed != _networkShuffleSeed)
        {
            _questionID = questionID;
            _networkShuffleSeed = networkShuffleSeed;
            if (questionsLoaded) { LoadQuestionLocal(); }
            ButtonNext.interactable = true;
        }
        // Refresh answer state
        if (revealAnswer != _revealAnswer)
        {
            _revealAnswer = revealAnswer;
            if (revealAnswer)
            {
                DataList answerList = GetCurrentAnswers();
                if (Utilities.IsValid(answerList))
                {
                    int answerCnt = answerList.Count;
                    string answerStr = answerList[0].String;
                    int correctAnswerIndex = -1;
                    for (int i = 0; i < PanelAnswers.Length; i++)
                    {
                        // Enable/disable panels according to number of answers.
                        if (i >= answerCnt || answerList[i].Error != DataError.None) { PanelAnswers[i].gameObject.SetActive(false); continue; }
                        // Set toggles accordingly.
                        bool isAnswer = answerStr.Equals(TxtAnswers[i].text);
                        if (ToggleAnswers[i].isOn != isAnswer) { ToggleAnswers[i].isOn = isAnswer; }
                        if (isAnswer) { correctAnswerIndex = i; _TweenAnswers[i].Restart(); }
                    }
                    int podiumCount = _registeredPodiums.Count;
                    for (int i = 0; i < podiumCount; i++)
                    {
                        if (_registeredPodiums[i].Error != DataError.None) { continue; }
                        TriviaPlayerPodium podiumScript = (TriviaPlayerPodium)_registeredPodiums[i].Reference;
                        podiumScript.SendAnswerStatus(podiumScript._selectedAnswer == correctAnswerIndex);
                        if (podiumScript._registeredPlayer == Networking.LocalPlayer.playerId) 
                        {
                            for (int j = 0; j < podiumScript.buttonAnswer.Length; j++) { podiumScript.buttonAnswer[j].DisableInteractive = true; }
                        }
                        if (answerCnt == 2) { podiumScript.podiumAnimator.Play("PodiumPromptQuestionDuo", 0, 0.5f); }
                        else { podiumScript.podiumAnimator.Play("PodiumPromptQuestionQuad", 0, 0.5f); }
                        podiumScript.podiumAnimator.speed = 0;
                        // Always show answer plate upon answer reveal.
                        if (podiumScript._registeredPlayer >= 0 && !_otherPlayersVisible) { ((Transform)_registeredAnswerPlyCircle[i].Reference).gameObject.SetActive(true); }
                        //podiumScript.podiumAnimator.SetInteger("answerCnt", 0);
                    }
                }
            }
            CheckPanelParents();
            ButtonNext.interactable = true;
            ImageNext.color = _revealAnswer ? colorNextQuestion : colorRevealAnswer;
        }

        _loadingNetworkedValues = false;
    }

    private void LoadQuestionLocal()
    {
        if (_questionID < 0 || _questionID >= totalQuestionCount || !Utilities.IsValid(questionsByID)
            || questionsByID[_questionID].Error != DataError.None) { return; }
        DataList questionAndAnswers = questionsByID[_questionID].DataList;
        DataList answerList = GetCurrentAnswers();
        if (!Utilities.IsValid(answerList)) { return; }
        int answerCnt = answerList.Count;
        TxtCategory.text = questionAndAnswers[1].String;
        TxtQuestion.text = questionAndAnswers[2].String;
        ShuffleTokens(answerList, _networkShuffleSeed);
        for (int i = 0; i < PanelAnswers.Length; i++)
        {
            // Enable/disable panels according to number of answers.
            if (i >= answerCnt || answerList[i].Error != DataError.None) { PanelAnswers[i].gameObject.SetActive(false); continue; }
            if (!PanelAnswers[i].gameObject.activeSelf) { PanelAnswers[i].gameObject.SetActive(true); }
            TxtAnswers[i].text = answerList[i].String;
            ToggleAnswers[i].isOn = false;
        }
        CheckPanelParents();
        for (int i = 0; i < TxtCategories.Length; i++)
        {
            ToggleCategories[i].image.color = TxtCategories[i].text.Equals(questionAndAnswers[1].String, StringComparison.InvariantCultureIgnoreCase) ? new Color32(40, 220, 153, 255) : new Color32(0, 0, 0, 255);
        }
        // While only the owner cares about the answered questions list, we'll store it locally as well in case they disconnect and owner transfers
        if (!_questionsAnswered.Contains(_questionID)) { _questionsAnswered.Add(_questionID); }
        // Finally, let's reveal answer buttons if applicable.
        int podiumCount = _registeredPodiums.Count;
        for (int i = 0; i < podiumCount; i++)
        {
            if (_registeredPodiums[i].Error != DataError.None) { continue; }
            TriviaPlayerPodium podiumScript = (TriviaPlayerPodium)_registeredPodiums[i].Reference;
            if (!Utilities.IsValid(podiumScript)) { continue; }
            bool isLocal = false;
            if (podiumScript._registeredPlayer == Networking.LocalPlayer.playerId || (podiumScript._registeredPlayer < 0 && Networking.IsOwner(podiumScript.gameObject)))
            {
                // We do this here to account for null answers
                podiumScript.ResetAnswerText();
                podiumScript.SetAnswer(-1);
                if (podiumScript._registeredPlayer == Networking.LocalPlayer.playerId)
                {
                    isLocal = true;
                    PlayAudioAtPoint(podiumScript.transform.position, TriviaManagerAudioClips.ShowQuestion);
                }
    
            }
            for (int j = 0; j < podiumScript.buttonAnswer.Length; j++)
            {
                podiumScript.buttonAnswer[j].gameObject.SetActive(j < answerCnt);
                podiumScript.buttonAnswer[j].DisableInteractive = !isLocal;
                if (answerCnt == 2) 
                { 
                    podiumScript.buttonAnswer[j].InteractionText = j == 0 ? "T" : "F";
                    podiumScript.tweenInteractAnswer[j].ChangeEndValue(podiumScript.INTERACTABLE_TWEEN_LOCALPOS + podiumScript.INTERACTABLE_TWEEN_RESET_DUO, false);
                    podiumScript.tweenInteractAnswer[j].Restart();
                }
                else 
                { 
                    podiumScript.buttonAnswer[j].InteractionText = ((char)('A' + j)).ToString();
                    podiumScript.tweenInteractAnswer[j].ChangeEndValue(podiumScript.INTERACTABLE_TWEEN_LOCALPOS, false);
                    podiumScript.tweenInteractAnswer[j].Restart();
                }
            }
            //podiumScript.podiumAnimator.SetInteger("answerCnt", answerCnt);
            if (answerCnt == 2) { podiumScript.podiumAnimator.Play("PodiumPromptQuestionDuo"); }
            else { podiumScript.podiumAnimator.Play("PodiumPromptQuestionQuad"); }
            podiumScript.podiumAnimator.speed = 1;
            podiumScript.QueuePhoto();
        }

    }

    private DataList GetAnswersFromQuestion(int in_questionID)
    {
        if (in_questionID < 0 || in_questionID >= totalQuestionCount || !Utilities.IsValid(questionsByID)
            || questionsByID[in_questionID].Error != DataError.None) { return null; }
        DataList questionAndAnswers = questionsByID[in_questionID].DataList;
        DataList answerList = new DataList();
        int listLen = questionAndAnswers.Count;
        // There should always be an ID [0], category [1], question [2], answer [3], incorrect answer[4], at minimum
        if (listLen < 5 || questionAndAnswers[0].Error != DataError.None) { return null; }
        for (int i = 3; i < listLen; i++)
        {
            if (questionAndAnswers[i].Error != DataError.None) { continue; }
            answerList.Add(questionAndAnswers[i]);
        }
        int answerCnt = answerList.Count;
        if (answerCnt < 2) { return null; }
        return answerList;
    }

    internal DataList GetCurrentAnswers()
    {
        return GetAnswersFromQuestion(_questionID);
    }


    [NetworkCallable]
    public void ReceiveOwnerSerializationRequest(int in_questionID, bool in_repeats, bool in_reveal, bool[] in_categories)
    {
        if (owner == null) { owner = Networking.GetOwner(gameObject); }
        if (!owner.isLocal || !Utilities.IsValid(in_categories)) { return; }
        if (in_categories.Length > selectedCategories.Length) { OnPushRefresh(); return; }
        for (int i = 0; i < selectedCategories.Length; i++)
        {
            if (i < in_categories.Length) { selectedCategories[i] = in_categories[i]; }
        }
        allowRepeatQuestions = in_repeats;
        otherPlayersVisible = in_reveal;
        if (in_questionID >= 0) { questionID = in_questionID; }
        RequestSerialization();
        OnDeserialization();
    }

    private void SendOwnerSerializationRequest(int in_questionID, bool in_repeats, bool in_reveal, bool[] in_categories)
    {
        if (owner != null)
        {
            if (owner.isLocal) { ReceiveOwnerSerializationRequest(in_questionID, in_repeats, in_reveal, in_categories); }
            else { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(ReceiveOwnerSerializationRequest), in_questionID, in_repeats, in_reveal, in_categories); }
        }
    }

    public void OnToggleRepeats()
    {
        if (_loadingNetworkedValues) { return; }
        _allowRepeatQuestions = ToggleRepeats.isOn;
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _otherPlayersVisible, _selectedCategories);
    }

    public void OnToggleOtherPly()
    {
        if (_loadingNetworkedValues) { return; }
        _otherPlayersVisible = ToggleOtherPly.isOn;
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _otherPlayersVisible, _selectedCategories);
    }

    public void OnToggleCategory()
    {
        if (_loadingNetworkedValues) { return;  }
        for (int i = 0; i < ToggleCategories.Length; i++)
        {
            if (i < _selectedCategories.Length) { _selectedCategories[i] = ToggleCategories[i].isOn; }
        }
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _otherPlayersVisible, _selectedCategories);
    }

    public void OnPushNext()
    {
        if (_loadingNetworkedValues || !Utilities.IsValid(selectedCategories) || !Utilities.IsValid(questionsByCategory) || !Utilities.IsValid(questionsByID) || selectedCategories.Length <= 0) { return; }
        if (owner == null) { owner = Networking.GetOwner(gameObject); }
        if (owner.isLocal) { ReceivePushNextRequest(); }
        else { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(ReceivePushNextRequest)); }
    }

    [NetworkCallable]
    public void ReceivePushNextRequest()
    {
        if (owner == null || !owner.isLocal || !questionsLoaded) { return; }
        revealAnswer = !revealAnswer;
        ButtonNext.interactable = false;
        if (!revealAnswer) { PickRandomQuestion(); }
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _otherPlayersVisible, _selectedCategories);
    }

    private void PickRandomQuestion()
    {
        DataList validCategories = new DataList(); int categoryCount = categoryNames.Count;
        for (int i = 0; i < selectedCategories.Length; i++)
        {
            if (selectedCategories[i] && i < categoryCount) { validCategories.Add(i); }
        }
        if (validCategories.Count == 0) { TxtQuestion.text = "No categories selected!"; ButtonNext.interactable = true; return; }

        int questionPickedID = -1;
        while (validCategories.Count > 0 && questionPickedID < 0)
        {
            // Pick a random valid category
            int randCatIndex = UnityEngine.Random.Range(0, validCategories.Count);
            // Search the list of questions in that category for valid ones
            DataList outerQuestionlist = questionsByCategory[categoryNames[validCategories[randCatIndex].Int].String].DataList;
            DataList questionCandidates = new DataList();
            for (int j = 0; j < outerQuestionlist.Count; j++)
            {
                int candidateID = outerQuestionlist[j].DataList[0].Int;
                if (allowRepeatQuestions || !_questionsAnswered.Contains(candidateID)) { questionCandidates.Add(candidateID); }
            }
            if (questionCandidates.Count > 0) { questionPickedID = questionCandidates[UnityEngine.Random.Range(0, questionCandidates.Count)].Int; }
            else { validCategories.RemoveAt(randCatIndex); }
        }

        if (questionPickedID < 0)
        {
            TxtQuestion.text = "(All questions answered; resetting list...)";
            ButtonNext.interactable = false;
            _questionsAnswered.Clear();
            if (_questionID >= 0) { _questionsAnswered.Add(_questionID); }
            revealAnswer = true;
            SendCustomEventDelayedSeconds(nameof(OnPushNext), 1.0f);
            return;
        }
        networkShuffleSeed = Networking.GetServerTimeInMilliseconds();
        SendOwnerSerializationRequest(questionPickedID, _allowRepeatQuestions, _otherPlayersVisible, _selectedCategories);
    }

    private void CreateCategories()
    {
        if (!Utilities.IsValid(questionsByCategory)) { return; }
        int categoryCount = questionsByCategory.Count;
        // Destroy everything in the old list before creating a new one.
        if (Utilities.IsValid(ToggleCategories) && ToggleCategories.Length > 0)
        {
            for (int i = 0; i < ToggleCategories.Length; i++)
            {
                if (!Utilities.IsValid(ToggleCategories[i])) { continue; }
                Destroy(ToggleCategories[i].gameObject);
            }
        }
        if (Utilities.IsValid(PanelCategories) && PanelCategories.Length > 0)
        {
            for (int i = 0; i < PanelCategories.Length; i++)
            {
                if (!Utilities.IsValid(PanelCategories[i])) { continue; }
                Destroy(PanelCategories[i].gameObject);
            }
        }

        _loadingNetworkedValues = true;

        int subpanelCount = Mathf.CeilToInt(categoryCount / (float)MAX_CATEGORIES_PER_PANEL);
        PanelCategories = new Transform[subpanelCount];
        for (int i = 0; i < subpanelCount; i++)
        {
            GameObject newPanelObj = Instantiate(PanelTemplateCategory.gameObject, PanelTemplateCategory.transform.parent);
            if (!Utilities.IsValid(newPanelObj)) { continue; }
            PanelCategories[i] = newPanelObj.transform;
            newPanelObj.SetActive(true);
        }
        
        ToggleCategories = new Toggle[categoryCount];
        TxtCategories = new TMP_Text[categoryCount];
        for (int i = 0; i < categoryCount; i++)
        {
            int subcategoryIndex = subpanelCount > 1 ? Mathf.FloorToInt(i % subpanelCount) : 0;
            GameObject newToggleObj = Instantiate(ToggleTemplateCategory.gameObject, Utilities.IsValid(PanelCategories[subcategoryIndex]) ? PanelCategories[subcategoryIndex] : null);
            if (!Utilities.IsValid(newToggleObj)) { continue; }
            ToggleCategories[i] = newToggleObj.GetComponent<Toggle>();
            newToggleObj.SetActive(true);
            if (Utilities.IsValid(_selectedCategories) && i < _selectedCategories.Length) { ToggleCategories[i].isOn = _selectedCategories[i]; }
            if (Utilities.IsValid(categoryNames) && i < categoryNames.Count && categoryNames[i].Error == DataError.None)
            {
                TxtCategories[i] = ToggleCategories[i].GetComponentInChildren<TMP_Text>(true);
                TxtCategories[i].text = categoryNames[i].String;
            }
        }

        _loadingNetworkedValues = false;

        if (!Utilities.IsValid(owner)) { owner = Networking.GetOwner(gameObject); }
        if (owner.isLocal && selectedCategories.Length != categoryCount)
        {
            selectedCategories = new bool[categoryCount];
            for (int i = 0; i < categoryCount; i++)
            {
                selectedCategories[i] = true;
            }
            SendOwnerSerializationRequest(-1, allowRepeatQuestions, _otherPlayersVisible, selectedCategories);
        }
    }


    // Local-only
    public void OnPushRefresh()
    {
        questionLoadAttempts = 0;
        ButtonRefresh.interactable = false;
        _refreshingArrowSpin.Restart();
        LoadQuestionsFromURL();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player)) { return; }
        base.OnOwnershipTransferred(player);
        owner = player;
    }

    public void LoadQuestionsFromURL()
    {
        if (!Utilities.IsValid(QuestionsURL)) { TxtQuestion.text = $"No URL Specified"; return; }
        VRCStringDownloader.LoadUrl(QuestionsURL, this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        questionLoadAttempts = 0;
        categoryNames = new DataList();
        questionsByCategory = new DataDictionary();
        questionsByID = new DataList();
        string resultAsUTF8 = result.Result;
        UnityEngine.Debug.Log($"[TriviaManager] string loaded result: {resultAsUTF8}");
        string[] lineItem = resultAsUTF8.Split('\n');
        totalQuestionCount = 0;
        for (int i = 0; i < lineItem.Length; i++)
        {
            string[] questionFull = lineItem[i].Replace("\r","").Split('^');
            // Need at least category [0], question [1], correct answer [2], and incorrect answer [3]
            if (!Utilities.IsValid(questionFull) || questionFull.Length < 4) { continue; }
            // If the category does not exist, create a list of questions for it.
            if (!questionsByCategory.ContainsKey(questionFull[0])) 
            { 
                questionsByCategory.Add(questionFull[0], new DataList());
                categoryNames.Add(questionFull[0]);
            }
            // Then fill the question list accordingly.
            DataList innerQuestionList = new DataList();
            innerQuestionList.Add((int)totalQuestionCount);
            for (int j = 0; j < questionFull.Length; j++)
            {
                innerQuestionList.Add(questionFull[j]);
            }
            // Replace errors with new lists.
            if (questionsByCategory[questionFull[0]].Error != DataError.None) { questionsByCategory[questionFull[0]] = new DataList(); }
            DataList outerQuestionList = questionsByCategory[questionFull[0]].DataList;
            outerQuestionList.Add(innerQuestionList);
            questionsByID.Add(innerQuestionList);
            totalQuestionCount++;
        }

        _refreshingArrowSpin.Restart();
        _refreshingArrowSpin.Pause();
        ButtonRefresh.interactable = true;

        if (totalQuestionCount == 0)
        {
            questionsLoaded = false;
            TxtQuestion.text = "Loaded URL, but no valid questions were found.\nCheck that the URL points to a raw text file.";
            return;
        }

        UnityEngine.Debug.Log($"[TriviaManager] Successfully loaded question pool with {questionsByCategory.Count} categories and {totalQuestionCount} questions.");

        // Now fill the categories.
        questionsLoaded = true;
        CreateCategories();
        LoadQuestionLocal();
        if (owner == null) { owner = Networking.GetOwner(gameObject); }
        if (localIsJoining && !owner.isLocal) { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(OnNewPlayerJoined)); }
        localIsJoining = false;
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (questionLoadAttempts < QUESTION_MAX_LOAD_ATTEMPTS)
        {
            questionLoadAttempts++;
            SendCustomEventDelayedSeconds(nameof(LoadQuestionsFromURL), 5f);
            TxtQuestion.text = $"Failed to load questions, trying again... (Attempt {questionLoadAttempts} of {QUESTION_MAX_LOAD_ATTEMPTS})\n<sub>({result.ErrorCode} - {result.Error})</sub>";
        }
        else
        {
            _refreshingArrowSpin.Restart();
            _refreshingArrowSpin.Pause();
            ButtonRefresh.interactable = true;
            questionsLoaded = false;
            TxtQuestion.text = $"Failed to load questions after {questionLoadAttempts} retries.\n<sub>({result.ErrorCode} - {result.Error})</sub>";
        }
    }
    private void ShuffleTokens(DataList list, double seed)
    {
        System.Random tokenRNG = new System.Random(BitConverter.DoubleToInt64Bits(seed).GetHashCode());
        int n = list.Count;
        // Exception case: true/false and yes/no questions will always be in True/False or Yes/No order.
        if (n == 2)
        {
            if (list[0].String.ToLowerInvariant().Contains("f") || list[0].String.ToLowerInvariant().Contains("n")) 
            {
                DataToken temp = list[0];
                list[0] = list[1];
                list[1] = temp;
            }
        }
        else
        {
            for (int i = 0; i < n - 1; i++)
            {
                int randomIndex = tokenRNG.Next(i, n);
                DataToken temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }
    }

    private void CheckPanelParents()
    {
        for (int i = 0; i < PanelParents.Length; i++)
        {
            if (!Utilities.IsValid(PanelParents[i])) { continue; }
            bool shouldBeActive = false;
            for (int j = 0; j < PanelParents[i].childCount; j++)
            {
                if (Utilities.IsValid(PanelParents[i].GetChild(j)) && PanelParents[i].GetChild(j).gameObject.activeSelf) { shouldBeActive = true; break; }
            }
            if (PanelParents[i].gameObject.activeSelf != shouldBeActive) { PanelParents[i].gameObject.SetActive(shouldBeActive); }
        }
    }

    [NetworkCallable]
    public void OnNewPlayerJoined()
    {
        if (owner == null || !owner.isLocal) { return; }
        SendOwnerSerializationRequest(_questionID, _allowRepeatQuestions, _otherPlayersVisible, _selectedCategories);
    }

    // -- Podium Answer Linking --
    internal void RegisterPodium(TriviaPlayerPodium triviaPlayerPodium)
    {
        if (_registeredPodiums.Contains(triviaPlayerPodium)) { return; }
        _registeredPodiums.Add(triviaPlayerPodium);
        GameObject newPlyCircle = Instantiate(PanelTemplateAnswerPlyCircle.gameObject, PanelTemplateAnswerPlyCircle.parent);
        RawImage image = newPlyCircle.transform.GetChild(0).GetComponent<RawImage>();
        image.texture = triviaPlayerPodium.photoRT;
        newPlyCircle.gameObject.SetActive(false);
        _registeredAnswerPlyCircle.Add(newPlyCircle.transform);
    }

    // While this should never be used, it's useful for posterity
    internal void UnregisterPodium(TriviaPlayerPodium triviaPlayerPodium)
    {
        if (!_registeredPodiums.Contains(triviaPlayerPodium)) { return; }
        int index = _registeredPodiums.IndexOf(triviaPlayerPodium);
        _registeredPodiums.Remove(triviaPlayerPodium);
        if (index < _registeredAnswerPlyCircle.Count) { _registeredAnswerPlyCircle.RemoveAt(index); }
    }

    internal void SetPodiumPlyCircleParent(TriviaPlayerPodium triviaPlayerPodium, int answerIndex)
    {
        if (!Utilities.IsValid(triviaPlayerPodium) || !_registeredPodiums.Contains(triviaPlayerPodium)) { return; }
        int index = _registeredPodiums.IndexOf(triviaPlayerPodium);
        Transform plyCircle = ((Transform)_registeredAnswerPlyCircle[index].Reference);
        bool answerIsVisible = triviaPlayerPodium._registeredPlayer >= 0 && triviaPlayerPodium._registeredPlayer == Networking.LocalPlayer.playerId;
        answerIsVisible = answerIsVisible || _otherPlayersVisible || !_localIsPlaying;
        plyCircle.gameObject.SetActive(answerIndex >= 0 && triviaPlayerPodium._registeredPlayer >= 0 && answerIsVisible);
        plyCircle.SetParent(answerIndex >= 0 ? PanelAnswerPlyList[answerIndex].transform : null, false);
    }

    internal TriviaPlayerPodium GetLocalPodium()
    {
        if (!_localIsPlaying) { return null; }
        int podiumCount = _registeredPodiums.Count;
        for (int i = 0; i < podiumCount; i++)
        {
            if (_registeredPodiums[i].Error != DataError.None) { continue; }
            TriviaPlayerPodium podium = (TriviaPlayerPodium)_registeredPodiums[i].Reference;
            if (!Utilities.IsValid(podium)) { continue; }
            if (podium._registeredPlayer == Networking.LocalPlayer.playerId) { return podium; }
        }
        return null;
    }

    // -- Podium Options --
    public void OnToggleAudioSource()
    {
        audioSource.gameObject.SetActive(ToggleSFX.isOn);
    }

    internal void PlayAudioAtPoint(Vector3 position, TriviaManagerAudioClips clip)
    {
        audioSource.transform.position = position;
        audioSource.Stop();
        audioSource.clip = audioClips[(int)clip];
        audioSource.Play();
    }

}
