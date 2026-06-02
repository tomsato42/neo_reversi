using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public interface ISetDisk
{
    public void SetDisk(int x, int y, Transform tile);
}

internal enum DiskColor
{
    None,
    Black,
    White,
}

public class Game : MonoBehaviour, ISetDisk
{
    [SerializeField] private Tile tilePrefab;
    [SerializeField] private Disk diskPrefab;

    [SerializeField] private GameObject blackTurnUI;
    [SerializeField] private GameObject whiteTurnUI;

    [SerializeField] private TMP_Text blackScore;
    [SerializeField] private TMP_Text whiteScore;

    [SerializeField] private GameObject gameEndPanel;
    [SerializeField] private TMP_Text gameResultText;
    [SerializeField] private Button retryButton;

    [SerializeField] private Toggle blackAiToggle;
    [SerializeField] private Toggle whiteAiToggle;

    private const int Size = 8;
    private DiskColor[][] _board;
    private Disk[][] _disks;
    private Transform[][] _tiles;
    private Tile[][] _tileObjects;

    private static readonly int[] Dx = { -1, -1, -1,  0, 0,  1, 1, 1 };
    private static readonly int[] Dy = { -1,  0,  1, -1, 1, -1, 0, 1 };

    private int _blackScore;
    private int _whiteScore;
    private DiskColor _currentColor = DiskColor.Black;
    private bool _isAiRunning = false;

