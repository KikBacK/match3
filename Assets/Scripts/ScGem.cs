using System.Collections;
using GlobalEnums;
using UnityEngine;


public class ScGem : MonoBehaviour
{
    [HideInInspector]
    public Vector2Int posIndex;

    private const float MinSwipeDistance = 0.5f;

    private Vector2 _firstTouchPosition;
    private Vector2 _finalTouchPosition;
    private bool _mousePressed;
    private float _swipeAngle;
    private ScGem _otherGem;

    public GemType type;
    public bool isMatch = false;
    public GameObject destroyEffect;
    public int scoreValue = 10;
    public int blastSize = 1;

    private Vector2Int _previousPos;
    private Vector2Int _registeredBoardPos;
    private bool _isBoardPosRegistered;
    private ScGameLogicManager _gameLogicManager;
    private ScGameVariables _gameVariables;
    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;
        _isBoardPosRegistered = false;
    }

    private void Update()
    {
        UpdatePosition();
        HandleInput();
    }

    private void UpdatePosition()
    {
        // If game logic or variables are not yet assigned, skip movement.
        if (_gameVariables == null || _gameLogicManager == null)
            return;

        var target = new Vector3(posIndex.x, posIndex.y, transform.position.z);
        var toTarget = target - transform.position;

        // Use squared magnitude to avoid unnecessary sqrt.
        const float snapThreshold = 0.01f;
        if (toTarget.sqrMagnitude > snapThreshold * snapThreshold)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                target,
                _gameVariables.gemSpeed * Time.deltaTime);
            return;
        }

        // Snap to exact cell position.
        transform.position = target;

        // Register position on board only when it actually changed or was not yet registered.
        if (_isBoardPosRegistered && _registeredBoardPos == posIndex) 
            return;
        
        _registeredBoardPos = posIndex;
        _isBoardPosRegistered = true;
        _gameLogicManager.SetGem(posIndex.x, posIndex.y, this);
    }

    private void HandleInput()
    {
        if (!_mousePressed)
            return;

        if (!Input.GetMouseButtonUp(0))
            return;

        _mousePressed = false;

        if (_gameLogicManager == null || _gameLogicManager.CurrentState != GameState.Move)
            return;

        if (_camera == null)
            _camera = Camera.main;

        _finalTouchPosition = _camera.ScreenToWorldPoint(Input.mousePosition);
        CalculateAngleAndMove();
    }

    public void SetupGem(ScGameLogicManager gameLogicManager, ScGameVariables gameVariables, Vector2Int position)
    {
        _gameLogicManager = gameLogicManager;
        _gameVariables = gameVariables;
        posIndex = position;
        _registeredBoardPos = position;
        _isBoardPosRegistered = false; // Let UpdatePosition register the new position once it has snapped.
    }

    private void OnMouseDown()
    {
        if (_gameLogicManager == null || _gameLogicManager.CurrentState != GameState.Move)
            return;

        if (_camera == null)
            _camera = Camera.main;

        _firstTouchPosition = _camera.ScreenToWorldPoint(Input.mousePosition);
        _mousePressed = true;
    }

    private void CalculateAngleAndMove()
    {
        var direction = _finalTouchPosition - _firstTouchPosition;
        _swipeAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (direction.magnitude <= MinSwipeDistance)
            return;

        MovePieces();
    }

    private void MovePieces()
    {
        _previousPos = posIndex;
        _otherGem = null;

        switch (_swipeAngle)
        {
            // Right swipe
            case < 45f and > -45f when posIndex.x < _gameVariables.RowsSize - 1:
            {
                _otherGem = _gameLogicManager.GetGem(posIndex.x + 1, posIndex.y);
                if (_otherGem != null)
                {
                    _otherGem.posIndex.x--;
                    posIndex.x++;
                }

                break;
            }
            // Up swipe
            case > 45f and <= 135f when posIndex.y < _gameVariables.ColsSize - 1:
            {
                _otherGem = _gameLogicManager.GetGem(posIndex.x, posIndex.y + 1);
                if (_otherGem != null)
                {
                    _otherGem.posIndex.y--;
                    posIndex.y++;
                }

                break;
            }
            // Down swipe
            case < -45f and >= -135f when posIndex.y > 0:
            {
                _otherGem = _gameLogicManager.GetGem(posIndex.x, posIndex.y - 1);
                if (_otherGem != null)
                {
                    _otherGem.posIndex.y++;
                    posIndex.y--;
                }

                break;
            }
            // Left swipe
            case > 135f or < -135f when posIndex.x > 0:
            {
                _otherGem = _gameLogicManager.GetGem(posIndex.x - 1, posIndex.y);
                if (_otherGem != null)
                {
                    _otherGem.posIndex.x++;
                    posIndex.x--;
                }

                break;
            }
        }

        if (_otherGem == null)
            return;

        _gameLogicManager.SetGem(posIndex.x, posIndex.y, this);
        _gameLogicManager.SetGem(_otherGem.posIndex.x, _otherGem.posIndex.y, _otherGem);

        StartCoroutine(CheckMoveCo());
    }

    private IEnumerator CheckMoveCo()
    {
        _gameLogicManager.SetState(GameState.Wait);

        yield return new WaitForSeconds(.5f);
        _gameLogicManager.FindAllMatches();

        if (_otherGem == null) 
            yield break;
        
        if (!isMatch && !_otherGem.isMatch)
        {
            _otherGem.posIndex = posIndex;
            posIndex = _previousPos;

            _gameLogicManager.SetGem(posIndex.x, posIndex.y, this);
            _gameLogicManager.SetGem(_otherGem.posIndex.x, _otherGem.posIndex.y, _otherGem);

            yield return new WaitForSeconds(.5f);
            _gameLogicManager.SetState(GameState.Move);
        }
        else
        {
            _gameLogicManager.DestroyMatches();
        }
    }
}
