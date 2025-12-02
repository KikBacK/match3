using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using GlobalEnums;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Threading.Tasks;

public class ScGameLogicManager : MonoBehaviour
{
    [SerializeField] private ScGameVariables gameVariables;  
    [SerializeField] private ScoreView scoreView;
    [SerializeField] private Transform gemsHandler;
    
    private ScGemPoolService _gemPool;
    private readonly HashSet<ScGem> _bombs = new HashSet<ScGem>();
    private float _score = 0;
    private float _displayScore = 0;
    private GameBoard _gameBoard;
    private GameState _currentState = GameState.Move;
    public GameState CurrentState => _currentState;

    private void Awake() => Init();

    private void Init()
    {
        _gameBoard = new GameBoard(7, 7);
        _gemPool = new ScGemPoolService(gemsHandler);
        Setup();
    }
    
    private void Setup()
    {
        for (var x = 0; x < _gameBoard.Width; x++)
        {
            for (var y = 0; y < _gameBoard.Height; y++)
            {
                var pos = new Vector2(x, y);
                var bgTile = Instantiate(gameVariables.bgTilePrefabs, pos, Quaternion.identity);
                bgTile.transform.SetParent(gemsHandler);
                bgTile.name = "BG Tile - " + x + ", " + y;

                var boardPos = new Vector2Int(x, y);
                var gemToSpawn = GetRandomGemForPosition(boardPos);
                SpawnGem(boardPos, gemToSpawn);
            }
        }
    }

    private ScGem GetRandomGemForPosition(Vector2Int position)
    {
        var gems = gameVariables.gems;
        if (gems == null || gems.Length == 0)
            return null;

        var gemIndex = Random.Range(0, gems.Length);
        var iterations = 0;

        while (_gameBoard.MatchesAt(position, gems[gemIndex]) && iterations < 100)
        {
            gemIndex = Random.Range(0, gems.Length);
            iterations++;
        }

        return gems[gemIndex];
    }

    private void SpawnGem(Vector2Int position, ScGem gemToSpawn)
    {
        var spawnPosition = new Vector3(position.x, position.y + gameVariables.dropHeight, 0f);
        var gem = _gemPool.Spawn(gemToSpawn, spawnPosition);

        if (gemsHandler != null)
            gem.transform.SetParent(gemsHandler);

        gem.name = $"Gem - {position.x}, {position.y}";

        _gameBoard.SetGem(position.x, position.y, gem);
        gem.SetupGem(this, gameVariables, position);
        if (gemToSpawn == gameVariables.bomb)
            _bombs.Add(gem);
    }

    public void SetGem(int x,int y, ScGem gem) => _gameBoard.SetGem(x,y, gem);

    public ScGem GetGem(int x, int y) => _gameBoard.GetGem(x, y);

    public void SetState(GameState currentState) => _currentState = currentState;

    public void DestroyMatches()
    {
        var matches = _gameBoard.CurrentMatches;
        if (matches == null || matches.Count == 0)
            return;

        var bombMatches = new List<ScGem>();
        var regularMatches = new List<ScGem>();

        foreach (var gem in matches)
        {
            if (gem == null)
                continue;

            if (IsBomb(gem))
                bombMatches.Add(gem);
            else
                regularMatches.Add(gem);
        }

        if (bombMatches.Count == 0)
        {
            HandleRegularMatchesOnly(regularMatches);
        }
        else
        {
            StartCoroutine(DestroyMatchesWithBombsCo(regularMatches, bombMatches));
        }
    }

    private void HandleRegularMatchesOnly(List<ScGem> regularMatches)
    {
        if (regularMatches == null || regularMatches.Count == 0)
            return;

        Vector2Int? bombPos = null;
        ScGem bombSource = null;

        if (regularMatches.Count >= 4)
        {
            bombSource = SelectBombSourceFromRegularMatches(regularMatches);
            if (bombSource != null)
            {
                bombPos = bombSource.posIndex;
            }
        }

        foreach (var gem in regularMatches)
        {
            if (gem == null)
                continue;

            // If this is the chosen slot for a bomb, skip destroying it as a regular piece.
            if (bombPos.HasValue && gem.posIndex == bombPos.Value)
                continue;

            ScoreCheck(gem);
            DestroyMatchedGemsAt(gem.posIndex);
        }

        if (bombPos.HasValue && bombSource != null)
        {
            CreateBombAt(bombPos.Value, bombSource);
        }

        StartCoroutine(DecreaseRowCo());
    }

    private ScGem SelectBombSourceFromRegularMatches(List<ScGem> regularMatches)
    {
        if (regularMatches == null || regularMatches.Count == 0)
            return null;

        // Final fallback: if neither swapped gem is in the match,
        // just use the first matched gem.
        return regularMatches[0];
    }

