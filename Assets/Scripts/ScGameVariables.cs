using UnityEngine;

[CreateAssetMenu(fileName = "ScGameVariables", menuName = "Match3/GameVariables")]
public class ScGameVariables : ScriptableObject
{
    public GameObject bgTilePrefabs;
    public ScGem bomb;
    public ScGem[] gems;
    public float bonusAmount = 0.5f;
    public float bombChance = 2f;
    public int dropHeight = 0;
    public float gemSpeed;
    public float scoreSpeed = 5;
    public float cascadeDelay = 0.05f;
    public float bombNeighborDestroyDelay = 0.2f;
    public float bombSelfDestroyDelay = 0.2f;

    [SerializeField] private int rowsSize = 7;
    public int RowsSize => rowsSize;

    [SerializeField] private int colsSize = 7;
    public int ColsSize => colsSize;
}
