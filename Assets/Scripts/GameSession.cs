using UnityEngine;

public enum FishGameMode
{
    Tutorial,
    Standard,
    TimeAttack,
    Riptide
}

public static class GameSession
{
    private const string PearlKey = "Fishfish.Pearls";
    private const string SkinKey = "Fishfish.Skin";
    private const string TutorialKey = "Fishfish.TutorialComplete";
    private const string TouchControlsKey = "Fishfish.TouchControls";

    public static FishGameMode Mode { get; set; } = FishGameMode.Standard;
    public static int SelectedStage { get; set; }
    public static int RunPearls { get; private set; }
    public static bool ShieldReady { get; private set; }
    public static bool SpeedDashReady { get; private set; }
    public static bool PearlMagnetReady { get; private set; }
    public static bool InvincibilityReady { get; private set; }

    public static readonly string[] StageNames =
    {
        "Coral Nursery",
        "Sunken Garden",
        "Midnight Trench"
    };

    public static readonly string[] StageDescriptions =
    {
        "Warm currents, wide lanes, friendly reef life",
        "Ruins, stronger currents, drifting obstacles",
        "Dark water, fast hazards, rare luminous pearls"
    };

    public static int PearlWallet => PlayerPrefs.GetInt(PearlKey, 500);
    public static int EquippedSkin => PlayerPrefs.GetInt(SkinKey, 0);
    public static int EquippedCharacter => EquippedSkin;
    public static string EquippedCharacterName => EquippedCharacter == 1 ? "Seal" : "Turtle";
    public static bool TutorialComplete => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
    public static bool ShowTouchControls
    {
        get => PlayerPrefs.GetInt(TouchControlsKey, Application.isMobilePlatform ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TouchControlsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void BeginRun()
    {
        RunPearls = 0;
        if (Mode == FishGameMode.Tutorial)
        {
            ShieldReady = SpeedDashReady = PearlMagnetReady = InvincibilityReady = false;
            return;
        }
        ShieldReady = ConsumePowerUp(0);
        SpeedDashReady = ConsumePowerUp(1);
        PearlMagnetReady = ConsumePowerUp(2);
        InvincibilityReady = ConsumePowerUp(3);
    }

    public static int PowerUpCount(int index) => PlayerPrefs.GetInt($"Fishfish.PowerUp.{index}", 0);

    public static void ResetScoresAndPearls()
    {
        foreach (FishGameMode mode in System.Enum.GetValues(typeof(FishGameMode)))
            PlayerPrefs.DeleteKey($"Fishfish.HighScore.{mode}");
        RunPearls = 0;
        PlayerPrefs.SetInt(PearlKey, 500);
        PlayerPrefs.Save();
    }

    public static bool BuyPowerUp(int index, int price)
    {
        // Store one of each power for the next dive, not a stack of duplicates.
        if (index < 0 || index > 3 || PowerUpCount(index) > 0 || PearlWallet < price) return false;
        PlayerPrefs.SetInt(PearlKey, PearlWallet - price);
        PlayerPrefs.SetInt($"Fishfish.PowerUp.{index}", 1);
        PlayerPrefs.Save();
        return true;
    }

    public static void GrantPowerUp(int index)
    {
        switch (index)
        {
            case 0: ShieldReady = true; break;
            case 1: SpeedDashReady = true; break;
            case 2: PearlMagnetReady = true; break;
            case 3: InvincibilityReady = true; break;
        }
    }

    private static bool ConsumePowerUp(int index)
    {
        int count = PowerUpCount(index);
        if (count <= 0) return false;
        PlayerPrefs.SetInt($"Fishfish.PowerUp.{index}", count - 1);
        PlayerPrefs.Save();
        return true;
    }

    public static void CollectPearl(int amount = 1)
    {
        RunPearls += Mathf.Max(0, amount);
    }

    public static void BankRunPearls()
    {
        if (RunPearls <= 0) return;
        PlayerPrefs.SetInt(PearlKey, PearlWallet + RunPearls);
        PlayerPrefs.Save();
        RunPearls = 0;
    }

    public static bool OwnsSkin(int index)
    {
        return index == 0 || PlayerPrefs.GetInt($"Fishfish.SkinOwned.{index}", 0) == 1;
    }

    public static bool BuySkin(int index, int price)
    {
        if (OwnsSkin(index)) return true;
        if (PearlWallet < price) return false;

        PlayerPrefs.SetInt(PearlKey, PearlWallet - price);
        PlayerPrefs.SetInt($"Fishfish.SkinOwned.{index}", 1);
        PlayerPrefs.Save();
        return true;
    }

    public static void EquipSkin(int index)
    {
        if (!OwnsSkin(index)) return;
        PlayerPrefs.SetInt(SkinKey, index);
        PlayerPrefs.Save();
    }

    public static void MarkTutorialComplete()
    {
        PlayerPrefs.SetInt(TutorialKey, 1);
        PlayerPrefs.Save();
    }
}
