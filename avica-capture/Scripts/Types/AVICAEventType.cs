using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AVICA.Capture.Types
{
    public enum CaptureEventType
    {
        // Core Game Events
        StartGame = 1,
        StopGame = 2,
        StartSubGame = 3,
        StopSubGame = 4,

        // PvP
        GameWon = 100,
        GameLost = 101,
        PointWon = 102,
        PointLost = 103,

        // Expanded PvP
        AssetGained = 200,
        AssetLost = 201,

        // Progression
        NextLevelReached = 300,

        // Story-driven events
        ImportantMoment = 400,
        EmotionalMoment = 401,
        GroupScene = 402,

        // Scoring/Results
        PlayerProfile = 501,
        PlayerRating = 502,
        OverallRating = 503,
        FinalResults = 504,
        ShowAwards = 505,

        Custom1 = 1000,
        Custom2 = 1001,
        Custom3 = 1002,
        Custom4 = 1003,
        Custom5 = 1004,
        Custom6 = 1005,
        Custom7 = 1006,
        Custom8 = 1007,
        Custom9 = 1008,
        Custom10 = 1009,
        Custom11 = 1010,
        Custom12 = 1011,
        Custom13 = 1012,
        Custom14 = 1013,
        Custom15 = 1014,
        Custom16 = 1015
    }
}