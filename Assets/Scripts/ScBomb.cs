using GlobalEnums;
using UnityEngine;

public class ScBomb : ScGem
{
    [SerializeField] private SpriteRenderer _bombImage;

    // Logical color of the bomb: defines which color group it belongs to.
    // GemType.Bomb means "generic" bomb that can interact with any color.
    public GemType color; // BombColor

    /// <summary>
    /// Configure the bomb's logical color and update its visual.
    /// </summary>
    public void SetBombColor(GemType bombColor)
    {
        color = bombColor;
        SetImageColor();
    }

    /// <summary>
    /// Returns true if this bomb can form a match / interact with the given gem
    /// according to the Task 3 rules:
    /// - Normal bomb (GemType.Bomb) can interact with any gem.
    /// - Colored bomb can only interact with gems of its own color group.
    /// </summary>
    public bool CanInteractWith(ScGem other)
    {
        if (other == null)
            return false;

        // Normal bomb can interact with any gem.
        if (color == GemType.Bomb)
            return true;

        // If the other piece is also a bomb, check its logical color.
        if (other is ScBomb otherBomb)
        {
            // Colored bomb interacts only with bombs of the same color
            // or with generic bombs.
            return otherBomb.color == GemType.Bomb || otherBomb.color == color;
        }

        // For regular gems, assume they expose a GemType compatible with bomb color.
        // This assumes ScGem has a public GemType field or property called "color".
        return other.type == color;
    }

    private void SetImageColor()
    {
        switch (color)
        {
            case GemType.Blue:
                _bombImage.color = Color.blue;
                break;
            case GemType.Green:
                _bombImage.color = Color.green;
                break;
            case GemType.Red:
                _bombImage.color = Color.red;
                break;
            case GemType.Purple:
                _bombImage.color = Color.magenta;
                break;
            case GemType.Yellow:
                _bombImage.color = Color.yellow;
                break;
            case GemType.Bomb:
                // Generic bomb: use neutral color (white) or any special visual you want.
                _bombImage.color = Color.white;
                break;
        }
    }
}