using System;
using System.Collections.Generic;
using ThumblysFieldTripsAPI.Registers;

namespace AlexBWFieldtrips.DededeeCartRacing;

[Serializable]
internal class CartMinigameGhostRacerData
{
    public byte[] Image;
    public string data;
}

[Serializable]
public class CartMinigameScore(string minigame, float time, string name) : FieldTripScore(minigame, 0, name)
{
    public double time = Convert.ToDouble(time);
}
[Serializable]
public class CartScoresData
{
    public List<CartMinigameScore> cartScores;
}