    void Start()
    {
        gameEndPanel.SetActive(false);
        retryButton.onClick.AddListener(() => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
        blackAiToggle.onValueChanged.AddListener(isOn => OnAiToggleChanged(isOn, DiskColor.Black));
        whiteAiToggle.onValueChanged.AddListener(isOn => OnAiToggleChanged(isOn, DiskColor.White));

        const int start = Size / 2 - Size;
        const int end = Size / 2;

        _board = new DiskColor[Size][];
        _disks = new Disk[Size][];
        _tiles = new Transform[Size][];
        _tileObjects = new Tile[Size][];
        for (int x = 0; x < Size; x++)
        {
            _board[x] = new DiskColor[Size];
            _disks[x] = new Disk[Size];
            _tiles[x] = new Transform[Size];
            _tileObjects[x] = new Tile[Size];
            for (int y = 0; y < Size; y++)
                _board[x][y] = DiskColor.None;
        }

        for (int x = start; x < end; x++)
        {
            for (int z = start; z < end; z++)
            {
                Tile tile = Instantiate(tilePrefab, transform);
                tile.transform.position = new Vector3(x, 0, z);
                int bx = x - start;
                int bz = z - start;
                tile.Init(this, bx, bz);
                _tiles[bx][bz] = tile.transform;
                _tileObjects[bx][bz] = tile;
            }
        }

        // 初期配置
        PlaceDisk(3, 3, DiskColor.White);
        PlaceDisk(3, 4, DiskColor.Black);
        PlaceDisk(4, 3, DiskColor.Black);
        PlaceDisk(4, 4, DiskColor.White);

        InitTurnUI();
        UpdateHints();
        TriggerAiIfNeeded();
    }

    private void PlaceDisk(int x, int y, DiskColor color)
    {
        Transform tile = _tiles[x][y];
        _board[x][y] = color;
        Disk disk = Instantiate(diskPrefab, tile);
        disk.transform.position = new Vector3(tile.position.x, tile.position.y + 100.0f, tile.position.z);
        disk.SetColor(color == DiskColor.Black);
        _disks[x][y] = disk;
    }

    // 人間（Tile クリック）からの着手。AI 手番中は無視。
    public void SetDisk(int x, int y, Transform tile)
    {
        if (IsAiTurn()) return;
        Place(x, y);
    }

    private void Place(int x, int y)
    {
        if (!IsValidMove(x, y, _currentColor)) return;

        ClearAllHints();

        Transform tile = _tiles[x][y];
        _board[x][y] = _currentColor;

        Disk disk = Instantiate(diskPrefab, tile);
        disk.transform.position = new Vector3(tile.position.x, tile.position.y + 100.0f, tile.position.z);
        disk.SetColor(_currentColor == DiskColor.Black);
        _disks[x][y] = disk;

        int flipped = FlipDisks(x, y, _currentColor);

        AddScore(_currentColor, flipped);

        SwitchTurn();
    }

    private bool IsAiTurn()
        => _currentColor == DiskColor.Black ? blackAiToggle.isOn : whiteAiToggle.isOn;

    private void OnAiToggleChanged(bool isOn, DiskColor color)
    {
        if (!isOn || _currentColor != color) return;
        UpdateHints(); // AI手番なのでヒントがクリアされる
        TriggerAiIfNeeded();
    }

    private void TriggerAiIfNeeded()
    {
        if (!HasAnyValidMove(_currentColor)) return;
        if (!IsAiTurn()) return;
        if (_isAiRunning) return;
        StartCoroutine(AiMove());
    }

    private IEnumerator AiMove()
    {
        _isAiRunning = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL は探索がメインスレッドを止めるため、直前の着手アニメを見せてから
        // 1 秒後に探索へ入ることで、カクつきを「考え中」の間に隠す。
        yield return new WaitForSeconds(1.0f);
#else
        yield return new WaitForSeconds(0.5f);
#endif

        var board = CloneBoard();
        int total = CountTotalDisks();
        DiskColor color = _currentColor;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL はマルチスレッド非対応（スレッドプールが無く Task.Run のタスクが
        // 実行されない）ため、メインスレッドで同期実行する。
        var (x, y) = OthelloAI.GetBestMove(board, color, total, 5);
        _isAiRunning = false;
#else
        // デスクトップ/エディタでは探索をスレッドプールで実行し、
        // 完了までフレームを回しながら待つ（GetBestMove は Unity 非依存でスレッドセーフ）。
        Task<(int x, int y)> search = Task.Run(() => OthelloAI.GetBestMove(board, color, total, 5));

        while (!search.IsCompleted)
            yield return null;

        _isAiRunning = false;

        if (search.IsFaulted)
        {
            Debug.LogException(search.Exception);
            yield break;
        }

        var (x, y) = search.Result;
#endif
        if (x >= 0) Place(x, y);
    }

    private DiskColor[][] CloneBoard()
    {
        var copy = new DiskColor[Size][];
        for (int i = 0; i < Size; i++)
            copy[i] = (DiskColor[])_board[i].Clone();
        return copy;
    }

    // ターン開始時 UI を初期化
    private void InitTurnUI()
    {
        blackTurnUI.SetActive(_currentColor == DiskColor.Black);
        whiteTurnUI.SetActive(_currentColor == DiskColor.White);
    }

    // 各マスに「返せる枚数」を表示。AI手番中は非表示。
    private void UpdateHints()
    {
        if (IsAiTurn())
        {
            ClearAllHints();
            return;
        }

        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
            {
                int count = CountFlips(x, y, _currentColor);
                if (count > 0)
                    _tileObjects[x][y].ShowHint(count);
                else
                    _tileObjects[x][y].ClearHint();
            }
    }

    private void ClearAllHints()
    {
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                _tileObjects[x][y].ClearHint();
    }

    // 指定マスに置いたとき返せる枚数を返す（置けない場合は 0）
    private int CountFlips(int x, int y, DiskColor color)
    {
        if (_board[x][y] != DiskColor.None) return 0;

        DiskColor opponent = color == DiskColor.Black ? DiskColor.White : DiskColor.Black;
        int total = 0;

        for (int d = 0; d < 8; d++)
        {
            int nx = x + Dx[d];
            int ny = y + Dy[d];
            int count = 0;

            while (nx is >= 0 and < Size && ny is >= 0 and < Size && _board[nx][ny] == opponent)
            {
                nx += Dx[d];
                ny += Dy[d];
                count++;
            }

            if (count > 0 && nx is >= 0 and < Size && ny is >= 0 and < Size && _board[nx][ny] == color)
                total += count;
        }

        return total;
    }

    // 得点計算：一手で返した枚数が多いほど、1枚あたりの価値が上がる「大返しボーナス」。
    // 返した枚数 flipped に対して 1+2+...+flipped（三角数）を加点する。
    // 例) 1枚→1, 2枚→3, 3枚→6, 4枚→10, 5枚→15, 6枚→21
    private void AddScore(DiskColor color, int flipped)
    {
        if (flipped <= 0) return;

        int points = flipped * (flipped + 1) / 2;

        if (color == DiskColor.Black)
        {
            int prev = _blackScore;
            _blackScore += points;
            StartCoroutine(CountUp(blackScore, prev, _blackScore));
        }
        else
        {
            int prev = _whiteScore;
            _whiteScore += points;
            StartCoroutine(CountUp(whiteScore, prev, _whiteScore));
        }
    }

    private int CountTotalDisks()
    {
        int count = 0;
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                if (_board[x][y] != DiskColor.None) count++;
        return count;
    }

    private IEnumerator CountUp(TMP_Text text, int from, int to, float duration = 0.6f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.text = Mathf.RoundToInt(Mathf.Lerp(from, to, elapsed / duration)).ToString();
            yield return null;
        }
        text.text = to.ToString();
    }

