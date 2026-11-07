
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TriviaManager : UdonSharpBehaviour
{
    [SerializeField] private VRCUrl QuestionsURL;
    [SerializeField] private TMP_Text TxtQuestion;
    [SerializeField] private Transform[] PanelAnswers;
    private TMP_Text[] TxtAnswers;
    [SerializeField] private Toggle ToggleTemplateCategory;
    [SerializeField] private Toggle ToggleRepeats;

    [UdonSynced] private double networkShuffleSeed = 0;
    private double _networkShuffleSeed = 0;
    [UdonSynced] private bool allowRepeatQuestions = false;
    private bool _allowRepeatQuestions = false;
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
    private bool questionsLoaded = false;
    private int totalQuestionCount = 0;
    private int questionLoadAttempts = 0;
    private const int QUESTION_MAX_LOAD_ATTEMPTS = 5;

    private Toggle[] ToggleCategories = new Toggle[0];

    private void Start()
    {
        owner = Networking.GetOwner(gameObject);
        questionsLoaded = false;
        ToggleTemplateCategory.gameObject.SetActive(false);
        TxtAnswers = new TMP_Text[PanelAnswers.Length];
        for (int i = 0; i < PanelAnswers.Length; i++)
        {
            TxtAnswers[i] = PanelAnswers[i].GetComponentInChildren<TMP_Text>(true);
        }
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
        // Refresh current question
        if (questionID != _questionID || networkShuffleSeed != _networkShuffleSeed)
        {
            _questionID = questionID;
            _networkShuffleSeed = networkShuffleSeed;
            if (questionsLoaded) { LoadQuestionLocal(); }
        }
        // Refresh answer state
        if (revealAnswer != _revealAnswer)
        {
            DataList answerList = GetAnswersFromQuestion(_questionID);
            int answerCnt = answerList.Count;
            string answerStr = answerList[0].String;
            for (int i = 0; i < PanelAnswers.Length; i++)
            {
                // Enable/disable panels according to number of answers.
                if (i >= answerCnt || answerList[i].Error != DataError.None) { PanelAnswers[i].gameObject.SetActive(false); continue; }
                bool isAnswer = answerStr.Equals(TxtAnswers[i].text);
                if (PanelAnswers[i].gameObject.activeSelf != isAnswer) { PanelAnswers[i].gameObject.SetActive(isAnswer); }
            }
        }

        _loadingNetworkedValues = false;
    }

    private void LoadQuestionLocal()
    {
        if (_questionID < 0 || _questionID >= totalQuestionCount || !Utilities.IsValid(questionsByID)
            || questionsByID[_questionID].Error != DataError.None) { return; }
        DataList questionAndAnswers = questionsByID[_questionID].DataList;
        DataList answerList = GetAnswersFromQuestion(_questionID);
        if (!Utilities.IsValid(answerList)) { return; }
        int answerCnt = answerList.Count;
        TxtQuestion.text = questionAndAnswers[1].String;
        ShuffleTokens(answerList, _networkShuffleSeed);
        for (int i = 0; i < PanelAnswers.Length; i++)
        {
            // Enable/disable panels according to number of answers.
            if (i >= answerCnt || answerList[i].Error != DataError.None) { PanelAnswers[i].gameObject.SetActive(false); continue; }
            if (!PanelAnswers[i].gameObject.activeSelf) { PanelAnswers[i].gameObject.SetActive(true); }
            TxtAnswers[i].text = answerList[i].String;
        }

        // While only the owner cares about the answered questions list, we'll store it locally as well in case they disconnect and owner transfers
        if (!_questionsAnswered.Contains(_questionID)) { _questionsAnswered.Add(_questionID); }
    }

    private DataList GetAnswersFromQuestion(int in_questionID)
    {
        if (in_questionID < 0 || in_questionID >= totalQuestionCount || !Utilities.IsValid(questionsByID)
            || questionsByID[in_questionID].Error != DataError.None) { return null; }
        DataList questionAndAnswers = questionsByID[in_questionID].DataList;
        DataList answerList = new DataList();
        int listLen = questionAndAnswers.Count;
        // There should always be an ID [0], question [1], answer [2], incorrect answer[3], at minimum
        if (listLen < 4 || questionAndAnswers[0].Error != DataError.None) { return null; }
        for (int i = 2; i < listLen; i++)
        {
            if (questionAndAnswers[i].Error != DataError.None) { continue; }
            answerList.Add(questionAndAnswers[i]);
        }
        int answerCnt = answerList.Count;
        if (answerCnt < 2) { return null; }
        return answerList;
    }

    [NetworkCallable]
    public void ReceiveOwnerSerializationRequest(int in_questionID, bool in_repeats, bool[] in_categories)
    {
        if (owner == null) { owner = Networking.GetOwner(gameObject); }
        if (!owner.isLocal || !Utilities.IsValid(in_categories)) { return; }
        if (in_categories.Length > selectedCategories.Length) { OnPushRefresh(); return; }
        for (int i = 0; i < selectedCategories.Length; i++)
        {
            if (i < in_categories.Length) { selectedCategories[i] = in_categories[i]; }
        }
        allowRepeatQuestions = in_repeats;
        if (in_questionID >= 0) { questionID = in_questionID; }
        RequestSerialization();
        OnDeserialization();
    }

    private void SendOwnerSerializationRequest(int in_questionID, bool in_repeats, bool[] in_categories)
    {
        if (owner != null)
        {
            if (owner.isLocal) { ReceiveOwnerSerializationRequest(in_questionID, in_repeats, in_categories); }
            else { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(ReceiveOwnerSerializationRequest), in_questionID, in_repeats, in_categories); }
        }
    }

    public void OnToggleRepeats()
    {
        if (_loadingNetworkedValues) { return; }
        _allowRepeatQuestions = ToggleRepeats.isOn;
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _selectedCategories);
    }

    public void OnToggleCategory()
    {
        if (_loadingNetworkedValues) { return;  }
        for (int i = 0; i < ToggleCategories.Length; i++)
        {
            if (i < _selectedCategories.Length) { _selectedCategories[i] = ToggleCategories[i].isOn; }
        }
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _selectedCategories);
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
        if (!revealAnswer) { PickRandomQuestion(); }
        SendOwnerSerializationRequest(-1, _allowRepeatQuestions, _selectedCategories);
    }

    private void PickRandomQuestion()
    {
        DataList validCategories = new DataList(); int categoryCount = categoryNames.Count;
        for (int i = 0; i < selectedCategories.Length; i++)
        {
            if (selectedCategories[i] && i < categoryCount) { validCategories.Add(i); }
        }
        if (validCategories.Count == 0) { return; }

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
            _questionsAnswered.Clear();
            if (_questionID >= 0) { _questionsAnswered.Add(_questionID); }
            SendCustomEventDelayedSeconds(nameof(OnPushNext), 1.0f);
            return;
        }
        networkShuffleSeed = Networking.GetServerTimeInMilliseconds();
        SendOwnerSerializationRequest(questionPickedID, _allowRepeatQuestions, _selectedCategories);
    }

    // Local-only
    public void OnPushRefresh()
    {
        questionLoadAttempts = 0;
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
            for (int j = 1; j < questionFull.Length; j++)
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
        UnityEngine.Debug.Log($"[TriviaManager] Successfully loaded question pool with {questionsByCategory.Count} categories and {totalQuestionCount} questions.");

        // Now fill the categories.
        questionsLoaded = true;
        CreateCategories();
        LoadQuestionLocal();
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

        _loadingNetworkedValues = true;

        ToggleCategories = new Toggle[categoryCount];
        for (int i = 0; i < categoryCount; i++)
        {
            GameObject newToggleObj = Instantiate(ToggleTemplateCategory.gameObject, ToggleTemplateCategory.transform.parent);
            if (!Utilities.IsValid(newToggleObj)) { continue; }
            ToggleCategories[i] = newToggleObj.GetComponent<Toggle>();
            newToggleObj.SetActive(true);
            if (Utilities.IsValid(_selectedCategories) && i < _selectedCategories.Length) { ToggleCategories[i].isOn = _selectedCategories[i]; }
            if (Utilities.IsValid(categoryNames) && i < categoryNames.Count && categoryNames[i].Error == DataError.None)
            {
                ToggleCategories[i].GetComponentInChildren<TMP_Text>(true).text = categoryNames[i].String;
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
            SendOwnerSerializationRequest(-1, allowRepeatQuestions, selectedCategories);
        }
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
            questionsLoaded = false;
            TxtQuestion.text = $"Failed to load questions after {questionLoadAttempts} retries.<sub>({result.ErrorCode} - {result.Error})</sub>";
        }
    }
    private void ShuffleTokens(DataList list, double seed)
    {
        System.Random tokenRNG = new System.Random(BitConverter.DoubleToInt64Bits(seed).GetHashCode());
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int randomIndex = tokenRNG.Next(i, n);
            DataToken temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

}
