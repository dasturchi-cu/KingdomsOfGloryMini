using System;

namespace KoG.MiniMvp.Network
{
    [Serializable]
    public class AuthResponse
    {
        public bool success;
        public string playerId;
        public string token;
        public string refreshToken;
        public string message;
        public PlayerSummary player;
    }

    [Serializable]
    public class PlayerSummary
    {
        public string nickname;
        public int castle_level;
        public long gold;
        public long mana;
        public long diamond;
    }

    [Serializable]
    public class PlayerStateResponse
    {
        public bool success;
        public PlayerStatePlayer player;
        // Unity JsonUtility requires arrays — List<> does not deserialize JSON arrays.
        public BuildingDto[] buildings;
        public TroopDto[] troops;
        public CampaignDto[] campaigns;
        public UnlockDto unlocks;
        public GoalDto goal;
        public TrainingDto training;
    }

    [Serializable]
    public class UnlockDto
    {
        public string[] placeable;
        public string[] troops;
        public int campaignMax;
        public string[] labels;
    }

    [Serializable]
    public class GoalDto
    {
        public string title;
        public string cta;
    }

    [Serializable]
    public class TrainingDto
    {
        public bool pending;
        public string troopType;
        public int quantity;
        public int secondsLeft;
    }

    [Serializable]
    public class PlayerStatePlayer
    {
        public string id;
        public string nickname;
        public int castleLevel;
        public long gold;
        public long mana;
        public long diamond;
    }

    [Serializable]
    public class BuildingDto
    {
        public string id;
        public string type;
        public int level;
        public int gridX;
        public int gridZ;
        public bool isUnderConstruction;
        public bool isDamaged;
        public int constructionSecondsLeft;
        public int rotationSteps;
    }

    [Serializable]
    public class TroopDto
    {
        public string type;
        public int quantity;
    }

    [Serializable]
    public class CampaignDto
    {
        public int fortressId;
        public int starsEarned;
    }

    [Serializable]
    public class PlaceBuildingResponse
    {
        public bool success;
        public BuildingPlaceDto building;
        public int costGold;
        public long goldBalance;
        public string message;
        public string error;
    }

    [Serializable]
    public class BuildingPlaceDto
    {
        public string id;
        public string type;
        public int level;
        public int gridX;
        public int gridZ;
    }

    [Serializable]
    public class CollectResponse
    {
        public bool success;
        public long goldCollected;
        public long manaCollected;
        public bool dailyMultiplierTriggered;
        public string message;
        public string error;
    }

    [Serializable]
    public class LoginStreakResponse
    {
        public bool success;
        public int currentStreak;
        public long goldRewarded;
        public long diamondsRewarded;
        public string message;
        public string error;
    }

    [Serializable]
    public class TrainResponse
    {
        public bool success;
        public int trainedQuantity;
        public int pendingQuantity;
        public int trainSeconds;
        public bool training;
        public int totalCostMana;
        public string message;
        public string error;
    }

    [Serializable]
    public class UpgradeResponse
    {
        public bool success;
        public int nextLevel;
        public int cost;
        public int upgradeSeconds;
        public bool finishedEarly;
        public UnlockGiftDto gift;
        public UnlockPayloadDto unlocked;
        public string message;
        public string error;
    }

    [Serializable]
    public class UnlockGiftDto
    {
        public long gold;
        public long mana;
        public long diamond;
    }

    [Serializable]
    public class UnlockPayloadDto
    {
        public string[] placeable;
        public string[] troops;
        public int campaignMax;
        public string[] labels;
    }

    [Serializable]
    public class DestroyBuildingResponse
    {
        public bool success;
        public int refundGold;
        public long goldBalance;
        public string message;
        public string error;
    }

    [Serializable]
    public class CampaignStartResponse
    {
        public bool success;
        public string sessionId;
        public int fortressId;
        public string message;
        public string error;
    }

    [Serializable]
    public class CampaignCompleteResponse
    {
        public bool success;
        public int campaignLevelCompleted;
        public int starsEarned;
        public int previousBestStars;
        public BattleResultDto battleResult;
        public LootDto loot;
        public long goldBalance;
        public bool firstClear;
        public TroopDto[] troopsConsumed;
        public string message;
        public string error;
    }

    [Serializable]
    public class BattleResultDto
    {
        public string battleId;
        public bool victory;
        public int stars;
        public int destroyedPercentage;
    }

    [Serializable]
    public class LootDto
    {
        public long gold;
    }

    [Serializable]
    public class ApiError
    {
        public string error;
        public string message;
    }
}