    private IEnumerator DestroyMatchesWithBombsCo(List<ScGem> regularMatches, List<ScGem> bombMatches)
    {
        var regularCount = regularMatches?.Count ?? 0;
        var bombCount = bombMatches?.Count ?? 0;
        var isDoubleBombMatch = bombMatches != null && bombMatches.Count >= 2;

        // Decide if we should create a new bomb:
        // - total matched pieces (regular + bomb) must be 4 or more
        // - and there must be at least 3 regular pieces (bomb + 3 regular)
        // - BUT if this is a double-bomb match, no new bomb is created (special interaction).
        Vector2Int? bombPos = null;
        ScGem bombSource = null;
        if (!isDoubleBombMatch && regularCount + bombCount >= 4 && regularCount >= 3)
        {
            bombSource = SelectBombSourceFromRegularMatches(regularMatches);
            if (bombSource != null)
            {
                bombPos = bombSource.posIndex;
            }
        }

        // First, handle all regular matches (non-bombs) except the reserved bomb slot.
        if (regularMatches != null)
        {
            foreach (var gem in regularMatches)
            {
                if (gem == null)
                    continue;

                if (bombPos.HasValue && gem.posIndex == bombPos.Value)
                    continue;

                ScoreCheck(gem);
                DestroyMatchedGemsAt(gem.posIndex);
            }
        }

        // Then, handle bombs.
        if (bombMatches != null && bombMatches.Count > 0)
        {
            if (isDoubleBombMatch)
            {
                // Special interaction: matching 2 or more bombs triggers a board-wide explosion.
                if (gameVariables.bombNeighborDestroyDelay > 0f)
                    yield return new WaitForSeconds(gameVariables.bombNeighborDestroyDelay);
                
                for (var x = 0; x < _gameBoard.Width; x++)
                {
                    for (var y = 0; y < _gameBoard.Height; y++)
                    {
                        var gem = _gameBoard.GetGem(x, y);
                        if (gem == null)
                            continue;

                        ScoreCheck(gem);
                        DestroyMatchedGemsAt(new Vector2Int(x, y));
                    }
                }
            }
            else
            {
                // Default behavior: each bomb destroys its 3x3 neighbor group, then itself.
                foreach (var bomb in bombMatches)
                {
                    if (bomb == null)
                        continue;

                    // Optional delay before destroying neighbors.
                    if (gameVariables.bombNeighborDestroyDelay > 0f)
                        yield return new WaitForSeconds(gameVariables.bombNeighborDestroyDelay);

                    var center = bomb.posIndex;

                    foreach (var neighborPos in GetBombNeighborPositions(center))
                    {
                        // Do not destroy the reserved bomb slot (where the new bomb will be created).
                        if (bombPos.HasValue && neighborPos == bombPos.Value)
                            continue;

                        var neighbor = _gameBoard.GetGem(neighborPos.x, neighborPos.y);
                        if (neighbor == null)
                            continue;

                        ScoreCheck(neighbor);
                        DestroyMatchedGemsAt(neighborPos);
                    }

                    // Optional delay before destroying the bomb itself.
                    if (gameVariables.bombSelfDestroyDelay > 0f)
                        yield return new WaitForSeconds(gameVariables.bombSelfDestroyDelay);

                    ScoreCheck(bomb);
                    DestroyMatchedGemsAt(center);
                }
            }
        }

        // After all bombs have exploded and been destroyed, create a new bomb if needed.
        // Double-bomb interaction does not create a new bomb (special behavior).
        if (!isDoubleBombMatch && bombPos.HasValue && bombSource != null)
        {
            CreateBombAt(bombPos.Value, bombSource);
        }

        // Only after all bombs have exploded and been destroyed do we start cascading.
        StartCoroutine(DecreaseRowCo());
    }

    private IEnumerable<Vector2Int> GetBombNeighborPositions(Vector2Int center)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var x = center.x + dx;
                var y = center.y + dy;

                if (x < 0 || x >= _gameBoard.Width || y < 0 || y >= _gameBoard.Height)
                    continue;

