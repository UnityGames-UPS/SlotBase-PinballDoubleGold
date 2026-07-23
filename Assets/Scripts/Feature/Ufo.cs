using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A "land-beside" prize. The ball travels one of its curated routes to a circle beside the UFO,
// then the UFO flashes. Matched to a backend shot by isSpecial + prizeIndex.
public class Ufo : MonoBehaviour
{
  [Tooltip("true = a \"+1 Shot\" special pocket (backend isSpecial).")]
  public bool isSpecial;
  [Tooltip("Backend selectedIndex this UFO pays (into prizes[] normally, or specialPrizes[] when isSpecial).")]
  public int prizeIndex = -1;
  [Tooltip("Runtime-set, bet-dependent prize label.")]
  public TMP_Text prizeAmount;

  [Tooltip("The \"+1 Shot\" object, shown only on special UFOs (toggled on at bonus start). Leave inactive by default.")]
  public GameObject plusShotLabel;

  [Tooltip("One or more curated approach routes (inner entry -> a circle beside this UFO). Random pick per shot.")]
  public List<BallRoute> routes = new List<BallRoute>();

  [Tooltip("The UFO graphic that flashes when the ball lands beside it.")]
  public Image image;
}
