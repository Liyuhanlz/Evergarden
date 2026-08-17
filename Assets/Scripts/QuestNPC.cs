using UnityEngine;

// An NPC that gives a quest, tracks its state, and hands out a reward on
// completion. Not wired to any GameObject yet -- use this once you add a
// quest-giving NPC to the scene.
//
// Other systems call ReportHarvest() (or add similar Report___ methods) to
// progress quests that depend on gameplay actions outside this script.
public class QuestNPC : NPCBase
{
    public enum QuestState { NotStarted, InProgress, ReadyToTurnIn, Complete }

    [Header("Quest")]
    public QuestState questState = QuestState.NotStarted;

    [TextArea(2, 4)]
    public string[] questOfferLines;

    [TextArea(2, 4)]
    public string[] questInProgressLines;

    [TextArea(2, 4)]
    public string[] questCompleteLines;

    [Header("Reward")]
    [Tooltip("Gold given to the player when the quest is turned in")]
    public int goldReward = 0;

    protected override void OnPlayerEnterRange()
    {
        FaceTarget(playerTransform);
        StartDialogue(GetLinesForCurrentState());
    }

    string[] GetLinesForCurrentState()
    {
        switch (questState)
        {
            case QuestState.NotStarted: return questOfferLines;
            case QuestState.InProgress: return questInProgressLines;
            case QuestState.ReadyToTurnIn: return questCompleteLines;
            default: return new[] { "Thanks again for your help." };
        }
    }

    protected override void OnDialogueComplete()
    {
        if (questState == QuestState.NotStarted)
        {
            questState = QuestState.InProgress;
        }
        else if (questState == QuestState.ReadyToTurnIn)
        {
            GiveReward();
            questState = QuestState.Complete;
        }
    }

    void GiveReward()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddGold(goldReward);
    }

    // Called by other systems (e.g. FarmManager) when the player completes
    // the action this quest is waiting on.
    public void ReportHarvest(CropData crop, int amount)
    {
        if (questState == QuestState.InProgress)
            questState = QuestState.ReadyToTurnIn;
    }
}
