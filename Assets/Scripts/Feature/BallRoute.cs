using System.Collections.Generic;
using UnityEngine.UI;

// One curated route: the ordered inner circles the ball lights on its way to a prize.
// A UFO holds one or more of these (random pick per shot); the marbles share a set of approach routes.
[System.Serializable]
public class BallRoute
{
  public List<Image> circles = new List<Image>();
}
