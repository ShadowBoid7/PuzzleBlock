using System;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleBlockGame : MonoBehaviour
{
    // ----- Visual Prefabs -----
    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject goalPrefab;
    public GameObject playerPrefab;
    public GameObject boxPrefab;

    [Header("Levels (ASCII)")]
    [TextArea(5, 20)]
    public string[] levels;

    [Header("UI (Optional)")]
    public UIManager ui;

    public int CurrentLevelIndex { get; private set; }
    public int LevelCount => (levels != null) ? levels.Length : 0;

    // ----- DOD Game State (dados puros) -----
    private int width, height;

    // Tiles do mapa (size = width*height)
    private TileType[] tiles;

    // Ocupação dinâmica (colisão rápida)
    // 0=vazio, 1=player, 2=box
    private byte[] occ;

    // Player
    private int playerX, playerY;

    // Boxes (Structure of Arrays)
    private int[] boxX;
    private int[] boxY;
    private int boxCount;

    // Vitória eficiente
    private int totalGoals;
    private int boxesOnGoals;

    // Visuais instanciados (separados da lógica)
    private Transform visualsRoot;
    private GameObject playerGO;
    private GameObject[] boxGOs;

    private bool levelWon;

    private enum TileType : byte { Floor, Wall, Goal }

    void Start()
    {
        // Não inicia automaticamente.
        // O UIManager chama StartGameFromLevel(0).
    }

    void Update()
    {
        // Se não há nível carregado, não processa input
        if (tiles == null || occ == null || visualsRoot == null) return;
        if (levelWon) return; // opcional: bloquear input depois de ganhar

        Vector2Int dir = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector2Int.right;

        if (dir != Vector2Int.zero)
        {
            TryMove(dir.x, dir.y);
            UpdateAllVisualPositions();

            if (!levelWon && CheckVictory())
            {
                levelWon = true;
                Debug.Log("VITÓRIA! 🎉");
                if (ui != null) ui.OnVictory();
            }
        }
    }

    // =========================================================
    // Public API (controlada pelo UIManager)
    // =========================================================
    public void StartGameFromLevel(int levelIndex)
    {
        if (LevelCount == 0)
        {
            Debug.LogError("Nenhum nível definido! Preenche o array 'levels' no Inspector.");
            return;
        }

        CurrentLevelIndex = Mathf.Clamp(levelIndex, 0, LevelCount - 1);
        LoadLevel(CurrentLevelIndex);
    }

    public void RestartLevel()
    {
        if (LevelCount == 0) return;
        LoadLevel(CurrentLevelIndex);
    }

    public void LoadNextLevel()
    {
        if (LevelCount == 0) return;

        int next = Mathf.Min(CurrentLevelIndex + 1, LevelCount - 1);
        CurrentLevelIndex = next;
        LoadLevel(CurrentLevelIndex);
    }

    public void StopGameVisuals()
    {
        if (visualsRoot != null)
        {
            Destroy(visualsRoot.gameObject);
            visualsRoot = null;
        }

        // Opcional: limpar referências para impedir input
        tiles = null;
        occ = null;
        boxX = null;
        boxY = null;
        boxGOs = null;
        playerGO = null;

        levelWon = false;
    }

    // =========================================================
    // Level Loading
    // =========================================================
    private void LoadLevel(int idx)
    {
        // Limpa visuais antigos
        if (visualsRoot != null)
            Destroy(visualsRoot.gameObject);

        visualsRoot = new GameObject("Visuals_Runtime").transform;

        string level = levels[idx];
        if (string.IsNullOrWhiteSpace(level))
        {
            Debug.LogError($"Nível {idx} está vazio.");
            return;
        }

        ParseLevel(level.Trim());
        BuildVisuals();
        UpdateAllVisualPositions();
        AdjustCameraToLevel();

        levelWon = false;

        if (ui != null)
            ui.RefreshLevel(CurrentLevelIndex, LevelCount);
    }

    // =========================================================
    // Nível -> arrays (DOD)
    // =========================================================
    private void ParseLevel(string text)
    {
        var linesRaw = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();

        foreach (var l in linesRaw)
        {
            var trimmed = l.TrimEnd();
            if (!string.IsNullOrWhiteSpace(trimmed))
                lines.Add(trimmed);
        }

        height = lines.Count;
        width = lines[0].Length;

        tiles = new TileType[width * height];
        occ = new byte[width * height];

        totalGoals = 0;
        boxCount = 0;

        // 1ª passagem: contar boxes e goals
        for (int y = 0; y < height; y++)
        {
            if (lines[y].Length != width)
                throw new Exception("Todas as linhas do nível devem ter a mesma largura.");

            for (int x = 0; x < width; x++)
            {
                char c = lines[height - 1 - y][x]; // inverter Y para ficar natural no Unity

                if (c == 'G' || c == '*') totalGoals++;
                if (c == 'B' || c == '*') boxCount++;
            }
        }

        boxX = new int[boxCount];
        boxY = new int[boxCount];
        boxGOs = new GameObject[boxCount];

        // 2ª passagem: preencher dados
        int boxIndex = 0;
        boxesOnGoals = 0;

        for (int y = 0; y < height; y++)
        {
            string line = lines[height - 1 - y];

            for (int x = 0; x < width; x++)
            {
                char c = line[x];
                int i = Idx(x, y);

                switch (c)
                {
                    case '#':
                        tiles[i] = TileType.Wall;
                        break;

                    case 'G':
                        tiles[i] = TileType.Goal;
                        break;

                    case 'P':
                        tiles[i] = TileType.Floor;
                        playerX = x; playerY = y;
                        occ[i] = 1;
                        break;

                    case 'B':
                        tiles[i] = TileType.Floor;
                        boxX[boxIndex] = x;
                        boxY[boxIndex] = y;
                        occ[i] = 2;
                        boxIndex++;
                        break;

                    case '*': // box em goal
                        tiles[i] = TileType.Goal;
                        boxX[boxIndex] = x;
                        boxY[boxIndex] = y;
                        occ[i] = 2;
                        boxIndex++;
                        boxesOnGoals++;
                        break;

                    default: // '.' ou espaço => chão
                        tiles[i] = TileType.Floor;
                        break;
                }
            }
        }
    }

    // =========================================================
    // Movimento (sistema DOD)
    // =========================================================
    private void TryMove(int dx, int dy)
    {
        int tx = playerX + dx;
        int ty = playerY + dy;

        if (!InBounds(tx, ty)) return;

        int tIdx = Idx(tx, ty);

        // Parede bloqueia
        if (tiles[tIdx] == TileType.Wall) return;

        byte targetOcc = occ[tIdx];

        // Vazio -> move player
        if (targetOcc == 0)
        {
            MovePlayerTo(tx, ty);
            return;
        }

        // Caixa -> tentar empurrar
        if (targetOcc == 2)
        {
            int pushX = tx + dx;
            int pushY = ty + dy;

            if (!InBounds(pushX, pushY)) return;

            int pushIdx = Idx(pushX, pushY);

            if (tiles[pushIdx] == TileType.Wall) return;
            if (occ[pushIdx] != 0) return;

            int b = FindBoxAt(tx, ty);
            if (b < 0) return;

            UpdateBoxesOnGoalsBeforeMove(b);

            // mover caixa
            occ[tIdx] = 0;
            boxX[b] = pushX;
            boxY[b] = pushY;
            occ[pushIdx] = 2;

            UpdateBoxesOnGoalsAfterMove(b);

            // mover player
            MovePlayerTo(tx, ty);
        }
    }

    private void MovePlayerTo(int nx, int ny)
    {
        occ[Idx(playerX, playerY)] = 0;
        playerX = nx; playerY = ny;
        occ[Idx(playerX, playerY)] = 1;
    }

    private int FindBoxAt(int x, int y)
    {
        for (int i = 0; i < boxCount; i++)
            if (boxX[i] == x && boxY[i] == y)
                return i;
        return -1;
    }

    private void UpdateBoxesOnGoalsBeforeMove(int boxIndex)
    {
        int i = Idx(boxX[boxIndex], boxY[boxIndex]);
        if (tiles[i] == TileType.Goal) boxesOnGoals--;
    }

    private void UpdateBoxesOnGoalsAfterMove(int boxIndex)
    {
        int i = Idx(boxX[boxIndex], boxY[boxIndex]);
        if (tiles[i] == TileType.Goal) boxesOnGoals++;
    }

    private bool CheckVictory()
    {
        return (totalGoals > 0) && (boxesOnGoals == totalGoals);
    }

    // =========================================================
    // Visual (separado da lógica)
    // =========================================================
    private void BuildVisuals()
    {
        // Tiles
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = Idx(x, y);
                var t = tiles[i];

                GameObject prefab = (t == TileType.Wall) ? wallPrefab :
                                    (t == TileType.Goal) ? goalPrefab :
                                    floorPrefab;

                var go = Instantiate(prefab, GridToWorld(x, y), Quaternion.identity, visualsRoot);
                go.name = $"Tile_{t}_{x}_{y}";
            }

        // Player
        playerGO = Instantiate(playerPrefab, GridToWorld(playerX, playerY), Quaternion.identity, visualsRoot);
        playerGO.name = "Player";

        // Boxes
        for (int i = 0; i < boxCount; i++)
        {
            boxGOs[i] = Instantiate(boxPrefab, GridToWorld(boxX[i], boxY[i]), Quaternion.identity, visualsRoot);
            boxGOs[i].name = $"Box_{i}";
        }
    }

    private void UpdateAllVisualPositions()
    {
        if (playerGO != null)
            playerGO.transform.position = GridToWorld(playerX, playerY);

        for (int i = 0; i < boxCount; i++)
            if (boxGOs[i] != null)
                boxGOs[i].transform.position = GridToWorld(boxX[i], boxY[i]);
    }

    [SerializeField] private float boardYOffset = 1.5f;

    private Vector3 GridToWorld(int x, int y)
    {
        float offsetX = -width / 2f + 0.5f;
        float offsetY = -height / 2f + 0.5f + boardYOffset;
        return new Vector3(x + offsetX, y + offsetY, 0f);
    }

    private void AdjustCameraToLevel()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float levelWidth = width;
        float levelHeight = height;

        float screenRatio = (float)Screen.width / Screen.height;
        float targetRatio = levelWidth / levelHeight;

        if (screenRatio >= targetRatio)
        {
            // Screen mais larga → ajustar pela altura
            cam.orthographicSize = levelHeight / 2f + 1f;
        }
        else
        {
            // Screen mais alta → ajustar pela largura
            float size = levelWidth / screenRatio;
            cam.orthographicSize = size / 2f + 1f;
        }
    }

    // =========================================================
    // Helpers
    // =========================================================
    private int Idx(int x, int y) => y * width + x;

    private bool InBounds(int x, int y) =>
        x >= 0 && x < width && y >= 0 && y < height;
}