    // 置けるかどうかを判定：CountFlips > 0 と等価
    private bool IsValidMove(int x, int y, DiskColor color)
        => CountFlips(x, y, color) > 0;

    // はさみうちの処理：8方向で挟んだ相手の石をすべてひっくり返す。返した枚数を返す
    private int FlipDisks(int x, int y, DiskColor color)
    {
        DiskColor opponent = color == DiskColor.Black ? DiskColor.White : DiskColor.Black;
        int flipped = 0;

        for (int d = 0; d < 8; d++)
        {
            int nx = x + Dx[d];
            int ny = y + Dy[d];
            var toFlip = new List<(int fx, int fy)>();

            while (nx is >= 0 and < Size && ny is >= 0 and < Size && _board[nx][ny] == opponent)
            {
                toFlip.Add((nx, ny));
                nx += Dx[d];
                ny += Dy[d];
            }

            if (toFlip.Count > 0 && nx is >= 0 and < Size && ny is >= 0 and < Size && _board[nx][ny] == color)
            {
                foreach (var (fx, fy) in toFlip)
                {
                    _board[fx][fy] = color;
                    _disks[fx][fy].Flip(color == DiskColor.Black);
                }
                flipped += toFlip.Count;
            }
        }

        return flipped;
    }

    private void SwitchTurn()
    {
        _currentColor = _currentColor == DiskColor.Black ? DiskColor.White : DiskColor.Black;

        if (_currentColor == DiskColor.Black)
        {
            blackTurnUI.SetActive(true);
            whiteTurnUI.SetActive(false);
        }
        else
        {
            blackTurnUI.SetActive(false);
            whiteTurnUI.SetActive(true);
        }

        UpdateHints();

        if (!HasAnyValidMove(_currentColor))
        {
            ShowGameEnd();
            return;
        }

        TriggerAiIfNeeded();
    }

    private bool HasAnyValidMove(DiskColor color)
    {
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                if (CountFlips(x, y, color) > 0) return true;
        return false;
    }

    private void ShowGameEnd()
    {
        int blackDisks = 0, whiteDisks = 0;
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
            {
                if (_board[x][y] == DiskColor.Black) blackDisks++;
                else if (_board[x][y] == DiskColor.White) whiteDisks++;
            }

        string winner = _blackScore > _whiteScore ? "Black WIN!!"
                      : _whiteScore > _blackScore ? "White WIN!!"
                      : "Draw!!";

        gameResultText.text = $"{winner}\n\n{_blackScore} - {_whiteScore}\n{blackDisks} - {whiteDisks}";
        blackTurnUI.SetActive(false);
        whiteTurnUI.SetActive(false);
        gameEndPanel.SetActive(true);
    }
}
