using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the on screen readouts in step with the match: score, lives, stacked
/// eggs, which power ups are running, and the big message in the middle.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Player one")]
    public Text playerOneText;

    [Header("Player two, only shown in Duo")]
    public Text playerTwoText;

    [Header("Middle of the screen")]
    public Text messageText;

    [Header("Top of the screen")]
    public Text difficultyText;

    void Update()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        UpdatePlayerText(playerOneText, manager.playerOne, "P1");
        UpdatePlayerText(playerTwoText, manager.mode == GameMode.Duo ? manager.playerTwo : null, "P2");
        UpdateDifficulty(manager);
        UpdateMessage(manager);
    }

    void UpdatePlayerText(Text label, BasketController basket, string prefix)
    {
        if (label == null)
        {
            return;
        }

        if (basket == null)
        {
            label.text = "";
            return;
        }

        string line = prefix + "  LIVES " + Hearts(basket) + "   SCORE " + basket.score;

        if (GameManager.Instance != null && GameManager.Instance.mode == GameMode.Single)
        {
            line += "   EGGS " + basket.ammo;
        }

        if (basket.combo >= 5)
        {
            line += "   x" + basket.ScoreMultiplier();
        }

        string effects = Effects(basket);
        if (effects.Length > 0)
        {
            line += "\n" + effects;
        }

        label.text = line;
    }

    /// <summary>Three lives shown as hearts, with a half heart when one is half spent.</summary>
    static string Hearts(BasketController basket)
    {
        string hearts = "";
        for (int i = 0; i < basket.WholeHearts(); i++)
        {
            hearts += "<3 ";
        }

        if (basket.HasHalfHeart())
        {
            hearts += "<";
        }

        if (hearts.Length == 0)
        {
            hearts = "-";
        }

        return hearts.TrimEnd();
    }

    static string Effects(BasketController basket)
    {
        string effects = "";
        if (basket.speedTimer > 0f)
        {
            effects += "SPEED ";
        }

        if (basket.freezeTimer > 0f)
        {
            effects += "FROZEN ";
        }

        if (basket.reverseTimer > 0f)
        {
            effects += "REVERSED ";
        }

        if (basket.sabotageTimer > 0f)
        {
            effects += "EGG STORM ";
        }

        return effects.TrimEnd();
    }

    void UpdateDifficulty(GameManager manager)
    {
        if (difficultyText == null)
        {
            return;
        }

        if (manager.phase != GamePhase.Playing)
        {
            difficultyText.text = "";
            return;
        }

        string line = "SPEED " + (manager.CurrentTier() + 1) + "   TIME " + Mathf.FloorToInt(manager.elapsed) + "s";
        if (manager.mode == GameMode.Single)
        {
            line += "   CHICKENS " + manager.chickensDefeated + "/" + manager.chickensToDefeat;
        }

        difficultyText.text = line;
    }

    void UpdateMessage(GameManager manager)
    {
        if (messageText == null)
        {
            return;
        }

        switch (manager.phase)
        {
            case GamePhase.Menu:
                messageText.text =
                    "ROTTEN EGGS\n\n"
                    + "PRESS 1  -  SINGLE PLAYER\n"
                    + "PRESS 2  -  DUO VERSUS\n\n"
                    + "P1 MOVE  A / D          P2 MOVE  LEFT / RIGHT\n"
                    + "THROW  SPACE     RESTART  R     MENU  ESC";
                break;

            case GamePhase.Playing:
                messageText.text = "";
                break;

            case GamePhase.Won:
                if (manager.mode == GameMode.Single)
                {
                    messageText.text = "ALL 3 CHICKENS DEFEATED\n\nPRESS R TO PLAY AGAIN     ESC FOR MENU";
                }
                else
                {
                    messageText.text = "PLAYER " + manager.winnerPlayer + " WINS\n\nPRESS R TO PLAY AGAIN     ESC FOR MENU";
                }

                break;

            case GamePhase.Lost:
                messageText.text = "OUT OF LIVES\n\nPRESS R TO TRY AGAIN     ESC FOR MENU";
                break;
        }
    }
}