                yield return new Vector2Int(x, y);
            }
        }
    }

    private void CreateBombAt(Vector2Int position, ScGem sourceGem)
    {
        // Remove the original gem at this position (do not trigger destroy effect here).
        var existingGem = _gameBoard.GetGem(position.x, position.y);
        if (existingGem != null)
        {
            ReturnGemToPool(existingGem);
            SetGem(position.x, position.y, null);
        }

        // Spawn a bomb piece at this slot. Color-sync logic (sprite/material)
        // should be handled inside the bomb prefab / ScGem implementation.
        var bomb = _gemPool.Spawn(gameVariables.bomb, new Vector3(position.x, position.y + gameVariables.dropHeight, 0f));
        if (gemsHandler != null)
            bomb.transform.SetParent(gemsHandler);

        bomb.name = $"Bomb - {position.x}, {position.y}";
        _gameBoard.SetGem(position.x, position.y, bomb);
        bomb.SetupGem(this, gameVariables, position);
        _bombs.Add(bomb);

        // Configure bomb logical color so that its interaction rules work correctly.
        var bombComponent = bomb.GetComponent<ScBomb>();
        if (bombComponent != null && sourceGem != null)
        {
            GemType bombColor;

            // If the source is already a bomb, reuse its logical color.
            if (sourceGem is ScBomb sourceBomb)
            {
                bombColor = sourceBomb.color;
            }
            else
            {
                // Assumes ScGem exposes its color as a GemType named "color".
                bombColor = sourceGem.type;
            }

            bombComponent.SetBombColor(bombColor);
        }
    }
    
    private IEnumerator DecreaseRowCo()
    {
        yield return new WaitForSeconds(.2f);

        for (var x = 0; x < _gameBoard.Width; x++)
        {
            var nullCounter = 0;
            for (var y = 0; y < _gameBoard.Height; y++)
            {
                var curGem = _gameBoard.GetGem(x, y);
                if (curGem == null)
                {
                    nullCounter++;
                }
                else if (nullCounter > 0)
                {
                    curGem.posIndex.y -= nullCounter;
                    SetGem(x, y - nullCounter, curGem);
                    SetGem(x, y, null);

                    // let this gem visually fall before processing the next one
                    yield return new WaitForSeconds(gameVariables.cascadeDelay);
                }
            }
        }

        StartCoroutine(FilledBoardCo());
    }

    private async Task UpdateScoreTask()
    {
        while (!Mathf.Approximately(_displayScore, _score))
        {
            if(destroyCancellationToken.IsCancellationRequested)
                return;
            
            _displayScore = Mathf.MoveTowards(_displayScore, _score, gameVariables.scoreSpeed * Time.deltaTime);
            scoreView.SetScore(_displayScore);
            await Task.Yield();
        }

        scoreView.SetScore(_score);
    }

    private void ScoreCheck(ScGem gemToCheck)
    {
        _score += gemToCheck.scoreValue;
        _ = UpdateScoreTask();
    }

    private void ReturnGemToPool(ScGem gem)
    {
        if (gem == null)
            return;

        _bombs.Remove(gem);
        _gemPool.Release(gem);
    }

    private bool IsBomb(ScGem gem) => gem != null && _bombs.Contains(gem);

    private void DestroyMatchedGemsAt(Vector2Int pos)
    {
        var curGem = _gameBoard.GetGem(pos.x, pos.y);
        if (curGem == null)
            return;

        Instantiate(curGem.destroyEffect, new Vector2(pos.x, pos.y), Quaternion.identity);

        // Return gem to pool instead of destroying it
        ReturnGemToPool(curGem);
        SetGem(pos.x, pos.y, null);
    }

    private IEnumerator FilledBoardCo()
    {
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(RefillBoardCo());
        yield return new WaitForSeconds(0.5f);
        _gameBoard.FindAllMatches();
        if (_gameBoard.CurrentMatches.Count > 0)
        {
            yield return new WaitForSeconds(0.5f);
            DestroyMatches();
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            _currentState = GameState.Move;
        }
    }
    
    private IEnumerator RefillBoardCo()
    {
        for (var x = 0; x < _gameBoard.Width; x++)
        {
            for (var y = 0; y < _gameBoard.Height; y++)
            {
                var curGem = _gameBoard.GetGem(x, y);
                if (curGem != null)
                    continue;

                var position = new Vector2Int(x, y);
                var gemToSpawn = GetRandomGemForPosition(position);
                if (gemToSpawn != null)
                {
                    SpawnGem(position, gemToSpawn);
                }

                // spawn new gems one by one
                yield return new WaitForSeconds(gameVariables.cascadeDelay);
            }
        }

        CheckMisplacedGems();
    }
    
    private void CheckMisplacedGems()
    {
        var foundGems = new List<ScGem>();
        foundGems.AddRange(FindObjectsOfType<ScGem>());
        for (var x = 0; x < _gameBoard.Width; x++)
        {
            for (var y = 0; y < _gameBoard.Height; y++)
            {
                var curGem = _gameBoard.GetGem(x, y);
                if (foundGems.Contains(curGem))
                    foundGems.Remove(curGem);
            }
        }

        foreach (var g in foundGems)
            Destroy(g.gameObject);
    }
    
    public void FindAllMatches() => _gameBoard.FindAllMatches();
}
