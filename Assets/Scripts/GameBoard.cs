using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameBoard
{
    private readonly int _height;
    private readonly int _width;
    private readonly ScGem[,] _allGems;
    private List<ScGem> _currentMatches = new List<ScGem>();

    /// <summary>
    /// Determines whether two gems belong to the same match group,
    /// taking into account normal gems, generic bombs and colored bombs.
    /// Rules:
    /// - If neither is a bomb: they must have the same type.
    /// - If one is a bomb: delegate to ScBomb.CanInteractWith.
    /// </summary>
    private bool AreInSameMatchGroup(ScGem a, ScGem b)
    {
        if (a == null || b == null)
            return false;

        if (a is ScBomb bombA)
            return bombA.CanInteractWith(b);

        if (b is ScBomb bombB)
            return bombB.CanInteractWith(a);

        return a.type == b.type;
    }
    
    public int Height => _height;
    public int Width => _width;
    public List<ScGem> CurrentMatches => _currentMatches;

    public GameBoard(int width, int height)
    {
        _width = width;
        _height = height;
        _allGems = new ScGem[width, height];
    }

    public bool MatchesAt(Vector2Int positionToCheck, ScGem gemToCheck)
    {
        if (gemToCheck == null)
            return false;

        var x = positionToCheck.x;
        var y = positionToCheck.y;

        // Horizontal check: [x-2][x-1][x]
        if (x > 1)
        {
            var left1 = _allGems[x - 1, y];
            var left2 = _allGems[x - 2, y];

            if (left1 != null && left2 != null &&
                AreInSameMatchGroup(gemToCheck, left1) &&
                AreInSameMatchGroup(gemToCheck, left2))
            {
                return true;
            }
        }

        // Vertical check: [y-2][y-1][y]
        if (y <= 1) return false;
        
        var down1 = _allGems[x, y - 1];
        var down2 = _allGems[x, y - 2];

        return down1 != null && down2 != null &&
               AreInSameMatchGroup(gemToCheck, down1) &&
               AreInSameMatchGroup(gemToCheck, down2);
    }

    public void SetGem(int x, int y, ScGem gem) => _allGems[x, y] = gem;

    public ScGem GetGem(int x, int y) => _allGems[x, y];

    public void FindAllMatches()
    {
        _currentMatches.Clear();

        for (var x = 0; x < _width; x++)
        {
            for (var y = 0; y < _height; y++)
            {
                var currentGem = _allGems[x, y];
                if (currentGem == null)
                    continue;

                TryAddHorizontalMatch(x, y, currentGem);
                TryAddVerticalMatch(x, y, currentGem);
            }
        }

        if (_currentMatches.Count > 0)
            _currentMatches = _currentMatches.Distinct().ToList();

        CheckForBombs();
    }

    private void TryAddHorizontalMatch(int x, int y, ScGem currentGem)
    {
        if (x <= 0 || x >= _width - 1)
            return;

        var leftGem = _allGems[x - 1, y];
        var rightGem = _allGems[x + 1, y];
        
        if (leftGem == null || rightGem == null)
            return;
        
        if (CheckDoubleBombMatch(currentGem, rightGem))
        {
            MarkAsMatch(new List<ScGem>(){currentGem,  rightGem});
            return;
        }
        
        if (CheckDoubleBombMatch(currentGem, leftGem))
        {
            MarkAsMatch(new List<ScGem>(){currentGem,  leftGem});
            return;
        }
        
        // Standard 3-in-a-row by group (colors / bombs)
        if (!AreInSameMatchGroup(currentGem, leftGem) ||
            !AreInSameMatchGroup(currentGem, rightGem))
            return;

        MarkAsMatch(new List<ScGem>(){currentGem,  leftGem, rightGem});
    }

    private bool CheckDoubleBombMatch(ScGem firstGem, ScGem secondGem)
    {
        if (firstGem == null || secondGem == null)
            return false;

        return firstGem is ScBomb && secondGem is ScBomb;
    }

    private void TryAddVerticalMatch(int x, int y, ScGem currentGem)
    {
        if (y <= 0 || y >= _height - 1)
            return;

        var aboveGem = _allGems[x, y - 1];
        var belowGem = _allGems[x, y + 1];

        if (aboveGem == null || belowGem == null)
            return;
        
        if (CheckDoubleBombMatch(currentGem, aboveGem))
        {
            MarkAsMatch(new List<ScGem>(){currentGem,  aboveGem});
            return;
        }
        
        if (CheckDoubleBombMatch(currentGem, belowGem))
        {
            MarkAsMatch(new List<ScGem>(){currentGem,  belowGem});
            return;
        }

        // Standard 3-in-a-row by group (colors / bombs)
        if (!AreInSameMatchGroup(currentGem, aboveGem) ||
            !AreInSameMatchGroup(currentGem, belowGem))
            return;

        MarkAsMatch(new List<ScGem>(){currentGem, belowGem, aboveGem});
    }

    private void MarkAsMatch(List<ScGem> gems)
    {
        if(gems == null || gems.Count == 0)
            return;
        foreach (var gem in gems)
        {
            if (gem == null)
                continue;

            gem.isMatch = true;
            _currentMatches.Add(gem);
        }
    }

    private void CheckForBombs()
    {
        if (_currentMatches.Count == 0)
            return;

        // Offsets for 4-directional neighbors (left, right, down, up)
        Vector2Int[] neighborOffsets =
        {
            new (-1, 0),
            new (1, 0),
            new (0, -1),
            new (0, 1)
        };

        var matches = _currentMatches.ToList();
        foreach (var gem in matches)
        {
            var pos = gem.posIndex;

            foreach (var offset in neighborOffsets)
            {
                var nx = pos.x + offset.x;
                var ny = pos.y + offset.y;

                if (nx < 0 || nx >= _width || ny < 0 || ny >= _height)
                    continue;

                var neighbor = _allGems[nx, ny];
                var bomb = neighbor as ScBomb;
                // Trigger only bombs that can interact with this matched gem.
                if (bomb != null && bomb.CanInteractWith(gem))
                {
                    MarkBombArea(new Vector2Int(nx, ny), neighbor.blastSize);
                }
            }
        }
    }

    private void MarkBombArea(Vector2Int bombPos, int blastSize)
    {
        for (var x = bombPos.x - blastSize; x <= bombPos.x + blastSize; x++)
        {
            for (var y = bombPos.y - blastSize; y <= bombPos.y + blastSize; y++)
            {
                if (x < 0 || x >= _width || y < 0 || y >= _height)
                    continue;

                var gem = _allGems[x, y];
                if (gem == null)
                    continue;

                gem.isMatch = true;
                _currentMatches.Add(gem);
            }
        }

        if (_currentMatches.Count > 0)
            _currentMatches = _currentMatches.Distinct().ToList();
    }
}
