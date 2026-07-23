using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One marble in the chute — a bottom-up accumulator, not an indexed prize. The marble's index is its
// POSITION in PinballBonusManager.marbles (fill order: [0] = bottom/first, [last] = jackpot/top). A
// chute shot lands on marbles[chuteHits-1], which lights (shared marbleLitSprite) and stays lit for
// the rest of the bonus. So a marble carries no backend index of its own.
public class Marble : MonoBehaviour
{
  [Tooltip("Runtime-set, bet-dependent prize label. Leave empty on the jackpot marble (it uses a JACKPOT graphic).")]
  public TMP_Text prizeAmount;

  [Tooltip("The marble graphic whose sprite is swapped to the lit sprite when collected.")]
  public Image image;
}
